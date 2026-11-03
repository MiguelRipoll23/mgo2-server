# The 161-byte rotation payload over the whole corpus

**What this file is.** Every room-state payload of the captures in `.pcapngs/`
(fifteen of them write one) read field by field, so the bytes
`RoomRotationPayloadUtils.BuildCore` still writes as literals can be told apart
from the bytes that are a room's own. It is the measurement behind §1 and §2;
where a byte's meaning is still open it is left open rather than guessed.

**How it was measured.** Each capture's gameplay session was decoded with the
session key from its two handshake bases, the host's first `0x1001` record of 161
bytes was taken (`tools/udp_state_vector.py`'s `session_records`), and every
offset of the fifteen bodies was tabulated against the room facts the body itself
carries: its sixteen rotation triples, its population (`0x56`), its current
rotation slot (`0x55`) and its round phase (`0x09`).

| capture | slots | pop | rot | round | rules in the rotation | max map |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| `joined-final-round-solo-then-final-round` | 15 | 2 | 0 | 3 | 1,2,3,4,5,6,7,16 | 17 |
| `joined-on-briefing-then-game` | 15 | 2 | 0 | 2 | 4,15 | 19 |
| `joined-on-game-solo` | 6 | 2 | 3 | 3 | 1 | 18 |
| `mgo2-game-failed-memoryviolation` | 1 | 13 | 0 | 2 | 16 | 7 |
| `mgo2-game-joined-when-red-team-then-briefing` | 15 | 6 | 0 | 5 | 1 | 21 |
| `mgo2-game-started-on-briefing` | 6 | 2 | 0 | 2 | 1 | 10 |
| `mgo2-game-started-team-selection-selected` | 15 | 2 | 7 | 3 | 4,15 | 17 |
| `mgo2-game1` | 15 | 2 | 12 | 3 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-game2` | 15 | 2 | 0 | 2 | 7,15 | 17 |
| `mgo2-game3` | 15 | 2 | 13 | 3 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-joined-game-in-progress-last-round` | 15 | 4 | 14 | 4 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-started-briefing-then-solo-game` | 15 | 2 | 13 | 2 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-started-on-game-round1` | 11 | 8 | 9 | 4 | 1,5 | 17 |
| `mgo2-started-on-solo-game` | 6 | 2 | 0 | 3 | 1 | 18 |
| `myserver` | 9 | 2 | 4 | 4 | 1,2,3,5,6,16 | 14 |

Two offsets are a room fact in all fifteen and nothing else:

* **`0x55` — the current rotation slot** (**[V]**), the value the native reader at
  `0x8EB0C0`/`0x8EB148` uses to select a triple;
* **`0x56` — the room's population, host included** (**[V]** in this corpus: 2 for
  a solo room, 4, 6, 8 and 13 for the larger ones).

## 1. Field 29's timer vector: what the tail entries are

The 17 little-endian `u16`s at `0x65`–`0x86` are rule times in seconds. Elements
0–7 are established: DM, TDM, RES, CAP, SNE, BASE, BOMB, TSNE from the room's own
timer bank. The corpus settles the tail. Seven vectors cover the fifteen captures:

| captures | elements 0–7 | elements 8–11 | elements 12–16 |
| --- | --- | --- | --- |
| `mgo2-game1`, `game3`, `joined-final-round-solo…`, `joined-game-in-progress…`, `started-briefing-then-solo-game` | 300 420 180 300 540 300 300 240 | 53251 44192 9216 34 | 180 5940 0 300 300 |
| `joined-on-briefing-then-game`, `game-started-team-selection…` | 300 300 420 240 480 300 420 240 | 53251 44192 10240 34 | 180 1200 0 5940 300 |
| `joined-on-game-solo`, `started-on-solo-game` | 300 420 420 240 420 300 420 600 | 53251 44192 10240 34 | 180 1200 0 300 300 |
| `mgo2-game2` (TSNE only) | 300 300 300 300 300 300 300 240 | 0 0 0 0 | 300 300 0 1800 300 |
| `mgo2-game-failed-memoryviolation` | 300 × 8 | 0 0 0 0 | 300 300 300 300 1200 |
| `mgo2-game-joined-when-red-team…`, `started-on-game-round1`, `myserver` | 300 × 8 | 0 0 0 0 | 300 300 300 300 300 |
| `mgo2-game-started-on-briefing` | 300 480 300 × 6 | 0 0 0 0 | 300 300 300 300 300 |

