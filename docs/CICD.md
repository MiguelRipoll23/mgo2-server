# CI/CD

How a commit becomes a running container, and why one service's failure cannot
hold another's deploy.

The repository has one pipeline per service. Each one decides for itself whether
a diff affects it, tests and publishes on its own, and writes its own image tag
to `deploy/`. There is no shared build job, no shared deploy step, and nothing in
CI touches the cluster — ArgoCD reconciles `deploy/`, which `deploy/README.md`
covers.

## What this replaced, and why

The repository used to have a single `build-deploy.yml` that built every changed
image in one matrix and then deployed everything from one job on a self-hosted
runner. Two different problems hid in it:

- **One pipeline gated every service.** A test failure, a flaky test, or a
  workflow-level error in one service's half of the run stopped the others, even
  when their code was untouched.
- **The deploy was imperative and order-dependent.** The deploy job ran
  `kubectl set image` and `kubectl rollout restart` against the live cluster, so
  the cluster's state was an accumulation of whatever previous runs had managed
  to finish. A run that failed between "migration applied" and "image swapped"
  left the two disagreeing, and the next run inherited that.

They are fixed independently: per-service pipelines for the first, GitOps for
the second. Neither needs a polyrepo, and neither needs a service to know
anything about another service's pipeline.

## The pipelines

| Workflow                      | Builds                     | Image                       | Pins                    | Tests                     |
| ----------------------------- | -------------------------- | --------------------------- | ----------------------- | ------------------------- |
| `gate-lobby-server.yml`       | `src/GateLobbyServer`      | `mgo2-gate-lobby-server`    | `deploy/gate`           | Shared                    |
| `account-lobby-server.yml`    | `src/AccountLobbyServer`   | `mgo2-account-lobby-server` | `deploy/account`        | Account + Shared          |
| `game-lobby-server.yml`       | `src/GameLobbyServer`      | `mgo2-game-lobby-server`    | `deploy/game-lobby`     | GameLobby + Shared        |
| `gameplay-server.yml`         | `src/GameplayServer`       | `mgo2-gameplay-server`      | `deploy/gameplay`       | Shared                    |
| `http.yml`                    | `src/Http`                 | `mgo2-http`                 | `deploy/http`           | Http + Shared             |
| `dns.yml`                     | `src/Dns`                  | `mgo2-dns`                  | `deploy/dns`            | Dns + Shared              |
| `stun.yml`                    | `src/Stun`                 | `mgo2-stun`                 | `deploy/stun`           | Stun + Shared             |
| `postgres.yml`                | the EF Core bundle         | `mgo2-postgres`             | `deploy/migrate`        | Shared                    |
| `release.yml`                 | all of the above, forced    | —                           | every folder            | —                         |

Every service workflow calls the same reusable workflow,
`.github/workflows/service.yml`, with its own project, image, manifests folder
and test filter. One file describes the pipeline; the per-service files only say
*which* service and *when*. `release.yml` is a matrix over the same inputs with
`force: true`, which is how a version tag or a manual run rebuilds and pins
everything without pretending a diff said so.

`release.yml` exists as its own workflow rather than as tag filters on the
per-service ones because GitHub does not evaluate `paths` for a tag push. Tag
triggers on eight path-filtered workflows would be unreliable at best; a
dedicated release workflow says what it means.

No workflow lists `deploy/**` in its `paths`. That is deliberate twice over: a
manifest change is ArgoCD's to reconcile rather than a reason to rebuild an
image, and it keeps the tag-bump commit each pipeline pushes to `main` from
starting another run.

## Cluster access

No workflow has any. The pipelines build an image and commit a tag; they never
hold a kubeconfig or a cluster credential, and every job runs on GitHub-hosted
runners.

That is a change from what was here before. The deploy job ran on a self-hosted
runner with `KUBECONFIG=/opt/github-runners/.kube/config`, against what the
workflow called "the deploy account" — a set of rights broad enough to
`kubectl apply` the whole `k3s/` folder, which included a `Namespace` and a
`GatewayClass`, and so was wider than the namespace it mostly worked in.

**None of that is in this repository.** No ServiceAccount, Role, RoleBinding or
token Secret was ever committed, and no doc described it, so there is nothing
here to delete. It lives on the runner host and in the cluster, and it is unused
now:

