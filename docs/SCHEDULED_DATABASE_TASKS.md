# Scheduled database work

Every recurring job in this codebase, what it runs against PostgreSQL, how often
it runs, and which process hosts it. This is the list to read before changing a
table's write path, tuning an interval, or wondering what a statement in
`pg_stat_activity` is.

The database is **external to the cluster**: no Postgres pod runs on the k3s
node, so none of this consumes node memory. It does consume the database's.

## The engine

Every worker below derives from `Shared/Utils/PeriodicWorker`, so the mechanics
are shared and are worth knowing once:

- **The first run happens immediately**, not after one interval. A heartbeat
  registers the row it keeps alive before the first interval elapses.
- **The wait is measured from the end of the run**, never scheduled by a clock.
  A run slower than its interval delays the next one instead of collapsing into
  a back-to-back loop. A slow database therefore slows these down rather than
  turning every worker into a load generator.
- **The wait is jittered upward by up to 10%.** Every instance of a role starts
  together with the same interval, and without the drift they would all reach
  the database in the same instant.
- **A failing run is logged and the loop continues**, with a doubling delay up
  to a ceiling (5 minutes by default). `PeriodicWorker.NoBackoff` is passed where
  the cadence *is* the signal — a heartbeat whose own backoff would exceed the
  threshold that deletes the row it is keeping alive.
- **A run has no transaction of its own.** Only the training-time credit and the
  game delete share one, and that is stated where it happens.

## Workers that write to the database

### Game lobby server — one process per lobby, nine of them

`deploy/game-lobby/` runs nine deployments of one image, so **every worker in
this section runs nine times concurrently, once per lobby process.**

| Worker | Interval | Default | Work against the database |
| --- | --- | --- | --- |
| `LobbyHeartbeatService` | `ServerOptions.LobbyHeartbeatIntervalSeconds` | 300 s | `UPDATE lobbies SET updated_at` for this lobby, then one `UPDATE` of the published population (`Lobby.PlayersCount`) per lobby this process serves. |
| `LobbyCleanupService` | same | 300 s | Reads the gameplay lobbies whose `updated_at` is older than `LobbyStaleSeconds` (600 s), then `DELETE`s them. The rooms of a deleted lobby cascade away with it. |
| `GameCleanupService` | same | 300 s | In one transaction: credits `character_training_times` from the rosters of the stale rooms, then `DELETE`s those `games`. Publishes match totals afterwards, which is telemetry and not a write. |
| `CharacterPresenceTickerService` | `CharacterPresenceService.HeartbeatInterval` | 30 s | `UPDATE character_presence SET last_seen` for this lobby's connected characters; `INSERT`s any row that went missing; then `DELETE`s every row older than `StaleAfter` (120 s) **cluster-wide**. |
| `AutomatchTickerService` | `AutomatchOptions.Tick` | 5 s | **None.** It drains an in-memory queue and pushes packets; the matchmaker holds no database rows. |

Two cadences are tied to thresholds rather than chosen:

- **Lobby rows** are deleted at `LobbyStaleSeconds`, which is twice
  `LobbyHeartbeatIntervalSeconds`. The heartbeat does not back off, because a
  backoff longer than the interval would delete the row it is keeping.
- **Presence rows** are deleted 120 s after their last stamp, and the heartbeat
  is 30 s, so four beats fit inside the window and three consecutive misses are
  needed before a live player is evicted. The heartbeat's failure backoff is
  capped at two beats (60 s) for the same reason.

### Gameplay server — one process per match host

| Worker | Interval | Default | Work against the database |
| --- | --- | --- | --- |
| `MatchService` | `ServerOptions.LobbyHeartbeatIntervalSeconds` | 300 s | Ensures the host's account row exists, finds or creates its `games` row plus the host's `game_players` row, then `UPDATE`s `games.updated_at`. |

`MatchService` refuses to back off, for the same reason as the lobby heartbeat:
the row it stamps is deleted once it goes quiet for twice the interval. Its
first run retries every 5 s until the game lobby it publishes in exists, since
that lobby is owned by a different process that may still be starting.

## Recurring work that never reaches the database

Listed so they are not mistaken for database load when reading thread or CPU
profiles:

- `PeerSessionService` — a 15 s timer that reaps idle UDP sessions, in memory.
- `DiscordGatewayClientService` / `DiscordStartupService` — the HTTP server's
  hosted services. The gateway heartbeat is driven by Discord's own
  `heartbeat_interval` from the Hello frame, not by a fixed period.
- `LobbyCoordinationClientService` — a 60 s reconnect loop for the coordination
  stream, and the snapshot it registers on reconnect.
- `ConnectionDrainUtils` — polls the connection count every 1 s during
  shutdown, and reports progress every 60 s. Shutdown only.

## Startup-time database work (not scheduled)

These run once per process start and are easy to mistake for timers:

- **Clearing this lobby's presence rows** (`ClearLobbyAsync`) and resetting its
  published population to zero, both before the listener opens. The comment
  above the latter explains why: a restart lands on the previous instance's row,
  and nobody is connected to a process that has just started.
- **`StartupUtils.RetryAsync`** wraps a startup step in 5 attempts, waiting 2 s
  and doubling to a 20 s ceiling. It is a retry ladder, not a poll — the point
  is to ride out a database that is briefly away rather than crash-loop the
  whole deployment.
- **The EF Core migration bundle**, which is an Argo `PreSync` hook Job
  (`deploy/migrate/`) and runs once per sync, not on a schedule. See
  `deploy/README.md`.

## Reading the volumes

Two consequences of the design are worth holding in mind:

1. **The three sweeps are global and run nine times over.** Stale-lobby
   cleanup, stale-game cleanup and the presence reap are deliberately *not*
   scoped to the lobby that runs them — cleaning up after a process that died is
   exactly what a surviving process has to do — and every statement is
   idempotent, so whichever instance arrives first wins and the rest are no-ops.
   That is nine copies of the same statement traffic, which is the price of the
   guarantee.
2. **Application clocks stamp the rows, not `now()`.** Presence timestamps are
   taken from the process and passed as values, because a row written by one
   process is compared against a cutoff taken by another, and process skew would
   otherwise evict connected players. The one exception is the training-time
   credit, whose SQL computes the interval with the database's `now()`, since it
   is comparing two columns inside a single statement.