So elements 12–16 are **per-room rule times for rule ids 12–16** and not a
constant: element 12 is 180 in nine rooms and 300 in six, element 13 is 5940,
1200 or 300, element 15 is 5940, 1800 or 300, element 16 is 1200 only in the room
that failed. Their units are seconds, like elements 0–7. One correlation holds
across all fifteen: element 14 is `0` in exactly the ten rooms that state an
INTERVAL or SCAP time other than 300, and 300 in the other five — so element 14
(ANEST) is not a constant but moves with the extended rules a room configures.

Elements 8–11 are **not** timers: every room that sets them writes the same four
words (`53251 44192 9216 34`, or `10240` in place of `9216`) and the seven rooms
whose payload is otherwise all-default write zero. A count of 53 251 seconds or of
44 192 has no reading as a rule time, and no rule in any captured rotation owns
those minutes. What they carry stays **[U]**; the corpus only says they are a
per-room-family constant and not a clock.

## 2. The bytes `BuildCore` writes as literals, and what the corpus says about each

| body offset | builder value | corpus | verdict |
| --- | --- | --- | --- |
| `0x4a` (field 3) | `0x11` | `0x0d` (1), `0x0f` (7), `0x11` (7) | per-room, and which room gets which is **[U]**: `0x0d` appears only in `joined-on-game-solo`, while `0x0f` and `0x11` both appear in fifteen-slot rooms, so it is neither the slot count nor the population |
| `0x4e` (field 5) | `0x02` | `3` (12), `2` (2), `6` (1) | per-room: `2` only in the two six-slot rooms, `6` only in `mgo2-game2` (a TSNE rotation), `3` in the other twelve |
| `0x4f`–`0x52` (field 7) | `0x00274022` | `0x0007622a`, `0x00126073`, `0x0007603a`, `0x00274022`, `0x00074022`, `0x00034033` | per-room 32-bit word; the native readers test bits `0x20` and `0x40`, and every captured value but `0x00034033` has both set, so the two flags are a room's own state and not a constant |
| `0x53`–`0x54` (field 8) | `0x0000` | `0x00b4`, `0x002c`, `0x00f0`, `0x0000` | per-room; **[U]** |
| `0x57`–`0x64` (fields 13, 17–27) | `00 00 00 05 00 00 00 05 00 00 01 00 00 00` | identical in the builder's own rooms, `00` in the all-default rooms, other values in the rest | partly a room's own: `0x60` is `2` in most rooms and `0` in the all-default ones, `0x63` is `2`, `3`, `6` or `0`, `0x64` is `10` or `0`; **`0x57` carries the same byte as `0xa0` in all fifteen** (below) |
| `0x87`–`0x9b` (fields 30–49) | `02`×8 `01 02 02 02 32 1e 05 03 00 00 00 7f 7f` | `0x89`, `0x8d`, `0x90` take `2`, `4` or `6`; `0x91` takes `2` or `20`; `0x93`/`0x94` take `20/30`, `40/99`, `50/30`, `51/30`, `55/30`; `0x9a`/`0x9b` are `7f 7f` except `03 06` and `04 09` | nine per-room bytes, and two slot references (`0x7f` = none) in the two captures that set them |
| `0x9c` (field 50) | `0xff` | `0xff` in all fifteen | **constant**; the unassigned slot reference, confirmed beyond the one capture it came from |
| `0x9d`–`0x9f` (fields 51–52) | `0x8c`, `0x0128` | a byte and a `u16`, and no two captures share the pair | per-room; it is not a timer — the values are in the thousands of seconds and no captured rotation owns those minutes — and its source is **[U]** |
| `0xa0` (field 53) | `0` | `0` or `1` | a truth value whose selector is **[U]**; offset `0x57` (field 13) carries the **same byte** in all fifteen captures, so the payload states it twice |