- if the kubeconfig is a copy of the k3s admin credential
  (`/etc/rancher/k3s/k3s.yaml`), there is no service account to remove — the copy
  on the runner host is the whole of it;
- if it is a dedicated service account, its Role/RoleBinding, whatever
  cluster-scoped binding let it create the Namespace and GatewayClass, and its
  token Secret can all go;
- the self-hosted runner registration is unused too, since no workflow names it
  any more, and removing it is what takes the machine out of CI's trust
  boundary.

Do it after `mgo2-root` is Synced and every child Application is Healthy, so
that a way to apply by hand still exists if the first sync goes wrong.

**What replaced it is ArgoCD.** The `argocd-application-controller` service
account from the standard install does the applying. Its default rights are
considerably broader than the deploy account's ever were, so the equivalent
hardening is an AppProject that scopes it to the `mgo2` namespace and to the
named cluster-scoped resources — the children currently say `project: default`,
which is the unscoped case.

## Affected detection

The trigger paths in each workflow over-trigger on purpose: every service lists
`src/Shared/**` and `src/Infrastructure/**`, because every service references
them, so a shared change starts every pipeline. Deciding which of those
pipelines the change *actually* reaches is then `dotnet affected`'s job.

The reusable workflow's first job runs:

```sh
dotnet affected --from <baseline> --to HEAD --format json --output-name affected
```

and looks for the service's project in `affected.json`. The baseline is the
target branch for a pull request and the pushed-before commit for a push,
falling back to `HEAD~1` when the event payload does not carry one. Exit code
`166` is the tool's "nothing changed" answer and is read as *not affected*
rather than as a failure; any other non-zero code stops the run.

Two details worth knowing:

- **`dotnet-affected` is pinned in `.config/dotnet-tools.json`**, so the
  pipelines run the same version the repository does. `dotnet tool restore`
  first, then `dotnet affected`.
- **Build inputs are checked separately.** `dotnet affected` only sees files
  that belong to a project, so `docker/Dockerfile`, `.config/dotnet-tools.json`,
  `Directory.Build.props` and `Mgo2Server.slnx` would map to no project and be
  silenced. The job diffs for those first and, if any changed, runs the
  pipeline — they rebuild every image.

The tool's own docs are worth reading before changing anything here:
<https://dotnet-affected.com/reference/cli/>.

### The dependency graph this is built on

Audited from the `ProjectReference` items in each `.csproj`:

```
Mgo2Server.Shared                      (no project references)

Mgo2Server.Infrastructure            → Shared

Mgo2Server.GateLobbyServer           → Shared, Infrastructure
Mgo2Server.AccountLobbyServer        → Shared, Infrastructure
Mgo2Server.GameLobbyServer           → Shared, Infrastructure
Mgo2Server.GameplayServer            → Shared, Infrastructure
Mgo2Server.Http                      → Shared, Infrastructure
Mgo2Server.Dns                       → Shared, Infrastructure
Mgo2Server.Stun                      → Shared              (no Infrastructure)

Mgo2Server.Tests                     → Shared, Dns, Stun, AccountLobbyServer,
                                       GameLobbyServer, Http
```

Two consequences the workflows depend on:

- A change to `Shared` or `Infrastructure` reaches **every** service, which is
  why those paths are in every pipeline's trigger list.
- `Stun` does not reference `Infrastructure`. Nothing currently depends on that
  distinction, but the trigger list carries `src/Infrastructure/**` for STUN as
  well, and the affected gate is what actually prunes it — the paths are allowed
  to be coarse precisely because the gate is not.

The graph is the thing to check when a service's pipeline does not run and
should have: `dotnet affected --from origin/main --dry-run --verbose` prints the
changed projects, the affected projects and the exclusions, and is the fastest
way to see which of the two is wrong.

## Flaky tests

Known-flaky tests carry two traits — their service, and `Flaky`:

```csharp
[Trait("Category", "Shared")]
[Trait("Category", "Flaky")]
public sealed class SomeTimingDependentTests
```

Nothing is quarantined at the moment. The trait is kept documented because the
job below is what a quarantined test needs, and the way out of quarantine is
worth writing down while it is fresh.

### The quarantine this had, and how it was left

