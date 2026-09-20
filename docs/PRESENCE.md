# Per-character presence across lobby processes

**Which lobby is each character in, right now.** The page began as the design of the table and
stayed the reasoning behind it; the table and the three readers it was written for are implemented
on this server as of 2026-09-18 (see *Status*).

## Why it is not answerable from a process's own sessions

Production runs **one process per lobby**. Each knows only its own connections — the reference's
`ChannelRegistry`, and this server's `ActiveGameSessionsService`, are deliberately instances rather
than statics, because the integration suite stands several servers up in one process and a shared
map would silently join them. So no process can answer "where is this character" about anyone
outside itself, and nothing else records it.

## What is blocked on it

| consumer | what it sent before the table | why that was wrong |
| --- | --- | --- |
| `0x4582` roster, wire `0x14` (`ROSTER_ENTRY_VISIBLE`) | a hardcoded **1** as a lobby id | [ELF] it is a **lobby id**, rendered into `STRING_F_LIST_LOBBY` and handed to `0x884300`/`0xD47CE0` on "move to lobby". Every client was told every friend was in lobby 1, and the jump was aimed there |
| `0x4602` player-search tail | three zeros | `lobby_id`, `lobby_name`, `game_id`, `game_name`, `lobby_type` — all confirmed 2026-07-31. Deliberately blank rather than guessed |
| `0x4b54` clan roster tail | the same zeros | the *third* carrier of the block, found by live testing a day after the first two |
| automatch slot-in eligibility | — | needs the same "who is where" query, and still does |

The `0x4582` case is the sharp one: it is not a blank, it is a **wrong answer that misroutes a
player action**. All three readers are served from the table on this server now; automatch slot-in
is not, and is still open.

## The design

**One table, written from `ChannelRegistry`'s existing hooks, swept by `GameServer`'s existing
scheduler.** No new lifecycle, no new thread, no new component.

```sql
CREATE TABLE public.chara_presence (
    chara_id  bigint PRIMARY KEY REFERENCES chara(id) ON DELETE CASCADE,
    lobby_id  bigint NOT NULL REFERENCES lobby(id) ON DELETE CASCADE,
    since     timestamptz NOT NULL DEFAULT now(),
    last_seen timestamptz NOT NULL DEFAULT now()
);
```

**Implemented on this server** (2026-09-18), with the schema and the three readers this page
describes: `character_presence` (migration `CharacterPresence`), the service in
`Shared/Domain/Presence`, the writes on the lobby tracker's join and leave, the boot clear, the
heartbeat and the sweep, and the location block served from it by the friends roster (`0x4582`),
the player search (`0x4602`) and the clan roster (`0x4b54`). The differences from the reference are
below, and they are about where the code runs rather than what it does:

- **The table and the entity use `character`, not `chara`** — `character_presence`, columns
  `character_id` / `lobby_id`. There is no abbreviation rule in this repository, and the rest of
  the schema spells the word out.
- **No `since` column** (dropped by migration `DropPresenceSince`). The reference's `V72` keeps one,
  and on this server it was written on every enter and read by nothing at all: no packet carries a
  "in this lobby since" field and no reader wanted it. It is the same test the character row's
  write-only `host_score`/`host_votes` failed in the same week. The upsert no longer resets it
  because there is nothing to reset.
- **One clock, the process's.** Every stamp the server writes — on enter, on the beat and in the
  repair — is taken from the process and passed as a value; the sweep's cutoff is taken from the
  same clock. The reference writes `now()` in all three statements, which is one clock too, and a
  better one when the only writer is the database — but a beat taken in one lobby and a sweep taken
  in the next are two processes, and the risk being removed is a host clock skew evicting live
  players. The columns keep their `now()` default for an insert that names no columns, which
  nothing in this server does.
- **The writes hang off `LobbyTrackerService`** rather than off the channel registry, because that
  is this process's equivalent: it is what is told a session joined or left a lobby, and it is
  already the one place that knows both the character and the lobby.
