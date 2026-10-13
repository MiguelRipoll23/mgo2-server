# The gameplay server: what it does, and how much of it is still a replay

**What this file is.** A map of the running UDP host: the lifecycle of a peer's
join, the briefing gate, the round, the beat, and — the question this file exists
to answer — **which of the bytes a client receives come out of the server's own
state and which are a replay of a capture**. It closes with what the findings
already supports building next — §5 is the list of what is not implemented yet.

Every claim below is read off the code named beside it, and the protocol findings
behind each record are in `docs/protocol/` — `UDP_GAME_CAPTURE.md` for the
byte-level record notes, `DATA_PHASE_DELIVERY.md` for what is gated on a client
record and what is not, `BRIEFING_FLOW.md` for the briefing and the round,
`GAME_SETTINGS_WIRE.md` for the room's rule, map and clock, and
`P2P_CONNECT_FSM.md` for the handshake and the acknowledgement window.

## 1. Where it sits

One process, one UDP port (`GameplayServerPort`, 5731 by default), one **room**.
The room is a row in `games`, published inside a gameplay lobby
(`GameplayLobbyName`, `Free Battle` by default) and named
`GameplayServerGameName` or `server-<port>`. `MatchService` creates it once and
then heartbeats it, so the row survives a restart of the container and a client
only ever joins a room whose heartbeat is inside `GameStaleSeconds`.

The room runs the rule and the map this instance is configured with
(`GameplayServerRule`, `GameplayServerMap`), each named the way the replay
console names it — `TDM`, `Blood Bath`, `n007a` — and mapped to the id a rotation
carries by `MatchRuleUtils` and `MatchMapUtils` (`GAME_SETTINGS_WIRE.md`). A name
neither table holds stops the process at startup rather than opening a round no
client can play.

The host plays as a character of its own (`GameplayServerCharacterName`, derived
from the port when unset) whose identifier is the character id in the room and in
every roster record, and the character is flagged `dedicated` at startup
(`CharacterService.EnsureDedicatedHostSettingsAsync`) — that flag is what makes
the room a *dedicated host* rather than a player's room, and the event subsystem
leases exactly these rooms when it needs a host for a match
(`src/Shared/Domain/Events/EventHostRoomPoolService.cs`), which is why a
host-role room is sized to the event's player capacity rather than a room's own.

Nothing here is a simulation of the game. The host's job, as measured, is to be a
peer the clients can key against, answer their join, gate their round on their
own readiness reports, and keep the channel moving underneath them.

## 2. The lifecycle

### 2.1 Startup

1. The account and its character are published and the dedicated flag written
   (`GameplayServerService.RunAsync`).
2. The host's endpoint pair is registered against the character
   (`GameService.SaveConnectionInformationAsync`), which is what a lobby hands a
   joining client over TCP.
3. The UDP socket binds, the session reaper starts (`PeerSessionService`, 60 s of
   idle), and `MatchService` publishes/heartbeats the room row.
4. On a stop request the host enters **drain mode**: it answers no new handshake
   (`GameplayServerService.FrameDispatch.cs`), keeps serving the peers it has,
   and returns once they have left.

### 2.2 A peer arrives

A joining client sends a handshake; `AcceptHandshakeHandler` records its
character id, counter base (the session key is the two bases XORed), its
advertised endpoint pairs and its capability/mode byte, then answers **the
keep-alive first and the handshake reply second, both pre-keyed** — the recorded
host's counters 0 and 1. The session is answerable at the endpoint that matches
the address the datagram came from when the peer advertised it, and at its first
valid pair otherwise, because behind a router the public pair is often the one
that cannot be dialled back.

From here the peer's traffic is keyed with the session key, carries a fifteen-bit
outbound counter per session, and is marked compressed only when the records in
it were compressed (`GameplayServerService.Outbound.cs`).

### 2.3 The join

The client re-sends its profile until it is answered; `PlayerProfileHandler` is
bound to both identifiers a joiner uses (`PlayerProfile`, `JoinRequest`) and
answers in the pass the profile arrived in:

