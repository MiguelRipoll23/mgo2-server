# The briefing run, field by field: live host against the captures

**Measured 30 October 2026.** What a join wrote against what the reference hosts
write, for the half of the run that the earlier comparisons did not cover: the
opening control, the join state answer, the state block's tail and the item-box
group. The instant of the run and its record list were compared in the same pass
and are repeated here so this one file holds the whole picture.

## 0. Sources and method

| | |
| --- | --- |
| live | pod `mgo2-gameplay-1-5d6fc5fcdd-hbxcs`, image pinned at `5966e3c` (the 2.0 s dwell), log window 12:01:06.459 – 12:01:11.100 |
| captures | `mgo2-started-on-game-round1.pcapng` (join mid-briefing, t+572.739), `mgo2-game-started-on-briefing.pcapng` (fresh room, t+58.255), `mgo2-started-briefing-then-solo-game.pcapng` (solo, t+57.626) |
| parsing | `tools/udp_session_records.py` and `tools/udp_frame.py` for all three captures |

The capture side is bytes on the wire. The live side is weaker: the pod log
prints each frame's records as `identifier/length/fourth byte` and never the
body. Every live body below is therefore marked **derived** where it follows from
the service that wrote it, and **measured** where the log states it outright. The
fourth byte (`ordinal=…` in the log) is measured everywhere.

## 1. The run's instant

| session | client's entry | run written | gap |
| --- | --- | --- | --- |
| round1 (joined a briefing in progress) | t+572.649 | t+572.739 | **0.09 s** |
| started-on-briefing (fresh room) | t+56.360 | t+58.255 | **1.90 s** |
| started-briefing-then-solo (fresh room) | t+55.312 | t+57.626 | **2.31 s** |
| live | 12:01:06.463 | 12:01:08.476 | **2.01 s** |

The run now follows the entry, which is what the dwell was for. The room-entry
answer (lobby-slot pair, countdown, settings header) followed at 12:01:10.762,
4.30 s after the entry.

One fact the same window shows and the captures do not: the live client **re-sent
its whole entry pair** at 12:01:08.335, 1.87 s after the first (`0x9001` ordinal 4
and `0x1001` ordinal 5 again, the relayed `0x1af3` at ordinal 1). Round1's client
sends its entry once, because that host's run answers it 0.09 s later.

## 2. The run's record list

Round1, t+572.739 — ten records:

| # | identifier | fourth byte | length | body |
| --- | --- | --- | --- | --- |
| 1 | `d001` | 121 | 1 | `05` |
| 2 | `5001` | 122 | 0 | — |
| 3 | `1254` | 0 | 90 | state block, tail `… ff 60 09` |
| 4 | `1254` | 1 | 2 | `0f 00` |
| 5 | `1001` | 57 | 26 | `85 96 fe ff …` |
| 6 | `1001` | 58 | 26 | `86 96 7f 7f …` |
| 7 | `025c` | 6 | 4 | `00 00 00 00` |
| 8 | `1001` | 59 | 20 | `07 0d 00 02 00 …` |
| 9 | `1001` | 60 | 20 | `07 0d 00 02 01 …` |
| 10 | `1001` | 61 | 7 | `07 00 00 00 00 00 03` |

Started-on-briefing, t+58.255 — fourteen records:

| # | identifier | fourth byte | length | body |
| --- | --- | --- | --- | --- |
| 1 | `5001` | 12 | 0 | — |
| 2 | `5001` | 13 | 0 | — |
| 3 | `1254` | 0 | 90 | state block, tail `… ff 00 0f` |
| 4 | `1254` | 1 | 2 | `0f 00` |
| 5 | `1001` | 8 | 26 | `85 00 fe ff …` |
| 6 | `1001` | 9 | 26 | `86 00 7f 7f …` |
| 7 | `025c` | 6 | 4 | `02 00 00 00` |
| 8 | `1001` | 10 | 20 | `07 0d 00 02 00 …` |
| 9 | `1001` | 11 | 20 | `07 0d 00 02 01 …` |
| 10 | `1001` | 12 | 7 | `07 00 00 00 00 00 03` |
| 11 | `1250` | 0 | 6 | `00 00 00 00 00 00` |
| 12 | `1250` | 1 | 6 | `01 00 00 00 00 00` |
| 13 | `125a` | 0 | 6 | `00 00 00 00 00 00` |
| 14 | `125a` | 1 | 6 | `01 00 00 00 00 00` |

