# The briefing and the round lifecycle

**What this file is.** The measured shape of one match on the gameplay channel: a
player arrives and its state is published, the briefing is answered with a run of
its own before the round opens, the round plays against its rule's own clock, it
ends with a result the host writes, and the room returns to a briefing for the
round after it. Every claim below is a measurement from a capture named beside it;
the two places where a reader would otherwise infer are marked **[I]**.

The three captures this file is measured from are the two that hold a full
briefing and a third that holds the room's own settings:

| capture | session | what it holds |
| --- | --- | --- |
| `mgo2-game-joined-when-red-team-then-briefing.pcapng` | `10.2.0.2:5730` ↔ `129.153.74.111:5720`, 930 s | three rounds, two of them to the clock, and the match result between them. The capture's own note (`.txt`, beside it) names the rule — team deathmatch, 40 kills, 4:55 — and the order of events: the join landed as the red team won, then a briefing with every player readying and this client last, then round 01/02, then a second briefing and another game |
| `mgo2-game-started-on-briefing.pcapng` | `10.2.0.2:5730` ↔ `143.47.227.126:5720` | a solo arrival into a briefing, six players reporting, then the round |
| `mgo2-game-started-team-selection-selected-blue-then-game.pcapng` | `10.2.0.2:5730` ↔ `69.213.233.238:5731` | a room list, a join, and a round already playing |

