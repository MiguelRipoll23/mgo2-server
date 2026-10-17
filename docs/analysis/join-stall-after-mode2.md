# The join that stalls with the client in the guest role

**What this file answers.** `P2P_CONNECT_FSM.md` §7.1b concluded that the live
join stalled because the client had resolved itself into the **host** role
(`mode = 1` in its handshake) where the reference client is a guest
(`mode = 2`), and that *"until [a client that resolves to a guest] does, no
server-side change is indicated, because the bytes this server writes are not
distinguishable from the recorded host's."* The capture of 2026-10-15
(`myserver2.pcapng`) holds a client in the guest role, and the join still
stalls: the condition is met and the explanation does not survive it. This file
records what that capture measures, what the reference captures do that this
host did not, and what was changed because of it. Every time in it is seconds
from the first datagram of the flow it names.

## 1. The client is a guest, and the join still does not finish

Decoded with `tools/map_roster_fields.py` (the handshake fields at the offsets
`docs/protocol/UDP_JOIN_FLOW.md` §3 proves them at):

| capture | handshake | PeerIdentifier | Capability | **Mode** |
| --- | --- | --- | --- | --- |
| reference client (`mgo2-game1`) | joiner | `0x0001232f` | `0x02` | **`2`** (guest) |
| `myserver.pcapng` (2026-10-15, earlier run) | joiner | `0x00000002` | `0x03` | `1` (host) |
| **`myserver2.pcapng` (2026-10-15)** | joiner | `0x00000002` | `0x03` | **`2`** (guest) |
| this host, both runs | host | `0x0000001a` / `0x00000020` | `0x02` | `1` (host) |

So the client of `myserver2` **is** the client §7.1b asked for, and its exchange
still stops short of the reference's: it never sends its own entity records and
never spawns, and the user's client shows no briefing. The client-side role is
therefore not the gate, and the difference has to be in what the two hosts
write.

## 2. The request this host never answers

The client's own records in the new capture are heard in one shape no answer
follows: a **four-byte body opening `0x83`**, on the roster identifiers
(`0x1001`/`0x9001`), carrying the array sequence byte the client last saw in its
second byte. Every reference capture holds it, and every reference host answers
it:

| capture | the client's request | when | the answer |
| --- | --- | --- | --- |
| `mgo2-game1` | `0x1001 83 f3 e0 7f` | t+70.226 | none (re-sent at t+72.566) |
| | `0x1001 83 f3 e0 7f` (re-sent) | t+72.566 | +0.859 s |
| | `0x9001 83 f4 00 07` | t+197.859 | +0.899 s — the player's own entity block |
| `mgo2-game2` | `0x1001 83 00 e0 7f` | t+76.539 | +2.447 s |
| | `0x1001 83 01 01 07` | t+111.542 | +0.325 s |
| `mgo2-game-started-on-briefing` | `0x1001 83 00 e0 7f` | t+92.7 | +0.4 s |
| | `0x1001 83 01 00 07` (×2) | t+200.5, 202.9 | +0.4 s |
| `mgo2-started-on-solo-game` | `0x1001 83 54 e0 7f` | t+98.665 | +0.362 s, +2.740 s |
| `mgo2-started-briefing-then-solo-game` | `0x1001 83 7e e0 7f` | t+87.469 | +0.648 s |
| | `0x9001 83 7f 01 07` | t+177.435 | none (re-sent at t+179.556) |
| | `0x9001 83 7f 01 07` (re-sent) | t+179.556 | +0.935 s → the client's own records at t+181.578 |
| `mgo2-game-joined-when-red-team-then-briefing` | `0x1001 83 03 e0 7f` | t+108.0 | answered |
| **`myserver2.pcapng`** | `0x9001 83 f3 00 07` | **t+73.487** | **none — and no re-send** |
| **`myserver.pcapng`** | `0x9001 83 f3 00 07` | **t+71.8** | **none — and no re-send** |

Three things are read off the table:

* **It is a request, not a report.** A copy that draws no answer is re-sent
  (`mgo2-game1` at 70.2 → 72.6, `mgo2-started-briefing-then-solo-game` at
  177.4 → 179.6) and the second byte climbs by one with every re-send
  (`f3`, `f4`, `f5` in game1; `00`, `01`, `02` in game2), which is the
  reliable-send retry the client uses. Both live captures hold **exactly one
  copy and no answer**, which is a client waiting on a host that writes none.
* **The answer is the room's own state, and it has a shape.** Across the
  captures its members are, in this order, the lobby-slot array pair under the
  sequence the request named (`85 NN` / `86 NN`, 26 B each), the requesting
  peer's own entry (25 B opening `07 12 00 04`), and the roster close
  (`07 00 00 00 00 00 03`, 7 B) — one frame, written 0.9 to 3.2 s after the
  request. The entry is measured in §4; the pair is the team array under the
  host's own tag and the occupancy array under the roster's. In the round the
  answer also carries the player's own entity block (`mgo2-game1` t+197.859:
  state, health, position, incapacity, weapon, aim under `0xd9`–`0xdf`).
  **None of the five captured answers carries a roster entry or an empty
  `0x5001` head**, which the roster run does.
* **It is answered in the pass that received it**, 0.3 to 2.9 s after the
  request, so nothing in the exchange waits on a clock.

## 3. The report a round does not answer

`MatchStartRunService` held a round open until the room held two players, on the
reading that a room's first player is briefed rather than started. The captures
measure the opposite of the delay it caused:

| capture | the client's readiness report | the round's run |
| --- | --- | --- |
| `mgo2-started-briefing-then-solo-game` | t+173.438, once | t+174.371 (**+0.93 s**) |
| `mgo2-game2` | t+107.978, once | t+109.120 (**+1.14 s**) |
| `myserver2.pcapng` | t+49.496, then **52.031, 54.890, 57.503, 60.215** | t+60.530 (the briefing deadline, **+11.0 s**) |

A report a capture answers is sent **once**; a report left unanswered is
re-sent every ~2.5 s until it is. The gate was therefore dropped, and a room
whose clients have all reported opens its round then, however many of them there
are — the briefing's own deadline still opens one for a player who never
reports.

## 4. The 25-byte entry, and what is still not written

**The entry is four measured fields and a recorded tail.** Read over the ten
copies the captures hold, the record is `07 12 00 04` followed by
`<index> <slot> 00 <slot> <slot> <team>`:

| capture | index | slot | team | tail (14 B) |
| --- | --- | --- | --- | --- |
| `mgo2-game1` t+194.042, t+565.467 | `00` | `0f`, `06` | `0` | `ff 3f 84 6d …`, `01 c0 78 ec …` |
| `mgo2-game1` t+73.425, t+396.046 | `01`, `03` | `14`, `0b` | `0`, `1` | `01 c0 f8 a7 …`, `01 c0 d4 ad …` |
| `mgo2-started-on-solo-game` t+101.405 | `01` | `03` | `0` | `fe 7f b9 fd …` |
| `mgo2-started-briefing-then-solo-game` t+180.491 | `00` | `03` | `0` | `ff 5f 0c fe …` |