Live, 12:01:08.482 (frame 53) — fourteen records:

| # | identifier | fourth byte | length | body |
| --- | --- | --- | --- | --- |
| 1 | `0xd001` | 4 | 1 | **derived**: `BriefingControlByte` `03` |
| 2 | `0x5001` | 5 | 0 | — |
| 3 | `0x1a54` | 0 | 90 | state block, tail `… ff 20 0d` (derived) |
| 4 | `0x1a54` | 1 | 2 | `0f 00` |
| 5 | `0x9001` | 8 | 26 | array pair, first |
| 6 | `0x1001` | 9 | 26 | array pair, second |
| 7 | `0x0a5c` | 6 | 4 | **derived**: `02 00 00 00` |
| 8 | `0x1001` | 10 | 20 | close |
| 9 | `0x1001` | 11 | 20 | close |
| 10 | `0x1001` | 12 | 7 | `07 00 00 00 00 00 03` |
| 11 | `0x1a50` | 0 | 6 | `00 00 00 00 00 00` |
| 12 | `0x1a50` | 1 | 6 | `01 00 00 00 00 00` |
| 13 | `0x1a5a` | 0 | 6 | `00 00 00 00 00 00` |
| 14 | `0x1a5a` | 1 | 6 | `01 00 00 00 00 00` |

The live run is started-on-briefing's fourteen records in its order, at its
fourth bytes. The only difference in the list is the opening pair: ours opens
`d001` + empty `5001` where that capture opens two empty `5001`. That is the
measured branch, not a deviation — a run opens on the **entry echo answered**
when the host heard one (round1 did, and opens `d001` + `5001`), and on the two
recorded empty heads when it did not.

The identifiers differ only by generation: round1 and started-on-briefing write
the sibling set (`1254`, `1250`, `125a`, `025c`), this round writes the first set
(`1a54`, `1a50`, `1a5a`, `0a5c`), as `MatchRecordGenerationUtils` maps them.

## 3. `0xd001` — the opening control

| | fourth byte | length | body |
| --- | --- | --- | --- |
| round1 | 121 | 1 | `05` |
| started-on-briefing | *(absent — no echo in that pass)* | | |
| live | 4 | 1 | `03` (derived) |

**The body is the host's own and is not matchable.** The measured values across
the set are `24` and `25` (`mgo2-game1`), `24` (`mgo2-game3`), `0c` and `0a`
(`started-on-briefing`), `05` here. Only its *place* is general: one byte, first
record, in the pass a run is written. Ours writes the measured briefing value
because the briefing generation existed by 12:01:06.474 (`ControlByteForAnswers`).

**The fourth byte is a real divergence, and is the only one in this record.**
Both reference runs number the opening pair from the answering host's own counter
— 121/122 in round1, 12/13 in started-on-briefing, sitting past that run's 8–12 —
while we copy the echo's own fourth byte (`AcknowledgementOf` sets
`message.Flags`), giving 4/5. The client acknowledged every record of the frame,
so this did not stop it.

## 4. `0x025c` — the join state answer

| | fourth byte | length | body |
| --- | --- | --- | --- |
| round1 | 6 | 4 | `00 00 00 00` |
| started-on-briefing | 6 | 4 | `02 00 00 00` |
| live | 6 | 4 | `02 00 00 00` (derived) |

**This record matches, in position, fourth byte, length and first byte.**

Round1's zeros are not a counter-example. That capture carries **no inbound
`0x025c` presence record at all** — its client joins mid-round and never sends the
one-byte record — so that host had no slot to set and wrote the empty set.
`started-on-briefing`'s client sends `025c 01` beside `e1 01` and is answered
`02`. Our client reported `1` (log 12:01:06.468, *“reports its map-loaded state
with 1: 1 of 1 players have reported, at publication index 0”*), so `JoinedMask`
is `1 << 1` and the body is `02 00 00 00`.

