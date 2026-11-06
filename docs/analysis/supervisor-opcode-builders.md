# The supervisor's per-opcode builders, and what 0x07, 0x08, 0x11 and 0x1b carry

**What this file settles.** Four operations-stream opcodes the corpus carries but no
document named were traced to the routines that build their bodies. The bodies are now
read off two images rather than inferred from a sample: the PS3 module that the corpus
traffic came from (`MGO2.ELF`), and the **native port**, whose whole supervisor network
module survives as a readable x86 family. The port is the stronger source — it keeps the
file name, the line numbers and the argument count of every builder — and it also shows
that the two revisions do **not** number every opcode the same, which the last section
spells out.

| source | where |
| --- | --- |
| PS3 image | `C:\Users\migue\Desktop\MGO2RE\MGO2.ELF` (19 615 992 bytes) |
| the port | `C:\Users\migue\Desktop\pc-client\system.dll`, PE32, base `0x10000000` |
| the port's module | `.\online\supervisor\net\sv_net.cc` — 0x10fda0bc's string, its assertions at 0x101f0021 (`0x5af`), 0x101f0047 (`0x5b0`), 0x101ef7e9 (`0x2e0`), 0x101efc46 (`0x47b`), 0x101efd26 (`0x4a7`), 0x101efe9e (`0x4f5`) |
| the builders (ELF) | `0x754570`–`0x7558xx`, with the known `0x1f` (`0x7551A4`), `0x22` (`0x754308`) and `0x0c`/`0x0d`/`0x20` writers |
| the builders (port) | `0x101ef340`–`0x101f0b00`, one function per record, each `sub esp,0x114` |
| append and send (ELF) | `0x26CC10(cursor, source, count)` appends; `0x2784B0(handle, cursor)` sends |
| append and send (port) | `0x1009d110(cursor, buffer, limit)` opens a 0x100-byte body; `0x1009d150(count, source)` appends one field; `0x1009d1b0(count, source)` appends a run; `0x1009ea50(writer, cursor)` hands the finished body to the `0x254` stream writer opened by `0x1009e8d0` |
| the writer's own slot | ELF `0x26D588()` = `byte(instance + 0xC8)`; port `0x1004f130` = `byte(global->+0xC8)` — the same offset |
| the host flag | port `0x1004f110` = `global->[0x10AC] & 1` |
| the adjacent byte | port `0x1004f120` = `byte(global->+0xC9)` |
| captures | `mgo2-started-on-game-round1.pcapng` (`0x07`, `0x08`, `0x1b`), the whole corpus for the others |

Every builder follows one shape: a cursor over a stack buffer, one store of the opcode,
one append per field, then the send. A body's length is therefore the opcode plus the
number of appends, and a field's width is the count passed to each append. In the port
the same shape is visible in the machine code, and the third thing the port adds is the
**argument count of the builder itself** (`ret 0xc` = three stack arguments), which names
the fields the body carries even where their meaning is still open.

## 1. The builders