The **slot** is the raw key of the room's 24-entry player table — the same key
the occupancy array of the same frame writes `07` at — and not the key plus one
the join's loadout record carries. The **team** follows the room: the record for
`mgo2-game1`'s slot `0b` is written `01` only after the arrays put that player on
side one. The **index** is unresolved: it runs 0–3 down one room's copies,
while a single-entry answer carries `00` in one capture and `01` in two, so
neither the entry's place in a run nor the room's population explains it. It is
written as `0` and no meaning is claimed. The **tail** moves per player and per
rejoin — `mgo2-game1` writes slot `06` as `ff 5f 0c fe …` at one roster position
and `01 c0 78 ec …` after re-joining under the same slot — so it is a per-player
value this host has no source for and is carried from the recording.

**Two shapes are still recorded rather than written:**

* a **24-byte loadout header**, `0b SS 02` and zeros, where `SS` is the player's
  own key (`0x02`, `0x04`, `0x06`, `0x15` across captures);
* in a round, the requesting player's **own entity block** — state, position and
  aim, which only a host that simulates the player has.

One further difference was measured and is now written:

* **The round's opening run was short.** `myserver2` wrote eleven records where
  every capture writes fourteen: the twenty-two-byte `07 0f` record and the two
  fifty-four-byte `07 2f` records the captures write there were absent, as were
  the same three records the reference hosts write through a briefing. They are
  written now, from the recorded bodies — which two captures that share no
  session hold byte for byte — in the run and on the briefing tick;
  `docs/analysis/briefing-room-state.md` carries the measurement.
* **The roster answer is written on the first copy of the profile**, where
  `UDP_JOIN_FLOW.md` §7 measures the reference host answering the **re-sent**
  copy (`mgo2-started-on-solo-game`: the joiner re-sends at t+8.780 and the
  roster follows at t+9.021). This host answers at t+0.204, 73 ms after the
  first copy, and its client never re-sends at all — the outcome
  `P2P_CONNECT_FSM.md` §7.1e describes for an answer that lands early. **The
  first copy is held now and the re-send is answered in the pass that receives
  it** (`RosterAnswerService`), with the first copy answered anyway if the
  client never re-sends.

## 5. What was changed

* `TeamArrayFramesUtils` recognises the four-byte `83 …` request and reads the
  sequence it names.
* `TeamArrayService.EnterRoomAsync` answers it with the room's own state in the
  pass that received it: the lobby-slot array pair under the named sequence, the
  requesting peer's own 25-byte entry, and the roster close, in that order. The
  entry is built by `RoomPlayerEntryBodiesUtils` — §4's four fields written into
  a recorded body — and the profile echo and the empty `0x5001` head the answer
  used to carry are gone, because no captured answer holds either.
* `PlayerProfileHandler` routes that body there instead of dropping it as a
  profile with no name, which is what it used to do (its own log line:
  *"carries no profile; not answered"*).
* `MatchStartRunService` opens a round on the reports of the room's own clients,
  with no minimum population.
* `RosterAnswerService` holds the first copy of a peer's profile and answers its
  re-send in the pass that receives it, falling back to the first copy once the
  captures' 1.93–2.11 s retry interval has passed.
* `MatchRecordBodiesUtils`, `MatchStartRunFramesUtils` and
  `BriefingCountdownService` write the room-state records the captures carry:
  `docs/analysis/briefing-room-state.md`.

## 6. Limits

**Nothing here is verified live.** The change is measured against the captures
and the build and the codec tests pass, but no client has joined the built host:
what a client does with the answer is what would close this, exactly as
§7.1b said of the promotion gate. The second live run (`myserver3.pcapng`)
reached the answer — the client sent its `83 f3 00 07` at t+73.630, was answered
in 176 ms and acknowledged all four records — and never sent its own player
block, which is what §5's change to the answer is measured against: the reference
writes the entry the answer lacked, and its client's own records follow 0.9 s
later (`mgo2-started-briefing-then-solo-game`, t+180.491 → t+181.578).
**Absence of acknowledged records is not absence of a client**: acking the answer
is not the same as being placed by it, and the tail of the record is still a
recording's. The two unwritten shapes of §4 remain the next candidates.

## 7. `myserver4.pcapng`: the join finishes, and the round it opens ends in a black screen