The room's rule, map, briefing time and kill limit do not travel on this
channel. They are negotiated over the lobby's TCP channel and are named in
[`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md); this file reads the gameplay
channel only.

## 1. Arriving into a briefing

A client that joins a game in its briefing sends its **join report** — the room's
map is loaded and it is on the briefing screen — and the host publishes the room's
states back. Both are measured, in both captures, on the round's payload
identifier and on the operations stream. It is not the player's readiness, which
is `1f <slot> 01` and §2's subject.

**The client's report.** Two records in one datagram, carrying the same byte
[V]:

```
0x1254 (ops)     1d <slot> 00 01 03 00 00    x5, the record's class 00..04
0x12de (payload) e1 <slot>                   the join report itself
0x0a5c (ops)     <slot>                      the one-byte companion
```

`joined-when-red-team` t+95.464 sends `1d 05 …` five times with classes
`00`–`04`; `started-on-briefing` t+62.623 sends `1d 01 …` the same way. The byte
behind the opcode is the **reporting player's own lobby slot**, and the same
byte appears in every record of the datagram [I]: the host's publication for
that player carries it, the run the round opens with echoes it, and the slot
number is in `1..24` in all nine reports across the two captures — including
game1's `0x14` (slot 20). What is [V] is that one value identifies the player
and that both sides echo it unchanged.

**The host's publication.** One record per player that has reported, in the
order the host learned them, each carrying that player's own byte under its own
class byte:

```
t+56.123  OUT 0x12de cls=00  e0 01 00 00 00 00 00 00   slot 1  reported t+55.838
t+112.619 OUT 0x12de cls=01  e0 02 00 00 00 00 00 00
t+144.750 OUT 0x12de cls=02  e0 03 00 00 00 00 00 00
t+154.370 OUT 0x12de cls=03  e0 04 00 00 00 00 00 00
t+213.122 OUT 0x1ade cls=04  e0 05 00 00 00 00 00 00
t+220.488 OUT 0x1ade cls=05  e0 06 00 00 00 00 00 00
```

The class byte is the player's place in the publication and the value is the
player's own; the set is rewritten as each report arrives. `joined-when-red-team`
publishes the same way (slots 01, 03, 05, 09, 0a). The **body length is not
settled**: five bytes (`e0 05 00 00 00`), six, seven and eight all appear on the
same record, and one five-byte body in the same capture carries an unrelated
tail (`e0 0a c0 00 03`) after the match ended [U] what the trailing bytes are.

**The room is written too, and not only the players.** Two records carry the room
itself while a briefing runs, and neither waits for a client packet. The
**rules-roster pair** — the empty list `fe` twice, under its two classes — is
written by `started-on-briefing` from its roster answer through its whole 198 s
briefing, 12 to 26 times per ten seconds, and it rides inside the sparse
controls and blobs; both captures that hold a join write it in the roster run
itself, ahead of the roster entries, and the beat takes it over when a round
opens. The **room's count** is a two-byte `0x1258`/`0x1a58` record whose second
byte is how many players the room holds: `started-on-briefing` writes `00 01`
beside its join and `00 02` once a second player is in the room, while the
`0x4313` reply it joined on reads **one player of a limit of seventeen** — so
the byte is the room's population and not its limit — and `joined-when-red-team`
writes `00 05` into a room of five joiners. Written as
`BriefingRoomPublishService`, which also writes the pair.

**A briefing's answer to `83 NN` is the pair, the countdown and a settings
header — and no entry.** The four-byte request to be placed in the room is
answered with the lobby-slot pair under its own sequence, the countdown record at
the count the room holds, and a twenty-four-byte `0b SS 02 00 …` for the
requesting player, in that order:

```
t+93.127  OUT 0x1001 cls=0f  85 00 fe …      the pair, at the request's sequence
t+93.127  OUT 0x1001 cls=10  86 00 7f 07 …
t+93.127  OUT 0x1254 cls=02  0b 3c 00 00 00 00 00   the countdown, 60 s, set empty
t+93.127  OUT 0x1001 cls=11  0b 02 02 00 … 01      slot 2, the player's own key + 1
```

(`mgo2-game-started-on-briefing` t+93.127; `mgo2-game2` t+78.987 answers
t+78.148's request the same way, slot byte and all. The countdown keeps its own
stream — class `02` while the pair's runs `0f`, `10` and the header takes `11` —
because it is the same series the tick writes.) The pair is the room's and the
header is the player's; **the entry is not here**, because it carries the map's
spawn and belongs to a round that is playing: the two answers given during a
round carry it with the roster close behind it (`mgo2-game1` t+201.04,
`started-on-solo-game` t+101.41, `started-briefing-then-solo` t+180.49). Handed
one while it is being briefed, a client leaves the briefing screen for a map with
no round behind it (`docs/analysis/join-stall-after-mode2.md` §24–§25). Written as
`TeamArrayService.EnterRoomAsync`, with `SettingsHeaderBodiesUtils` and
`BriefingCountdownService.AnswerRecord`.

**The countdown that precedes a round.** A seven-byte operations record counts
the briefing down, and its two halves move apart:

```
t+72.560  OUT 0x1a54  0b 3c 00 40 00 00 00   60 s, set 0x0040
t+72.560  OUT 0x1a54  0b 3c 00 c0 00 00 00   60 s, set 0x00c0
t+76.665  OUT 0x1a54  0b 3b 00 c0 00 00 00   59 s, set unchanged
t+76.666  OUT 0x1a54  0b 3a 00 c2 00 00 00   58 s, a report moved the set
```

(measured on the first briefing of `joined-when-red-team`, under `0x1a54`; the
second runs under `0x1254`. It runs in every briefing of the capture and never
during a round.)

**The seconds are what is left of the room's briefing time.** It opens at `0x3c`
(60) — `0x37` (55) in `started-briefing-then-solo` — and falls by about one a
second. It is **held at the whole time until the room has a second player**: a
room of one stays frozen for the whole briefing, which is `started-on-briefing`
holding `0b 3c` from t+93.1 to t+132.2 with a single player in it and counting
only after that; and it is why 60 → 0 spans 98 s there and 41 s in
`joined-when-red-team` — the same 60 counts, a hold before them. Two records at
one instant carry the same count under different sets (`3c … 40` and `3c … c0` at
211.533), the set before a report and the set after it.

**The set behind the count grows as the players report, and it is wider than a
byte.** The bits are *added*, never cleared: the first briefing runs `40 00`,
`c0 00`, `c2 00`, `ca 00`, `ea 01` and the second `00 08`, `00 18`, `00 38`.
Read little-endian the set is two bytes (`0x01ea` has bits past its first byte,
and the room's second briefing carries `0x7c02`), so it names slots up to 15 — the
reference's rooms hold fourteen. **Which bit is which player is measured, and the
bit is the slot its own ready record named** (§2): the host sends the roster entry
for that slot at the very instant the bit appears, and the two agree for every bit
of both of this capture's briefings. A ready moves the set while the seconds keep
ticking, which is what makes the record the briefing's whole state and not a count
alone.

**Its last word is a zero carrying the whole set, and that is the round
opening.** The instant the room is fully ready the host writes
`0b 00 00 ea 01 00 00` (t+113.758), and the ticking records at that same moment
still carry the count they were on (`0b 17 00 ca 00 00 00`) beside the round's
run, with twenty-odd seconds left. So a briefing that fills does not wait its
count out; its zero is the ending written out, and the count is left unfinished.
A briefing whose count *does* reach zero first — `started-on-briefing`, 60 at
t+132.2 down to 0 at t+191.4 — is one the room never filled in time.

## 2. The round opens on the ready records, not on a clock

**Measured.** In `started-on-briefing` the last player reports at t+196.828 and
the round opens 1.543 s later, at t+198.371; the publications before it are four
records wide and the run itself opens with the fourth. In `joined-when-red-team`
the client reports at t+95.464 and the round opens at t+123.774, twenty-eight
seconds later, because the room's other players were still arriving, and its
third round opens 37.8 s after its report. No round opens without a client's
record behind it and no round opens on a fixed wait.

**The record that ends the wait is the ready, and the report is what a run is
written from.** The two are different bodies on different streams: `e1 <slot>`
is a client saying it has loaded, and `1f <slot> 01` is the player changing its
briefing state to ready. `joined-when-red-team` sends the ready at t+112.750 and
its round is open at t+113.758, one second later; its second briefing sends the
same record at t+844.858 and opens at t+848.046, 3.2 s later. The reports of those
same two briefings are minutes away from the opens — t+57.656 and t+870.222, the
same client reporting its load — so a gate on the reports would open the first of
them thirty seconds before this client had readied at all.

**The room's ready set, ready by ready.** The capture is one client's flow, so the
other players' ready records are not in it; what is in it is their effect on the
countdown's set, and every bit is named by the roster entry the host sends at the
instant it appears. Read down the two briefings:

| t+ | what moved the set | the room's ready set | the slot's player |
| --- | --- | --- | --- |
| 72.560 | `0b 3c 00 40 00 00 00` — the host adds its first bot and has it ready in the same instant | slot 6 | BOT-01 |
| 72.560 | `0b 3c 00 c0 00 00 00` | slot 7 | BOT-02 |
| 76.666 | `0b 3a 00 c2 00 00 00` | slot 1 | Aozori |
| 81.168 | `0b 36 00 ca 00 00 00` | slot 3 | haw9 |
| 111.166 | `0b 17 00 ca 01 00 00` — the host adds its third bot, already ready | slot 8 | BOT-03 |
| 112.750 | `1f 05 01` **from this capture's client — the room's last ready** | — | fargraven2, slot 5 |
| 113.758 | `0b 00 00 ea 01 00 00` — the closing zero, the whole set | slots 1, 3, 5, 6, 7, 8 | the six above |

and then the round, and then the second briefing:

| t+ | what moved the set | the room's ready set | the slot's player |
| --- | --- | --- | --- |
| 799.165 | `0b 3c 00 00 08 00 00`, `… 18`, `… 38` — three bots added and ready | slots 11, 12, 13 | BOT-01, BOT-02, BOT-03 |
| 800.807 | `0b 3a 00 00 78 00 00` | slot 14 | BOT-04 |
| 805.291 | `0b 39 00 02 78 00 00` | slot 1 | Aozori |
| 811.040 | `0b 30 00 02 7c 00 00` | slot 10 | Tddan80T |
| 828.562 | `0b 20 00 02 7e 00 00` | slot 9 | haw9 |
| 844.858 | `1f 05 01` **from this capture's client — the room's last ready** | — | fargraven2, slot 5 |
| 848.046 | `0b 00 00 22 7e 00 00` — the closing zero, the whole set | slots 1, 5, 9, 10, 11, 12, 13, 14 | the eight above |

**What the two tables say.** The count is held at the room's whole briefing time
(`3c 00`) until the room has a second player, and moves only then; the set grows by
one bit per ready and never by a report; the bots are readied by the host in the
record that adds them, so a room's *reported* players and its *readied* ones are
different sets — this room reports as `2a` (slots 1, 3 and 5) and readies as
`0x01ea` (slots 1, 3, 5, 6, 7 and 8). The client that readied last in both
briefings is the one whose bit the closing zero adds last, which is the note this
capture ships with: *every player changed their state to ready, me last*.

**A briefing is answered before its round is.** A client reports twice a round and
the host answers the two differently. The first report is met by the run under its
own set while the room is still briefed: `started-on-briefing`'s first report
arrives at t+55.838 on `0x12de` and the run under `0x1254` goes out at t+58.255,
two and a half seconds later and 140 s before the round; `started-briefing-then-solo`
reports at t+54.918 on `0x12de`, its run under `0x1254` follows at t+57.626, and
the run under `0x1a54` — the round's — only at t+174.371.

**The report names the set; the client's entry is the instant.** Between the report
and the run the client echoes the run's lobby-slot pair — `82 NN` · `84 NN`, the
handshake's own request to be placed in the room — and every run in every capture
held here follows that echo, 0.3 s to 4.7 s behind it (`started-briefing-then-solo`
t+55.312 → t+57.626; `started-on-briefing` t+56.360 → t+58.255; and
`joined-when-red-team`'s mid-round entries at t+94.2 → t+96.9 and t+131.9 →
t+132.8, where the room's last report was 39 s and 75 s earlier). A run written on
the report instead goes out before the client is in the room
(`docs/analysis/join-stall-after-mode2.md` §18). The round opens on the ready that
completes the room's readied players, or — in a room that has a second player to wait
on — on the briefing's own deadline when a player never readies
(`GAMEPLAY_SERVER.md` §4.2); it plays under the *other* set: `joined-when-red-team`'s
briefing is written under the set its first report named and its round under the
sibling, which is the set its run and beat are written on. `started-on-briefing`
reports its second time at t+196.828 on `0x1ade` and `started-briefing-then-solo`
asks for its round at t+173.438; the set flips between the briefing and the round,
and the run the round opens with is on the round's own set.

**The opening run.** The round's records arrive in one compressed frame — the
marker first and **thirteen** behind it — under the identifier set the report
arrived on. The thirteen are listed in `DATA_PHASE_DELIVERY.md` and in
`docs/analysis/replay-findings.md` §1.4: two empty `0x5001` roster heads, the
operations state block and its two-byte companion, the lobby-slot pair, the close
**triple** (two twenty-byte records and the seven-byte roster close), the round
clock, the payload and the rules pair. `tools/udp_start_run.py` reads the frame
out of a capture and prints it. The second round of `joined-when-red-team` opens under
`0x1a54`/`0x5ade` and the third under `0x1254`/`0x52de`, and in
`started-on-briefing` the set flips from `0x12de` to `0x1ade` between the report
at t+55.838 and the report at t+196.828 — the run answers the report's own set.

## 3. The clock is the rule's time limit, in 125 ms units

The operations record `0x01` carries a little-endian `u16` count of 125 ms
units. Measure the two captures' first round:

| capture | room | rule's time (lobby) | clock at the round open | as minutes |
| --- | --- | --- | --- | --- |
| `started-on-briefing` | Best TDM \[EU\] | TDM 8 | 3 840 units | 480 s = 8:00 |
| `joined-when-red-team` | vs bots | TDM 5 | 2 400 units | 300 s = 5:00 |

So **480 units are one minute**, and the round's length is the room's own
setting for that rule, not a constant: game1's room reads 3 360 units (7:00).
The clock counts down in real time — 32 units per 2.5 s, i.e. exactly 125 ms per
unit, over the 300 s between t+123.774 and t+423.051.

**The count is the round's own, and it moves.** The same room need not open two
rounds on the same value, because the value is the round's rule time and the
rotation moves: game1's room opens its first two rounds at 3 360 (7:00 TDM) and a
later round at **2 400 (5:00)**, and each of those counts down at the same
8 units/s — 8.03/s over the 83.7 s of `mgo2-game2.pcapng`'s round, which opens at
**14 400 (30:00)**. A host that wrote one value for the life of the container
would be replaying a byte, not running a clock; the value it writes is the one
[`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md) §2 reads out of the room's own
rule time, and the room's rows are where this server reads it
(`MatchSettingsService`).