## 5. The state block's tail (bytes 85–88 of the 90-byte block)

| source | tail | 16-bit value | units | room |
| --- | --- | --- | --- | --- |
| started-on-briefing, all six blocks (t+58.255, 60.383, 60.384, 65.124, 198.371, 200.869) | `00 ff 00 0f` | `0x0f00` | 3840 | 8-minute rounds |
| round1 t+10.265 (joined mid-round) | `00 ff ab 05` | `0x05ab` | 1451 | the round it joined, part-spent |
| round1 t+215.632, 572.739, 601.404, 1285.939, 1353.284 | `00 ff 60 09` | `0x0960` | 2400 | 5-minute rounds |
| live 12:01:08.482 | `00 ff 20 0d` (derived) | `0x0d20` | 3360 | the log's “3360 of 3360 units of its 7-minute clock” |

Bytes 87–88 are the **round clock, seeded with that room's own length**; byte 86
is the `ff` sentinel; byte 85 is the last of the `state[61..85]` zero run. This is
what `MatchStateBodiesUtils.MatchStartStateBlock` writes, and the value cannot be
equal across rooms — 8 minutes, 5 minutes and 7 minutes here. Round1's `ab 05` is
the same field in a run that began mid-round, which is why it is the one block of
its session that is neither 2400 nor a fresh seed.

The block appears **only in runs** in both captures — the beat's clock is the
separate 7-byte record — and ours follows that: `0x1a54` at 90 bytes only in the
run frame, at 7 bytes in the beat.

## 6. The item-box group (`1250`/`125a`, ours `1a50`/`1a5a`)

**Bodies and numbering match.** Every group in both captures carries
`00 00 00 00 00 00` then `01 00 00 00 00 00` under both identifiers, all round
long — the filler pair, not a box event, which is what
`ItemBoxRecordBodiesUtils.ItemBoxState(index)` writes. The sequence is the run's
group at 0/1, then 2/3, then 4/5, then the repeat from 6/7.

**The order does not match.** The capture writes the run frame (counter 88,
group 0/1) and only then the two extra groups: counter 89 at 2/3 and counter 91
at 4/5, with the `0261 fe` pair between them. The live host has group 2/3 in frame
**51** (12:01:08.479927) and group 4/5 in frame **52** (12:01:08.482984), both
ahead of the run frame **53** (12:01:08.482990). The client therefore sees 2/3,
4/5, then 0/1 — the reverse of every capture. `WriteItemBoxGroupsAsync` sends
groups 1 and 2 inline and only attaches group 0 to the run its caller sends
afterwards.

**The repeat's wake is thinner.** At t+60.383 the reference writes four climbing
groups in one burst (6/7, 8/9, 10/11, 12/13), and each of those frames also
carries a second cycle numbered 0…23, so its client answers
`5250@0…@23`/`525a@0…@23` — 164 records of each identifier over eight seconds.
Ours writes three (6/7, 8/9, 10/11) at 12:01:10.833 and reaches 12/13 only at
12:01:11.100, from the 264 ms continuation timer.

Nothing is going unanswered: this client acknowledged every group we sent, 0/1
through 12/13, within 40 ms of each frame, and 176 records in total. The window
is draining, so the item-box stream is not what left the screen black.

## 7. What is left open

| finding | status |
| --- | --- |
| run composition and fourth bytes | match started-on-briefing's fourteen |
| run instant after the entry | fixed (2.01 s); capture's range 1.90–2.31 s, but its mid-briefing join answers in 0.09 s |
| `0x025c` body | matches |
| state-block tail | matches the rule; each room's own clock |
| `0xd001` body | host-specific; not matchable |
| `0xd001`/`0x5001` fourth bytes | ours are the echo's, the reference's its own counter — unverified as a cause |
| item-box send order | **fixed**: the run frame goes out first, then groups 2/3 and 4/5 (`MatchStartRunService.BuildItemBoxGroups`) |
| item-box repeat burst | three groups against the capture's four; the fourth arrives 267 ms late |
| client re-sending its entry after 1.87 s | the reference client never does when the same pass answers it (0.09 s) |

No capture was recorded of the failing join itself, so every live figure here is
the host's log side of it.