The third live run is the first one whose client **plays**. It ran the image the
pin named (`3d148a9`, §5's answer), and this is what it measures — every time in
it is seconds from the flow's first datagram, and the server's own log is the pod
that served it (`kubectl logs -n mgo2 mgo2-gameplay-1-*`, whose clock is UTC).

| t | the run |
| --- | --- |
| 0.122 | the client's profile, first copy |
| 2.143 | its re-sent copy → the roster answer, 4 records |
| 48.745 | its presence record `8a5c 01 de` |
| 51.288 | its readiness report `1ade e1 01` |
| 51.377 | the host's run: arrays, clock 90 B, two entries, close, `5ade`, payload `1ade e0 01 …` |
| 51.650 | **the client opens its own round** (`5ade`) and streams its beat |
| 64.4 | its room request `9001 83 f3 00 07`, answered in the same pass |
| 80.9 | the beat's first vitals record |
| **91.093** | **the client's last datagram**; the server keeps writing for 60 s and reaps it at 151.1 |

So §5's change is confirmed live: the client is answered, claims the room, reports
itself ready, opens the round and acknowledges `1a54`, `1a50`, `1a5a` and `0a61`
for forty seconds. What the user saw at 91 s is the round **fading out with the
screen black behind it, where the fade should have been into a briefing** — and
the client never sent another datagram, which is a client that stopped rather than
one that is waiting for a record: a client waiting on this channel re-sends (§2).

### The slot the room put the client in, and the slot the beat named

Three independent places in the capture and the log name the client **slot 1**,
and none names it slot 0:

* its presence record's byte is `01`, which is the player's own lobby slot
  (`PlayerPresenceHandler`, `BRIEFING_FLOW.md` §1);
* the occupancy array the run carries is `86 f3 7f 07 7f …` — index 0 is the
  host's own sentinel `7f` and index 1 is `07`, taken;
* the run's payload echoes `e0 01` — the report's own byte, `01`.

`RoomRosterService` numbers that client `SessionSlot` 1 as well
(`FirstJoinerSessionSlot`); the `slot 0` in its log line is the member's index in
the roster list and not its key in the player table.

**The beat wrote a different player.** Every vitals record of that round went out
as `0x090c`, the recorded capture's constant — and `0x090c` is the key **20**'s
value in the measured per-slot table (`0x0844 + 10 × slot`): the client was told
30 times a second about the health of a player the room does not hold. The rule
is measured over every capture that carries a pair: `mgo2-game1`'s five occupied
slots `4`, `6`, `11`, `15` and `20` draw `0x86c`, `0x880`, `0x8b2`, `0x8da` and
`0x090c`, `mgo2-game-started-on-briefing`'s slots 1–6 draw `0x84e`–`0x880`, and
this client's slot 1 draws **`0x084e`** (`PlayerVitalsFramesUtils`).

The beat adds the record 27.0 s after the run — 80.9 s here — and the client's
last datagram is ten seconds later. That is the whole of the change below: the
vitals record's identifier is now the peer's own slot, and the round's set still
decides its two forms (`0x084e` / `0x04e`), because the recorded set's value is
what moved between those rounds in the captures.

**Nothing here is proven to be the black screen.** The record is the one field of
the beat that is measured to be per-player and that this host still wrote as a
recording's constant, and the client goes quiet inside the window it is written
in; whether a client reading health for a slot it does not hold is what stops it
is **[U]**, and what would close it is the next run: the same join, with the beat
naming slot 1, and a capture that shows whether the client comes back from the
fade.

## 8. `myserver5.pcapng`: the vitals fix is live, and the silence is the quit

The fourth live run served the image the pin named for it (`98cca55`), which is
§7's change, and the same client with the same room: TDM on Blood Bath, one
7-minute round, opened at t+53.2 on the client's own report. Every time below is
seconds from the flow's first datagram.

| t | the run |
| --- | --- |
| 53.223 | the host's run: arrays at sequence `f3`, the peer's own entry, the close, the clock at 3359 of 3360 units |
| 53.4–54.4 | the client opens its own round (`5ade` ×6) and streams its beat |
| 75.3 | the client's room request `9001 83 f3 00 07`, answered in the same pass, all four records acknowledged |
| 83.0 | **the beat's first vitals record, `0x084e`** — slot 1's own value, the fix §7 asks for |
| 105.444 | the client's last datagram |
| 165.4 | the server reaps the peer after 60 s of quiet |

**So the vitals fix is live and the client still ended in the black screen**, and
what the capture times is not the client: the client's own log (`RPCS3.log`, the
emulator's, whose clock runs `09:54:15` at its start) has the game asked to exit
at `1:27:23.4`, which is `t+103.4` on this capture's clock — the last datagram
lands 2 s later, inside the exit. **The client's silence in this capture is the
user closing the emulator, and §7's reading of `myserver4`'s 91.093 s has the same
shape**: nothing in the client's stream stops by itself, so a capture cannot time
the fade. What the client's log does time is the round: the patch's prints and the
stage's own data at its open, a text-input (`cellOskDialog`) dialog opened `1:26:34`
— 2 s into the round — and dismissed at `1:26:39.7`, the map's music
(`env_n007a_02.bgm`) at `1:26:40`, and then nothing logged until the exit.

### The one field of the beat written an order of magnitude too often

Measured per round, the item-box group's cadence is not the recording's:

| capture | first 60 s of the round | shape |
| --- | --- | --- |
| `mgo2-game1` | 38 frames, 113 `0x1a50` | five bursts at 2.36, 4.84, 9.97, 12.50 and 14.88 s after the run, then **nothing** until the round's next event |
| `mgo2-started-briefing-then-solo-game` | 40 frames, 59 `0x1a50` | the same shape: four bursts inside the first 8.4 s, then nothing until the round's end at 96.0 |
| **`myserver5`** | **388 frames, 418 `0x1a50`** | **the beat's 8-frame period, for the whole round** — one group every 0.29 s, 346 groups in 100 s |

The client answers every one of them (`0x5a50`/`0x5a5a`, 364 and 360 of the 418
and 414 the host wrote before its last datagram: 1:1), so the channel carries
roughly 7 records a second in each direction that no reference host writes — and
the streams the client's own window is drained by are the ones the recording
leaves quiet between its opening and its events. `ItemBoxFramesUtils`
(`ItemBoxPeriodFrames = 8`, `ItemBoxPhaseFrames = 6`) is the writer, and the
opening burst's own bodies are right: its two spawn pairs and the filler are the
capture's. What the period does is repeat the filler for the rest of the round.
**[V]** on the counts; **[U]** on whether a client dies of it.

## 9. `myserver5.pcapng` against `started-on-briefing`: the round's one structural difference is the item-box group's window

The briefing flow is the one this host is being built for — a client arrives into a
briefing, waits there, reports itself ready, and the round opens — so the reference
is `mgo2-game-started-on-briefing` (its roster run at t+8.1, its round at t+56.1,
48 s of briefing) rather than the post-round briefings of game1 and game2.

**The briefing's own rates are close, but its record set is not — §10 corrects the
first reading below, which compared rates and not records.** Over that capture's
briefing window
the host writes the rules pair 9.8 times per ten seconds under each class, a
`0x0002` state blob every 8 s, a `0xd001` control every 5 s and the room's count
`0x1258 00 01` beside the join — and this host's 51 s briefing writes the pair at
0.8 records a second, eight blobs, twenty-three controls and `0x1a58 00 01`. The
rates are the same order and no family is missing on either side.

**The round's beat is the difference, and it is one family.** First 60 s of each
round, host to client, by identifier:

| identifier | reference | ms5 |
| --- | --- | --- |
| item boxes (`0x1250`/`0x125a`) | 164 | **418** (`0x1a50`/`0x1a5a`) |
| rules pair (`0x0261`/`0x0a61`) | 124 | 3326 |
| state block (`0x1254` 90 B) | 9 | 5×18 records at the run |
| publication (`0x12de`/`0x1ade`) | 6 | 6 |
| the client's own record | none in 60 s | none |

The pair's rate differs because that capture's beat writes the pair alone twice a
second per class where this host writes the same pair in every one of its 30
frames a second — a second reading of the same divergence, since the reference's
`0x1250` count is *records*, and 164 of them over 60 s is one group every 0.73 s,
not the one frame in eight this host writes (418 over 60 s, one group every
0.29 s).

**And the reference's groups are bursts, not a cadence.** The 244 records
`started-on-briefing` writes in its 230 s round fall in six buckets: 42 and 48 in
the round's first ten seconds, **nothing at all between +10 s and +55 s**, 74 in
the 55–60 s bucket, nothing from +60 s to +90 s, then 38, 4 and 38 in the 90–105 s
buckets. `myserver5` writes 34–36 in *every* five-second bucket for the whole
112 s round — the beat's 8-frame period, never quiet. **[V]** on the counts.

So `BeatFramePlan.CarriesItemBoxes` is bounded now by the round's opening window
(`ItemBoxOpeningFrames`, 303 frames, ten seconds at the beat's 33 ms frame), which
is what all three captures agree on: the group is the round's own event and the
beat behind it carries none until the round's next one. The events themselves
remain the live round's and are still unwritten, as `ItemBoxFramesUtils` says.

**Not this change, and worth recording.** The client's own tick block
(`0x0861`…`0x0867`: state, position, weapon pair, aim) is what a reference client
streams once it is in a match — `mgo2-game1`'s from 1.4 s *before* its round opens
and `mgo2-started-briefing-then-solo-game`'s from 6.1 s *after* its host's first
health record — and this host's client never sends it. The host is not what is
missing: in the briefing-flow capture the host writes only the peer's health
record (`0x0862`) and never the block, so the block's absence in a live capture is
the client's own state and not a record a host owes it. The health record's onset
is a recording's schedule and not a rule: measured against each round's own open
it is +4.9 s (`joined-when-red-team`), +6.1 s (`started-briefing-then-solo`),
+27.0 s (game1) and +54.5 s (game2), so this host's 27 s is inside the measured
range and is left as it is. **[V]** on the onsets.

## 10. The briefing carries the round's own records, and the briefing reference carries none

§9 compared the briefing by *rates* and concluded no family was missing on either
side. Compared by *record*, the two briefings are not the same room state at all —
and this is the shape of the symptom the user reports, "the briefing screen is
never reached": the client is told a round exists before the briefing has even
been shown.

Both windows are a fresh room's first briefing — the roster run to the round open,
48 s in the reference and 51 s here — and both are host → client:

| records written during the briefing | `started-on-briefing` | `myserver5` |
| --- | --- | --- |
| `07 0f …` room-state record (22 B) | **0** | **21**, from +0.18 s, one every ~2.5 s |
| `07 2f …` close pair (54 B × 2) | **0** | **42**, from +0.18 s |
| roster close `07 00 00 00 00 00 03` | **0** | **2** |
| settings payload `0b <slot> …` | 7 | 3 |

The first three are the round's own close triple: the piece `BRIEFING_FLOW.md` §2
lists inside a round's opening run, and the piece §5 of this file added to the run
*and* to the briefing tick. Its evidence for the briefing tick was
`mgo2-game-joined-when-red-team-then-briefing`, whose briefing is the briefing of a
room that had **just played a round** — that room's briefing legitimately carries
the round's leftovers (its 90-byte state block and its clock are in the same
capture). A room that has **not** played a round has no round to carry, and
`started-on-briefing`, the join into a fresh room, writes none of the three.

So the record set belongs to the room's state and not to the phase: a briefing
writes the pair, the blobs, the controls, the room's count and the readiness
publications, and the round's close triple only once a round has played. Writing it
from the join onward is what tells the client that the room is in a round while it
is still being briefed. **[V]** on the counts above.

**What was changed.** The writer is the briefing tick that publishes the room's
state while it waits — `BriefingCountdownService.PublishRoomStateAsync`, which
writes the twenty-two-byte record and the two entries beside the countdown — and it
is narrowed to a room whose round has played: the group goes out only once the
room's own round count is past zero, so a fresh room's first briefing writes the
countdown and the room's other records and none of the round's. The round's opening
run still carries the group, which is where a fresh room is first told its stage
(`mgo2-started-briefing-then-solo-game`'s run at t+174.371 is the case).

**Still open, and what closes it.** No client has joined this change. What would
confirm it is the same join, one capture: the briefing window should then hold no
`07 0f`, `07 2f` or close record, and the client should reach the briefing screen
before the round opens.

## 11. `myserver9.pcapng` against the two briefings, record by record

§9 compared a briefing by *rates* and §10 by *record*. This is the same comparison
against the capture taken before the changes of §10 landed, with the two references
the user named — the run's own shape is the one thing it still leaves open.

The three windows, host → client, with the report each answers:

| | `mgo2-game-started-on-briefing` | `mgo2-started-briefing-then-solo-game` | `myserver9` |
| --- | --- | --- | --- |
| span | 285 s | 294 s | 196 s |
| room | one player at its run, six by its round | one player throughout | one player throughout |
| report | `e1 01` t+55.838 and `e1 01` on `0x9ade` t+196.828 | `e1 03` t+54.918 and `e1 03` t+173.438 | `e1 01` t+53.844, and none after |
| the run | t+58.255, plain | t+57.626, LZSS | t+54.186, plain |
| the round | t+198.371, 1.5 s after the second report | t+174.371, 0.9 s after the second report | never |
| rules pair | 0.86 records/s over the capture | 42–75 records/s | 0.8 records/s |

**The round opens on a report, and the countdown's zero does not open it.** The
count is held at the room's whole briefing time for as long as the room holds one
player, and both references carry it that way: `started-on-briefing` writes
`0b 3c 00 00 00 00 00` from t+93.127, moves the set to `0x0002` when its client
reports and starts falling at t+132.249, reaching zero about t+191.4 — and the
round does not open there. It opens 1.5 s after the report at t+196.828. The solo
briefing is the same shape: `0b 37 00 08 00 00 00` at t+167.992 with the room still
at one reporter (`0x08` = slot 3, its report's own byte), and the round 0.9 s after
the second report. So a briefing that nobody re-reports in is opened by its
deadline and not by the count, which is what `MatchStartRunService`
(`OpenWhenBriefingEndsAsync`) and `BriefingCountdownService` do.

**The live host's briefing was the shape §10 describes**, and the changes there
close it: it wrote the ready set (`0x1ade e0 01 00 00 00 00`, 63 records), the
countdown (`0x1a54 0b 3c 00 02 00 00 00`, held at 60, one per 2.5 s tick) and the
rules pair (`0x0a61 fe` twice) from the report at t+53.8 to the capture's end at
t+196.2, where the reference writes none of the three between its run at t+57.626
and its round at t+174.371. The countdown's cadence is its own: the reference's
record is one per 2.3 s per peer (t+132.249, t+134.244, t+136.515, t+138.369,
…), so the 2.5 s tick is not the thing that was wrong.

**The run's own shape.** The live run is the same eighteen records as game1's
(t+46.42): the marker, the two empty roster heads, the state block and the
`0f 00` behind it, the two lobby-slot arrays, the presence answer, the close
triple, the clock, the payload and the rules pair. What differs is how often it
goes out — five times in the live capture, and one, two and three times in the
captures (`mgo2-game1`'s two rounds, `mgo2-game2`) — and nothing measured says what
moves that count: not the room's population (`mgo2-game2` writes three into a room
its count record holds at one player) and not the reports (that client reports once
before the three runs). `RepeatCount` is two for that reason, the most any capture
shows. Its frame is plain in this capture and in `started-on-briefing` and LZSS
marked in `mgo2-game1` and `started-briefing-then-solo`, so the compression is the
host's and not a rule.

**Two records the comparison settles, and one it does not.** The presence answer
grows a bit per reporting slot in both references and in the live capture —
`e1 03` answered `08 00 00 00` and `e1 01` answered `02 00 00 00` — which is
`PlayerReadyService.PresenceMask`'s reading of the report's own byte. The payload
publication's **trailing zeros are not the room's population**: the live host writes
four (`e0 01 00 00 00 00`), `started-on-briefing` six and `started-briefing-then-solo`
twelve, and each keeps its own length as its room goes from one player to six
to one — so the offset is the host's, as `MatchStartBodiesUtils.MatchStartPayload`
already records. What is *not* settled is the rules pair's rate in
`started-briefing-then-solo`, which writes it 42 to 75 times a second where the
other two write it under one. **[U]**

## 12. The run's copies: one to three per report, and a repeat on the tick

§11 found the copies by counting whole record sets inside a 0.1 s window. Counted
over every capture that holds a run — all eleven, `mgo2-started-on-solo-game`
excepted, which has none — the copies are 1 to 3 per report, and what writes them
is the host rather than anything the client sends (`tools/udp_run_repeats.py`,
which reads an instant as one copy and prints the client's records in each gap).

| capture | run instants | answering a report | without one | copies in one instant |
| --- | --- | --- | --- | --- |
| `mgo2-game1` | 11 | 6 | 5 | 1, and 2 at t+194.042 |
| `mgo2-game2` | 5 | 3 | 2 | 3 at t+62.181 |
| `mgo2-game3` | 2 | 1 | 1 | 1 |
| `mgo2-started-briefing-then-solo-game` | 4 | 2 | 2 | 2 at t+271.100 |
| `mgo2-game-started-on-briefing` | 3 | 2 | 1 | 2 at t+60.383 |
| `mgo2-game-joined-when-red-team-then-briefing` | 3 | 3 | 0 | 1 |
| `mgo2-joined-game-in-progress-last-round` | 4 | 4 | 0 | 1 |
| `mgo2-game-failed-memoryviolation` | 5 | 3 | 2 | 2 at t+64.586 |
| `joined-final-round-solo-then-final-round-...` | 2 | 2 | 0 | 1 |
| `mgo2-game-started-team-selection-...` | 1 | 1 | 0 | 1 |
| `mgo2-started-on-solo-game` | 0 | 0 | 0 | — |

**Not the client's report.** `mgo2-game3`'s client reports once — `0x1ade e1 10`
— and is answered twice, at t+59.596 with the marker and again at t+62.092 without
it, 2.5 s later. **Not the client's answer either:** the client's answers to the
run's own records arrive 0.4 to 0.8 s after every run (t+59.596's at +0.698,
t+57.626's at +0.680, t+174.371's at +0.495), which is *before* each repeat, and
one repeat goes out with the client silent over the whole gap — `mgo2-game1`
t+472.590, two seconds after the copy before it and with nothing from the client
in between. So a copy is not owed to a missing acknowledgement, and the client
acknowledges the run it is sent: `0x5a54` to `0x1a54`, `0x5a50` to `0x1a50`,
`0x5ade` to `0x1ade`.

**The separate instant is the tick**, 2.0 to 2.5 s behind the report's answer and
carrying the run's records again (the state block, the close triple, the clock,
the payload and the rules pair) with no marker. That is the same cadence the
briefing's own tick runs at, and it is why the copies are read as the host's
writes rather than as retries.

**What moves the within-instant count is still open.** Two sets go out at one
instant in three rounds and three in `mgo2-game2`'s, with nothing from the client
between them; the counts match neither the room's population (game2's run is
written three times into a room its own count record holds at one player) nor the
reports (that client reports once before the three). `RepeatCount` is two — three
sets — for that reason, the most any capture shows, and the number is **[U]**.

**The client's post-join records, and the one that returns.** The joiner's first
seconds are the handshake (`0x1000` ×2), the keep-alive (`0x5000` ×3), its profile
(`0x1001` 140 B ×2), the empty `0x5001` answers to the host's roster run, and two
one-byte `0x9001` records that carry no profile. Beside them it sends a **five-byte
`0x1001`-family record, `0c 01 00 00 00`** — an opcode `0x0c` and a `u32` of one —
first plain and then under the payload-class bit, 2 s apart, 7 s and 9 s after the
handshake (`mgo2-game-started-on-briefing` t+7.238 and t+9.209). The host answers
it: `0xd001` — `0x9001` with the answer bit — carrying a one-byte body `0c`, the
same opcode back, 0.9 s after the second copy. That is the rule `answer_body_length_of`
records, one body byte for a record of the `0x8000` class, and it is what makes the
record a request rather than a report. Across the captures it is sent 1 to 6 times
a session (game1 6, started-on-briefing 6, started-briefing-then-solo 4, game2 2,
game3 1, this project's own `myserver9` 1). What opcode `0x0c` *is* stays **[U]**:
the replay decoder's opcode table covers the operations family (low vid `0x254`)
and not the `0x001` one, so the name needs the client's sender, not another read of
the wire (`tools/mgo2_elf.py`, `tools/mgo2_static_use.py`).

**One capture never gets a run at all.** `mgo2-started-on-solo-game` — the
capture whose note reads "game appeared (no round)" — carries no marker, no state
block with a close, and no payload that is not a publication: the client joined,
entered the game and sat with a frozen clock, which is the shape the notes of
both solo captures describe (times frozen until a second player joins).

**The client's own ending, and why this capture cannot test the round.** Its
records stop at t+137.13 — 83 s after its report — and the host goes on writing to
t+196.24. Both references' clients report a second time 118.5 s and 141 s after
their first, and that second report is what opens their rounds; this client's would
have been due at about t+172, and it was already gone (`rpcs3.dmp` sits beside the
capture). So the briefing this capture holds is not a round that failed to open: it
is a room of one whose client left before its own timer came round.

## 13. `myserver10.pcapng`: the briefing's clock is written as a zero

§10 took the round's own close triple out of a fresh room's briefing, and the
briefing screen is still not reached. This is the next record of the round's that
a briefing writes, and the one a client reads as *the round is over*.

`myserver10` is one join into the same one-player room (client
`192.168.1.20:5730`, host `89.129.16.203:5731`, 181 s), and its briefing window
is the same shape §10 records: the countdown `0b 3c 00 00 00 00 00` held at 60
with the set empty, the room's count `0x1a58 00 01`, the rules pair, the blobs
and the controls, and no `07 0f`, `07 2f` or close record. The client reports
`e1 01` at t+53.694 and is answered with the briefing's run at t+54.023 — the
thirteen roles, the payload `e0 01 00 00 00 00` and the rules pair, written three
times — which is what §12 measures the copies to be.

**The run's clock is right and the beat's is a zero.** The run carries
`01 20 0d` — 3 360 units, the room's own seven-minute round. Every clock record
behind it reads `01 00 00`, from t+57.280 and once every ~3.2 s for the rest of
the briefing:

| | the reference (`mgo2-started-briefing-then-solo-game`) | `myserver10` |
| --- | --- | --- |
| the run's clock | `01 60 09` (2 400 units) at t+57.626 | `01 20 0d` (3 360 units) at t+54.023 |
| the beat's clock, briefing | `01 60 09` unchanged, t+57.626 → t+170.366 | **`01 00 00`**, t+57.280 → the capture's end |
| the beat's clock, round | counts down from 2 400 at t+174.371 | — the round never opens |

So a client that is still being briefed is told, once every three seconds, that
the round in front of it has no time left on it. The reference holds the whole
value there for the whole 113 s of its briefing.

**The cause is a writer with no caller.** `MatchRoundStateService.RemainingUnits`
answers with `BriefingClockUnits` for a room whose round has not opened, and
`BriefingClockUnits` is the field `NoteBriefingClock` sets — a method that, before
this change, had **no caller anywhere in the solution**, so the field never left
its zero. The field's own documentation says what that costs: *"A room that wrote
zero here would tell every client whose round has not opened yet that the round is
over."* The beat is the writer that reads it: it starts on the briefing's run
(`MatchRecordService`'s `WaitForRunWrittenAsync`) and takes `RemainingUnits` per
frame, so the zero is what the whole briefing's beat carries.

**What was changed.** `MatchStartRunService.BeginBriefing` now notes the clock
where every briefing begins: the whole time of the round that briefing precedes,
which is `settings.Round(roundState.RoundNumber + 1).TimeLimitUnits` — the same
round the briefing's own run carries, so the run and every record behind it agree.
Nothing else reads the value while a round is open (`Open` sets the round's own
settings), so the change reaches only a room whose round has not opened.

**Still open, and it is the same shape of defect.** The beat's other offsets are
counted from the run it starts on, and a *briefing's* run is up to a minute before
the round's: the item-box burst rides 1.8 s after it (t+55.828 here), which the
reference does too (t+59.992, 2.4 s after its run), but the vitals floor of
`MatchRecordFramesUtils.VitalsOnsetFrames` comes due 27 s in — `myserver10` writes
`0x084e fa fa` from t+85.094, while the reference writes its player's health for
the first time at t+180.491, 6.1 s after its round opened. A briefing is therefore
still sent one record of a round it is not in. **[V]** on the instants; whether a
client acts on either is **[U]**, as §7 leaves it.

**Not verified live.** The build and the 54 codec tests pass and the change is
measured against the captures, but no client has joined the built host. What would
confirm it is one capture of this join: the briefing window's clock records should
read the room's whole round time and not zero.

## 14. `myserver11.pcapng`: the initial data against two briefings, record for record

§13's clock is live, and the briefing screen is still not reached. This is the
same join into the same one-player room (`192.168.1.20:5730` ↔ host
`89.129.16.203:5731`, 181 s), read the way the user asked for it: the whole of the
host → client data, window against window, rather than a rate or one family.

**The clock §13 wrote is the room's whole round and it holds.** Every clock record
of this capture reads `01 20 0d` — 3 360 units, the room's own seven-minute round —
from the run at t+58.221 to the capture's end at t+180.8, 77 of them, none of them
a zero. The reference's briefing does the same in its own room's units
(`started-briefing-then-solo`, `01 60 09` held, 66 records). **[V]**, and it closes
§13's open question.

**The three briefing windows, by the records they write.** Each window is host →
client, from that host's briefing run to the round it precedes (or to the capture's
end where no round came):

| | `started-on-briefing` | `started-briefing-then-solo` | `myserver11` |
| --- | --- | --- | --- |
| span | 58.255 → 191.0 | 57.626 → 174.371 | 58.221 → 180.8 |
| records / kinds | 1 081 / 116 | 6 951 / 37 | 9 514 / 30 |
| rules pair `0x0261`/`0x0a61` `fe` | 266 | 6 644 | 6 670 |
| the clock, held whole | — | 66 (`01 60 09`) | 77 (`01 20 0d`) |
| item-box records (`0x1250`/`0x1a50` …) | 488 | 133 | 156 |
| room's count `0x0002` / 23 B | 34 | 19 | 17 |
| the close triple in the run | none | 2 + 4 | 3 + 6 |
| **`0x08xx` records** | **0** | **0** | **2 516** |

So the live briefing is `started-briefing-then-solo`'s shape and not
`started-on-briefing`'s — the rules pair at ~55 a second, the item-box group's own
shape and its fillers' bodies (`00 74 01`, `01 8c 00`, `00 00 00`, `01 00 00`), the
count, the arrays, the payload `e0 01 …`, the run's close triple — and
§12's three copies are the three the run frame carries at t+58.221. The two
references differ from each other on the close triple (§10 measured that), so the
live host's three are inside the measured range and are left as they are. **[V]**

### The one family a briefing writes and no reference briefing writes

**The vitals record.** `myserver11` writes `0x084e fa fa` 2 516 times inside its
briefing, the first at t+89.889 — 31.7 s after the run, which is
`MatchRecordFramesUtils.VitalsOnsetFrames` (818 frames) counted from the run the
beat starts on. Neither reference host writes a single `0x08xx` record in its
briefing window: **0** in both. They write them in the round that follows, and each
family's first record is a few seconds after the round's own open —
`started-on-briefing` t+203.498 (its round opened at t+198.371, **+5.1 s**),
`started-briefing-then-solo` t+180.491 (its round at t+174.371, **+6.1 s**). **[V]**

That is §13's "one record of a round it is not in", now measured on the wire: the
beat's own offsets are counted from the run it starts on, and a *briefing's* run is
written up to a minute before the round it precedes, so an onset that is right for a
round comes due in the middle of a briefing. A client four seconds into its
briefing is told, 28 times a second for the rest of it, about the health of a round
that has not begun.

**What was changed.** The beat's vitals record is gated on the round being open:
`BeatFramePlan` takes the round's own state beside its clock and `IncludeVitals`
requires it, so the measured floor still decides *when in the round* the record
joins and the round's open decides *whether*. A briefing's beat, which starts on the
briefing's run, therefore writes none, and every round keeps the onset the captures
place it at.

### The join burst, and two differences left in it

Recorded because they are what the same comparison shows and neither is changed
here:

* **The countdown runs before the room has heard anyone.** The live host writes
  `0b 3c 00 00 00 00 00` from t+2.505, one every 2.5 s, 22 of them before the
  client's first report — with an **empty** reporting set on every one of them,
  including the two ticks that follow the report (t+55.413, t+57.927). Each
  reference writes its first countdown at or after the room's second reporting
  player (`started-on-briefing` t+93.127), and each carries the reporters' bits —
  `0b 3c 00 02 …` once one player is on the set, `0b 37 00 08 00 00 00` for the solo
  room's slot 3 (a byte, not the room's population). **[V]** on the instants.
* **The report that arrived beside the client's presence record was answered 3.2 s
  late, by the retry.** The client sends both in one datagram at t+55.010
  (`0x0a5c` slot `01`, then `0x1ade e1 01`) and sends the report alone at t+58.018;
  the presence answer (`0x0a5c 02 00 00 00`), the publication (`0x1ade e0 01 …`) and
  the briefing's run all follow the **second**, at t+58.065 and t+58.221. The
  reference host answers the same two-record datagram in **0.3 s** (`0x025c 02 00 00 00`
  at t+56.123 for a report at t+55.838) and writes its run 2.4 s later. What the
  first datagram reached in the live host is **[U]** — the capture holds what was
  sent and not what was dispatched — and it is why the room's mask still read `00`
  at t+57.927 while the run four seconds later carries `02` in the same answer.
* **A ten-byte record the references write at the join and this host writes
  nowhere**: `0x1001 len=10 body=0b 00 00 04 00 00 00 00 00 0N`, twice in
  `started-on-briefing`'s join answer (t+5.996, N=2), once in `joined-when-red-team`
  (t+9.398, N=6) and once in the solo capture's second briefing (t+262.746, N=3).
  Its last byte climbs with the room's own progress and ours never writes the
  record at all. What it tells a client is **[U]**.

**Not verified live.** The build is clean (0 warnings, 0 errors) and the 54 codec
tests pass, and the change is measured against the captures; no client has joined
the built host. What would confirm it is the same join: the briefing window should
then hold no `0x08xx` record at all, and the round's own beat should grow them a few
seconds after it opens, as both references do.

## 15. `myserver12.pcapng`: the briefing the client was given lasted eight seconds

§14's clock is live and the briefing screen is still not reached. This is the next
join into the same one-player room (`192.168.1.20:5730` ↔ host `89.129.16.203:5731`,
158 s), and it is the first capture in which the **round opens while the client is
still reading the briefing it was just sent** — which is what the client's own
memory dump, taken beside it, shows it doing.

| | `myserver12` |
| --- | --- |
| the handshake / the roster run / the settings burst | t+0.072 / t+2.335 / t+5.070 |
| the client's report (`0x0a5c 01` + `0x1ade e1 01`, one datagram) | t+54.050 |
| the answers (`0x0a5c 02 00 00 00`, `0x1ade e0 01 00 00 00 00`) | t+54.099 — **0.049 s** |
| the briefing's run, three copies, clock `01 20 0d` (3 360, whole) | t+54.406 |
| the countdown `0b 3c 00 02 00 00 00` (60 s, the reporter's own bit) | t+54.564, 57.004, 59.540, 62.052 |
| **the round's run**, three copies, same set, clock whole | t+62.371 — **8.3 s after the report** |
| the countdown's closing zero `0b 00 00 02 00 00 00` | t+62.564 |
| the round's clock begins to move (3 360 → 3 348 → …) | t+123.973 — **61.6 s later** |

**The room's briefing was armed at the join and spent by the report.** The
handshake is answered at t+0.072 and the roster run at t+2.335; the room's
one-minute briefing ran from there, and the client — which took fifty-two seconds to
load its map — reported with eight seconds of it left. The deadline that opened the
round at t+62.371 is that briefing's end (`MatchStartRunService`, armed at
`BeginBriefing`), so the client was briefed for 8.3 s where both reference briefings
give the reporting client its own time: `mgo2-started-briefing-then-solo-game`
reports at t+54.918 and its round's run goes out at t+174.371 (**119.5 s**), and
`mgo2-game-joined-when-red-team-then-briefing` reports at t+95.464 with its round at
t+123.774 (**28.3 s**). **[V]** on the instants; that the client renders nothing
until its briefing is a screenful is **[U]**.

**And the round it opened held its clock for a minute it had already spent.** The
timer a round's clock is held for is the briefing's remaining time
(`MatchStartRunService.OpenRound`), read from `BriefingCountdownService.Remaining` —
which for a **room of one** is the room's whole briefing time and never moves, because
that is what the reference writes in the count byte (`started-on-briefing` holds
`0b 3c` for tens of seconds). So a round opened at the end of a briefing was held
whole for the briefing's *nominal* minute: the beat carries `01 20 0d` — 3 360 units,
7:00 — unchanged from t+54.406 to t+123.973, sixty-one seconds of a client that is in
the round being shown a frozen seven minutes. That is the report this session was
opened on ("initially it shows 7m of game frozen"), and it is the same defect as the
paragraph above: the host held a clock for briefing time the client had not spent.

**The countdown is now the reference's shape.** §14's first bullet is closed: the
record no longer runs before the room has heard anyone. The first is written at
t+54.564, 0.14 s after the run, it carries the reporting player's own bit (`02` for
the slot the report named, §13's fix), it is held at the room's whole 60 — the
measured frozen count of a room of one — and it closes on `0b 00 00 02 00 00 00` at
the round's open. §14's second bullet is closed too: the single report datagram is
answered in **0.049 s** by all three records, where `myserver11` answered the retry
3.2 s late. **[V]** on the instants.

**The one thing the client does in a reference briefing and does not do here.** Both
reference clients write their own operations 2.4 s after their readiness report —
`mgo2-started-briefing-then-solo-game` at t+57.304 sends `1d 03 00 01 03 00 00` five
times and a 22-byte `22 03 00 0b 16 …`, under `0x1254`, and `1f 03 01` 108 s later.
`myserver12`'s client writes **none** of the three families in its whole session: its
own records are the report and its presence, the lobby-slot handshake (`82 f3`,
`84 f3`), its empty `0x5001` answers and its `0x5a50`/`0x5a5a`/`0x5a54`
acknowledgements. What the three are is the client's own phase tick and its own run
opener (`udp-handlers-and-senders.md` §5.2, §5.4), so a client that writes none is a
client that never entered the phase they belong to. Whether it is the eight-second
briefing that stopped it or something the host never sent is **[U]**, and it is the
sharpest thing left to measure: the next capture of this join should hold the trio,
and where in the briefing it lands says which of the two it is.

### The dump beside the capture: the client holds a round, not a briefing

`rpcs3.dmp` was taken beside this session and its guest memory answers the one
question the capture cannot — which screen state the client ended up in.

* **The live session is this server.** The P2P module's instance (`0x011A6308` →
  `0x01856C10`, module state `2`) has its session array at `0x50000060`, and slot 0
  carries the endpoint `59 81 10 cb` — `89.129.16.203`, the capture's host — with
  port `63 16`. `docs/analysis/udp-record-dispatch.md` §2, §4 reads the same fields.
