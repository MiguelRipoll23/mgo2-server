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
| `0x4a` (field 3) | `0x11` | `0x0d` (1), `0x0f` (8), `0x11` (13) | per-room, and which room gets which is **[U]**: `0x0d` appears only in `joined-on-game-solo`, while `0x0f` and `0x11` both appear in fifteen-slot rooms, so it is neither the slot count nor the population. It is the room's and not its host's: `129.153.74.111:5720` writes `0x11` for its thirteen-player room and `0x0f` for the six-player one (§7) |
| `0x4d` (field 5) | `0x02` | `02` in 21 of 22, `14` in one | **a state byte, not a setting**: `02` in every body of a room's game phase and `14` (`20`) in the body written while that room was held at its briefing, so it moves with the room rather than being pinned by it |
| `0x4e` (field 6) | `0x03` | `3` (19), `2` (2), `6` (1) | per-room: `2` only in the two six-slot rooms of `202.215.217.164`, `6` only in `mgo2-game2` (a TSNE/SCAP rotation), `3` in the other nineteen. **This is the byte an earlier row here called field 5** |
| `0x4f`–`0x52` (field 7) | `0x00274022` | `0x0007622a`, `0x00126073`, `0x0007603a`, `0x00274022`, `0x00076022`, `0x00074022`, `0x00034033` | per-room 32-bit word; the native readers test bits `0x20` and `0x40`. Bit `0x20` is set in **all twenty-two** bodies; bit `0x40` only in `0x00126073`, and only for the one host whose join showed the client a team-selection screen (§7). An earlier reading claimed both bits were set in every value but `0x00034033`, which is wrong: six of the seven carry `0x20` alone. The rule reading — "set in the rooms that rotate SNE and SCAP" — is refuted by `mgo2-game2`, which rotates rule 15 and writes `0x00034033` |
| `0x53`–`0x54` (field 8) | `0x0000` | `0x0000`, `0x00b4`, `0x00f0`, `0x012c` | per-room, and constant across that room's own captures; every value is a whole number of minutes held as seconds (`0`, `180`, `240`, `300`), which no other offset here but field 4 is. The `0x002c` an earlier row recorded is the low byte of `0x012c`. What the time limits is **[U]** |
| `0x57`–`0x64` (fields 13, 17–27) | `00 00 00 05 00 00 00 05 00 00 01 00 00 00` | identical in the builder's own rooms, `00` in the all-default rooms, other values in the rest | partly a room's own: `0x60` is `2` in most rooms and `0` in the all-default ones, `0x63` is `2`, `3`, `6` or `0`, `0x64` is `10` or `0`; **`0x57` carries the same byte as `0xa0` in all fifteen** (below); the two words at `0x5a` and `0x5e` are a per-round pair (§7) |
| `0x87`–`0x9b` (fields 30–49) | `02`×8 `01 02 02 02 32 1e 05 03 00 00 00 7f 7f` | `0x89`, `0x8d`, `0x90` take `2`, `4` or `6`; `0x91` takes `2` or `20`; `0x93`/`0x94` take `20/30`, `40/99`, `50/30`, `51/30`, `55/30`; `0x9a`/`0x9b` are `7f 7f` except `03 06` and `04 09` | nine per-room bytes, and two slot references (`0x7f` = none) in the two captures that set them |
| `0x9c` (field 50) | `0xff` | `0xff` in all fifteen | **constant**; the unassigned slot reference, confirmed beyond the one capture it came from |
| `0x9d` (field 51) | `0x8c` | changes between bodies of one room (`140`, `232`, `233` for `64.181.216.250:5720`) | the capture's byte and not the room's; it is not a checksum of the body — byte sum, negated sum, `u16` sum, byte XOR and CRC16 over every prefix ending at `0x9d` or later match it in none of the twenty-two bodies |
| `0x9e`–`0x9f` (field 52) | `0x0128` | constant per room across its own bodies (`277`, `296`, `3813`) | the room's own word, so the pair an earlier row called opaque is two fields; which counter it names is **[U]** |
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
`0x1203e60`, and it appears in **no method table and at no call site**: unlike the
rule getter, which sits in nine tables (bases `0x119d250`, `0x119d270`, `0x119d2d8`,
surrounded by the `0x8eb`/`0x8ed` getter family), this one is unreferenced in the
shipped build. Its name cannot come from a slot, because it has none.

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