1. The peer is filed on the room roster with a slot, a session slot, its
   character id, name, clan and endpoint pairs (`RoomRosterService.Register`).
2. The room's roster run is written — an empty head, the host's own entry, one
   entry per joined player, and a close record — **compressed**, then repeated
   without its head, then the trailer. The run is also broadcast to the peers
   already in the room.
3. 2.52 s later the **settings burst** goes out
   (`PlayerSettingsBurstSchedulerService`): the room's rotation, from this
   instance's own settings, and one loadout per player of the room, read from the
   room's rows, and a state blob.
   It is once per *host*, not per joiner — the capture holds one burst in the
   whole round and none for the peers that join later — so it reaches whichever
   peer first had its roster answered.
4. The **sparse phase** starts for that peer alone (`MatchRecordService`): 17
   steps of control records and state blobs at the capture's delays, 42.2 s in
   total.
5. The **round watcher** is armed (`MatchStartRunService.Start`), so the next
   round's opening run reaches this peer whether it reports ready first or last.

### 2.4 The briefing

A client in a briefing repeatedly sends a two-record datagram: a one-byte
presence record carrying the player's own lobby slot and, on the round's payload
identifier, `e1 <byte>` — the readiness report. `PlayerReadyHandler` is bound to
the four payload identifiers
the report can arrive on, records the byte against the peer, and **publishes the
room's ready set back** as one `e0 <byte>` record per reporting player, each
under its own fourth byte. The publication goes out on every report, not only the
first: a client whose report changed nothing is still waiting to be answered.

The round opens when the room is ready, and "ready" is a measure of the room's
membership rather than of a chosen interval:

```
AllReady  =  every peer on the roster has reported at least once
             (PlayerReadyService.AllReady, counted over RoomRosterService.Snapshot)
```

**A room's first player is briefed, not started.** A report from the room's only
member opens nothing (`MatchStartRunService.PlayersAReportOpensFor`): below two
members a room is one player waiting for a game, and that player sits on the
briefing screen until the room's own briefing time ends the wait. No capture
holds a room whose sole player reported — the solo arrival of
`mgo2-game-started-on-briefing` is one of six reporters and its round opens
1.5 s after the last of them ([`BRIEFING_FLOW.md`](protocol/BRIEFING_FLOW.md)
§2) — so this is a decision rather than a measurement, and it is the client's
own contract on both ends of it: a client in a briefing waits either until every
player is ready or until its own briefing timer runs out.