- **Two timestamp columns on the character are this server's answer to one `last_seen_at`**, and
  the presence pair is still the presence pair: a process that dies leaves its rows behind, which
  is why the boot clear exists at all.
- **The boot path also writes the lobby's population as zero.** The row is keyed by port, so a
  restart lands on the row the previous instance published, count included, and nothing moves that
  count until a join or a leave — which cannot happen before a client is served. Every connection
  to the lobby died with the process it was talking to, so the same "nobody is connected to a
  process that has just started" that clears the presence rows also zeroes the count, before the
  listener opens.
- **The ticker also records missing rows again.** A live character with no row is re-recorded
  (insert-only, so a lobby hop that already claimed them keeps its row) and the anomaly is logged.
  Two ways to get there, both by design: a write on the enter path is fire-and-forget, and a row is
  removed by cascade when the lobby row it points at is deleted. The reference deliberately does not
  resurrect, to keep an over-aggressive sweep visible — that visibility is kept by the log line, so
  the repair does not hide anything except a player who would otherwise be invisible to every friend
  list until they reconnected.
- **The reaper runs in every gameplay lobby process.** There is no other process it could run in
  usefully: the account server never writes a presence row, so it would only be sweeping after
  lobbies, which each lobby already does for itself and for whoever else had died.

### Considered on this server and deliberately not changed

**Running the sweep in the gate and the account server too.** The reference runs it in every server,
because the gate is what serves its lobby list and so has to clean up after lobbies it does not own.
Here the three readers are gameplay-lobby packets (`0x4582`, `0x4602`, `0x4b54` are registered for
`ServerType.GameplayLobby` alone), so "no gameplay lobby is running" already means "nothing can read
the table"; the extra processes would reap for no reader. It is one move of the worker into
`Shared` away if the HTTP side ever serves a location.

**The read is two statements, and the search only ever wants one.** `FindLocationsAsync` asks
presence and the room rosters separately, then matches them in memory, and the player search hands
it a single identifier. Left alone because the reply is one row and a search is a person typing a
name, so an overload whose joins are stated once for one character buys a round trip nobody waits
for. The friend and clan rosters are where the batching matters, and they batch.

**The strongest argument for changing something is not one of the above: the semantics have no
automated test.** The tests pin the wire block, the label table and the relationship between the
beat and the staleness bound, but the three rules that are actually subtle — the upsert that claims,
the delete that checks ownership, and the repair that must not overwrite a hop — are argued in
comments and unwitnessed by anything executable. Closing that means an integration test against a
real PostgreSQL (Testcontainers), which puts Docker in the path of `dotnet test` for everyone,
including a laptop with no daemon running. That is a decision rather than an oversight.

### `chara_id` alone is the primary key

Not `(chara_id, lobby_id)`. **A character is in exactly one lobby at a time**, and making that the
key lets the database enforce the invariant instead of relying on every call site being careful.
It also makes a lobby change a single upsert rather than a delete-then-insert with a window in the
middle.

### Game membership is NOT duplicated here

`game_player` already records which game a character is in. Presence answers *which lobby*; the
game comes from a join. Two tables both claiming to know the current game is exactly the kind of
second source that goes stale and then gets believed.

So `0x4602`'s five-field tail is one query: `chara` → `chara_presence` → `lobby` (name, subtype) →
`game_player` → `game` (id, name).

### The race that will bite if it is missed

A lobby hop is **two processes racing**: the destination's insert and the origin's
disconnect-delete, in either order. So the two statements are asymmetric on purpose:

- **enter** — upsert: `ON CONFLICT (chara_id) DO UPDATE SET lobby_id = excluded.lobby_id, ...`
- **leave** — *conditional*: `DELETE ... WHERE chara_id = :chara AND lobby_id = :myLobby`

**Without `AND lobby_id = :myLobby`, a late disconnect from the old process erases the presence the
new one just wrote**, and the player intermittently disappears from every friend list in the game.
It is one clause, and it is very hard to diagnose after the fact — the symptom is intermittent and
depends on process scheduling.

### Crash recovery: a process clears its own lobby on boot

`DELETE FROM chara_presence WHERE lobby_id = :myLobby`, after a successful bind.