**The rule reading of bit `0x40` is refuted, and a better candidate exists.**
`mgo2-game2` rotates rule 15 and writes `0x00034033`, so the bit does not follow the
rules. What the two rooms have in common is their host: both are
`69.213.233.238:5731`, and `briefing/working server/working-server-4-sessions.txt`
records that this room is the one that *asked which team to join* before the round —
blue, auto or red. The bit is therefore the room's "choose your side" condition,
**[I]** on one host and one note, and it is the best-named unresolved bit in the body.

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

The callers were then traced. `FUN_00d6c4a0` and `FUN_00d6cda8` have **no direct
call site**, and the method tables settle what they are. The tables are arrays of
32-bit `.opd` pointers, and `FUN_00d6c4a0`'s entry appears exactly once, in the
table at `0x11db5d8`:

```
  0x11db5dc -> 0x00d6c4a0   0x11db5e0 -> 0x00d6c460   0x11db5e4 -> 0x00d6cca8
```

Three adjacent copiers, nothing else — a transport family, not a serializer. The
encoder is therefore not behind a table that holds these functions, and the static
anchors are exhausted (see the port check below).

### The port's own DLLs do not hold the encoder

`system.dll` and `savemgo.dll` are both PE32 for x86 (image base `0x10000000`),
16.6 MB and 4.7 MB. `savemgo.dll` carries the record type (`0x1001`, 18 code
immediates) and body-sized lengths (`0xa1`, 117 code immediates), but **no function
holds both**: zero pairs fall within 0x100 bytes of each other. Its hits are a
network and graphics layer — the `rotation` string is `SDL.surface.rotation`, the
`settings` one is a DXVK config comment. Neither DLL contains the presence-map
bytes (`ff 27 fe ee`), which is what a serializer would emit. The encoder stays in
the PPC client.

## 6. Open

* field 3 (`0x4a`) and field 6 (`0x4e`), which are per-room and correlate with
  nothing in the corpus;
* field 7's two flag bits and field 8, which the native readers test but do not
  name — field 8 is now measured to be whole minutes of some unidentified time;
* field 5 (`0x4d`), which is state rather than a setting and whose two measured
  values (`02`, `14`) name nothing;
* the capture's tail byte at `0x9d` and the room's tail word at `0x9e`, which are
  two fields and not one, and match no timer, slot, count or checksum measured
  here; the two per-team words at `0x5a` and `0x5e`, which are per-round state and
  are not Drebin points;
* fields 30–49, of which nine bytes are per-room and two are slot references;
* elements 8–11 of field 29, which are a per-room-family constant but carry no
  reading as time;
* the descriptor table's `flags` word, and the object offsets of the fields whose
  wires the body places after `0x64` — the widths match one field each, but the
  packing order there is not yet traced through the writer;
* the object-to-wire step itself: the setter fills the object, and no code was
  found that lays that object out as the 161-byte body.

## 7. The wider corpus of 3 November, and what it corrects

Three more captures — the `briefing/` set — add five rooms to the fifteen above:
`briefing-then-solo-game` (`64.181.216.250:5720`), `working-server-log` and
`working-server-4-sessions`, which between them hold that host's second room on
`:5722`, a second room on `69.213.233.238:5731`, and the eleven-slot room on
`143.47.227.126:5725`. Recounted over the whole set, **twenty-two distinct bodies
from eleven rooms and nine hosts**:

| room | slots | pop | rot | phase | rules | max map |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| `briefing-then-solo-game` (`64.181.216.250:5720`) | 9 | 2 | 4 | 2 | 1,2,3,5,6,16 | 14 |
| `working-server-log` (`64.181.216.250:5722`) | 15 | 3 | 1 | 3 | 1–7,16 | 17 |
| `working-server-log` (`69.213.233.238:5731`, second room) | 15 | 2 | 8 | 3 | 4,15 | 19 |
| `working-server-log` (`143.47.227.126:5725`) | 11 | 14 | 6 | 4 | 1,5,7,12 | 17 |

**`myserver`'s body is not evidence.** It is the reference's, field for field
(`7e e5 0e` at the tail, as in `mgo2-started-briefing-then-solo-game`), because
`BuildCore` pins that capture; counting it as a second measurement of a pinned
offset confirms the pin and not the wire.

What the recount settles, and where the tables above are corrected:

* **Field 5 (`0x4d`) is state.** `0x02` in twenty-one bodies and `0x14` (`20`) in
  the body `joined-on-briefing-then-game` wrote while its room was held at the
  briefing — the same host and rotation as bodies reading `0x02` in another phase.