With that gate met, `MatchStartRunService.Open` resolves what the room is running
(`MatchSettingsService`, from this instance's settings), latches the round
(`MatchRoundStateService.Open`, once per round number) and arms the clock. A host
whose room is not published yet opens no round, and the client simply goes on
reporting — which is the state a host with nothing to serve should leave it in.

**The room does not wait for a report that never comes.** A client in a briefing
waits for the host's round *either* until every player is ready *or* until its own
briefing time is up, so the wait is a deadline and not a rule: the first peer's
join and every round's end arm one for the briefing that follows
(`MatchStartRunService.BeginBriefing`), and a briefing that runs out opens the
round for the players that did report — under the identifier set the last of their
reports arrived on (`OpenWhenBriefingEndsAsync`). A briefing no player has answered
opens nothing, and neither does one whose reporters have all since left: a round
is still something a report asks for, and a room nothing has answered has no set
to open one under. The player still to report is served the
round as a late joiner is (§4.1).

### 2.5 The round

**The opening run** is written per peer, five times (once and four repeats), in
one compressed frame: the marker, the payload echoing that peer's own reported
byte, the two lobby-slot arrays — which name the room's own players, each in the
slot it holds and on the side the room's rows put it — the presence answer, the
operations state block and clock, the two-byte operations record, the
rules pair and the close pair (`MatchStartRunFramesUtils`). Everything in it that is measured as an offset
is an offset **from this run**, which is why the beat waits for it. **The clock it
carries is what is left of the round**, not its whole length
(`MatchStartRunService.WriteRunAsync` → `MatchRoundStateService.RemainingUnits`),
so a peer that joins inside a round is handed the round it joined and the beat
behind it counts down from the same moment; at a round's own open the two values
are one, nothing having been spent in the first 125 ms tick.

**The beat** (`MatchRecordService`) then runs per peer for as long as the peer is
there: a frame every 33 ms carrying the rules-roster pair, and, on the capture's
periods, an insertion at its head — the round's **clock, counted down**
(`RoundClockUtils` → `MatchRoundStateService.RemainingUnits`), a state blob
alternating zero/live, or a one-byte control — with the vitals record and the
item-box group in one frame in eight. The clock, the generation (which flips to
the sibling identifier set each round) and the round number are live, and so is
the vitals record's **onset**: it rides from the round's first player vitals
(`MatchRoundStateService.NoteVitalsArrived`), with the capture's own 818-frame
(27.0 s) offset kept as its floor. The rest of the cadence is the capture's.

**The lobby-slot arrays** are a handshake of their own (`TeamArrayService`): the
pair goes out with the run, the client answers `82 NN`/`84 NN`, the host confirms
`81 NN`, and the pair is republished under the next sequence. Only the
confirmation moves the client on, so a host that never confirms gets one answer
and then silence.

### 2.6 The end of a round, and the next briefing

A round ends on whichever of its two limits comes first. The clock is the first:
`MatchStartRunService` waits the round's own time —
`MatchRotationEntry.TimeLimitMinutes`, this instance's `GAMEPLAY_SERVER_ROUND_MINUTES`
for every rule — and then ends it. The other is the round's **kill limit**: with
every kill and death counted (`MatchScoreService`), a side that reaches
`MatchRotationEntry.Tickets` ends the round itself (`TicketsReachedAsync`), which is
`GAMEPLAY_SERVER_KILL_LIMIT` and unset by default, because nothing on this channel
counts kills for a host (§3.3) and a clock-only round is what a room with no limit
to state plays.

Whichever ends it, the round now carries a **result**: `state[2]` of the
operations state block is written behind it as the `0x02` state write
`[0x02, offset, count, values]` the capture shows at every round end, from the
side that won. The match's own result at `state[3]` is not written, because how
many rounds a match is has not been resolved (§5).

Ending a round clears the ready set (`PlayerReadyService.BeginNextBriefing`), so
the next round waits for reports of its own rather than opening on the states of
the round before — under a fresh briefing deadline of its own — and the round walk
steps to the next entry of the room's rotation. It also clears the round's score
(`MatchScoreService.Reset`), which is what makes the scoreboard a per-round board.

## 3. What is live, and what is still a replay

### 3.1 Live

| what | where it comes from |
| --- | --- |
| frame cipher, session key, counter bases, tail digest, per-session counters | the peer's handshake |
| session lifecycle, reaper, roster and ready-state cleanup on a reaped peer | `PeerSessionService`, `GameplayServerService.DropReapedPeer` |
| handshake answer, host identity, advertised/private endpoints | `HostIdentityService` (character id from the database) |
| roster entries, their slots, names, clans, endpoint pairs, run ordinals | `RoomRosterService` + each peer's profile and handshake |
| the ready set: who reported, the byte, and the publication order | the clients' own reports |
| **the round gate, the round number, the rule, the map, the round's length and its kill limit** | `MatchSettingsService` → `MatchSettingsUtils` (this instance's settings) |
| **the briefing's deadline: how long the room waits for a report before opening a round without it** | `MatchStartRunService.BeginBriefing` → the room's own briefing time |
| **the round clock, counted down in real time** | `MatchRoundStateService` + `RoundClockUtils` |
| **the clock each peer's run carries: what is left of the round when that peer's run is written** | `MatchRoundStateService.RemainingUnits` → `MatchStartRunService.WriteRunAsync` |
| which identifier set a round plays under | the report the round opened on |
| the lobby-slot handshake loop | the client's answers |
| **the lobby-slot arrays: the room's own players, each in the slot it holds and on the side the room's rows put it** | `RoomRosterService.Snapshot` + `GameService.GetPlayerTeamsAsync` → `LobbySlotService` → `TeamArrayFramesUtils.BuildPair` |
| relay of every record a client sends that this host has no handler for | `GameplayServerService.FrameDispatch` |
| record acknowledgements and the answer to a numbered record | `PeerAcknowledgementUtils` |
| the room row: name, mode, capacity, ping, heartbeat — and **the rotation, written from this instance's settings** | `MatchService` |
| **the rotation the settings burst carries: this instance's configured rounds, the field walked round when the room names fewer than fifteen** | `MatchSettingsService` → `PlayerSettingsBurstService.RoomRotationBody` |
| **the loadout each player is announced under: its own appearance, its equipped skills, the slot it holds and the side the room puts it on** | `CharacterService` (appearance + equipped skills) and `GameService` (sides) → `LobbyLoadoutService` → `LoadoutFramesUtils.TryWrite` |
| **the vitals each player last reported: health and stamina, kept per player and cleared when it leaves** | `PlayerVitalsHandler` → `PlayerVitalsService`, cleared by the session reaper |
| **the round's score: a death when a player's health reaches zero, a kill and a death when a kill feed names one, and a side's total over the slots the room puts it on** | `PlayerVitalsHandler`/`KillFeedHandler` → `MatchScoreService`, reset when the round opens |
| **the scoreboard each player's own numbers changed for** (`0b <slot+1> <21-byte mask> <values>` on the roster identifier) | `MatchScoreService` → `MatchScoreboardFramesUtils` → `ScoreboardPublishService` |
| **the index byte every roster-identifier record carries: one counter per identifier, consumed in the order the records are written** | `Rooms/RoomRosterService.TakeRunOrdinal`, read by `PlayerSettingsBurstService`, `MatchStartRunFramesUtils.BuildRun`, `TeamArrayService` and `ScoreboardPublishService` |
| **the round's result: `state[2]` written as the operations state write when the round ends** | `MatchStartRunService` (score → `MatchRecordBodiesUtils.StateWrite`) |
| **the vitals record's onset: the round's first player vitals rather than a frame count** | `MatchRoundStateService.VitalsArrived` |