* **The run reached it, record for record.** The run's lobby-slot pair is resident
  at `0x500104b8` and again at `0x500e1688` and `0x500e1a58`: `85 f3 fe 00 ff ff …`
  and `86 f3 7f 07 7f 7f …`, each behind a `10 01 00 00 00 26 00 05 13 00` record
  header — the exact bytes `myserver12`'s run carried at t+54.406. So the briefing's
  run was decrypted, read and kept.
* **And the room's state vector is resident byte for byte**, at `0x54cc6a9e`: the
  vector's own runs (`18 18 ff ff ff ff 18 18 18 18 18`, `fe fe fe fe`, `3c 3c 3c 3c`,
  `state[2] = 0xff`, `state[86] = 0xff`) line up with the alignment the wire's
  89 bytes have, and `0d 20` — 3 360 — sits immediately before it.
* **Its clock reads 2 816 of those 3 360** — `state[87..88] = 00 0b`, little-endian as
  the wire writes it, **68 s** into the round. A briefing's own run carries the whole
  3 360 there, so what the client holds is a round's state with its clock moving, not
  a briefing's. The same structure holds a second, `0x28`-seeded vector at
  `0x54fc6a1a` whose clock is `0`, which is why the block is read as the current
  room's own and not as the only one. Both are aliased 4 GB above, which is the
  image's own aliasing and not a second copy.