* **Field 6 (`0x4e`) is the per-room `3`/`2`/`6` byte**, not field 5. Its `2` is the
  two six-slot rooms of one host.
* **Field 3 (`0x4a`) is the room's, not the host's**: two rooms of one host differ.
  It only ever holds `13`, `15` or `17`.
* **Field 8 (`0x53`) is whole minutes**: `0`, `180`, `240`, `300`, constant across a
  room's own bodies. The `0x002c` recorded above is the low byte of `0x012c`.
* **Field 7's bit `0x40`** is set in the four bodies of `69.213.233.238:5731` and in
  no other body; bit `0x20` is set in all twenty-two. The bit does not follow the
  rules — `mgo2-game2` rotates rule 15 without it — and the room it does follow is
  the one whose joins asked the client which team to join, per
  `briefing/working server/working-server-4-sessions.txt`: so it reads as the room's
  team-selection condition, **[I]**.
* **The tail is two fields**: `0x9e` holds across a room's bodies (`277` for all
  three of `143.47.227.126:5725`, `296` for the three of `64.181.216.250:5720`,
  `3813` for the five of `99.66.131.177:5731`) while `0x9d` does not.
* **The words at `0x5a` and `0x5e` are a per-round pair**: one room's bodies hold
  them equal (`5/5`, `40/40`, `50/50`, `55/55`, `0/0`), another's hold them apart
  (`15/33`, `27/18`, `30/32`, `38/44`, `47/35`, `48/47`). **Within one capture the pair
  does not move** — every room re-reads it identical one to two seconds later
  (`40/40`, `55/55`, `0/0`, `5/5`, `47/35`, `50/50`) — so it is a room's standing total
  and not a clock. Two per-team values, equal at some moments and apart at others, is
  what a *remaining ticket count* looks like, and the client's own TDM panel uses that
  word for it: "reducing the other team's tickets to zero"
  (`docs/analysis/rule-instruction-texts.md`). That reading is **[I]**, not **[V]**: it
  fits the shape and the vocabulary and nothing here observes a ticket spent. They are
  not Drebin points — the capture's Drebin values sit in a 54-byte `0x1001` record of
  their own (`docs/analysis/drebin-point-records.md`) and no 161-byte body carries
  `1000` or `5000`.

The files still cannot name these fields. Every one of them is a room-creation
setting, and the gameplay wire only ever shows the value its host already decided:
their source is the lobby's room-settings reply, which none of these captures hold.
The client log in the `briefing/` set does not hold them either — its only room fact
is stage order, and it confirms nothing beyond `0x55` selecting `triple[slot][0]`.

## 8. What the client's own Create Game screen calls them

The PC client downloaded on 3 November carries the Create Game menus in
`game/stage/lobby/scenerio.gcx`, so the list of settings these fields can be is now
read rather than guessed. The common settings block, with its offsets in that file:

| setting | offset |
| --- | --- |
| `Round Time`, `TIME(min.)` | `0x0cce91`, `0x0ccdcf` |
| `Number of Rounds` | `0x0ccf8f` |
| `Extra Time` | `0x0ccfe8` |
| `Starting Number of Tickets` | `0x0cceed` |
| `Number of Snake Eliminations to Win` | `0x0cd02c` |
| `Checkpoints` | `0x0cd0f8` |
| `Max Number of Characters` | `0x0cfd9c` |
| `Briefing Time` | `0x0cfe3b` |
| `Headshots`, `Lock On (AUTO AIM)`, `Enemy Nametag Display` | `0x0cfeb5`, `0x0cfefb`, `0x0cff6c` |
| `Silent Mode`, `Auto Assign Teams`, `Teams Switch Positions` | `0x0cffff`, `0x0d0066`, `0x0d00fc` |
| `Ghost Pranks`, `Allow Quick-Join` | `0x0d0193`, `0x0d0200` |
| `Level-Limit` | `0x0d0289`, `0x0d02a5` |
| `Team Kill Kick`, `Idle Kick`, `Auto Balance Teams` | `0x0d02dd`, `0x0d0366`, `0x0d03ef` |
| `Weapon Restriction`, `Clan Members Only`, `Allow Solo Players` | `0x0d04b1`, `0x0d0564`, `0x0d0632` |
| `Log Personal Stats`, `Dedicated Host Settings` | `0x0d06d3`, `0x0cfb21` |

