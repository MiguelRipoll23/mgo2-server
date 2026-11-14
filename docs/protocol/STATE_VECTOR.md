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
count still in the twenties, and the count goes on ticking afterwards — that zero
opens no round (`joined-when-red-team` t+113.758; its run is at t+132.791). The
record and its bytes are read in full in
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
| `state[40..41]` | the two objective holders; `0x3f` for a dropped objective, otherwise `value & 0x3f` is the slot and `& 0x40` a flag. The parser's table reads `0xff` as "nobody", and a live working server writes `ff ff ff ff` across `state[40..43]` (`working-server-log`, clock `0x03dd`); `briefing-then-solo-game` writes `0x1e`, which is **slot 30 of a 24-slot table** and so names nobody. This host wrote `0x1e` and now writes `0xff` |
| `state[42..43]` | the two capture clocks, `0xff` in every non-Capture room and `0x1e` in `briefing-then-solo-game` — the same four bytes as the holders above, which is why this host writes one value at `state[40..43]`. **A wrong holder is the first thing to check when a client's score HUD stays empty**: `myserver` carried `1e 1e 1e 1e` through a whole 5-minute round while the live working server carried `ff ff ff ff` |
| `state[44]` | `0x18` in five captures, `1` in the sixth — moves with the seed at `state[57..60]` |
| `state[57..84]` | seeded `0x3c` in five captures and `0x28` in the sixth; 4 bytes in five, 28 in the 30-minute room |
| `state[87..88]` | the round clock, little-endian, 125 ms units |

`state[87..88]` also carries a second, unrelated reading: an `0x02` write at
offset 87 with count 2 puts a countdown and a lobby slot there (the decoder reads
it as Team Sneaking). The block here writes the clock, which is what a round's
open carries.

**A `0x02` write's offset counts from the state base, two bytes before this
vector's index 0. [V]** The port's builder appends the `count` bytes at
`[this+0x84] + offset` — `mov edi,[ebx+0x84]`, `movzx ecx,al`, `add edi,ecx` at
`0x101ef999`–`0x101efa0b` — and the `0x00` builder reads 62 bytes from
`[this+0x84] + 2` and then the clock at `[this+0x84]`
(`0x101f0b41`–`0x101f0b63`). The captures agree: the write at `0x28` in
`failed-memoryviolation` moved exactly `state[38..39]` of the next block
(t+77.235 against t+82.229). So a write names this vector's index two lower, and
`briefing-then-solo-game`'s opening `02 2a 04 1e 1e 1e 1e` names `state[40..43]`
— the four unclaimed bytes of the table above.

**The result write is the one place the naming and the offset disagree.**
`joined-when-red-team` writes `02 02 01 02` and `02 02 02 02 02` at its round
ends, and §3 below and `BRIEFING_FLOW.md` §4 read those as `state[2]` and
`state[2..3]` — the direct base. Under the `offset − 2` reading just stated they
name `state[0]` and `state[0..1]` instead. **Both readings are of the same
bytes**, and neither the captures nor the client's table say which byte a banner
is drawn from; so what to send is settled and what to call it is not (§6). Our
host writes the capture's own body either way.

## 5b. The clock is a plain count, and the score is not in it

**The record is one `u16` and nothing else.** Opcode `0x01` is the opcode, then
two bytes: `MGO2.ELF` `P2P_PublishRoundClock` (`0x754848`) opens a cursor, appends
the byte `1`, appends the two bytes the word pointer at `param_1 + 0x104` points
at, sends through `param_1 + 4`, and remembers the value it sent at
`param_1 + 0x108`. There is no mask, no flag byte and no second field — the
record is a copy of one word, and the word is the same one the state vector
carries at `state[87..88]` little-endian (§5).

**Measured, over five rooms:** `2400` (5:00), `3360` (7:00), `3840` (8:00 twice)
and `14400` (30:00), dropping `32` units every four seconds — 8 a second, 480 a
minute, 125 ms a unit — to zero, which is the round's end. The values are whole
minutes of the rule's own time, which is the property that rules a packed field
out: a mode bitfield would not land on `480 × minutes` in five rooms of five
different rules.

**Kills and deaths do not travel here, and the record that carries them was
measured against the feed.** They are the scoreboard delta — `0b <slot + 1>
<21-byte mask> <values>` on the roster identifiers — with **bit 5 kills** and
**bit 6 deaths**. In `working-server-log` every one of the first six kill feeds
(`09 <killer> <victim>`) has its delta on the wire in the **same millisecond**:

| feed | delta | what it carries |
| --- | --- | --- |
| `t+12.375 09 07 02` | `t+12.375` slot `2` | deaths (bit 6) |
| `t+20.961 09 10 14` | `t+20.961` slot `16` | kills, headshots, score |
| `t+34.308 09 02 10` | `t+34.308` slot `2` | deaths, score |

The killer's slot takes bit 5 and the victim's bit 6, both at clock values in the
`0x09xx` range — the first at `t+12.3` of a 5-minute round. A room whose score
starts counting late is therefore not reading a masked clock: the clock carries
no flag, and the delta that carries a kill was on the wire at that instant.

**What our own host's capture shows instead.** `myserver.pcapng`'s clock opens on
`0x0954` (2 388, 4:58.5) — the capture begins mid-round — and passes `0x0780`
(4:00) at `t+65.193`. Its kill/death deltas are present from `t+31.7` and are
*byte-identical across consecutive frames* (`t+31.664`, `32.282`, `32.850`,
`33.466`: slot `5` mask `40 00 00`, slot `10` mask `20 01 00`) with the right
slots for their feeds — so the values are on the wire before any 4:00 crossing,
and what to look at when a client shows none is the stream's repeat and the
room's own rule time, not the clock's encoding.

**The T‑03:55 reading does not survive the measurement.** The hypothesis was that
a room whose settings say four minutes opens its clock at five, so the client only
starts counting when the count falls to the rule's own `4:00` — and our clock does
cross `4:00` 65 s in. Two measurements refuse it. First, **no measured room opens
above its rule**: the clock is the rule's own minutes × 480 in all five rooms of
five rules above, and `myserver`'s room states 300 s in its own body while its
clock runs the same `300 × 480` (`2 388` is merely the first value the capture
sees, because it begins mid-round), so there is no one-minute lead to fall through. Second, **nothing on the wire gates the count at 4:00**: in
`myserver` the first kill feed (`09 06 64 00 09 f7`) arrives at `t+18.383` with the
clock still at `2 388`, **79 of its 95 feeds** arrive while the clock is above
`1 920`, and the clock only reaches `4:00` at `t+76.066`; `working-server-log`
feeds from `t+12.3` at clock `0x09xx`. A scoreboard that starts late is therefore
a client display matter, not something the clock or the feed withholds.

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
* **What `0x18` is: the client's player-slot count.** The entry table is twenty-four
  records of `0x74` bytes at `instance + 0xD4`, and every walker loops twenty-four
  times (`P2P_RemovePlayerEntry` `0x26FC80`, `P2P_PublishPlayerRecord` `0x2753C8`,
  the run opener `0x270E00`). Six bytes of 24 in the state block are that number
  [I]; nothing on the wire names the field. **`0x3c` is still open** — sixty in
  every room of every rule. §2.
* **Which vector index the round result sits at.** Every capture writes
  `02 02 01 02`, so the body is settled; whether that offset names `state[2]`
  (direct base) or `state[0]` (`offset − 2`) is still open, because both readings
  are of the same bytes. §5.
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
