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
used: a hand-written record-framing walk over four of the five files returned
plausible-looking vectors that did not survive contact with the file's own tick
structure. `tools/mgo2_replay_parser.js` no longer performs that walk — it
delegates the replay to `rpdt.js` — so nothing here rests on the old reading.
`rpdt.js` is used only for the field *names* it already carries, cross-checked
against the wire.

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
| `0x0b` | 7 bytes | the briefing countdown: seconds left and the room's reporting set (§4) |
| `0x0e` | `[reason]` | **round over**, with the reason that ended it |
| `0x0f` | 2 bytes | the short record the run carries |
| `0x13` | array | per-player rating |
| `0x1d`, `0x1f`, `0x22` | varies | the **joiner's own** operations; **[U]** |
| `0x26` | 3 bytes | the host writes it; **[U]** |

The other half of the same table — which function *parses* an incoming body and which
one *writes* a body a client sends — is read out of the image itself in
`docs/analysis/udp-handlers-and-senders.md`: the state access layer every body moves
through (§2), the keys the live handler container holds and the seven codes they reach
(§3, §4), and the bodies the client's own per-phase tick and run opener send (§5).

The two directions write **disjoint** operations, measured over
`mgo2-game-joined-when-red-team-then-briefing` (both identifiers together, 1 192
records):
the host writes `0x00`, `0x01`, `0x02`, `0x09`, `0x0b`, `0x0f` and `0x26`, in bodies of
24, 3, 4–6, 3, 7, 2 and 3 bytes; the joiner writes `0x1d` (7 bytes), `0x1f` (3) and
`0x22` (22). The client's own table — a table of *states*, read out in
`docs/analysis/udp-record-dispatch.md` §3.5–§3.6 — has no entry for any of the joiner's
three: its indices stop at `0x19`, so the host's operations are the ones a client is
built to read and the joiner's are the host's to read — and their decode is not in this
client at all.

This matters because it is what makes the vector *writable* rather than
whole: a working server sends `0x00` when a round opens and afterwards only
sends `0x02` slices of it. A host that cannot derive a field can still leave it
alone instead of rewriting the capture's value over it.