**One measured round does not move, and it is the case worth knowing.** game1's
first round holds 3 360 from its open at t+46.4 to the record before the next
room's first frame [U] why: the record is present and unchanged for 139 s, so
the freeze is the host's and not a gap in the capture.

**The same record is the briefing's clock too.** At the start of
`joined-when-red-team` it reads `0x0080` (128 units = 16 s) and counts to zero in
16.2 s while the client sits in a briefing, before any round opens. A host writes
the round's time limit when it opens the round and carries the remaining time in
the records behind it.

**That capture's own note, and what it leaves open.** It ships one
(`mgo2-game-joined-when-red-team-then-briefing.txt`): team deathmatch, 40 kills,
4:55, the join landing as the red team won, the briefing that followed with every
player readying and this client last, red taking the second round for 2–0, then a
second briefing and another game. So the 16.2 s clock falls in the gap between the
round that had just ended and the briefing after it, and it could be the **result
screen's** countdown rather than the briefing's: nothing in the capture separates
the two, because both would be this one record on this one stream and the host
writes no other clock between them. Either way the measurement holds — the values,
and the 45 s of zeros before the round — and it is not the briefing countdown this
server writes, which is the `0x0b` count of §1 and opens on the briefing's own
sixty. **[U]** which of the two it is, and whether the client's own timer is the
briefing time at all.

