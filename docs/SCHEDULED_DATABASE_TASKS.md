# Scheduled database work

Every recurring job in this codebase, what it runs against PostgreSQL, how often
it runs, and which process hosts it. This is the list to read before changing a
table's write path, tuning an interval, or wondering what a statement in
`pg_stat_activity` is.

The database is **external to the cluster**: no Postgres pod runs on the k3s
node, so none of this consumes node memory. It does consume the database's.

## The engine

Every worker below derives from `Shared/Utils/PeriodicWorker`, directly or
through `DailyWorker`, so the mechanics are shared and are worth knowing once:

- **The first run happens immediately**, not after one interval. A heartbeat
  registers the row it keeps alive before the first interval elapses. The daily
  cleanups are the exception, and the reason the rule is spelled out: they wait
  for their hour, because nothing they do is keeping a row alive.
- **The wait is measured from the end of the run**, never scheduled by a clock.
  A run slower than its interval delays the next one instead of collapsing into
  a back-to-back loop. A slow database therefore slows these down rather than
  turning every worker into a load generator. The daily cleanups are the other
  exception: they override the wait with the distance to their UTC time, which is
  the one place a clock reading is the cadence rather than a way of spacing runs.
- **The wait is jittered upward by up to 10%.** Every instance of a role starts
  together with the same interval, and without the drift they would all reach
  the database in the same instant.
- **A failing run is logged and the loop continues**, with a doubling delay up
  to a ceiling (5 minutes by default). `PeriodicWorker.NoBackoff` is passed where
  the cadence *is* the signal — a heartbeat whose own backoff would exceed the
  threshold that deletes the row it is keeping alive.
- **A run has no transaction of its own.** Only the training-time credit and the
  game delete share one, and that is stated where it happens.

## What a list shows, and what it costs to stop showing it

Every list a client or the HTTP API can ask for is filtered by the same stamp
this document's workers write, and by the same window the cleanup deletes within.
That is deliberate, and it is what lets the deletes be a daily batch: a row that
is no longer being written to stops being *shown* the moment it leaves the
window, whatever the cleanup has not yet got round to *removing*.

| Read | Kept only while | Where |
| --- | --- | --- |
| Gate lobby list (request `0x2005`, entries `0x2003`), player counts included | a gameplay lobby's `updated_at` is inside `LobbyStaleSeconds` (2 h) | `LobbyService.ReadLobbiesAsync`, `GetLobbiesAsync` |
| HTTP `GET /lobbies` | the same window | `LobbyService.FindAllAsync` |
| Lobby a dedicated host publishes its match in | the same window | `LobbyService.FindActiveGameLobbiesAsync` (`MatchService`) |
| HTTP `GET /games` | `games.updated_at` is inside `GameStaleSeconds` (1 h) | `GameService.FindAllAsync` |
| Room browser (request `0x4300`, entries `0x4302`), automatch lookups | the same window | `GameService.FindByLobbyAsync` |
| Friend, search and clan rosters (location block) | `character_presence.last_seen` is inside `CharacterPresenceService.StaleAfter` (1 min) | `CharacterPresenceService.FindLocationsAsync` |

The gate and the account server are the exception in the other direction: they
are permanent endpoints, carry no heartbeat of their own, and are always listed.

The gate's entry is the one that is not simply a query: it serves the list from an
in-memory cache for `LobbyHeartbeatIntervalSeconds` (an hour) and answers a failed
read with the list it still holds, so what a client is shown can be up to that much
older than the window by itself implies. Every other read above goes to the
database each time.

## Workers that write to the database

### Game lobby server — one process per lobby, nine of them

`deploy/game-lobby/` runs nine deployments of one image, so **every worker in
this section runs nine times concurrently, once per lobby process.**

| Worker | Interval | Default | Work against the database |
| --- | --- | --- | --- |
| `LobbyHeartbeatService` | `ServerOptions.LobbyHeartbeatIntervalSeconds` | 3600 s | `UPDATE lobbies SET updated_at` for this lobby, and nothing else. |
| `LobbyCleanupService` | `DailyWorker.MidnightUtc` | 00:00 UTC | Reads the gameplay lobbies whose `updated_at` is older than `LobbyStaleSeconds` (7200 s), then `DELETE`s them. The rooms of a deleted lobby cascade away with it. |
| `GameCleanupService` | `DailyWorker.MidnightUtc` | 00:00 UTC | In one transaction: credits `character_training_times` from the rosters of the rooms whose `updated_at` is older than `GameStaleSeconds` (3600 s), then `DELETE`s those `games`. Then, per lobby it emptied, a `SELECT count(*) FROM games` to republish that lobby's match total — a read, not a write, and only while a telemetry collector is listening. |
| `CharacterPresenceTickerService` | `CharacterPresenceService.HeartbeatInterval` | 30 s | `UPDATE character_presence SET last_seen` for this lobby's connected characters; `INSERT`s any row that went missing. |
| `CharacterPresenceCleanupService` | `DailyWorker.MidnightUtc` | 00:00 UTC | `DELETE`s every presence row whose `last_seen` is older than `StaleAfter` (60 s) **cluster-wide**. |
| `AutomatchTickerService` | `AutomatchOptions.Tick` | 5 s | **None.** It drains an in-memory queue and pushes packets; the matchmaker holds no database rows. |

