"""Checks deploy/ against the invariants the source-IP and rollout changes rely on.

There is no kubectl on this machine and no cluster to ask, so this reads the
manifests and asserts the things that would otherwise only fail on a sync: every
file parses, every Service that says Local actually says Local, every Deployment
whose Service says Local has the probe and the rollout settings that make Local
safe, and the port-check responder is not behind a load balancer at all.

Run from the repository root:

    python tools/check_deploy_source_addresses.py
"""

import pathlib
import sys

import yaml

DEPLOY = pathlib.Path("deploy")

# A Service that preserves the source IP is one whose pods must be Ready before
# the load balancer sends them anything, so its Deployment owes a probe and a
# rollout that never goes below the ready count.
failures: list[str] = []


def fail(message: str) -> None:
    failures.append(message)


class DuplicateKeyLoader(yaml.SafeLoader):
    """A loader that refuses a mapping with the same key twice.

    A repeated key is not an error to PyYAML: the last one silently wins. That is
    how a deployment ended up carrying both `dnsPolicy: ClusterFirstWithHostNet`
    and a later `dnsPolicy: ClusterFirst`, with the wrong one taking effect and
    nothing reporting it.
    """


def _reject_duplicate_keys(loader: yaml.SafeLoader, node: yaml.MappingNode, deep: bool = False) -> dict:
    mapping = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node, deep=deep)
        if key in mapping:
            raise yaml.constructor.ConstructorError(
                "while constructing a mapping",
                node.start_mark,
                f"found duplicate key {key!r}",
                key_node.start_mark,
            )
        mapping[key] = loader.construct_object(value_node, deep=deep)
    return mapping


DuplicateKeyLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, _reject_duplicate_keys
)


def load(path: pathlib.Path) -> dict:
    with path.open(encoding="utf-8") as handle:
        return yaml.load(handle, Loader=DuplicateKeyLoader)


def check_parses() -> list[pathlib.Path]:
    """Every manifest parses. Returns the files that did."""
    parsed = []
    for path in sorted(DEPLOY.rglob("*.yaml")):
        try:
            load(path)
            parsed.append(path)
        except yaml.YAMLError as error:
            fail(f"{path}: does not parse: {str(error).splitlines()[0]}")
    return parsed


def check_traffic_policies(parsed: list[pathlib.Path]) -> None:
    """Each Service's policy matches what its protocol allows."""
    for path in parsed:
        document = load(path)
        if not isinstance(document, dict) or document.get("kind") != "Service":
            continue

        spec = document["spec"]
        name = document["metadata"]["name"]
        policy = spec.get("externalTrafficPolicy")
        kind = spec.get("type")
        protocols = {port.get("protocol", "TCP") for port in spec.get("ports", [])}

        if name == "mgo2-stun":
            # The port check cannot be proxied at all: MAPPED-ADDRESS is how the
            # console learns its own public address, and a proxying load balancer
            # makes every player look like the load balancer.
            if kind != "ClusterIP":
                fail(f"{path}: the port check must not be a {kind}; see its header comment")
            if spec.get("clusterIP") != "None":
                fail(f"{path}: the port check Service must be headless, or it proxies")
            continue

        if "UDP" in protocols:
            # Neither the gameplay relay nor the name server needs the client's
            # address, and both are proxied without harm: there is no reflexive
            # mapping to get wrong. The port check is the only UDP service with a
            # reason to care, and it is handled above.
            if policy == "Local":
                fail(f"{path}: {name} is UDP and gains nothing from Local")
            continue

        if kind == "LoadBalancer" and policy != "Local":
            fail(
                f"{path}: {name} is {kind}/{policy}, so its pods see the load "
                f"balancer's address rather than the console's"
            )


def check_deployments(parsed: list[pathlib.Path]) -> None:
    """Each Local Service has a Deployment that can survive being Local."""
    local_names = set()
    for path in parsed:
        document = load(path)
        if not isinstance(document, dict) or document.get("kind") != "Service":
            continue
        if document["spec"].get("externalTrafficPolicy") == "Local":
            local_names.add(document["metadata"]["name"])

    for path in parsed:
        document = load(path)
        if not isinstance(document, dict) or document.get("kind") != "Deployment":
            continue

        name = document["metadata"]["name"]
        if name not in local_names:
            continue

        spec = document["spec"]
        strategy = spec.get("strategy", {})
        rolling = strategy.get("rollingUpdate", {})

        # 25% of one replica rounds to zero, so the surge never happens and the
        # old pod is killed before the new one can serve.
        if rolling.get("maxUnavailable") not in (0, "0"):
            fail(f"{path}: {name} is Local but maxUnavailable is {rolling.get('maxUnavailable')!r}")
        if rolling.get("maxSurge") not in (1, "1"):
            fail(f"{path}: {name} is Local but maxSurge is {rolling.get('maxSurge')!r}")
        if spec.get("minReadySeconds", 0) < 1:
            fail(f"{path}: {name} is Local but has no minReadySeconds to cover the LB health check")

        template = spec["template"]["spec"]
        spread = template.get("topologySpreadConstraints", [])
        if not any(
            constraint.get("topologyKey") == "kubernetes.io/hostname" for constraint in spread
        ):
            fail(f"{path}: {name} is Local but does not spread across nodes")

        for container in template.get("containers", []):
            if "readinessProbe" not in container:
                fail(
                    f"{path}: {name} is Local and container {container.get('name')!r} has no "
                    f"readinessProbe, so it is Ready before it binds its port"
                )


def check_port_check_isolation(parsed: list[pathlib.Path]) -> None:
    """The port-check responder is on a node's own network, on a chosen node."""
    for path in parsed:
        document = load(path)
        if not isinstance(document, dict) or document.get("kind") != "Deployment":
            continue
        if document["metadata"]["name"] != "mgo2-stun":
            continue

        template = document["spec"]["template"]["spec"]
        if not template.get("hostNetwork"):
            fail(f"{path}: the port check responder must run with hostNetwork")
        if template.get("dnsPolicy") != "ClusterFirstWithHostNet":
            fail(f"{path}: hostNetwork without ClusterFirstWithHostNet loses cluster DNS")
        if not template.get("nodeSelector"):
            fail(f"{path}: the responder must be pinned to the node the console dials")

        # A surge would start the replacement while the old pod still holds the
        # host's ports, and the new one could not bind.
        if document["spec"]["strategy"].get("type") == "RollingUpdate":
            fail(f"{path}: a host-networked responder cannot be rolled, it has to be recreated")

        ports = {port.get("containerPort") for port in template["containers"][0]["ports"]}
        if ports != {3478, 3479}:
            fail(f"{path}: the responder serves {sorted(ports)}, expected 3478 and 3479")


def main() -> int:
    parsed = check_parses()
    check_traffic_policies(parsed)
    check_deployments(parsed)
    check_port_check_isolation(parsed)

    if failures:
        print(f"{len(failures)} problem(s):\n")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print(f"{len(parsed)} manifests parse and every source-address invariant holds")
    return 0


if __name__ == "__main__":
    sys.exit(main())