## 4. The round ends, and the host writes the result

**Measured, both endings.** `joined-when-red-team` plays three rounds and every
one ends when its clock reaches zero:

```
t+123.774  OUT 0x1254  01 60 09   clock 2 400 units, round 1 opens
t+423.051  OUT 0x1254  01 00 00   clock 0 — the round is over
t+423.052  OUT 0x1254  02 02 01 02          state write: offset 2, count 1, value 2
t+450.297  OUT 0x1254  01 60 09   clock 2 400 units, round 2 opens
t+754.040  OUT 0x1254  01 00 00   clock 0
t+754.040  OUT 0x1254  02 02 02 02 02       state write: offset 2, count 2, values 2 2
t+776.791  OUT 0x1254  02 04 03 00 02 02    state write: offset 4, count 3, values 0 2 2
t+856.541  OUT 0x1254  01 60 09   clock 2 400 units, the next match's round opens
```

The write is opcode `0x02` followed by an offset into the 90-byte state block
(opcode `0x00`), a count, and the values; the state block itself is written whole
eight times in that capture. The values land where the replay decoder reads
them, which is the one place a live capture is checked against the replays:

| state byte | meaning | measured |
| --- | --- | --- |
| `state[2]` | the round's winner: `2` = raw team 0, `3` = raw team 2, `1` = drawn, `255` = not yet written | written `2` at t+423.052 and again at t+754.040 |
| `state[3]` | the match result, same coding | written `2` with the second round — the match ended 2–0 |

