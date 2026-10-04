#!/usr/bin/env python3
"""Check the host rule that pins the gameplay reply's source address and port.

This rule is a POD-NETWORK-ONLY workaround, and the gameplay host does not run
on the pod network. On `hostNetwork` there is no flannel masquerade and no rule
is needed, so this exits 0 with a note rather than failing.

Why the pod network needed it at all -- note that it is NOT the socket. An
earlier version of this file claimed the pod's socket was "bound but never
connected, so Linux picks an ephemeral source port for every send". That is
wrong: a UDP socket that is `bind()`-ed but never `connect()`-ed sends from its
bound port, and the kernel only picks an ephemeral port for a socket that has
never been bound. `GameplayServerService` binds its `UdpClient` to an explicit
endpoint, so its replies already leave from the gameplay port.

The real culprit was flannel:

    -A FLANNEL-POSTRTG -s 10.42.0.0/16 ... -j MASQUERADE --random-fully

`--random-fully` rewrites the source port of anything leaving the pod CIDP, so
a correct reply port was randomized on the way out and the console -- which
matches every inbound datagram against the single `ip:port` it dialled --
discarded them all. It re-handshakes forever and logs nothing on either side.

So: on the pod network this rule is necessary and its absence is a silent,
total failure of joining. On `hostNetwork` it is correctly absent.

Usage:
    python tools/check_gameplay_reply_source.py [--ssh HOST] [--topology auto]

    --topology  auto      read deploy/gameplay/deployment.yaml (default)
                   host    assert the rule is absent, as hostNetwork requires
                   pod     assert the rule is present, correct and persistent

Exit status is 0 when the topology's requirement is met, 1 when it is not.
"""
from __future__ import annotations

import argparse
import pathlib
import re
import subprocess
import sys

try:
    import yaml
except ImportError:  # pragma: no cover - PyYAML is a repo dependency
    yaml = None

GAMEPLAY_DEPLOYMENT = pathlib.Path("deploy/gameplay/deployment.yaml")

RULE_FILE = pathlib.Path("/etc/nftables.d-mgo2-gameplay.nft")
UNIT_FILE = pathlib.Path("/etc/systemd/system/mgo2-gameplay-nft.service")
TABLE = "ip mgo2_gameplay"

# The rule the console's session filter needs. The destination port is the
# selector because it is the port the console dialled; the source port is what
# flannel's masquerade randomises, so conditioning on it is the bug this
# replaces.
EXPECTED_RULE = re.compile(
    r"ip saddr (?P<pod_cidr>[\d./]+)\s+"
    r"ip daddr (?P<client>[\d.]+)\s+"
    r"udp dport (?P<dport>\d+)\s+"
    r"snat to (?P<snatted>[\d.]+):(?P<sport>\d+)"
)

# Conditioning on the source port matches only the case where the pod happens to
# emit from the gameplay port, and lets every other reply through to the
# randomising masquerade. The console then rejects those, silently.
SOURCE_PORT_MATCH = re.compile(r"udp sport \d+")

REQUIRED_UNIT_DIRECTIVES = (
    "WantedBy=multi-user.target",
    "ExecStart=",
    "Before=k3s.service",
)


def run(ssh_host: str | None, *args: str) -> subprocess.CompletedProcess[str]:
    """Runs a command, over ssh when a host is given."""
    command = ["ssh", ssh_host, *args] if ssh_host else list(args)
    return subprocess.run(command, capture_output=True, text=True)


def check_rule_file(ssh_host: str | None) -> str | None:
    """Returns the single rule the file declares, or why it is wrong."""
    result = run(ssh_host, f"sudo cat {RULE_FILE.as_posix()}")
    if result.returncode != 0:
        return f"{RULE_FILE} is not readable: {result.stderr.strip()}"

    rules = [line.strip() for line in result.stdout.splitlines() if "snat to" in line]
    if len(rules) != 1:
        return (
            f"{RULE_FILE} declares {len(rules)} snat rules, expected exactly 1. "
            "More than one means an edit was reloaded without deleting the table "
            "first, because `nft -f` adds to an existing table."
        )

    rule = rules[0]
    if SOURCE_PORT_MATCH.search(rule):
        return (
            f"{RULE_FILE} matches on the source port, which the masquerade is what "
            f"randomises: {rule}"
        )

    if not EXPECTED_RULE.search(rule):
        return f"{RULE_FILE} does not match the shape the console needs: {rule}"

    return None