That list is the whole candidate set: the body's unresolved bytes are these settings,
not conditions the game invents. It also explains a shape that did not have one —
**field 7 is four bytes because the common settings are a dozen and a half switches**,
and the client's reader at `0x8eb008` returns one bit of it as a boolean.

### What the values now single out

| body field | measured values | the candidates, and how far each is supported |
| --- | --- | --- |
| `0x4a` field 3 | `13`, `15`, `17`, per room | `Level-Limit` or `Max Number of Characters`: `level >= population` holds for all sixteen room configurations, and 13–17 is squarely inside the 0–22 level range while 17 sits *above* the sixteen-player maximum. Neither is excluded by the corpus, so it stays **one of the two** until the lobby's room reply is read |
| `0x4d` field 5 | `02`, and `14` in one body | a **state** byte: it moves between bodies of one host while the settings around it do not. `14` = 20 is the shape of a countdown or a count remaining **[U]** |
| `0x4e` field 6 | `2`, `3`, `6`, per room | `Number of Rounds`: the two rooms that state `2` are the one-rule six-slot rooms, and `6` appears only in the TSNE/SCAP room. **[I]** |
| `0x53` field 8 | `0`, `180`, `240`, `300` | `Briefing Time` or `Round Time`, both of which the screen sets in minutes — every value is a whole number of minutes and a room holds its own. **[I]**; the two cannot be told apart from the gameplay wire |
| `0x4f` field 7 | 4 bytes, bits `0x02` in all 34 bodies | the **common-settings switch word**. Bit `0x02` is Drebin Points (`docs/GATES.md` §2) and is set in every captured room, which is why every room played DP rounds; bit `0x40` is set only in the room whose joins asked which team to join, which is `Auto Assign Teams` showing up as a role-selection screen **[I]** |
| `0x5a`, `0x5e` | `5/5`, `40/40`, `47/35`, `15/33`, … | each team's **remaining tickets**. `0x93` is the room's *starting* count and this pair never exceeds it, so direction is now measured rather than assumed: the pair equals `0x93` in the rooms whose round has not been spent (`40/40` with `0x28`, `50/50` with `0x32`, `55/55` with `0x37`) and sits below it once players have respawned (`30/32` and `5/5` against `50`; `5/5` against `20`). The TDM panel states the mechanic — "Each team starts with a set number of tickets… steal tickets" — and the tooltip at `0x0cee5e` says a ticket is spent on each respawn |
| `0x93` | `20`, `40`, `50`, `51`, `55` | the round's **ticket allotment** — `Starting Number of Tickets` (`0x0cceed`) — in the rooms that play a ticketed rule: never below the pair at `0x5a`/`0x5e` in any of the 34 bodies, and equal to it in the thirteen whose round is untouched. The SNE/SCAP room carries `51` where no tickets are played, so the reading is a ticket count *where tickets apply* **[I]** and open there **[U]** |
| `0x9a`, `0x9b` | `7f 7f` in most, `03/02`, `03/06`, `04/09` in one room | the **Sneaking role slots** — the players holding Snake and Metal Gear Mk.II. All fourteen bodies whose rotation holds neither SNE nor TSNE read `0x7f 0x7f`; a real pair appears only in the SNE/SCAP room, while sixteen of the twenty bodies that *do* hold SNE or TSNE still read `0x7f 0x7f`. So the pair is the assignment for the round being played, not a room setting, and it moves between bodies of that one room (`03/02` → `03/06` → `04/09`) |
| `0x87`–`0x9b` | `… 32 1e 05 03`, `… 06 00 01 06 14 02` | the remaining per-rule counts — `Number of Snake Eliminations to Win` (`0x0cd02c`), `Checkpoints` (`0x0cd0f8`) — whose per-room values are still not tied to a rule **[U]**. `0x93` and `0x9a`/`0x9b` in this run are the two exceptions, above |
| `0x91` | `2` everywhere, `20` in the one SNE room | a candidate for Snake's **dog-tag count** — the panel's "Snake wins if he collects a set number of dog tags" — on one room's evidence **[U]** |

### What the stage script adds beyond the labels

Three things, and one of them reopens a question rather than closing it.