The capture's own note records red winning the round that ended as this client
arrived, then both rounds it played, then the match, 2–0; the wire agrees
`state[2] = 2` at both round ends and `state[3] = 2` at the second. The coding
is `rpdt.js`'s, measured by it over 28 round ends in 16 replays
([`RPDT_REPLAY_RECORDS.md`](RPDT_REPLAY_RECORDS.md)).

**A goal is not on this channel.** Nothing in the scoreboard, the kill feed or
the result writes carries the "40 kills" a room is set to; the round's kill
limit is the room's `TDM tickets` setting and the host's own score-keeping is
what decides a round that reaches it. See
[`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md) §3.

## 5. Back to a briefing, and the next round

A round is preceded by its own briefing. The captures rewrite the ready set
between rounds rather than carrying it over: `joined-when-red-team` publishes
nothing between t+423 and t+450 except the countdown `0b …`, and the round that
opens at t+450.297 opens from the same set of reports that opened the last one —
which is why a host that keeps the states from the round before opens the next
round on a player who has not reported it.

The full cycle, as `MatchStartRunService`, `BriefingStateService` and
`MatchRoundStateService` implement it:

```
briefing    players report 1d/e1 <slot>, host publishes e0 <slot> per player;
            the first report is answered by the run under its own set, while the
            room is still briefed
round open  the report on the briefing's other set opens the round (or the
            briefing's deadline does): the run under that report's set,
            the clock 0x01 = the round's rule time, read from the room's rows
round play  the clock counts down; the beat's records ride behind it
round end   the clock reaches 0 (or a goal ends it): state[2] = the winner
briefing    the states are cleared; the next round waits for reports of its own
```

## 6. What the host does not write

Every scoreboard record, kill-feed record (`0x09`) and result write in all three
captures travels host → client. The client sends only its own reports, its
position/state stream (`0x8077`/`0x8877`) and its answers to the lobby-slot
arrays. A host therefore publishes a score it has derived itself; there is no
client report of a kill to read. A server that only relays records and does not
simulate cannot know a round's winner — which is why the round's result write is
the seam `MatchStartRunService.End` leaves open, and not a record this server
sends today.

## 7. Open

* **Corrected: the `0x0b` countdown *is* the briefing's, and the reasons it was
  thought not to be are three misreadings of one per-peer record.** It was called
  a recurring per-entity timer because it "continues past a round's opening run",
  "eight distinct values arrive in one tick", and "60 → 0 takes 98.2 s and 41.2 s,
  so not one per second". Each is the record written per peer: the eight values are
  one tick's write to each peer *plus* the closing `0b 00`; the count that runs
  after a round opened is the *next* briefing's, because the captures hold two; and
  the uneven 1.64/0.69 s per count is the frozen hold at the start plus the early
  zero when the room fills. Read per peer the record is the briefing's own —
  seconds down from the room's briefing time, set grown by each report, zero with
  the full set at the round's open, and nothing during a round (§1).
  `STATE_VECTOR.md` §4 still carries the old reading and should be corrected with
  it.

* **[U]** the trailing bytes of the `e0` publication: 5, 6, 7 and 8 all appear.
* **[I]** the report's byte as the lobby slot; measured is only that one byte
  identifies the player and is echoed unchanged.
* **[U]** what ends a round the players reach the rule's kill limit on this
  channel — no capture holds a round that ended on kills rather than time.
