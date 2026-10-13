# The operations stream's state vector, and what of it is a recording

**What this file answers.** The gameplay host writes eighty-nine bytes of round
state into a ninety-byte record every time it opens a round, and until this was
read those eighty-nine bytes were one recorded blob: a capture's vector, copied,
with six offsets overwritten by the fields a parser named. This file says which
of the eighty-nine are **constants of the layout**, which are **the room's own**,
and which are still unread — so that the record is built from what is known
rather than carried as a recording of one match.

**Where the measurement came from.** Six captures of a **working dedicated
server**, read with `tools/udp_state_vector.py` over the operations stream:

| capture | room | opening round clock |
| --- | --- | --- |
| `mgo2-game1.pcapng` | 7-minute round | 3 360 units (7:00) |
| `mgo2-game2.pcapng` | 30-minute round | 14 400 units (30:00) |
| `mgo2-game3.pcapng` | 5-minute round | 2 400 units (5:00) |
| `mgo2-game-started-on-briefing.pcapng` | 8-minute round, visible briefing | 3 840 units (8:00) |
| `mgo2-game-started-team-selection-selected-blue-then-game.pcapng` | 8-minute round | 3 840 units (8:00) |
| `mgo2-game-joined-when-red-team-then-briefing.pcapng` | three rounds, one match decided | 2 400 units (5:00) |

A seventh source, the RPDT `.dat` replays under `tools/replays/`, was **not**
used: a record-framing walk over four of the five files returns plausible-looking
vectors that do not survive contact with the file's own tick structure
(`tools/mgo2_replay_parser.py`), so nothing here rests on them. `rpdt.js` is used
only for the field *names* it already carries, cross-checked against the wire.

## 1. The stream is an opcode language, not a stream of blocks

Every record on the operations identifiers (`0x1254`, `0x1a54`, `0x9254`,
`0x9a54`) opens with an opcode that says what its body is:

| opcode | body | meaning |
| --- | --- | --- |
| `0x00` | 89 bytes | the whole state vector |
| `0x01` | 3 bytes | the clock, a little-endian count of 125 ms units |
| `0x02` | offset, count, values | a **write**: patch `count` bytes from `offset` |
| `0x04` | 5–6 bytes | objective feed; **[U]** |
| `0x05` | `[slot]` | names one player; **[U]** |
| `0x09` | killer, victim | the kill feed |
| `0x0b` | 6 bytes | a recurring per-entity countdown — **not** the briefing countdown (§4) |
| `0x0e` | `[reason]` | **round over**, with the reason that ended it |
| `0x0f` | 2 bytes | the short record the run carries |
| `0x13` | array | per-player rating |
| `0x1d`, `0x1f`, `0x22`, `0x26` | varies | **[U]** |

This matters because it is what makes the vector *writable* rather than
whole: a working server sends `0x00` when a round opens and afterwards only
sends `0x02` slices of it. A host that cannot derive a field can still leave it
alone instead of rewriting the capture's value over it.

**`0x0e` is measured as the round's own verdict, and it is worth having.** In
`mgo2-game1` the round that ends with no result sends `0e 00` at t+352.007, and
`state[2..3] = 06 01` lands at the same instant; the round a side takes sends
`0e 07` at t+688.753 and `state[2] = 03` 2.1 s later. The reason byte is
`[U]` — two samples — but the record itself is the host announcing that the round
is over, and this host writes no equivalent.

## 2. Forty-nine of the eighty-nine bytes are constants

Folded over every round of all six captures, **49 of the 89 bytes hold one value
everywhere** and are properties of the layout, not of any match:

| offsets | value | note |
| --- | --- | --- |
| `state[5..10]` | `0x00` | |
| `state[14..37]` | `0x00` | the long zero run |
| `state[38..39]` | `0x18` | twenty-four, the first two of seven |
| `state[45..48]` | `0x18` | the other five |
| `state[49..52]` | `0x00` | |
| `state[53..56]` | `0xfe` | the sentinel run |
| `state[61..85]` | `0x00` | |
| `state[86]` | `0xff` | |

