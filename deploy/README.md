# deploy/

The source of truth for the cluster. This folder is what ArgoCD reconciles, and
it is the only thing that changes the running stack: CI builds images and writes
an image tag here, and Argo does the rest. No workflow runs `kubectl apply`.

## Layout

| Folder             | Reconciles                                             | Image                     |
| ------------------ | ------------------------------------------------------ | ------------------------- |
| `cluster/`         | the namespace, GatewayClass, Gateway and HTTPRoute      | —                         |
| `migrate/`         | the one-shot schema migration, as a PreSync hook Job    | `mgo2-postgres`           |
| `gate/`            | the gate lobby                                          | `mgo2-gate-lobby-server`  |
| `account/`         | the account lobby                                       | `mgo2-account-lobby-server` |
| `game-lobby/`      | the nine lobby roles                                    | `mgo2-game-lobby-server`  |
| `gameplay/`        | the gameplay host                                       | `mgo2-gameplay-server`    |
| `http/`            | the HTTP API                                            | `mgo2-http`               |
| `dns/`             | the name server                                         | `mgo2-dns`                |
| `stun/`            | the port-check responder                                | `mgo2-stun`               |
| `argocd/`          | the Application objects themselves, not the workloads    | —                         |

Each service folder is a kustomization with exactly one `images:` entry, and
that entry's `newTag` is the only thing a deploy changes there. Nine lobby
deployments share one image, so they share one tag and move together.

## How a change reaches the cluster

1. A commit lands on `main` and touches a service, a project it references, or
   something every image is built from.
2. That service's pipeline decides whether it is genuinely affected
   (`dotnet affected`, see `docs/CICD.md`), then builds, tests and publishes an
   image tagged with the commit SHA.
3. The pipeline's last step rewrites `newTag` in that service's
   `kustomization.yaml` and commits it. That is the whole deploy: one line in
   one folder, on `main`. The `migrate` folder is the one exception: it also
   carries `bundle.yaml`, the marker ConfigMap the `mgo2-migrate` Application
   compares, and the pipeline rewrites its digest in the same commit. The
   marker is what makes the Application sync and run the PreSync hook — the
   pin alone does not — so a marker that lags the pin is a migration that
   never runs, and a release serving a schema its code was never migrated to.
4. ArgoCD notices the commit and syncs the Application for that folder. Sync is
   automated and self-healing, so the folder and the cluster cannot drift.

Nothing in that sequence reads anything a previous run wrote. A run that failed
a week ago cannot stop this one, and two services never wait on each other.

## Bootstrap

ArgoCD is installed once, on the cluster, and then applies itself from here.

```sh
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# The one Application that is applied by hand. Everything else is its child.
kubectl apply -f deploy/argocd/root.yaml
```

`deploy/argocd/root.yaml` points at `deploy/argocd/applications`, whose children
are ordered by sync wave:

| Wave | Application  | Why                                                          |
| ---- | ------------ | ------------------------------------------------------------ |
| 0    | `mgo2-cluster` | the namespace and Gateway API resources everything needs    |
| 1    | `mgo2-migrate` | the migration hook, which has to finish before anything rolls out |
| 2    | the services | the workloads, none of which may roll out before the schema is ready |

Two things are prerequisites and are deliberately **not** in this folder,
because they carry environment-specific values that do not belong in git:

- the `mgo2-secrets` Secret, which every deployment and the migration Job take
  their whole environment from;
- the `mgo2-appsettings` ConfigMap, which every deployment mounts as
  `/app/appsettings.json`.

Create them before the first sync, or the pods will not start.

### Reloader

Both of those carry values that do not belong in this folder, so nothing Argo
reconciles can connect a change in them to the pods that mount them. Reloader
does: it watches ConfigMaps and Secrets and rolls the workloads that name them.

It is installed on the cluster once, the way ArgoCD itself is, and it is pinned
to a version for the same reason an image tag is:

```sh
helm repo add stakater https://stakater.github.io/stakater-charts
helm upgrade --install reloader stakater/reloader \
  --namespace reloader --create-namespace \
  --version 2.2.17 \
  --set reloader.reloadStrategy=annotations
```

`reloadStrategy=annotations` is not cosmetic. The default injects a hash of the
ConfigMap into the container's `env`, and the only `ignoreDifferences` that would
cover it is the whole `env` list — which would also stop Argo correcting every
variable this folder declares. The annotations strategy writes a single
annotation into the pod template instead, which an Application can ignore by
name without blinding itself to anything else.

### The seed tag

Each kustomization's `images:` entry is seeded with `newTag: latest`, because
the tag that is right for a service is the commit that last built it, and until
the pipeline has run once there is no such commit to write.

