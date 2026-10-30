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

| capture | the client's join report | the round's run |
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
| 48.745 | its join state record `8a5c 01 de` |
| 51.288 | its join report `1ade e1 01` |
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

* its join state record's byte is `01`, which is the player's own lobby slot
  (`JoinStateHandler`, `BRIEFING_FLOW.md` §1);
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
writes the pair, the blobs, the controls, the room's count and the join
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
`0f 00` behind it, the two lobby-slot arrays, the join state answer, the close
triple, the clock, the payload and the rules pair. What differs is how often it
goes out — five times in the live capture, and one, two and three times in the
captures (`mgo2-game1`'s two rounds, `mgo2-game2`) — and nothing measured says what
moves that count: not the room's population (`mgo2-game2` writes three into a room
its count record holds at one player) and not the reports (that client reports once
before the three runs). `RepeatCount` is two for that reason, the most any capture
shows. Its frame is plain in this capture and in `started-on-briefing` and LZSS
marked in `mgo2-game1` and `started-briefing-then-solo`, so the compression is the
host's and not a rule.

**Two records the comparison settles, and one it does not.** The join state answer
grows a bit per reporting slot in both references and in the live capture —
`e1 03` answered `08 00 00 00` and `e1 01` answered `02 00 00 00` — which is
`BriefingStateService.JoinedMask`'s reading of the report's own byte. The payload
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
* **The report that arrived beside the client's join state record was answered 3.2 s
  late, by the retry.** The client sends both in one datagram at t+55.010
  (`0x0a5c` slot `01`, then `0x1ade e1 01`) and sends the report alone at t+58.018;
  the join state answer (`0x0a5c 02 00 00 00`), the publication (`0x1ade e0 01 …`) and
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
reference clients write their own operations 2.4 s after their join report —
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
clock for a briefing that has ended. ~~A room of one still arms a deadline (a solo
room is briefed and then started)~~, and a briefing no player answers still arms
nothing. **The struck clause is §17's subject and was wrong**: the report this arms
on is the client's join and not a readiness, so a room of one was timed by a join
that says nothing about playing and was handed a round no one had asked for. §17
arms the deadline at the room's second player instead.

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
| the answers (publication, join state answer, the run's marker) | t+56.140 — **0.048 s** |
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


## 17. `myserver17.pcapng`: the round a sole client was handed 60 s after it reported

§15 and §16 both arm a briefing's deadline at the client's **join report** — the
`0x1ade e1 <slot>` record that says the room's map is loaded and the client is on
the briefing screen — and both measure a round opening on it. That is the defect
this section is about, because the join report is not a readiness. A player's
readiness is a different record, on a different stream: `1f <slot> 01` on the
operations stream, the one `OperationsStreamHandler` takes and
`MatchStartRunService.OpenWhenEveryPlayerIsReady` counts. A round opens on the
room's readies, or — in a room that has a second player to wait on — on the
briefing's own time running out. A room of one has no such timer: its count is
held at the room's whole briefing time, which is what the reference writes for a
sole client, so nothing but that client's own ready starts its round.

### The session

`myserver17.pcapng`, read record by record out of a fresh dump
(`tmpout/ms17recs.txt`, `_scratch/ms17_timeline.py`): one client
(`192.168.1.20:5730` ↔ host `89.129.16.203:5731`), 133 s, nobody else ever in the
room — the presence answer stays `02` (one reporter's bit, §13) from the first
answer to the last.

| t+ | what | |
| --- | --- | --- |
| 53.441 | the client's join report `0x0a5c 01` · `0x1ade e1 01`, one datagram | §1's shape |
| 53.454 / 53.503 | the host's answers: the presence answer `02` and the bare `0x1ade e0 01 …` | |
| 53.908 | **the briefing's run**, three copies, with the state block and the countdown's stream opened | |
| 54.051 | the countdown `0b 3c 00 00 00 00 00` — **60 s, held** | a room of one's frozen count |
| 54.396 → 107.6 | that countdown again every ~2.5 s, still `3c`, each with the room's own state group | |
| 68.473 / 91.389 / 107.619 | the client re-reports its join on the sibling set, `0x12de e1 01` | |
| 71.262 → 95.059 | and retransmits it as `0x92de e1 01` **eight times** over 23.8 s | the retransmission `JoinReportHandler` names |
| **113.712** | **the round's run** — 60.27 s after the report, with no ready anywhere in the session | **the defect** |
| 133.208 | the capture ends, the round still playing, the client having sent nothing of its own | |

**Nothing in this session is a ready.** `grep type=0x1f` over the whole dump is
zero records: the client never changes its briefing state. It also never writes
`1d` or `22`. So the round at t+113.712 answers no ready, no second report of the
kind the reference opens on and no clock the client was ever given — it is the
briefing's one-minute deadline, armed on the join report at t+53.441, firing while
the count the client was sent still reads 60.

`myserver18` (§16) is the same shape one session along: its briefing's run is at
t+56.220, its clock records hold `3 360` — the room's whole seven minutes —
through t+115.322, and its round's run is at t+116.181, ~60 s later. §16 reads the
held clock as a briefing that then ends; §17 reads the instant it ends on as the
defect.

### What was changed

| file | change |
| --- | --- |
| `MatchRecords/MatchStartRunService.cs` | `Open` → `OnJoinReport`: the first report arms the count only, and the report that gives the room a second player (`JoinedCount >= 2`) arms the deadline, once per briefing (`armedDeadlineBriefing`) |
| `MatchRecords/MatchStartRunService.Briefing.cs` | `ArmBriefingClock` split: `BeginBriefingClock` (the count a peer is shown, held) and `ArmBriefingDeadline` (the count moving plus `OpenWhenBriefingEndsAsync`) |
| `MatchRecords/BriefingCountdownService.Clocks.cs` | `TimeRemaining` — the reading the round's clock hold takes — now runs from the moment the count starts and answers nothing while a room of one's count is frozen, so a solo round that is opened by its player's ready counts from its own open |
| `Rooms/BriefingStateService.cs` | `PlayerReadyService` renamed: it holds both of a briefing's states, the join report and the ready record, and its count of reporters is `JoinedCount` |
| `Commands/Peer/Room/JoinReportHandler.cs` | `PlayerReadyHandler` renamed: the record it takes is the join report and not a readiness |
| `Commands/Peer/Room/JoinStateHandler.cs` | `PlayerPresenceHandler` renamed: one join, two records, one vocabulary |
| `MatchStartBodiesUtils` / `MatchRecordGenerationUtils` | `*ReadinessReport*` → `*JoinReport*`, `PlayerPresence*` → `JoinState*`, `PresenceMask` → `JoinedMask` |

The rule the three code files now hold: **the briefing's count is shown from the
join report, and the room's deadline from its second player.** A room of one has a
held count, no deadline and no game clock; it starts a round only when its player
readies. A room of two starts its briefing's minute at the second join report and
opens on the readies or at the end of it. The map-loaded report — the join report —
is never a readiness, in the code or in the name.

### Not verified live

The build is clean (0 warnings, 0 errors) and the 58 codec tests pass; no client
has joined the built host. What would confirm it is this same join again: a sole
client should be sent its held count and **no round at all** for as long as it is
alone, and its round should appear only after its own `1f <slot> 01`. The next
capture should also be read for that record, because a client that never sends it
is a client that can no longer be started by this host in a room of one — which is
the client's own behaviour (`myserver17`'s client sent neither `1f` nor `1d` nor
`22` in 133 s) and not something a host can supply for it.


## 18. `myserver19.pcapng`: the run is written before the client has entered the room

§17's rule is on the wire and holds: a room of one is frozen, and the client in it
is handed **no round at all** in 220 s. The briefing screen is still not reached,
and this section is the measurement that separates the two — the round rule is
right, and the room's state is written at the wrong instant.

### The session

`myserver19.pcapng`, read record by record (`tmpout/ms19recs.txt`) against the
deployed pod's log (`tmpout/ms19-server.log`): one client (`192.168.1.20:5730` ↔
host `89.129.16.203:5731`), 220 s, nobody else ever in the room.

| t+ | what | |
| --- | --- | --- |
| 58.568 | the client's join report `0x0a5c 01` · `0x1ade e1 01`, one datagram | §1's shape |
| 58.612 | the host's publications: the join state answer, the payload echo and its bare ack | |
| **58.906** | **the briefing's run** — 18 records, state block and clock whole, the pair at `f3` | **0.34 s after the report** |
| 59.281 | **the client's entry** — `82 f3` · `84 f3`, the answer to the pair in the run | **0.38 s after the run** |
| 59.319 | the host's answer to it: confirm `81 f3`, the group naming `f4`, the pair at `f4` | |
| 60.196 | the client's acks of the run and the settings burst | the frame was read |
| 61.320 | the run repeated, one tick behind it, without marker or heads (§16) | |
| 75.104 | the client's next entry `82 f4` · `84 f4` — the handshake advancing, and its own retry 15.8 s after the first | |
| 75.31 | the host answers it with the bare `0x52de`, the confirm `81 f4` and the pair at `f5` — **no run** | |
| 220.9 | the capture ends, the countdown still held at `3c`, the client sending probes only | |

**No round starts, and that is the rule working.** At t+74.5 the client reports a
second time, on the sibling set, and the log answers it in the terms §17 asked for:

```
The room asked for a round and the join report arrived on 0x12de, but 1 player(s)
in it have not readied; the round stays closed until they do or the briefing runs out
```

and no round's run is anywhere in the capture. The client can no longer be started
by this host in a room of one because it never readies, and it never readies
because it never shows the briefing screen: `grep` over the whole dump returns
**zero** `1d`, zero `1f` and zero `22` records from the client, where both
reference clients write their `1d`/`22` phase record within five seconds of the
entry and the solo one readies with `1f 03 01` at t+165.780.

### What every capture says the run is the answer to

Every run in every capture held here, and the two events that precede it — the
client's **entry echo** (`82 NN`·`84 NN`, the lobby-slot handshake's request to be
placed in the room) and its **join report** (`e1 NN`):

| capture | run | behind its entry | behind its report |
| --- | --- | --- | --- |
| `started-briefing-then-solo` | 57.626 | **2.31 s** | 2.71 s |
| | 174.371 | **0.34 s** | 0.93 s |
| `started-on-briefing` | 58.255 | **1.90 s** | 2.42 s |
| | 198.371 | **1.20 s** | 1.54 s |
| `started-team-selection-blue` | 66.159 | **1.41 s** | 1.91 s |
| `game1` | 46.415 | **0.73 s** | 1.66 s |
| `game2` | 62.181 | **0.59 s** | 5.46 s |
| | 109.120 | **0.89 s** | 1.14 s |
| | 224.618 | **0.67 s** | 0.99 s |
| `joined-when-red-team` | 62.666 | **4.68 s** | 5.01 s |
| | 65.138 | **2.36 s** | 7.48 s |
| | 96.915 | **2.73 s** | **39.3 s** |
| | 132.791 | **0.88 s** | **75.1 s** |
| | 450.300 | **0.61 s** | none |
| | 819.290 | **2.11 s** | none |

Every run follows an entry, and the two in `joined-when-red-team` at t+96.9 and
t+132.8 cannot follow a report: the room's last report before them is 39 s and
75 s earlier, while each comes 2.7 s and 0.9 s behind that peer's own entry. The
entry echoes in these captures are the room filling up — one per player placed in
the room — and the host's answer to each is the room's state.

**And the live host writes the same run at the opposite end of that entry.**

| session | the run | the client's entry | |
| --- | --- | --- | --- |
| `myserver16` | t+52.997 | t+53.557 | **run 0.56 s early** |
| `myserver17` | t+53.910 | t+54.280 | **run 0.37 s early** |
| `myserver19` | t+58.906 | t+59.281 | **run 0.38 s early** |

The client reports while it is still loading its map — 0.4 s before its entry here,
0.4 s in `started-briefing-then-solo`, 0.5 s in `started-on-briefing` — so a host
that answers the report with the run hands the room's whole state to a client that
has not reached the room yet, and the client that then enters asks for it and is
answered with the handshake alone. `myserver19`'s second entry at t+75.1 is that
question asked a second time and answered with no run at all.

That is the shape the operator reports: the map fades in, out and in again, and
the client sits on the last frame — it entered the room twice and was never given
the state of the phase it entered.

### What was changed

| file | change |
| --- | --- |
| `MatchRecords/MatchStartRunService.Entries.cs` | new partial — `OnRoomEntry` notes the peer's entry, and `HasEntered` is the note the run waits on |
| `MatchRecords/MatchStartRunService.Runs.cs` | `WriteBriefingRunAsync` waits for the peer's entry beside the briefing's report, so the run is written when the client has been placed in the room |
| `Commands/Peer/Room/PlayerProfileHandler.cs` | the lobby-slot echo's pass — which already answers the handshake — notes the entry, so the run follows the echo in the same pass |
| `MatchRecords/MatchStartRunService.Reports.cs` | new partial — `OnJoinReport` moved out of `MatchStartRunService.cs`, which had passed the 400-line limit |

The rule the three code files now hold: **the run is the room's answer to the
client's entry, not to its report.** The report names the set the run is written
under and the byte its payload echoes; the entry is the instant it goes out.

### Not verified live

No client has joined the built host. What would confirm it is this same join: the
briefing's run should appear **behind** the client's `82 NN`·`84 NN` rather than
0.4 s before it, the client should then write its own `1d`/`22` record within a
few seconds — the record no live session has produced in five captures — and the
briefing screen should show with the count held at the room's whole minute. What
would falsify it is the same fade-in, fade-out and silence with the run behind the
entry, which would put the missing state in the run's contents rather than in its
instant. Two things beside it stay open: the second entry is still answered with
the handshake alone (writing the run again for a repeated entry of the same phase
is not measured in any capture), and a peer that never echoes would now be written
no briefing run at all.

## 19. `myserver20.pcapng`: the run is behind the entry, and the entry's own confirmation stops the client

§18's change is live: the briefing's run is written behind the client's entry. It
is, and the client still does not reach its briefing. This is that session
(`192.168.1.20:5730` ↔ host `89.129.16.203:5731`, 230 s), against the two
reference briefings of a working host.

### What the run's instant now is

| | entry echo (`82 NN`·`84 NN`) | briefing's run | gap |
| --- | --- | --- | --- |
| `started-briefing-then-solo` | t+55.312 | t+57.626 | +2.31 s |
| `started-on-briefing` | t+56.360 | t+58.255 | +1.90 s |
| `myserver19` (before §18) | t+59.281 | t+58.906 | **−0.38 s** |
| **`myserver20`** | **t+61.265** | **t+61.466** | **+0.20 s** |

The instant is inside the references' own range and the client **acknowledges every
record of the run** — the whole first frame at t+61.847 (`0x5001` classes `06`–`19`,
`0x5a54` classes `00`–`02`, `0x5ade`) and the repeat's eight records at t+64.201.
Nothing in the run is dropped.

### Where the client then stops

It sends nothing but its own heartbeat from t+64.201 until t+193.7, when it goes
silent altogether and the host reaps it 60 s later. It never writes:

* the `1d 03 00 …` stage records plus the 22-byte `22 03 00 …` record, which
  **both** reference clients write two seconds after their entry
  (`started-briefing-then-solo` t+57.304, four of them and the `22`), and which
  `joined-when-red-team` writes at t+95.464;
* a ready;
* an echo of the `f4` pair the entry's own pass published twice.

The operator's description — the game renders, its audio stops, and it does not
move — is a client that entered the world and is waiting for a stage it was never
moved to.

### What the entry pass writes that no capture does

| host write, in the pass that received the echo | captures |
| --- | --- |
| the pair at the echoed sequence, `81 NN`, the next-sequence group, the pair at `NN+1`, **twice** (once per echo record) | `joined-when-red-team` writes the pair at `02` beside the run and confirms `81 02` at t+72.560, 14.6 s later; `started-briefing-then-solo` confirms `81 7e` at t+167.992, 112 s later and 4 s before its round; `started-on-briefing` at t+193.621, 137 s later; `game1` and `started-on-solo` never confirm at all |

So the confirmation is not the entry's answer in any capture, and it is never
written ahead of the run. Here it went out 37 ms after the echo, the run 164 ms
behind it, and the client stopped there.

### What was changed

| file | change |
| --- | --- |
| `MatchRecords/MatchStartRunService.Entries.cs` | the entry's sequence is noted beside the entry (`entrySequences`), and `ConfirmEntryAsync` writes the confirmation when the round opens |
| `MatchRecords/MatchStartRunService.Runs.cs` | `RunAsync` confirms the entry after the briefing's run has been written and the round has opened, ahead of the round's own run |
| `Commands/Peer/Room/PlayerProfileHandler.cs` | the echo's pass notes the entry and writes no handshake record |
| `MatchRecords/MatchStartRunService.cs` | the service takes `TeamArrayService`; the run watcher is what writes the confirmation |

The rule: **the lobby-slot confirmation is the briefing's last handshake and not
its first** — it belongs with the round the briefing precedes, which is where the
two captures that hold one put it (14 s, 112 s and 137 s behind the entry, the
last two within 4 s of the round).

### Not verified live

No client has joined this build. What would confirm it is this same join: the
echo should be answered with the run and **nothing else**, the client should then
write its `1d`/`22` records two seconds later (the records no live session has
produced yet), and its briefing screen should show with the count held at the
room's whole minute. What would falsify it is the same silence with the entry
answered by the run alone, which would put the missing state in the run's contents
— the ready-set record is 6 bytes here where `started-on-briefing` writes 8 and
`started-briefing-then-solo` 14 — rather than in what the entry pass writes. A
room whose round never opens (a solo player who does not ready) now never confirms
the pair at all, which is what `game1` and `started-on-solo` show.

## 20. `myserver21.pcapng`: the run is behind the entry and the entry is still answered

§19's change is live and it moves the client further than any run before it: the
game renders its own screens — the basic UI and the room's rule text — and fades
out. It then sits on the black screen between that fade and the briefing screen,
which is where this section leaves it.

### The session

`myserver21.pcapng`, read record by record (`tmpout/ms21.log` through
`tools/pcap_gameplay_log.py`), against the two reference briefings: one client
(`192.168.1.20:5730` ↔ host `89.129.16.203:5731`), a fresh room, nobody else in it.

| t+ | what | |
| --- | --- | --- |
| 2.237 | the roster burst: the two entries, the close, and no `0x0261` pair | |
| 4.813 | the settings payloads: the rotation (`0b 00`, 161 B) and two loadouts (`0b 01`, `0b 02`) | |
| 57.083 | the client's companion `0x8a5c 01` — **before** its report, where both references send it with one | |
| 57.091 | the host: the answer `0xca5c 24` | |
| 58.341 | the client's entry echo `82 f3` · `84 f3` | |
| **58.427** | **the host: `0xd001 24` · `0x5001`** | **the entry answered, where no capture answers it** |
| 59.635 | the client's join report `0x1ade e1 01` — **after** its entry, where both references report first | |
| 59.679–59.722 | the presence answer, the publication `e0 01 …` and the briefing's run, 0.09 s behind the report | |
| 60.226 / 62.603 | the client acknowledges the run and its repeat, `0x5a54` ×241 over the session | |
| 62.9 | the host's beat: the pair and the round's own item boxes, every frame | |
| 88.560 | the client's room request `83 f3 00 07`, 30 s after its entry, answered with the arrays and its entry | |
| never | the client's `1d`/`22` stage records, its ready, or a second report | |

**What the references do in that pass.** `started-on-briefing` takes the echo at
t+56.360 and writes **nothing** until its run at t+58.255; `started-briefing-then-solo`
takes it at t+55.312 and writes nothing until its run at t+57.626, and its client
writes its own `1254 7` stages and the 22-byte record 2.0 s after the echo and
**0.3 s before that run arrives** — so the stage the live client never reaches is
not written by the run in the capture that reaches it without one. No capture
answers `82 NN` · `84 NN` at all.

**What this host still wrote there.** `PlayerProfileHandler` returns on the echo
without writing anything (§19), and the two records that went out 86 ms behind it
are the dispatch's own record-level answers: `0xd001 24` for the `0x9001` half of
the pair and `0x5001` for its `0x1001` half. They are the last of what the entry
pass used to write — the confirmation went in §19 — and they are exactly the shape
§19 measured a client stopping on.

### What was changed

| file | change |
| --- | --- |
| `GameplayServer/GameplayServerService.FrameDispatch.cs` | the automatic answer skips a body `TeamArrayFramesUtils.IsArrayEcho` reads, so the pass that receives the lobby-slot echo writes nothing, as every capture's does |

### Not verified live

No client has joined this build. What would confirm it is this same join: the echo
should be answered by the run and by **no** record-level answer before it, the
client should then write its `1d`/`22` records within a few seconds, and its
briefing screen should show with the count held at the room's whole minute. What
would falsify it is the same black screen with a silent entry pass, which would
leave the missing state in the run's contents or in the client's own phase rather
than in what the entry is answered with.

**Two differences beside it are measured and not changed.** The live client sends
its companion **before** its report where both references send it 0.4 s after, and
its entry echo carries the `0x8000` class bit on the `82` half where both
references write it plain; the host reads both by body, so neither is answered
differently here. And the briefing's beat still writes the round's own item boxes
and the rules pair every frame, where the reference's briefing writes them as
events (`docs/analysis/join-stall-after-mode2.md` §9), which is the next thing to
take out of the briefing window.

## 21. `myserver22.pcapng`: the entry's pass is silent, and the client tears the session down

§20's change is live. The pass that receives the lobby-slot echo writes nothing at
all — the pod's log has no `answering` line between the entry at 13:57:27 and the
reap at 13:58:35 — and the client does not reach its briefing screen. This time it
does not sit there either: it abandons the session itself.

### The session

`myserver22.pcapng` (client `192.168.1.20:5730` ↔ host `89.129.16.203:5731`, a
fresh room of one), against the deployed pod's log (`tmpout/ms22-server.log`) and
the client's own emulator log (`RPCS3.log`, whose clock starts 15:35:29 local,
which is 13:35:29 UTC). Times below are the pod's, UTC.

| UTC | what | |
| --- | --- | --- |
| 13:56:31 | the roster run, and the settings burst three seconds later | |
| 13:57:27 | the client's entry echo `82 f3` · `84 f3` (`0x9001` class `0x0d`, `0x1001` class `0x0e`) | **unanswered** |
| 13:57:29 | the client's report `0x1ade e1 01` | |
| 13:57:29 | the briefing's run, thirteen of its eighteen records acked within a second | |
| 13:57:30 | **the echo re-sent, unchanged** | 3 s retry |
| 13:57:32 | the run repeated one tick behind it | |
| 13:58:00.7 | **the client's last datagram** (pcap `t+106.68`) | |
| 13:58:10.7 | **the client's own modal**: *Disconnected from the host. Exiting the game.* | 42 s after the report |
| 13:58:35 | the host reaps it — 25 s after the modal | |

**Nothing was lost.** 1 777 host frames decoded from the capture under the
session key, no duplicate counters, and two gaps in the sequence (`37→40`,
`67→70`), both before the join. The client acknowledged the run, its repeat and
the countdown (ordinals 0…32) and stopped on the thirty-third tick.

### What the captures say the echo is answered by

Not by a record written beside it, and not by nothing. Both reference briefings
answer it with **the run's opening pair, matched by identifier and fourth byte**:

| capture | the echo | the run's opening pair |
| --- | --- | --- |
| `started-briefing-then-solo` | t+55.312 `82 7e` · `84 7e`, classes `0x0c`/`0x0d` | t+57.626 `0x5001` class `0x0c`, `0x5001` class `0x0d` |

(It is `0x5001` there because both halves are `0x1001` records; this client's
first half is `0x9001`, whose answer is `0xd001` with one body byte, which is why
`PeerAcknowledgementUtils` derives the pair rather than copying it.)

So the pair the run opens with is not a recorded head: it is the peer's own echo
answered, and this host was writing two empty `0x5001` records with class `0` in
its place. The client's two echoed records were therefore named by nothing, and
— §2 of `connection-not-stable-disconnect.md` — a record nothing names does not
age out of the peer's window.

### What was changed

| file | change |
| --- | --- |
| `GameplayServerService.Answers.cs` | the pass that receives the echo holds its answers instead of dropping them (`NoteEntryAnswers`) |
| `MatchRecords/MatchStartRunService.Entries.cs` | `entryAnswers` per session, dropped with the session's entry |
| `MatchRecords/MatchStartRunFramesUtils.cs` | `BuildRun` opens on those answers, falling back to the recorded empty pair; `RepeatOf` drops the opening three by position, since the pair now carries the peer's identifiers |
| `MatchRecords/MatchStartRunService.Runs.cs` | the run is built with the peer's entry answers |

### The rejoin, and the one thing it is missing

The client returned to the menu and joined again at 13:59:38. The handshake was
answered, the session restarted on its new base, the profile was registered, the
roster run went out at 13:59:40, and the room told it how many players it held.
The settings burst did not go out, and the log says why:

```
UDP 5731: settings burst already scheduled; not scheduling another for 89.129.16.203:5730
```

`PlayerSettingsBurstSchedulerService` keeps one flag for the process, so the
rotation and the loadouts are described to the first peer that ever joins and to
nobody after it. That matches the captures — the peers joining at t+154, t+385
and t+493 get none — but none of those is a session that *restarted*, which is
the case this build introduced. The client polled for twenty seconds and then
stopped, and the host reaped it at 14:00:30.

That is measured here and **not** changed: the capture holds no restarted session
on a reference host, so which of the two rules it takes is not something this
session can settle. What is settled is that a rejoining peer currently differs
from its own first join by exactly the burst.

### Not verified live

No client has joined this build. What would confirm it is this same join: the
run's opening pair should carry `0xd001` and `0x5001` with the echo's own fourth
bytes (`0x0d`, `0x0e`) instead of two empty `0x5001` class `0`, the client should
not re-send its echo, and it should write its `1d`/`22` stage records within a few
seconds of the run.

## 22. `myserver23.pcapng`: the entry is answered, the client stays, and it still
does not enter its briefing

§21's change is live and it does what it was meant to. The run's opening pair is
the peer's own echo answered, the echo is not re-sent, and the client keeps the
session up for the whole capture instead of tearing it down. It still never shows
the briefing screen, and the one thing both reference clients do in this window —
start their own operations stream — it never does.

### The session

`myserver23.pcapng` read as the host's side (`tmpout/ms23.log` through
`tools/pcap_gameplay_log.py`, `tmpout/ms23recs.txt` through
`tools/udp_session_records.py`): client `192.168.1.20:5730` ↔ host
`89.129.16.203:5731`, a fresh room of one, 233 s.

| t+ | what | |
| --- | --- | --- |
| 0.000 | the handshake | |
| 2.244 | the roster run, written twice as every capture writes it | |
| 4.863 | the settings burst: the rotation (161 B), two 50-byte loadouts, the 23-byte blob | |
| 56.248 | the client's report `0x1ade e1 01`, with its presence in the same datagram | |
| 56.279 | the report answered: the publication, the join-state answer and the marker | |
| 56.789 | the client's entry echo `0x1001` class `0x0d` (`82 f3`) · class `0x0e` (`84 f3`), beside its phase tick | |
| 56.822 | the tick's record-level answer `0xd001 24` | |
| **57.007** | **the briefing's run: eighteen records, opening on `0x5ade` and the two `0x5001` at the echo's own classes `0x0d`/`0x0e`** | §21's fix, **[V]** |
| 58.778 | the client acknowledges the run: eight `0x5001` at classes `06`–`0d`, the acks and the marker | |
| 59.510 | the run's second instant, marker and pair left out | |
| 60.473 / 62.601 | the client acknowledges the repeat and the beat | |
| 57.3 → 233 | the beat (rules pair, item boxes) and the countdown `0b 3c 00 00 00 00 00` every 2.5 s | |
| 73.6 | the client's room request `83 f3 00 07` | |
| never | the client's own operations (`0x1d`/`0x22`), a ready (`0x1f`), or a second report | |
| 14:49:28 | the client's last datagram, 30 s before the capture ends — the reaper's window | |

**The run is the reference's, record for record.** Its 90-byte state block is
byte-identical to `mgo2-game-started-on-briefing`'s, its stage records, its close
triple and its payload echo are the same bodies, and its clock differs only in the
room's own length (`01 20 0d` — 3 360 units, seven minutes — against that
capture's `01 00 0f`, eight). The two records no reference run is missing are
present, and the two the echoes of both captures open on are now the peer's own
answered.

### The one instant that is not the captures'

| capture | entry echo | the briefing's run | gap |
| --- | --- | --- | --- |
| `started-briefing-then-solo` | t+55.312 | t+57.626 | **2.31 s** |
| `started-on-briefing` | t+56.360 | t+58.255 | **1.90 s** |
| **`myserver23`** | **t+56.789** | **t+57.007** | **0.22 s** |

Both references write nothing at all between the echo and the run, and their
clients use that gap: `started-briefing-then-solo`'s writes its own operations
stream at t+57.304 — five seven-byte `0x1d` records and the twenty-two-byte
`0x22`, at the stream's own first six ordinals — **1.99 s after its entry and
0.32 s before the run arrives** (`docs/protocol/STATE_VECTOR.md` §1,
`docs/analysis/udp-handlers-and-senders.md` §5.5). This host's run answered the
echo inside that dwell, and the client acknowledged it and then wrote none of its
own operations for the rest of the session.

So §21 answered the echo with the right records in the wrong pass: the run is the
answer, and the captures space it two seconds from the entry rather than writing
it in the entry's own dwell. That is the rule §19 measured for the confirmation
(37 ms behind the echo, and the client stopped) — it applies to the run too.

**The room's briefing then never ends, and that is by design.** One player is in
the room, so the count is held at the room's whole minute and no deadline is
armed (`BriefingCountdownService.StartCounting`, §17): the round opens on that
player's own ready, and the ready is the one record the client still does not
send. Nothing about the host's briefing window is wrong on this capture — the
client is simply not in it.

### What was changed

| file | change |
| --- | --- |
| `MatchRecords/MatchStartRunService.Entries.cs` | the entry's instant is kept (`entryInstants`), `EntryRunDwellMilliseconds` is the measured two-second dwell, and `WriteBriefingRunAsync` moves here — it is the run written in answer to the entry — and waits the dwell out before writing, giving it up if the round opened inside it |
| `MatchRecords/MatchStartRunService.Runs.cs` | the watcher calls it; the file is back under the 400-line limit |

### Not verified live

No client has joined this build. What would confirm it is this same join: the
echo at the entry should be answered **two seconds later** instead of two hundred
milliseconds, the client should write its own `0x1d`/`0x22` operations in that
gap as both references do, and — the actual success line — its `1f` ready should
follow, which is what opens a room of one's round.

## 23. `myserver24.pcapng`: the client enters before it reports, so the two-second dwell is already spent

`myserver24.pcapng` is the first join of the build §22 shipped
(`mgo2-gameplay-1`'s ReplicaSet was created at 17:23:27 local and the capture
runs 17:26–17:29). The room is the same fresh room of one, and the record set is
the references' — the rules pair, the item boxes, the 90-byte state block, the
`85`/`86` arrays, the `02` mask, the clock, the `0b` countdown, the `e0`
publication and the settings burst, all present — so nothing is missing from it.

**The dwell did not apply, and the order of the two instants is why.** Both the
report and the entry were measured, and this client sends them the other way
round from both references:

| capture | report | entry (`82`/`84`) | entry − report | entry → run |
| --- | --- | --- | --- | --- |
| `mgo2-game-started-on-briefing` | t+55.838 | t+56.360 | +0.52 s | 1.90 s |
| `mgo2-started-briefing-then-solo-game` | t+54.918 | t+55.312 | +0.39 s | 2.31 s |
| **`myserver24`** | **t+59.024** | **t+56.047** | **−2.98 s** | **0.29 s** |

The echo arrived 2.98 s *before* the report, so `EntryRunDwellRemaining` found
its two seconds already spent when the report gave the briefing its set, and the
run went out 0.29 s behind the report — the same overtaking the fix was for. The
client acknowledged it and everything behind it, and over the following 117 s it
wrote none of its own `0x1d`/`0x22` operations, no `1f` ready and no second
report. `/proc` confirms the image was `4f8d653`, so this is the dwell's anchor
and not the deployment.

**What was changed.** The dwell now runs from the later of the peer's entry and
the briefing's first report (`briefingReportInstant`), because a client that
enters before it reports is still loading its map at the entry and its report is
the instant it finishes. That gives this join a run at report + 2.0 s, the gap
both references give their clients, and leaves a client that enters after its
report — the references' order — on the entry it already had.

**Not verified live.** What would confirm it is the same join: the run should
land about two seconds after the `e1` report rather than 0.29 s behind it, the
client should write its `0x1d`/`0x22` operations, and its `1f` ready should
follow.

## 24. The three streams a fresh room's briefing must not carry

§23's dwell is right and the client still does not show the briefing screen. The
newer captures say why, and they say it in three records rather than one: what
this host writes to a peer whose room has **not played a round** is the stream of
a room that has. This section is measured against the two split cases the
captures now hold — a fresh room's briefing (`mgo2-game-started-on-briefing`,
map 5, TDM) and a room between rounds (`mgo2-started-briefing-then-solo-game`,
map 5, its round 2) — and against the default room this host actually runs, the
map 7 TDM room of `mgo2-game-joined-when-red-team-then-briefing`.

### The run

`MatchStartRunFramesUtils.BuildRun` writes one shape for both the briefing and
the round. The reference writes two. Its fresh room's run at t+58.255 is ten
records — the two heads, the 90-byte block, the two-byte companion, the arrays,
the join-state answer, the close triple — and four item-box records; its
between-rounds run at t+57.626 is seventeen, with the room's stage (the 22-byte
record and the two 54-byte entries), the round's clock, the payload and the rules
pair. This host's briefing run at `myserver24` t+59.3 is the between-rounds
seventeen, in a room with no round behind it.

That is the symptom the user sees. The 22-byte `07 0f` record and its two `07 2f`
entries are the room's stage, and the clock behind them is a round's; a client
handed them while it is still loading its map concludes a round exists — in a
one-round rotation, *the final round* — and enters the game instead of showing the
briefing screen. The reference never sends them to a fresh room.

The report's answer is the same split. This host wrote the six-byte
`MatchStartPayload` (`e0 NN 00 00 00 00`) that game1's older room holds; both
captures of a fresh briefing answer with eight bytes (`e0 01 00 00 00 00 00 00`,
`mgo2-game-started-on-briefing` t+56.123), and the trailing bytes are zeros in
every form, so the shorter one was a length chosen from the only capture measured
then. `MatchStartBodiesUtils.MatchStartPayload` now writes the eight-byte form, in
the run and in the publication beside the report.

**What was changed.** `BuildRun` takes `carriesRoundState`, which
`WriteRunCoreAsync` derives as `!briefing || roundState.RoundNumber > 0`; a false
one leaves out the stage, the clock, the payload and the rules pair, and the close
triple takes the values directly behind the arrays (`BriefingRosterOrdinalCount`
= 5, `BriefingOperationsRecordCount` = 2), which is where the reference's t+58.255
run numbers them (`08`, `09` arrays then `0a`, `0b`, `0c` closes). A room that has
played a round keeps the full run, which is what t+57.626 measures.

### The beat

The beat started on the briefing's run, by design (§10–§14): a peer left with
nothing but sparse publications was the stall being fixed. The rate it then held
does not match a fresh room's briefing. Host frames a second, measured:

| window | `started-on-briefing` | this host (`myserver24`, t+65–170) |
| --- | --- | --- |
| fresh briefing | **3.8** (71 frames in 20 s) | **27.9** (2 924 frames in 105 s) |
| the round after it | **42.2** | — |

So a fresh briefing received roughly 56 rules records a second here against the
reference's 2 — the stream of a round, in a room that is not in one. **What was
changed.** `MatchRecordService` now starts the beat only when the room has a round
behind it or one is open; a fresh room waits for `WaitForOpenAsync` and a peer
that leaves before a round is written no beat. A room between rounds keeps the
beat through its briefing, which is `started-briefing-then-solo`'s 34 a second.