**What the builder now writes.** Every field this server does not derive is the
working capture's: `.pcapngs/mgo2-started-briefing-then-solo-game.pcapng`, the
capture the port's notes call `briefing-then-solo-game`. Its body therefore
reproduces that capture field for field except the three the room owns — the
rotation triples at `0x1a`, the slot at `0x55` and the population at `0x56`. The
regions taken from it are `0x4a`–`0x54`, `0x57`–`0x64`, `0x65`–`0x86`, `0x87`–`0x9b`
and `0x9c`–`0xa0`, and the port's own builder is checked against the capture byte for
byte in exactly those regions.

**What the duplication settles.** Field 13 at `0x57` and field 53 at `0xa0` hold
the same byte in every capture, so `BuildCore` writes both from one constant
(`ReferenceSelector`) instead of a literal at `0x57` and a second at the tail.

**What this changes in the builder.** Elements 12–16 are now documented as the
times of rule ids 12–16 — SDM, INTERVAL, ANEST, SCAP and RACE, indexed by rule id
rather than by the lobby reply's order elements 0–7 follow — and the reference 300
stands only because the lobby's own times for them are settings this server never
receives: the bank it does receive is in the reply's order, which has no word for
these rule ids. Values of 0 are **not** written there, because element 14 reads 0
only in a room that configures an extended rule and 300 in the ones that do not,
so the constant belongs to the room and not to the format. The four words before
them stay zero, which is what seven captures measure. Nothing else in this file's
table is renamed, because nothing else is measured to a meaning.

## 3. What the client binaries and the port's notes added

The mapping of elements 12–16 to rule ids is already read out in
`docs/protocol/UDP_SETTINGS_PAYLOAD.md`: the PC binary's code at VA `0x1020fd59`
selects `Rule_Eng_SDM`, `Rule_Eng_INTERVAL` and `Rule_Eng_SCAP` for ids 12, 13
and 15, and the five adjacent name pointers at file offset `0x441b24` order SDM,
INTERVAL, ANEST, SCAP, RACE. That is what fixes the five elements to rule ids
rather than to five unnamed slots, and this corpus is the independent check on it:
the values move per room exactly where the per-rule table says they should, and
index 14 is the one with no captured rule to exercise it. The same holds for the
`0x4313` tail bytes `0x166`–`0x16a`, whose bytes 0, 2 and 4 read 60× into
indices 12, 13 and 15.

Nothing in the binaries names fields 51–52 or the byte at `0xa0`; the payload is a
state transfer rather than a record with a parser, so those stay as §2 states them.

## 4. What the native readers say

Three findings from the PS3 `system.dll`'s PPC code, read with `tools/mgo2_disasm.py`
over `.game/ps3/MGO2.ELF`:

* **The client's rotation object is a 48-byte block plus a one-byte index, and both
  are fetched through one accessor.** `FUN_008eb0c0`, `FUN_008eb148` and
  `FUN_002816f4` each call `FUN_0027e2b0` (resolve the settings object), then
  `FUN_0027e480(object, 0x15, 48, dst)` and `FUN_0027e480(object, 0x52, 1, &slot)`,
  and then return `dst[3*slot + n]`. That is the same sixteen-entry, three-byte
  layout this file's tables list at body `0x1a`–`0x49`, selected by the byte at
  `0x55`; the client's object keeps the block at offset `0x15` and the index at
  `0x52`, so the object offsets are not the wire offsets.
* **The third triple byte has a getter of its own.** `0x8eb0c0` and `0x2816f4`
  return `triple[slot][1]` (the rule) and `0x8eb148` returns `triple[slot][2]`.
  The engine asks for that byte, by itself, from the entry the room selected — so
  it is a property of the round and not of the room. What the value *means* stays
  **[U]**; the getter proves the shape, not the word.
