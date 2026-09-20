# Memory usage

Where the memory on the k3s node actually goes, and what is worth reclaiming.
Measured on **2026-09-20** on the single node `raspberrypi`, which is a Raspberry
Pi with 8 GB (8255872 Ki capacity).

## What was measured

```
Mem:   total 8062   used 5636   free 176   buff/cache 3103   available 2425
Swap:  total 2047   used  981
swapon: /dev/zram0 partition 2G used 981.1M prio 100
```

`kubectl top nodes` reports 5517 Mi / 68%, but it measures pod working sets and
excludes the page cache the kernel is using. **The `free` figures are the ones
that matter: 981 MiB of zram swap is in use**, so the node is already under real
pressure, not merely busy. zram is compressed RAM, so that swap is still
occupying the physical memory it is trying to relieve.

### Split by consumer

| Consumer | Memory | Notes |
| --- | --- | --- |
| 15 `mgo2` service pods | ~1191 Mi | Nine of them are the game-lobby roles (~856 Mi) |
| Argo CD, 7 pods | ~448 Mi | Added 2026-09-20 |
| kube-system (traefik, metrics-server, Headlamp, coredns, local-path) | ~131 Mi | |
| `svclb-*` pods (17 of them) | ~0 Mi | Service loadbalancer placeholders |

The largest single pod is `argocd-application-controller` at **206 Mi** — larger
than any application pod on the node.

Every pod in the cluster has `resources: {}` — **no requests and no limits
anywhere**, for the applications and for Argo CD's own components alike.

## The highest-value change: turn off Server GC

Every .NET service in this repository runs with **Server Garbage Collection
enabled**, which was verified inside the running containers rather than inferred:

```
$ kubectl -n mgo2 exec deploy/mgo2-http -- cat /app/*.runtimeconfig.json
  "configProperties": { "System.GC.Server": true, ... }
```

Confirmed identical in `mgo2-gate`, `mgo2-basic-training` and `mgo2-gameplay-1`,
so this is the whole fleet, not one service.

Server GC allocates a heap **per visible core and lets each grow**, and each pod
sees **4 cores** (`nproc` → 4) with no CPU limit to constrain it. That is the
right default for a throughput-bound server on a big machine; it is the wrong
trade for fifteen I/O-bound lobby processes sharing four cores on a Pi, where
each process is mostly waiting on sockets and allocating modestly.

Turning it off is one variable, no rebuild:

```yaml
env:
  - name: DOTNET_gcServer
    value: "0"
```

or permanently, in `Directory.Build.props`:

```xml
<ServerGarbageCollection>false</ServerGarbageCollection>
```

This is the change to try first, and to measure: apply it to one or two services,
record `kubectl top pod` before and after, and roll it out only if the win is
real. Expect tens of MiB per pod; across fifteen pods that is the difference
between the current footing and a comfortable one. The cost is throughput under
sustained heavy allocation, which these services do not have.

## Second: give the runtime a limit to size itself against

With no limits, .NET sizes its heaps against the *node's* memory rather than
what the pod is allowed, and the kernel has no basis for choosing a victim if
the node does run out — it may kill something arbitrary, or thrash swap as it is
doing now.

A memory limit per pod does two jobs at once: the runtime reads the cgroup limit
and sizes its heaps to it, and the node can evict one pod cleanly instead of
degrading everything. Requests matter equally — without them the scheduler
cannot tell how much memory the node has committed.

`resources: {}` on every deployment is the gap here. Start from what is measured
(~100 Mi), set a request near it and a limit above it, and let the runtime do the
rest.

## Argo CD: ~448 Mi, and ~73 Mi of it is components this deployment does not use

The standard install ships components this repository has no use for:

| Component | Memory | Verdict |
| --- | --- | --- |
| `argocd-application-controller` | ~206 Mi | Needed. The one to give a memory limit. |
| `argocd-server` | ~92 Mi | Needed — it is the UI and API. |
| `argocd-repo-server` | ~67 Mi | Needed — renders kustomize. |
| `argocd-dex-server` | ~28 Mi | **Remove.** SSO is not configured; the local `admin` account does not authenticate through dex. Delete the deployment and service and drop the dex configuration from `argocd-cm`. |
| `argocd-applicationset-controller` | ~23 Mi | **Remove.** `deploy/argocd/` declares plain `Application` objects only — no `ApplicationSet` exists. |
| `argocd-notifications-controller` | ~22 Mi | **Remove.** Nothing wires notification triggers or subscriptions. |
| `argocd-redis` | ~10 Mi | Needed. |

Removing those three is ~73 Mi with no loss of function. The controller's 206 Mi
is its cache of cluster resources; it is not configurable down, so a memory limit
is the practical control.

Worth stating plainly: **installing Argo CD added roughly 448 Mi to this node**,
which is the single largest change made to it recently, on a node that is already
swapping. It was the right call for deploy reproducibility, but if headroom ever
becomes the binding constraint, running Argo CD off this host is a legitimate
option rather than a trim.

## Headlamp is the smallest win, not the biggest

The 27 Mi in-cluster Headlamp is one of the **least** significant consumers on
the node — smaller than coredns plus local-path-provisioner combined. Replacing
it with the Headlamp desktop app, or dropping it for `kubectl` and the Argo CD
UI, frees ~27 Mi and one `svclb-*` placeholder that reports 0 Mi. It is a
reasonable simplification, but it will not change the node's memory situation,
and it is worth knowing that before spending a change on it.

Same for `metrics-server` (30 Mi): it is only needed for `kubectl top` and for
horizontal autoscaling, which nothing here uses. Dropping it is a real 30 Mi. It
is also how every figure in this document was obtained.

## The block that actually dominates: the nine lobby roles

The nine game-lobby deployments are ~856 Mi of the node's ~1191 Mi of application
memory — about 90 Mi each, for processes that are near-idle between players:

```
automatching 102Mi      basic-training 98Mi    combat-training 93Mi
replays 98Mi            tournament 96Mi        survival 90Mi
survival-hosts 98Mi     registration 93Mi      free-battle 88Mi
```

Those nine sum to ~856 Mi. The other six `mgo2` pods are separate services on
separate images, and add ~335 Mi: `http` (104 Mi), `account` (73 Mi), `gate`
(68 Mi), `gameplay-1` (67 Mi), `stun` (12 Mi) and `dns` (11 Mi).

If any role is not serving players at the current population, disabling that
deployment is the largest discrete saving available — roughly 90 Mi per role,
far more than every Argo CD trim combined. It is an operator decision about which
game types stay open, not a technical one, but it is the lever with the most
behind it.

## What is not worth changing

- `svclb-*` (17 pods): 0 Mi each. Placeholders for the LoadBalancer services.
- `argocd-redis` (10 Mi), `mgo2-dns` (11 Mi), `mgo2-stun` (12 Mi),
  `local-path-provisioner` (14 Mi): all trivial.
- **PostgreSQL**: not on this node at all. No Postgres pod exists in the cluster,
  so the database contributes nothing to these figures and the `mgo2-postgres`
  image never runs here.

## Measuring again

```sh
kubectl top nodes
kubectl top pods -A --sort-by=memory | head -30
free -m && swapon --show
```

Read `free`/`swapon` alongside `kubectl top`: the first two include everything
the kernel accounts for, and the swap line is the one that says whether the node
is actually under pressure. A node at 68% with no swap in use is comfortable; a
node at 68% with half a gigabyte swapped is not.
