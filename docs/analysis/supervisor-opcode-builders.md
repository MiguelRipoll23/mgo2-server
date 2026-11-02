# The supervisor's per-opcode builders, and what 0x07, 0x08, 0x11 and 0x1b carry

**What this file settles.** Four operations-stream opcodes the corpus carries but no
document named were traced to the routines that build their bodies in `MGO2.ELF`, and
each one's shape is now read off the code rather than inferred from a sample. It also
corrects a counting error: the fifteen-byte `11 …` bodies the corpus carries are **not**
operations records at all.

| source | where |
| --- | --- |
| image | `C:\Users\migue\Desktop\MGO2RE\MGO2.ELF` (19 615 992 bytes) |
| the builders | `0x754570`–`0x7558xx`, the same supervisor module as the known `0x1f` (`0x7551A4`), `0x22` (`0x754308`) and `0x0c`/`0x0d`/`0x20` writers |
| append and send | `0x26CC10(cursor, source, count)` appends; `0x2784B0(handle, cursor)` sends |
| the writer's own slot | `0x26D588()` returns `byte(instance + 0xC8)` |
| captures | `mgo2-started-on-game-round1.pcapng` (`0x07`, `0x08`, `0x1b`), the whole corpus for the others |

Every builder below follows one shape: a cursor over a stack buffer, one `li`/`stb` of the
opcode, one append per field, then the send. A body's length is therefore the opcode plus
the number of appends, and a field's width is the count passed to each append.

## 1. The builders

| opcode | builder | body it appends | measured sample |
| ---: | ---: | --- | --- |
| `0x01` | `0x754848` | `01` + the `u16` at the record pointer's `+0` — the round clock | `01 20 0d` |
| `0x03` | `0x7549A0` | `03` + the writer's own slot, taken when `0x26D540()` is nonzero | — |
| `0x07` | `0x7557CC`, sibling `0x756284` | `07` + three single bytes (`r4`, `r5`, `r6`) | `07 05 0b 00` |
| `0x08` | `0x7556FC`, sibling `0x75639C` | `08` + two single bytes, each clamped into `0…0xff` | `08 04 02` |
| `0x0f` | `0x754710` | `0f` + one byte | `0f 00` |
| `0x11` | `0x754570` | `11` + two single bytes (`r4`, `r5`) | none in the corpus |
| `0x17` | `0x7549A0` | `17` + the writer's own slot, the other arm of `0x03` | — |
| `0x1A` | `0x754B50` | `1a` + a byte, the writer's own slot, another byte | — |
| `0x1B` | `0x754A90` | `1b` + the writer's own slot, when the caller's argument is `1` | `1b 11` |
| `0x1C` | `0x754A90` | `1c` + the writer's own slot — the same body for any other argument | — |
| `0x24` | `0x7547B0` | `24` + one byte | — |
| `0x26` | `0x7548F0` | `26` + two single bytes (`r4`, `r5`) | `26 02 06` |

Two of the four named here are settled beyond their shape:

* **`0x1B` names the writer's own slot. [V]** The body is two bytes and its second is
  `0x26D588()`, the routine the `0x1f` and `0x22` writers also lead with. The capture's
  `1b 11` comes from the client whose slot is `0x11`. The argument `1` is what selects
  `0x1b` over its `0x1c` sibling; the caller that passes `1` is `0x778BC8`, at the end of
  a loop over the session's players (`0x74FE10`/`0x74FEB0` look each one up, bit
  `0x24000` is tested, and a state pair is written at `r22+0x0E`/`+0x0F` before the
  record). What the record tells the host is **[U]**: the client's own state table stops
  at `0x19` (`docs/protocol/STATE_VECTOR.md` §1) and the parser has no entry.
* **`0x26` names a subject's slot and a signed byte, about someone who is not the
  writer. [V]** Both callers (`0x744284`, `0x7444C8`) compare the record's slot against
  `0x26D588()` and skip the send when they are equal; the slot goes out as the low byte and
  the value as a signed byte. The capture's `26 02 06` is therefore slot 2, value 6, sent
  by the host to a client about slot 2. The gates around it are read and not named: a
  flag from `0x7501A8()`, and a value of at most three from `0x750108()` for a
  non-negative value.

## 2. `0x07` and `0x08` are three- and two-argument records

Neither has a direct caller anywhere in the image — the same is true of the other
builders here, which are reached through their OPDs — so the arguments are read from the
body and from the instant the capture writes it:

| record | instant in `mgo2-started-on-game-round1` | what the host had just written |
| --- | --- | --- |
| `07 05 0b 00` | t+1 353.344 | `state[8] = 0x0f`, `state[13] = 1`, `state[15] = 5`, with the countdown at 14 s and the ready set `0x0003` |
| `08 04 02` | t+1 368.348 | `state[8] = 0`, `state[13..15] = 0`, the countdown at zero — and the round's boxes 0.4 s later |

So `0x07` is `07 a b c` with three byte fields and `0x08` is `08 a b` with two, and the
second arrives as the briefing ends. **What the fields mean is [U]**: one sample each, no
reading in the client's tables, and the values are small enough to be teams, phases or
counts. This host does not write either, and a body invented from one sample would be a
guess written onto the wire.

## 3. `0x11` is real, and the corpus's `11 …` bodies are not it

`0x754570` builds `11` + two bytes, so the opcode exists. The fifteen-byte bodies that a
low-byte filter reports as `0x11` records are something else entirely: they travel on
`0x0854`, whose block is another player's — state `0x0857`, health `0x0858`, position
`0x0859`, weapon `0x085D`, aim `0x085B`-family — and `0x0854` is that block's **damage**
slot, five below the position record, exactly the role the replay grammar gives
`VID_DAMAGE`. The body is a `TYPE_DAMAGE` payload (`11 00 a5 14 19 4F 07 25 00 F7 09 48
00 27 CB`), not an operation.

A reader who counts opcodes by the identifier's low byte will pick these up; the correct
test is the identifier *with its class bits*, which for the operations stream is
`0x1254`/`0x1a54` and their `0x800` and `0x8000` forms. That is the filter
`tools/udp_session_records.py` and `tools/udp_record_semantics.py` apply, and the one the
corpus counts in `docs/protocol/RPDT_REPLAY_RECORDS.md` now use.
