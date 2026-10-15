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