The beat's period is also held by a `PeriodicTimer` rather than a delay behind the
send: `Task.Delay(33)` cost the send as well and the live beat measured 28 frames
a second against the recording's `tickRate` of 30.303 (33 ms). The timer holds the
measured rate.

### The entry's tail, and the camera

The 25-byte entry's fifteen recorded bytes are two positions on the stage, and the
body this host wrote was `mgo2-started-briefing-then-solo-game`'s — **map 5's**.
This host's default room is Blood Bath (map 7), so the client was placed at map
5's spawn on Blood Bath, which is the camera outside the map the user reports:
`mgo2-started-briefing-then-solo`'s slot-3 body reads
`(2457, -50)` against Blood Bath's own spawns at `(-819, 2075)` and
`(2457, -2375)`.

**What was changed.** `RoomPlayerEntryBodiesUtils` no longer carries one map's
recorded bodies: it writes the pair `MapSpawnUtils` holds for the map the room
plays (`MatchSettingsService.Map`). Each pair was measured from a completed
Survival match downloaded through
`https://mgo2pc.com/api/v2/survival/matches` — its `replayUrl` archive, read by
the checked-in `docs/analysis/replay-console/rpdt.js` decoder. The decoder's
first frame is the round's opening; each player's first position is that
player's spawn, and the entry uses an actual member's spawn nearest the side's
centroid rather than an average. The side is the decoder's `teamGroup` (0 red,
1 blue), not the raw `0`/`2` team byte; all nineteen replays were cross-checked
against the match record's `redPlayers` and `bluePlayers`, and the two sets
agree. The second point is the other side's spawn, which is the direction both
teams face at that opening. The replay's world
coordinates are divided by `PlayerPosition.CoordinateScale` to the entry's
signed 16-bit units, so Desert Duel and Icebound Inferno's far sides stay inside
the record. `19` Perilous Palace and `20` Quaint Quarrel have no completed
Survival match in the API's list, so no replay exists for them; they fall back
to Blood Bath's pair until one is measured.

### Not verified live

No client has joined this build. What would confirm it is the same join: the run
should carry no `07 0f`, `07 2f` or round clock before the round opens, the host
should write about four frames a second through the briefing, the client should
show the briefing screen and write its `1d`/`22` operations, and its camera should
sit on a spawn of the configured map rather than outside it.

## 25. `myserver26.pcapng`: the briefing's run is right, and the entry takes the client into the round

**What the live join holds.** `myserver25` and `myserver26` are two joins of the
build pinned at `aa9703a`, and §24's run is fixed in both: the frame that answers
a fresh room's briefing carries the marker, the heads, the state block and its
companion, the arrays, the join-state answer and the close triple — and **no room
stage, no round clock, no payload and no rules pair** (`myserver26` t+56.351). The
briefing's own countdown rides behind it, `0b 3c 00 00 00 00 00` every 2.5 s: held
at the room's whole minute with an empty ready set, which is
`docs/protocol/BRIEFING_FLOW.md` §1's frozen hold for a room of one that never
readied. Nothing in the capture opens a round — the operations identifier never
flips generation and the count stays `3c` to the last frame.

**And the client still does not show the briefing screen.** It leaves the briefing
0.9 s after one record, and that record is not the run:

```
t+51.820  IN  e1 01                    the report
t+56.351  OUT the briefing's run        (its shape, no round state)
t+67.770  IN  83 f3 00 07              the client asks to be placed in the room
t+67.840  OUT 85 f3 / 86 f3            the lobby-slot pair, slot 1 occupied
          OUT 07 12 00 04 00 01 …      the player's own entry, with map 7's spawn
          OUT 07 00 00 00 00 00 03     the roster close
t+68.740  IN  08 4f / 08 4d / 08 51 …  the client's own position and aim records
t+69.510  IN  (nothing after this)
```

The client never writes its `1d`/`22` operations, never readies, and its own
position records — the records a client writes from inside the map — start
0.9 s after the entry. `myserver25` is the same join to the second (request
t+186.98, entry t+187.00, first position t+191.11) with the entry body byte for
byte identical. RPCS3 then reads `[r28 + 0xb0]` with `r28` clear and freezes,
28 s after the client's last packet (`0:21:04.471`, CIA `0x8d2fbc`): the client is
in a map whose round never opened.

**Every live capture says the entry is the record that does it.** Counted over the
whole `myserver` series — twenty-six sessions — the client's own position records
exist in exactly the two that were written a 25-byte entry, and in none of the
twenty-four that were not:

| capture | entry written | client's first position record |
| --- | --- | --- |
| `myserver26` | t+67.8 | t+68.7 (0.9 s later) |
| `myserver25` | t+187.0 | t+191.1 (4.1 s later) |
| the other twenty-four | never | none — the client sits in the briefing, which is §15's stall |

**The reference writes it only for a round.** Split the six captures' answers to
the same `83 NN` request by the phase the room was in:

| capture | phase | answer |
| --- | --- | --- |
| `mgo2-game-started-on-briefing` t+93.13 | briefing | pair, countdown, 24-byte loadout header — **no entry, no close** |
| `mgo2-game2` t+78.99 | briefing | pair, countdown, loadout header — **no entry, no close** |
| `mgo2-game1` t+201.04 | round | pair, **entry**, close, the requester's entity block |
| `mgo2-started-on-solo-game` t+101.41 | round (clock `01 20 0d` at t+99.03) | pair, **entry**, close, loadout header |
| `mgo2-started-briefing-then-solo` t+180.49 | round (opened t+174.371) | pair, **entry**, close, beat |
| `myserver26` t+67.84 | **briefing** | pair, **entry**, close |

The entry carries the map's spawn and the player's side, which is where a player
stands *in a round*; the reference publishes it with the round's records — the same
six entries for the room's six players ride the round's run at t+134.92 of
`joined-when-red-team` — and never while the room is being briefed. Handed one
there, the client acts on it: it leaves the briefing screen for a map that has no
round behind it, which is the "round start" the player sees in place of the
briefing and, in this room's one-round rotation, the *final* round.

**What was changed.** `TeamArrayService.EnterRoomAsync` writes the player's entry
and the roster close only while `MatchRoundStateService.IsOpen`, and answers a
briefing with the records the reference answers one with: the pair, the countdown
(`BriefingCountdownService.AnswerRecord` — the tick's own body and its own stream,
so the record is one the client could have received a tick later) and the
player's twenty-four-byte settings header (`SettingsHeaderBodiesUtils`,
`0b <key+1> 02` and a zero tail). The answer is still written in the pass that
received the request, still under the sequence the request named.

**And the briefing now carries its own beat.** The rules-roster pair
(`0x0261 fe`) used to stop at the peer's run, on the reading that the reference
writes nothing of the room's between its run and its round; the measurement says
the opposite. Over `mgo2-game-started-on-briefing` t+68.0 to t+92.0 — twenty-four
seconds behind the run at t+58.255 — the host writes 22 pairs at nine instants
1.9 to 4.4 s apart (some instants carry the pair two to four times), with the
`0xd001` controls and `0x0002` blobs riding among them, and nothing else of the
room's. So `BriefingRoomPublishService` runs until the round
opens rather than until the run is written, and the window that used to carry the
countdown alone (0.4 frames a second) now carries the pair on the same tick
(0.8 to 1.2), against the recording's 1.3.

**Not verified live.** No client has joined this build. What would confirm it is
the same join: the `83` answer should carry four records — pair, countdown,
header — and no `07 12` body, the client should stay on its briefing screen,
write its `1d`/`22` operations and then its `1f` ready — and the round should
open from that ready rather than from a record the client acted on.

**Still open.** The three records below, which the reference writes in this window
and this host does not; and the header's trailing byte — `0x00` and `0x01` for the
same slot in the same briefing — a per-player value no record here carries, and
written zero.

### To write later: what the reference writes in that window and this host does not

Each of these is measured in the reference. The first two are written here now and the
third is not; each item carries what changed under it. They are listed with the
measurement behind them so the next change starts from bodies and a cadence rather than
from a resemblance.

**1. The `0xd001` controls, on a 4 to 5 s tick.** One-byte bodies under a session
sequence — `mgo2-game-started-on-briefing` t+56.123 `05`, t+65.124 `0c`,
t+69.999 `03`, t+74.495 `0d`, t+78.874 `03`, t+83.495 `06`, t+90.748 `0a`;
`mgo2-game-joined-when-red-team-then-briefing`'s briefing t+65.138 `0c` to
t+111.166 `08`, ten of them, 1.8 to 9 s apart. The bodies come from a small set
(`00 03 04 05 06 08 0a 0c 0d 10 2c`) and what they mean is **[U]**;
`SparsePhaseFramesUtils` already pins its own `02`–`09` for the steps before the
run, and those are the capture's own values. **Written since:** the continuation goes
out on the beat's insert, every ~5 s through a briefing, carrying those pinned values
rather than the reference's per-copy code — §30.3 counts it in this project's own two
captures and §30.5 says where the byte lands in the client.

**2. The `0x0002` state blobs, on a 10 s tick and one per player.** The 22-byte
body the sparse phase writes, but re-published for the whole room and not once:
`mgo2-game-started-on-briefing` t+62.878, 72.554 and 83.495 carry one, its fourth
byte `02` each time and its trailing value `0x000a`; `joined-when-red-team`
re-announces every slot — `02`–`06` at t+62.667, `02`–`06` again at t+72.560,
`07` and `08` at t+76.665, all of them at t+83.546, 94.541 and 101.665 — with the
value `0x32` (50) for most and `0x0a` (10) or `0x64` (100) for some. So the
fourth byte is the player's own place and the body is that player's state, which
is the burst's blob written per player rather than once. The two open questions
are the same one: **what the trailing value is** ([U]; the burst's own is 50 at
the join, 100 through the round) and which players a tick re-announces.

**3. The `1250`/`125a` groups inside the bursts a join triggers.** Both captures
write them in the seconds after the room's run — `mgo2-game-started-on-briefing`
45 groups across t+58.3 (3), t+60.4 (18), t+62.9 (9) and t+65.1 (15), and
`joined-when-red-team` 33 across t+62.7 (15), t+65.1 (16) and t+67.7 (2) — and
**nowhere else in the briefing**: the steady window t+68.0 to t+92.0 above holds
none. They are a
round's item-box group with no round behind it, and in `joined-when-red-team`
they carry content — the 10-byte position records (`02 a2 fe 07 ea 20 03 00 00
00`) and 9-byte tails beside the 6-byte pair — which is the map's own objects,
not a body this host holds. The two six-byte filler pairs are writable from
`ItemBoxRecordBodiesUtils`; the position and tail records are not, so a group
written here would be the filler alone and the client would be told about boxes
that were never placed.

**All three were then measured again and closed or re-read in §27** — the control
bodies are not derivable (§27.2), the blob already matches the closest
reference byte for byte (§27.3), and the item-box group belongs to the room that
writes it rather than to a briefing (§27.4).

## 26. `myserver27.pcapng`: the briefing's run answers the report a second time

`myserver27` is the first join of the build §25 shipped (`deploy` pin `a144b37`
→ `2dc7da9`): one client (`192.168.1.20:5730` ↔ host `89.129.16.203:5731`), a
fresh room of one, 176 s. Every time below is seconds from the flow's first
datagram.

**What §25 fixed, measured on the wire.** The `83 f3 00 07` request at t+108.411
is answered 14 ms later with the pair, the countdown `0b 3c 00 00 00 00 00` and
the 24-byte `0b 02 02 …` header, and with **no** `07 12` entry and no roster
close. The run at t+57.626 carries no room stage, no round clock, no payload and
no rules pair, and the countdown is held at the room's whole minute with an empty
set to the last frame. So the records §25 names are gone from the window.

**The client still does not leave the fade**, and its own stream says which record
it acted on. The operator's description is the map, then the rule text, then a
fade out that never fades in to the briefing; the client never writes `1f`, never
re-reports, and goes quiet at t+146.4. What it does write are two shapes no
reference client writes — `0b0c` (16 B, 12 times, ~6 s apart) and `1ae2 … 1ae9`
with the body `ff 01` (eight records, 14 times, ~6 s apart, acknowledged by the
host as `5ae2 … 5ae9` and otherwise unanswered). Those are the client's phase, not
this host's records, and are left [U] here.

**The one host record that says a round is starting was written twice.** The
report at t+54.698 is answered at t+54.740 with the marker `0x5ade` beside the
publication and the companion — which is what `mgo2-game-started-on-briefing`
writes at t+56.123 (`0x52de`, `0x025c`, `0x12de e0 01 …`). The run that follows
at t+57.626 then opens on **`0x5ade` again**:

```
54.740  OUT 0x5ade / 0x0a5c 02 00 00 00 / 0x1ade e0 01 00 00 00 00 00 00   the report's answer
57.626  OUT 0x5ade 0xd001 24 0x5001 0x1a54 90 …                            the run, marker first
```

**The marker belongs to whichever record answers the report, and to one only.**

| capture | the report | answered beside it | the run's opening |
| --- | --- | --- | --- |
| `mgo2-game-started-on-briefing` | t+55.838 | t+56.123 `0x52de` + `0x025c` + `0x12de` | t+58.255: two `0x5001` heads, **no marker** |
| `mgo2-started-briefing-then-solo` | t+54.918 | nothing — the next host datagram is the run | t+57.626: `0x52de` + two heads |
| `mgo2-game1` (round) | t+44.76 | nothing | t+46.415: `0x5ade` + two heads |
| `mgo2-game2` (round) | t+56.72 | answered before the run | t+62.181: one head, **no marker** |
| **`myserver27`** | t+54.698 | t+54.740 `0x5ade` + publication | t+57.626: **`0x5ade`** + `d001` + one head |

This host answers every report beside it (`JoinReportHandler`), so its briefing's
run is never the report's answer and must not open on the marker. A client handed
one there has been told twice that a round is starting, three seconds after it
entered, and the round it waits for never opens — the fade out with no fade in.

**What was changed.** `MatchStartRunFramesUtils.BuildRun` takes `answersReport`,
and a briefing's run omits the marker (`MatchStartRunService.Runs` passes
`!briefing`; a round's run still carries it, which is the record that answers the
report that opened the round). `RepeatOf` takes the generation and drops the
opening by what the run actually carries — three records when it opens on the
marker, two when it opens on the entry's pair.

**Not verified live.** What would confirm it is the same join: the run behind the
entry should open on the two answers to the `82`/`84` echo and carry no `0x5ade`,
the client should stay on its screens past the fade, write its own operations and
then its `1f` ready — and the round should open from that ready rather than from
a record the client acted on.

## 27. The briefing's remaining gaps, measured against the two references

§25 listed three records the reference writes through a briefing and this host
does not, and §26 left two client-only shapes unexplained. This section answers
those five questions from the captures and from `MGO2.ELF`, so the next change
starts from a measurement rather than from a resemblance. Nothing was changed
in the server for this section: what it establishes is that three of the five
are already matched or not writable, and what it leaves open is named.

### 27.1 The identifiers are a vid under class bits, and that names both client shapes

Every identifier in the session is `class bits | vid`, and reading them that way
turns the two "client-only" shapes into ordinary streams:

| identifier | bits | vid | what it is |
| --- | --- | --- | --- |
| `0x0a5c` / `0x025c` | `0x0800` / — | `0x25c` | the presence companion |
| `0x0a61` / `0x0261` | `0x0800` / — | `0x261` | the rules pair |
| `0x1a54` / `0x1254` | `0x1800` / `0x1000` | `0x254` | the operations stream |
| `0x1ade` / `0x12de` | `0x1800` / `0x1000` | `0x2de` | the readiness report and its publication |
| `0x5ade` / `0x52de` | add `0x4000` | `0x2de` | the same record with the answer bit |
| `0xd001` | `0x4000` on `0x9001` | `0x001` | the answer to a join request |
| **`0x0b0c`** | `0x0800` | **`0x30c`** | the live client's 16-byte record |
| **`0x1ae2`…`0x1ae9`** | `0x1800` | **`0x2e2`…`0x2e9`** | the live client's `ff 01` reports |

So the eight `ff 01` records are not eight unrelated identifiers: they are the
eight vids immediately above the readiness vid `0x2de`, and the `0x0b0c` record
is one vid of its own under the `0x0800` class. [V] the arithmetic, over every
identifier in all the captures; what either vid states stays [U].

**Both shapes track the build, not the client.** `myserver24` — the join
captured before §25 — carries neither: no `0x0b0c` and no `0x1ae2`…`0x1ae9` in
the whole session, while `myserver27` carries twelve of the first and fourteen
groups of the second. The client is the same console in both, so the two shapes
are a reaction to what this host now writes, not a constant of the client.

### 27.2 `0xd001` is an answer per request, and its body is not derivable — the constant stays

§25 item 1 asked for the control bodies to continue through a briefing. Read as
a request/answer pair, the identifier is `0x4000 | 0x9001`, and the captures say
what the pair does:

| capture | client request | host answers | distinct bodies written (count in brackets) |
| --- | --- | --- | --- |
| `started-on-briefing` | `0x9001` len 1 body `00`, every 3–5 s, class 3…19 | `0xd001` len 1, same class | `01`…`10` and `12`, `9a` — `0a`(9), `10`(7), `02`(7), `0b`(6), `01`(6), `03`(5) … |
| `joined-when-red-team` | same, class 3…19 | same | `08`(48), `10`(45), `04`(38), `0c`(33), `00`(11), `14`(7), then `2c 34 58 78 7c 84 8c a4 b4 bc c8` once each |
| **`myserver27`** | same, class 11…21 | same | `24`, every time |

The request body is **always `00`** and the answer's class is the request's own,
so the body is neither an echo nor a reply derived from what was asked: it varies
inside a session with no record between the two that could move it. The two sets
are not even shaped alike — the five-player room's are all multiples of four
(`00`, `04`, `08`, `0c`, `10`, `14`, `2c`, `34`, `58`, `78`, `7c`, `84`, `8c`,
`a4`, `b4`, `bc`, `c8`), while the one-player room's run `01` to `10` with `12`
and `9a` among them — so whatever the body carries, the two rooms do not carry
it the same way, and no field is being echoed under two codings without a
mapping the captures do not show. Its meaning is therefore [U] and the value is
not writable faithfully, which is what `PeerAcknowledgementUtils.HostControlByte`
already documents. Nothing in the captures shows a client's next record following
this body, so it is not a gate either. **Closed: no change.**

### 27.3 The `0x0002` blob is already the reference's, byte for byte

§25 item 2 asked for a per-player re-announcement on a ten-second tick. The
closest reference — the same one-player room, `started-briefing-then-solo` —
writes exactly what this host writes:

| | the join's blob | the tick's blob |
| --- | --- | --- |
| `started-briefing-then-solo` | t+6.006 class 4, `…40 00 00 32` | class 1, `…40 00 00 64` at 8.99, 15.1, 20.4, 27.4, 32.6, 39.2, 44.0 … |
| `myserver27` | t+4.761 class 0, `…40 00 00 32` | class 1, `…40 00 00 64` at 7.17, 14.4, 19.5, 26.6, 31.5, 38.2, 44.5 … |

Same body, same two values, same tick. The *per-player* form §25 describes
(bodies under classes 2…9 with `32`, `0a` and one `64`) is in
`joined-when-red-team`, a room of five, and its values are constant per slot for
the whole briefing — a slot at `0a` stays at `0a` for two minutes while one
momentarily reaches `64` — so they are not a load percentage climbing to 100 and
the value is [U]. **Closed for a solo room; not indicated for a room of one.**

**Withdrawn in part by §28.** That comparison stopped at t+44, where this host's
replay of the recorded sparse phase stops; the reference host goes on writing the
blob for the rest of the session, and one copy per player rather than one for the
room. §28 measures it over the whole corpus and the tick is written now.

The one byte that differs is the join blob's class: `4` there, `0` here. In
`joined-when-red-team` that class is the player's lobby key (`2`…`9`), so it is
the announcing player's own slot rather than a constant — a measured candidate,
left open below.

### 27.4 The `1250`/`125a` group is the room's, not the briefing's

§25 item 3 listed the item-box group as missing from a briefing. The two
reference briefings disagree, and that settles it:

* `started-on-briefing` t+58.255 — its briefing's run carries **24 six-byte
  filler pairs**, `0x1250` and `0x125a` alternating, each pair `000000000000`
  and `010000000000`, and no position or tail record at all in the window.
* `started-briefing-then-solo` t+57.626 — its briefing's run carries **none**;
  the run is the marker, two heads, the state block, the pair, the three
  room-state records, the close, the clock and the publication.

So the group rides the run of the room that has one and not of the room that
does not, and what decides it is not the phase. Writing it would be copying one
room's contents into another's; **closed: not written.**

### 27.5 What the reference's run carries that a solo room's does not

Side by side, the two runs are close, and both references' *pair* marks no slot
occupied while this host's marks the joiner:

| record | `started-briefing-then-solo` | `myserver27` |
| --- | --- | --- |
| marker `0x52de`/`0x5ade` | present (this run answers the report) | **absent**, per §26 |
| two `0x5001` heads | `f2=12, 13` | `f2=12, 13` |
| state block + `0f00` | `90` + `2` | `90` + `2` |
| pair `0x9001`/`0x1001` | `85 7e fe ff…` / `86 7e 7f 7f…` — **no slot marked** | `85 f3 fe 00 ff…` / `86 f3 7f 07 7f…` — slot 1 marked |
| room state | 22 + 54 + 54 | 22 + 54 + 54 |
| close pair + close | `20`+`20`+`7` | `20`+`20`+`7` |
| round clock | `0x1254` len 3 `01 60 09` | none |
| publication | `0x12de` len 14 | `0x1ade` len 8 |
| rules pair | six `0x0261` in the run | none in the run; the tick writes two `0x0a61` 44 ms later |

The clock and the publication's length are the room's own (the clock is 2 400
units = 300 s there, and §25 already measured 5-, 6-, 8- and 14-byte
publications across the corpus). The **pair's occupancy** is the one difference
that is not a value: both references' runs publish the pair with every slot
free, and the slots are marked later — `game1`'s pair gains `(14, 0)` 27 s after
its run — while this host marks the joiner at the run. It was left open rather
than changed, because `joined-when-red-team`'s join burst does mark its slots
and nothing measured said a marked slot in a briefing's run was wrong;
**§29.1 closes it**: the run now publishes both arrays with every slot free and
the entry answer marks the slot.

Two more of this section's numbers are superseded. The "six `0x0261` in the run"
and the 44 ms behind it are one run, not a rhythm; **§30.1** measures the pair's
tick over ten captures (2.13–2.38 s median, and 0.07 s once a round plays), and
**§30.2** measures what a tick carries — the pair alone, at every tick.

### 27.6 What `MGO2.ELF` says, and what it does not

The binary was read with the Capstone readers in `tools/` (`mgo2_immediate_scan.py`,
plus the raw scans used below), and it answers the question §25 would have
asked of it — *what does the client mean by these bodies* — in the negative:

* **No site constructs any of these vids as an immediate.** Sweeping every
  `addi`/`addis`/`cmpwi`/`cmplwi`/`ori`/`oris` word whose low half is `0x30c`,
  `0x2e2`, `0x2de`, `0x254`, `0x25c`, `0x261`, `0x001` or `0x002` returns no
  identifier build: the hits are the `0x0254xxxx`/`0x0261xxxx` pointer setups
  (`lis` at `0x01076760`, `0x010a9c1c`, `0x0100ef24`) and structure displacements
  (`stw r0, 0x1a54(r11)` at `0x0043283c`). The ids are read from a table, which
  the corpus already records for `0x1ade`/`0x0a5c`
  (`docs/protocol/UDP_GAME_CAPTURE.md`), and that is true of the two shapes of
  §27.1 as well.
* **The 16-byte body is computed, not stored.** Its invariant word `46 1e e3 00`
  occurs nowhere in the image, so the body is not a recorded constant a scan can
  follow to its builder; the vid has to be found through its handler.
* **Finding the handler needs the analysed project.** `docs/GHIDRA.md`'s
  instructions are for the machine that holds `MGO2.gpr`; there is no Ghidra on
  this host, and the table-driven dispatch is not reachable by immediate or
  string search. Until the project is analyzed and `Mgo2Probe.java offset` can
  be asked for the vid table's reader, the two shapes of §27.1 stay [U] — named
  now, which they were not before.

### 27.7 What is left

1. The two client-only vids (`0x30c`, `0x2e2`…`0x2e9`) — named, both appearing
   only on this build. The next measurement is a live run of the §26 fix: if
   they vanish, they were §25's records being answered twice; if they remain,
   they are the next thing to read with a decompiler.
2. ~~The join blob's class byte~~ **closed by §28**: the class is the player's slot
   byte, and the burst's closing copy carries the joining peer's.
3. ~~The run's pair, which this host marks occupied and both references' runs do
   not~~ **closed by §29.1**: the run now publishes both arrays with every slot
   free, and the marking is the entry answer's.
4. ~~The settings header's trailing byte~~ **read by §29.2**: it is not a trailing
   value at all but the byte of the one slot-state field the body carries, and
   what names it is still [U].

## 28. The `0x0002` blob is one record per player, and a briefing keeps writing it

§27.3 closed the blob on the join window — eight copies, then silence on this host,
against a reference that writes the same eight — and left the value and the class
open. Read over the seven captures the repository holds, whole sessions rather than
their first minute, both are settled: the record is **the host's statement about one
player**, the fourth byte is that player's slot byte, and no briefing stops writing
it.

### 28.1 The record is the host's, one per player, keyed by the player's slot byte

Decoded with `tools/udp_frame.py`'s frame reader (`tools/udp_briefing_flow.py` carries
the pipeline); every `0x0002` of twenty-two bytes in the five reference captures and
`myserver27`:

| capture | records | client-sent | fourth bytes | players in the room |
| --- | --- | --- | --- | --- |
| `mgo2-game1` | 141 | **0** | `01`, `05`, `07`, `0c`, `10`, `15` | host + keys `04`, `06`, `0b`, `0f`, `14` |
| `mgo2-game2` | 45 | **0** | `02`, `03` | host + key `01` |
| `mgo2-game-started-on-briefing` | 86 | **0** | `02`…`07` | host + keys `01`…`06` |
| `mgo2-started-briefing-then-solo-game` | 51 | **0** | `01`, `04`, `08`, `14` | host + keys `03`, `07`, `13` |
| `mgo2-game-joined-when-red-team-then-briefing` | 570 | **0** | `02`…`15` | host + keys `01`…`14` |
| `mgo2-joined-game-in-progress-last-round` | 69 | **0** | `01`, `08`, `0c`, `14` | host + keys `07`, `0b`, `13` |
| **`myserver27`** | **8** | 0 | `00`, `01` | host + key `01` |

Two facts fall out. **The fourth byte is the player's own slot byte, key plus one** —
the byte the loadout records carry (`UDP_SETTINGS_PAYLOAD.md`) and not the raw table
key the pair writes: `game1`'s five joiners are keys `04`, `06`, `0b`, `0f`, `14` and
the capture announces `05`, `07`, `0c`, `10`, `15`, with `01` for the host's own slot
zero. And **the record never travels from the client**: none of the 962 copies in the
corpus is inbound, so it is the host's own report and nothing echoes it. The body is
one shape in all of them — eighteen zeros, `40`, `00`, a `u16` — which is
what `MatchStateBodiesUtils` writes. **§28.7 re-reads that shape**: the leading
twenty-one bytes are a state transfer's presence map, the `0x40` is bit 150 of it,
and the last byte is that field's value — the trailing two bytes only *look* like a
big-endian `u16` because the map's last byte is zero.

### 28.2 The value is the player's own, it starts at 50, and it moves

The value per fourth byte, in order (§28.7 makes it one byte, not a `u16`):

| capture | fourth byte | value over the session |
| --- | --- | --- |
| `game1` | `07` | 50, 69, 93, 99, 98, 94, 100, 98, 94, 100, 93, 98, 99 |
| `game1` | `0c` | 50, 55, 73, 83, 77, 78, 83, 78, 73, 85, 74, 79 |
| `game1` | `10` | 50, 91, 90, 94, 98, 94, 99, 94 |
| `game1` | `15` | 50, then 0 for the rest of the session |
| `game1` | `01` | 100, every copy |
| `started-on-briefing` | `02` | 50, then 10 for 60 s |
| `started-on-briefing` | `03`, `04`, `06` | 100, every copy |
| `started-briefing-then-solo` | `04` | 50, then 0 |
| `started-briefing-then-solo` | `08` | 50, 38, 0 |
| `joined-in-progress` | `14` | 89, 84, 89, 84, 85, 90, 87, 92, 88, 83 … |
| `joined-in-progress` | `0c` | 0, 9, 35, 30, 40, 0 |
| `myserver27` | `00` | 50 — once, at the join |
| `myserver27` | `01` | 100, seven times, then nothing for 100 s |

So the value is **per player**, **bounded by 100**, **starts at 50 the first time a
player is named**, **sits at that player's own level afterwards**, and **falls to 0
when that player's records stop**; the host's own slot reads 100 in every capture
that holds it. It is not a clock, a count of players, a round length or a load
percentage that climbs once: `game1`'s class `0c` moves 73 → 85 → 74 and
`joined-in-progress`'s class `14` moves 89 → 84 → 92 → 83 with no direction, which is
a figure the host measures per peer and not a monotone.

**The standing reading is the peer's round-trip time in milliseconds, capped at
100.** Three things in the values point at it and rule out a stored attribute — the
class of thing the lobby's own player record carries:

* **A level, a clan emblem or a friend flag does not move.** Those are one value
  per player for the session, and the blob's moves per copy: `game1`'s class `0c`
  reads 73, 83, 77, 78, 83, 78, 73, 85 and `joined-in-progress`'s class `14` 89,
  84, 89, 84, 85, 90, 87, 92, 88, 83. A few milliseconds of jitter between
  samples is what a latency does and what none of those fields do.
* **Every player starts at 50 and the host's own slot reads 100.** No attribute
  schema starts every player at 50, and no player's own level or clan is 100 by
  being the host. 50 is a "not measured yet" sentinel and 100 a cap and a self
  value.
* **A silent peer falls to 0**, which is what a round-trip time reads when nothing
  is coming back, and not what a stored field does.

**And the TCP lobby carries exactly this per character.** `0x4398 UpdatePings` is
sent by the host only, is keyed by character identifier, and stores one `u32` per
player (`UpdatePingsHandler` → `GamePlayer.Ping`, written back out by
`RoomBrowserHandlers`); `TCP_GAME_SERVER_PROTOCOL.md` calls the pair "targetId+ping
pairs". So the same host that writes the blob already has a per-player RTT to put in
it, over the other channel, in the other direction — a ping is the one per-player
figure the client is known to be handed, and it is the shape the numbers have.