`MatchStateBodiesUtils` writes these as **named runs** rather than as a recorded
89-byte blob, and the block it builds is **byte-identical to the round-open
vector of three of the six captures**. The three that differ do so only at
offsets this file names as the room's own:

| capture | differs at | why |
| --- | --- | --- |
| `…team-selection-selected-blue…` | `state[44]`, `state[57..60]` | `state[44] = 1` and the seed is `0x28` (forty), not `0x3c` |
| `mgo2-game2` | `state[61..84]` | the seed runs across 28 bytes, not four |
| `mgo2-game3` | `state[11..13]`, `state[40]` | `01 01 04` where the others have zero, and a holder of `0x00` |

**What the runs do not claim.** Twenty-four and sixty are the *values* every
capture carries. They are not read as lengths, durations or counts — nothing on
this channel names them, and this host has no better source than the captures.

## 3. `state[0..6]` is a result block, and the match's result is writable

`state[2]` is the round's result and `state[3]` the match's, in the decoder's
coding (`2` raw team 0, `3` raw team 2, `1` drawn, `6` no result). That much was
already known. What the corpus adds:

* **`state[3]` is written in the same `0x02` write as `state[2]`.** The one
  capture that decides a match sends `02 02 02 02 02` — offset 2, count 2,
  values `2, 2` — at the round end that decides it. No capture ever writes one
  without the other. A match result is therefore a real field of the round's end
  and not a thing this host has to invent.
* **A round's open leaves `state[3]` at zero, not at the `0xff` sentinel.** Five
  of six captures open `state[2] = 0xff` and `state[3] = 0x00`. The asymmetry is
  real, and the block here reproduces it rather than tidying it.
* **`state[0]`, `state[1]` and `state[4..6]` move with them.** In the capture
  that ends a match: the writes are `state[2..3] = 02 02`, then 20.7 s later
  `state[4..6] = 00 02 02`, and the host's next full vector reads
  `state[0..4] = 02 02 00 02 02`. The two do not agree with each other — the
  full vector's `[2]` is `0` where the write left `2` — so the full vector and the
  incremental writes are not the same field read the same way.
* One further shape, in `mgo2-game1`: a round that ends with no result sends
  `state[2..3] = 06 01` and, 21.9 s later, `state[4..5] = fe 05` — five values,
  not three, and `fe` where the other capture has `00`.

**`state[0..1]` and `state[4..6]` are `[U]`.** One capture decides a match. That
is the whole of the evidence and it is not enough to name them.

## 4. The briefing countdown is the clock at 128 — and `0x0b` is not it

`docs/GAMEPLAY_SERVER.md` §5 item 3 listed the briefing countdown as unread and
offered two candidates: a six-byte `0x0b` record, and the operations clock's
128-unit countdown before a round. **The second is right, and the first is
cleared.**

**The countdown: the operations clock at 128 units, in four-second steps.** In
`mgo2-game-joined-when-red-team-then-briefing` the clock reads `0x0080` — 128
units, sixteen seconds of the stream's own tick — at the start of the briefing
and counts `128 → 96 → 64 → 32 → 0` over **16.2 s**, holding at zero afterwards
while the client sits in the briefing. That room's round opens at t+72.560 —
**~45 s after the clock reached zero** — and no round-length value appears
anywhere in it, so the countdown is a fixed sixteen seconds and **not** the
briefing's length. An earlier reading here had it counting to zero over 58.5 s, so
that the round followed 14 s later; the capture's own readings are 32 units and
four seconds apart, and no reading of it supports that span. It is the same opcode, the same little-endian 125 ms unit
and the same stream as the round clock that follows: a briefing is a round's clock
opened on 128 units rather than on the round's own length.
`BRIEFING_FLOW.md` §3 had already measured the span; this host now writes it
(`BriefingCountdownService`, `BriefingCountdownBodiesUtils.CountdownUnits`).