* **The accessor call sites are the client's field map.** Scanning the whole text
  segment for `bl FUN_0027e480` finds 283 sites, each carrying its key and length in
  `r4`/`r5`; the pair the three getters use is `0x15`/48 and `0x52`/1, and the
  others name the remaining settings fields. `FUN_0027bb10` uses the same pair and
  then computes `3*(slot+1)`, clears the slot to zero and raises an engine notify
  when `slot > 14` — the rotation walk, and the reason a room plays fifteen slots
  and leaves the sixteenth zeroed.

## 5. The settings descriptor table

`FUN_0027e480` resolves a field by walking an array of ten-byte records, and the
records are in the image at VA `0x1229b80`–`0x1229ca2` (file `0x1219b80`), 29 of
them, decoded by `tools/mgo2_settings_descriptors.py`:

| u16 | meaning |
| --- | --- |
| `key` | byte offset of the field inside the settings object |
| `length` | width of the field in bytes |
| `stride` | `1` in every record but one, which is `2` |
| `flags` | a per-field word (`0x0025`, `0x0125`, `0x0225`, `0x0235`, `0x0064`, `0x0000`) |
| — | zero |

The records tile the object densely from `0x00` to `0x8a`, which is the arithmetic
the accessor performs (`key + length * stride`), and three records share key
`0x5a` with lengths 8, 4 and 1 — one field with three widths.

**The table confirms the wire layout field for field.** The object is not the wire
body: the body packs the fields and the object leaves two gaps (`0x11`–`0x14` and
`0x4a`–`0x4b`), which is why the reader's offsets and the body's differ by 9 bytes
before the first gap and by 5 and 3 after them. Matched by width, and by the two
readers' own keys, the correspondences are:

