# Three unknown bytes, read from the two solo captures

**What this file is.** Three readings the corpus carried as **[U]**, measured on the
two captures of one room each — `briefing-then-solo-game.pcapng` (host
`64.181.216.250:5720`, 231 s, client slot `0x0a`) and `myserver.pcapng` (host
`89.129.16.203:5731`, 78 s, client slot `0x01`) — and checked against what the
client's own binaries do with the records they ride in.

**How it was measured.** Every datagram of each session was decoded with the
session key derived from the two handshake bases, and each record family's bodies
were listed in time order with the client's own report (`e1 <slot>`) and ready
(`1f <slot> <state>`) records beside them, so a byte is tied to the event that
moved it rather than to a plausible story. The tool is
`tools/udp_state_vector.py`'s `session_records`, which is the same reader every
other tool in `tools/` uses. The client side is
`C:\Users\migue\Desktop\pc-client`: `system.dll`, `mgo2.exe` and `savemgo.dll`
carry no reader for these record identifiers, which
`roster-record-kinds.md` §7 already establishes, so where a client reading is
quoted below it is the ELF dispatch that file reads.

## 1. The countdown's ready set: two bytes, little-endian, one bit per readied slot — [V]

The seven-byte briefing countdown is `0b [seconds LE 2] [set LE 2] [00] [00]`
(`BriefingCountdownBodiesUtils`). The set's width and byte order are settled by a
session whose ready is at slot **10**, which no earlier reading had:

| capture | client's report | client's ready | closing countdown |
| --- | --- | --- | --- |
| `briefing-then-solo-game` | `e1 0a` at t+8.696 | `1f 0a 01` at t+144.244 | `0b 00 00 00 04 00 00` at t+145.098 |
| `myserver` | `e1 01` at t+9.358 | none | every record `0b 3c 00 00 00 00 00` |

Read little-endian the first set is `0x0400`, which is bit 10 — the slot the
client's own ready record named. Read big-endian it would be bit 2, which is
nobody in that room. So three things are measured at once:

* the set is **two bytes little-endian** at body offsets 3 and 4;
* its **bit is the reporting player's own slot**, not its place in any
  publication — the same rule `BriefingStateService.ReadyMask` implements;
* a slot above 7 has a bit in the **second** byte and not the first, which is the
  width the record has room for (slots 1–15).

`myserver`'s day is the negative control: a room whose only client never readied
writes the set as `00 00` on all fourteen of its ticks, with the count frozen at
`3c`. The ready, and nothing else, is what moves those two bytes.

`BriefingCountdownBodiesUtils.CountdownBody` already writes `(byte)(set & 0xff)`
then `(byte)(set >> 8)`, so the code agrees with the measurement; a host that
wrote the pair in the other order would name a different player for every ready
above slot 7.

## 2. The countdown's reserved bytes: offset 2 is zero in every record — [V] for this corpus, [U] what it could carry

The body's byte 2 is `0x00` on **all sixteen** countdown records of the two
captures, including the closing record that carries a nonzero set
(`0b 00 00 00 04 00 00`). That rules out the two readings an earlier draft of
`BRIEFING_FLOW.md` §1 left open for it: it is not a third set byte (a 24-bit set
would put slot 10's bit in a different byte than measured above), and it is not a
count of readies — the closing record of `briefing-then-solo-game` has one ready
and still writes zero there.

So byte 2 is a reserved byte in the two rooms this file covers, and what a host
could put in it stays **[U]**. The byte that *does* move in
`mgo2-started-on-game-round1` — the sixth, `0b 22 00 00 08 02 00` — is offset 5,
not this one, and it stayed zero here even at the ready
(`0b 00 00 00 04 00 00`): if it counts readies, the closing record is not where
the count is written, and nothing in these two captures moves it at all.

## 3. The join-state answer: an eight-bit slot set, and three constant zeros — [V]

The answer is the four-byte `0x025c` / `0x0a5c` body. Both captures write it in
the run a peer is answered with, and `myserver` also writes it in the pass that
received the report:

| capture | when | body | what it follows |
| --- | --- | --- | --- |
| `myserver` | t+9.374 | `02 00 00 00` | the report `e1 01` at t+9.358 |
| `myserver` | t+12.268, t+14.631 | `00 00 00 00` | the two briefing runs |
| `briefing-then-solo-game` | t+9.780 | `00 00 00 00` | the report `e1 0a` at t+8.696 |
| `briefing-then-solo-game` | t+147.650 | `00 00 00 00` | the round's run |

Byte 0 is `1 << reported slot` over slots 1–7: a slot-1 report answers `0x02`, and
a slot-10 report answers `0x00` because an eight-bit set has no bit 10. That is
exactly `BriefingStateService.JoinedMask`, and these two sessions are the two ends
of it — one that sets a bit and one whose slot cannot.

The three trailing bytes are **zero in all four copies**, including the one whose
first byte is nonzero. That settles the shape of what the earlier reading left
open: they are not a continuation of the set into 32 bits (which `02 00 00 00`
would have to be `00 00 00 02` for under a little-endian 32-bit mask) and not a
count that tracks the set. Whether they are always zero, or carry something in a
room of many players, stays **[U]** — the six-player captures do not ship here.

## 4. Records that are identical across two unrelated rooms — [V] they are not per-session

Both captures write these bodies byte for byte, on different hosts, three days
apart, with different clients and slots:

```
07 0d 00 02 00 00 00 00 02 d2 5a 00 00 00 e0 8f 7e 00 00 00     kind 0x02 close pair
07 0d 00 02 01 00 00 01 02 34 a4 1f 00 00 18 6e 01 00 00 00     kind 0x02, second index
07 0f 00 03 00 00 00 00 84 03 84 03 3e fd 84 03 84 03 3e fd fd 00   kind 0x03 stage pair
07 0f 00 03 01 00 00 01 3e fd 7d 03 07 03 3e fd 7d 03 07 03 fd 00
```

Both rooms are on the same map, so this is what `roster-record-kinds.md` §8 leaves
open read from the other side: the pair is a **stage fact and not room state**, and
a host may replay the recording's bytes for that stage (`RoomStateRecordBodiesUtils`
does). The two records are also the ones a second room cannot distinguish, so
nothing a room's players do reaches them.

## 5. What the client binaries add, and what they do not

`system.dll` (19.8 MB, linked at `0x10000000`) has no reader for these three
identifiers — it is the supervisor's engine build, and the record dispatch it does
hold is the ELF's, not a second copy (`roster-record-kinds.md` §7). The two
readings that are available from the client side are the ones already taken there:
the key-`07` reader at `0x282D80` consumes the record's `+1`…`+6` and hands the
payload at `+7` to a per-kind node, and the `0x0b` operations records have no
static reader in the dispatch table at all. So:

* nothing in the client image compares the countdown's reserved byte, which is why
  a value there is a host's choice and not a protocol rule;
* nothing in it compares the join-state answer's trailing bytes either, for the
  same reason;
* and the countdown set's bits are read by the briefing screen's own state, which
  is where `briefing-state-machine.md` already located the gating
  (`0x3181b038`), not by a record parser.

## 6. Open

* countdown offsets 2 and 5 — zero in every record here, and offset 5 moves only in
  `mgo2-started-on-game-round1`, which is not in this tree;
* the join-state answer's trailing three bytes in a room of more than one slot
  below 8;
* the close pair's eight-byte value, which §4 shows is a stage fact and which
  `roster-record-kinds.md` §8 still needs a live node dump to read.