**Do not leave a `latest` in place.** While a folder still reads `latest`, that
service's deploy is not reproducible and Argo will happily redeploy whatever the
registry's `latest` points at. The pipeline replaces it with a commit SHA on its
first run — which is why the recommended order is: merge this change, let the
pipelines pin every folder, and only then apply `root.yaml`.

## Migrations

The migration Job in `migrate/job.yaml` is an Argo **PreSync** hook. Argo runs
every hook before it applies the rest of an Application's resources, and it does
not start the rollout until the hook has succeeded, so a failed migration leaves
the previous revision serving.

The schema rules the migration has to obey:

1. **Never migrate at application start.** Several replicas would race the same
   `Database.Migrate()` on rollout. The servers cannot migrate at all — the
   bundle lives in the `mgo2-postgres` image, which is never deployed as a
   workload.
2. **The bundle is versioned with the commit that built it.** `mgo2-postgres`
   carries the EF Core bundle built from that revision, and `deploy/migrate/`
   pins the image tag, so the schema a release applies is the schema its code
   was built against.
3. **Expand, then contract.** During a rollout the old and new pod versions run
   side by side, so a schema change and the code that needs it cannot land in
   the same step if the old code would break:
   - *expand* — add the column, table or index. Nullable, or defaulted. Deploy.
   - *migrate the code* — read and write the new shape. Deploy.
   - *contract* — drop what no running version still needs. A later deploy.
4. **One database per service, and its migrations stay in that service's
   pipeline.** No service reads another's tables, so no migration here has to
   coordinate with another service's release.

## Changing the ConfigMap

Editing `mgo2-appsettings` does nothing on its own. Argo has no view of it, every
mount is a `subPath` that kubelet never refreshes, and each server reads its
options once at startup — so a pod only takes a new file by being replaced.

Reloader is what replaces it. Write the change and wait:

```sh
kubectl create configmap mgo2-appsettings --namespace mgo2 \
  --from-file=appsettings.json=appsettings.json \
  --dry-run=client --output yaml | kubectl apply --filename -
```

Each workload rolls if, and only if, its Deployment names the ConfigMap:

```yaml
configmap.reloader.stakater.com/reload: "mgo2-appsettings"
```

All fifteen do, so a change to any value is a rollout of the whole stack — the
nine lobbies included, and a lobby with players in it closes its listener,
refuses anyone new and waits for the players it has, up to its six hour grace
period. The trigger is the ConfigMap rather than the setting, so a key only the
name server reads still restarts the lobbies. Splitting the ConfigMap per service
is what narrows that.

Three things worth knowing:

- **Whitespace counts.** Reloader hashes the ConfigMap's data, so reindenting the
  document rolls the stack exactly as a changed value does.
- **The annotation sits on the Deployment's metadata, not its pod template.**
  Adding it, or moving it, rolls nothing by itself.
- **Reloader restarts by writing into the pod template**, which is a manifest
  Argo owns. Every Application holding a workload therefore ignores the one path
  it writes, and sets `RespectIgnoreDifferences=true` so a sync cannot strip it
  again. The pointer names the annotation rather than the pod template, so Argo
  still corrects everything else about it.

## Source addresses and the load balancer

A pod behind a proxying load balancer sees the load balancer's address, not the
console's. `TcpServerBase` reads the address off the accepted socket and puts it
in `TcpSession.RemoteAddress`, so the question is not how to read it but whether
anything has already rewritten it. The answer differs per service, and the three
cases below are not interchangeable.

### TCP — `externalTrafficPolicy: Local`

Every TCP Service here sets `externalTrafficPolicy: Local`. With the default
`Cluster`, kube-proxy masquerades the traffic and the pod sees the load
balancer. With `Local`, a node forwards only to its own pods and does not
masquerade, so `RemoteAddress` is the console's real address. The Kubernetes
reference states the trade exactly: *preserves the source IP … by routing only to
endpoints on the same node … (dropping the traffic if there are no local
endpoints).*

That is the whole cost, and it is a real one. So each Local Service's Deployment
carries what makes the drop window as small as it can be made:

| Setting | What it prevents |
| --- | --- |
| `readinessProbe` (`tcpSocket`) | The pod is Ready before it has bound, so the LB sends to a listener that is not there yet. **The most important one.** |
| `maxUnavailable: 0` | The rollout taking the ready count below the serving count. |
| `maxSurge: 1` | Nothing. `25%` of one replica rounds to zero, so the surge a Local rollout depends on would not happen at all. |
| `minReadySeconds: 10` | The old pod dying before the LB has health-checked the new pod's node. |
| `topologySpreadConstraints` | Replicas sharing a node, and so sharing its exposure. |

`healthCheckNodePort` is allocated automatically and is what tells the LB to stop
sending to a node with no local endpoint; the OVHcloud CCM wires up the
health monitor for it when the policy is `Local`.