| object key | width | body offset | what this file calls it |
| ---: | ---: | ---: | --- |
| `0x00` | 1 | `0x09` | round phase (field 0) |
| `0x01` | 16 | `0x0a` | field 1, sixteen reserved bytes |
| `0x15` | 48 | `0x1a` | the sixteen rotation triples |
| `0x45` | 1 | `0x4a` | field 3 |
| `0x46` | 2 | `0x4b` | field 4, always `0x00b4` |
| `0x48` | 1 | `0x4d` | field 5 |
| `0x49` | 1 | `0x4e` | field 6 |
| `0x4c` | 4 | `0x4f` | field 7, the word with bits `0x20`/`0x40` |
| `0x50` | 2 | `0x53` | field 8 |
| `0x52` | 1 | `0x55` | current rotation slot (the getters' key) |
| `0x53` | 1 | `0x56` | population |
| `0x68` | 34 | `0x65` | field 29, the seventeen rule times |

So the two bytes this file has been calling "field 9" and "field 10" are, in the
client, a one-byte slot index and a one-byte population at object offsets `0x52`
and `0x53`; the seventeen-`u16` timer vector is one 34-byte field, not seventeen;
and the third byte of a triple is inside the 48-byte block the client treats as one
field, which is why only the engine's own getter reads it.

### What the shipped assets do and do not contain

Naming a field from data alone is not possible here, and it is worth saying which
sources were checked. `pc-client/game/params` holds three NPC parameter files
(`npc_parameter_mission.mgaparam`, `npc_parameter_tdm.mgaparam`,
`npc_system_parameter.mgaparam`); `game/mgo2_slots.txt` is a stage-sector list
(`slot_oct_list_online <slot> <sector> <sectors>`); `game/slotdat/` holds the
cosmetic slot files (`slot_online_head.slot` and its neighbours); and
`.gametools/replay-console` is a 3D replay viewer, whose every `rotation` match is
quaternion code. None of the four describes a settings field. `.gametools/Haven`
is stage, GEO and GCL tooling. So a name has to come from the code that reads a
field, which is what the call-site table below sets up.

The `flags` word is what still has no reading: `0x0064` marks the slot index and
one other byte (`0x58`), `0x0125` the sixteen-byte field and the rotation block,
`0x0225`/`0x0235` the widest variants at `0x5a` and the timer vector, `0x0025`
nearly everything else, and `0x0000` four bytes. It is not a width or an order.

### What the call sites show, key by key

53 of the accessor's 283 call sites pass a constant key the descriptor declares.
Grouped by key, with what the caller does with the value it gets back:

| key | width | sites | what the caller does |
| ---: | ---: | ---: | --- |
| `0x00` | 1 | 25 | compares it against `0` and `7` and branches to the same place — a phase with two "not playing" values |
| `0x01` | 16 | 3 | returns one byte of the block (`lbz r3, dst[n]`) and compares one byte against another — a sixteen-byte text field read character by character |
| `0x15` | 48 | 5 | the rotation block, always paired with a `0x52` fetch |
| `0x45` | 1 | 1 | loaded into a per-round loop that also walks a list at `-0x8000(r30)` |
| `0x4c` | 4 | 8 | tests **bit `0x20`** (`rlwinm r0, r0, 0, 0x1a, 0x1a`) and stores the answer at `+0x6244` of its own object — this is the bit the port already knows is read |
| `0x52` | 1 | 5 | the slot, multiplied by three and added to the block base; `0x27bb10` wraps it past 14 |
| `0x59` | 1 | 2 | passed as the id argument (`r4`) to `0xf0ea88` with a destination buffer — an index into a table, not a value |
| `0x5a` | 8/4/1 | 2 | returns one byte from inside the variant (`lbz r3, dst[1]` and `dst[5]`) |
| `0x63` | 1 | 2 | fetched, then the caller branches on its own `+0xb8` counter and a `0xff` sentinel |

### The two hot keys, traced

**Key `0x00` (body `0x09`, width 1, flags `0x0025`) — 25 sites in 17 functions.**
Two of them, in `FUN_00288160` and `FUN_00288540`, branch away when the value is
`0` or `7`; a third (`0xaa1bb4`) compares it with `3`; the other twenty-two hand
it to flow helpers (`0xf9d840`, `0xf9dca0`, `0x97c124`, `0xe6fc08`, `0xcdc90`,
`0xd6f5b8`, `0x2891e0`). So the field is an enumerated phase in `0..7` where `0`
and `7` are outside a running round and `3` is a phase a caller names — and the
corpus only ever shows `2`, `3`, `4` and `5`, which fits that range and never
reaches the two boundary values.

**Key `0x4c` (body `0x4f`, width 4, flags `0x0025`) — 8 sites in 6 functions.**
It is a bit field, and the client reads three bits out of it:

| bit | instruction | site | effect |
| --- | --- | --- | --- |
| `0x20` | `rlwinm r0, r0, 0, 0x1a, 0x1a` | `0xa3a90`, `0xa3c6c`, `0xa5690`, `0xa5720`, `0xa5904` | branches; one site stores the answer at `+0x6244` of its own object |
| `0x40` | `rlwinm r3, r3, 0, 0x19, 0x19` | `0x8eb008` (in the getter family) | returned as a boolean by a getter |

Bit `0x40` has a getter of its own: `FUN_008eafe0` fetches key `0x4c` (4 bytes) and
returns `rlwinm r3, r3, 0, 0x19, 0x19` zero-extended — a boolean property in the same
family as the map, rule and third-component getters. Its `.opd` entry is
`0x1203e60`, and **nothing in the image references that entry**: the property is
reached through the object's method table, so its slot index is not a constant and
the neighbours that would have named it are not statically visible.

Bit `0x20` is branched on by the lobby code at `0xa3978`, `0xa3af8` and `0xa5030`,
which also store the answer at `+0x6244` of its own object. Their shape is dispatch,
not property logic: `0xa3978` has five callers (`0xb3ca0c`, `0xb7b8e8`, `0xb97d58`,
`0xbb8568`, `0xbb8b50`) and branches on the values `0xa` and `6` from two state
getters; `0xa5030` has one caller (`0xa76fc`) and switches on `[r31+0x70]` compared
against `4`. Neither compares the flag with a room concept, so the bit still has a
reader but no name.

Against the corpus: bit `0x20` is set in **all fifteen** rooms, and bit `0x40` in
exactly the two rooms whose rotation is rule 4/15 (`joined-on-briefing-then-game`,
`game-started-team-selection`). That is a correlation, not a name; what it rules out
is the reading that field 7 is a plain number — two bits of it are decisions the
client makes.

One earlier reading is corrected here: the `rlwinm r0, r9, 0, 0x1d, 0x1d` at
`0xa3c74` tests `r31+0x2c0`, not the word the accessor fetched, so it is not a third
bit of field 7.

That is as far as the reader side goes. No asset in `pc-client` and none in
`.gametools` names these fields, and no string in the binary annotates the
descriptor, so a name would have to be consistent with the width, the flags word
and the usage shape above rather than read off a table. Two keys can be named
outright — `0x15` (rotation) and `0x52` (slot) — and one described but not named:
the `0x4c` word's bit `0x20`.

### The writer: found — `FUN_0027e578`, and what it copies

`FUN_0027e578` is the setter counterpart of the getter: the same 26-entry table
walk over the same ten-byte records, but it stores `r5` bytes of `r6` into the
object at `r4`. It has **210 call sites**, and one cluster — `0x9e9184` to
`0x9e9590`, 42 calls in ascending key order — is the settings object being filled
field by field:

| order | key | width | source | | order | key | width | source |
| ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | --- |
| 1 | `0x01` | 16 | `r1+0xd9` | | 22 | `0x8a` | 1 | `r1+0x162` |
| 2 | `0x11` | 4 | `r1+0xe9` | | 23 | `0x8b` | 1 | `r1+0x163` |
| 3 | `0x15` | 48 | `r1+0xa6` | | 24 | `0x8c` | 1 | `r1+0x164` |
| 4 | `0x45` | 1 | `r1+0x11d` | | 25 | `0x8d` | 1 | `r1+0x165` |
| 5 | `0x49` | 1 | `r1+0x121` | | 26 | `0x8e` | 1 | `r1+0x166` |
| 6 | `0x4c` | 4 | `r1+0x78` | | 27 | `0x8f` | 1 | `r1+0x167` |
| 7 | `0x50` | 2 | `r1+0x72` | | 28 | `0x90` | 1 | `r1+0x168` |
| 8 | `0x52` | 1 | `r1+0x12a` | | 29 | `0x92` | 1 | `r1+0x16a` |
| 9 | `0x53` | 1 | `r1+0x12b` | | 30 | `0x93` | 1 | `r1+0x16b` |
| 10 | `0x54` | 2 | `r1+0x12c` | | 31 | `0x94` | 1 | `r1+0x16c` |
| 11 | `0x56` | 2 | `r1+0x12e` | | 32 | `0x95` | 1 | `r1+0x16d` |
| 12 | `0x58` | 1 | `r1+0x130` | | 33 | `0x96` | 1 | `r1+0x16e` |
| 13 | `0x59` | 1 | `r1+0x131` | | 34 | `0x97` | 1 | `r1+0x16f` |
| 14 | `0x5a` | 8 | `r1+0x7c` | | 35 | `0x9a` | 1 | `r1+0x172` |
| 15 | `0x62` | 1 | `r1+0x13a` | | 36 | `0x9b` | 1 | `r1+0x173` |
| 16 | `0x64` | 2 | `r1+0x13c` | | 37 | `0x9c` | 1 | `r1+0x174` |
| 17 | `0x66` | 1 | `r1+0x13e` | | 38 | `0x9d` | 1 | `r1+0x175` |
| 18 | `0x67` | 1 | `r1+0x13f` | | 39 | `0x9e` | 2 | `r1+0x176` |
| 19 | `0x68` | 34 | `r1+0x84` | | 40 | `0x46` | 2 | register |
| 20 | `0xa0` | 1 | `r1+0x70` | | 41 | `0x48` | 1 | register |
| 21 | `0xa4` | 1 | register | | 42 | `0x00` | 1 | register |

The decisive detail is the arithmetic: **27 of the 42 read their source at
`r1+0xd8+key`** (`0x45`→`0x11d`, `0x49`→`0x121`, `0x52`→`0x12a`, `0x53`→`0x12b`,
`0x54`→`0x12c`, `0x56`→`0x12e`, …, `0x9e`→`0x176`). That is a stack copy laid out
exactly like the object, so the record's key really is the object's own offset —
confirmed independently of the accessor's `key + length * stride` arithmetic. The
other fifteen take a computed temporary (`0x01` at `r1+0xd9`, `0x11` at `r1+0xe9`,
`0x15` at `r1+0xa6`, `0x4c` at `r1+0x78`, `0x50` at `r1+0x72`, `0x5a` at `r1+0x7c`,
`0x68` at `r1+0x84`) or a register (`0x00`, `0x46`, `0x48`, `0xa0`, `0xa4`).

**What this settles, and what it does not.** It settles the key's meaning and the
order the client fills the object in. It does not give the wire offsets, and the
search for the encoder needs one correction first: an earlier pass here reported
that no code stores at wire offsets `0x55`, `0x56`, `0x65` or `0x9c`, but that scan
was invalid — Capstone's generator stops at the first undecodable byte, so it had
only walked a fragment of the text segment. Re-run with `skipdata` over the whole
segment (**4 585 680 instructions**), the answer changes:

* **Two functions do store at those offsets** — `FUN_00d6c4a0` and its twin
  `FUN_00d6cda8`, at `0x55`, `0x56` and the `0x9c`–`0x9f` tail. But the pattern is
  not per-field: the stores come in runs of consecutive registers into consecutive
  bytes (`stb r0, 0x10`, `stb r8, 0x11`, `stb r7, 0x12`, …, at the `0x10`, `0x50`
  and `0x90` columns). That is an unrolled sixteen-byte block copy of a
  body-sized buffer — a transport of the body, not a serializer that computes it.
* **Nothing loads the length and the map together.** No function holds both `0xa1`
  and the presence-map bytes `0x27`/`0xfe`/`0xee`/`0x3f`; the one candidate that
  looked like it, `FUN_00b14ae0`, is a six-instruction dispatcher on a small enum.
* **No literal pointer to the descriptor table.** The raw search still stands: no
  aligned 32-bit reference to `0x1229b80` or to a record inside it exists in the
  image (the only hit is unaligned bytes at file `0x12198ac`).

The callers were then traced, and they are not statically reachable either:
`FUN_00d6c4a0` and `FUN_00d6cda8` have **no direct call site**, and their `.opd`
entries (`0x1211148` and `0x1211160`) are referenced as data once and never — so
both are dispatched through the runtime method table
(`lwz r9, [obj]; lwz r11, 8(r9); lwz r0, 0(r11); mtctr r0; bctrl`), the same route as
the getters. The encoder is therefore either behind that table or not in the client
at all, and the static anchors are exhausted: what remains is to resolve the
method table, which needs the object's class id at runtime rather than a constant.

## 6. Open

* field 3 (`0x4a`) and field 5 (`0x4e`), which are per-room and correlate with
  nothing in the corpus;
* field 7's two flag bits and field 8, which the native readers test but do not
  name;
* fields 51–52, which vary per room and match no timer, slot or count measured
  here;
* fields 30–49, of which nine bytes are per-room and two are slot references;
* elements 8–11 of field 29, which are a per-room-family constant but carry no
  reading as time;
* the descriptor table's `flags` word, and the object offsets of the fields whose
  wires the body places after `0x64` — the widths match one field each, but the
  packing order there is not yet traced through the writer;
* the object-to-wire step itself: the setter fills the object, and no code was
  found that lays that object out as the 161-byte body.