**By definition nobody is connected to a process that has just started**, so this is unconditionally
correct, and it handles hard kills, `docker restart` and crash-loops with no staleness heuristic.
That matters here specifically: this project has already had a container crash-loop ~1300 times, and
a TTL-only design would have left every one of those players "present" until the timer expired.

The heartbeat below therefore covers only the remaining case — a process that dies and **never comes
back**.

### Heartbeat

One batched statement per scheduler tick, over the live channel set:

```sql
UPDATE chara_presence SET last_seen = now() WHERE chara_id = ANY(:ids)
```

One statement per lobby per tick regardless of population. **30s tick, 120s staleness.** A reaper
deletes rows older than the staleness bound.

### Writes go on transitions, never in `onPacket`

`ChannelRegistry.onPacket` fires for **every inbound packet on every controller**. A database write
there is a write per packet. Presence is written where the channel map is written — `track()`, which
the authenticating handler also calls precisely because `onPacket` runs before `authenticate()` and
would otherwise miss a character until its second packet.

## Only game lobbies record presence

Gate and account servers are constructed with `lobbyId` 0, which is not a row in `lobby`. They
**do not write presence**, for two reasons and not just the foreign key: a connection to a gate is
not "in a lobby" in any sense a player would recognise, and the character may not even be selected
yet. Asserting otherwise would put a false row in front of every friend list.

They **do** still run the reaper. It is scoped to no lobby — it has to be, since its whole job is
cleaning up after a process that is not running — so every server that runs it shortens the window
after a lobby dies and never returns.

## Test hazard, and it is the same one `ChannelRegistry` documents

The integration suite stands **several servers up in one JVM against one database**. Presence is
keyed by `lobby_id`, so distinct lobby ids do not collide — but **the boot-time DELETE means
starting a second server for lobby 1 wipes the first one's rows.** Any test that stands two servers
on the same lobby id will see the second clear the first.

`TestDatabase`'s truncation keep-list also needs a decision; `starter_gear` was truncated between
tests once already, and its own comment had predicted it.

## Staging

1. **Plumbing only, no wire change.** Migration, service, boot-clear, hooks, heartbeat, reaper.
   Independently testable: rows appear on connect, vanish on disconnect, survive a heartbeat, and a
   lobby hop leaves exactly one row pointing at the destination. **Nothing the client sees changes**,
   so this cannot regress a live screen.
2. **`0x4582` wire `0x14`.** The smallest consumer, and the only one whose current value is actively
   wrong. One field, one query.
3. **`0x4602`'s five-field tail.** The full join, filling three fields we deliberately blanked.

Automatch slot-in eligibility becomes possible after step 1 and is tracked separately.

## Status

> The steps below are the **reference server's** history, kept because the order and the mistakes
> are the useful part of it. This server's port is the entry after them.

- **This server: DONE** (2026-09-18). `character_presence` (generated migrations
  `CharacterPresence` and `DropPresenceSince`), `CharacterPresenceService` in
  `Shared/Domain/Presence`, writes on `LobbyTrackerService.JoinLobby`/`LeaveLobby`, the boot clear
  and the population reset in `GameLobbyServerRunner`, `CharacterPresenceTickerService` (beat every
  30 s, repair of missing rows) and `CharacterPresenceCleanupService` (sweep at midnight UTC, over
  every row past the minute-long `StaleAfter`) in the gameplay lobby, and the location block served
  by the friends roster, the player search and the clan roster. 165 tests pass; nothing is applied
  to a database until the migrations run. The deltas from the reference are listed above under the
  design.

  The beat is the reference's 30 s; the window is a minute rather than its 120 s, because the
  client's own cadence is half a minute and a player who is quiet for two beats is gone. What
  changed with the sweep is where it runs: the reader no longer depends on it — `FindLocationsAsync`
  drops a row whose `last_seen` has left `StaleAfter` for itself, so a ghost is off a friend list
  the moment its stamp ages out and the delete is only what reclaims the row — which is what let
  the sweep move off the beat and into the daily pass in `CharacterPresenceCleanupService`. See
  `SCHEDULED_DATABASE_TASKS.md` for the schedule as a whole.