The room's rotation, map and clock were *not* live before this revision — they
were the recorded room's constants — so a host could serve a room whose rows said
one thing and whose round said another. That is what `GAME_SETTINGS_WIRE.md`
revises, and the direction of the fix is worth stating: they are this instance's
own settings now, and the room row is written *from* them rather than read back,
so the row a client is handed over TCP and the round the host plays are one
rotation with one source (`MatchSettingsUtils.StoredRotationJson`). A dedicated
host never receives the client's `0x4312` push that a player-hosted room's
settings come from, which is why a row read here could only ever return what the
host had put there itself. The burst's loadouts followed: their own fields are the
room's players' now, read from the character and game rows, and only the bytes
nothing names stay a recorded body's — so a joiner is told the room it joined
rather than the capture's room.

### 3.2 Replayed

Everything below is a capture's bytes or a capture's timing, and each is a
candidate for §5. The file names are the ones that own it.

| what | replayed from | note |
| --- | --- | --- |
| the sparse phase: 17 steps, their delays and bodies, its 42.2 s close | `SparsePhaseFramesUtils` | the whole pre-round draw is the recording |
| the beat's periods and phases (33 ms, 90/45/181/130 frames, blob alternation, item-box phase) | `MatchRecordFramesUtils`, `ItemBoxFramesUtils`, `BeatFramePlan` | the cadence is the recording, not a simulation; the vitals onset is the exception — it is the round's first vitals, with the recorded 818-frame offset as its floor |
| the control bodies (`02`, `03`, `04`, `25`) | `MatchRecordBodiesUtils` | unnamed on the wire |
| the host's vitals record: always full health and stamina (`fa fa`) | `MatchRecordBodiesUtils.Vitals` | the capture measures it constant while `0x0080` reports deaths, and says not to substitute a player's values into it; the players' own values are read and kept instead |
| the operations state block: an 89-byte recorded vector, whose named offsets the run writes | `MatchStateBodiesUtils` | the two holders, the two capture clocks and the round clock; `state[2]` is opened as `0xff` and written by the round's end; the other bytes, and the last-round result at `state[3]`, are the recording |
| the item-box group's bodies | `ItemBoxRecordBodiesUtils` | the round's opening event pair, then the filler; the later spawn events are not written at all, and §5 item 6 has what the capture settles about them |
| the match-start payload's trailing zeros and length | `MatchStartBodiesUtils` | game1's six-byte form |
| the presence answer (`02 00 00 00`) | `MatchStartBodiesUtils` | the room's players as a bit set, measured growing `02` → `06` → `0e` → `1e` as a room's players report; written here as the one-player body, trailing three bytes **[U]** |
| the close pair's 18 unnamed bytes | `MatchRecordBodiesUtils` | nothing names them |
| the loadout bodies' unnamed bytes — the four at `0x12`, the two before the appearance block, the byte a no-pair region opens with — and the burst's closing blob's value 50 | `LoadoutBodiesUtils`, `PlayerSettingsBurstService` | the named fields are the room's; these are per-player values nothing names, so a recorded body of the player's own shape supplies them |