def check_loaded(ssh_host: str | None) -> str | None:
    """Returns why the live table is wrong, or None when it is correct."""
    result = run(ssh_host, f"sudo nft list table {TABLE}")
    if result.returncode != 0:
        return f"table {TABLE} is not loaded: {result.stderr.strip()}"

    rules = [line.strip() for line in result.stdout.splitlines() if "snat to" in line]
    if len(rules) != 1:
        return f"table {TABLE} holds {len(rules)} snat rules, expected exactly 1"

    if SOURCE_PORT_MATCH.search(rules[0]):
        return f"the live rule matches on the randomised source port: {rules[0]}"

    if not EXPECTED_RULE.search(rules[0]):
        return f"the live rule is not the expected shape: {rules[0]}"

    return None


def check_persistence(ssh_host: str | None) -> str | None:
    """Returns why the rule is lost on reboot, or None when it survives."""
    result = run(ssh_host, f"sudo cat {UNIT_FILE.as_posix()}")
    if result.returncode != 0:
        return (
            f"{UNIT_FILE} is missing, so the rule is lost on reboot. Without it the "
            "next restart silently reintroduces a join that hangs with no error."
        )

    for directive in REQUIRED_UNIT_DIRECTIVES:
        if directive not in result.stdout:
            return f"{UNIT_FILE} is missing {directive!r}"

    enabled = run(ssh_host, "sudo systemctl is-enabled mgo2-gameplay-nft.service")
    if enabled.stdout.strip() != "enabled":
        return f"mgo2-gameplay-nft.service is {enabled.stdout.strip()}, expected enabled"

    return None


def check_absent(ssh_host: str | None) -> str | None:
    """Returns why a rule hostNetwork makes unnecessary is present, or None.

    A leftover rule is not a neutral thing here. It runs at `srcnat - 10` and
    SNATs replies to a fixed address and port, so on a host-networked pod it
    would rewrite a reply that is already correct, back out of the address the
    console dialled.
    """
    loaded = run(ssh_host, f"sudo nft list table {TABLE}")
    if loaded.returncode == 0:
        return (
            f"table {TABLE} is loaded, but the pod is host-networked so nothing can "
            f"randomise its reply and this rule only rewrites a correct one. "
            f"Remove it: sudo nft delete table {TABLE}"
        )

    if run(ssh_host, f"sudo test -f {RULE_FILE.as_posix()}").returncode == 0:
        return (
            f"{RULE_FILE} exists for a host-networked pod that does not need it, and "
            f"the systemd unit loads it at boot, so it returns after every reboot "
            f"until it is deleted."
        )

    return None


def detect_topology() -> str | None:
    """Reads the topology from the manifest, or None when it cannot be read."""
    if yaml is None or not GAMEPLAY_DEPLOYMENT.exists():
        return None
    with GAMEPLAY_DEPLOYMENT.open(encoding="utf-8") as handle:
        document = yaml.safe_load(handle)
    template = document["spec"]["template"]["spec"]
    return "host" if template.get("hostNetwork") else "pod"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ssh", help="ssh host to check, default is this machine")
    parser.add_argument(
        "--topology",
        choices=("auto", "host", "pod"),
        default="auto",
        help="auto reads deploy/gameplay/deployment.yaml (default)",
    )
    arguments = parser.parse_args()

    topology = detect_topology() if arguments.topology == "auto" else arguments.topology
    if topology is None:
        print(
            f"FAIL: could not read the topology from {GAMEPLAY_DEPLOYMENT}. "
            f"Pass --topology host or --topology pod to say which applies."
        )
        return 1

    if topology == "host":
        failure = check_absent(arguments.ssh)
        if failure:
            print(f"FAIL: {failure}")
            return 1
        print(
            "PASS: the gameplay pod is host-networked, so its reply source needs no "
            "pin. The flannel --random-fully masquerade this rule exists to counteract "
            "only applies to the pod CIDP, and this pod is not in it."
        )
        return 0

    failures = [
        failure
        for failure in (
            check_rule_file(arguments.ssh),
            check_loaded(arguments.ssh),
            check_persistence(arguments.ssh),
        )
        if failure
    ]

    for failure in failures:
        print(f"FAIL: {failure}")
    if failures:
        return 1

    print("PASS: the gameplay reply source is pinned and survives a reboot")
    return 0


if __name__ == "__main__":
    sys.exit(main())