**The unit words confirm the ticket pair.** The screen prints a value's unit as its own
string, immediately before the labels that use it: `ROUND` and `ROUNDS` at `0x0cce0e`
and `0x0cce14`, `TICKET` and `TICKETS` at `0x0cce28` and `0x0cce2f`, then `tickets`,
`Tickets` and `ticket` at `0x0cd162`–`0x0cd172`, sitting beside `Round Time`
(`0x0cce91`), `Number of Rounds` (`0x0ccf8f`) and `Starting Number of Tickets`
(`0x0cceed`). So a team's number on this screen is denominated in tickets and not in
points or score, which is why the `0x5a`/`0x5e` pair is read as tickets and the two
`0x1001` Drebin words are not.

**The level limit is a pair, not a number.** The only numeric format string the settings
screens use is `%d ± %d` (`0x0d09fd`), and it follows the `Allow`/`Disallow` labels that
begin the level block, so the client presents `Level-Limit` as a base with a spread.
That reopens field 3: `13`, `15` and `17` are plausible *bases* for such a range, and
the byte at `0x64` — `10` in five rooms and `0` in the rest — is the shape of a spread
half. It is not a resolution: `0x64` is ten bytes away from `0x4a`, and `Idle Kick`
and `Team Kill Kick` are also small counts. It does add a second, independent test:
if the lobby's reply carries a level range, it should carry two numbers, and field 3
should match the larger of the two.

**The room list has no status words at all.** The list screen's column headers are in
the file — `STATUS` at `0x0b0cdf`, `HOST SCORE` at `0x0b04fb`, `HOST NAME` at
`0x0b04b9`, `LEVEL` at `0x0b0ae2`, `AVG.LEVEL` at `0x0b026e`, `PLAY TIME` at
`0x0b094e`, `GAME TYPE` at `0x0b042d`, `RULE` at `0x0b0c57`, `MAP` at `0x0b06b2` — but
no value it can print in that status cell. The whole file's short upper-case strings are
`LOCKED ON`, `OPEN`, `NOW LOADING`, `NEXT`, `STATUS`, `ENDZIEL`, `LOCKED`, `WAITING ROOM`,
`NEXT PAGE` and `FULL SCREEN`; none of them is a room state. So the status cell is drawn
from something other than these strings — a number, or an image — and the state bytes
(`0x4d`, and the phase at `0x09` that the client compares with `0`, `3` and `7`) cannot
be named from this file at all. A lobby packet capture remains the only route to them.

### What the rule panels add

Grouping the corpus by which rules a room's rotation holds separates two fields out of
the `0x87`–`0x9b` run that nothing else did, and gives a third a direction.

**The Sneaking role slots.** All fourteen bodies whose rotation holds neither SNE nor
TSNE read `0x7f 0x7f` at `0x9a`, and the four bodies that carry a real slot pair all
belong to one room — `69.213.233.238:5731`, which plays SNE and SCAP — where the pair
moves between bodies (`03/02` → `03/06` → `04/09`). The other sixteen bodies that do
hold SNE or TSNE read `0x7f 0x7f` as well, so the reading is not "this room has Snake" but
"which slots hold the roles for the round being played": an unassigned pair is a round
that is not a Sneaking round. The panels name the roles: "One player controls Snake…
If many players join, one player becomes Metal Gear Mk.II and can support Snake."

**The ticket pair's direction.** `0x93` holds `20`, `40`, `50`, `51` or `55`, and the
pair at `0x5a`/`0x5e` is at or below it in all 34 bodies: equal where the round is
untouched (`40/40`, `50/50`, `55/55` against `0x28`, `0x32`, `0x37`) and below where it
has been spent (`30/32` against `50`, `5/5` against `50`, `5/5` against `20`, `15/33`
and `48/47` against `50`). So in a ticketed room `0x93` is the starting count and the
pair is what is left of it — the two labels the screen gives are `Starting Number of
Tickets` (`0x0cceed`) and `TICKETS` (`0x0cce2f`), and the tooltip at `0x0cee5e` states
the rule: "One ticket is used each time a player respawns on the field." The SNE/SCAP
room holds `51` with no ticketed rule in its rotation, which is why the reading is
scoped to rooms that play one.

**Snake's dog-tag count.** `0x91` is `2` in every body but the SNE room, which carries
`0x14` = `20`. The SNE panel's win condition is "Snake wins if he collects a set number
of dog tags", so 20 is a candidate for that number — one room, one value, **[U]**.

**What would close it.** The room-details reply (`0x4313`) for one room carries the
same settings as values, so one capture of a join alongside its gameplay wire turns
every **[I]** in that table into a **[V]** — and the two candidates for field 3 into
one.