**How much, in one sentence.** The *channel* is live — who is here, what they
reported, how healthy they are, which round is playing, how long it has left,
who is on which side and what each of them looks like — while the *content of
the round* is still the recording: the beat's cadence and the bodies of
everything the client sees during it. A client joining today is told its own
room's rotation, its own room's slots, teams and loadouts, and then shown a round
that is genuinely timed.

### 3.3 What a peer sees of another peer

Nothing is simulated between peers, but nothing is swallowed either: a record
this host has no handler for is relayed unchanged to the room's other established
peers — both below and above the gameplay tick threshold — which is how one
client sees another move. The readiness report and its companion are relayed by
their own handlers, so the relay is a decision there rather than the default.

**What the relay gives a scoreboard, and what it does not.** The scoreboard the
live captures carry is the *host's*: every scoreboard record and every kill feed
in all three captures travels host → client, and a client sends only its own
position and state. The host is therefore the side that counts, and what it can
count from is what its players report — a death when a health reaches zero, and a
kill when a kill feed arrives. A hosted room whose clients write no feed still
has its deaths, and the scoreboard published from them is the host's own reading
rather than a replay of the capture's numbers.

## 4. Behaviour worth knowing

### 4.1 A second joiner arriving while a round is playing

**It does not get a briefing for that round — it is served the round already in
progress.** Concretely, with player A and player B:

1. A joins. `AllReady` is true the moment A's report arrives, because the roster
   holds exactly one member and it has reported, so `Open` opens round 1
   for a one-player room.
2. B joins afterwards. `RoomRosterService.Register` adds B, `AllReady` becomes
   false, but the round is already open: `MatchRoundStateService.Open` returns
   `null` for a second open and no second round starts.
3. B is served the join (roster run, burst, sparse phase) and then **the current
   round's opening run and the beat**, because its round watcher waits on a
   round that is already open and writes the run for it. B is in the round, not
   in a briefing.
4. B's own briefing arrives at the **next** round boundary: the round ends, the
   ready set is cleared, and the round after that waits for A *and* B to report.

That matches the captures: `mgo2-game-started-team-selection-selected-blue-then-game.pcapng`
is a join into a round already playing, and no capture shows a room holding a
round open while a late peer is briefed on its own.

The one rough edge that path had is closed: B's run carries **what is left of the
round** and not its whole length (`WriteRunAsync` → `MatchRoundStateService.RemainingUnits`),
so a peer joining six minutes into a seven-minute round is handed the minute that
is left, which is what the beat behind it counts down from. It is also how the
client is told it is in a round rather than a briefing — the client reads that
from the records it is sent, and the run's own clock is the one that says so.