Read out with `_scratch/sessionstate.py` (the instance and its session slots),
`_scratch/findbytes.py` (the patterns the run wrote) and `_scratch/findstate.py` (the
state block a capture carried).

**What the dump does not settle.** It cannot say at what instant the client left its
briefing, and it does not hold a state block for the briefing itself: a client
mid-briefing keeps the same block, with the clock still whole. It shows where the
client *was* — in this room, in a round, 68 s into its clock — and the capture shows
how it got there: a briefing of 8.3 s and a clock held for 61.6 s after it.

### What was changed

| file | change |
| --- | --- |
| `MatchRecords/MatchStartRunService.Briefing.cs` | `BeginBriefing` no longer arms the countdown or the deadline; new `ArmBriefingClock` does both where the briefing hears its first report, and logs it |
| `MatchRecords/MatchStartRunService.cs` | `Open` arms the briefing's clock on the first report; `OpenRound` takes the clock's hold from `countdown.TimeRemaining` |
| `MatchRecords/BriefingCountdownService.Clocks.cs` | new partial — the briefing's clock: `Begin`, `StartCounting`, `IsCounting`, `Remaining`, and the new `TimeRemaining`, the briefing's own end, which is not the held count `Remaining` answers with |
| `MatchRecords/MatchStartRunService.Lifecycle.cs` | new partial — `ResetWhenEmpty`, `End` and `EndWhenTimeRunsOutAsync`, moved so no file passes the 400-line limit |