**The client's own table is a table of *states*, not of these opcodes — and that is a
measured difference, not a coincidence of numbering to lean on.** `0x27E8B0` (the module
object's vtable slot 0) indexes **26 state descriptors** at indices `0x00`–`0x19`: `0x00`
is one 168-byte state, `0x19` another of 284 bytes, and `0x01`–`0x18` are **24 states of
one shape whose blocks are consecutive 1248-byte allocations** — the session's slots. Each
descriptor carries a field table of `(state offset, length)` pairs over that block, a
`ceil(count/4)`-byte map of its own, and the flags that say how a field moves
(`docs/analysis/udp-record-dispatch.md` §3.6). Read it out with
`tools/mgo2_record_schema.py`.

It does **not** parse the bodies above: its structure is
`[state index][ceil(count/8)-byte presence map][the present fields]`, so a `0x00` state
needs 8 bytes before its first field and a slot state 22, while the operations bodies are
2–3 bytes — and the state record is the *opposite* case that closes the question: its
opcode `0x00` with the next seven bytes read as a map wants 28 bytes where the record
carries 90. Over 406 144 bodies from three captures (177 179 distinct), **none** of the
operations identifier's 2 214 records fits, and the 1.41% of distinct bodies that do fit
are unrelated tick bodies whose arithmetic coincides
(`tools/mgo2_state_wire_check.py`). The two levels are separate: the registry
dispatches a *queued* record by its first byte, one handler per key, and those handlers
*write* the state the descriptors describe — and the one step that stays **[U]** is
whether the wire's records are the ones that queue carries
(`udp-handlers-and-senders.md` §4, §6). What the joiner's `0x1d`, `0x1f`
and `0x22` say is therefore **[U]** on both counts, and a server body for them stays the
recorded one.

**`0x0e` is measured as the round's own verdict, and it is worth having.** In
`mgo2-game1` the round that ends with no result sends `0e 00` at t+352.007, and
`state[2..3] = 06 01` lands at the same instant; the round a side takes sends
`0e 07` at t+688.753 and `state[2] = 03` 2.1 s later. The record is the host
announcing that the round is over, and its byte is the **client's own round-over
count**: the per-phase tick appends `0x0e` and `instance + 0xC06`, clears that byte as
it sends, and the client's writers of it increment it per recorded round-over while one
is pending — or set it to zero when the pending one is answered as zero
(`docs/analysis/udp-handlers-and-senders.md` §5.2, §5.3). [I] that is what the two
samples are, `0x00` and `0x07`; this host writes no equivalent.

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

## 4. The briefing countdown is the `0x0b` count — and the 128-unit clock is not it

`docs/GAMEPLAY_SERVER.md` §5 item 3 listed the briefing countdown as unread and
offered two candidates: a six-byte `0x0b` record, and the operations clock's
128-unit countdown before a round. **The first is right; this file had it
backwards, and the 128-unit clock is a separate, much shorter timer.**

**The countdown: the `0x0b` record, the briefing's own seconds.** It opens on
`0x3c` (60) — `0x37` (55) in `mgo2-started-briefing-then-solo-game` — and falls
by about one a second, so it is the room's briefing time and not a fixed sixteen
seconds. Its zero is not what usually ends a briefing: when the room fills, the
host writes `0b 00 00 <full set> …` the instant every player is ready, with the
count still in the twenties, and the round opens on that
(`joined-when-red-team` t+113.758). The record and its bytes are read in full in
[`BRIEFING_FLOW.md`](BRIEFING_FLOW.md) §1; this host writes it
(`BriefingCountdownService`, `BriefingCountdownBodiesUtils`).

**The 128-unit clock is a separate short timer.** In
`mgo2-game-joined-when-red-team-then-briefing` an operations clock reads `0x0080`
— 128 units, sixteen seconds of the stream's own 125 ms tick — and counts
`128 → 96 → 64 → 32 → 0` over **16.2 s**, holding at zero afterwards while the
client sits in the briefing. Sixteen seconds is not the briefing's length, so it
is not the briefing countdown; the record is the same opcode, unit and stream as
the round clock that follows, and it falls in the gap between the round that had
just ended and the briefing after it, which is why `BRIEFING_FLOW.md` §3 still
leaves open whether it is the result screen's. Either way it is not the `0x0b`
count and not the briefing's length.

**The set behind the `0x0b` count is the room's readied players.** It grows as each
player readies — `40 00`, `c0 00`, `c2 00`, `ca 00`, `ea 01` in one briefing,
`00 08`, `00 18`, `00 38` in the next — read little-endian over two bytes, so it is
wider than a byte. **The bit is the slot the player's own ready record named**
(`1f <slot> 01`, `BRIEFING_FLOW.md` §2): the host sends the roster entry for that
slot at the instant the bit appears, and the two agree for every bit of both
briefings of `joined-when-red-team`. The reports are a narrower set — `2a` for the
room whose count carries `01ea` — because a bot the host adds readies without ever
reporting. The earlier reading as a set of the room's own objectives is cleared.

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
* **Which player is which bit in `0x0b`'s reporting set** — §1.
* **The length of the `state[57..84]` seed run, and what its length tracks.** It
  is 4 in five captures and 28 in the only 30-minute room, and it is not the
  player count: the five captures hold 2 to 12 players and the 28-byte one holds
  2. The seed value (`0x3c` against `0x28`) moves with `state[44]` (`0x18` against
  `1`) in the same capture, so the two are read together and neither is written
  as a constant.
* **What `0x18` and `0x3c` mean.** Seven bytes of twenty-four and a seed of
  sixty, in every room, of every rule. §2.
* **Whether the 128-unit clock is the result screen's or a briefing's.** §3,
  §4.
* **`0x04`, `0x1d`, `0x1f`, `0x22`, `0x26`** — present in the captures, not read.
  For the joiner's `0x1d`, `0x1f` and `0x22` §1 now says why they stay open:
  they are past the end of the client's own state table, whose indices stop at `0x19`.

## 7. Reproducing this

```sh
python3 tools/udp_state_vector.py mgo2-game1.pcapng              # the folded vector and a per-offset history
python3 tools/udp_state_vector.py mgo2-game1.pcapng --writes     # every full vector and every 0x02 write, in order
python3 tools/mgo2_record_schema.py                              # the client's 26 state descriptors, and their fields
python3 tools/mgo2_state_wire_check.py                           # whether that schema consumes any captured body (it does not)
```

The tool folds the stream exactly as the replay console folds it — a full vector
replaces the whole thing, a write patches from its offset — so what it reports is
the state a reader replaying the stream holds, not the last body that happened to
arrive.