### 4.2 A player who never reports

The gate is "every roster member has reported", and a member is removed only when
its session is reaped (60 s of silence). A peer that joins, stays alive and never
reports would hold the next round shut for as long as it is there — so the gate is
a **deadline** as well as a count: the room's own briefing time, armed when the
briefing begins, after which the round opens for the players that did report and
the silent peer is served it as a late joiner is. That is the client's own
contract, and the reason it is a deadline rather than patience: a client in a
briefing waits either for every player to be ready or for its own briefing timer
to end, so a room that waited longer would leave the players it did hear from past
the end of their timer with nothing to play.

**A room of one is the same shape at the other end.** Its single player has
reported, so the count is met — and the round still waits, because a report from
the room's only member does not open one (§2.4). The deadline is what ends that
wait too, so a room's first player is on the briefing screen for the room's own
briefing time and then in the round, while a second player's arrival turns the
room into one whose reports can open it.

No capture shows the deadline firing, because every captured client does report;
what the live experiments *did* produce is a client that sat in its ready state for
two minutes while a round it had been sent went unrepeated
(`DATA_PHASE_DELIVERY.md`), which is the shape a gate with no deadline holds in
place.

### 4.3 What ends a round

Three things now, in the order they were measured. The **clock** is first and
always was: the round's own time, counted down. The **kill limit** is second:
with the host's own score kept, a side that reaches the limit this instance is
configured with ends the round (`MatchResultService.TicketsReachedAsync`) and its
win is written. A **goal** is still invisible on this channel as a cause — the objective
is scored in the round's state block and the end-of-round report travels over the
lobby's TCP channel — so `End(reason, result)` remains the seam a goal would
call, and no round ends early for one here.

## 5. Suggested followups

What is left to build, in the order a player would notice it. Nothing below is
implemented; everything the sections above describe is, so those are the account of
what this host does and this is the account of what it does not. An item blocked on
a reading rather than on a decision says so, and the reading is in §7.

1. **A match rather than a round.** The match's own result at `state[3]` is not
   written, no round report is stored (`game_rounds` is written by a lobby's own
   handlers, not by a dedicated host), and the end-of-match report the lobby reads
   over TCP is not produced. What blocks all three is one question, and it is a
   choice rather than a measurement: how many rounds a match is, and what the host
   reports once the last of them ends. The round's own result is written and
   measured (`state[2]`, `BRIEFING_FLOW.md` §4).
2. **A rotation rather than one round.** The room's rotation is this instance's
   configuration and states one round, so the round walk (`MatchSettings.Round`)
   plays the same entry every time. A configured list of rounds — and the room's own
   `games.current_game` column, which exists and is unused — would let a room play a
   rotation, which is the shape a player-hosted room's `0x4313` reply and the
   burst's fifteen-triple field already have room for.
3. **The briefing countdown.** The room's briefing time is what its deadline is
   measured with, and it is sent to no client. The two candidates on the wire are
   the six-byte `0x0b` operations record that counts 60 down over a briefing and the
   operations clock's 128-unit (16 s) countdown before a round; both are **[U]** in
   their unit, so this one wants a capture with a visible briefing before it is
   written.
4. **A score the host computes.** The scoreboard's `score` bit moves only when a
   peer states a value (§7), and the kills behind it come from a kill feed or a
   peer's own delta: a room whose clients send neither has neither. Both are the
   host's own counting to derive, from the deaths it already reads, and the formula
   is **[U]**.
5. **A shorter sparse phase.** The 17-step, 42.2 s close is the only thing that
   makes a join take that long, and nothing about the client gates on it — the phase
   is a keep-alive, and the ready report is what opens the round. Ending it when the
   peer reports, or when the round opens, would cut a join's wait without touching
   the round.