**`0x0b` is not the briefing countdown — it is a recurring per-entity timer.**
This corrects an earlier reading of this file. `0x0b` does count down from
`0x3c` (60), and it does so at roughly two-second spacing on the operations
stream, which is what first made it look like a briefing timer. But:

* it **continues past a round's opening run** — in
  `joined-when-red-team` the round opens at t+799.165 and `0x0b` records run to
  t+836.375;
* a **fresh countdown starts after the round opens** (0x39 at t+799.369, four
  tenths of a second after the open, counting down to 0x0b by t+836.374);
* **several distinct values arrive in one tick** — eight at once at t+78.261 in
  `started-on-briefing`, stepping 59, 57, 56, 54 — which one countdown would not;
* its rate is **not one per second**: 60 → 0 takes 98.2 s in
  `started-on-briefing` and 41.2 s in `joined-when-red-team`, so 1.64 and 0.69
  s per count.

A recurring, per-entity timer that a round does not stop is what it is. Its
purpose is **[U]** and this host writes none of it.

**Its other three bytes are `[U]` too**, and were read as a growing mask of the
room's own objectives: the fourth byte differs per room and fills in during a
briefing — `0x40`, `0xc0`, `0xc2`, `0xca`, `0xea` in one, and in a second
briefing `0x08`, `0x18`, `0x38`, `0x78`, `0x7c`, `0x7e` — while the fifth is an
index that is `0` or `1`.

## 5. The offsets that are the room's own

| offset | what the captures do |
| --- | --- |
| `state[11..13]` | `01 01 04` in one capture, zero in the other five — the one round-specific triple |
| `state[40..41]` | the two objective holders; `0xff` for nobody, `0x3f` for a dropped objective, otherwise `value & 0x3f` is the slot and `& 0x40` a flag |
| `state[42..43]` | the two capture clocks, `0xff` in every non-Capture room |
| `state[44]` | `0x18` in five captures, `1` in the sixth — moves with the seed at `state[57..60]` |
| `state[57..84]` | seeded `0x3c` in five captures and `0x28` in the sixth; 4 bytes in five, 28 in the 30-minute room |
| `state[87..88]` | the round clock, little-endian, 125 ms units |

`state[87..88]` also carries a second, unrelated reading: an `0x02` write at
offset 87 with count 2 puts a countdown and a lobby slot there (the decoder reads
it as Team Sneaking). The block here writes the clock, which is what a round's
open carries.

## 6. Open

* **`state[0..1]` and `state[4..6]`** — one decided match. §3.
* **`0x0e`'s reason byte** — two samples, `0x00` and `0x07`. §1.
* **`0x0b`'s unit, flag word and index** — §4.
* **The length of the `state[57..84]` seed run, and what its length tracks.** It
  is 4 in five captures and 28 in the only 30-minute room, and it is not the
  player count: the five captures hold 2 to 12 players and the 28-byte one holds
  2. The seed value (`0x3c` against `0x28`) moves with `state[44]` (`0x18` against
  `1`) in the same capture, so the two are read together and neither is written
  as a constant.
* **What `0x18` and `0x3c` mean.** Seven bytes of twenty-four and a seed of
  sixty, in every room, of every rule. §2.
* **What `0x0b` is.** A recurring per-entity timer that opens on sixty, is not
  one count per second, and survives a round's opening. §4.
* **`0x04`, `0x1d`, `0x1f`, `0x22`, `0x26`** — present in the captures, not read.

## 7. Reproducing this

```sh
python3 tools/udp_state_vector.py mgo2-game1.pcapng              # the folded vector and a per-offset history
python3 tools/udp_state_vector.py mgo2-game1.pcapng --writes     # every full vector and every 0x02 write, in order
```

The tool folds the stream exactly as the replay console folds it — a full vector
replaces the whole thing, a write patches from its offset — so what it reports is
the state a reader replaying the stream holds, not the last body that happened to
arrive.