| opcode | builder (ELF) | builder (port) | body it appends | measured sample |
| ---: | ---: | ---: | --- | --- |
| `0x00` | — | `0x101f0b00` | `00` + 62 bytes from `(this+0x84)+2` + the `u16` at `this+0x84`; copies the same 64 bytes to `this+0x88` | the 90-byte state block |
| `0x01` | `0x754848` | `0x101ef8d0` | `01` + the `u16` at `(this+0x84)`, then stores that word at `this+0x88` — the round clock | `01 20 0d` |
| `0x02` | — | `0x101ef960` | `02` + offset + count + the `count` bytes at `(this+0x84)+offset` | `02 2a 04 1e 1e 1e 1e` |
| `0x03` | `0x7549A0` | — | `03` + the writer's own slot, taken when `0x26D540()` is nonzero | — |
| `0x04` | — | `0x101efa40` | `04` + two single bytes | — |
| `0x05` | — | `0x101efb20` | `05` + the member; when the member is the writer's own it raises script event `0x15000f` instead | `05 …` |
| `0x06` | — | `0x101efdb0` | `06` + one byte, host-only | — |
| `0x07` | `0x7557CC`, sibling `0x756284` | `0x101efc20` | `07` + three single bytes, host-only, three arguments (`ret 0xc`) | `07 05 0b 00` |
| `0x08` | `0x7556FC`, sibling `0x75639C` | `0x101efd00` | `08` + one byte that **packs two fields**: `(a << 4) \| (b & 0x0f)`, host-only, two arguments | `08 04 02` |
| `0x09` | — | `0x101efe60` | `09` + two single bytes, host-only — killer and victim | `09 <killer> <victim>` |
| `0x0a` | — | `0x101f00e0` | `0a` + one byte | — |
| `0x0b` | — | `0x101f01e0` | `0b` + two single bytes | `0b 3c 00 00 00 00 00` |
| `0x0c` | — | `0x101f0350` | `0c` + six single bytes; the same function builds `0x0d` and `0x1d` on other arms | — |
| `0x0e` | — | `0x101f0510` | `0e` + one byte | — |
| `0x0f` | `0x754710` | `0x101f0670` | `0f` + one byte | `0f 00` |
| `0x10` | — | `0x101f0720` | `10` + one byte, reached from the state block's builder when `0x100a1670()` is `0xA` | — |
| `0x11` | `0x754570` | **absent** | `11` + two single bytes in the PS3 image; no function in the port writes `0x11` | none in the corpus |
| `0x12` | — | `0x101ef340` | `12` + one byte, and the builder reads a pair back | — |
| `0x13` | — | `0x102ba9a0` | `13` + one byte | — |
| `0x15` | — | `0x101ef3f0` | `15` + two single bytes | — |
| `0x16` | — | `0x101ef4c0` | `16` + two single bytes | — |
| `0x17` | `0x7549A0` | `0x101ef590` | `17` + the writer's own slot, the other arm of `0x03`; the port's version appends three bytes | — |
| `0x18` / `0x19` | — | `0x101ef670` | `18` when the caller's argument is `1`, `19` otherwise, + the writer's own slot | — |
| `0x1a` | `0x754B50` | **absent** | `1a` + a byte, the writer's own slot, another byte | — |
| `0x1b` | `0x754A90` | `0x101f0000` | the PS3 body is `1b` + the writer's own slot; the port's is `1b` + **a member** (asserted `< 0x18`: "invalid member %d") + one byte | `1b 11` |
| `0x1c` | `0x754A90` | `0x101f0290` | the same body under the other arm | — |
| `0x1e` | — | `0x101f05d0` | `1e` + one byte | — |
| `0x1f` | `0x7551A4` | `0x101ef7a0` | `1f` + the subject slot + a state byte; the port compares the slot with the writer's own and logs `to me? %d` | `1f 0a 01`, `1f 05 01` |
| `0x20` | — | `0x101b6880` | `20` + three single bytes | — |
| `0x24` | `0x7547B0` | `0x10153770` | `24` + one byte | — |
| `0x26` | `0x7548F0` | `0x101b5e20` | `26` + two single bytes in the PS3 image; the port's version appends one | `26 02 06` |

**`0x02`'s offset and `0x00`'s vector are not on the same base, and the two
bytes between them are the clock. [V]** The write's builder reads
`mov edi,[ebx+0x84]`, `movzx ecx,al`, `add edi,ecx` and appends `count` bytes from
there (`0x101ef999`–`0x101efa0b`), while the state block's builder appends 62
bytes from `[ebx+0x84]+2` and then the `u16` at `[ebx+0x84]`
(`0x101f0b41`–`0x101f0b63`). A write at `offset` therefore names the block's
`offset - 2`, which the captures bear out: the write at `0x28` in
`mgo2-game-failed-memoryviolation` moved exactly `state[38..39]` of the next block
(t+77.235 against t+82.229), and `briefing-then-solo-game`'s
`02 2a 04 1e 1e 1e 1e` names `state[40..43]` — the two objective holders and the
two capture clocks, unclaimed (`STATE_VECTOR.md` §5).

Two of the four named here are settled beyond their shape:

* **`0x1B` is the writer's own slot in the captured revision. [V]** The PS3 body is two
  bytes and its second is `0x26D588()`, the same `byte(instance + 0xC8)` the `0x1f` and
  `0x22` writers lead with, and the port reads that exact offset with `0x1004f130` in the
  builder that plays this role (`0x101ef670`, which picks `0x18`/`0x19` — see §4). The
  capture's `1b 11` comes from the client whose slot is `0x11`. The argument `1` is what
  selects the record over its sibling; the caller that passes `1` is `0x778BC8`, at the
  end of a loop over the session's players (`0x74FE10`/`0x74FEB0` look each one up, bit
  `0x24000` is tested, and a state pair is written at `r22+0x0E`/`+0x0F` before the
  record). What it announces is still **[U]**: the client's own state table stops at
  `0x19` (`docs/protocol/STATE_VECTOR.md` §1) and the parser has no entry.
* **`0x26` names a subject's slot and a signed byte, about someone who is not the
  writer. [V]** Both callers (`0x744284`, `0x7444C8`) compare the record's slot against
  `0x26D588()` and skip the send when they are equal; the slot goes out as the low byte and
  the value as a signed byte. The capture's `26 02 06` is therefore slot 2, value 6, sent
  by the host to a client about slot 2. The gates around it are read and not named: a
  flag from `0x7501A8()`, and a value of at most three from `0x750108()` for a
  non-negative value.