So a briefing's time is the reporting client's own, as both references' are, and a
round opened once that time is out counts from its own open instead of holding a
clock for a briefing that has ended. A room of one still arms a deadline (a solo
room is briefed and then started), and a briefing no player answers still arms
nothing.

**Not verified live.** The build is clean (0 warnings, 0 errors) and the 54 codec
tests pass, and both changes are measured against the captures; no client has joined
the built host. What would confirm them is this same join: the briefing's run should
now go out a whole briefing time after the report rather than eight seconds after it,
the countdown should tick its 60 down for that minute, and the clock records behind
the round's run should move from the run onward instead of holding 3 360 for a
minute. The other thing to look for in that capture is the one the section above
leaves open: the client's own `1d`/`1f`/`22` trio, which neither of this host's two
live sessions has produced yet (`myserver11`, `myserver12`).


## 16. `myserver18.pcapng`: the room's state is written once where every capture repeats it

§15's clock and countdown are live, and the briefing screen is still not reached.
This is the next join into the same one-player room (`192.168.1.20:5730` ↔ host
`89.129.16.203:5731`, 173 s), read the same way as §14: the whole of the host →
client stream on the reference's own windows. It is the first of these captures in
which **§15's two changes are both on the wire at once**, and it is the one that
shows the host's remaining difference from every reference briefing — a record set
the captures write twice and this host writes once.