6. **The item-box group's later spawns.** The round's opening event pair and the
   filler are written; the later spawn events are not. The capture that holds them
   is `joined-when-red-team`, and it settles the *trigger* and not the body: 71
   later events, each a `02 <nine bytes>` position record and a `03 00 00 00 21 00
   21 00 21` tail, written under the round's own item-box identifier (the counter's
   next two values) within 0.0–4.3 s of a death. The tail is byte-identical in all
   71 and the positions are only 57 bodies, so the record names one of the map's
   own box points and not the death; a host would need that point from somewhere
   this channel does not carry, so the group stays the opening pair and the filler.

## 6. Where the code is

| file | what |
| --- | --- |
| `GameplayServerService*.cs` | the port, the sessions, the frame pipeline, dispatch, relay, outbound |
| `Commands/Peer/Connection/*` | handshake, keep-alive, join probe |
| `Commands/Peer/Room/*` | the profile (join), the readiness report, its companion |
| `Match/MatchService.cs` | the room row (written from this instance's settings) and its heartbeat |
| `Match/MatchSettings{Service,Utils}.cs` | what the room runs, from this instance's settings |
| `Match/MatchRuleUtils.cs`, `MatchMapUtils.cs` | the rule and map names and the ids they map to, ported from `rpdt.js` |
| `Rooms/RoomRosterService.cs` | who is in the room, and the roster run |
| `Rooms/PlayerReadyService.cs` | who has reported, and the publication |
| `Rooms/PlayerSettingsBurstService.cs` | the post-join burst |
| `Rooms/TeamArrayService.cs` | the lobby-slot handshake |
| `Rooms/LobbySlotService.cs` | the room's own players and sides, as that pair names them |
| `Rooms/LobbyLoadoutService.cs`, `LoadoutFramesUtils.cs` | the room's own players' appearances and skills, and the loadout records that name them |
| `Gameplay/PlayerVitalsService.cs`, `Commands/Peer/Gameplay/PlayerVitalsHandler.cs` | the health and stamina each player last reported, and the death when health reaches zero |
| `MatchRecords/MatchScoreService.cs`, `MatchScoreboardFramesUtils.cs`, `ScoreboardPublishService.cs` | the round's score, the scoreboard body, and its publication |
| `MatchRecords/MatchResultService.cs` | the round's winner, the kill-limit check, and the result write |
| `Commands/Peer/Gameplay/KillFeedHandler.cs` | the operations stream's kill feed, and the score it feeds |
| `MatchRecords/MatchStartRunService.cs`, `MatchStartRunService.Runs.cs` | the round opener, its briefing deadline and its clock, and the per-peer run watcher |
| `MatchRecords/MatchRoundStateService.cs`, `RoundClockUtils.cs` | the room's round, and how much of it is left |
| `MatchRecords/MatchRecordService.cs` | the per-peer beat |
| `MatchRecords/BeatFramePlan.cs`, `MatchRecordFramesUtils.cs`, `SparsePhaseFramesUtils.cs` | the frames the beat and the sparse phase write |
| `MatchRecords/ItemBoxFramesUtils.cs`, `MatchStateBodiesUtils.cs` | the item boxes and the state vector |

## 7. Open

* **[U]** why the captured burst writes the first joiner's loadout body **twice**,
  byte for byte identical, beside the host's own. The live burst writes each
  player of the room once; whether the second copy is a repeat the peer needs,
  as the roster run's second copy is, nothing measured says.

* **[U]** the state blob's only varying field (a 16-bit value that runs 100, 0,
  94, 98, 99, 50 through a round). `joined-when-red-team`'s 754 steady blobs are
  all zero, so the live round's own reading of the field is the constant one —
  what moves it in the other captures is still unestablished.
* **[U]** the rotation triple's third byte, whose meaning is still unestablished;
  zero is what fifteen of the sixteen recorded triples hold and zero is what this
  host writes. The u8 timer bank at `0x166` — SDM, INTERVAL, DM, SCAP, RACE — is
  unread as well, and no longer anything this host needs: a dedicated host's round
  length is its own setting for every rule (`GAME_SETTINGS_WIRE.md`).
* **[U]** the `0x0b` briefing countdown's unit and its `0x40`/`0xc0` flag.
* **[U]** the 18 unnamed bytes of the close pair, and the trailing zeros of the
  match-start payload.
