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

## What reclaims the registry

Publishing adds a version and nothing takes one away. Every commit that reaches
a service leaves a version of that image behind — a multi-arch manifest list
rather than a layer — and no pipeline deletes what an earlier one made, so the
packages grow monotonically with the commit history and the releases on top of
it.

`purge-images.yml` is what stops that. It runs nightly at 04:17 UTC, over a
matrix of the eight images, and each version is kept unless one of four rules
lets it go:

| Kept                                                                             | Because                                                                   |
| -------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| a version carrying `latest` or the default branch's name                          | those two names follow the newest build, and the compose install pulls `latest` |
| a version carrying a SHA any `deploy/*/kustomization.yaml` or `bundle.yaml` pins  | whatever Argo is reconciling has to be pullable                             |
| a version carrying a SHA one of the last `keep` release tags points at             | that is the rollback window, and a rollback is a one-line commit           |
| the newest `keep` versions of each image                                           | this is what bounds the growth *between* releases                          |

The second rule is read out of the manifests rather than derived from an age,
which is what lets the third be short: the pin that is there now is protected
whatever the retention window says, and so is every release inside the window.
The third rule matches on the **SHA** a release tag points at, not on the tag's
name, because the two names are not the same string — git has `v1.4.0`, the
registry has `1.4.0`. Both the tag object and the commit it peels to are
protected, because an annotated tag pushes the former as `GITHUB_SHA` and a
lightweight one pushes the latter, and the pipeline published whichever it was
handed.

**An old release loses its version tag with the SHA.** The registry API deletes
a version whole and cannot drop one tag from it, so `1.4.0` cannot be kept while
the digest beside it goes. Keeping a release therefore means keeping all of it,
and past the last `keep` releases a version name stops resolving. That is the
trade this workflow makes, and it is the reason the window is expressed in
release tags rather than in versions kept.

`packages: write` is the one permission it asks for beyond `contents: read`, and
it is the only workflow here that can delete anything. It runs with
`fail-fast: false`, because a run that failed halfway still has to say what it
deleted; a version it could not delete is a warning and a non-zero exit at the
end, not a stop.

Run it by hand with `dry_run: true` first — it prints one line per version with
the reason it was kept or deleted, which is the whole audit trail:

```
keep  10  2026-10-04T10:00:00Z  7b043cd… main latest  main follows the newest build
keep  12  2026-10-03T08:00:00Z  9cb959ee5581…      9cb959 is pinned or released
keep  9   2026-10-04T09:00:00Z  4a1f0e2…             one of the newest 3
would delete  13  2026-09-28T09:00:00Z  8f029a58… 0.6.0
```

The buildx caches the pipeline writes (`cache-to: type=gha`) are deliberately
not in it. GitHub expires a cache that has gone unread for seven days and evicts
the least recently used past the repository's ten-gigabyte limit, so they bound
themselves without a job guessing at them.

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
referencing six projects. It is **the protocol codecs and nothing else**: the
frame cipher and its tail digest, the TCP packet codec, the record framing and
the handshake body. Payloads, handlers and domain services are not tested —
their byte offsets and behaviour are written down in `docs/protocol/` instead,
and the codecs are what a wrong implementation breaks in a way nothing else
catches.

Every test class carries `[Trait("Category", "Shared")]`, and each pipeline runs
its own category **plus `Shared`**, so the gate, the gameplay server and the
game lobby all run the same tests.

**Still shared: compilation.** The test project references every service it
tests, so a *compile* error in one service stops every pipeline's build. What
the partition removes is one service's test *failure* blocking another's deploy,
which is the case the split was for.

## Validation

The five checks that matter, and how to run each:

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
- **The purge keeps what it should.** Dispatch `purge-images.yml` with
  `dry_run: true`, and read the log: every `newTag` in `deploy/` and the last
  `keep` release tags must appear on a `keep` line, and nothing carrying
  `latest` or the default branch's name may appear on a `would delete` line.
  That is the check that the rules still describe the repository's real state —
  in particular that no manifest moved to a digest pin, which would change what
  "the SHA it pins" means.

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