- **Step 1: DONE** (2026-08-01). `V72__chara_presence.sql`, `PresenceService`, hooks in
  `ChannelRegistry`, boot-clear and the periodic heartbeat/reap. `mvn verify` 233 unit / 236
  integration, ten of them `PresenceServiceIT`. No wire change: nothing the client sees moved.
- **Step 2: DONE, then corrected same day.** The first pass only wired `0x4582` wire `0x14` — the
  bare numeric `lobby_id` — leaving `lobby_name`/`game_id`/`game_name`/`lobby_type` zero. Live
  testing found the Friend and Block List lobby column still blank: the client draws `lobby_name`
  as a **string column** (`STRING_F_LIST_LOBBY` / `STRING_B_LIST_HOST`), and the numeric id is a
  *separate* consumer only used for the "move to lobby" jump target. `HostGameController.listRoster`
  now calls `presenceService.locationsOf()` and writes the full five-field block, same as step 3.
- **Step 3: DONE** (2026-08-01). `0x4602`'s five-field tail is served from one bulk join per
  batch. Not-connected players send zeros and empty names, render `"----"`, and still appear —
  search results are not gated on the block.
- **Step 4: DONE** (2026-08-01). The clan member roster (`0x4b54`) is a *third* carrier of the same
  five-field location block — missed in the original staging, found by live testing. `writeRoster`
  in `ClanGameController` now takes `PresenceService` and fills it the same way.
- **Match history's game-type byte fixed the same day, but it is not a presence bug.** `0x4680`'s
  trailing byte comes from `GameService.metPlayers`, which joined `round_report` through `game` and
  `lobby` to find the subtype — and `game` rows are deleted at teardown, so that join returned
  nothing for any match that had actually finished. Fixed to read `round_report.lobby_subtype`
  directly; it was already captured at report time (same reasoning as the `rule` column on that
  table), just never read back out.

### Known gap, not yet fixed: presence answers "connected", not "chosen"

Live testing 2026-08-01 also surfaced a real design gap, tracked in `BACKLOG.md`: `chara_presence`
writes wherever `ChannelRegistry.track()` fires, i.e. as soon as a character authenticates against
a lobby process — not when the player picks that lobby from a menu. For lobbies that double as menu
servers (Automatching, at least), that means a character merely browsing the menu reads identically
to one actually queued. See `BACKLOG.md` § "Presence conflates 'connected to a lobby process' with
'the player chose that lobby'" for the observed case and why it is not fixed yet (needs a real
"entered" signal, which is ELF work).

### A trap step 3 had to avoid

`lobby_type` in a **search** row decodes through `0x8E1110`, which has **no arm 9** and disagrees
with the match-history table at 5 and 6. Reusing `gameTypeLabel` — the helper written for `0x4682`
one step earlier — would have been exactly the illegal cross-packet transfer both schemas warn
about. `searchLobbyLabel` is separate for that reason and says so.

### Two things step 1 got wrong first, kept because they will recur

**`GameServer` had a single periodic-task slot**, held by automatching. It is now a list on the same
single thread, which strengthens the existing guarantee rather than weakening it: no two tasks can
overlap either, not just no two runs of one task. Building that list with `List.of(automatchTask,
presenceTask)` then failed **51 integration tests at server construction**, because `automatchTask`
is null on every non-game lobby and `List.of` rejects nulls before the constructor's own filter sees
them.

**The reaper's SQL could never have compiled.** `where last_seen < now() - make_interval(secs =>
:seconds)` renders through StringTemplate, which reads the `<` as an expression opener and consumes
up to the `>` in `=>`. Reversing it to `where now() > last_seen + ...` fixes it. This is the third
time the project has hit that trap, so it is now a rule in `CLAUDE.md` rather than a third inline
comment.

Worth noting *how* it was caught: the test backdates a row and then reaps. A test that merely called
`reapStale()` on fresh data would have passed against a statement that cannot compile, because Jdbi
only parses on execution.