## 2. `0x07` is three independent byte fields, `0x08` two

**`0x07` is [V] on its width and [U] on what the three bytes are.** The port's builder
(`0x101efc20`) takes three byte arguments and appends each one after the opcode — the
dispatch at `0x101f07f0` reaches it with `ret 0xc` — and it is **host-only**: the builder
asserts `0x1004f110()` (the host flag) before it writes, with `sv_net.cc` line `0x47b`.
The capture's `07 05 0b 00` is the same four bytes, written by a host at t+1 353.344
right after `state[8] = 0x0f`, `state[13] = 1`, `state[15] = 5`, with the countdown at
14 s and the ready set `0x0003`.

**`0x08` carries the same two fields the port packs into one byte. [V] width, [U] meaning.**
The port's builder (`0x101efd00`) appends one byte computed as
`(arg0 << 4) | (arg1 & 0x0f)`, host-only (line `0x4a7`), so the two arguments are four-bit
quantities. The captured revision does not pack them: it writes `08 04 02` — the same two
small values as two plain bytes — at t+1 368.348, `state[8] = 0`, `state[13..15] = 0`, the
countdown at zero and 0.4 s before the round's boxes. A host writing this record for a
captured client must send **three** bytes, `08 04 02`, not the port's packed two.
Neither the port nor the PS3 image names the pair; they are small enough to be teams,
phases or counts, and a body invented from one sample would be a guess written onto the
wire.

## 3. `0x11` is real in the PS3 image and absent from the port

`0x754570` builds `11` + two bytes, so the opcode exists in the PS3 revision. The port's
supervisor module has **no** builder for it: of the 1 248 functions in `system.dll` that
open a `0x100`-byte body and append at least one field, every opcode from `0x00` to `0x2A`
the port writes is accounted for in the table above except `0x03`, `0x11` and `0x1A`. So
`0x11` is a record of the PS3 revision that the port's revision dropped, and the corpus
carries none — the two images and the captures agree that this server does not have to
write it.

The fifteen-byte bodies that a low-byte filter reports as `0x11` records are something
else entirely: they travel on `0x0854`, whose block is another player's — state `0x0857`,
health `0x0858`, position `0x0859`, weapon `0x085D`, aim `0x085B`-family — and `0x0854`
is that block's **damage** slot, five below the position record, exactly the role the
replay grammar gives `VID_DAMAGE`. The body is a `TYPE_DAMAGE` payload
(`11 00 a5 14 19 4F 07 25 00 F7 09 48 00 27 CB`), not an operation.

A reader who counts opcodes by the identifier's low byte will pick these up; the correct
test is the identifier *with its class bits*, which for the operations stream is
`0x1254`/`0x1a54` and their `0x800` and `0x8000` forms. That is the filter
`tools/udp_session_records.py` and `tools/udp_record_semantics.py` apply, and the one the
corpus counts in `docs/protocol/RPDT_REPLAY_RECORDS.md` now use.

## 4. The two revisions do not number every record the same

The port is the PS3 game rebuilt for x86, not a different protocol, and the records that
matter most agree exactly: `0x00` is the state block (the port appends 62 bytes and the
clock pair, the capture sends 90 bytes), `0x01` is the `u16` round clock in both,
`0x02` is the state write with offset, count and values, `0x09` is killer and victim,
`0x0f` is one byte, `0x1f` is slot and state. Three records disagree by exactly the width
of one field, and each disagreement is a **revision** difference rather than a reading
error, because the port's own assertion text and argument counts are what changed:

| record | capture (PS3 traffic) | port | reading |
| --- | --- | --- | --- |
| `0x07` | `07 a b c` (4) | `07 a b c` (4), host-only | the same record; the port adds the host-only assertion, not a field |
| `0x08` | `08 a b` (3) | `08 (a<<4 \| b&0x0f)` (2), host-only | the port packs the same two four-bit fields into one byte |
| `0x1b` | `1b <ownSlot>` (2) | `1b <member> <byte>` (3), member `< 0x18` | the port's `0x1b` names a **subject**, so its own-slot record moved to `0x18`/`0x19` (`0x101ef670`: `18` when the argument is `1`, `19` otherwise, then `0x1004f130`) |
| `0x26` | `26 <slot> <value>` (3) | `26 <value>` (2) | the port drops the subject byte the PS3 image and the capture both carry |

So the port settles *what kind of field* each argument is — a member, a slot, a four-bit
code — and it does **not** license copying its opcode numbers onto the wire. Our host
follows the captures: `07 05 0b 00`, `08 04 02`, two-byte `1b`, three-byte `26 02 06`.
