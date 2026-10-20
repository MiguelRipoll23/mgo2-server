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
| `0b SS 02 00 … 00` | byte 0 = `0x02` | 1 (`state+1`, 1 byte) | `0x00`, `0x01`, and `0xe0` once (`mgo2-game1` t+449.043) |
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