The published population (`Lobby.PlayersCount`) is deliberately absent from that
table: it is written when it *changes* — on a join, on a leave, and once after a
burst of abrupt disconnects (`GameplayLobbyServer`'s coalescing timer, 5 s) —
rather than republished by a beat. It is the number a lobby list shows, and a
list is read rarely, so a worker that rewrote it every interval would be writing
a value nobody had asked for, over the count the transitions had just published.
The one startup write is the reset to zero in `GameLobbyServerRunner`, for the
same reason the presence rows are cleared there: the row a restart lands on
carries the count the previous instance published, and nobody is connected to a
process that has just started.

Three windows, each measured against the thing it is a window on, and the deletes
are not on an interval at all:

- **Lobby rows** leave the stale window at `LobbyStaleSeconds`, which is twice
  `LobbyHeartbeatIntervalSeconds` — an hour's beat, a two-hour window. A lobby
  container answers for itself and holds no player connection, so it is the slow
  one. The heartbeat does not back off, because a backoff longer than the interval
  would let the row it is keeping fall out of the window it is kept in.
- **Rooms** leave the room list at `GameStaleSeconds`, an hour, because the beat
  is the host's own ping report at half a minute and an hour of silence is a host
  that is gone. A room hosted by a dedicated server is stamped by
  `MatchService` every `MatchHeartbeatIntervalSeconds` — half the window, so its
  own beat lands inside it just as a lobby's does.
- **Presence rows** are the tight one at `StaleAfter`, a minute, because the
  client's own cadence is half a minute: the beat lands once inside the window, so
  a single missed beat is still survived and a live player is never hidden by the
  read filter, while a player who left is off every friend list within a minute.
  The heartbeat's failure backoff is capped at two beats (1 min) so its own
  absence cannot evict the players it is stamping.
- **The deletes are a fixed hour, once a day.** A row stops being *listed* when
  it leaves the window — see the table above — and is *deleted* at 00:00 UTC.
  Doing the delete on the same schedule as the window would be nine lobby
  processes repeating one statement every hour to remove rows that nobody can
  see; a daily batch removes the same rows for a ninth of the traffic, at the
  hour when a deployment is quietest.

A daily worker is a `DailyWorker` rather than a `PeriodicWorker`: it sleeps until
the next occurrence of its UTC time, never runs at startup, and retries a failed
run within the same day on a 5 min-to-1 h ladder instead of waiting a whole one.

### Gameplay server — one process per match host

| Worker | Interval | Default | Work against the database |
| --- | --- | --- | --- |
| `MatchService` | `ServerOptions.MatchHeartbeatIntervalSeconds` | 1800 s | Ensures the host's account row exists, finds or creates its `games` row plus the host's `game_players` row, then `UPDATE`s `games.updated_at`. |

`MatchService` refuses to back off, for the same reason as the lobby heartbeat:
the row it stamps leaves the window it is listed within on the same order as the
interval, so any wait beyond the interval would take the match off the room list
before the next beat could put it back. Its first run retries every 5 s until the
game lobby it publishes in exists, since that lobby is owned by a different
process that may still be starting.

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

1. **The three sweeps are global, and once a day all nine copies run together.**
   Stale-lobby cleanup, stale-game cleanup and the presence reap are deliberately
   *not* scoped to the lobby that runs them — cleaning up after a process that
   died is exactly what a surviving process has to do — and every statement is
   idempotent, so whichever instance arrives first wins and the rest are no-ops.
   Nine copies of a statement at midnight is the price of the guarantee. The same
   nine copies every five minutes bought the same guarantee, because the read
   filters above are what stop a stale row being shown, and that was the trade
   this schedule was changed for.
2. **Application clocks stamp the rows, not `now()`.** Presence timestamps are
   taken from the process and passed as values, because a row written by one
   process is compared against a cutoff taken by another, and process skew would
   otherwise evict connected players. The one exception is the training-time
   credit, whose SQL computes the interval with the database's `now()`, since it
   is comparing two columns inside a single statement.