Two things this does **not** give you, both from the upstream sources rather than
from here:

- **It narrows the race; it does not close it.** The window between the last pod
  on a node terminating and the LB noticing still exists. Kubernetes 1.26's
  ProxyTerminatingEndpoints closes most of it — kube-proxy routes to terminating
  pods by readiness and fails the health check port when a node has only
  terminating pods — so **the cluster must be 1.26 or newer** for this to hold.
- **Replicas still need to fit the nodes.** With one replica, a node failure is
  an outage, and `Local` does not change that. `topologySpreadConstraints` only
  helps once there is more than one.

Verify the return path once, on a real console, before trusting it: with `Local`
the pod replies to the console directly rather than through the load balancer,
and the AWS equivalent has a documented history of asymmetric-routing failures
that look exactly like an application bug.

### UDP gameplay and the name server — `Cluster`, deliberately

Neither needs the console's address. Gameplay relays, and `docs/STUN.md` traces
the peer descriptor as built **client-side** at `0x9444BC` from the port check
result — so the server never has to see the raw address for peer to peer to work.
The name server answers queries and has no use for it either.

### The port check — not behind a load balancer at all

`stun/` is the exception, and it is the reason the other two cases can be simple.
MAPPED-ADDRESS is how the console learns its own public address, and a proxying
load balancer hands the datagram over on a port of its own — so the responder
would observe the load balancer as the console, every player would be given the
same address, and peer to peer would fail **silently**, as the "Adjusting port
settings" hang `docs/STUN.md` describes.

`externalTrafficPolicy: Local` does not rescue it. The pod would see the right
address, but its reply would leave from the pod rather than from the address the
console dialled, and the console identifies the responder by where an answer came
from. So the responder runs on the node's own network instead:

- `hostNetwork: true`, so the datagram arrives with the console's real address
  and port and the reply leaves from the address the console dialled;
- `dnsPolicy: ClusterFirstWithHostNet`, without which it loses cluster DNS;
- a `nodeSelector` on `mgo2.io/stun-node`, because the node is chosen by whoever
  owns the public address, not by the scheduler;
- `type: Recreate`, not `RollingUpdate`: the pod owns the host's ports, so a surge
  would start the replacement while the old pod still holds 3478.

Two consequences worth stating plainly:

- **The console is reached by a DNS A record pointing at that node's public IP.**
  There is no load balancer, so there is nothing to hand out a stable address.
- **There is no invisible failover.** The pod rescheduling onto another labelled
  node changes the address the console is told, so the A record has to change
  with it. A second responder needs a second address and a second record. A
  health check cannot fix this, because there is no load balancer left to ask.

`STUN_SECONDARY_ADDRESS` is the second address a change-address request is
answered from, and without one a restricted-cone NAT is read as a full-cone one
— which passes the console's check and then fails to connect, so it is a worse
failure than it looks. It has to be a second address of the same node
(`ip addr add …`), which does not survive a reboot and so wants a node bootstrap
script rather than a manifest here.

## Rolling back

Point `newTag` at the previous commit's SHA and commit it. That is the entire
operation, and it works because every image the cluster has ever run is still
pinned to the commit that produced it.

For a schema change, rolling the image back is not enough: migrations are
forward-only. The rollback for a breaking change is the next migration — which
is the reason for the expand/contract rule above, rather than a down script.

## What is not here

- **No `latest` in a tag Argo deploys.** The registry may still carry `latest`
  for convenience; nothing in this folder names it.
- **No CI step that applies anything.** The pipelines stop at a commit.
- **No `kubectl.kubernetes.io/restartedAt` annotations.** They used to be how a
  rollout was forced from the outside; changing a tag now does that, and a
  static annotation would only be drift. The one annotation written into a pod
  template is the exception, and it is ignored by name in every Application that
  holds a workload — see [Changing the ConfigMap](#changing-the-configmap).
- **No load balancer in front of the port check**, and none to be added — see
  [Source addresses and the load balancer](#source-addresses-and-the-load-balancer).

## Checking this folder

`tools/check_deploy_source_addresses.py` parses every manifest and asserts
the invariants the source-address decisions depend on: each Service's traffic
policy matches what its protocol allows, each Local Service has the probe and
rollout settings that make `Local` survivable, and the port check is host-networked
on a chosen node.

It exists because there is no `kubectl` in the loop to say so, and because one of
these files briefly carried two `dnsPolicy` keys — PyYAML takes the last silently,
so the wrong one would have taken effect and nothing would have reported it. The
loader it uses rejects a repeated key for that reason.

```sh
python tools/check_deploy_source_addresses.py
```

Wire it into the pipeline if these manifests ever stop being reviewed by a human
who already knows all this.