**What it measures is still [I]**, because nothing on the UDP wire names the unit and
the client's handler is not reachable to settle it: the identifier is registered
(the inbound registry's entry 0, `udp-record-dispatch.md` §4.1) but its handler is
read off a runtime container and no site builds the vid as an immediate (§27.6), and
the replay console does not decode the record at all
(`RPDT_REPLAY_RECORDS.md`, the `0x0002` row). What this does settle is that it is a
measurement of the peer and not one of its stored attributes, which is what a server
has to know before it writes one.

### 28.3 What the briefings do, and what this host did

**The tick does not stop at the sparse phase.** `mgo2-game-started-on-briefing`
writes the blob at t+6.0, 12.2, 22.7, 32.4, 38.9, 42.7, 51.6, then 62.9, 72.6, 83.5,
93.1, 102.8 as its briefing runs to 198 s, and on through its round: gaps of 3.9 to
11.3 s, which is the host's own tick and not a fixed period.
`mgo2-started-briefing-then-solo-game` writes one every 5 to 9 s from t+9.0 to the
round it opens at t+174, its host's copy at class `01` and the guest's at `04` from
t+92.9 on, two per instant once there are two players.

This host's sparse phase is a replay of the recorded host's own draw, so its eight
blobs at t+4.8…44.5 are the recording's — and then it wrote **no `0x0002` at all**
for the 100 s of briefing that followed (`myserver27`), while its join burst's
closing copy carried class `0` where the reference names the joining player's slot
byte. The client was told its own state once at the join and once per sparse step,
and never again from its own slot.

### 28.4 What was changed

`BriefingRoomPublishService` writes the blob again, on its own ten-second tick, for
as long as the briefing runs:

* **one record per player of the room**, its fourth byte the player's slot byte
  (`key + 1`) from `LobbySlotService`, which is the byte every capture carries;
* **from the end of the sparse phase onward** — `SparsePhaseFramesUtils`'s steps are
  the recording's and a blob written among them would be a copy no capture holds;
* **the host's own slot 100 and every peer 50** (§28.7.3): the field is the host's
  measurement of that peer, and 50 is the pre-measurement value — every peer of
  seven of the ten captures is named with it first, and it is the value the
  join-time burst carries. A host that later reports pings over TCP
  (`UpdatePingsHandler`) has the measurement and can write it here instead.

The settings burst's own closing copy takes the same rule from the other side: it
is sent to one peer, so its fourth byte is **that peer's** slot byte
(`PlayerSettingsBurstService.BuildSettingsBurst`'s `answeringSlot`), which is what
all three captures that hold a burst carry.

### 28.5 What is left

1. **The burst's closing copy names the joiner now.** It wrote class `0`; the
   scheduler resolves the peer's slot from the roster
   (`RoomRosterService.Find(context.Session.RemoteAddress)`) and the burst writes
   `key + 1` — `4` in `started-briefing-then-solo`, `2` in `started-on-briefing`,
   `0x15` in `game1` — which is the byte each capture carries. It is `0` only for a
   peer the roster cannot name, which is a burst that should not have been
   scheduled.
2. **The value is a measurement this host does not make**, so 100 is a statement
   that the peer is there and nothing more.
3. **Nothing here is verified live**: the change is measured against the captures and
   the build and the codec tests pass, but a client has not joined it. What would
   confirm it is the same join, its briefing carrying one `0x0002` per player every
   ten seconds with the client's own slot byte, and the client left on its screens
   past the fade.

### 28.6 What `MGO2.ELF` says about it, and where it stops

The binary was swept with the Capstone readers in `tools/`
(`mgo2_immediate_scan.py`'s segment map plus the raw scans below), for the four
things a builder or a reader of this body would have to contain.

* **The marker is not a constant any site sets.** No instruction in the image
  loads `0x4000` at all (`addi rD, 0, 0x4000` → **0 hits**), so the word at body
  offset `16` is not written as a literal from code — it is either computed or
  copied. `li rD, 0x40` near a `stb …, 0x12(…)` returns six sites, all of them
  `0x0053…`/`0x0091…` graphics code, and none is on this path.
* **There is no static template of the body.** A `18`-zero + `40 00` sequence
  appears seven times in the file, every one of them unaligned inside other data,
  and none in a read-only block a body would be copied from.
* **The value is computed somewhere with a 100 cap.** The only `li rD, 100` beside
  a `sth rS, 0x14(…)` in the whole image is `0x00263874`, in a block that reads a
  `u16` at `+0xb2`, multiplies it by `200` (`mulli r0, r0, 0xc8`), divides by
  `100 − x` (`subfic r9, r28, 0x64`), clamps the result at `100`
  (`cmpwi r25, 0x64` / `ble`), compares two such figures at `19` (`0x13`) and `9`
  and averages them. That is a **percentage the host derives per peer with the
  blob's own cap**, and it is the only one in the module the record handlers live
  in — a lead for the value's meaning, not a reading.
* **The registry entries are not built as immediates either.** Of the eighteen
  entry words `udp-record-dispatch.md` §4.1 reads out of the live container
  (`(index << 12) | vid`), only four occur as immediates anywhere in the text, and
  the one that looks like an identifier — `0x1001` at `0x00bdc688` — is an `ori`
  of a UI flag on an object at `+0x64`, not a registration. The table is filled
  from computed values, which is what the dump read.

So the handler for vid `0x002` is not reachable this way: it is bound at runtime
into a container whose populate sites do not name the vid, and the body it reads
carries no literal a scan can follow back. **§28.7 supersedes this section's
conclusion**, though not its measurements: the route that closed it is not the
vid's handler but the body's own shape, which is a parse `MGO2.ELF` contains in
full and whose length arithmetic the corpus checks.

## 28.7 The handler is the state dispatcher, and the body is one field of a slot state

The four scans of §28.6 were looking for a per-`vid` handler and a body built from
literals. Neither exists for this record: the client's body dispatcher reads a
**state index, a presence map and the present fields**, and this record is one of
those transfers — the shape `udp-record-dispatch.md` §3.5–§3.6 reads out and puts at
`0x27E8B0`, which is the module object's own vtable slot 0.

### 28.7.1 The length arithmetic closes, in all 1429 copies

The record is `{ id u16, len u8, class u8, body }` and for an id below `0x1000` the
`len` byte counts the class byte with the body (`tools/udp_frame.py`,
`parse_messages`). Every one of the 1429 copies of `0x0002` in the thirteen captures
in the repository root has **`len = 23`** and a 22-byte body, so a reader of `len`
bytes — class first — is the whole record. Read through the state schema
(`tools/mgo2_record_schema.py`):

| byte of the payload | what the schema says it is |
| --- | --- |
| 1 | the state index. `0x01`–`0x18` are the session's **24 slot states**, 164 fields each; the class byte is `key + 1`, so slot `0x01` is the host's own and `0x15` the key-`0x14` joiner of `game1` |
| 21 | the **presence map**, `ceil(164 / 8)` bytes, one bit a field |
| 1 | the byte of the one field whose bit is set |

`1 + 21 + 1 = 23`, exactly, in every copy. The map of every copy is zero but for
one bit. **Which bit** the code answers itself: `0x27E8B0` walks the map with

```
0x27E964: srawi  r9, r31, 3      ; byte index = field index / 8
0x27E968: clrlwi r11, r31, 0x1d  ; bit       = field index & 7
0x27E984: lbz    r0, 8(r9)       ; the map byte
0x27E988: sraw   r0, r0, r11     ; shift right by the bit
0x27E98C: clrlwi r0, r0, 0x1f    ; and take bit 0
```

so **a byte's low bit is its first field** and the `0x40` every copy carries at body
offset 18 is `18 × 8 + 6 = 150`. Field 150 of the shared 164-field table is
`state+287`, **one byte**, and — measured over the table — the **only field of the
164 flagged `0x0040`**, the flag `0x27E578` publishes whether or not the value
changed (`udp-record-dispatch.md` §3.6). That is why the record repeats on the
host's tick instead of only on a change.

The two superseded readings are both arithmetic that this closes: the body is not
"eighteen zeros, `40 00`, a big-endian `u16`" (§28.1) — the last two bytes are the
map's final byte (zero) and the field's one byte, which coincide with a `u16` only
because the map's last byte is zero — and the value is not a sixteen-bit field.

### 28.7.2 The chain the record runs on the client

1. **the body dispatcher** — `0x27E8B0`, the module object's vtable `0x011A63C8`
   slot 0: read one byte (`0x26CB20`), scale it by four and index the 26-entry table
   `0x0187E990` (`rlwinm r0, r0, 2, 0x16, 0x1D`, so the code is written for 256
   entries and the module fills 26). The class byte is that index.
2. **the map** — `ceil(field count / 8)` bytes, read by the same cursor.
3. **the field mover** — one call per set bit to the descriptor's handler object
   `0x0122A5C8` (vtable `0x011F17F0`, slot 0 `0x27EC48`), which moves that field's
   bytes between the stream and the slot's **1248-byte state block**
   (`0x01876F88 + k × 0x4E0`). Field 150 is one byte at `+287` of it, so this record
   writes one byte of one slot's state and nothing else.

So the handler for the record is not a per-`vid` handler at all: it is the state
dispatcher, reached for every record whose payload opens on a state index, and the
`0x002` entry of §4.1's inbound registry only says the client accepts the stream.
What is still **[U]** is the **name** of field 150. Nothing reads it through the
accessor at the size the table gives it: of the 283 `bl 0x27E480` sites, the nearest
`li r4, 0x96` before each call is size `0x7c` (124) in all three that carry one, a
different descriptor's field 150; the only size-1 call with index `0x96` is a
*setter* at `0x009e94ac`, in another module. So field 150 is read as part of the slot
state block by code that owns that block, and
the block's fields are addressed by the table rather than by literals in code, which
is why no `stb …, 0x11f(…)` exists either. The reading of the value stands as §28.2
has it: a per-peer figure the host measures, 100 for its own slot, 50 before the
first measurement, 0 once the peer's records stop.

### 28.7.3 What the code writes now

`MatchStateBodiesUtils.StateBlobBody` builds the record as the schema describes it —
the bit for `StateBlobFieldIndex` in a `StateBlobMapLength`-byte map, then the byte
at `StateBlobValueOffset` — which is byte-for-byte what the earlier `18 zeros + 0x40
+ u16` builder wrote, since the field is the map's last byte plus one. The briefing's
tick (`BriefingRoomPublishService`) now writes **100 for the host's own slot and 50
for every peer**. The host's own slot is 100 in every capture that holds it, and 50
is the value a peer is named with before it has been measured: every peer of seven
of the ten captures starts there, and the join-time burst carries it. The three
exceptions name a peer with the measurement it already has — 100 for a peer whose
figure the host has settled, and 62, 0 and 89 in a join into a running game — which
is a measurement this host cannot make, so 50 is what a peer's slot takes.

## 29. Two more bodies are the same state transfer, and a run publishes its arrays free

§28.7 found that the `0x0002` blob is a one-field state transfer. Two of §27's open
items turn out to be the same shape, and one of them is a change rather than a
reading.

### 29.1 The run's arrays are published with every slot free

§27.5 measured it and left it open: both reference briefings' runs carry
`85 7e fe ff …` / `86 7e 7f 7f …` — the pair with **no slot marked** — and this
host's run carried `85 f3 fe 00 ff …` / `86 f3 7f 07 7f …`, its joining player's slot
taken. The marking arrives later in the reference, and not in the run: `mgo2-game1`'s
pair gains `(14, 0)` 27 s after its run, and `started-on-briefing` marks the slot with
the entry answered at t+93.127.

`MatchStartRunFramesUtils.BuildRun` now builds the pair with an empty slot list, so
every slot of both arrays is the vacant byte. Nothing else changes: the run's own
ordinals, the room's stage, the closes, the clock and the rules pair are untouched,
and the marking still happens where the reference puts it — `TeamArrayService`'s
answer to the peer's entry, which reads the room's slots and writes them marked.

### 29.2 The settings header is a state transfer too, and its "phase byte" is a map bit

`SettingsHeaderBodiesUtils` described its twenty-four bytes as a type, a slot, a
*phase* byte (`0x02` through a briefing) and a zero tail whose last byte moves. The
length is what corrects that: `1 + 1 + 21 + 1`, the same twenty-one-byte presence map
and single field bytes `0x0002` carries (§28.7), and the map's first byte `0x02` is
`1 << 1` — **field 1**, one byte at slot-state offset 1, whose value is the body's
last byte. So:

| family | map bit | field | value seen |
| --- | --- | --- | --- |
| `0b SS 02 00 … 00` | byte 0 = `0x02` | 1 (`state+1`, 1 byte) | `0x00`, `0x01`, and `0xe0` (three in `mgo2-game1` from t+449.043, three in `mgo2-joined-game-in-progress-last-round`) |
| `0b SS 00 … 01 00 02` | byte 19 = `0x01` | 152 (`state+289`, 1 byte) | `0x02` |

Both families are the same answer carrying a different field, and both are read the
same way as the blob — one byte of map, one field. **The value is still [U]**: it moves
per player and per copy, and the side it resembles does not agree slot by slot with
the team array in the same capture (`started-on-briefing`'s slot `05` reads `0x01` in
the header and `0` in the array). This host writes **field 1 at zero**, which is the
byte the earlier builder wrote, so no wire byte changes here — what changes is that
the read-out no longer calls the `0x02` a phase.

### 29.3 What is still different

Three items were listed here and two of them are closed; the section is kept as the
record of what reading one window got wrong.

1. **Cadence — closed.** §30.1 measures the tick over all ten captures and §30.6 the
   countdown beside it; this host now writes every briefing record on that one tick.
2. **The "join-free republication" — withdrawn.** The instants at 65.1 s and 86.1 s
   in `started-on-briefing` are ordinary ticks: §30.1's median gap covers them and
   nothing special happens there. There is no rule to find.
3. **Field 1's value — still [U]**, above.

## 30. The briefing tick measured over every reference capture

§27.5 read the pair's rhythm off one 24-second window and called it "9 instants 1.9 to
4.4 s apart"; that was a window, not the cadence. Counted over the ten reference
captures (the two `myserver*` ones excluded), the pair is written on a steady tick and
is the only record that tick carries.

### 30.1 The rhythm

Instants are deduplicated at 50 ms (a pass that writes the pair twice writes both
within a millisecond), and gaps of 30 s or more are dropped as a phase change.

| capture | pair instants | median gap | min | max | blob median |
| --- | ---: | ---: | ---: | ---: | ---: |
| `joined-when-red-team-then-briefing` | 205 | 2.23 | 0.06 | 4.87 | 9.72 |
| `started-on-briefing` | 78 | 2.30 | 1.56 | 4.53 | 9.00 |
| `started-team-selection-selected-blue` | 107 | 2.13 | 0.05 | 3.36 | 2.40 |
| `game1` | 182 | 2.38 | 0.10 | 5.12 | 5.77 |
| `game2` | 92 | 2.21 | 0.22 | 5.00 | 9.69 |
| `started-briefing-then-solo-game` | 55 | 2.25 | 0.15 | 2.58 | 6.00 |
| `joined-game-in-progress-last-round` | 536 | 0.07 | 0.05 | 4.91 | 5.99 |
| `started-on-solo-game` | 506 | 0.07 | 0.05 | 5.00 | 2.61 |
| `game-failed-memoryviolation` | 59 | 0.47 | 0.05 | 4.96 | 10.17 |
| `game3` | 0 | — | — | — | 6.02 |

Two rhythms, and which one a capture shows is its phase: **~2.3 s** where the room is
briefing and **~0.07 s** (a beat frame) where a round is playing. So `§27.5`'s "nine
instants" is withdrawn — it counted one window. Pooled over every reference capture
that has the record, the pair's 813 gaps median **2.336 s** and the seven-byte
countdown's 78 gaps **2.372 s**, so the briefing's tick is **2.35 s** and the blob's
four-tick interval is **9.4 s**, between the reference blobs' 9.0, 9.7 and 9.7 s. The
pair holds during a round because the beat writes it a frame at a time. §30.6 measures
the countdown against the pair, which is where the earlier per-capture medians stopped.

### 30.2 What a tick carries

Composition of each pair instant in the two briefing captures, as "records of this
shape at this instant / instants":

| record | `started-on-briefing` | `started-briefing-then-solo` |
| --- | --- | --- |
| the pair `0x0261` | **78/78** | **55/55** |
| `0xd001`, one byte | 37/78 | 30/55 |
| `0x0002` blob | 23/78 | 19/55 |
| the briefing's countdown `0x1254` len 7 / the clock `0x9254` len 3 | 28/78 | 42/55 |
| the arrays `0x1001` len 26 | 13/78 | 4/55 |
| the filler pairs `0x1250`/`0x125a` | 12/78 | 5/55 |
| the state block `0x1254` len 90, the headers len 20 and the closes | **3/78** | **2/55** |

So the state block, the settings headers and the close triple are *not* per-tick
records — they arrive with a join and a handful of times beside one — which is what
this host already writes, on the run. The pair is the one record a tick owes, and it
is the one this host writes every 2.35 s (§30.6).

### 30.3 `0xd001` through a briefing: what the reference writes, and what this host does

Every reference capture writes host-side `0xd001` records with a **one-byte** body,
2.4 to 4.8 s apart, through the whole briefing:

| capture | count | median gap | bytes |
| --- | ---: | ---: | --- |
| `joined-when-red-team` | 196 | 4.63 | `08` 48, `10` 45, `04` 38, `0c` 33, `00` 11, `14` 7 |
| `game1` | 176 | 4.75 | `04` 56, `05` 41, `02` 25, `03` 25 |
| `game2` | 86 | 3.02 | `07`, `05`, `0b`, `0d`, `0a`, `11` … |

The family is vid `0x001`, the identifier `udp-record-dispatch.md` §4.1 calls the
*player entry, keep-alive class*, and its stream bits `0x0D` are one of the ten live
registry entries. §30.5 follows that handler to the byte's destination.

**The record is not missing here — the first reading of it was.** Counted on this
project's own two captures, which the earlier sections of this file had excluded, the
host writes `0xd001` through a briefing too:

| capture | records | in the briefing window | gap there | bytes |
| --- | ---: | ---: | --- | --- |
| `myserver24` | 64 | 3 of 21 datagrams, t+46.2 to t+59.3 | ~5.0 s | `24`, `04`, `02`, `03` |
| `myserver27` | 41 | 20 of 194 datagrams, t+46.0 on | 5.0 to 5.4 s | `24`, `02`, `03` |

The bodies are the sparse phase's pinned capture values
(`SparsePhaseFramesUtils`, `MatchRecordBodiesUtils.EarlyControl`), which the beat goes
on writing after the phase, and the cadence is the beat's insert rather than the
briefing's tick. Two differences remain against the table above: the gap is ~5.0 s
where the reference medians 3.0 to 4.8 s, and the bytes are four pinned values where
the reference's vary per copy. Both are the *value* question of §30.5, which the
binary answers only as far as the destination.

### 30.4 The settings header's field 1, re-checked

§29.2 left field 1's value open, with the side as a candidate. Re-checked against the
team arrays in all ten captures: at the **nearest** array in time the slot reads the
vacant byte (the arrays a briefing writes are the vacant ones, §29.1), and at the
capture's **last** array the side disagrees with the header for the slots that switched
— `started-on-briefing`'s slot `05` reads `0x01` in the header and `0` in the array. So
the side is not the field, `0x00` stays what this host writes, and the value remains
**[U]**.

### 30.5 Where `0xd001`'s byte lands, read out of `MGO2.ELF`

§30.3 stopped at "a slot key with a flag in its high bits". The binary settles the
destination and the corpus rules that reading out.

**The chain, instruction by instruction.** Stream key `0x0D`'s handler is `0x26DB78`:

```
0x26DBA4: bl   0x26CC88        ; read 1 byte from the record into r1+0x70
0x26DBAC: cmpwi cr7, r3, 0     ; bytes read
0x26DBB0: li   r3, 0           ; state index 0
0x26DBB4: ble  cr7, 0x26DBEC   ; nothing read -> return
0x26DBB8: bl   0x27E2B0        ; 0x27E2B0(0) = table[0] + 8, state 0x00's BLOCK pointer
0x26DBC8: li   r4, 0x63        ; byte offset 99
0x26DBCC: li   r5, 1           ; one byte
0x26DBD0: bl   0x27E578        ; write it
```

and `0x27E578` is `(block pointer, byte offset, size, value)` — it finds the descriptor
whose `+0x08` is that block (`0x27E5D4`), then the field whose `[offset, offset+len×mult)`
holds the offset (`0x27E628`–`0x27E664`), copies at `block + offset`, and applies the
flags. So the record writes **state `0x00`, byte `99`**, which is **field 24** of that
state's 55: one byte, flags `0x0000`, so nothing is queued for publication — the client
stores it and says nothing back. `udp-record-dispatch.md` §3.6 carries the signature,
which this corrects there and here: the `0x63` in the earlier reading was taken for a
field index and is an offset.

**What the value is not.** Measured over every capture in the repository, host-side,
body length 1:

| capture | records | value range | high bits seen (count) |
| --- | ---: | --- | --- |
| `joined-final-round-solo-then-final-round` | 196 | `02`–`08` | `0x20` (2) |
| `joined-when-red-team-then-briefing` | 196 | `00`–`1c` | `0x20` (3), `0x40` (2), `0x60` (2), `0x80` (2), `0xa0` (3), `0xc0` (1) |
| `game1` | 176 | `02`–`09`, `10` | `0x20` (17), `0x40` (2), `0x60` (1) |
| `game2` | 86 | `01`–`11`, `13` | none |
| `game3` | 57 | `03`, `04`, `07` | `0x20` (3) |
| `started-on-briefing` | 71 | `01`–`10`, `0e`, `0f`, `12`, `1a` | `0x80` (1) |
| `started-on-solo-game` | 43 | `01`–`05`, `12`–`14` | `0x20` (4) |
| `started-team-selection-selected-blue` | 68 | `02`–`05`, `12`–`16` | `0x20` (1) |
| `joined-game-in-progress-last-round` | 86 | `03`–`0a` | `0x20` (10), `0x40` (2) |
| `started-briefing-then-solo-game` | 81 | `02`–`06` | `0x40` (1) |
| `game-failed-memoryviolation` | 20 | `00`, `04`, `08`, `0c`, `10` | none |

So the value is a **five-bit code, `0`–`31`**, and the high bits are a minority variant
(`0x20`, `0x40`, `0x80`, and combinations) rather than part of every record. Three
readings are ruled out by that table:

* **a slot key** — `started-on-briefing` reaches `0x12` and `0x1a` in a room whose six
  players hold keys `1`–`6` (classes `02`–`07`), and `game2` reaches `0x13` with two
  players;
* **a clock or a sequence** — `started-on-briefing` steps `0c 0a 03 0a 10 0b 10 02 08 …`
  with no order at all, and `game3` holds `03` for a hundred seconds before stepping to
  `04`;
* **a rotation over the room's players** — that is exactly one value per player, and
  `started-on-briefing` writes eighteen distinct values with six players.

What is left is a per-copy code of the session's own — a state index or an event code —
and **that stays [U]**, as does the meaning of the high bits. The gain is that its
*destination* is now exact: field 24 of the session state, one byte, no reply owed, so a
server can write one into the place the client reads without re-deriving anything here.
What this host writes today is the four pinned capture values of §30.3, on the beat's
insert, and that is the difference left in this record rather than its absence.

### 30.6 The countdown and the pair are one tick, and this host had them on two

§30.1 and §30.5 leave the briefing's records counted but not their *phase*. Comparing
the two streams instant by instant answers the remaining cadence question, and it is
the one diff left in it.

**The reference writes them in the same pass.** For every reference capture, each
seven-byte countdown instant (`0b <seconds> 00 <ready set>`, on `0x1254`/`0x1a54`) was
measured against the nearest rules-pair instant:

| capture | countdown instants | inside the pair's span | within 50 ms of a pair | median distance |
| --- | ---: | ---: | ---: | ---: |
| `started-on-briefing` | 31 | 31 | **28** | 0.000 |
| `joined-when-red-team` | 39 | 39 | **21** | 0.000 |
| `game2` | 3 | 3 | **3** | 0.000 |
| `started-briefing-then-solo` | 2 | 2 | **2** | 0.001 |
| `game-failed-memoryviolation` | 3 | 1 | 1 | 0.000 |
| `game1` | 16 | 16 | 0 | 28.1 |
| `joined-game-in-progress` | 6 | 6 | 0 | 7.9 |

Where both records are on, the countdown lands on the pair's instant to the
millisecond — the two are separate datagrams in one pass, not one frame (a frame
carrying both was seen once in the whole corpus). The captures that show no coincidence
are the ones where the countdown is written while the pair is absent from that stretch,
which is a counting-room's second half rather than a second tick.

**This host ran three periods.** The pair on 2.25 s, the countdown on 2.5 s and the
ready set on 2.5 s, each a constant of its own service, and the tick wait polled every
250 ms — so a period that was not a multiple of a quarter second was rounded up to one
anyway. A pair at 2.25 s and a countdown at 2.5 s drift 0.25 s apart per tick: they
meet on one tick in nine and never again, where the reference has them together at every
countdown instant it writes.

**What changed.** `MatchRoundStateService.BriefingTickMilliseconds` is now the one
period — 2.35 s, the pooled median of the pair (2.336 s over 813 gaps) and of the
countdown (2.372 s over 78) — and the pair, the countdown and the ready set all read it.
`WaitBriefingTickAsync` no longer takes a period, so no writer can choose its own again,
and its poll is 50 ms so the tick lands on the number instead of on the quarter second
above it. Nothing else on the wire moves: the four-tick blob interval becomes 9.4 s and
the state blob, headers and closes stay where §30.2 puts them, on the run.

### 30.7 What the client does with `0xd001`'s byte: field 24, two readers, one event

§30.5 followed the record into `MGO2.ELF` and stopped at its destination. Continuing
from there names what the byte *is* to the client, and rules out the readings the
corpus alone left open.

**It is stored and never read back out.** `0x27E578` walks the state's field table and,
having copied the value, tests the field's flags (`0x27E6A4`–`0x27E6C4`): `clrlwi r0, r9,
0x1f` for bit `0x0001` and `rlwinm r0, r9, 0, 0x19, 0x19` for bit `0x0040`. Field 24's
flags are `0x0000`, so neither branch is taken and nothing is marked in the state's map —
the byte is a leaf the session keeps, not a field the client republishes.

**Two readers, both on the same pattern.** Every `get(offset, size, dest)` call to
`0x27E480` that names offset `0x63` and size `1` — there are exactly two in the image —
copies the field into their own object and then does the same five things:

| site | what it does with it |
| --- | --- |
| `0x77E668` (read at `0x77E6A0`) | `lbz r5, 0xd0(obj)`; **`cmpwi r5, 0xff` → return**; if the screen step (`0xb8`) is `12` → return; else step = `12`, flag `0xc4` = 1, `emit(0x16002C, byte)` |
| `0x77F7E0` (read at `0x77F878`) | the same five, after testing field 161 of the same state with `0x27E198` |

`0x7770F0` is `emit(r4 = id, r5 = value)`: it takes the singleton from `0xB2F598` and
calls `0xC1208`, which walks a listener list at `+0x34` (entries of `{id, listener,
argument}`) and calls the listener whose id matches, passing the byte as the argument.
So the byte is a **one-shot argument to event `0x16002C`**, consumed by a screen state
machine that moves to step 12 and does not do it again until it has moved on.

**What that fixes about the reading.** Three things are now settled and one is not:

* **`0xFF` means "no value".** Both readers test for it and skip the event. The corpus sends it
  exactly once, in `game1`, and that copy is inert for the same reason; §58 counts the field
  whole, and the range over all twelve captures is `0x00`–`0x1c` in the code bits with the flag
  bits above them.
* **It is written in groups, and the bytes differ within one.** 68 instants in the
  corpus carry two different bytes — `game-failed-memoryviolation` t+17.5 has `04` and
  `0c`, `joined-when-red-team` t+46.4 `00` and `04`, `started-on-briefing` t+107.5 `0a`
  and `0f` — beside instants that repeat one byte (`joined-when-red-team` t+18.8, `04`
  twice). So a pass writes one record per something that varies within the pass, and a
  group is not a duplicate; that rules out the "one scalar per tick" reading the
  single example first suggested.
* **The host authors it** — the record's handler is the only writer of field 24 in the
  image, and a client record carrying the same code within three seconds of each
  `0xd001` is a chance match, not an echo (`whole` 1–9 of 20–196 in every capture).
* **Its meaning is still [U]**, but narrower than before: a value with `0xFF` as the
  empty case, one per tick, that a screen consumes once. A five-bit code with a
  sentinel is the shape of an **index into a table of at most 31 entries** — and the
  session's player table is 24 — but `started-on-briefing` sends `0x12` and `0x1a` in a
  six-player room and `game1` sends `0x24`/`0x25`, above both 24 and any class id, so a
  bare index does not fit either unless `0x20`, `0x40` and `0x80` are flags, which the
  corpus cannot confirm.

This host writes four pinned bytes from the sparse phase (`24`, `04`, `02`, `03`) and
never `0xFF`, which is the honest shape of the gap: the destination, the reader, the
event and the empty value are known; the code itself is not.

### 30.8 What is still different, counted the same way on both sides

With the tick settled (§30.6), the rest of the diff is in *what* a briefing carries, not
when. The comparison below is the first 60 s of each capture, host side, with the
sibling bit (`0x800`) and the class bit (`0x8000`) masked off so a generation form is not
counted as a missing record, and with `myserver24`/`myserver27` summed as "ours":

| shape | reference | ours | reading |
| --- | ---: | ---: | --- |
| `0x0261 len 1` rules pair | 1253 | 90 | same record, ours denser per tick but the capture is shorter |
| `0x0002 len 22` state blob | 195 | 16 | ours every fourth tick (§30.6); the reference's count is over a longer window |
| `0x5001 len 1` | 152 | 48 | the same family |
| **`0x125a len 6`** | 144 | **0** | the item-box pair, absent outside a round here |
| **`0x1250 len 6`** | 130 | **0** | the same, the other half of the pair |
| `0x1001 len 54` loadout | 55 | 2 | present in both |
| `0x1254 len 3` clock | 47 | 1 | ours writes the sibling form and rarely |
| **`0x1001 len 63`** | 41 | **0** | a profile body the join answer does not carry (33 of the 41 in `game-failed-memoryviolation`) |
| **`0x1001 len 25`** | 25 | **0** | 13 of them in `joined-game-in-progress` |
| **`0x1001 len 79 / 52 / 83 / 51 / 57 / 67`** | 18 / 14 / 12 / 10 / 10 / 10 | **0** | further profile and entry bodies, one or two per capture |
| **`0x0261 len 4 / 5`** | 19 / 15 | **0** | rules-family bodies; this host writes the pair as `len 1` only |
| **`0x1250 len 9 / 10`** | 17 / 17 | **0** | the item-box group's other two bodies |

Nothing this host writes is absent from the reference in that window — the extra shapes
in its own captures (`0x5ae2`–`0x5ae9`, `0xdae9`) are the same item-box group under the
generation this host plays, so they are the mirror of the two rows above.

So the remaining differences, in order of how much wire they are: **the item-box group
is not written through a briefing** (only with a round), **the join answer carries fewer
`0x1001` profile bodies** than the reference's (lengths 25 to 83), **the rules family
sends no `len 4`/`len 5` record**, and **`0xd001`'s cadence and byte set** are the
pinned ones of §30.3/§30.7. Each is a record body and a cadence to be read out of the
captures in turn, not a protocol question.

### 30.9 Two candidates checked and dropped, so the list does not grow back

Before treating §30.8's rows as things to write, two of them looked like a single
systematic cause and are not.

**The generation is not inverted.** Reading §30.8's identifiers raw suggests this host
briefs on the set the reference rounds on (`0x0a61`/`0x1a54`/`0x1a50`) while the
reference briefs on the other (`0x0261`/`0x1254`/`0x1250`). It follows the *client's
report*, as `MatchRecordGenerationUtils.OfJoinReport` already has it: every capture whose
client reported on `0x12de` briefs on `0x0261` (`started-on-briefing` 382 against 130,
`started-briefing-then-solo` 6644 against 5746 at its open) and `mgo2-game3`, whose
client reported on `0x1ade`, briefs on `0x0a61` alone (8852 to none). This host's own two
captures report on `0x1ade` and get `0x0a61`, which is the same rule. Nothing to change.

**The records this host sends and the reference does not are answers to records only this
project's test client sends.** `0x5ae2`–`0x5ae9` are `PeerAcknowledgementUtils`' answers
(`| 0x4000`, empty body) to the client's `0x1ae2`–`0x1ae9` group — eight two-byte
settings records written at `myserver27` t+54.70. No reference capture carries that group
in either direction, so the host has nothing else to answer there, and the answers are
faithful to what it was sent. The briefing writes nothing of its own that the reference
does not.

What §30.8 lists is therefore all in the other direction — records the reference writes
and this host does not — and each needs its bodies read out of the captures: the
item-box filler pair `00 00 00 00 00 00` / `01 00 00 00 00 00` under the generation's own
two identifiers, in the run's burst rather than on the tick (`started-on-briefing`
t+58.255 and t+60.383, twelve groups each, sequence-numbered per identifier); the
profile bodies `0x1001` at lengths 25 to 83 in the join's answer; the rules family's
`len 4`/`len 5` bodies; and `0xd001`'s code with its `~4.6 s` insert.

### 30.10 The two record groups this host does not write, read out of the bodies

§30.8's remaining rows are two families, and both are now identified by their bodies
rather than by their lengths.

**The long `0x1001` records are the player-entry structs of the `07` opcode, and they are
now decoded field by field.** Every one of them opens `07 <size>`, where `<size>` is the
body length minus seven, and then a zero and a **kind** byte: `0x00` is the roster entry
— `07 48` for a seventy-nine-byte one, `07 3c` for a sixty-seven-byte short one — while
`07 12`/`07 2d` are kind `0x04` and `0x02`, `07 0f`/`07 16` kind `0x03`, and `07 2f` kind
`0x01`. `UDP_COMMANDS.md` has the offset table for the entry in both layouts, together
with what each field was measured against.

The reading here that supersedes the earlier one: the `07 48` shape is **not** a repeated
`(key, key, flag, value)` run. `e2 14 00 14 2f 23 01 00` is the roster index at `0x04`,
the player's 24-table slot at `0x05` and again at `0x07`, and the character id as a u32 at
`0x08` — one of each, which is what `PlayerProfileRecordUtility` already writes. What is
still missing from this host's answer is not the entry's fields but the four kinds beside
it: 39 records of kind `0x01`, 166 of `0x02`, 43 of `0x03` and 155 of `0x04` in the
captures, none of them an entry.

Those four are decoded now, as far as the wire goes, in `UDP_COMMANDS.md`: they are
host-to-joiner records that ride the same datagram as the team and occupancy arrays, the
seven-byte header above with the slot at `0x05`, and **kind `0x04` is one spawn point per
slot** — slot, its place in the group, a sixteen-bit heading and three signed coordinates,
written one per player and every two seconds or so. Kinds `0x02` and `0x03` carry geometry
that reads as a box's four corners and a mirrored pair of points; kind `0x01` is a quad of
four signed sixteens and then four items of the same, which is kind `0x02`'s tail moved two
bytes down, with what the items state still **[U]**. Which of them this host has to write
for a join to proceed is not settled by the bodies — nothing in the captures shows a client
refusing an answer for the want of one — so none of the four is written here yet.

**The rules family's `len 4`/`len 5` bodies are the roster list.** They read
`ff <key> [<key>] fe fe`: `mgo2-game1` writes `ff 14 fe fe` (4532 of them), `ff 06 fe fe`
(534), `ff 0f fe fe` (272), `ff 0b fe fe` (56) and the two-entry `ff 04 14 fe fe` (429),
`ff 06 14 fe fe` (84) — and the keys are the room's own: that capture's five joiners hold
`04`, `06`, `0b`, `0f` and `14`. `mgo2-joined-game-in-progress` the same way, `ff 07 fe
fe`, `ff 0b fe fe`, `ff 13 fe fe` and `ff 0b 13 fe fe`. So the pair is not always the
empty `fe` this host writes twice: the second record is a list of the room's slot keys,
terminated by `fe fe`, and it grows as the room does.

Both are implementations of their own and neither was written in this pass: the first
needs a struct decode, and the second needs the list's rule (which keys one record
carries, and when a record replaces the previous one) read out of the same captures.

### 30.11 `myserver28`: the briefing's records are all on the wire, and the screen still does not come

The live capture of 2026-10-21 (`myserver28.pcapng`, `192.168.1.20:5730` joiner ↔
`89.129.16.203:5731` host, key `0xc22ed7fd`) is another where the briefing's own records
are present and the client does not reach its screen. Read with `tools/udp_frame.py` and
`tools/udp_briefing_flow.py`:

| t+ | what |
| --- | --- |
| 4.9 | the join: roster answer, run, and the sparse phase's `0xd001`/`0x0a61` records |
| 54.88 | the client's report: `1ade e1 01`, `1af3 00 01 ac aa`, `1afe/fd/fc 00 01`, `0a5c 01`, and its `82 f3`/`84 f3` echoes in the same datagram |
| 54.93 | the host's answer: the four acknowledgements (`da f3`, `5a fe/fd/fc`), `0a5c 02 00 00 00`, the publication `1ade e0 01 00 …` and the `5ade` marker |
| 56.95 | the run: state block, the two arrays, the close pair, the roster close, and the item-box pair — **and the first countdown**, `0b 3c 00 00 00 00 00` |
| 56.95–76.2 | the countdown repeats every 2.4 s with the ready set at `00 00`; the client sends no ready, only its `0x9001 … 00` keep-alives |
| 76.17 | the client asks for entry (`83 f3 e0 7f`); the host answers with the arrays, the countdown and the **24-byte** settings header |

So the answer to the report, the run, the close triple, the countdown and the entry answer
are all written, and the client still never ready — it never left whatever screen it is on.

**The row that looked like an absence is a loadout, not a settings header, and the earlier
reading of it was wrong.** The fifty-four-byte body
opens `0b 02 02` — the settings payload type, slot `0x02`, side `0x02` — carries the skill
region at `0x16` (`02 ff 01 03 02 01 ff`) and the appearance block at `0x1f`
(`1e 45 2f 57 39 67 68`), which is `LoadoutFramesUtils`' layout and the shape §30.8 already
counts as its "`0x1001 len 54` loadout" row. The settings header is the other family — 24
bytes, a twenty-one-byte presence map and one field (`0b SS 02 00 …`, §29.2) — and the
reference writes it at t+8.125 (a 25-byte copy) and t+93.127 (24 bytes), neither of them in
this window. So both hosts write both families; what differs here is when.

Host-side records in the 25 s after each client's `e1` report, with the class and
generation bits masked, reference (`mgo2-game-started-on-briefing` t+55.8) and this capture
(t+54.9):

| shape | reference | `myserver28` |
| --- | ---: | ---: |
| `0x1001` 50–62 B opening `0b` (a loadout) | **3** (t+56.123, 58.255, 60.383) | **0** — its two are in the join burst at t+4.953 |
| `0x1001` 24–25 B opening `0b` (the settings header) | 0 | **1** (t+76.220, on the `83` request) |
| `0x9001`/`0x1001 len 26` (the arrays) | 8 | 3 |
| `0x1001 len 20` (the close pair) | 8 | 4 |
| `0x1001 len 7` (the close) | 4 | 2 |
| `0x1250`/`0x125a len 6` (the item-box pair, under either generation) | 45 each | 6 each |
| `0x1254 len 7` (the countdown) | 0 | 11 |
| `0xd001 len 1` | 5 | 6 |

The three loadouts are a repeat of the peer's own record and not an answer to the report:
of the fourteen captures that decode, only this one writes a loadout within six seconds of a
report, and its own second report (t+196.828) is answered with none. So the row is a phase
difference, as are the item-box pair and the countdown, and the window holds **no record
this host never writes at all** — the correction withdraws the "test the 54-byte header
first" that stood here. The shapes this host does not write are the ones §30.8 already
lists, and one of them is now measurable as a rule rather than a count:

### 30.12 The loadout is rewritten when the room's membership changes, and this host writes it only at the join

**The reference writes a player's loadout again at each join.** Host-side `0x1001` bodies
opening `0b` at a length of 50 to 62 bytes, with the slot byte at `+1`, are 142 across the
eleven reference captures and 2 in each of this project's three. Their instants:

| capture | loadout instants (slot) |
| --- | --- |
| `started-on-briefing` | 6.0 ×2 (2), 8.1, 10.4, 34.4 (2), 56.1, 58.3, 60.4 (2), 102.8 (3), 138.4 (4), 149.9 + 152.0 (5), 184.5 + 186.9 (6), 213.1 + 215.5 (7) |
| `game1` | 6.7 ×3 (1, 21, 21), 156.0 + 158.2 + 160.6 + 162.8 (16), 208.2 + 210.7 ×2 (5), 386.7 + 391.2 (12), 494.2 + 496.7 ×2 (7) |
| `game2` | 6.1 ×3 (2), 10.2 ×3, 12.4, 36.0, 203.5 ×2 (3) |

In `started-on-briefing`, each slot that is not the joining peer's own first appears a few
seconds **before** that player's ready state is first published — slot `3` at t+102.758
against its `e0 02` at t+112.619, slot `4` at 138.369 against `e0 03` at 144.750, slot `5`
at 149.860 against `e0 04` at 154.370 — and the copies of one slot sit 2.1 to 2.5 s apart,
which is the briefing's tick (§30.1), one to four of them. The record is therefore written
when a player joins the room, and not on a clock of its own.

**And the join reaches the peers already in the room as four records.** In the same
capture, at t+102.758, to the peer that joined at t+6.0: `0x1001 len 65` (the
newcomer's roster entry, `07 3a 00 00 02 02 …`), `0x1001 len 7` (the run's close
`07 00 00 00 00 00 03`), `0x1001 len 10` (`0b 00 00 04 00 00 00 00 00 03`, its slot at
`+9`) and `0x1001 len 56` (that player's loadout, opening `0b 03`). The quartet is the same
at each of the room's next three joins, entry length first and loadout last: `66 / 7 /
10 / 57` at t+138.369 (slot `4`), `62 / 7 / 10 / 56` at t+149.860 (slot `5`) and `65 / 7 /
10 / 55` at t+184.538 (slot `6`).

**This host announces the entry and the close, and neither of the other two.**
`PlayerProfileHandler`'s answer ends by broadcasting every record of the newcomer's run to
the other peers — the entry, the close and the host's own entry — and the loadout is not in
that run: it is built by `PlayerSettingsBurstService` into the burst one peer receives,
once, 2.52 s after that peer's own roster answer. So a peer that joined earlier is never
told what a later player looks like, and a briefing that draws the room from these records
has nothing to draw for the players who arrived after it.

**Not verified live.** What would confirm it is a three-client join: each peer already in
the room should receive the newcomer's entry, close, ten-byte control and loadout, and this
host today writes the first two. Nothing in `myserver28` tests it — that capture holds one
client — and nothing here says the missing loadout is what holds the screen.

**The countdown row is a phase too.** Both hosts write the same seven-byte body,
`0b <seconds> 00 <ready set> 00 00 00`. This host writes 56 copies from t+57.092 to
189.016, all of them `0b 3c 00 00 00 00 00`; the reference's first is at t+93.127, 37 s
after its report and 35 s after its run, and it writes `0x3c` with a zero set for 26.7 s
before the set reads `0x02` (t+119.874) and the count moves (`0x3b` by 132.249, `0x00` at
193.621). The set's first non-zero bit is the second player ready. This host's room has one
ready player — the client's `e1 01` load report is not a ready record, and
`BriefingStateService.ReadyMask` counts only the ready ones — so its count is held by the
one-player rule and its set stays empty, which is exactly the reference's own state for
those 26.7 s. Nothing there is a defect; it is the same rule, caught at a different point
in the briefing.

### 30.13 The client retries four vids no reference client sends, and this host can only acknowledge them

The one thing in `myserver28` that no reference capture holds at all is a client-side
retry group. Counted over both directions of all fourteen captures with
`tools/udp_frame.py`'s reader, **no record of vid `0xAF3`, `0xAFE`, `0xAFD` or `0xAFC`
exists outside `myserver28`**: the eleven reference captures hold none, and neither do
`myserver24` and `myserver27`, whose clients are stuck in the same place. The sixty
copies here are all inbound, in two class forms:

| wire id | body | copies | this host answers with |
| --- | --- | ---: | --- |
| `0x1AF3` | `00 01 ac aa` | 5 | `0x5AF3`, empty |
| `0x9AF3` | `00 01 ac aa` | 10 | `0xDAF3`, body `24` |
| `0x1AFE` / `0x1AFD` / `0x1AFC` | `00 01` | 15 each | `0x5AFE` / `0x5AFD` / `0x5AFC`, empty |

They are written as one group, at t+54.9 and then at 5.1 to 8.2 s intervals (12.3 s to the
second): 54.9, 67.2, 75.4, 82.1, 87.6, 93.3, 99.1, 105.7, 111.3, 116.4, 122.1, 127.7,
133.3, 139.4, 145.3 — fifteen instants, the same four records each time, byte for byte.
The first copy rides the join report's own datagram (`9af3`, and `1ade e1 01` behind it),
and the rest ride the keep-alive datagrams the client writes between its heartbeats.

**Every one of the sixty is answered.** The host's `PeerAcknowledgementUtils` answers each
numbered record with the same identifier and the ack bit, and the client sends the group
again at the next interval regardless. So the ack is not what the client is waiting for:
either it wants a record of its own on one of those vids, which nothing on the reference
wire suggests because no reference host ever writes one, or it is a retry timer that a
later answer is supposed to clear.

**What it does and does not change.** It does not name the stall, because `myserver24` and
`myserver27` sit on the same screen without ever sending one — the group is the *marker* of
a client state, not its cause. What it does name is where the state is entered: the first
copy is already in the report's datagram, so it was set inside the join, before the report
at t+54.9 answered it — which is the window §30.8 lists the differences in
(the item-box groups, the `07` entry kinds, the rules family's `len 4`/`len 5` bodies) and
not the report's answer, whose four records match the reference's byte for byte except the
sequence byte the pair carries, which is the request's own. A capture that does not carry
this group is the one that will show which of those records cleared it.

## 31. The retry group is operations-class, and the fade's step is what the binary says to read next

§30.13 named the sixty retries by their wire ids and could go no further with them. This
section re-reads the same session with the identifier split the way the client's own
registry splits it, which turns the four ids into four records of one class; then it
checks what a healthy host writes in that class, what the fade window carries in each
direction, and what `MGO2.ELF` says about the screen a peer is on when it never leaves
the fade. Nothing in the server was changed: every claim below is a measurement or an
instruction, and the records §30.8 still lists are still unwritten.

### 31.1 The identifier is a class byte and a sub byte, and that is what the group is

Every session record's identifier is `class << 8 | sub`, and the client's inbound
registry — eighteen entries, `udp-record-dispatch.md` §4.1 — is keyed by the **sub**:
`0xA54` is the operations stream's sub `0x54`, and the wire writes it `0x1254` / `0x1A54`,
which are class `0x12` and class `0x1A` on that same sub. Read that way the §30.13 table
is one class and four subs:

| wire id | class | sub | the sub's other forms in the corpus |
| --- | --- | --- | --- |
| `0x1af3` | `0x1a` | `0xf3` | `0x82f3` / `0x84f3` — **the lobby-slot pair** |
| `0x1afc` | `0x1a` | `0xfc` | none |
| `0x1afd` | `0x1a` | `0xfd` | none |
| `0x1afe` | `0x1a` | `0xfe` | none |

The class is the operations family's second generation — the one `0x1a54` and `0x1ade`
carry — so the group is not four vids at all, it is four operations-class records on
four subs, one of which is the sub the slot pair is written on. Its bodies (`00 01`,
and `00 01 ac aa` on the pair's) are the same statement on each.

**No reference host writes those four subs, and no healthy client does either.** Counted
over both directions of the eleven reference captures: zero records on subs `f3`, `fc`,
`fd` or `fe` in class `0x12` or `0x1a` — the pair's sub `f3` is written only as
`0x82f3`/`0x84f3`, the class-`0x82`/`0x84` slot pair.

**The body `00 01` is a real record, but in the other direction and on another sub.**
Three reference records carry it, all host-side and all on sub `0x58`: `0x1258` in
`mgo2-game-started-on-briefing` at t+8.125 and t+10.379 (with the settings burst, 2.25 s
apart) and in `mgo2-game2` at t+6.112. Each is answered by the client 132 ms later with
an **empty** `0x5258`, so the shape is a numbered record that draws an empty
acknowledgement — the same shape the retry group draws from this host. So the group is
the client sending request records of a kind the reference host also sends, on four subs
no host ever answers with data, and the reference client never sends one at all.

**What the reference host does write in that class is a *different* set of subs.**
`mgo2-game-joined-when-red-team-then-briefing` writes `0x12e2` … `0x12e7` — six
operations-class records on subs `e2` to `e7`, three bytes each, `ff <value> 18` —
in one pass, then re-sent as `ff 01 18`, `ff 0a 18` and `ff 09 18` under ordinals `00`,
`01` and `02` at t+799.165, 800.807 and 805.291;
`mgo2-game-started-team-selection-selected-blue-then-game`
writes `0x12e1` (7 bytes, twice) beside `0x12e2` and `0x12e3`. This host writes none of
them. They are round-side records — every capture that holds them holds them beside tick
traffic — so they are not what a client in a fade is waiting for, and they were not
written.

### 31.2 The early rules pair is not the cause, and the ordinal stream is inconsistent

Two candidates were checked before treating the composition of the fade window itself
as the remaining difference.

**The pair written before a report is not it.** This host writes the rules pair from
t+2.4, before the peer has reported; `mgo2-started-briefing-then-solo-game` writes its
first pair only at t+57.6, after its report, which made the early pair look like the
cause. It is not: `mgo2-game-started-on-briefing` writes its first pair at t+6.0 and
`mgo2-game2` at t+6.1, both before their reports (t+55.8 and t+56.7). Two references
open the fade with the pair and two do not, so the pair's start is a phase this host
may choose and nothing in the corpus says the client acts on it.

**The control record's ordinal is written twice for two different bodies.** `0xd001` is
one byte in a class of its own (`0xd0` = `0x10 | 0x40 | 0x80`) and the one record §30.5
follows into the client, where it is a one-shot argument to event `0x16002C`. The
reference host never lets it go backwards — `01` at t+6.0 through `0b` at t+55.2 and on to
`13` in `started-briefing-then-solo` — and repeats an ordinal only for an identical body
(t+32.644 and t+32.645, `02` twice; f=12 twice at t+88.117 and 88.118, `02` both times).
This host's
ordinals run `01, 00, 00, 02, 02, 02, 03, 03, 03 …` (t+2.5 to 14.5) and carry **two
different bodies**: ordinal `00` is `24` and then `02` at the same instant (t+4.953),
ordinal `02` is `24` at t+5.4 and `02` twice at t+7.3, and ordinal `03` is `24`, `03`,
`02` at t+10.7, 12.1 and 14.5 — the pinned continuation of `SparsePhaseFramesUtils` and
the answers of `PeerAcknowledgementUtils` counting separately, where the reference numbers
every copy of the record on one counter and only repeats an ordinal for the same byte.
**[I]** a receiver that drops a repeated ordinal drops one of the two bodies; the corpus
cannot say that it does, and the first copy in every session is free of the collision, so
this is recorded rather than changed.

### 31.3 The screen the fade belongs to, and the steps it can and cannot leave

The binary was read for the screen state machine the `0xd001` byte moves (§30.7). The
object carries a 32-bit **step** at `+0xb8`, a 32-bit companion at `+0xbc` and a one-byte
flag at `+0xc4`; every transition writes all three and most emit an event through
`0x7770F0(event, argument)`. The transitions the image holds are:

| step written | site | condition |
| ---: | --- | --- |
| `9` | `0x77EB98` | step != 9; `0xbc = 0`, `0xc4 = 1`, `emit(0x16001B, 0)` |
| `9` | `0x77EC24` | bit `0x04` of `+0xc0` set; `0xbc = 0` |
| `10` | `0x77F438` | step != 10; `0xbc = 0`, `0xc4 = 1` |
| `11` | `0x77AAE8`, `0x77ABE0` | step ≤ 6 — the test is `bgt` on 6, so `step == 6` as first recorded here is wrong (§32.3); `0xc4 = 1` |
| **`12`** | `0x77E668` | **step <= 9**, state `0x00` field 24 (`0x63`) != `0xff`; `0xbc = 0`, `0xc4 = 1`, `emit(0x16002C, byte)` |
| **`12`** | `0x77F7E0` | the same, **after** a test of state `0x00` field 161 (`0xA1`): a nonzero byte there skips the transition |

So the byte `0xd001` carries can only move a screen that is at step **nine or below**,
and `0x77E668` reads it from the session state (`0x27E480(block, 0x63, 1, obj+0xd0)`) every
time it runs. A screen at step `10` or `11` is one the byte cannot reach at all — which is
exactly what a fade with a missing next event looks like, and it is where the next reading
has to start: what enters `10` and `11`, and which step the join screen is on when the map
and the rule text have been drawn. Both readers are virtual — no `bl` in the image names
`0x77E668` or `0x77F7E0` — so the entry is the screen class's vtable, and the step-10 and
step-11 handlers are the functions to read next.

### 31.4 What is left, in the order the wire supports

1. **The step-10 and step-11 handlers**, and the vtable that reaches them (§31.3). This is
the only measured place where the client can sit and be unreachable by the records this
host writes.
2. **The operations-class records on subs `e1`–`e7`** (§31.1), unwritten here and
round-side in every capture that holds them.
3. **The `0xd001` ordinal** (§31.2): one counter for the continuation and the answers.
4. **§30.8's remaining rows**, unchanged: the join answer's `0x1001` bodies below the
reference's lengths, the rules family's `len 4`/`len 5` roster list, and the item-box
group through a briefing.

None of these is a gate the corpus names. What the corpus does say is that the state the
peer is in is entered **inside the join and before the report** (§30.13), that no
reference client has ever produced its marker, and that the record §30.7 followed — the one
this host writes with a pinned byte — is the only one in the join whose value the binary
turns into a screen transition.

## 32. The client's `1d` rows are its own stream, and the step setters are dispatched dynamically

§31.3 left two questions: what enters steps `10` and `11`, and which step the joining
screen holds. This section answers the second as far as the image allows, closes the
timing hypothesis §31.4 left open, and withdraws a claim the code has been leaning on.
Nothing in the server changed.

### 32.1 Sub `0x54` is one channel with a one-byte opcode, and each side writes its own

Every record on sub `0x54` — the identifier `0x1254`/`0x1A54` — begins with a **one-byte
opcode**, and the two sides write different opcodes on it:

| opcode | length | writer | the body behind it |
| --- | ---: | --- | --- |
| `00` | 90 | host | the room's state blob, the operations clock in its tail (`… ff <lo> <hi>`) |
| `01` | 3 | host | the operations clock, 125 ms units, little-endian |
| `02` | 4, 27 | host | a state write — offset, count, values |
| `0b` | 7 | host | the briefing countdown |
| `0f` | 2 | host | the short record the replay parser does not name |
| `1d` | 7, five of them | client | one row per slot `00`–`04` |
| `1f` | 3 | client | one player's readiness |
| `22` | 22 | client | the room's second table |

The five `1d` rows are one table: `1d <session> <slot> <four bytes>`, with the slots
`00`–`04` and the same session byte in every row (`03` in
`mgo2-started-briefing-then-solo-game`, `01` in `mgo2-game2`); slot `00` carries
`01 03 00 00`, slot `01` `02 01 00 00` and slots `02`–`04` zeros. They travel beside the
22-byte `22` record in one datagram, which is the six records
`docs/protocol/STATE_VECTOR.md` §1 counts as the client's operations-stream opening — its
first six ordinals, written by the client rather than the host.

**So the delay is the client's, and it is not two seconds.** Measured from the client's
lobby-slot echo (`82`/`84 NN`) to its first `1d` row, over every capture that writes one:

| capture | entry | first `1d` | delay |
| --- | ---: | ---: | ---: |
| `mgo2-game2` | t+57.355 | t+59.040 | **1.69 s** |
| `mgo2-game-failed-memoryviolation` | t+59.988 | t+61.694 | **1.71 s** |
| `mgo2-started-briefing-then-solo-game` | t+55.312 | t+57.304 | **1.99 s** |
| `mgo2-game-started-on-briefing` | t+56.360 | t+62.623 | **6.26 s** |
| `mgo2-game-joined-when-red-team-then-briefing` | t+57.983 | t+95.464 | 37.48 s |
| `mgo2-joined-game-in-progress-last-round` | t+56.730 | t+281.402 | 224.7 s |
| `mgo2-game1` | t+45.688 | t+470.168 | 424.5 s |
| `myserver24`, `myserver27`, `myserver28` | 56.0, 55.6, 54.9 | — | none |

Seven captures write the rows and no two at the same distance from the entry; the long
delays are second entries into a room whose round has already played. `1d` is therefore
the client's own stream opening and not a stage a host can wait a fixed two seconds for,
which is what the remark on `MatchStartRunService.ConfirmEntryAsync` in
`MatchStartRunService.Entries.cs` says of `myserver20` — *"never the `1d` stage records
every reference client writes two seconds after its entry"* — and the table above
withdraws it. What `myserver20` lacks is the client's own stream at all, and that is the
same fact this host's three later captures show, not a second one about a two-second
stage.

### 32.2 A run that precedes the rows does not stop the client

`mgo2-game-started-on-briefing` writes its briefing's run **1.90 s** after the entry
(t+58.255) and the client's five rows only at t+62.623 — **4.4 s behind the run** — and
that client briefs, readies at t+115.771 and plays. So a run before the client's own
stream opening is tolerated, and
`MatchStartRunService.EntryRunDwellMilliseconds` (2 000, against the 1.90 s and 2.31 s
the two briefings measure) is a difference this host has already closed: `myserver28`'s
run lands 2.07 s after the entry, inside the measured range, and that client still never
opens its stream.

**[V]** the timings; **[D]** the conclusion, which is that the dwell is necessary and not
sufficient — a run 164 ms behind the echo fails (`myserver20`) *and* a run 2.07 s behind it
fails (`myserver28`), while the reference's 1.90 s succeeds, so what is left in that run is
its **content** and not its instant.

### 32.3 The five step setters are one table, and nothing in the image names them

§31.3's five sites are methods of one object, laid out as a descriptor array of
`(code, TOC)` pairs at **`0x11FEB80`**. The read-only data holds a run of class records
at a `0x18`–`0x1C` stride, each carrying one vtable pointer: `0x123E2BC` holds
`0x11FEB80`, and its neighbours hold `0x11FFC00`, `0x11FFC70`, `0x11FFF40`, `0x1200048`,
`0x1200090` and `0x12000E8` — the **sibling screens'** vtables, which is where the screen
a joining client actually holds has to be read next.

A slot is an offset of eight bytes from the base, which gives the methods §31.3 lists:

| slot | offset | method | what it writes |
| ---: | ---: | --- | --- |
| 0 | `0x000` | `0x77C1C0` | — |
| 21 | `0x0A8` | `0x77E668` | step `12` from the `d001` byte, only while step ≤ 9 |
| 24 | `0x0C0` | `0x77EB98` | step `9` |
| 39 | `0x138` | `0x77F430` | step `10` |
| 44 | `0x160` | `0x77F7E0` | step `12` from the `d001` byte, gated on state `0xA1` |

**No instruction in the image reaches any of them by name.** No `bl` in any segment
targets `0x77E668`, `0x77EB98`, `0x77F430` or `0x77F7E0`, and of the four slot offsets the
only load inside the screen's own range (`0x770000`–`0x790000`) is
`0x782980 lwz r4, 0xA8(r31)`, a field of the object and not a slot of the table. The
dispatch is dynamic — a caller computes the slot, or the object is messaged — so a static
search cannot reach it, and §31.3's "the entry is the screen class's vtable" is where
that search stops rather than a step it completes.

**And the step-`11` guard is `step <= 6`, not `step == 6` as §31.3 records it.** Both
sites test `cmpwi r0, 6; bgt skip` (`0x77AAEC`, `0x77ABD8`), so the transition is taken
from **six or below**: a screen at `3` can jump to `11`, and a screen at `9` or `10`
cannot. The step-`10` setter has no step guard at all beyond `step != 10` (`0x77F438`),
clears `0xbc`, sets `0xc4` and returns `0`. So the chains a join can walk are
`… → 6 → 11` (a loading screen) and `9 → 12` or `10 → 12` (the briefing's byte), and
`10` is enterable from anywhere and leaves only through `12`.

### 32.4 What is left, in the order the wire supports

1. **The six sibling vtables** (§32.3), and the class records that name them. The joining
   screen's step and its triggers can only be read there now.
2. **§30.8's remaining rows**, unchanged: the join answer's `0x1001` bodies, the rules
   family's `len 4`/`len 5` roster list, and the item-box group through a briefing.

The operations clock through a briefing is **not** on that list, though an earlier draft of
this section put it there: the captures say the opposite, and §32.5 measures it. The timing
hypothesis §31.4 left open is closed: the dwell is inside the measured range and the client
still does not open its stream, so the difference is in what the run carries, not when it
arrives.

### 32.5 The clock through a briefing belongs to a played room, and the working capture has none

The claim worth withdrawing is that a briefing is owed the operations clock (`01`, three
bytes) and that its absence is what this host lacks. The first half is wrong: the second is
true of a fresh room here and equally true of the reference. Measured per capture: the
window from a briefing's first `0b` count to the round that ends it, and every `01` record
inside that window.

| capture | briefing window | `01` inside | `01` before the window |
| --- | --- | ---: | --- |
| `mgo2-game-started-on-briefing` | 93.127 → 198.371 | **0** | **0** |
| `mgo2-game2` | 78.987 → 104.649 | **0** | **0** |
| `mgo2-game-failed-memoryviolation` | 62.332 → 77.235 | **0** | **0** |
| `mgo2-started-briefing-then-solo-game` | 167.992 → 174.371 | 1 (2 400) | 65, 2 400 from 57.626 |
| `mgo2-game1` | 517.542 → 565.467 | 25, every one 2 400 | 325 |
| `mgo2-joined-game-in-progress-last-round` | 306.441 → 320.438 | 8 | 139 |
| `myserver24` (this host) | counts 59.553 → 175.448 | 71, every one 3 360 | — |
| `myserver27`, `myserver28` (this host) | first count 57.626 / 57.092 | **0** | **0** |

**The three reference briefings with no round behind them write no standalone clock at
all** — no `01` is in their runs either, and a run carries one only when the room has a
round. The one among them whose client briefs and readies —
`mgo2-game-started-on-briefing`, §32.2 — writes its first `01` as `3840` at its own round's
open, t+198.371, **105 s** after the first `0b`. So the record is not a briefing's timer and
not what a joining client waits for; the briefing's own timer is the `0b` count, which this
host writes.

**The clock's value does ride a briefing — in the run's 90-byte state block, whose tail is
`ff <lo> <hi>` of the room's own limit — and both sides write it there.** Measured from
each host's run:

| capture | state block tail | units | the room's limit |
| --- | --- | ---: | --- |
| `mgo2-game-started-on-briefing` | `… ff 00 0f` | 3 840 | 8 min |
| `mgo2-game-failed-memoryviolation` | `… ff 80 25` | 9 600 | 20 min |
| `myserver28`, `myserver24` (this host) | `… ff 20 0d` | 3 360 | 7 min |

So a briefing is told the round's length on both sides, and the only record the reference
omits from a fresh briefing is the separate three-byte `01`.

**Where the reference does write `01` through a briefing, the room has a round behind it,
and the value is held at the round's whole time** — 2 400 in all 25 records inside
`game1`'s window, 2 400 again in `started-briefing-then-solo`, whose run is what starts it
at t+57.626. That is the branch this host already has: `carriesRoundState` in
`MatchStartRunFramesUtils`, measured in `myserver24`, which holds 3 360 across its briefing
at the ~2.5 s tick. A fresh room writes none on either side — `myserver27` and `myserver28`
write zero `01` records in their whole captures, exactly as `game2` and
`started-on-briefing` do before their rounds open.

**[V]** the counts, the tails and the times; **[D]** that adding the clock to a fresh
briefing is not supported by the captures and would not have been the missing record —
`myserver24`, which writes it, is one of the runs whose client never opens its stream
either, and the reference's own working briefing never writes it.

One measurement kept apart: `mgo2-game-joined-when-red-team-then-briefing` reads `01`
as `128 → 96 → 64 → 32 → 0` over 16.2 s from t+11.671 and holds at zero for 45 s, all
**before** the briefing's first `0b` at t+72.560. That is the short timer
`STATE_VECTOR.md` §4 separates from the briefing countdown, and it is not one of the
records this section counts as inside a briefing window.

`tools/udp_briefing_flow.py` printed this record as `briefing clock` — the label this claim
came from, though its values are 2 400, 3 840 and 14 400 units of the room's time and not a
countdown. It now says `operations clock`.

## 33. The screen is decided at the join, and the settings header is the record the join lacks

The client that briefs does not wait for the host after it echoes the slot pair: it opens its
own stream on its own clock. Measured from the client's echo (`82`/`84 NN`) to its first
`1d` row, in the captures where the two can be told apart from surrounding traffic:

| capture | echo | first `1d` | gap | host traffic in the gap |
| --- | ---: | ---: | ---: | --- |
| `mgo2-started-briefing-then-solo-game` | 55.312 | 57.304 | **1.99 s** | **none at all** |
| `mgo2-game2` | 57.355 | 59.040 | **1.69 s** | the answer at 57.362 |
| `mgo2-game-failed-memoryviolation` | 59.988 | 61.694 | **1.71 s** | the run at 60.871 |
| `mgo2-game-started-on-briefing` | 56.360 | 62.623 | 6.26 s | the run at 58.255, 60.383 |

`started-briefing-then-solo` is the case that settles it: between its echo and its `1d`
records the host writes **nothing**, and the client still reaches the map. So the screen is
not opened by a record of the briefing's tick — it is opened by state the client already
holds, and that state is what the **join burst** (§30.13's window) gave it. What the
briefing writes after the report can only fill a screen the client has already entered.

That moves the search off the tick and onto the join, and the join is where the one
universal difference is. Host-side records of the first ten seconds, by shape (class bit
`0x8000` folded), for the three reference joins into a fresh room and for `myserver28`:

| shape | `started-on-briefing` | `game2` | `failed-memoryviolation` | `myserver28` |
| --- | ---: | ---: | ---: | ---: |
| `0x1001` 26 B, the arrays | 2 | 2 | 2 | 2 |
| `0x1001` 7 B, the close | 2 | 2 | 2 | 2 |
| entries, kind `0x00` | 57, 67 | 57, 67 | 63, 67 | **80, 81** |
| **`0x1001` 25 B, the settings header** | **2** | **4** | **2** | **0** |
| **`0x1001` 10 B, the control** | **2** | **3** | 0 | **0** |
| loadout opening `0b` | 54 | 54 | 54 / 52 / 51 | **50, 50** |
| `0x1001` 161 B, the settings blob | 2 | 4 | 2 | 1 (`0x9001`) |

**The settings header is the only family missing outright, and every reference join has it.**
Its join-time body is `0b <slot> 02` then a 21-byte map and **two** values, where the answer
the client gets from a `83` request carries one:

```
join   0b 01 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 fe ff
answer 0b 02 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

read as `0x02` at map byte 0 = field 1 with the value `0xFE`, and `0x10` at map byte 19 =
field 156 with the value `0xFF`. So the join's copy sets the player's **side-or-unknown field
to the team array's "no side" byte** (`0xFE`, the value `TeamArrayFramesUtils` writes for a
player on none) beside the second field, where `SettingsHeaderBodiesUtils.Build` writes one
field at `0x00` and 24 bytes. The reference writes it **before its client reports** — 8.125
and 10.379 in `started-on-briefing`, 10.236 ×3 and 12.361 in `game2`, 11.085 and 13.206 in
`failed-memoryviolation` — that is, 47 to 49 s before the echo it precedes, and this host
writes no header at all until the client asks for the room at t+76.22, at which point it
writes the *answer* form.

**The ten-byte control is the second, and it is not universal.** `0b 00 00 04 00 00 00 00 00
02`, the settings family's byte, one value at `+9`: two copies in `started-on-briefing`'s join,
three in `game2`'s, none in `failed-memoryviolation`'s and none in this host's. It carries the
room's population, the host counted — `02` in a room holding the host and one joiner. Its
meaning is no longer **[U]**: §34 parses it as a state-0x00 transfer of one field through
`0x27E8B0` — field 10 at `state+83` on the join, field 0 at `state+0` on the round's own
phases — and all 188 ten-byte bodies in the corpus fit that parse with one field each.

**The loadout is not the third.** It looked like a difference of four bytes before the
appearance block — reference (`game2`, 54 B) against this host (`myserver28`, 50 B):

```
ref   0b 02 02 00×15 78 00 00 | 50 02 ff 01 03 02 01 ff 7b 05 | 1e 45 2f 57 39 67 68 0e 0e 00 0e 0e 00 00 0b 16 00 0e 0e 07 0f 00 00
here  0b 01 00 00×18            50 02 fe ff c2 05             | 1e 45 2f 57 39 67 68 0e 0e 00 0e 0e 00 00 0b 16 00 0e 0e 07 0f 00 00
```

So the appearance block sits at `0x1f` there and at `0x1b` here — and the four bytes are
the **skill list**: the region after `50 02` is a marker, the pairs and an end —
`ff 01 03 02 01 ff` there against `fe ff` here — so the four extra are the two pairs the
decoder reads, `HAND_MAS/3` and `SBMC_MAS/1`, where this player has none. Length is
`50 + 2·pairs`, so two equipped skills are 54 bytes and none is 50. The reference's own
no-skill body settles it: `mgo2-game1` t+197.829 writes
`0b 01 02 00 … 50 02 fe ff c2 05 1e 45 …`, **byte for byte what this host writes** for the
same shape. The `78 00 00` at `0x12` is that player's and is zero in the no-skill recorded
body, which is what both no-skill copies carry. Nothing is short; §30.8's shape count is
the skills'.

**And the entry's length is a name, not a record.** Ours are 80 and 81 bytes where the
reference's are 57 and 67, because `PlayerProfileRecordUtility` pads the roster entry's name
field to a fixed width. The record set is the same; the length is the name's.

### What this rules out, and the reading it withdraws

The retry group (§30.13) is **a symptom and not the cause**: it begins in the report's own
datagram, so the client was already in that state before the host answered the report; it
repeats every 5.1 to 8.2 s while the join's data is what it wants; and `myserver24` and
`myserver27` sit on the same screen without sending one.

The earlier reading of its bodies as an unsatisfied retransmission request — `00 01` as a
first ordinal and a count, and `00 01 ac aa` as the same plus a sixteen-bit value — **does
not survive the mirror image**. A two-byte `00 NN` body is a record the reference **host**
writes, on sub `0x58`, as the room's count: `0x1258 00 01` at t+8.125 and t+10.379 in
`started-on-briefing`, `00 02`, `00 03`, `00 04` as its players join (t+194.645 to
+241.521), and `00 01` in `game2` at t+6.112. The reference **client** answers each one with
an **empty** `0x5258`, 131 to 144 ms later (`started-on-briefing` t+95.408 and t+97.674) —
the same empty acknowledgement `PeerAcknowledgementUtils` writes, for the same two-byte
body. So an empty ack is not evidence of an unsatisfied request; it is what the reference
client itself gives that shape.

What would be needed to answer with data is a record on one of those four subs, and no
reference host writes sub `0xf3`, `0xfc`, `0xfd` or `0xfe` in class `0x12`/`0x1a` in any of
the eleven reference captures (§31.1: zero records). An answer carrying records would have
to invent the family it carries. The acks stay empty, and the claim is withdrawn rather
than turned into a second defect; a capture in which a host answers those subs would reopen
it. It is not the gate in any case, because the client that is missing the join's header
asks before any of it.

The arrays are **not** a heartbeat, which is why their sequence byte is not the gate either:
the client echoes `82`/`84` only when the sequence **changes** — `00` through the whole
briefing and `01` at the round's open in `started-on-briefing` (t+56.360 and t+197.175), one
pair in each — so the 136 host-side array records of that capture produce two answers. This
host's `0xf3` (`OpeningSequence`) is a room constant like the captures' `00`, `0f` and `0x6b`,
and its single echo at t+54.882 is the same behaviour.

**[V]** the counts, the bodies, the gaps and the absence of host traffic between an echo and
a `1d`. **[D]** that the gate is the join, and that the settings header — the one family
missing there — is the record to write.

### What was changed

| file | change |
| --- | --- |
| `Rooms/SettingsHeaderBodiesUtils.cs` | `BuildJoin` — the 25-byte form: field 1 at `0xFE` and field 156 at `0xFF`, with the offsets as constants |
| `Rooms/PlayerSettingsBurstService.Header.cs` | new: `RotationAndHostHeader`, the rotation and the host's own header under the burst's consecutive ordinals |
| `Rooms/PlayerSettingsBurstService.cs` | the burst opens with that pair instead of the rotation alone; the class is `partial`, 399 lines |

So the join's burst is now: the control byte, the rotation, **the host's header**, one
loadout per player, the two second loadouts and the state blob — the reference's order, with
the header where `game2` puts it (rotation ordinal `5`, header `6`, loadout `7`; this host's
counter is at `3` for the rotation there, and what the capture settles is the order and the
consecutiveness, not the values).

What would confirm it is the same join with the header on the wire: the client should reach
its `1d` rows within two seconds of its echo, as the three captures above do. What would
falsify it is the same silence with the header there, which would leave the second field's
value as the remaining candidate — the ten-byte control is now written too (§34). The
loadout is not one: its length follows the player's equipped skills, and this host already
writes the reference's own 50-byte no-skill body byte for byte.

## 34. The ten-byte control is a state-0x00 transfer, and its handler is `0x27E8B0`

§33 left the ten-byte record `0b 00 00 04 00 00 00 00 00 NN` as the join's second candidate
with its meaning **[U]**. It is not a settings record at all: it is one field of the round's
own **state**, written through the state-transfer handler, and the arithmetic of its length
settles both its parse and which byte is the record's dispatch key.

### 34.1 It is `[key 0x0b][state 0x00][7-byte map][one value]`

`0x27E8B0` reads a **state index** byte, then `ceil(count / 8)` bytes of one-bit-per-field
presence map, then each present field's bytes, indexing the 26 descriptors at
`*(0x011A646C)` (`docs/analysis/udp-handlers-and-senders.md` §4). State `0x00` has **55
fields**, so its map is `ceil(55 / 8) = 7` bytes and a body carrying one field is

```
1 key + 1 index + 7 map + 1 value = 10 bytes
```

which is exactly the record. Its map byte 1 is `0x04` — bit 2 of that byte, field **10** —
and the value is the byte at `+9`. The other family the corpus holds, `0b 00 01 00 00 00 00 00
00 NN`, sets map byte 0's bit 0, field **0**, with the value at `+9`.

**All 188 ten-byte `0b` bodies in the fourteen captures fit that parse and nothing else**:
each has exactly one bit set, each consumes exactly ten bytes, and every one is state `0x00`.
There is no second reading of the length. The fields are state `0x00`'s own:

| field | offset | length | flags | values measured |
|---:|---:|---:|---|---|
| 0 | `state+0` | 1 | `0x0025` | `00`–`06` |
| 10 | `state+83` | 1 | `0x0025` | `02`–`09` |

Field 10's values climb with the room: `02` at the first join of `started-on-briefing`
(t+93.147), `03` at the second (t+189.909), and on to `09` in the red-team capture. Earlier
readings took the value for the answered peer's slot byte, because a joiner usually takes the
next slot and the two figures then agree; the corpus refutes it where the slots have gaps.
The red-team capture t+106.610 publishes the roster entry for key `8` and the loadout
`0b 09` in the same datagram and writes `07` here; its t+772.235 join writes `08` beside a
`0b 0b` loadout; `game1` t+154.328 writes `03` beside a `0b 10`. And the range does not fit
a slot at all: the same captures hand out keys up to `0x14` (`game1` t+6.687), while all 188
values lie between `02` and `09`. What they do fit is the number of players in the room, the
host counted: `started-on-briefing` writes `02`, `03`, `04`, `05` and `06` as one player
joins after another, and `game1` alternates `03` and `04` across its rounds as its room loses
and regains a player. `mgo2-game2` writes `02` in a room the host's own count record
(`0x1258 00 01`) calls one joiner. Field 0's values (`00`–`06`)
come in the phases' own bursts, `00`/`01` alternating while the briefing waits and `03`–`05`
after it. **[V]** for the parse, the fields and the values; **[D]** for field 10 being the
room's population, the reading every captured value fits and the slot reading does not;
**[I]** for either name — the schema names neither field, and **no call site of `0x27E480`
in the image passes `0x53` as its offset** (283 sites scanned, none), so the consumer that
would name them is not read.

### 34.2 The handler, and the chain that reaches it

| step | address | what it does |
| --- | --- | --- |
| dispatcher | `0x26E7D0` | pops one queued record, reads its **first byte**, looks that byte up in the container's sorted keys and hands the entry and the cursor past the byte to `0x27A180` |
| entry | key `0B`, OPD `0x011F17E0` | the container's fifth entry (`udp-record-dispatch.md` §3.3.1) |
| registered by | `0x270E00` → `0x2711B0` | the run opener registers keys `0x0B`, `0x08` and `0x0D` as it opens a phase |
| handler | **`0x27E8B0`** | `0x27E8E8` reads the state index, `0x27E8F8` indexes the descriptor table, `0x27E934` reads the map, `0x27E998` calls the descriptor's field handler (`0x27EC48`) per set bit |

So the record's **first byte is the key and the second is the state index** — the seam
`udp-handlers-and-senders.md` §4 left **[U]** is closed for this record, because the parse
only balances when `0b` is consumed as the key: keep it in the body and the index would be
`0x0B`, a slot state whose presence map alone is 21 bytes against a 10-byte record.

**What this does not settle is the rest of the burst.** Key `0x0B` is registered for the
whole `0b` family, so a queued 161-byte rotation or a 50–62-byte loadout is read by the same
handler; but neither fits that handler's arithmetic the way the ten-byte record does. Tested
against all 26 descriptors, of the 63 distinct rotation and loadout bodies 14 fit state
`0x00`, 47 fit every slot state at once (the loose case: a mostly-zero map against small
fields) and two fit nothing. So whether the
burst's longer records are state transfers too — and the replay console's rotation and
appearance reads a second reading of the same bytes — is **[U]**, and the ten-byte record's
exact fit is what makes it the one case this section can claim.

### 34.3 What this changes

`§33`'s table called the record a control and its meaning **[U]**; the handler is
`0x27E8B0`, and what it carries is state `0x00` field 10 (join) or field 0 (round phase).

**The field-10 form is now written.** Seven of the nine reference gameplay captures hold the
record, and the join burst is where the reference puts it — the first data record after the
control byte in `started-on-briefing` t+93.147, `game2` t+120.811 and the red-team capture
t+148.371 — so the burst writes it too, from
`RoundStateBodiesUtils.JoinPlayerCountBody` between the control byte and the rotation. Its
value is the room's **population, the host counted** (`RoomRosterService.JoinerCount` plus one),
not the answered peer's slot: §34.1's measurement, and the one difference the burst's own
reading of the room cannot get wrong — the room's players are already in it as loadouts.
All 56 field-10 copies in the corpus are this layout with one byte last, so the builder
reproduces the shape of every one; what changes between copies is the figure, which this
host computes from its own room rather than copying.

Because the value is the room's and not the peer's, the record is written on every join,
including one whose peer the roster cannot name at slot time — a count needs no name. That
is the change from the first version of this record, which wrote `slot + 1` and left itself
out when it had no slot to write.

The field-0 form belongs to the room's transitions rather than to a join, and it is written
there (§34.4). One reference join (`failed-memoryviolation`) writes none of the ten-byte
family at all, so its absence is not a stall on its own. What would still refute the count is
a burst whose room's player count is knowable and differs from the value it writes; every one
checked here agrees.

### 34.4 Field 0 is the room's phase, and the client's join waits on it

The two fields are not the same kind of fact. Field 10 is a figure about the room — how many
players it holds. Field 0 is the **phase the room is in**, and the client's own join machine
is where that is legible:

```
0x2882A8: li    r4, 0            ; the field entry's offset: field 0
0x2882AC: li    r5, 1            ; one byte
0x2882B4: bl    0x27E480         ; read state 0x00's field 0 into the stack
0x2882B8: lbz   r0, 0x71(r1)
0x2882C0: cmpwi cr7, r0, 0       ; 0 -> wait
0x2882C4: beq   cr7, 0x2883D0    ;    and ask again after a delay
0x2882C8: cmpwi cr7, r0, 7       ; 7 -> wait
0x2882CC: beq   cr7, 0x2883D0
                                 ; anything else: write the lobby-slot echo
0x288324: stb   r0=0x82, 0x71(r1)
0x28832C: bl    0x26CC10         ; the stream gets 0x82
0x288340: bl    0x26CC10         ; and then the peer's slot byte
```

So a peer whose room state says `0` or `7` keeps waiting, and every other value releases it.
The corpus' values are `00`–`06`: `mgo2-game2` writes `00` in its round's opening datagram
(t+102.663) and again at the close (t+218.629), its round's own number in between, and the
value behind the close; `game1` climbs `04` → `06` and starts over at `01` when its room
does. That is **the round the match is on, `0` for a room with none**, which is also what the
room's own reset does to the count. **[V]** for the reader, the gate and the values;
**[I]** for the name, since the schema names no field.

**Written at the round's open, on every tick of it, and at its close.** `MatchStartRunService`
writes it behind the round's run, once a tick for as long as the round lasts, and again behind
the result, from `RoundStateBodiesUtils.RoundPhaseBody` — the reference's boundaries, `00` at
the close and the round's number while it plays. All 7 distinct field-0 bodies in the corpus
are that layout (`0b 00 01` then six zero map bytes and the value) and the builder reproduces
every one.

**The tick is the briefing's own 2.35 s, and only while a round plays.** The gaps between two
copies of the record, over the fourteen captures and ignoring the zero gaps that are several
peers' copies at one instant, cluster at 2.37 s (five), 2.35, 2.38, 2.40, 2.41, 2.46 and 2.48 —
thirteen of the thirty-eight gaps in the 1–4 s band — which is
`MatchRoundStateService.BriefingTickMilliseconds`, the period the countdown, the ready set and
the room's rules pair already share. The second mode is 1.87 s, seven gaps in
`started-briefing-then-solo-game` alone, so the reference is not perfectly regular. The record
rides the tick in `MatchStartRunService.PublishRoundPhaseAsync` and stops with the round, so a
peer never receives a phase for a round that has ended.

**A briefing ticks none of it, and the slower gaps are another record.** The 4.9 s, 10 s and
40 s stretches taken for a briefing's own copies of it are all field **10**: every 10-byte body
in a stretch no round was playing is the count, `0b 00 00 04 00 00 00 00 00 <players>`, whose map
byte names field 10 and not field 0. Field 0 is written at a **boundary** and never on a tick
through a briefing, and the two boundaries are its entry's confirmation and its round:
`mgo2-game-started-on-briefing` writes the pair `00`/`01` at t+193.62, again at t+196.03, and
`04` as its round opens at t+200.87, with not one copy in the 193 s of briefing before it;
`mgo2-game-joined-when-red-team-then-briefing` writes `01`/`02` at t+72.56 and again at
t+76.67. **The pair's last copy is never `0`** — so a reference briefing answers the wait with
a non-zero phase and then says nothing until the round's own boundary, while the record it
ticks through the briefing is the count (§52 records what ticking the phase there did).

## 35. The briefing's tick carries the lobby-slot pair, and this host wrote none of it

`myserver29.pcapng` (2026-10-22, `192.168.1.20:5730` joiner ↔ `89.129.16.203:5731` host, key
`0x542b0f05`, 196 frames over 129.5 s) is the join §33's change was meant to serve, and its
client does not report either. What it does instead is in one shape, repeated:

| t+ | what |
| --- | --- |
| 0.28 / 2.32 | its profile, twice |
| 2.37 – 65.4 | its acknowledgements, its `0x9001` keep-alives and nothing else |
| **65.382** | `0a5c 01` — its join state — **and `82 f3` / `84 f3`**, the lobby-slot echo |
| 68.6 – 98.0 | that echo ten more times, 2.0 to 2.3 s apart |
| 70.4 – 101.6 | `1001 NN 00` records of its own; then silence |

So the client reached the step the reference client reaches half a second after its report
(§33's table), said it is in the room, and asked for the arrays — and the host answered the
echo **eleven times with nothing at all**, while the room's rules pair, its state blobs, its
count and its `0xd001` records went out on the tick beside it. That is the whole of the gap
in that session: no run, no countdown, no phase, and no report to ask for any of them.

**The pair is written through a reference briefing, and this host wrote none.** Counted host
side between t+60 and t+100 of `mgo2-game-started-on-briefing` — forty seconds in which that
room is briefed and no round plays — the 26-byte records of the two arrays number **twelve**
(six pairs, one pair per ~7 s, sometimes two copies in one instant); over the same forty
seconds this host writes **zero**. The steady tick of that window is the rules pair, a state
blob every ~11 s, a `0xd001` control, one `0x0002` blob and the occasional array pair; the
item-box pair is *not* on it, and neither are the close pair and the close — those ride the
run's own burst at t+58.255 and t+60.383 (§30.9).

**What was changed.** `MatchRecords/BriefingRoomPublishService` publishes the room's pair on
the briefing's tick, from `TeamArrayFramesUtils.BuildPair` at
`TeamArrayFramesUtils.OpeningSequence` and read per publication from `LobbySlotService`, the
way the confirmation and the entry answer already read it. The sequence is the room's
constant and therefore the one its client echoes — `myserver28` and `myserver29` both echo
`f3` before they have been sent a pair at all — and no confirmation is written beside it: the
`81 NN` belongs to the round's open, which is where the captures put it and where
`myserver20` was stopped by one written 37 ms behind the echo.

**What it does not change.** The run still waits for a report, and the briefing's own
families — the close pair, the close, the item-box groups, the countdown — still ride the
run's burst rather than the tick, because that is where the captures write them. So this
change is one record family on the wire, and it is stated as such.

**What would confirm it** is the same join with the pair on the wire: the client should stop
repeating its echo (it only echoes when the sequence changes) and send its `1d` rows and its
`e1` report behind it, as every reference client does. **What would falsify it** is the same
silence with the pair present, which leaves the four `07` entry kinds §30.10 decodes and the
join answer's profile bodies as the remaining candidates — both are records this host still
does not write.

## 36. The item-box pair runs on past the briefing's run, and this host stopped it at the repeat

**The record.** The item-box pair — `0x1250`/`0x125a` 6-byte bodies under a fresh briefing's
set, `0x1a50`/`0x1a5a` under the next round's — is the one family a briefing writes in bursts
rather than once. Counted host side in `mgo2-started-briefing-then-solo-game`: the run at
t+57.626, then the pair at t+59.993, t+62.244, t+64.71, t+65.04 and t+67.37 — **61 records,
thirty groups, 7.9 s** — and none of it for the rest of that briefing. `mgo2-game-started-on-briefing`
writes its run at t+58.25 and the pair through t+65.12, 90 records, and then not again until
another player reports at t+112.6.

**What that is not.** It is not the briefing's tick. The quiet middle of the same capture,
t+70 to t+110, holds **1.43 host datagrams a second** and **no item-box record at all**: the
rules pair, a state blob every 8.7 s, a `0xd001` control, and a burst of arrays and `0x1254`
when a player joins at t+86.0. `mgo2-game2`'s t+70 to t+100 is the same — 1.27 a second, no
item-box record. So the steady briefing is not the 30 fps beat an earlier reading of the run's
own burst implied, and this host's 2.35 s tick is within a factor of two of it rather than the
wrong shape.

**The client answers every record of the stretch, one for one.** It sends one `0x5250` and one
`0x525a` per item-box record received — `mgo2-game-started-on-briefing` 164 of each between
t+62.6 and t+115.4. `myserver28`'s client sends **twelve** of each, from t+59.21 to t+60.97:
the run's three groups and the repeat's three, and then nothing, because this host wrote
nothing more on that exchange for the remaining sixty-four seconds of its briefing.

**What was changed.** `MatchStartRunService` keeps the pair going behind a fresh briefing's
repeat: thirty groups at `ItemBoxFramesUtils.BriefingContinuationPeriodMilliseconds`
(264 ms — `ItemBoxPeriodFrames` at the beat's measured 33 ms), stopped on the round's open or
on the peer's departure, numbered on from the repeat's last value by
`MatchRecordSequenceService`. The bodies are the existing filler pair, which is what a room
with no spawned box writes, so nothing here invents a box's event.

**What it does not change.** The run's own copies stay as they are — the captures write three
full run frames in the first 2.1 s of a briefing and this host writes two, which is a
repetition of the same bytes rather than a different record and is left for a capture to
confirm. The players' profile records (`0x1001` `07 3x`) are already written, one per player
with its own roster index, in the roster run `RoomRosterFramesUtils.BuildRun` answers the join
with; the `07 37`/`07 3a`/`07 65` records a briefing carries at t+102.76 are that run sent again
when another player joins, which is a broadcast this host has no join-after-join path for yet.

**What would confirm it** is the next live briefing with the pair continuing: the client's
`0x5a50`/`0x5a5a` acknowledgements should go on at roughly four a second for eight seconds
after the run's repeat, against the twelve this host's clients finish on today.
**What would falsify it** is the same acknowledgements with the pair present — which would make
the acks a client clock rather than a reply, and the stretch a record the client does not need.

## 37. The briefing's frame is one second, and the lobby-slot pair is not on it

**The quiet window, counted.** `mgo2-game-started-on-briefing`, t+70 to t+110 — forty
seconds with no report and no join in them — holds **1.43 host datagrams a second**, 76 rules
records (**1.9 a second**), five `0x0002` records of **one** player, that player's alone
repeating every 9.7 s, nine `0xd001` controls, and
**six** 26-byte lobby-slot records — all six inside the burst a join drew at t+86.0, and none
outside it. `mgo2-game2` over its own thirty quiet seconds is the same shape: 1.27 a second,
six array records in one burst at t+79.0, no item-box record at all.

**What this host wrote after §35.** The pair on every 2.35 s tick — 51 array records a minute
against the reference's nine — and the rules pair twice per tick, 51 a minute against 114.
§35 is right that the pair was missing; its rate was not the reference's, and the rate is half
of what a briefing is.

**What was changed.** `BriefingRoomPublishService` runs on a **one-second frame** instead of the
room's 2.35 s tick: the rules pair every frame (1.8 records a second), the pair of arrays every
thirteenth frame (2 records per 13 s — 9 a minute), the state blob every ninth frame (9.0 s),
and the count still only when the room's population moves. The countdown and the ready set keep
the room's tick (`MatchRoundStateService.BriefingTickMilliseconds`, 2.35 s), which is the
measurement they were written from, and `MatchRoundStateService.WaitBriefingFrameAsync` is the
wait that lets a writer choose its own period while the round's open still cuts it short.

**Why the arrays stay periodic and are not made event-driven.** The reference's burst is drawn
by a join, and this host has no join-time hook that reaches a peer already in the room; more to
the point, §35's client echoes the pair at its sequence and must be answered whatever moved, so
a pair that only went out on a change would leave the second echo unanswered — the exact shape
`myserver29` failed on. Thirteen frames is the burst's average on the frame loop: nine records
a minute, the reference's rate, without the answer depending on a change.

**What still differs.** The run's own copies: the captures write three full run frames in the
first 2.1 s of a briefing (`started-on-briefing` t+58.25, t+60.38 ×2) where this host writes
two. That is the same records sent again rather than a record missing, and it is left until a
capture shows whether the third copy is what a client waits on. The per-player profile records
are written once, in the roster run; a room whose players join one after another is a room the
reference re-announces, and this host has no path for that broadcast yet.

**One blob per player, not one per instant — measured before it was changed.** The blob is the
room's per-player record and a briefing writes one for every player at the same instant:
`mgo2-game-joined-when-red-team-then-briefing` t+62.67 carries five 22-byte `0x0002` records
under fourth bytes `2`, `3`, `4`, `5`, `6` — five players, values `32 32 32 32 0a` — and again
at t+72.56, 9.9 s later. `mgo2-game-started-on-briefing` grows the same way as its room fills:
one record at t+62.88, two from t+107.49, four from t+152.0, five from t+186.87. A single
record per instant would announce one player and drop the rest, so the per-player loop in
`BriefingRoomPublishService.PublishStateBlobAsync` stands, with this section as the reason.

## 38. The state blob's value is a per-player ladder: 50, then the host's own figure, then 0

**Every copy of the record, per slot.** `0x0002` is a one-field state transfer whose class is
the slot key plus one and whose last byte is the value (`MatchStateBodiesUtils.StateBlobBody`).
Surveyed over the five captures that hold a briefing, the value follows one ladder:

| capture | slot | the value over the capture |
| --- | --- | --- |
| `game1` | `01` | 100 on every tick, 6.7 s to the end |
| `game1` | `10` | **50**, 91, 90, 90, 94, 98, 94, 94, 99, 94, 98, 94, 94 |
| `game1` | `07` | **50**, 69, 93, 99, 98, 98, 94, 100, 98, 98, 94, 100, 93, 98 |
| `game1` | `0c` | **50**, 55, 73, 83, 77, 78, 78, 83, 83, 78, 73, 85, 74, 79 |
| `game1` | `05` | **50**, 53 — then the slot stops being written |
| `game1` | `15` | **50** at t+6.7, **0** on every tick from t+78.3 |
| `game2` | `02` | **50**, then 10 on every tick from t+10.2 |
| `game2` | `03` | 100 on every tick |
| `started-on-briefing` | `02` | **50** at t+6.0 … t+32.4, then 10 from t+38.9 |
| `started-on-briefing` | `03`, `04`, `06` | 100 from each one's first tick |
| `started-on-briefing` | `05`, `07` | 50, never measured |
| `red-team` | ten slots | 50 throughout; `06` moves to 10 at t+27.9 |
| `solo` | `01` | 100 on every tick |
| `solo` | `04` | **50** at t+6.0, **0** from t+92.9 |
| `solo` | `08` | **50**, 38, 0 |

**What that reads.** Three values are measured facts rather than inference: **50** is what every
peer is named with first, in all five captures and for all fourteen players — the rung the
ladder starts on; **100** is what the host's own player carries in every capture that holds it
(`game1` slot `01`, `game2` slot `03`, the solo capture's `01`), and what some peers sit at
(`started-on-briefing`'s `03`, `04`, `06`); **0** is what a slot falls to once its player has
gone, and no slot's record is dropped when that happens.

**What it counts is still [I].** Higher is better and the numbers never leave 0–100. A round
trip is the reading they fit — 100 for the host's own player, 50 for a peer the host has not
measured, 10 for one at 90 ms — and the same host holds a per-player ping over TCP (`0x4398`,
`GamePlayer.Ping`), but no packet in these captures carries that ping (their TCP streams are
all HTTPS) and nothing on either wire names the unit. A host that measured a peer could write
it here; this host measures none, so it writes the two values it can name.

**What was changed.** `MatchStateBodiesUtils.EmptyStateBlobValue` (0) is new and
`BriefingRoomPublishService` keeps the slots it has announced, writing a slot that its player
has left as empty instead of letting the client keep the last value it was told. The 100 for
the host's own slot and the 50 for a peer are unchanged: those *are* the measured values for
the two cases this host can be in, and inventing a measured figure it has not measured is what
`docs/analysis/briefing-start-comparison.md` §4.3 refuses.

## 39. The blob's value is now this host's own round trip, timed from the peer's answers

**The measurement is on the wire already.** A client answers a record it received by sending
the same identifier under the answer bit (`0x1001 → 0x5001`, `0x1250 → 0x5250`) with the
record's fourth byte echoed in its own. One answer per record, no batching: `myserver28`'s
client sends twelve `0x5a50` and twelve `0x5a5a` for this host's twelve item-box records, and
`mgo2-game-started-on-briefing`'s sends 164 of each for 164. So the pairing is the protocol's
own and needs nothing added to the stream.

**What was changed.** `PeerSession` stamps every record it writes with the instant it left
(`NoteSent`, called once per message in `GameplayServerService.Outbound.SendMessages`) and, on
an answer that names a record still waiting, records the difference as a round-trip sample:
smoothed over four samples (`(3 × previous + sample) / 4`), dropped if the record is older than
ten seconds, and bounded to 512 pending records per session so a peer that answers nothing
costs nothing. `MatchStateBodiesUtils.StateBlobValueFor` turns that figure into the byte the
briefing writes — `100 - roundTrip`, clamped to 0…100 — and the host's own player stays at 100.

**Why that mapping.** It is the only reading of §38's ladder that puts every observed value in
the byte's range with no unit of its own: 100 is the host's own player (0 ms), 50 is the
unmeasured default (50 ms), `game1`'s 91–99 is 1–9 ms, its 73–85 is 15–27 ms, `game2`'s and
`started-on-briefing`'s 10 is 90 ms, and a peer above 100 ms lands on the 0 that the captures
write for a slot whose player has gone. A peer this host measures at 12 ms is written 88; one
that has answered nothing is still written 50.

**What it is not.** It is an upper bound of the wire's round trip rather than the wire's alone —
the peer's frame handling is inside the interval — and it is **[I]**, because nothing on either
wire names the unit; if a client answers with a fourth byte that names something other than the
record's own, the pairing fails and the value stays at 50, which is the reference's own value for
a peer it has not measured.

**What would falsify it** is a live briefing where a healthy peer's value lands at 0 or a slow
one at 100: that would mean the ladder is not `100 - roundTrip` and the value should go back to
the fixed 50 rather than stay wrong.

## 40. `myserver30`: the client reports, and the briefing's records match the references family for family

`myserver30.pcapng` (2026-10-22, `192.168.1.20:5730` console ↔ `89.129.16.203:5731` host, key
`0xab1cc0da`, 844 frames, 235.4 s) is the join §35's pair was written for, and it is the first
capture of this host in which the client **reports**. Its screen still does not come.

**§35's prediction is half confirmed, and on the wire it is exact.** With the pair published
through the briefing the client stops repeating its echo — one `82 f3`/`84 f3` at t+50.740,
where `myserver29` re-sent it eleven times over thirty-five seconds — and, in the same
datagram, it writes the load report `1ade e1 01` and the presence companion `0a5c 01`. The
host answers at t+50.785 with the companion at `02 00 00 00`, the publication `1ade e0 01 00 00
00 00 00 00` and the marker `5ade`, then writes the run at t+52.799 and the countdown at
t+52.950. The second half of the prediction — the client's own `1d` rows behind the report —
does not happen: the capture holds **no `0x1d` row and no `0x1f` ready** from the client at all.

**What it writes instead is one group, twenty-nine times.** Every ~5.4 s from t+50.740 to
t+201.919 the client sends `1acd 01 01` and then `1ae2` … `1ae7`, each `ff 01`, in one
datagram. The host answers the first with `dacd` and the six with empty `5ae2` … `5ae7`
acknowledgements, one for one. The client's last datagram is t+205.762; the capture's host
traffic runs to t+235.4.

**The briefing's own records are all there, and they are the references' byte for byte.** Over
t+50.7 to t+76, folded by class bit and compared with `mgo2-game-started-on-briefing`'s
t+55.5 to t+80 (a fresh room, no round behind it) and with `mgo2-game3` at its run (the one
reference on this host's own generation, `0x0a61`/`0x1a54`/`0x1ade`), every family the
reference writes is written:

| what | this host | reference |
| --- | --- | --- |
| answer to the `82` echo (`0x9001`) | `d001` one byte `24` | `d001` one byte `24` (`game3` t+59.596) |
| answer to the `84` echo (`0x1001`) | empty `5001`, the echo's own fourth byte | empty `5001`, same fourth byte (`game3` t+59.596) |
| marker | `5ade` beside the publication only, run clear of it (§26) | the same |
| run's state block, 90 B | `00 00 00 ff … 18 18 ff ff ff ff 18 18 18 18 18 00 00 00 00 fe fe fe fe 3c 3c 3c 3c … ff 20 0d` | identical to `started-on-briefing` t+58.255's, whose tail reads `ff 00 0f`: the room's own limit and nothing else |
| `0f 00` | present | present |
| arrays, 26 B | `85 f3 fe 00 ff …` / `86 f3 7f 07 7f …` | `85 00 fe ff …` / `86 00 7f 7f …`: same two bodies, the room's players as the arrays name them |
| close pair, close | two 20 B and one 7 B, twice | the same |
| countdown, 7 B | `0b 3c 00 00 00 00 00`, every 2.4 s | `0b 3c 00 00 00 00 00`, from t+93.127 |
| item-box pair | six groups at the run, then one every 264 ms | 90 of each through the run's 7 s |
| rules pair | two `fe`, 1.9 a second | two `fe`, 1.9 a second |
| state blob, 23 B | once a run | once a run |

**So the gate is not in the briefing's own stream.** The 90-byte state block matching
`started-on-briefing`'s byte for byte except for the room's own time limit (3360 against 3840)
is the strongest single identity available: the client is handed the same room description a
client that briefs is handed. The answer to its echo is `game3`'s answer, record for record.
What `myserver30` removes from the list is therefore the whole of §30.8 still open on the
briefing's side, and the pair's absence that §35 named.

**The one stream with no counterpart is the client's own.** No reference client writes
`1acd`/`1ae2` … `1ae7`; `mgo2-game3`'s client — same generation, same host build shape — sends
none of them, and the capture that holds them host-side (`mgo2-game-joined-when-red-team-then-
briefing` t+850.167) writes them as `1acd 00 <value> ff ff 00` and `1ae2` … `1ae7` `ff <value>
18`, three bytes each, with `<value>` the same across both families. This host's client writes
the two-byte form `ff 01` and gets an empty answer, and repeats it for 151 s.

**What would confirm the next reading** is a capture in which those seven records are answered
with the reference's bodies: the client's repetition should stop there, as its echo stopped when
the pair appeared. **What would falsify it** is the repetition continuing with them answered, or
a reference client that writes the group at all. Nothing was changed in the server for this
section: the measurement closes §35 and §30.8's briefing rows, and the two candidates left are
§32.4's sibling vtables and this group.

**The binary adds one constraint, and it is about the fade.** §31.3/§32.3 left the step
machine at "the joining screen's step and its triggers can only be read there now", so the
seven tables were read. The child screen — the one the `0xd001` byte drives, table
`0x11FEB80` — has its step-12 setter at slot 44 (`+0x160`), code `0x77F7E0`, and the guard is
exactly

```
0x77f840: bl    0x27e198        ; read state 0x00 byte 0xa1 (field 161)
0x77f850: bne   0x77f900        ; nonzero: the other path, which re-enters the same test
0x77f880: lwz   r0, 0xb8(r28)   ; the screen's step
0x77f888: bgt   0x77f8d0        ;   step > 9 -> nothing happens
0x77f88c: lbz   r5, 0xd0(r29)   ;   field 24, the d001 byte
0x77f894: beq   0x77f8d0        ;   == 0xff -> nothing happens
0x77f898: beq   0x77f8d0        ;   step == 12 -> nothing happens
0x77f8c8: bl    0x7770f0        ; emit(0x16002C, byte), step = 12, 0xbc = 0, 0xc4 = 1
```

So the byte this host writes every five seconds can move a screen that is at **step nine or
below**, and nothing else — a screen already at `10` or `12` is untouched by it, and the
`0xa1` path at `0x77f900` merely reads bytes `0x52` and `0x58` and rejoins the same test. The
six sibling tables (`0x11FFC00`, `0x11FFC70`, `0x11FFF40`, `0x1200048`, `0x1200090`,
`0x12000E8`) carry their own methods at the same slots, and none of their slot-44 entries
touches `+0xb8` at all: the join screen is the only object this record's byte reaches.

The consequence for the symptom is that **the fade-out is not a state `0xd001` can rescue**.
If the client has drawn the map and the rule text and is sitting in the fade, its step is
already past the only window the byte opens, and the fade-in has to be another object or
another input. That is what the next reading has to name, and it is the one thing in this
capture's session that is still unexplained.

## 41. The `0xc4` flag is read by the script bindings, and the `1d` rows wait on a session flag

§40 left one question for the binary: who consumes the screen's `0xc4` flag, and which screen it
builds at step `12`. The answer is that **no C++ code builds it** — the flag is exposed to the
game's own script layer — and the client's missing `1d` rows turn out to be gated on a flag of
the session object, which is a better place to look than the screen.

**The flag has one reader in the whole image.** The writers are the step setters themselves
(`0x77A904`, `0x77AA48`, `0x77AAFC`, `0x77ABE8`, `0x77B278`, `0x77B2B0`, `0x77B46C`,
`0x77E6E8`, `0x77F454`, `0x77F8C0` and the rest — thirty sites in the screen code). The only
instruction that reads the byte is

```
0x77baf0: lbz r3, 0xc4(r3)
0x77baf4: blr
```

and it is reached from exactly one call site, `0x74903C`:

```
0x74902c: lwz  r30, -0x7300(r2)   ; the UI manager
0x749034: lwz  r9,  -0x8000(r30)  ; its screen slot
0x749038: lwz  r3,  0(r9)         ; the screen the client is on
0x74903c: bl   0x77baf0           ; its 0xc4 byte, returned as-is
```

That function is one of a run of one-line thunks at `0x748FE0`–`0x749100` that each fetch the
same global and return one field of the current screen (`0xb0`, `0xc4`, and the predicates at
`0x77BB08`: step `== 4` with `0xbc == 0`, and `0x77BB30`). They take no arguments, and the only
things that call them are `0xB2DFA0` and `0xB35670`, the script bridge's own call path — the
thunk `0x749020` draws two callers and both are in it. So the byte is read by a **script**, from
a screen the C++ side never inspects for it, and no vtable is installed when the step is
written. **The "next reading" of §40 — the hand-off — is therefore answered as far as the image
allows and withdrawn as a lead.**

**The event the step writes carries no subscribable name.** `0x16002C` is formed by exactly two
instructions in the image (`0x77E6C8` and `0x77F8C8`, both emitters). No other site builds the
constant, so the listener is registered at run time: the bus `0x7770F0` forwards to `0xC1208`,
which walks a table of twelve-byte entries (`addi r29, r29, 0xc`) keyed by the event word and
compares with `cmpw` against an id it never materialises as an immediate. The subscriber is a
script callback, for the same reason the flag's reader is a thunk.

**And the client's `1d` rows are gated on a session flag, not on a screen step.** The writer is
`0xB9C2F8` — one of two copies, the other at `0xB9D5F8` — and the guard around it is

```
0xb9c2c0: bl    0x26d540        ; returns session->0xbcc & 1
0xb9c2c8: beq   0xb9c224        ;   zero -> no row
0xb9c2d0: lbz   r0, 0x70(r1)    ; a per-slot byte the helper at 0xb9b3b8 filled
0xb9c2d4: beq   0xb9c224        ;   zero -> no row
0xb9c2f8: li    r0, 0x1d
0xb9c30c: bl    0x26cc10        ; the stream gets 0x1d
0xb9c320: bl    0x26cc10        ; and the row's second byte
```

`0x26D540` is `lwz r3, 0xbcc(r11); clrldi r3, r3, 0x3f` — the low bit of the flag word at
`0xbcc` on the object reached through `r2-0x7a9c` → `-0x8000` → `0(r9)`; the neighbours in the
same block read `0xbcc`, `0xbc9` and `0xc9` of the same object, so it is the client's session
state **[I]**, and bit 0 of `0xbcc` is "this session may write its operations rows". A jump
table at `0xB9C260` switches on `r29` (`0`–`6`, `cmplwi 6`), and the case that falls through to
`0xB9C2F8` is the one that writes a row's `1d` opcode and then its slot byte, so the same
function is what writes the five rows `00`–`04`.

**Bit 0 is not set by any of the word's own setters.** The family at `0x26D6A8` (bit 17),
`0x26DBE8` (bit 3), `0x26F42C` and `0x2716EC` and `0x2721F0` (bit 2), `0x271240` (bit 3),
`0x27158C` (bits 8–11) and `0x272BBC` (bit 11) all write other bits, and no `stw` to `0xbcc`
follows an `ori …,1`. So the bit that opens the client's stream is set somewhere the block
above does not name, and until it is set the client can neither open its stream nor ready — which
is the state `myserver30` shows for 151 s.

**What would settle it** is the caller chain of the setter that touches bit 0: it will name the
record the client is waiting for, exactly as `TeamArrayFramesUtils`' pair named the echo. Until
that chain is read, the briefing's records stay as §40 leaves them — matching a working
reference family for family.

## 42. Bit 0 is a session kind chosen where the session is built, not a flag a record sets

§41 closed with one question: *the caller chain of the setter that touches bit 0 of `0xbcc`*.
There is no such setter. The word has twenty writers in the session object, and not one of them
sets `0x1`; the single write that can carry it is the constructor's, and the value it carries is
an argument its six call sites each spell out. So bit 0 asks **what kind of session this is**, and
the client answers that itself.

**The writers, read from the whole image rather than a block.** Every `stw …,0xbcc(rA)` with a
real base register is below — the whole population, twenty writers on the session object and a
handful on unrelated objects that happen to hold a `0xbcc` field:

| Site | Operation | Bits |
| --- | --- | --- |
| `0x26D6A8` | `oris r0,r0,2` | `0x00020000` |
| `0x26D6CC` | `rlwinm 14,1,31` + `rotlwi 18` | clears `0x80000000` |
| `0x26D7F4` | `oris r0,r0,0x20` | `0x00200000` |
| `0x26D824` | `rlwinm 10,1,31` + `rotlwi 22` | clears `0x80000000` |
| `0x26DBE8` | `ori r0,r0,8` | `0x8` |
| `0x26E340` | `oris r0,r0,0x40` | `0x00400000` |
| `0x26E374` | `stw r9` from `rotlwi r9,r9,0x17` | whole word, field extract |
| `0x26F42C` | `ori r0,r0,4` | `0x4` |
| `0x270460` | `and r0,r0,0x12` | keeps `0x12` only |
| `0x2709DC` | `stw r29` with `li r29,0` | clears the word |
| `0x270EFC` | `stw r29` with `r29 = r5` | **the constructor's argument** |
| `0x271240` | `ori r0,r0,8` | `0x8` |
| `0x27158C` | `ori r0,r0,0x700` | `0x700` |
| `0x2716EC` | `ori r0,r0,4` | `0x4` |
| `0x2721F0` | `ori r0,r0,4` | `0x4` |
| `0x272A68` | `or r0,r9,r3`, gated on `0x10` | a computed word |
| `0x272B14` | `rlwinm 11,1,31` + `rotlwi 21` | clears `0x80000000` |
| `0x272BBC` | `oris r0,r0,0x10` | `0x00100000` |
| `0x272DB4` | `or r0,r0,(r24 & 0x00100000)` | `0x00100000` |
| `0x272E60` | `oris r0,r0,4` | `0x00040000` |

Not one immediate in that column is `1`, and the three rotate pairs are `rlwinm …,1,31` followed
by a `rotlwi` that sums to thirty-two — they *clear* the top bit, they do not set a low one. So
the low bit is not written by any of the twenty. **It is written once, when the word is created**:

```
0x270e5c: mr    r29, r5            ; arg 5
0x270e6c: lwz   r31, 0xc0(r9)      ; field 0xc0 = "a session already exists"
0x270e74: beq   0x270ec8           ;   zero -> build it
...
0x270ef8: clrlwi r28, r29, 0x1f    ; the low five bits are kept separately
0x270efc: stw   r29, 0xbcc(r9)     ; and the whole argument becomes 0xbcc
```

**And the six call sites each spell the kind out**, `0x270E00` being the session constructor:

| Call site | `r5` | Low bit |
| --- | --- | --- |
| `0x288DD8` | `li r5, 0x18` | `0` |
| `0x2890C0` | `li r5, 0x18` | `0` |
| `0x8EE3B8` | `li r5, 2` | `0` |
| `0x8EE60C` | `li r5, 3` | **`1`** |
| `0x9E97D4` | `r5 = r28`, `r28` possibly `\| 0x2000` | caller's flags |
| `0xAA13A0` | `r5 = 0x2002` or `mask \| 0x2002` | `0` |

So `0xbcc` is a **kind word**, and the gate at `0xB9C2C0` reads "was this session built as kind
`3`?" rather than "has a flag arrived". Only one of the six construction paths produces that low
bit, and it is a branch of the client's own connect flow:

```
0x8ee5a0: lbz   r9,  0x85(r31)   ; two flag bytes of the menu object
0x8ee5a4: lbz   r11, 0x86(r31)
0x8ee5ac: andc  r0, r11, r9      ; bits set in 0x86 and not in 0x85
0x8ee5b0: rlwinm r0, r0, 0, 0x1d, 0x1d
0x8ee5b8: bne   cr7, 0x8ee700    ;   bit 2 -> a different branch
0x8ee5cc: bl    0xf06808         ; a query of the client's own state
0x8ee5d8: cmpwi cr7, r3, 0
0x8ee5dc: bne   cr7, 0x8ee320    ;   non-zero -> a different branch of the same machine
0x8ee608: li    r5, 3            ; zero -> a kind-3 session
0x8ee60c: bl    0x270e00
```

and its sibling, which builds kind `2` after a virtual call returns `8` behind a `cmplwi r3,
0x3a98` timer test (`0x8EE35C`, `0x8EE38C`). Both are branches of the menu state machine that
writes its own state word at `0x78(r31)` (`li r0,7` at `0x8EE3C8`, `li r0,0xa` at `0x8EE320`). No
host record is consulted at either branch.

**What that settles.** §40 and §41 both ended on a missing record — the fade-in's input, and the
record the client waits on before it writes its `1d` rows. Neither exists. The rows, the ready
and the fade are all behind state the client builds for itself at session creation, so a client
that never writes `1d` was constructed as some kind other than `3` and is waiting on its own
transition, not on this host. §41's last paragraph ("set somewhere the block above does not
name") is answered, and no server change is implied by any of it: the briefing stream still
matches a working reference family for family (§40).

**The same reading answers the screen question from §40 from the other side.** The script table
`0x11FD700` is a forty-slot mirror of the screen's vtable `0x11FEA80` — slots `11`–`16` are the
same function in both, and each thunk fetches the current screen through
`*(*(r2-0x7300)-0x8000) → 0(r9)` and tail-calls it. The accessors:

| Slot | Address | Returns |
| --- | --- | --- |
| `11` | `0x77BAC8` | `0xb8 == 3` |
| `12` | `0x77BAF0` | the `0xc4` byte — the only reader in the image |
| `13` | `0x77BAF8` | `0xb0` |
| `14` | `0x77BB00` | `0xb4` |
| `15` | `0x77BB08` | `0xb8 == 4` and `0xbc == 0` |
| `16` | `0x77BB30` | **writes**: `++0x6ac`, `0x6ad = r4`, `0xbc = 0`, `0xb0 = r5`, step `4` |
| `17` | `0x77BB58` | entry base `this + 0xd8 + n*0x34` |
| `18` | `0x77BB70` | `0x5b8` |
| `19` | `0x77BB80` | `*(0x5bc + n*4)` |
| `20` | `0x77BBA0` | `*(0x5cc + n*4)` |
| `21` | `0x77BBC0` | `0xac`, with its setter at `0x77BBD0` |

A script can therefore ask the screen for two step *predicates* and one step *transition*; it
cannot ask for the step itself, and the only byte records drive that it can read is `0xc4` at slot
`12` — which the step setter refuses to move at step `≥ 10` (§40). So **no second screen is built
at step `12`**: the steps are fields of this one object, written by its own transition at slot
`16`, and the predicate that reads them (`0xb8 == 4`, `0xbc == 0`) is false both before and after
`12`. The hand-off lead of §40 is withdrawn on both sides — there is no hand-off, because there is
no other object.

**What is left to read** is the pair of branch conditions above: the virtual call whose result is
compared with `8` (`0x8EE38C`) and `0xF06808`, whose result is compared with `0` (`0x8EE5D8`).
Those two name what the client waits on before it builds a kind-3 session, and they are the last
place a host answer could possibly be felt. Everything else on this path is now accounted for.

## 43. The phase record now rides the briefing's frame, and the re-test's falsifiers

> **Withdrawn by §52.** The premise — that the reference's 4.9 s, 10 s and 40 s gaps are field 0 — is
> wrong: those records are field 10, the room's count, and the reference writes field 0 only at a
> boundary — its entry's confirmation and its round — with a non-zero last copy. The writer this
> section adds is removed again, and `myserver31.pcapng` is the
> measurement: a `0` every 2 s keeps the client's placement machine in its wait, so the echo that
> opens its entry is never written and the briefing's run never goes out. The falsifiers below stand
> as they were.

§34.4 measured the room's phase as the one host-written field a client's join machine gates on, and
§42 showed that the fade and the `1d` rows are not driven by any record. What survives both is a
hole the round's own writer cannot cover: **every copy this host writes comes from a round**.

**The gap, in the code rather than the wire.** `MatchStartRunService` writes field `0` three times
— behind the round's run, once a `PhasePeriodMilliseconds` for as long as the round lasts, and
again at its close with `0` — and there is no other writer in the tree. So a peer that is in a
briefing *before its first round*, and the peer that joins between two rounds, receives no copy of
the field at all, and its state store keeps whatever the last room it was in put there. The
reference does write it in those stretches: §34.4's slow gaps (4.9 s, 10 s, 40 s) are exactly the
intervals in which no round was playing, and a record that only a round writes cannot produce them.

**What was changed.** `MatchRecords/BriefingRoomPublishService.Phases.cs` adds the writer and its
period to the briefing's publication, and the frame loop calls it every second frame —
`RoundPhaseFrameInterval = 2`, which is 2.0 s against the reference's 2.35 s tick, the nearest whole
frame on a one-second loop (0.5 records a second against 0.43). The value is
`MatchRoundStateService.RoundNumber`, the round the room is on and `0` while none is open, which is
the figure the corpus' briefings carry and the one `RoundStateBodiesUtils.RoundPhaseBody` is built
for. Nothing else of the briefing's frame moved: the rules pair, the array pair, the state blob and
the count keep their rates.

**What it does not do.** It does not release a client that is holding a wait value: `0` and `7` are
the values the client waits on, and a briefing's room is `0` until its first round opens. What it
changes is *whose* `0` the client holds — this room's, written 2 s ago, instead of the one its last
room left — and it puts the round's number on the wire the moment one opens, without waiting for
that peer's own report. It is a correctness fix on the wire's own terms, and it is stated as that
rather than as a remedy for the stall.

**What a re-test has to falsify, in order.**

1. If a client that joins a room which never opens a round now reaches its `1d` rows, the field was
   the gate and §34.4's reading is promoted from **[I]** to **[V]**. If the same silence follows
   with the record written every 2 s, the join's gate is not what holds that client, and the
   remaining candidates are the four `07` entry kinds §30.10 decodes and the join answer's profile
   bodies.
2. If a stalled client begins writing `1d` rows with no further host change, §42's kind reading is
   wrong — the session kind is not fixed at construction, or something else re-creates it.
3. If a capture shows a host writing field `0` at a boundary other than a briefing's tick and a
   round's open, tick and close, the period above is wrong and the record belongs to that boundary.
4. A stalled client whose own `82`/`84` echo stops being repeated, with the arrays unchanged, says
   the echo was answered at its sequence and the pair is not the gate (§35).

## 44. The two conditions that build a kind-3 session are the client's own, and one is a pad byte

§42 left two branch conditions unread — the pair that must hold before the client builds the kind
`3` session whose low bit opens its `1d` rows. Both are now read, and neither consults the wire.

**The state query is a 130-entry table, and the id is `0x1A`.** `0xF06808` is a thunk, not a
function: `li r4, 0x1a` then `bl 0xEFEDD0`, which is

```
0xefedd4: cmplwi cr6, r4, 0x81     ; ids 0..0x81 only
0xefede0: slwi   r9, r4, 2
0xefede8: addi   r0, r9, 0x370     ; entry at 0x370 + id*4
0xefedf4: lwzx   r11, r3, r0
0xefedf8: extsw  r3, r11           ; -0x18 for an id out of range
```

so the join's idle path asks whether **slot `0x1A` of a 130-entry word table is zero**, and its
sibling thunks at `0xF06834`/`0xF06860` ask the same table for ids `0x20` and `0x1F` through
`0xF015D0`, which reads a second array at `0x168 + id*4` and answers `-1` when that word is `2`.
The table is the client's own — the id is a constant in the thunk, the base is an argument, and no
record of the gameplay stream is read to decide it.

**The `0x85`/`0x86` bytes are the menu's input bits, written per frame.** The gate at `0x8EE5A0`
takes `0x86 & ~0x85` — the bits pressed this frame — and the writes around it are the frame
bookkeeping: at `0x8EE56C` the code consumes bit `0x4` (`0x86 |= 4`, `0x85 &= ~0x4` via
`rlwinm r11,r0,0x1d,1,31` + `rotlwi 3`), and at `0x8EE714` it sets it. So the kind-3 branch is
taken on a frame with **no new press** whose table slot `0x1A` is zero: an idle frame of the
client's own menu, not an answer to anything.

**What is still unread** is the third condition beside it, the virtual at `0x8EE384` whose result is
compared with `8`. It is reached through three dereferences from `0x88(r31)`
(`lwz r9,0(r11)` → `lwz r10,0(r9)` → `ld r0,0(r10)` → `bctrl`), so its class is not identifiable
statically and the `8` is not attributable to a record. Nothing in the readings above needs it:
the two conditions that decide the session kind are the client's own state and its input.

## 45. The `0x88` virtual is a peer slot's state, and `8` is the value that drops the client to kind 2

§44 left the virtual at `0x8EE384` unread. Its operand and its meaning are now read; its getter's
body is not, and the reason is a gap in the pool's construction that is worth stating rather than
papering over.

**`0x88(r31)` is one of the client's fixed 24 peer slots.** The screen stores it at `0x8EE498`, from
`0x261FE0(buf, 4, 0)`, and that function is a pool allocator:

```
0x261fec: lwz   r30, -0x7abc(r2)   ; the module's own block
0x262004: lwz   r9,  -0x8000(r30)  ; its session
0x26200c: cmpwi cr7, r0, 2         ; 2 -> allocate nothing, return 0
0x26203c: li    r0, 0x18           ; twenty-four slots
0x262040: lwz   r31, 0x28(r9)      ; the pool's base
0x262050: addi  r31, r31, 0x6d8    ; stride
0x26205c: lbz   r0, 4(r3)          ; the slot's "in use" byte
0x262074: bl    0x268148           ; the initialiser
```

There is a second pool for one kind (`0x2620A0`: `addis r9,r9,1` then `addi -0x6298`, i.e.
`+0x10000-0x6298`), used when the requested kind is `2` (`0x262034`). The initialiser writes
`+4 = kind`, `+5 = arg3`, `+0x56 = 1`, `+0x78 = 0`, `+0x88 = 0x708`, clears `+0x40`, `+0x42` and
`+0x44`, and copies a five-word descriptor out of the caller's structure into `+0x18`–`+0x28`. So
`0x88(r31)` is the screen's **peer slot** — 24 of them, 0x6d8 apart, the same width as the room's
own player table.

**The virtual is slot 0 of that slot, and `8` is the state that ends the client's session.** The
dispatch is the ordinary PPC64 shape — `lwz r9,0(r11)` (the slot's vtable), `lwz r10,0(r9)` (its
slot-0 descriptor), `lwz r0,0(r10)` + `lwz r2,4(r10)` (code and TOC), `bctrl` — and its result is
compared with `8`:

```
0x8ee38c: cmpwi cr7, r3, 8
0x8ee390: bne   cr7, 0x8ee268    ; not 8 -> the frame ends doing nothing
0x8ee3b0: li    r5, 2            ; 8 -> a kind-2 session
0x8ee3b8: bl    0x270e00
0x8ee3c8: li    r0, 7            ;   and the screen moves on
0x8ee3cc: stw   r0, 0x78(r31)
```

So the value is a **state code of the peer slot**, and `8` is the one at which the client builds a
session of kind `2` — the kind §42 showed never writes `1d` rows, because only kind `3` carries the
low bit of `0xbcc`.

**What that implies, and how far it is evidence.** If a client's peer slot reaches `8` — the slot the
screen allocated for its own connection, then told to move on — the client rebuilds its session as
kind `2` and from that instant cannot write its operations rows, cannot ready, and cannot leave the
fade, which is the state `myserver30` shows for 151 s. That is **[I]**: the getter's body is unread,
so the field it returns and the write that puts `8` there are both still open.

**Why the body is unread.** The pool's initialiser installs no vtable: over `0x268148`–`0x268B00`
the only word store to a zero displacement is a buffer (`0x268418`, `stw r0,0(r11)`), so the slot's
first word is set where the pool is first built or by a base constructor outside that range. The
candidate reached through the GOT is `0x1191AF8` (installed by `0x269130`, `stw r0,0(r3)` from
`*(*(r2-0x7ab8)-0x7ff8)`), and its slots `0`–`7` are all descriptor `0x0121A628` → `0xFB32B8`,
which calls `0xFBA650` and returns `0`: the pure-virtual stub. A table whose slot 0 always answers
zero cannot be the one a slot-0 state getter lives in, so the vtable belongs to another class in the
same neighbourhood and the slot's own is still to be found. What would settle it is the pool's first
construction site — the one that writes a vtable into `+0(rX)` of the base at `0x28(r9)` — and that
is the next thing to read.

## 46. The slot's vtable is `0x1191AB8`, slot 0 returns its `+4` byte, and `8` is its teardown mark

§45 left two things open: which table the pool's slots carry, and what puts `8` into the field that
`0x8EE38C` compares. Both are read now, and they resolve each other.

**The vtable is `0x1191AB8`.** The class's setup blocks install it from the module's GOT —
`lwz r0,-0x7ffc(r30)` then `stw r0,0(r3)`/`0(r29)` at `0x2850A4`, `0x28513C`, `0x2851DC`, `0x285684`,
`0x2856B4`, `0x28599C` and `0x285A34` — and the GOT page holds it at `0x11A632C` (`*(*(r2-0x7ab8)
-0x7ffc)`), one word before the abstract base `0x1191AF8` whose slots `0`–`7` are the pure-virtual
stub. The derived table is complete:

| slot | address | what it does |
| --- | --- | --- |
| `0` | `0x263328` | `lbz r3, 4(r3); blr` — **the byte at `+4`, returned** |
| `2` | `0x263330` | `if (+4 != 0) +4 = 1` — the "taken" mark |
| `8` | `0x269130` | installs this vtable (`stw r0,0(r3)`) |
| `9` | `0x269148` | the same, for a reset path |
| `12` | `0x264C78` | |
| `13` | `0x263DF0` | |

So the virtual at `0x8EE384` is not an opaque state call: it returns **the byte at `+4`**, the same
byte the pool allocator tests for zero when it looks for a free entry (`0x26205C`), the same byte the
peer table's own step gates on (`0x262544`: `lbz r0,4(r31)` … `cmplwi 6` … `bgt`, so a slot past six
is not stepped), and the same byte `0x268148` initialises to the allocation kind.

**`8` is written in exactly three places, and both kinds of writer are the client's own teardown.**

```
0x268c44: clrlwi r0, r11, 0x1e    ; the low two bits of the flag halfword at +0x14
0x268c4c: cmpwi  cr7, r0, 3
0x268c50: bne    cr7, 0x268c5c
0x268c54: li     r0, 8
0x268c58: stb    r0, 4(r28)       ; and the function answers 1
```

`0x268C58` sits in `0x268BA8`, the slot's **step**, and its entry shows where the flags come from:
`lhz r11, 0x14(r3)` then `rlwinm r0,r11,0,0x1e,0x1e` — bit `0x2` of the halfword at `+0x14`. The
step is reached from only two call sites, `0x262550` and `0x2625C0`, both inside the peer table's
step, and both after a check that a record has arrived for that peer (`0x262528: bl 0x2666C8`, then
`cmpwi r0,0; beq`). The other two writers are two copies of one block:

```
0x285434: stb  r26, 3(r25) ...    ; the four bytes at +0xca and +0xce cleared
0x285454: lhz  r9, 0x14(r29)
0x285458: li   r0, 8
0x28545c: stw  r28, 0x10(r29)
0x285460: ori  r9, r9, 7          ; +0x14 |= 7 — the same two bits and a third
0x285464: stb  r0, 4(r29)         ; then +4 = 8
```

with `+0x90 = 0x0001F400`, `+0xb0`–`+0xb6` zeroed and the class vtable installed just above it — a
session slot being torn down and left marked, not opened.

**What that means for the screen, and for this host.** The test at `0x8EE38C` reads "is this slot
torn down", and `8` is the answer that tears the client's session down with it: `li r5,2` → kind `2`
(§42), which cannot write `1d` rows or carry the low bit of `0xbcc`. Both routes to `8` are inside the
client — its own step, once the flags at `+0x14` say so, or its own teardown block — and no record
this host writes appears in either. So the sequence §45 offered as **[I]** is now read in its parts:
the value, its writers, and the kind-2 branch it selects. What remains unread is the write that
first puts `+0x14 & 3` to `3` on a live connection; the teardown arms `+0x90` with `0x1F400`
(128000) in the same block, which if it is milliseconds is about two minutes — the order of
`myserver30`'s 151 s of silence, and a hypothesis to check rather than one to assume.

**Still open.** `+0x14`'s bit `0x2` is set by the teardown itself (`|= 7`) and read by the step, so
the step's condition is true for a slot already torn down — which suggests the step's `8` is a
confirmation of a teardown that happened elsewhere, not an independent close. The write that first
sets `+0x14 & 3 == 3` in a live connection is the next thing to read.

## 47. The two writes that make `+0x14 & 3` equal `3` are a send and a comparison

§46 left the write that first sets the flag pair. It is two writes, not one, and each belongs to a
different half of the slot's own activity.

**Bit `0x1` comes from the transport, at `0x267430`.** The block is the slot's send path: it xors the
stream bytes with the values it just read (`lbz r0,0(r21)` … `xor` … `stb`), calls `0xFA4E20`, and on
its answer

```
0x267414: cmpwi  cr7, r3, 0
0x267418: bne    cr7, 0x266918   ; non-zero -> the send did not finish
0x26741c: lhz    r0, 0x14(r25)
0x267428: ori    r0, r0, 9        ; bits 0x8 and 0x1
0x267430: sth    r0, 0x14(r25)
```

So `0x1` is raised where a send has completed — the same block that binds the stream's key bytes.

**Bit `0x2` comes from the slot's own handshake, at `0x268608`.** The block writes the slot's
descriptor bytes to the stream — `addi r4,r1,0x70` with `li r5,1`, then `bl 0x26CC10` per byte — and
then compares:

```
0x2685f4: bl    0x26b778          ; 0x26b778(slot, id) — reads *(slot) and resolves it through 0x261438
0x2685fc: beq   cr3, 0x268610     ; not equal -> no flag
0x268600: lhz   r0, 0x14(r27)
0x268604: ori   r0, r0, 2
0x268608: sth   r0, 0x14(r27)
```

`0x26B778` takes the slot's **first word** (`lwz r3,0(r3)`), resolves it with `0x261438` and compares
the result; equality sets the bit. So `0x2` is the "this slot's descriptor matched the table" mark.

**Together they are `& 3 == 3`, and the next step for that peer writes `8`** (§46, `0x268C58`). Both
halves are the client's: one is its stream's send completing, the other its own lookup comparing
equal. Nothing on the wire from this host appears in either, and the value compared is a local id, not
a received one.

## 48. `0x1F400` is not a timeout, and §46's two-minute hypothesis is withdrawn

§46 noted `0x1F400` (128000) stored at `+0x90` beside the teardown and asked whether it is
milliseconds. The slot's own clock reader settles it in the negative.

**The slot's timers are 16-bit and seeded in milliseconds.** The update block decrements two
halfwords by the clock delta and treats a negative result as expiry:

```
0x263654: bl    0x26c9f8          ; the delta
0x26365c: lhz   r0, 0x50(r31)     ; the countdown
0x263664: subf  r0, r3, r0
0x26366c: sth   r0, 0x50(r31)
0x263670: cmpwi cr7, r9, 0
0x263674: blt   cr7, 0x2638e0     ; expired
```

and the same for `+0x52`. Their seeds in the same function are `0x3E8` (1000), `0x3E80` (16000) and
`0xE10` (3600) — one second, sixteen and 3.6 — so the millisecond scale lives at `+0x50`/`+0x52`, in
halfwords that cannot hold 128000 at all (it truncates to `0x400`).

**`0x1F400` is a word-sized limit, not a timer.** The connect block builds it into `r29`
(`lis 1; ori 0xf400`) in the branch that also stores `+0x50 = 1000` and `+0x88 = 3600`, and the
teardown block writes it to `+0x90`, a word. So it is a capacity/limit carried with the slot, and the
two-minute reading §46 offered as a coincidence is withdrawn: `myserver30`'s 151 s is not this
constant.

## 49. The id the descriptor match compares is a 12-bit index into the session's own table

§47's `0x26B778` resolves the slot's first word through `0x261438`, and that function says exactly
what the id is:

```
0x26143c: rldicl r0, r3, 0x34, 0x34   ; index = word & 0xFFF
0x261444: slwi   r11, r0, 4           ; 16-byte entries
0x26144c: bge    cr7, 0x261460        ; a negative word resolves to nothing
0x261460: lwz    r9,  -0x8000(r30)    ; the module's session wrapper
0x261464: lwz    r0,  0x34(r9)        ; the table base
0x261468: add    r0, r11, r0
0x261470: lwz    r0, 0(r9)            ; the entry's id word
0x261474: cmpw   cr7, r0, r3
0x261478: bne    cr7, 0x261450        ; -> return 0
0x26147c: lbz    r0, 0xa(r9)          ; the entry's state byte
0x261484: cmpwi  cr7, r0, 0
0x261488: bge    cr7, 0x261450        ; not negative -> return 0
```

So the word is a **12-bit index into a 16-byte entry table at `wrapper+0x34`**, on the same wrapper
(`r2-0x7abc` → `-0x8000`) that owns the 24-slot pool at `+0x28` and the `0xbcc` kind word: index in
the low twelve bits, the rest carried along, and resolution requires the entry's id word to match
exactly and its state byte `+0xa` to be negative.

`0x26B778` then copies that entry into a stack descriptor — `0(r31)` → `0x70(r1)`, `4(r31)` →
`0x74(r1)`, byte `0xa`'s low bit → `0x79(r1)`, bytes `0xb`/`0xc` bounded against `0x17` and `0xff` —
and the comparison that matters happens against that copy:

```
0x26ba28: lwz  r3, 8(r28)
0x26ba2c: lwz  r0, 0(r31)
0x26ba30: cmpw cr7, r3, r0        ; the descriptor's id against the entry's
0x26ba34: bne  cr7, 0x26ba78
0x26ba38: lbz  r9, 9(r31)         ; then (byte9 & 5) == 5 and byte9 >= 0
```

So the "descriptor matched the table" flag (§47's `0x2`) is the client finding **its own** session
entry: an id from a local descriptor, matched against a table the client itself owns. No value this
host writes selects which index is looked up, and none can make the entry's state byte negative.

## 50. Silence does not tear the slot down: the timers expire to zero and carry on

§47 left the possibility that a slot is closed because an answer never came. It is not. The two
countdowns the slot keeps are read, decremented and tested, and the expiry branch of each does
nothing but zero itself:

```
0x263674: blt  cr7, 0x2638e0      ; +0x50 expired
...
0x2638e0: li   r0, 0
0x2638e4: sth  r0, 0x50(r31)
0x2638e8: b    0x263678           ; straight on to the next field

0x26368c: blt  cr7, 0x2638d0      ; +0x52 expired
0x2638d0: li   r0, 0
0x2638d4: sth  r0, 0x52(r31)
0x2638d8: b    0x263690
```

No flag is set, no teardown block is called, and the update continues. Both routes to `8` are
completions, not absences: bit `0x1` only after `0xFA4E20` answers (`0x267430`), bit `0x2` only after
the descriptor resolves and compares equal (`0x268608`), and the `8` itself only in the step that
reads the pair (`0x268C58`).

**That rules out the reading this host's stall seemed to invite.** A client that is sent nothing for
two minutes — `myserver30`'s 151 s — does not close its own slot for the silence; the countdowns it
keeps for that purpose reach zero and it keeps waiting. Whether that client's slot reads `8` at all
is therefore a question for its own step, not for the absence of traffic, and no host record is
implicated either way. What remains is the step above these: the one that decides to send, since
that is what raises `0x1` and lets the pair complete.

## 51. `0xFA4E20` is `memcmp`, so bit `0x1` is a verification and §47's "send" is withdrawn

§47 read the block at `0x267430` as the slot's send path and the bit it raises as "the send
finished". The callee says otherwise: `0xFA4E20` is **`memcmp`**.

```
0xfa4e38: ld    r0, 0(r3)
0xfa4e3c: ld    r10, 0(r4)
0xfa4e44: cmpd  cr7, r0, r10
0xfa4e48: bne   cr7, 0xfa4f58      ; differ
...
0xfa4ecc: li    r3, 0              ; equal
0xfa4ed0: blr
0xfa4f58: cmpld cr7, r0, r10       ; the ordered compare decides the sign
0xfa4f60: li    r3, -1
0xfa4f70: li    r3, 1
```

The 8-byte and 4-byte chunk loops, the ordered and unsigned compares and the three returns are the
C library's own. Its callers in the block are the buffer builders — `0xF99B60` to append a field,
`0xF99D28` and `0xF99B00` to patch one — and the bytes it then xors into `0(r21)`, `0(r31)` and
`0(r24)` are the tail of that build. So the block **builds a packet and verifies it against a second
copy**:

```
0x267414: cmpwi cr7, r3, 0
0x267418: bne   cr7, 0x266918      ; the copies differ -> elsewhere
0x267428: ori   r0, r0, 9          ; equal -> the pair's first half is set
0x267430: sth   r0, 0x14(r25)
```

**What that means for the pair.** The two bits that make `+0x14 & 3 == 3` — and so the step's `8` —
are both *checks the client performs on its own material*: the packet it just built against the
expected bytes (`0x1`), and the descriptor id it resolved against its own table (`0x2`). Neither is an
event on the wire, and neither has an absence behind it, which is consistent with §50's reading of the
countdowns: the slot closes on two verified matches, not on a send and not on silence.

**Withdrawn: §47's "the transport send completed" and the question it invited** — the step that decides
to send. Nothing in that block sends. What is left is to read the branch that reaches `0x2672C0`,
where the builder begins, because that names *what* is being verified; the enclosing function is the
large one whose frame is `0x2900` bytes, and its entry is the next thing to read.

## 52. A field-0 record written through a briefing does not answer the wait: it repeats it

**What was changed, and why it was wrong.** §34.4's slower gaps were taken for a briefing host's own
copies of state `0x00` field 0, and `BriefingRoomPublishService` was made to write
`RoundStateBodiesUtils.RoundPhaseBody((byte)roundState.RoundNumber)` every second frame — `0` for a
room with no round open. The first live run after that change does not reach its briefing.

**The measurement.** `myserver31.pcapng` (2026-10-23, `192.168.1.20:5730` joiner ↔
`89.129.16.203:5731` host, key `0x8bcea1e3`, 1360 host frames over 350 s) holds 167 records of
`0x1001`/10 bytes, all of them

```
0b 00 01 00 00 00 00 00 00 00
```

from t+2.2 through the peer's last datagram. The client answers with the same two records it always
does at the end of its wait — `0x0a5c`/1 byte (`01`, its slot) and `0x1ade`/8 bytes (`e0 01 …`, the
map-loaded report) at t+47.86 — and with nothing else:

| record | `myserver30` (before the change) | `myserver31` (after) |
| --- | --- | --- |
| `0x9001`/2, `82 f3` — the lobby-slot echo | t+50.74 | **never** |
| `0x1001`/2, `84 f3` — the occupancy echo | t+50.74 | **never** |
| the client's own operations stream (`0x1ae2`–`0x1ae7`, then the `0x5a..` rows) | t+50.74 onward | **never** |
| the briefing's run (`0x1a54`/90 + the array pair + the two header records) | t+52.80 | **never** |
| the host's relay of the client's own records | from t+52.80 | **never** |

The echo is the entry `MatchStartRunService.OnRoomEntry` waits on (§18), so without it the briefing's
run is never written: that is the client's map without its rule instructions and without the fade
into the briefing, and the host's log says as much — no `asked to be placed in the room` line at all,
then the round watcher waiting for a round that never comes.

**Why repeating the value is worse than never writing it.** The machine at `0x2882B4` reads field 0
and treats `0` as "wait, and ask again after a delay". A single copy is a value the client eventually
times out of; a copy every 2 s re-arms the wait each time it arrives, so the placement machine never
reaches the echo that opens the entry. The reference's briefing writes are the pair at its entry's
confirmation whose **last copy is non-zero** — `00`/`01` in `started-on-briefing`, `01`/`02` in
`joined-when-red-team-then-briefing` (§34.4) — and then nothing until the round's own boundary; it
never leaves the field sitting at `0` for the client to keep reading. What the reference's briefing
host ticks is the count, field 10.

**The fix.** The publication is withdrawn: `BriefingRoomPublishService` no longer writes field 0, and
its partial file with it. The round's own writes — at the open, on its tick, and at the close — are
unchanged, which is the whole of the reference's field-0 corpus. What remains of §34.4 stands; its
last paragraph is corrected there.

## 53. `myserver32`: the revert restores the entry, and the pair's first copy belongs after the report

**What the re-test shows.** `myserver32.pcapng` (2026-10-23, gameplay pinned to `10c6ef5`, `192.168.1.20:5730` joiner ↔ `89.129.16.203:5731` host, key `0xa56f798d`, 196 s) is the run §52's
withdrawal was written for, and it does what that section predicted:

| measurement | `myserver31` (with §34.4's writer) | `myserver32` (after its withdrawal) |
| --- | --- | --- |
| field-0 records (`0b 00 01 …`) | 167, from t+2.2 to the end | **none** |
| client's `82 f3`/`84 f3` echo | never | t+52.605, t+57.181, t+59.679 |
| client's map-loaded report (`0x1ade e1 01`) | t+47.86 | t+57.181 |
| the briefing's run (`0x1a54`/90 + `0f 00` + the array pair + the two headers + the closes) | never | t+60.221, repeated t+62.597 |
| the client's own operations stream (the five `1d` rows and the `22` record) | never | **never** |
| the retry group (`0x1ae2`–`0x1ae7`, `ff 01`) | never | **gone** (present 29× in `myserver30`) |

So the room's rule instructions are drawn, accepted, and the client fades out and reports its map —the entry §52's mechanism blocked — and it then stays in the fade, which is §33's stall and not a new one.

**The fade-in is not a host record, again.** In `mgo2-started-briefing-then-solo-game` the host writes
*nothing* between the client's echo (t+55.312) and its five `1d` rows (t+57.304); in `game2` it writes
the report's answer at t+57.362 and the rows follow at t+59.040; in `failed-memoryviolation` the run
goes at t+60.871 and the rows at t+61.694. The rows are the client's own stream opening, and §32.1
measures them 1.7 s to 424 s behind an echo in the seven captures that write them. `myserver32` is an
eighth capture that does not, with the join burst, the run and the tick now matching the references
family for family (§33's join table re-measured, §1's run and §30.1's tick).

**The one ordering the references never show.** In both working single-join captures the client
reports its map **first** and echoes the lobby-slot pair **second** — `started-briefing-then-solo`
t+54.918 → t+55.312 (0.39 s), `started-on-briefing` t+55.838 → t+56.360 (0.52 s) — and in this host's
live sessions the order is inverted: the echo at t+52.605 precedes the report at t+57.181 by 4.6 s,
and the client echoes twice more after it. The echo is the client's own request to be placed, and it
is drawn by the pair: this host publishes `85`/`86` from t+2.2 and every 13 s (`ArrayPairFrameInterval`),
where **every reference host writes the pair for the first time inside its run**, after the report —

| capture | client's report | first `85`/`86` pair | where |
| --- | --- | --- | --- |
| `started-briefing-then-solo-game` | t+54.918 | t+57.626 | inside the briefing's run |
| `started-on-briefing` | t+55.838 | t+58.255 | inside the briefing's run |
| `game2` | t+56.7 | t+62.181 | inside the briefing's run |
| `failed-memoryviolation` | t+59.988 | t+60.871 | inside the briefing's run |
| `myserver32` | t+57.181 | **t+2.216** | **before any report** |

and none of the four writes one in the twenty seconds before its entry.

**What was changed.** `BriefingRoomPublishService` publishes the pair from the pass that hears the
briefing's first report — the arming `BriefingCountdownService.Begin`, which
`MatchStartRunService.BeginBriefingClock` does on that report — and every
`ArrayPairFrameInterval` passes after it, which is the reference's own shape: a burst at the report
followed by copies through the briefing. Nothing else of the publication moves, and the pair the run
carries is unchanged.

**What would confirm it.** The client's echo should follow its map-loaded report by roughly half a
second, as both single-join references do, its run should follow the echo by the two-second dwell, and
the five `1d` rows should then open its operations stream. **What would falsify it** is the same
silence with the order restored: that would leave the state the client holds from the join's own
records as the remaining candidate, which is where §33 and §42 stopped.

## 54. `myserver34`: the entry order is restored, the run is answered, and the fade does not move

**What was tested.** §53 moved the lobby-slot pair's first copy from the join to the pass that hears the
briefing's first report, so that a client's map-loaded report precedes its `82`/`84` echo. `myserver34`
(2026-10-23, `192.168.1.20:5730` joiner ↔ `89.129.16.203:5731` host, 2912 packets over 328.8 s; the
session's last client datagram is at t+157.0) is the first run on that build, and the ordering §53
predicted is on the wire.

| measurement | `myserver32` (pair from the join) | `myserver33` (pair from the join) | `myserver34` (pair after the report) |
| --- | ---: | ---: | ---: |
| client's map-loaded report | 57.181 | 58.955 | **55.439** |
| client's `82 f3`/`84 f3` echo | 52.605 (**4.58 s before**) | 54.252 (**4.70 s before**) | **56.785 (1.35 s after)** |
| the pair's first copy | t+2.2, repeated | t+2.2, repeated | **56.260 (+0.82 s)** |
| the briefing's run | 60.221 | 61.502 | **58.894 (+2.11 s after the echo)** |
| the client's own operations stream | never | never | **never** |
| the client's `1ae2`…`1ae9` group | never | never | **11 copies from 55.439** |
| the client's `0b0c` heartbeat | never | never | **10 copies from 56.785** |

**The briefing's own records are the references', and the client acknowledges them.** The run at
t+58.894 carries `0x1a54`/90 (`00 00 00 ff …`), `0x1a54`/2 (`0f 00`), the array pair
`85 f3 fe 00 ff …` / `86 f3 7f 07 7f …`, the close triple (`20`, `20`, `7`) and the count
`0b 3c 00 00 00 00 00`; the repeat at t+61.154 carries the same without the echo answers. The client
returns `0x5a50` (72), `0x5a5a` (72) and `0x5a54` (45) over the next 95 s — it is parsing the item-box
groups and the count — and never writes a `1d` row or a `1f` ready.

**The run's instant is inside the measured dwell.** It follows the echo by 2.11 s, against 1.90 s in
`mgo2-game-started-on-briefing` and 2.31 s in `mgo2-started-briefing-then-solo-game`. So the copy of §53
that could have been wrong — the run arriving outside the window the client tolerates — is not what this
capture shows.

**The one record no reference writes before the echo is the pair.** Both reference briefings write their
first `85`/`86` copy *inside the run*, 1.90 s and 2.31 s after the echo (`started-on-briefing` t+58.255,
`started-briefing-then-solo-game` t+57.626), and neither writes one in the twenty seconds before it. This
host's copy at t+56.260 is 0.53 s before the echo, and the client answers it — the echo follows 0.53 s
later, which is the first capture of this host in which the report precedes the echo. Whether the pair's
presence is what drew the echo cannot be told from this capture alone: in `myserver32` and `myserver33`
the pair had been circulating for fifty seconds before the client echoed, and in the references the echo
precedes any pair at all.

**The client's own state moved, and not towards the screen.** The eight-record group `1ae2`…`1ae9`
(`ff 01`, one record per vid, all under class `0x00`) rides its ~6.7 s heartbeat from the report onward,
and every copy is answered — `0x5ae2`…`0x5ae9` empty, `0xdae9` one byte `24` — so the group is not an
unanswered retry in the sense §30.13 measured. It is the same shape `myserver30` carried (there as
`1acd` + `1ae2`…`1ae7`), and `myserver32`/`myserver33` carry none of it. The `0b0c` heartbeat (17 bytes,
`b4 00 00 00` then a fixed-point stamp climbing by `0x2ccd1100` a copy) appears with it and in no
reference capture. Both are the client's own statements; nothing in the room's stream is missing that
they could be asking for, family for family.

**What the room never does is end its briefing.** The count is `0b 3c 00 00 00 00 00` from t+58.894 to
the client's last datagram at t+157.0 — 98 s of a one-minute briefing, held at the room's whole time.
That is `BriefingCountdownService.Remaining`'s held value, which is returned while `countingFrom` is null,
and `countingFrom` is set by `MatchStartRunService.ArmBriefingDeadline`, which `OnJoinReport` calls only
when `briefingStates.JoinedCount >= CountingJoinReportCount` (2). `JoinedCount` counts *peers that
reported*; the host's own player is not one of them, so a room holding the host's character and one joiner
— the population the join's own ten-byte control states as `02`, and the two loadouts the burst writes —
has exactly one report and never arms a deadline. The round therefore opens only on the client's `1f`
ready, which never comes, and the briefing's own time never runs out.

**The memory dump does not hold the object the question is about.** `rpcs3.dmp` is a Windows minidump
(`MDMP`, 17 streams, a `Memory64List` of 6 034 ranges, 4.49 GB of data, written 2026-10-23 12:18:13, 49 s
after the session's last datagram). The guest's RAM is mapped at `0x300000000` — the ELF header at
`0x300010000`, entry `0x11E7368` — but the dump holds only the first 32.2 MB of it, the loaded image and
its `.data`/`.bss` (`0x300010000`–`0x301ED0000`). The next region, `0x301ED0000`–`0x310000000` (236 MB),
is `MEM_COMMIT` with `PAGE_NOACCESS` in the dump's `MemoryInfoList` (state `0x1000`, protect `0x1`) and
was skipped by the writer; that is the guest heap, and the session object, the screen object and the
state store all live in it. The `0x400010000` range that also begins with an ELF header is the loader's
file image, not guest memory. So the dump can say the image is intact — the screen class's vtable at
`0x11FEB80` is present with its `(entry, TOC 0x1222A00)` pairs — but not what step the client's screen
holds. No live instance of any of the seven screen classes appears in the dumped pages, which is expected
of objects in the missing heap and is therefore not evidence either way.

**What the next test has to separate.** With the ordering settled, the remaining candidates are the
client's state from the join (§33, §42) and the room's own timer:

1. If the client is on the briefing screen with a stopped `1:00`, the held count is the defect, and a
   room whose population is the host plus one joiner should arm its deadline at that joiner's report.
   The falsifier is the same join with the count moving: the client should reach its `1f` ready (or the
   round should open when the room's briefing time is out) and the run's entry should place it.
2. If the client is still in the fade with no screen, the timer is not the gate, and the pair's copy
   before the echo is the only remaining host-side difference: the next run should write its first pair
   inside the run, where both references do.

## 55. The dump in this pair *does* hold the client's heap, and it holds no screen at all

§54 read `rpcs3.dmp` as an image-only dump and could not answer which screen the client was in. That
reading is wrong for the file beside `myserver34`, and this section corrects it, then uses what the heap
says.

**What the file is.** `MDMP`, 17 streams, a `Memory64List` (stream type 9, file offset `0x22A4A1`) of
**6 034 ranges holding 4.49 GB**, and **444 of those ranges are the guest's memory**, mapped at
`guest + 0x300000000` as `docs/analysis/udp-record-dispatch.md` §1 reads them. The writer did not skip
the heap: `0x50000000`–`0x50100000`, the `0x54xxxxxx` aliases and the `0xD0xxxxxx` upper copy are all
present with live data. §54's "the dump holds only the first 32.2 MB" is true of the 8.5 GB dump that
document was written from and not of this one; the `MEM_INFO` entry it read as `PAGE_NOACCESS` describes
a reservation beside the committed pages, not their absence from the file. `tools/mgo2_dump_read.py`
reads and searches the file by that one constant.

**The client is in our room and has read the run.** The P2P module's instance is `0x011A6308` →
**`0x01856C10`**, module state `2`, its session array at **`0x50000060`**, and slot 0 carries the endpoint
`59 81 10 cb` (`89.129.16.203`) on port `16 63` (`5731`) — the capture's host. The received-record ring
at **`0x500E2xxx`** holds the run's own records: the lobby-slot pair `85 f3 fe 00 ff ff …` at
`0x500E23F8`, the countdown `0b 3c 00 00 00 00 00` at `0x500E246C` and `0x500E2480`, and the rules
records beside them. So the records that §54 measured on the wire were all read and kept.

**And no screen object of the join family exists.** The seven sibling vtables §32.3 names —
`0x11FEB80`, `0x11FFC00`, `0x11FFC70`, `0x11FFF40`, `0x1200048`, `0x1200090`, `0x12000E8` — are each
referenced from their own class record in the read-only data (`0x123E2BC` onward, stride `0x1C`) and,
outside it, only at unaligned addresses inside other data. No object base in the guest's memory carries
one — and of those unaligned occurrences, none has a plausible step at `+0xb8` with an empty `+0xbc` —
so no live object carries a step to read, and the answering of §31.4's first open item ("which step the joining screen holds")
is that there is no such screen to hold a step: the client never built one. The module's own stream
objects are live and do carry their vtables (`0x551C6A60` → `0x011953A8`, `0x551C711C`/`0x551C7130`/
`0x551C7144` → `0x01195448`, `0x554C6918` → `0x01195888`), so the scan does find live objects where
they exist; the screen family's absence is a read and not a failure of the scan.

**What was changed.** §54's branch 2 is applied: the room's withdrawal of the pair's first copy. The pair
is no longer written on the pass that hears the briefing's first report; it waits for
`MatchStartRunService.IsEntryAnswerDue` — a briefing generation, the peer's own entry, and the run's
`EntryRunDwellMilliseconds` behind it — which is the instant both references write it *inside* the run.

| file | change |
| --- | --- |
| `MatchRecords/MatchStartRunService.Entries.cs` | new `IsEntryAnswerDue`, the run's own three preconditions read from outside the service |
| `MatchRecords/BriefingRoomPublishService.cs` | the pair's first copy is gated on it instead of on `BriefingCountdownService.Remaining`; the countdown service is no longer a dependency |

**The countdown's set was checked against the captures and is right.** Our host holds `0b 3c 00 00 00 00 00`
— sixty seconds and an empty set — which §54 recorded as a candidate defect. It is not one: the reference
briefings open the same way. `mgo2-game-started-on-briefing`'s first countdown is `60` with set `0x0000`
at t+93.127 and holds that set for 37 s past the client's report (t+55.838) and the host's `e0 01`
answers to it; the set becomes `0x0002` only at t+119.874, **4.10 s after the client's `1f 01 01` at
t+115.771**, and `0x0006` at t+149.860 adds a second bit. `mgo2-game2` is the shorter version of the same
record: `secs=60 set=0x0000` at t+78.987, then `secs=0 set=0x0002` at t+104.649, **1.46 s after its
`1f 01 01` at t+103.190**. So a bit is the slot a `1f` names and nothing else — not the reports, which
arrive 40 s earlier in both — and an empty set on a room whose only client has not readied is the
reference's own opening state. What `mgo2-game-joined-when-red-team-then-briefing` adds is the bits the
host itself fills: `0x0040` and `0x00c0` at t+72.560, before any `1f` in the capture at all, which are the
host's own bots readied on creation — a room of ours holds no bots and seeds no bit, so `00` is what it
should write. Nothing in this record is changed.

**Not verified live.** The build is clean (0 warnings, 0 errors) and the 58 codec tests pass; no client has
joined the built host. What would confirm it is the same join: the pair should appear 0.5 s after the
echo instead of 0.5 s before it, on the run's own pass. What it would not settle is the fade itself —
with the heap now readable, the next dump taken at the stall can be asked the one question this one
answers negatively: whether *any* screen object exists, and if one does, which step its `+0xb8` holds.

## 56. The `0xd001` byte is the answering host's phase, and this host wrote the joining phase for ever

§30.5 and §30.7 stopped at a byte whose meaning the image could not name, and §27.2 closed the
record with "the constant stays". This section reads the same byte from the other end — when a
value changes in the captures, and where the binary puts it — and it separates into three phases
that the captures hold without contradiction. Only the *place* of a value is general; the value
itself is the host's own, so the three phases are what this host can be made to follow.

**What the descriptor says.** State `0x00`'s table is `0x01229B80`, 55 entries of ten bytes, and
entry 24 is `off 99, size 1, mult 1, flags 0x0000` — the smallest entry in the table: the fields
around it carry `0x0037`. Five fields of the 55 are unflagged (offsets 84, 86, 89, 99, 165) and
field 24 is one, which is why `0x27E578` marks nothing for it and no second field is written with
it. So the byte is a leaf of the session note and nothing in the state is derived from it.

**What steps.** Host side, one byte, aligned against each capture's own run markers:

| capture | join pass | briefing | the run's pass | round |
| --- | --- | --- | --- | --- |
| `mgo2-game3` | `24`, t+8.595 | `03`, t+10.967 to t+108.6 (nine answers) | **`24`**, t+59.596 beside the `0x5ade` marker | `07` then `04` from t+118.218, the pass its own run opens in |
| `mgo2-game1` | `24`, t+6.687 | `02`/`03` alternating, t+9.102 to t+78.298 | **`25`** at t+194.042 (first `0x52de`), `04` at t+196.417 (the second) | `04`-`06` from t+88.035 |
| `started-on-briefing` | `0c`, t+8.125 | `0a`, `03`, `0a`, `10`, `0b`, `10`, `02`, `08`, `0b`, `01`, `04`, `05` — a value per answer, no phase band | `05` at t+56.123 beside its `0x52de` | — |

So the byte is **not** a function of the record answered: it holds `03` over nine answers of
`mgo2-game3` and then moves in one pass to `04`, where the answer's own identifier, length and
class are identical throughout. It is the answering host's own phase, it changes when that host's
screen changes, and the two hosts that brief step at the same two moments — the briefing's run and
the round's opening run. `started-on-briefing` is the widening case: six players, a value per
answer, the same band (`01`-`0f`, `10`, `12`) and the same `0x0x`-`0x1x` shape, which is why no
derivation of the value is attempted here and only the phase is.

**What this host did.** `PeerAcknowledgementUtils.HostControlByte` (`0x24`) was written on every
`0xd001` this host answers, for the whole session — the value a reference host writes in its
joining pass and in the pass that writes its run. A room that had briefed, and a room playing a
round, were both told the value that says "you have just been handed a run". The byte reaches one
screen step (`0x77E668`: step at most 9 to 12, once) and one script event (`0x16002C`), so nothing
in the C++ path changed with it — but the argument the script was handed every four seconds named
a pass that was forty seconds old.

**What was changed.** The answer's byte is the phase now, read once per pass so two answers
written together cannot disagree:

| phase | byte | source |
| --- | --- | --- |
| a round is playing | `0x04` | `MatchRoundStateService.IsOpen` |
| a briefing has begun, no round | `0x03` | `MatchStartRunService.BriefingGeneration` |
| a room that has not briefed (the join pass) | `0x24` | unchanged |

| file | change |
| --- | --- |
| `PeerAcknowledgementUtils.cs` | `BriefingControlByte` and `RoundControlByte` added, `HostControlByte` keeps the joining pass, `AcknowledgementOf` takes the byte |
| `GameplayServerService.Answers.cs` | `ControlByteForAnswers`, computed once in `AnswerReliableRecords` |
| `GameplayServerService.cs` | the round's state is resolved beside the other services |

**Not verified live.** The build is clean (0 warnings, 0 errors) and the 58 codec tests pass; no
client has joined the built host. What would confirm it is the same join with a briefing and then
a round: the `0xd001` answers should read `24` while the room waits, `03` from the first report,
and `04` from the round's opening run — three byte sets on one wire where this host wrote one. What
it would not settle is what the script does with each, which stays [U]; the next dump taken at the
stall can be asked the one thing this reading leaves open: whether the screen's step moved to 12
on the new values or not at all.

## 57. `0x16002C` has no handler to name: nothing in the image and nothing in the dump binds it

§56 named the phases the byte holds but not the values, on the reading (§41) that the listener for
`0x16002C` is a script callback registered at run time. This section asks the question that
reading left — *which* handler, and what each value means to it — and the answer is that neither
the image nor this dump can produce one. The negative is worth recording with its evidence,
because it also removes the byte from the stall's causes.

**Nothing in the image can be its handler.** The identifier occurs **zero** times as a 32-bit or
64-bit word anywhere in `MGO2.ELF`'s two loadable segments, and as an instruction operand only at
its two emitters (`0x77E6C8`, `0x77F8A4`). A static table of `{id, handler}` cannot therefore hold
it, and the family's own dispatcher does not either: the compare chain at `0xB32C68` — the one
that owns the `0x1600xx` family, in a binary search over `0x160002`, `004`, `008`, `00a`, `00e`,
`00f`, `010`, `012`, `01d`, `01f`, `021`, `022`, `024`, `025`, `027`, `029`, `02b`, `02d`, `02e` —
skips `0x16002C`. So the id is neither a case of the script engine's chain nor an entry of any
data table, which leaves exactly one registration path: the runtime listener list.

**Nothing in the dump holds it.** The bus the emitter walks is `0xC1208`, whose list sits at the
object's `+0x34` and whose entries are twelve bytes — `{id, descriptor, argument}`, with the
descriptor holding `{code, toc}`. Over every guest range in `rpcs3.dmp` (`0x30000000`–`0x60000000`)
there is **no entry whose id falls in `0x00150000`–`0x0016ffff`**, and with the descriptor test
tightened to a code pointer below `0x01190000` and a TOC inside `0x01190000`–`0x012ac000` there is
no entry at all. §55 explains it: the client built no screen of the join family, and the screen is
what registers. A session with no screen has no listener, so the byte this host wrote every four
seconds arrived at a list that had nothing to match it.

**And there is no game data to read either.** The install this dump came from holds one executable
and nothing else: `dev_hdd0/game/NPMG00010/USRDIR/` is `EBOOT.BIN` alone, and `dev_bdvd/` is empty
(`RPCS3.log`, the paths at `0:00:10.98` and `0:00:11.02`). So the "script" the §41 reading points
at is not a file on this machine; there is no archive to search for the handler, and no name to be
found for a value.

**What that means for the stall.** Both effects the byte can have need the screen: the step move
(`0x77E668`, step at most 9 → 12) is a screen method, and the script event is a listener the same
screen registers. With no screen there is neither, which is consistent with §54's live run: the
client stored the byte (§30.5's field 24) and did nothing with it. So §56's phase change is a wire
correction and not a lever, and the fade has to be explained by something that does not pass
through this record.

**The one experiment that would name the values.** Take a dump while the client is **on a screen**
— an ordinary join that reaches the briefing, not the stall — and read two things:
`*(0x0121af44)` → `0x011a6308` → `*(…-0x8000)` for the module table, then the bus the emitter uses,
`*(module+8)`; its `+0x34` list's entry for `0x16002C` is the handler's descriptor, and the
function at that descriptor is the consumer. Its first comparison against the event argument names
the value set, and a dump of the *same* client on a different screen says whether the handler
changes with the screen. Until such a dump exists the values stay nameless, and this document does
not guess at them.

**The twin does say what the byte is, even though it cannot name each value.** The same code is
also raised as `0x15002C`, from three sites that are not a session's field at all: `0x79D288`,
`0x7A0A00` and `0x7A0D7C`. The two `0x7A0...` sites read the byte **out of a received record**
(`0x26CC88`); the `0x79D...` one takes it as its argument and stores it. All three emit
`0x15002C` with it, and then test `0x7504F8()` against `0xE`. `0x79D288`'s enclosing function
starts at `0x79D218`, which is entry 19 — offset `+0x98` — of the function table at `0x011FF600`,
a sibling of the seven screen tables §55 lists (the same `{code, toc}` eight-byte entries — the
join screen's `0x011FEB80` carries `0x77F7E0` at slot 44, which is §40's step-12 setter), and its
first instructions index `this + 0x14 + index·4`
for a per-slot pointer and skip the slot when that pointer is null. So the family is a screen's
**per-slot** method: the byte is a peer slot's **state code**, arriving either in a record (the
`0x15002C` sites) or in the session's own copy of it (the `0x16002C` sites). The neighbouring slot
code reads a slot object's byte at `+0x20` and compares it with `0x17` — the state space is a small
enumeration, not a set of flags — but the image's only use of it is that single `> 0x17` test, so
the enumeration's names are still not in the image and are still [U] here.

That reading is consistent with every measurement §56 records, including the ones that looked
arbitrary: two hosts write one byte that holds for minutes and steps where their screens step
(`03` through a briefing, `04` in a round), and the host whose room churns writes values in strides
of four because its states are spaced that way. It is still not a name for `0x02` or for `0x2c`,
and this document does not invent one.

## 58. The value's alphabet is the whole code space; its names are not in any of the three artifacts

§57 left the values unnamed and asked what each artifact could add. This section answers the
narrower question — *can the possible values be enumerated?* — by taking the same measurement over
every capture in the repository rather than one at a time, and by asking the image and the dump
what they bound rather than what they name.

**The corpus is now counted whole, and both peers write the record.** `0xd001`, body length 1, all
twelve captures: 1157 copies, 60 distinct bytes — 1123 written by the host and **34 written by the
client**, which answers the host's own `0x9001` requests with the same record and a byte of its own.
That second side matters: the client writes from the same code space, so the field is a state the
two peers exchange rather than a value only a host invents.

| capture | host | client | distinct | values (both peers) |
| --- | ---: | ---: | ---: | --- |
| `joined-final-round-solo-then-final-round` | 196 | 4 | 12 | `02`–`06`, `08`, `12`, `22`, `24`, `25`, `39`, `3a` |
| `game-failed-memoryviolation` | 20 | 0 | 5 | `00`, `04`, `08`, `0c`, `10` |
| `joined-when-red-team-then-briefing` | 196 | 0 | 19 | `00`–`14` in fours, then `1c`, `20`, `2c`, `34`, `58`, `78`, `7c`, `84`, `8c`, `a4`, `b4`, `bc`, `c8` |
| `started-on-briefing` | 71 | 0 | 18 | `01`–`10`, `12`, `9a` |
| `started-team-selection-selected-blue` | 68 | 1 | 11 | `02`–`05`, `12`–`16`, `22`, `34` |
| `game1` | 178 | 5 | 18 | `01`–`06`, `08`, `09`, `10`, `23`–`28`, `49`, `69`, **`ff`** |
| `game2` | 86 | 0 | 18 | `01`–`11`, `13` |
| `game3` | 57 | 2 | 5 | `03`, `04`, `07`, `22`, `24` |
| `joined-game-in-progress-last-round` | 86 | 15 | 20 | `02`–`09`, `0f`, `11`, `1d`, `22`, `27`, `28`, `2a`, `2e`, `3b`, `46`–`48` |
| `started-briefing-then-solo-game` | 81 | 2 | 7 | `02`, `03`, `04`, `06`, `2d`, `39`, `45` |
| `started-on-solo-game` | 43 | 2 | 10 | `01`–`03`, `10`, `12`–`14`, `24`, `34`, `3a` |
| `myserver34` (ours) | 41 | 3 | 6 | `02`, `03`, `1e`, `1f`, `22`, `24` |

**Split into five bits and three, the two sides together fill the code space.** Read `v & 0x1f` as
the code and `v & 0xe0` as flags. The host alone writes 26 of the 32 codes (`0x17`, `0x19`, `0x1b`,
`0x1d`, `0x1e`, `0x1f` absent); the client alone writes 17, and they are almost disjoint — its 34
copies carry `0x19`, `0x1b`, `0x1d`, `0x1e` and `0x1f`, which the host never writes. Over both,
**31 of the 32 codes appear and only `0x17` is unseen**, and the flag bits are set on 107 copies in
**all seven** combinations (`0x20` 87, `0x40` 9, `0x60` 3, `0x80` 3, `0xa0` 3, `0xc0` 1, `0xe0` 1,
the last from the client). That is why §30.5, reading one capture at a time, called the high bits
"a minority variant": within a session they are rare, across the corpus they are every value, and
the codes a host does not use are the ones its peer does. So the observed alphabet is the full code
range `0`–`31` with an independent flag byte beside it, and §57's `> 0x17` test bounds a range
inside that code, not the whole field.

**`0xff` is on the wire once, from the client, and is skipped.** `game1` carries a single `ff` and it
is one of the five copies that session's client wrote; both readers test `cmpwi r5, 0xff` and return
before the event (`0x77E6C0`, `0x77F890`), so that copy is inert and cannot be named by what it
moves. It also corrects §30.7's "no reference capture ever sends it". Two readings survive — a peer
writing the reserved value, or all three flags over code 31 — and the corpus does not choose.

**What each artifact can and cannot add.**

* *The captures* give exactly the set above: what the two peers wrote, 1157 copies over twelve
  sessions of briefing, round and run. They cannot give a value neither side has written.
* *The image* gives bounds and no names. The field is one byte (the readers zero-extend with
  `clrldi r5, r5, 0x38`), it is masked nowhere, it is compared only with `0xff` and guarded by the
  screen step, no table is indexed by it, and `0x7770F0` forwards it verbatim. So `0x00`–`0xfe` are
  all representable and the binary names none of them. The identifier itself is built by exactly
  two instructions in the whole file — `lis r4, 0x16` at `0x77E6C8` and `0x77F8A4` with
  `ori r4, r4, 0x2c` behind it — which confirms §57's pair and rules out a third emitter.
* *The dump* gives one session's stored byte: field 24 of state `0x00`, the `0x63` the record
  handler writes. That is a single value already present in that session's capture, so it adds
  nothing to the set. It also has to be re-read before it is cited: the file now beside
  `myserver34` is **not** the session §55 describes — `0x500E23F8` is zeroed and the module's data
  sits elsewhere — so §55's addresses belong to the previous dump.

**So the possible set cannot be closed from these three, and one artifact would close it.** The
writer is the reference host, and the reader is a script callback the client registers only while a
screen of the join family exists (§57). The enumeration therefore lives in the reference server's
own state table, or in the script that registers the listener — neither of which is on this machine.
What is worth taking to that source is small and exact: **32 codes, `0x17` still unseen, three flag
bits that combine freely, and `0xff` reserved.** A scoreboard of names against that is the whole of
the remaining gap; this document does not guess at any of them.

## 59. The stage data has been searched: it names the screen, not the codes

§57 said there was no game data on this machine. There is, and this section records what the search
of it found, so the same ground is not walked twice: the install's stage tree at
`MGO2RE/Game files/o` holds **641 files, 2.16 GB** in **42 stage directories** — 19 under `stage/`
and 23 patch copies under `dl/p/stage/` — and it holds no handler for `0x16002C` and no name for any
of the values.

**What is in it.** The extensions are the data-side ones only: 228 `.dlz`, 102 `.dar`, 56 `.dci`,
44 `.qar`, 44 `.gcx`, 43 `.vfp`, 43 `.cnf`, 29 `.geom`, 20 `.ptl`, 17 `.nni`, 10 `.dld`, 4 `.ssp`
and one `.gsp`. There is no executable here — `EBOOT.BIN` sits beside this tree, not in it — and the
`.gcx` files are **scenario scripts with a multilingual string table**, not a table of event ids:
`stage/lobby/scenerio.gcx` unrolls to 30 157 printable runs, among them the room's own labels
(`WAITING ROOM`, `LEADER OPTIONS`, `MEMBER OPTIONS`, `Team Standby`, `HOST`, `HOST NAME`) and the
one line that describes the screen the byte drives:

```
Number of Players Currently Joined: %d / %d
```

So the data names the *screen* and its vocabulary, and nothing in it names a code.

**The identifier is not in it, and the first reading that said it was is wrong.** Scanning all 641
files for `0x0016002C` and `0x0015002C`, both endiannesses and as ASCII, produces hits in 40, 36, 24
and 42 files — every one of them a coincidence of **16-bit** data. The contexts show it: in
`stage/lobby/cache.dar` the match is `00 16 00 2c` between `00 11 ff ff` and `00 19 00 17`, a run of
16-bit values; in `n001a.ptl` it is one pair among `00 45`, `00 46`, `00 45`, `00 2d`. The largest
"table" the scan reports — 3192 values from `0x00150000` upward in `r_sna01_n/resident.dar` — is the
same artifact at scale: a stream of 16-bit words whose first is `0x0015`, read as the high half of a
word it never is. Nothing in the tree builds `0x0016002C`, which agrees with the image: the id is
formed only at its two emitters, so a script cannot name it by value.

**The archives hold layouts, not bindings.** A `.dar` opens with a count and a name table, and the
names it lists are the screen's own assets: `stage/lobby/cache.dar` carries 121 `.la2` entries —
the layout files `album_name.la2`, `brghtadjst_online.la2` and their siblings — beside 84 `.cpef`,
58 `.mdn` and 50 `.img` resources. So the join screen's *appearance* is data in this tree and its
*behaviour* is not: a `.la2` places an element, and no layout in the tree references the event id or
names a slot state, so the enum cannot be recovered from the layouts either.

**And the one reader that looked like the answer is a different structure.** The image has, besides
the two screen readers §30.7 counts, a pair at `0x1197D4` and `0x1384F8` that take a byte at
`+0x63`, test it against `0xff` and index a table of `0x50`-byte entries:

```
0x1197d4: lbz   r9, 0x63(r22)
0x1197d8: cmpwi cr7, r9, 0xff
0x1197dc: beq   cr7, 0x119ebc
0x1197e0: slwi  r11, r9, 6      ; v*64
0x1197e4: slwi  r9, r9, 4       ; v*16
0x1197e8: add   r9, r9, r11     ; v*80, the entry stride
```

Each entry carries **three string references** (at `+0x20`, `+0x30`, `+0x40`, each a pointer and a
length), and the offsets are not a lone byte: the same block reads `0x61` and `0x62` the same way, so
the object holds three consecutive indices with `0xff` as the empty one — a profile-style slot triple
with a name, a short name and a description per entry, not this record's single field in state
`0x00`. It is the place a `+0x63` byte with an `0xff` sentinel gets mistaken for it, and it is worth
recording as such.

**What that leaves.** The codes are named where the listener is registered, and that registration is
still a run-time act of the client's screen (§57). The stage data would name them only if a scenario
script carried the id, and it does not; the reference host's state table remains the one source that
can. What this search removes is the assumption that the data was merely missing.

## 60. The briefing's run is bounded by a grace, so a silent echo cannot hold it for ever

**What the live session of 2026-10-29 adds.** A client loaded the round's map (`n008a`
in its own log, 135.5 s in), sat on a black screen where the briefing's fade belongs, and
63 ms after the stage started failed two of its own supervisor checks —
`sv_custom_point.cc` line 94 (*over action 14*) and the script VM's `parse.c` line 301.
The checks come from the client, but the state they are run against is this host's, and
the shape of the session is the one the record gate produces: the briefing's run is
written only for a peer whose lobby-slot echo this host has read
(`MatchStartRunService.OnRoomEntry`), so a peer whose echo is not read as `82 NN`/`84 NN`
gets **no run at all** — no room state, no briefing, nothing until the briefing's deadline
opens a round over it, which is the map load and the black screen.

**The gate was measured on one client's shape and is now bounded.** Both reference clients
echo 0.39 s and 0.52 s after their report (`started-briefing-then-solo` and
`started-on-briefing`), so the grace is never reached on that shape and the run's instant
is unchanged. What it removes is the case where the echo never arrives, or arrives in a
shape `TeamArrayFramesUtils.IsArrayEcho` does not read: after three seconds
(`MatchStartRunService.EntryEchoFallbackMilliseconds`) the run is written anyway, opening
on the recorded empty `0x5001` heads because no entry answer was held, and the room's
first `85`/`86` pair follows it (`IsEntryAnswerDue` reads the same grace). The failure is
logged once per peer, so a host that never hears the echo says so instead of going quiet.

**What would confirm it** is the same join: the briefing's run within a few seconds of the
report even with no echo on the wire, the client's own `1d` rows behind it, and a briefing
screen instead of the fade. What would falsify it is the same black screen with the run
already on the wire — that leaves the run's contents, which §33, §34 and §54 have already
taken as far as the captures go, rather than its gate.

## 61. The `action 14` of the supervisor's check is a literal in the stage's own script

**What the check reads.** `sv_custom_point.cc` line 94 is the function at
`system.dll:0x10178570`: it takes the GCL globals `0x6bb316` (*action*, default `-1`) and
`0x39323b` (*custom point*, default `0`) with the engine's `GetGlobal(name, default)`
(`0x10065f70`), bounds the first at `cmp edi, 0xe` (`0x10178590`), bounds the second at
`cmp ebx, 0xff`, and writes `byte[action]` of a table at `0x318654a8`. The getter beside it
(`0x10178540`) and the same file's other writer read the same table under the same bound, so
the table is **14 entries, `0` to `13`**, and `14` is one past its end. Both globals hold
values a **script** has set: `GetGlobal` reads the GCL variable registry and returns the
caller's default only when the name is absent, and the reported value is `14` and not `-1`,
so the global exists and holds `14`.

**Where the `14` is written.** Every game stage's `scenerio.gcx` carries 17 sets of the pair
(the game stages `n001a` … `n024a`, `sm_dd` and `sm_ll`; `init_n`, `nt_mgsetup`, `lobby`,
`mga_boot` and `r_sna01_n` carry none, which is why no check fires at boot). In `n008a` they
sit at `0x15b7e2` … `0x15b9e6`, and each reads

```
6d 15  c9 2b 08 04 06 | f0 e7 af | 55 61 16 b3 6b <action> 55 70 3b 32 39 <point> 00
                 ^ 0xafe7f0 (the setter itself, registered at 0x110de05c)  ^ 0x6bb316  ^ 0x39323b
```

a 21-byte record with the setter's name id, both argument name ids, and two bytes that are
the arguments. The pairs, in file order, are `(15,5)`, `(1,13)`, `(1,5)`, `(2,4)`, `(3,2)`,
`(4,4)`, `(5,5)`, `(6,5)`, `(7,1)`, `(8,16)`, `(11,1)`, `(12,1)`, `(13,1)`, `(11,4)`,
`(12,3)`, `(13,2)`, `(14,2)`. The last one carries `14`, which is what the client printed,
and it is one of the same 21 records in every map, so `14` is not map data: it is the disc's
own table, the same in `n008a` as in `n001a`.

**The records are not applied in file order.** The first record carries `15`, which is past
the same bound and would report `over action 15` before anything else; the client reported
`14` once and nothing else. So the client selects among these records — and the four runs
(`15`; `1` … `8`, with `1` set twice; `11` … `13`; `11`, `12`, `13`, `14`) are the choices it
selects between.

**Why no record of this host carries the number.** The value is compiled into the script, not
carried: the gameplay channel has no field that writes a GCL global (§34), and neither the
port's `savemgo.dll` nor `mgo2.exe` mentions either name id at all — the only writer of
`0x6bb316` in the whole client is the stage script's own table. The check also fires 63 ms
after `Stage n008a` starts, during the stage's own init, before the briefing's records are
read.

**The rule is the one input of this host that can choose a run.** The client does not read the
stage's rule out of the script: its engine holds a **named dispatch per rule** and calls the
stage's function for it. The names are in `system.dll` — `Rule_Eng_COOP`, `Rule_Eng_TSNE`,
`Rule_Eng_BASE`, `Rule_Eng_CAP`, `Rule_Eng_RESCUE`, `Rule_Eng_TDM`, `Rule_Eng_DM`,
`Rule_Eng_CLANROOM`, `Rule_Eng_SNEAK`, `Rule_Eng_TRA`, `Rule_Eng_OFFICER_TRA` — and each is
looked up as a script function (`0x10151790`, itself `0x100c35b0(0x654515, name)`) and called
(`0x101515c0`) from its own dispatch stub (`Rule_Eng_TDM` at `0x102b4430`, `Rule_Eng_RESCUE`
at `0x102b4467`). A second set — `Rule_TSNE`, `Rule_RESCUE`, `Rule_TDM` at `0xff0554` — is
read at `0x102c82ea` the same way. **That rule id is ours**: it is the triple's first byte in
the `0x4313` rotation this host publishes (`GAMEPLAY_SERVER_RULE`), so a round on `TDM` runs
the client's `Rule_Eng_TDM` path and a round on `SNE` runs another.

**What is still not measurable here.** Whether another rule's path reaches a run that stops
at `13`. The script names its functions by a hash this session could not recover (the ids in
the file are 15-bit indices, not the engine's string hashes), so the runs cannot be tied to a
rule statically. The test is one join on a different `GAMEPLAY_SERVER_RULE`: if the message
still names `14`, no rule avoids it and only the client's own bound can be changed.

The check is a `devcheck`, not a crash: the process ran on for another 56 s in the session
of §60 and the stage kept drawing. It is not what leaves the screen black. **The release
client does not report it at all**: the port's print path (`0x103e6070` → `0x10017390`) is
unconditional in this build, and the RPCS3 log of a complete working session (`RPCS3.log`,
168 921 lines, 27 October) contains no `Game check failed`, no `.cc line` and no `parse.c`
line. The same data shipped and was played without a word of it.

## 62. The custom-point table is the score table, and the script fills it in four runs

**What the table is for.** The 14 bytes at `0x318654a8` are read at thirteen sites, all
through the getter at `0x10178540`, and the scorer at `0x102bed20` shows what an index means:

```
score  = table[1]
if (flag)          score += table[2]
if (flag)          score += table[3]
if (match)         score += table[8]
switch (kind) { 0: += table[10]; 1: += table[11]; 2: += table[12] }
```

so the table is **the points each score action is worth**, not a spawn point or a slot. The
other sites read index `0` (`0x101ec17d`), `9` (`0x101edfe5`), `4` (`0x101fe04f`) and a
computed index (`0x101ebfc9`, `0x101ec116`, `0x101fe1c4`). Nothing in the image reads `13` or
`14` by constant, so the entry the script overflows is one no scoring code asks for.

**The four runs, with their values.** The 17 records of §61 are not one list but four, each
opening with the same 21-byte shape:

| run | pairs `(action, points)` |
| --- | --- |
| A | `(15,5)` |
| B | `(1,13)`, `(1,5)`, `(2,4)`, `(3,2)`, `(4,4)`, `(5,5)`, `(6,5)`, `(7,1)`, `(8,16)` |
| C | `(11,1)`, `(12,1)`, `(13,1)` |
| D | `(11,4)`, `(12,3)`, `(13,2)`, `(14,2)` |

B is common ground (a kill and its bonuses, which is what the scorer reads 1, 2, 3 and 8 for);
C and D are **two variants of the same three actions** `11`, `12`, `13`, and D alone adds
`14`. That is the shape of a rule family split — two rule groups worth `1, 1, 1` and `4, 3, 2`
respectively — rather than of a per-map table, which is what the byte-identity across all 21
stages already said.

**Why this is as far as static work goes.** The records are data, not instructions: the
decoder at `0x100653f0` whose nibble scheme fits `6d 15` belongs to `.\command.c`'s format, not
the script's, and the script's own instruction encoding is not documented anywhere in the
image. Tying a run to a rule therefore needs the client's execution, not its bytes.

**The one test that closes it.** A join on another `GAMEPLAY_SERVER_RULE`. If the message
still names `14`, D runs for that rule too and no rule avoids it; if it disappears, the rule
selected D and the round can sidestep the client's overflow by running a rule from C's family.
Either answer is one join's worth of work, and neither changes what the server sends, since
the number is never on the wire.

## 63. The script names are hashed, and the hash is now known

**The hash.** `system.dll:0x100869d0` turns a name into the 24-bit id the script and the engine
exchange. It is `h = (((h << 5) | (h >> 19)) + c) & 0xffffff` over the bytes, and a result of
`0` is stored as `1`:

```python
def gcl_hash(name):
    value = 0
    for character in name.encode():
        value = ((value << 5) | (value >> 19)) & 0xFFFFFFFF
        value = (value + character) & 0xFFFFFF
    return value if value else 1
```

**It is verified against the client's own check.** The failing setter looks up two ids —
`0x6bb316` and `0x39323b` — and prints `over action %d` / `over custom point %d` for them.
`gcl_hash("action") == 0x6bb316` and `gcl_hash("point") == 0x39323b`, both exactly. So the
intrinsic of §61 is `SetCustomPoint(action, point)` and the stage sets the score points for
action `14`, with `point` the value. The engine files the same ids as named arguments
(`0x101958bb` in the host-call dispatcher), which is the second reading of the same pair.

**Names this recovers.** `gcl_hash("Rule_Eng_TDM")` is `0x890dd1`, and it occurs exactly once
in `n008a` — at `0x1ed43`, inside a table of **497 declarations** that runs `0x1ed11` …
`0x21f5a`, immediately before the stage's own UI strings (`Deathmatch` at `0x21f88`). Every
rule name of §61 — `Rule_Eng_DM`, `Rule_Eng_TSNE`, `Rule_Eng_CAP`, `Rule_Eng_BASE`,
`Rule_Eng_SNEAK`, `Rule_Eng_COOP`, `Rule_Eng_RESCUE`, `Rule_TDM`, `Rule_RESCUE`, `Rule_TSNE` —
appears there exactly once, so the stage defines a function for each rule and the engine's
per-rule dispatch of §61 is calling into that table.

**What it does not yet recover.** Which declaration index is which function, and therefore
where each rule function's body begins: the code section (`0x158a5c` … `0x16b958`, framed by
`0x12efc` at `0x158a58`) holds the declarations, the strings and the bodies in one stream, and
the mapping from declaration order to body offset is the loader's function table, which this
session did not reach. Until that is walked, the run a rule selects stays a matter of running
the client, not of reading it.

## 64. The measured join of 29 October: the server's half completed, the stage script's did not

**What the deployed host did.** The k3s pod `mgo2-gameplay-1-7f989c764d-drf2x`
(namespace `mgo2`, image of `0f8d591`) served one join, and every step of it is in its log:

```
16:33:50  Peer connected from 89.129.16.203:5730 (peer 2); handshake, session established
16:33:52  profile from character 2 (PhilDunphy23); roster entry under 0x9001, the rest 0x1001
16:33:54  match 289's rotation written into the settings burst
16:34:02  the peer reports its map-loaded state: 1 of 1 players have reported
16:34:02  the briefing hears its first join report on 0x1ade; reports complete, 1 of 1
16:34:06  opening run: 10 records, payload echoing 1, rule 1, map 9, a fresh briefing
16:34:12  the peer's request to be placed is answered: 4 records at sequence f3
16:34:29  the peer's last datagram
16:34:59  reaped: 177 records acknowledged, roster acknowledged: yes
```

Nothing in that sequence is unanswered and nothing is left unacknowledged. The join report
arrived, the briefing was armed, the opening run and the placement went out, and the client
acknowledged all 177 records including the roster. The stall is not a missing record: it is
that the peer stopped sending.

**What the client did with it.** The client log of the same join puts the stage start ~27 s
before its window closed, which is the second in which the first `map-loaded` report reaches
the host, and it fails twice within two seconds of that start:

```
[57926 ms] Stage n009a: texture memory 85898/278528 KB free, 0 overflows; textures 2679/16384
[58020 ms] Game check failed: online\supervisor\cp\sv_custom_point.cc line 94: over action 14
[60160 ms] Game check failed: parse.c line 301: argno <= ((GCL_ARGS *)(...argstack_p[-1]))->argc
[85707 ms] Window closed (27 s since the last stage start)
```

So the failure happens **at stage initialisation**, after the map's textures are up and before
the briefing can be presented — the two records the host sends afterwards (`16:34:06` and
`16:34:12`) land in a client whose simulation has already stopped. The 26 seconds between the
second failure and the closed window are the black screen, and the peer's last datagram
(`16:34:29`) is an acknowledgement of a record it is no longer able to act on.

**The second failure is beside the first, and probably a consequence of it.** `sv_custom_point`
writes `byte[action]` of a 14-entry table (§62). The run that carries `(14, 2)` writes one
byte past it, and two seconds later the GCL VM refuses a call whose argument index exceeds the
caller's `argc` — the shape of argument state that the out-of-bounds write has clobbered. The
check build therefore reports the write and then the damage; a release build, which has no
`assert`, does the same write and goes on with the clobbered word, which is why the disc was
played for years without anybody seeing either line.

**This makes the write the thing to avoid, and the rule the only lever the host has.** The
run is selected inside the stage script, from context the client builds out of the room it was
handed; the number is never on the wire (§34, §62). What is on the wire is the rotation's rule
byte, and the same table ships in all 21 stages with the `14` in exactly one of the four runs
(§62). The host publishes that byte from `GAMEPLAY_SERVER_RULE`; this room published `1`
(TDM), and TDM is the rule the reference session of 27 October played — on RPCS3, where the
check build of this port's `system.dll` is not the one running. If a rule from the other
family clears the overflow, the room can sidestep it without touching the client at all; if
it does not, the write is in the disc data every rule reaches and no rule choice avoids it.

## 65. `mgo2-started-on-game-round1`: the briefing is published without a gate, and a played room holds a zero clock

**What this capture adds.** It is the longest session in the corpus (1 400 s, six
rounds) and the first that holds a room returning from a played round to its
briefing. Its shape is measured directly: after a round's clock reaches zero the
host writes `01 00 00` under the round's own operations identifier every four
seconds — six times after the first round, from t+191.599 to t+211.596 — until the
briefing's reports arrive, then one `e0` per reporting player at t+214.231 …
t+215.445, and the round opens at t+215.632 on its whole 2 400 units. The second
boundary is the two-set shape `BRIEFING_FLOW.md` §2 describes: the reports and
their `e0` publications are on `0x12de` (t+566.551 … t+571.145), the briefing's run
follows under `0x1254` at t+572.739 carrying the whole 2 400 in its state block,
the `0b 3c …` countdown ticks under `0x1254` from t+574.755 — **the briefing's own
operations identifier, not the first generation's** — and the round opens at
t+601.404 under `0x1a54` on the report the client sent on `0x1ade` at t+601.160.
The third boundary is the same-generation shape: `0x1ade` publications, the run
under `0x1a54` at t+1 285.939, the countdown under `0x1a54`, and no flip.

**What was wrong here.**

* **The briefing's run was gated on the lobby-slot echo.** §60 bounded that wait at
  three seconds instead of removing it, so a peer whose echo the host does not read
  as `82 NN`/`84 NN` still got its run on an artificial timer, and one whose echo
  arrived later than the timer got the run before it entered — the `myserver19`
  shape the file already records. The echo is now only the source of the run's
  opening pair; the run goes out after the entry's dwell from the later of the
  entry and the report, which is the instant both reference briefings use.
* **A room of one armed a deadline on its own report.** `OnJoinReport` took
  `Math.Min(2, PlayerCount)` reports as complete, so a solo client's first report
  started the briefing's count and opened a round sixty seconds later whether or
  not the player had readied. That is the `myserver17` failure §17 records, and it
  contradicts the design the rest of the file states: a solo briefing is frozen and
  its only exit is the player's own ready. The deadline is now armed by the second
  player's report alone.
* **The ready path opened the round under the briefing's sibling.** Every round in
  this capture plays under the generation its briefing was published on, and
  `MatchRecordGenerationUtils.OfJoinReport` already reads that from the report. The
  ready that completes a room now opens under the set the briefing last heard, not
  under `Sibling`; a client that re-reported on the other generation is on that
  one, and a round written under the first is one its client never hears.
* **The countdown was pinned to the first generation.**
  `BriefingCountdownService` wrote `0x1a54` for every briefing, but the capture
  writes the countdown on the briefing's own operations stream: `0x1254` through
  the briefing whose reports and run are on the `0x12` generation (t+574.755), and
  `0x1a54` through the one on the `0x1a` generation (t+1 289.154). A briefing
  written on the sibling therefore had its run and its reports on one stream and
  its count and ready set on another. The stream is now restarted, and the count
  armed, on the generation the briefing's first report named.
* **A played room's briefing carried the next round's whole clock.** The beat
  writes `RemainingUnits` when no round is open, and that returned
  `BriefingClockUnits`, which `BeginBriefing` set to the whole next round for every
  briefing. The capture holds zero there and reserves the whole value for the
  round's open. A fresh room keeps the whole clock — that is
  `mgo2-started-briefing-then-solo-game`, whose briefing writes `01 60 09` — and
  the difference is the room's history, which is what `BeginBriefing` now reads.

**What is not changed.** The ready records still gate a round that has heard a
report on the other generation, the briefing's countdown and its set are still the
`0x0b` record, and the capture's `e1` stream (2 262 reports over the session) is
answered by the same `e0` publication the code already writes. Nothing here was
verified against a live client; what it changes is the wire this host would write
for the next join, and the check is that join: a briefing screen after a map load
with no entry echo on the wire, a round that opens on the player's ready alone in a
room of one, and a zero clock through the result screen that follows a round.

## 66. `mgo2-started-on-game-round1`: the briefing's two states, and the cadence of a room that has played

**What this section adds beyond §65.** §65 read the boundary between a played round
and the briefing after it. This reads the briefing itself: the states its client
reports, in the only capture that holds a player changing its mind, and the
instants a played room's briefing is written at, so that a writer's cadence can be
compared against it rather than guessed.

**The states, on the client's own slot.** The capture's client is slot `0x11` (17)
— `e1 11` on its reports and `1f 11 NN` on its states — and it writes eight state
records in the whole 1 408 s session: five `01` and three `00`.

| t+ | record | identifier | state |
| --- | --- | --- | --- |
| 595.319 | `1f 11 01` | `0x9254` | ready; the briefing's closing zero adds slot `0x11` |
| 1 310.637 | `1f 11 01` | `0x9a54` | ready |
| 1 311.329 | `1f 11 00` | `0x9a54` | not ready — settings |
| 1 315.993 | `1f 11 01` | `0x9a54` | ready again |
| 1 316.727 | `1f 11 00` | `0x1a54` | not ready — settings |
| 1 322.485 | `1f 11 01` | `0x9a54` | ready again |
| 1 325.885 | `1f 11 00` | `0x9a54` | not ready — settings |
| 1 331.838 | `1f 11 01` | `0x9a54` | ready again |

**The three zeros are the three settings visits, and the settings byte itself is
still unmeasured.** The briefing screen has three states — not ready, ready, and in
the settings screen — and this capture maps the first two: `00` is written when the
ready is taken back and `01` when it is given. Its three `00` records sit between the
three settings visits, each 4.7 s, 5.8 s and 6.0 s before the `01` that follows it,
and the first is written 0.7 s after the ready that preceded it — a player readying
and then opening its menus, not a player that was never ready. What the capture does
not hold is a record for the settings state alone: across all sixteen captures here
the three-byte `1f` body carries nothing but `01` and `00`
(`joined-when-red-team` slot 5, `started-on-briefing` slot 1,
`started-briefing-then-solo` slot 3, `joined-on-briefing-then-game` slot 3,
`mgo2-game1` slot 20, `game2` slot 1, `joined-game-in-progress-last-round` slot 11,
and this capture's slot 17). So the client drops its ready on the way into the
screen and gives it back on the way out, and whether it also writes a byte of its own
for the screen is **open**: a capture of a player entering settings *from* the
not-ready state, or one whose settings record arrives while the ready bit stays set,
is what would name it.

**What that means for this host.** Nothing has to be added for settings, and
nothing has to be removed: `BriefingStateService.IsReady` counts `01` and every
other value takes the player's bit out of `ReadyMask` and out of `AllReady`, which
is the reading the capture's cycles produce. A host that read `00` as "unknown,
keep the bit" would open a round over a player in its menus, and a host that read it
as "left the room" would drop a player who is still there.

**The cadence of a played room, measured.** A round's clock reaches zero and the
host writes `01 00 00` under the round's own operations identifier — not the
briefing's — every four seconds for as long as the room waits, and then the
publications and the open:

| t+ | what | identifier |
| --- | --- | --- |
| 191.599 … 211.596 | `01 00 00`, six times, 4.0 s apart | `0x1254` |
| 214.231 … 215.445 | one `e0` per reporting player | `0x1ade` |
| 215.632 | `01 60 09` — round 2 opens, 0.19 s after the last publication | `0x1a54` |
| 522.006 … 562.045 | `01 00 00`, eleven times, 4.0 s apart | `0x1a54` |
| 566.551 … 571.145 | the reports and their `e0`s | `0x12de` |
| 572.739 | the briefing's run, whole 2 400 in its state block | `0x1254` |
| 574.755 | the `0b` countdown starts, on the briefing's own set | `0x1254` |
| 595.319 | `1f 11 01`, the client's ready | `0x9254` |
| 601.160 | the round's report | `0x1ade` |
| 601.404 | `01 60 09` — round 3 opens, 6.1 s after the ready | `0x1a54` |
| 1 238.176 … 1 278.177 | `01 00 00`, eleven times, 4.0 s apart | `0x1254` |
| 1 285.939 | the briefing's run, whole 2 400 | `0x1a54` |
| 1 310.637 … 1 331.838 | the eight state records above | `0x1a54` / `0x9a54` |
| 1 353.284 | `01 60 09` — round 4 opens, 21.4 s after this client's last ready | `0x1254` |

**Both waits are the room's, not a timer's.** Round 2 opens 0.19 s after the
publication that completes it, round 3 6.1 s after the ready and 0.24 s after the
report that follows it, and round 4 21.4 s after *this* client's ready because
another player had not readied yet — the readiness of the room is what moves, and
no interval in the capture is a fixed pause. The one interval that looks like one —
the four seconds of `01 00 00` — is a repeat, written while the room waits and
replaced by the publication and the open; it is not a gate on anything.

**The open is written twice.** Each round's `01 60 09` is followed by a second copy
about 6.3 s later (222.027, 607.751, 1 359.361), and then the clock counts down in
32-unit steps every 2.5 s (`01 60 08` at 254.025). That is the recorded host's own
cadence, and it is the one a writer has to match; `docs/protocol/BRIEFING_FLOW.md`
§4 carries the round's clock itself.

**What is not changed by this section.** No host branch is added for the settings
screen, because the wire does not carry one; the only new fact the code takes from
it is that `00` is a real record rather than an unread value, which
`BriefingReadyBodiesUtils` now states and `BriefingStateService` already behaves
according to. The §65 fixes stand: the run is not gated on an entry echo, a room of
one arms no deadline, the ready opens under the report's own set, the countdown
rides the briefing's generation, and a played room's briefing holds a zero clock.
## 67. The live join of 30 October: the run went out before the entry, because a missing entry read as a spent dwell

**The session.** The gameplay pod on the k3s node (`mgo2-gameplay-1-77fdbf7fbc-shlxr`,
image `8066f5b`) served a join that never reached a briefing screen: the client's log
(`savemgo.log`) shows the map's stage starting and then the supervisor refusing two of
its own checks — `online\supervisor\cp\sv_custom_point.cc` line 94, *over action 14*,
and the script VM's `parse.c` line 301 — the same pair `myserver34` shows
(§60). What the host wrote before that is measured, and the comparison is with this
capture's own briefing.

**The host's half of that join.**

| clock | what the log says |
| --- | --- |
| 11:36:11.306 | the room's briefing heard its first join report on `0x1ade`; its 1-minute count is armed |
| 11:36:11.365 | **the run is written**: 10 records, operations `0x1a54`, payload `0x1ade`, 3 360 of 3 360 units, as a fresh briefing with no round state |
| 11:36:12.174 | the client asked to be placed in the room — **0.81 s after the run** |
| 11:36:13.720 | the run repeated one tick behind it, without its opening answer |
| 11:36:14.422 | the room-entry request answered: 4 records, the lobby-slot pair, the briefing's countdown and the player's settings header |
| 11:37:13.606 | the briefing's countdown ended; the client had left by 11:37:18 |

**The capture's half, for the same instant.** `mgo2-started-on-game-round1` answers the
pair at t+572.649 (`82 96` on `0x9001`, `84 96` on `0x1001`) and writes the run
**0.09 s later**, at t+572.739. That run is also ten records — `d001 05`, an empty
`0x5001`, the ninety-byte operations state block, `0f 00`, the `85 96`/`86 96` pair,
`025c` with four zeros, the two twenty-byte records and the seven-byte close — and the
host's ten records and its 3 360 units are that composition and that round's clock. The
composition was never the difference; the instant was.

**Why the run overtook the entry.** `EntryRunDwellRemaining` returned `null` — "the
dwell is spent" — whenever the session had no entry recorded, so a peer that had not
entered yet was treated as a peer whose dwell had passed, and the run was released on
the report. §18 measured that shape in `myserver19`: a run written ahead of the entry
is a run for a client that is not in the room, and the client handed one does not report
again, does not ready and never reaches its briefing screen. The entry's dwell is now
counted from the later of the entry and the report, and the report alone when no entry
has been heard, so the run cannot precede the echo that places the peer in the room: the
same join would write at report + 2 s instead of report + 0.06 s.

**What the next join should show.** The run at least two seconds after the report and
after the client's own `82`/`84` pair, followed by the client's `1d` class rows and its
`1f 11 01` ready. If the screen still does not appear with the run behind the entry,
the pair to compare is the run's own bytes against t+572.739 rather than any cadence:
§33 and §34 take the state block and the close triple field by field, and the client's
`sv_custom_point.cc` check is a stage-script failure that a differently timed run
cannot explain on its own (§61 has it as a `devcheck` the release client does not even
print, and explicitly not what leaves the screen black).

**The deployed join after the fix.** The gameplay pod on image `5966e3c` served the
next join at 12:01 on 30 October: entry 12:01:06.463, report 12:01:06.474, and the run
at 12:01:08.476 — **1.99 s after the entry and never before it**, which is the fix
applied. The run is the capture's ten records in the capture's order (`d001`, an empty
`0x5001`, the ninety-byte operations block, `0f 00`, the pair, the four-byte close, the
two twenty-byte records and the seven-byte roster close), its state block matches
t+572.739 at every offset that is read (`v[2]=ff` the round result, `v[38..39]=18`,
`v[40..43]=ff`, `v[44..48]=18`, `v[53..56]=fe`, `v[57..60]=3c`, `v[86]=ff`, clock low
and high), and the room-entry answer follows at 12:01:10.762 with the pair, the
countdown and the settings header. What the client did not do is write its own
operations stream: the reference client's five `1d` rows and the twenty-two-byte record
land 1.69 s, 1.71 s and 1.99 s after its entry, and this one wrote none of them while
the run landed at 1.99 s — level with that window rather than after it, which is the
`myserver23` shape the dwell exists to avoid. The dwell is now 2.5 s, past the latest
measured row and still inside the reference's own 1.90–2.31 s.

## 68. The live join of 30 October after the 2.5 s dwell: the wire matches, and the client stops itself

The pod `mgo2-gameplay-1-6947df958d-hfkv5` (image `674c669`, the 2.5 s dwell and the
item-box order fix) served one join, `89.129.16.203:5730`, log window 13:20:46–13:22:04.
The client's `savemgo.log` is the same session, with `t+0 ≈ 13:20:46.4`. Both halves are
below, in the order they happened.

| clock | host | client |
| --- | --- | --- |
| 13:20:58.5 | handshake, session established | — |
| 13:20:58.7 | profile from character 2 `PhilDunphy23`, slot 0 | — |
| 13:21:00.6 | profile re-sent; roster answered (`0x9001`/80 B, `0x1001`/81 B, close 7 B), repeat and trailer | — |
| 13:21:03.2 | settings burst: `0xd001`/1, `0x1001`/10 (the control), `0x9001`/161 (the host's own), **`0x1001`/25 (the join's settings header)**, two `0x1001`/50 loadouts, `0x0002`/22 | — |
| 13:21:11.09 | entry and report in one datagram: `0x9001`@4 `82 f3`, `0x1001`@5 `84 f3`, `0x1ae4`/`0x1ae3`/`0x1ae2` `00 ff`, `0x1ade` `00 e1 01` | **t+24.7 report** |
| 13:21:11.09 | answers `0x5ae4`/`0x5ae3`/`0x5ae2`, `0x0a5c`/4 `02 00 00 00`, publication `0x1ade`/8, marker `0x5ade` | — |
| 13:21:13.60 | **the run**, ten records: `d001`@4, `5001`@5, `1a54`/90 @0, `1a54`/2 @1, `9001`/26 @8, `1001`/26 @9, `0a5c`/4 @6, `1001`/20 @10 and @11, `1001`/7 @12 — plus the item-box group 0/1 | — |
| 13:21:13.61 | groups **2/3 and 4/5 after the run**, each in its own frame | — |
| 13:21:15.65 | the entry answered: arrays 15/16, countdown `@3`, header `0x1001`/24 `@17` | — |
| 13:21:15.96 | the repeat (8 records, no opening) and four climbing groups 6/7…12/13 | — |
| 13:21:22.8 | item-box continuation ends at ordinal 65 (30 groups) | — |
| 13:21:23 – 32 | the tick: the rules pair ~1/s, the countdown `0x1a54`/7 every ~2.5 s, the array pair every ~13 s, the `0x0002` blob and `0xd001` every ~5 s | — |
| 13:21:31.79 | — | **last datagram (t+45.4)** |
| 13:21:41.1 | the tick continues, all of it acknowledged by nobody | `Stage sm_dd` — map 17 Desert Duel (t+54.7) |
| 13:21:41.9 | — | `sv_custom_point.cc:94 over action 14` |
| 13:21:41.9 | — | `sdregion.c:214 not found prop data!!! (region:0)`, `parse.c:301` |
| 13:21:43.7 | — | `chara.c:192 unknown chara-class 0` |
| 13:22:04.9 | reaped: 172 records acknowledged, roster acknowledged, 0 players reported | window closed (t+78.2) |

**The host's half is the reference's, family for family, and the item-box order is now the
reference's too.** The run follows the entry by **2.51 s** (the reference's 1.90–2.31 s plus
the dwell's margin), carries the ten records §67 measured against t+572.739, and its
`0x0a5c`/4 and `0xd001`/1 are the join state answer and the control byte. The two groups
behind it go out at 13:21:13.607, **after** the run frame (13:21:13.603) and not before it,
which is the change of `674c669` seen on the wire for the first time. The settings burst
carries the `0x1001`/25 header §33 found missing from every earlier live join. Nothing the
client sent in its first 20 s went unanswered, and every record listed above was
acknowledged before the client stopped.

**The client stops its own stream 20.7 s after its report and 9.2 s before its stage
starts.** Its last datagram is at t+45.4; the map stage begins at t+54.7 and fails its own
checks in the two seconds after. So the run arrived 18 s before the client went quiet and
27 s before the stage — the stall is not a record arriving late, missing or out of order,
and it is not the stage either, because the client was already silent before the stage
began.

**Two of the four checks are this stage's and are new.** `sv_custom_point.cc:94` is the
one §61–§64 explain and explicitly withdraw as the cause. `sdregion.c:214 not found prop
data!!! (region:0)` and `chara.c:192 unknown chara-class 0` fire only on map 17's stage
(`sm_dd`) and appear in no earlier live log — the earlier joins played maps 9 and 12 and
printed `unknown chara-class 45A2B8` instead. Both are the stage's own debug assertions at
initialisation, read from the disc's data, and neither names a record this host writes.

**What that leaves.** §42, §45–§47 read the client's `1d` rows, its ready and the fade as
state the client builds for itself at session creation (kind `3`, low bit of `0xbcc`),
with a kind-`2` session built when its own peer slot is torn down at `+4 = 8`. Nothing in
that path reads the wire, and this join adds no record to the list of things it could
read. What would answer it is a memory dump taken at this stall rather than a wire
comparison: `tools/mgo2_dump_read.py` can be asked for the screen objects' step at `+0xb8`
and the peer slot's `+4` byte, which §55 shows is a read the dump supports.

## 69. The PC port's own binaries: the briefing lives in `system.dll`, and the stage data does not name it

The client that joins this host is not the RPCS3 image every earlier section reads. It is the
native port in the `pc-client` folder — `mgo2.exe` (a 441 KB launcher), `savemgo.dll` (4.9 MB,
the port's own layer) and `system.dll` (19.9 MB, the game). This section records what those two
files say about the briefing, so the addresses of §32, §40 and §61 are not carried across by
mistake.

**`system.dll` is a 32-bit PE.** `FILE_HEADER.Machine` is `0x14c`, image base `0x10000000`,
`.text` at `0x10001000` (5.85 MB), `.rdata` at `0x10596000` (11.7 MB), `.data` at `0x110c1000`.
Every address in the sections above is a 64-bit PS3 address and does not denote the same code
here; the `system.dll:0x10178570` of §61 is one of the few that are already this file's.

**The briefing is in the game flow and in the HUD, and nowhere in the port's layer.**
`savemgo.dll` has **no string containing `briefing`** at all (11 977 strings), while
`system.dll` carries the flow file `..\game_flow\MGA_BattleBefor\MGA_BattleBefor_Briefing.cpp`
(`0x10fc505c`) and the HUD `..\online\hud_mgo2\h_mgo2_briefing.cc` (`0x10feffb0`). Beside the
flow's other names sit the transition labels `matching_before`, `Mission_Rule_Loading`,
`briefing-matching`, `matching`, `briefing` and `matching_after` (`0x10fc55f0`–`0x10fc5660`),
which is the order a match walks through: matching, the rule's loading screen, the briefing,
the matching screen again, and the map.

**The flow file is reached only through the check macro.** The two `briefing` pairs are
referenced from `system.dll`'s `.text` exactly twice each, and no `MGA_BattleBefor_Briefing.cpp`
reference is a direct one — the file name is the `__FILE__` of that unit's own `devcheck`. So a
session's log can say whether any check **in that file** fired, and §68's session printed none
from it: the four that fired are `sv_custom_point.cc`, `sdregion.c`, `parse.c` and `chara.c`.
That is suggestive of the flow never entering the briefing unit and is not proof of it, because
a unit whose checks never fire prints nothing either way.

**The stage data does not contain a briefing stage.** The port ships 26 stage directories
(`game/stage`: `init_n`, `lobby`, `mga_boot`, `n001a`…`n024a`, `nt_mgsetup`, `r_sna01_n`,
`sm_dd`, `sm_ll`), and `sm_dd` holds `cache.dar`, `cache.dlz`, `cache.qar`, `cache_d.dlz`,
`cache_nodld.dlz`, `data.cnf`, `n021a.geom`, `n021a.ptl` and `scenerio.gcx` — the geometry of
`n021a` under the `sm_dd` name, which is the desert map map 17 names. Neither `briefing` nor
`briefing-matching` nor `matching_before` exists as a directory, so those six names are
loading-screen labels rather than stages, and the briefing itself is the map stage plus the
HUD unit.

**The stage scripts do not carry the rule as a literal.** Each playable stage's
`scenerio.gcx` (1.49 MB, 18 % printable) contains the word `briefing` twice and `custom`
seventeen times — the seventeen `SetCustomPoint` records of §62 — and no `-rule` literal. The
`-rule N -score <37 ints>` of `PROTOCOL.md` is the reader's notation for the same records, not
a string in the file, so the score table cannot be located in this install by searching for it.

**The patcher rewrites two different hosts and they agree with the session.**
`patcher/mgo2-patcher.ini` sends HTTP `mgo2pc.com` to `altmgo.duckdns.org` and every TCP
connection to `15.204.239.231` to `100.69.153.69` (a Tailscale address), and the port rewrote
the sockets of the join of §68 to that address on ports 5731, 5732, 5733 and 5739. The
*gameplay* host is reached at the public `89.129.16.203:5731` the room advertises, which is
this server's own hairpinned address, and the UDP session of §68 is that pair. Nothing in the
rewrite touches the gameplay endpoint.

**What this leaves.** The port has the briefing, the flow that reaches it and the stage it is
shown on, so the screen is not a missing asset. The three unexplained facts remain the ones §68
lists — the client's own stream stopping at t+45.4, the stage starting nine seconds later, and
no `MGA_BattleBefor_Briefing` check ever firing — and none of them is a record this host fails
to write.

## 70. The port client's own briefing run writes both arrays under `0x1001`, and this host wrote one under `0x9001`

**The capture.** `mgo2-started-on-game-round1.pcapng` is the port client's own session —
`10.2.0.2:5730` (the PC client) ↔ `143.47.227.126:5725` (a reference host), 08:52:57 to
09:16:27, 99 332 datagrams — and it holds two briefings, at 09:02:30 and 09:15:24, each after a
round's end. `tools/pcap_gameplay_log.py` reads it as the host's side, so its `OUT` lines are
what a host that shows the briefing screen writes.

**The briefing's run, in the port client's own identifier set.** At 09:15:24 the reference host
writes, in one frame:

```
0xd001 len  1     the control
0x5001 len  0     the empty roster head
0x1a54 len 90     the state block
0x1a54 len  2     the operations short
0x1001 len 26     the team array
0x1001 len 26     the occupancy array
0x0a5c len  4     the join state answer
0x1001 len 20     the close pair
0x1001 len 20
0x1001 len  7     the roster close
0x1a54 len  3     the round clock
```

and at 09:02:30 the same ten records without the trailing clock (which the beat writes instead,
batching it into that frame at 09:15). **Both arrays travel under `0x1001`.** The live join of
§68 writes them as `0x9001 len 26` and then `0x1001 len 26`.

**`0x9001` is the joiner's identifier, not the host's.** In those twenty-three minutes the
reference host writes `0x9001` **never**: every one of the 825 occurrences in the capture's log
is the client's (`IN`), and the 261 `OUT` ones are the acknowledgement report's warnings —
*"1 records await an answer (0x9001) and the host answered none"*. `0x9001` is
`UdpCommandConstants.JoinRequest`, the tag a joiner opens the roster exchange with
(`RoomRosterService.HostEntryType`), and a host that writes the room's team array under it is
writing the joiner's own tag back at it: the `0x8000` bit is what a client sets on its own
records (§4.1 of `briefing-start-comparison.md`), so the array arrives as one the client itself
is supposed to have sent.

**Fixed** in the two places a 26-byte team array is written — `MatchStartRunFramesUtils.BuildRun`
and `BriefingRoomPublishService.PublishArraysAsync` — which now both write `0x1001`.

**What is not settled.** Whether this is the whole of the black screen. The run's remaining
difference is the trailing three-byte clock, and the capture contradicts itself on it: one
briefing's run carries it and the other does not, so it is the beat's record landing inside the
frame rather than the run's own, and it is left out. The comparison that would settle the rest
is the client's own screen state at the stall (§55, §68), not another wire diff.

## 71. The join of 30 October 14:27: the briefing's wire is the capture's, and the stage that ended black is map 19's `n024a`

**The session.** The gameplay pod `mgo2-gameplay-1-785b95bffb-6plfh`, image `6444a9b` — the first
build carrying §70's array fix — served one join from `89.129.16.203:5730`, log window
13:26:16–13:27:54. The client's `savemgo.log` is the same session with `t+0 ≈ 13:26:37.3`.

| clock | host | client |
| --- | --- | --- |
| 13:26:47.0 | handshake, session established, transcript trusted | — |
| 13:26:47.9 | profile from character 2 `PhilDunphy23`, slot 0 | — |
| 13:26:49.8 | profile re-sent; roster answered (`0x9001`/80 B own entry, `0x1001`/81 B, close), repeat, trailer | — |
| 13:26:52.3 | settings burst: `0xd001`/1, `0x1001`/10, `0x9001`/161, **`0x1001`/25** header, two `0x1001`/50 loadouts, `0x0002`/22 | — |
| 13:27:03.53 | entry and report in one datagram; publication `0x1ade`/8, marker, `0x0a5c`/4; briefing armed at the whole 1-minute count | **t+26.2 report** |
| 13:27:06.04 | **the run**: 10 records + four item-box records, map 19, 3 360 of 3 360 units, as a fresh briefing with no round state | — |
| 13:27:07.76 | the entry answered: the pair, the countdown `@3`, the 24-byte settings header | — |
| 13:27:08.39 | the repeat: 8 records, no opening answer | — |
| 13:27:09.36 → | the tick: the countdown `0x1a54`/7 every ~2.5 s, the rules pair ~1/s, the array pair every ~13 s, the blob and the control every ~5 s | — |
| ≈ 13:27:24 | — | **last datagram (t+47)** |
| 13:27:32.4 | ticks continue, unanswered | `Stage n024a` (t+55.1) |
| 13:27:32.4–34 | — | `dg_texture.cpp:3025` null, twenty *texture memory full*, 39 overflows; `brk_light.cc:1017`, `roll_obj.cc:224`, `windowpane_mng.cc:297`, `chara.c:192 unknown chara-class 0`, `crt_monitor.cc:781`, `tree_test_02.cc:1149`, `sdregion.c:214`, `sdpoint.c:595`, `sv_custom_point.cc:94 over action 14` |
| 13:27:54.3 | reaped: 170 records acknowledged, roster acknowledged, 0 players reported | window closed (t+76.7) |

**The run is §70's fix on the wire for the first time.** The frame carries the ten records the
capture's own briefing writes, with **both 26-byte arrays under `0x1001`** and none under the
joiner's `0x9001`:

```
0xd001 len 1 · 0x5001 len 0 · 0x1a54 len 90 · 0x1a54 len 2 · 0x1001 len 26 · 0x1001 len 26
0x0a5c len 4 · 0x1001 len 20 · 0x1001 len 20 · 0x1001 len 7
0x1a50 len 6 ×2 · 0x1a5a len 6 ×2      the item-box group, behind the run and not ahead of it
```

**The countdown's family is the sibling's, and that is the whole difference.** The fresh-room
reference `mgo2-game-started-on-briefing` writes its countdown as `0x1254`/7 — 137 of them, held
at `0b 3c 00 …` — because its report arrived on `0x12de`; this host's peer reported on `0x1ade`,
so the same seven-byte record travels on `0x1a54` (19 of them, held at the whole minute, a room
of one). Counting the two generations together, every family this host writes is the reference's:
90+2 state, 26+26 arrays, `0x0a5c`/4, 20+20+7 closes, the 7-byte countdown, the 6-byte item-box
groups. Nothing was missing, late or misordered — the run landed 2.5 s after the report and 3.1 s
after the entry, inside the reference's 1.90–2.31 s window plus the dwell's margin (§67).

**What went black is the stage, and it is the first join on map 19.** The room rotated onto
Perilous Palace (`MatchMapUtils` id 19, stage `n024a`), and the client's first load of that stage
is the first log here that fails its own asset lookups: a null texture list, twenty allocations
refused, thirty-nine texture overflows, and five prop directories the stage script asks for and
the stage data does not carry, plus `chara-class 0`. Every earlier join played a stage the map
table names (9, 12, 14, 17) and printed none of the `brk_light`/`windowpane`/`crt_monitor`/
`tree_test` family. Id 19 is the one entry the table holds with **no stage name** and no measured
spawn — `MapSpawnUtils` answers it with Blood Bath's pair because no replay exists — so a room
rotated onto it is a room whose stage this host can neither state nor place.

**The change.** `MatchMapUtils.Next` now walks the maps whose **stage** the table names rather
than the ones with a name, so the rotation skips id 19 and a client is never handed `n024a` by
this host. Everything else in this join matches the capture and is unchanged.

**What is not settled.** Whether the briefing screen follows once the stage loads. The client's
stream stops 20.8 s after its report — before the stage begins — exactly as in §68, so this join
does not say the reception paths work; it says the host's half of the briefing is the reference's
and that the stage it was pointed at is the one this log shows failing. The next join on a
measured stage is what separates the two.