`PeriodicWorkerTests` carried the trait for a while. The tests were not timing
out; they were measuring the wrong machine. They watched the **gaps between the
worker's runs**, and a gap is the wait the worker chose *plus* however long the
test host took to get back to the thread — so an upper bound on a gap is a bound
on the machine's load, and it failed under load while the worker paced itself
perfectly.

The fix was not a longer patience. `PeriodicWorker.ResolveWait` is
`protected virtual`, so the probe worker records the waits it **resolved**, and
the assertions moved onto those: a wait is at least the interval and at most it
plus the drift, and it grew while failing and came back once the run succeeded.
Those are the claims the test was always trying to make, and the load is no
longer part of them. The trait came off with the fix, as the paragraph above
asks.

Where a gap *is* the right measure, it is only safe as a **lower** bound:
`Waits_after_a_run_that_took_longer_than_its_interval` asserts a gap is at least
the run plus its interval, which load can inflate but never shrink. An upper
bound on a wall-clock gap has no such refuge.

The service category decides *which* pipelines run the test; the `Flaky`
category decides that it never gates one. Each pipeline's `test` job excludes
them (`--filter "<service> & Category!=Flaky"`), and a separate, non-blocking
`flaky` job runs only them (`--filter "Category=Flaky"`) with
`continue-on-error: true`. That job reports; it does not fail the run, and it
cannot stop a deploy.

To quarantine a test, add the trait. To release one, remove it and let it run in
the gating job again — a quarantined test that stays quarantined is a claim
nobody is checking.

## Tests are partitioned by trait, not by project

The suite is a single project, `tests/Mgo2Server.Tests`, in a single namespace,
referencing six projects. Splitting it per service would be the cleaner
boundary long-term, but it is a large move; what the pipelines actually need is
a way to run *one service's* tests, and that is what the service trait gives
them.

Every test class carries `[Trait("Category", "<Service>")]` for the service that
owns the code it exercises. Each pipeline runs its own category **plus
`Shared`**, because shared code is a dependency of every service and a failure
there is genuinely everyone's.

The mapping is by layer, which is why a few assignments look surprising:

- `LobbyCoordinationConnectionTests` is `GameLobby` — it drives the lobby side
  of the gRPC link, though it also touches `Http.Coordination`.
- `ServerMetricsServiceTests`, `PersistenceModelTests` and the protocol codec
  tests are `Shared` — they exercise code every service is built from.
- The gate and the gameplay server have no tests of their own, so their
  pipelines run the shared half of the suite and nothing else.

**Still shared: compilation.** The test project references every service it
tests, so a *compile* error in one service stops every pipeline's build. What
the partition removes is one service's test *failure* blocking another's deploy,
which is the case the split was for.

## Validation

The four checks that matter, and how to run each:

- **One service changes.** Touch a file under `src/Dns/` and push. Only
  `dns.yml` should run to completion; the other seven start (their paths match
  `tests/**` at most) and stop at their affected job with
  `… is not affected by this diff.`
- **A shared library changes.** Touch `src/Shared/`. Every service pipeline
  starts, and each one's affected job decides for itself — all of them should
  report affected, unless the change is confined to a project only some of them
  reference.
- **A test fails in one service.** Break a test tagged `Category=Http` and push.
  `http.yml` fails; run any other service's pipeline and it should still
  publish and pin. Break a `Shared`-tagged test instead and every pipeline
  fails, correctly — nothing can be deployed against code that is broken.
- **A migration runs end-to-end.** Push a change under `src/Shared/Persistence`
  (which rebuilds the bundle and re-pins `deploy/migrate`) and watch Argo: the
  `mgo2-migrate` Application's PreSync hook Job must reach `Completed` before
  any wave-2 Application begins to sync.

## What the old workflow's answers were

The spec this implements left four questions open. They are settled:

- **CI system**: GitHub Actions.
- **GitOps controller**: ArgoCD, chosen over Flux because the manifests are
  plain kustomizations rather than Kustomization objects, and because the
  migration needs a hook that runs before a sync — which Argo has and Flux
  models as a dependency between Kustomizations.
- **Database**: PostgreSQL 18, one external instance. The bundle is
  `dotnet ef migrations bundle`, verified in the `postgres` Dockerfile stage.
- **Replicas**: one per service (`replicas: 1` in every deployment). Expand and
  contract still applies: a rollout runs the old and the new pod together even
  at one replica, so the discipline is about the rollout window, not about
  replica count.