### What §15's changes produced

| | `myserver18` |
| --- | --- |
| the client's report (`0x0a5c 01` + `0x1ade e1 01`, one datagram) | t+56.092 |
| the answers (publication, presence answer, the run's marker) | t+56.140 — **0.048 s** |
| the briefing's run, clock `01 20 0d` (3 360, whole) | t+56.220 |
| the briefing's countdown `0b 3c 00 00 00 00 00`, one per 2.5 s | t+56.428 → t+116.159, 23 records |
| **the round's run** | t+116.181 — **60.089 s after the report** |
| the countdown's closing zero `0b 00 00 00 00 00 00` | t+116.159, 22 ms before the round's run |
| the round's clock, from the open | 3 360 at t+116.181, 3 335 at t+119.370, 3 322 at t+120.987 |
| `0x08xx` records in the briefing | **0**; the first is `0x084e fa fa` at t+145.545, 29.4 s after the round opened |

So a briefing's time is now the reporting client's own minute and not the join's
leftovers, and a round opened at the end of it counts from its own open instead of
holding a clock for a briefing that has ended: the two defects §15 names are gone
from the wire. The countdown is held at the room's whole 60 with an **empty** set,
which is what a room whose only client has not readied writes — the set is the
ready set (`BriefingCountdownBodiesUtils`), and this client sends no `1f` record
(after §15's item: the ready still does not arrive).

### The host's stream against the two references, family for family

The briefing windows — the report's answer through the round (or the capture's
end) — with each host's retransmissions counted as their own records:

| | `started-on-briefing` | `started-briefing-then-solo` | `myserver18` |
| --- | --- | --- | --- |
| span | 58.255 → 191.0 | 57.626 → 174.371 | 56.220 → 116.181 |
| rules pair `0x0261`/`0x0a61` `fe` | 266 | 6 644 | 3 064 |
| countdown `0b …` (7 B) | 66 | 4 | 24 |
| the operations clock (`01 …`, 3 B) | 88 | 65 | 35 |
| the state block (90 B) | 2 | 2 | **1** |
| the close triple (`07 0f`, `07 2f` ×2) | 2 + 4 | 2 + 4 | **1** |
| the room's count / blob (23 B) | 34 | 19 | 8 |
| the item-box group (`0x1250`-family) | 488 | 133 | 156 |
| `0x08xx` records | 0 | 0 | **0** |

Every family is inside the two references' own spread — the rules pair is 0.8/s in
one and 57/s in the other, and this host writes 51/s — except one, which is not a
rate at all: **the run's record set.** Both references write it twice in a
briefing, and this host writes it once.

### The run's second instant

The 90-byte state block is the one record of the run that can be followed across
captures without a role table, and every capture writes it twice in a short
window, then stops:

| capture | the run | the second instant | the third |
| --- | --- | --- | --- |
| `mgo2-started-briefing-then-solo-game` | t+57.626 | **t+59.993** (+2.37 s) | — (its round's run at t+174.371) |
| `mgo2-game-started-on-briefing` | t+58.255 | **t+60.383** (+2.13 s), t+65.124 (+4.74 s) | its round at t+198.371, then t+200.869 (+2.50 s) |
| `mgo2-game3` | t+59.596 | **t+62.093** (+2.50 s) | — |
| `mgo2-game1` | t+46.415 | t+48.777 (+2.36 s), t+51.252 (+2.48 s), t+56.389 (+5.14 s) | — |
| **`myserver18`** | **t+56.220** | **none** | — |

The second instant is the whole set and not the block alone:
`mgo2-started-briefing-then-solo-game` t+59.993 writes the lobby-slot arrays
(`0x9001`/`0x1001` 26 B), the room's stage (`0x1001` 22 B and two 54 B), the close
triple, the state block, the `0f 00` short record, the round clock, the payload and
the rules pair — **the run's records with the marker and the two empty roster heads
left out**, keeping the ordinals the run gave them (`0x07`…`0x0e` for the family,
`00 01 02` for the operations). It is the same set re-sent, not a second run: the
marker is the answer to the peer's own report and there is nothing for it to answer
again, and the heads open the roster run it answered.

`docs/analysis/connection-not-stable-disconnect.md` §4 removed the run's copies
because they were stamped inside the pass that built the run — three datagrams of
the same frame, which stand or fall together and give a client that lost one
nothing. That argument holds and this is the other half of the same measurement:
the repeat a capture writes is spaced by a tick and carries the set again, which is
what a client that lost the first frame is answered by.

### What was changed

| file | change |
| --- | --- |
| `MatchRecords/MatchStartRunFramesUtils.cs` | new `RepeatOf(run, marker)` — the run minus the marker and the two `0x5001` heads, the set the captures write one tick behind the run |
| `MatchRecords/MatchStartRunService.Runs.cs` | `WriteRunCoreAsync` writes the run, then writes `RepeatOf` one briefing tick (2.5 s) later, and only while the room is still in the phase the run was written for — a briefing whose round opened inside the tick is not repeated over the round's own run |

### What the client does, and the one thing §15 leaves open

**The client is alive and its probes keep coming.** Its `0x9001 00` probe and the
host's `0xd001` answer run every ~5.1 s from t+2.337 to t+225.209 — 52 client
datagrams, the last one 109 s after the round opened — so this is not a reaped
session, a dead client or a broken channel: it is a client that is waiting. What it
does **not** send is still the §15 item: no `1d`, no `1f`, no `22` in the whole
session, where both references' clients write their own operations 2.4 s after
their report and `1f <slot> 01` when their briefing ends.

**The report datagram is missing its own operations.** §1 of `BRIEFING_FLOW.md`
measures the client's report as the five `1d <slot> …` records, the `e1 <slot>`
itself and the one-byte companion in one datagram
(`joined-when-red-team` t+95.464, `started-on-briefing` t+62.623). This client
sends the companion, the `e1` and the two lobby-slot answers and none of the five
(`0x0a5c 01`, `0x9001 82 f3`, `0x1001 84 f3`, `0x1ade e1 01`). `0x1d` is the record
whose writer is not in the image (`udp-handlers-and-senders.md` §5.5), so which of
the two the client is waiting for cannot be read off the wire: the client's own
phase is the [U] that the next capture has to settle.

### Not verified live

The build is clean (0 warnings, 0 errors) and the 58 codec tests pass, and the
change is measured against four captures. What would confirm it is one more join of
this same room: the state block and the close triple should then appear twice per
briefing — in the run and one 2.5 s tick behind it — with the same ordinals and no
marker on the second, and the client's own `1d`/`22` should be looked for in the
same capture, since that is the record the briefing screen is still missing.
