# The briefing and the round lifecycle

**What this file is.** The measured shape of one match on the gameplay channel: a
player arrives and its state is published, the round opens when every player in
the room has reported, the round plays against its rule's own clock, it ends with
a result the host writes, and the room returns to a briefing for the round after
it. Every claim below is a measurement from a capture named beside it; the two
places where a reader would otherwise infer are marked **[I]**.

The three captures this file is measured from are the two that hold a full
briefing and a third that holds the room's own settings:

| capture | session | what it holds |
| --- | --- | --- |
| `mgo2-game-joined-when-red-team-then-briefing.pcapng` | `10.2.0.2:5730` ↔ `129.153.74.111:5720`, 930 s | three rounds, two of them to the clock, and the match result between them |
| `mgo2-game-started-on-briefing.pcapng` | `10.2.0.2:5730` ↔ `143.47.227.126:5720` | a solo arrival into a briefing, six players reporting, then the round |
| `mgo2-game-started-team-selection-selected-blue-then-game.pcapng` | `10.2.0.2:5730` ↔ `69.213.233.238:5731` | a room list, a join, and a round already playing |

The room's rule, map, briefing time and kill limit do not travel on this
channel. They are negotiated over the lobby's TCP channel and are named in
[`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md); this file reads the gameplay
channel only.

## 1. Arriving into a briefing

A client that joins a game in its briefing sends its briefing state and the host
publishes the room's states back. Both are measured, in both captures, on the
round's payload identifier and on the operations stream.

**The client's report.** Two records in one datagram, carrying the same byte
[V]:

```
0x1254 (ops)     1d <slot> 00 01 03 00 00    x5, the record's class 00..04
0x12de (payload) e1 <slot>                   the report itself
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

**The countdown that precedes a round.** A seven-byte operations record counts
down while the room is briefed, with a per-side flag byte:

```
t+72.560  OUT 0x1254  0b 3c 00 40 00 00 00   count 0x3c = 60, flag 0x40
t+72.560  OUT 0x1254  0b 3c 00 c0 00 00 00   count 0x3c = 60, flag 0xc0
t+76.665  OUT 0x1254  0b 3b 00 c0 00 00 00
```

It runs in every briefing of the capture (`joined-when-red-team`, t+72.5 to
t+191.4 and again t+754 to t+848) and never during a round.

**The unit is one second, and the count ends on a zero.** Read per distinct
record — the same bytes are resent until the client answers, which is why the
capture holds 267 of them for two briefings — the count falls one per second: 60
at 211.533, 29 at 250.139, 15 at 982.610, and into every briefing's close a `0`
(`0b 00 00 ea 01 00 00` at 252.731, `0b 00 00 22 7e 00 00` at 987.019). The span
is the briefing's own: 60 → 20 over the 41.2 s of the first one and 60 → 15 over
the 44.5 s of the second, so the count is what is *left* of the room's briefing
time and not a fixed 60-second wait. Its zero is the briefing's end and not
necessarily the round's: the first briefing writes it at 252.731 and the round's
own clock appears at 262.748, ten seconds later, because the round stays gated on
the reports (§2).

**The tail is a bit set that grows as the reports arrive.** Behind the count sit
four bytes, and the bits in them are *added*, never cleared, as the room fills:
the first briefing runs `40`, `c0`, `c2`, `ca`, `ea` over its records and the
second `08`, `18`, `38`, `78`, `7e` — a bit per player, in the order they
reported. Two records written at one instant carry the same count under
different sets (`3c … 40` and `3c … c0` at 211.533), which is the same reading:
the set before a report and the set after it. Which bit is which player — the
lobby slot, or the order the host learned them — is **[U]**; that the set is the
room's reporting players is measured, and it is why the record is a briefing's
whole state rather than a countdown alone.

## 2. The round opens on the reports, not on a clock

**Measured.** In `started-on-briefing` the last player reports at t+196.828 and
the round opens 1.543 s later, at t+198.371; the publications before it are four
records wide and the run itself opens with the fourth. In `joined-when-red-team`
the client reports at t+95.464 and the round opens at t+123.774, twenty-eight
seconds later, because the room's other players were still arriving, and its
third round opens 37.8 s after its report. No round opens without a report, and
the span between the last report and the round is 1.5 s to 38 s — a gate on the
reports, never a fixed wait.

**The opening run.** The round's records arrive in one compressed frame — the
marker first and the eleven behind it — under the identifier set the report
arrived on. The second round of `joined-when-red-team` opens under
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

The full cycle, as `MatchStartRunService`, `PlayerReadyService` and
`MatchRoundStateService` implement it:

```
briefing    players report 1d/e1 <slot>, host publishes e0 <slot> per player
round open  the last report opens the round: the run under the report's set,
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

* **Answered: the seven-byte `0x0b` countdown's unit** — one count per second, not
the 118 s an earlier reading of resends suggested, and it ends on a written `0`
when the round opens (§1). What the byte's low bits are keyed on is still open:
briefing one writes `40`, `c0`, `c2`, `ca`, `ea` and briefing two `00`, `02`,
`22`, over the same kind of growing mask.
* **[U]** the trailing bytes of the `e0` publication: 5, 6, 7 and 8 all appear.
* **[I]** the report's byte as the lobby slot; measured is only that one byte
  identifies the player and is echoed unchanged.
* **[U]** what ends a round the players reach the rule's kill limit on this
  channel — no capture holds a round that ended on kills rather than time.