* **Measured: one counter per roster identifier, and every record under it takes
  a value.** `joined-when-red-team`'s live rounds carry every `0x1001` record in
  one ascending sequence — the lobby-slot arrays take the pair 23 and 24, then
  the pair 28 and 29, and the scoreboard deltas take 25, 26 and 27 between them —
  so the run's measured 7 and 8 are *where the counter is* after a join's own
  records and its burst, not values to pin, and a republished pair takes the next
  two. `RoomRosterService.TakeRunOrdinal` is the counter, seeded at a join and
  consumed by the burst, the run's pair, each republished pair and each scoreboard
  delta.
* **Answered: the round's own records are not the only thing under that counter.**
  The same capture carries 2 606 `0x1001` records against 57 and 38 under the two
  payload identifiers, so a host that numbered the payloads and the roster apart
  would repeat values inside the one stream the peer answers.
* **[U]** the pair's publication at a round boundary, and why. game1 un-marks
  every slot at t+442.7 — 396 s into its round, with players still in the room —
  re-marks the room with `e0` sides at t+449.0, and names the sides at t+517.5.
  This server marks the room's members on every publication and drops a slot only
  when its player is reaped, so it never writes that empty or `e0` pair.
* **[U]** what ends a round that reaches its kill limit, because no capture holds
  one. This host ends it on the kill limit it is configured with, counted from the
  score it keeps; the write it produces (`state[2]`) is the measured shape, and the
  round it ends is not a measured one.
* **Measured: what a host writes when a round decides its match.**
  `joined-when-red-team` ends its third round at t+754.040 with
  `02 02 02 02 02` — a *count of two*, so `state[2]` and `state[3]` together, both
  `2` — and follows it 22.75 s later with `02 04 03 00 02 02`, three values at
  `state[4]` (a `0`, then the match's two `2`s). The first two rounds write
  `02 02 01 02`, the count-one form this host already sends, so the count-two write
  and the triple behind it are the *match's* and arrive once the match is decided.
  What `state[4]`–`state[6]` name is **[U]**; the shape and the order are not.
* **Measured: the scoreboard's index byte is the roster counter's next value.**
  Its fourth byte is not its own numbering and not the roster run's: in
  `joined-when-red-team` the 2 606 `0x1001` records carry one ascending sequence,
  and the scoreboard's deltas take the values between the lobby-slot arrays' pairs
  (25, 26 and 27 between 23–24 and 28–29). `ScoreboardPublishService` takes one
  per delta; the parser still reads a scoreboard's slot from its body, never from
  the index.
* **[U]** the formula behind the scoreboard's `score` bit: measured, it climbs
  3, 7, 13, 20, 28 as a player's kills step 1…5, which is not a function of the
  kills alone, so the host writes a score only when a peer states one.
* **[U]** whether the host can attribute a kill at all. Every kill feed in every
  capture travels host → client, and a client sends only its own position, state
  and health — the kill a feed names is the host's own count and not a client's
  report. This host counts a death from the vitals it reads and takes a side's
  kill total from the opponents' deaths until a feed names one.
* **Measured: a kill is two records, and the second one is unread.**
  `joined-when-red-team` carries 385 `0x09 <killer> <victim>` records and 188
  `0x26 <slot> <value>` ones, the two travelling together (a `09 02 01` beside a
  `26 02 06` in one datagram at t+160.298) and each resent until answered — which
  is why the counts are roughly double the kills the capture holds. The second
  record's `<slot>` is the killer's own and its `<value>` never leaves 3…7, so it
  is a per-kill number the host keeps; nothing measured says which number, and this
  host writes no `0x26` record rather than a value it cannot derive.
* **[I]** how a client in a briefing decides its wait is over: it is waiting for
  the host's round, and it waits either until every player is ready or until its own
  briefing time is up. The second half is what the room's deadline is set to (§2.4),
  and what a client does at the end of its own timer in a room that has not opened a
  round is not captured.
