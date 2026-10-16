# The P2P module's record dispatch: the addresses, and what the dump resolves

**What this file is.** The read-out of how `MGO2.ELF` hands an inbound record to its
handler and how it decides which records a peer may carry, with every address taken
from the disassembly below and the run-time values taken from `rpcs3.dmp`. It closes
the route `docs/analysis/replay-findings.md` §3 left open, and it corrects one
address in that section.

The two questions a reader comes here with are: *which function handles a given
`vid`*, and *where does the game say so*. §3 answers the first — the handler is an
address the *registry entry* carries, and the thing the module keeps per `vid` is not a
handler pointer but a use-count — and §3.6 is the second half of the same chain, the
per-operation **field schema** every operations record is read against.

## 1. What was read, and with what

* `tools/mgo2_disasm.py <start> <end> MGO2.ELF` — the disassembly quotes below.
* `tools/mgo2_static_use.py <low> <high>` — the register-tracking scan that names the
  function reading a static field (§3.4). The addresses in the block below are never
  materialised, so carrying the register that holds their base is the only static
  route to a reader.
* `tools/mgo2_record_schema.py` — §3.6's descriptors and their field tables, read out
  of the image, with the coverage check that ties a table to its state block.
* `tools/udp_frame.py` / `tools/udp_session_records.py` — the wire framing and the
  `vid` / class split (`type = (vid & 0xfff) | class-bits`, §3 of `UDP_P2P_PROTOCOL.md`).
* `rpcs3.dmp` — an 8.5 GB Windows minidump (`MDMP`), read here through its
  **Memory64List** (stream type 9, file offset `0x13A62F`): 5 498 ranges, and the
  guest's memory is the host mapping at **`guest + 0x300000000`**. That mapping is the
  whole of the dump's usefulness below; `tools/*` reads the file by a single constant
  delta, which is why the guest's data segments look file-identical through it.

## 2. The module instance: where the module keeps its state

The module reaches its own state through two loads that every one of its functions
opens with (`0x261FE0` session create, `0x2614F8` the lookup, `0x261AB0` the send
path):

```
lwz r30, -0x7ABC(r2)     ; r2 = TOC (0x01222A00) -> r30 = 0x011AE308
lwz r10, -0x8000(r30)    ; r10 = *(0x011A6308) = the module instance
```

So **`0x011A6308` is a static holding the instance pointer**, and the instance is at
`0x01856C10` **at run time** — the dump's word there, not a value in the image. The
instance is what the code tests:

| field | meaning | value in the dump |
|---|---|---|
| `+0x00` | module state; `0x261FE0` returns 0 unless it is exactly **2** | `2` |
| `+0x28` | the session array, **24** slots of **`0x6D8`** bytes | `0x50000060` |
| `+0x2C` / `+0x30` | the two channel objects the lookup's key is banked on | — |
| `+0x34` | the per-`vid` descriptor array (§3) | `0x500CE4A0` |

`docs/analysis/replay-findings.md` §3 reads `0x011A6308` as the instance itself. It is
not: the image's bytes at `0x011A6308` are the pointer, and the instance is the
allocation it names. The descriptor array hangs off **that**, which is why the
image's own bytes at what used to be read as `instance + 0x34` (`0x011A633C`) are a
block of `float` constants (`0x3F800000`, `0x40AAAAAB`, `0x42C80000`, `0x20C49BA5E353F7CF`,
which is exactly what the dump shows there too, because the image is what is there).

## 3. The dispatch, read out

### 3.1 The lookup: `FUN_002614F8` — `vid` and channel to a descriptor

`0x2614F8` returns the address of a 0x10-byte descriptor entry, or `-1`:

```
0x2614FC: lbz   r0, 5(r4)        ; the record object's byte +5
0x261508: cmplwi cr7, r0, 5      ; <= 5 takes the other key form (0x2615CC)
0x261510: lwz   r10, -0x8000(r30)  ; the instance
0x26151C: cmpw  cr7, 0x2C(r10), r4 ; which of the two channels is the caller's
0x261530: beq   cr7, 0x261540      ; bank 1 -> r9 = 1
0x261544: addi  r4, r9, 0x18       ; the key's top byte: 0x18 or 0x19
0x26154C: lwz   r10, 0x34(r10)     ; the descriptor array
0x261550: clrlwi r9, r8, 0x10      ; the vid
0x261554: slwi  r0, r4, 0x18       ; (0x18 | bank) << 24
0x26155C: or    r6, r0, r9         ; the key
0x261560: ori   r11, r11, 0xF460   ; the walk opens 0xF460 into the array
0x261574: add   r11, r11, r10
0x26157C: mtctr r0x1000            ; at most 0x1000 entries
0x261588: clrldi r3, r11, 0x20     ; the entry
0x26158C: addi  r11, r11, 0x10     ; 0x10 bytes per entry
0x261590: subf  r0, r10, r11       ; the walk's own offset
0x261594: lbz   r9, 0xA(r3)        ; the entry's flag byte
0x2615A4: bge   cr7, 0x2615B8      ; bit 7 clear -> the entry is not considered
0x2615A8: lwz   r3, 0(r3)          ; the entry's key
0x2615AC: and   r0, r3, r7         ; & 0x7F000FFF
0x2615B0: cmpw  cr7, r0, r6
0x2615B4: beq   cr7, 0x261534      ; -> returns the entry's address
0x2615B8: ble   cr6, 0x2615C0      ; offset <= 0xFFFF: keep walking
0x2615BC: mr    r11, r10           ; else wrap to the array's own base
0x2615C0: bdnz  0x261588
```

Four facts fall out of this and none of them is a guess: the key is
**`((0x18 + bank) << 24) | (vid & 0xFFFF)`** where the bank is which of the module's
two channel objects the caller passed; an entry is only considered when **bit 7 of its
byte `+0xA` is set**; the walk is bounded at **`0x1000`** entries; and it **wraps** to
the array's base after `0xFFFF` bytes, so the walk is a 64 KB window of `0x1000`
entries entered at `+0xF460` and read through the wrap. The key's `bank` byte pairs with
`0x261490`'s index 0x18/0x19, so this registry is the **channel** one: its keys carry a
channel, and the `cmpw` against `word0 & 0x7F000FFF` drops the 12–23 bits that hold the
entry's own index (§3.2.1) — which is why one walk serves both channels' keys. `0x261438` is the same walk for
a bare handle (`lwz r3, 0(r3)` at `0x26B1DC` and friends).

### 3.2 The registrar: `FUN_00261AB0` and `0x261D40` — the key carries its own offset

`0x261AB0` is the send-side counterpart: it walks the same ring, and where the
lookup returns `-1` it **creates** an entry at the first free slot:

```
0x261B28: ori   r0, r4, 0x800     ; the class bit the vid family carries (0x261 / 0xA61)
0x261B2C: lwz   r6, 0x34(r10)     ; the ring
0x261B48: or    r5, r11, r0       ; the key, once r11 holds the channel's block byte
0x261D40: subf  r9, r3, r9        ; offset = entry - base
0x261D48: rlwinm r9, r9, 8, 0, 0x13   ; offset << 8, into bits 8..23
0x261D4C: stb   r0, 0xA(r10)      ; the entry's byte +0xA = 0x80  <- the "used" bit
0x261D50: or    r9, r5, r9        ; word0 = key | (offset << 8)
0x261D68: stw   r9, 0(r10)        ; the entry's word 0
0x261D6C: bl    0x26B178          ; initialise the rest of the entry
```

So an entry's word 0 is
**`((0x18 + block) << 24) | (offset << 8) | (vid & 0xFFF)`** — its own byte offset into
the ring is a field of its key, which is why the walk's `& 0x7F000FFF` mask drops bits
12–23 and compares only the block byte and the vid. `0x26B178` fills what is left:
word 0 set to the key again, the two `sth` counts at `+4`/`+6` cleared, byte `+8` = 1,
byte `+9` = `0x80`, bytes `+0xB`–`+0xE` cleared, and the handle handed back through its
first argument. **An entry is a key with a use count, not a key with a handler** — which
is the point the next section settles.

### 3.2.1 The handle *is* the key: index in bits 12+, vid in bits 0–11 — `FUN_00261438`

`0x261438`, the function every consumer calls with a handle, settles what an entry is:

```
0x26143C: rldicl r0, r3, 0x34, 0x34   ; r0 = (handle >> 12) & 0xFFF  — the entry's INDEX
0x261444: slwi   r11, r0, 4           ; × 16 = the entry's byte offset
0x261460: lwz    r9, -0x8000(r30)     ; the instance
0x261464: lwz    r0, 0x34(r9)         ; the table base
0x261468: add    r0, r11, r0          ; base + index × 16
0x261470: lwz    r0, 0(r9)            ; the entry's word 0
0x261474: cmpw   cr7, r0, r3          ; == the handle, or nothing
0x26147C: lbz    r0, 0xA(r9)          ; and the used bit has to be set
```

So a handle **is** the entry's word 0, and its shape is **`(index << 12) | vid`**: the
consumer hands back the same word the entry carries, and the entry it lands on is the one
whose index those bits name. This is the table the dump holds — 18 entries at the base,
listed in §4 — and it is *not* the table §3.1's key addresses: that one is keyed
`(lane << 24) | vid` and lives a lane away. Two registries, one per direction: §3.1's is
built by a **send**, this one is looked up by a **receive**.

There is no field in the 16-byte entry for a handler either way: `+4`/`+6` are the counts
`0x26B178` clears, `+8` = 1, `+9` = 0x80, `+A` the used bit, `+B`–`+E` zero, and the rest of
the word is state.

`0x261490` — the resolver beside it — names the other index space, the one §3.1's bank byte
picks from: index 0x00–0x17 is a session (`*(instance + 0x28) + index × 0x6D8`, live when
its byte `+4` is nonzero) and **0x18 / 0x19 are the two channel objects at
`instance + 0x2C` / `+0x30`**. That is what the lookup's bank byte is: a channel, and the
keys it walks are `base + channel × 0x10000 + offset`.

### 3.3 The handler is an address an entry carries, not a `vid`'s

`0x27A180` runs one entry of the registry §3.3.1 describes, and the entry decides
what is called:

```
0x27A194: lbz   r0, 0xd(r11)     ; the entry's byte +0xD
0x27A1A0: rlwinm r0, r0, 0, 0x1e, 0x1e   ; its bit 1
0x27A1A8: beq   cr7, 0x27a1e8
; --- bit 1 set: the entry's +4 is an OBJECT, called through its vtable slot 0
0x27A1AC: r11 = lwz 4(r11)
0x27A1B4: r9 = lwz 0(r11)      ; the object's vtable
0x27A1B8: r10 = lwz 0(r9)      ; slot 0's OPD entry
0x27A1C0: r0 = lwz 0(r10)      ; that entry's code; bctrl
; --- bit 1 clear: the entry's +4 is an OPD itself, called with the entry's +8
0x27A1E8: r9 = lwz 4(r11)
0x27A1F0: r3 = lwz 8(r11)
0x27A1F8: r0 = lwz 0(r9) ; r2 = lwz 4(r9) ; bctrl
```

So a `vid` still has no handler address of its own, and the reason is now mechanical:
what is called for a stream is whatever address the *registry entry* carries, and that
entry is filled by whoever registers the stream. §3.3.1 reads the registry out.

### 3.3.1 The registry: the container, its entries, and the live one in the dump

The registry is the object §3.4's `0x01191B30` is the vtable of: `0x26E790` is its
slot 0, and it walks its entries and calls `0x27A180` for each one that is ready.

```
0x26E7D0: lwz   r9, 0xc(r3)      ; the entry count
0x26E810: bl    0x26cad8         ; memcpy(count * 16) into the frame
0x26E83C: lwz   r3, 0x14(r28)    ; the lock
0x26E8C8: lbz   r0, 0xc(r27)     ; an entry's byte +0xC, against a stack read
0x26E9CC: r3 = r28, r4 = r25, r5 = r21, r6 = 0   ; then `bl 0x27a180`
```

`rpcs3.dmp` holds exactly one live instance — word 0 = `0x01191B30` at **`0x501030D4`**,
the only occurrence of that word in `0x50000000`–`0x51000000` and
`0x01800000`–`0x02000000`. Its `+0xC` is **16** and its `+0x10` is **`0x50103140`**, and
ten of the sixteen 16-byte entries are filled:

| entry | +0 | +4 | +8 | tag bytes `+0xC` |
|---:|---:|---|---|---|
| 0 | 6 | object `0x54CC711C` | 0 | `01 02 00 00` |
| 1 | 5 | object `0x54CC6A60` | 0 | `06 02 00 00` |
| 2 | 4 | `0x011F1928` → `0x282D80` | `0x50103340` | `07 01 00 00` |
| 3 | 2 | `0x011F1310` → `0x26DE98` | 0 | `08 01 00 00` |
| 4 | 8 | object `0x54CC7144` | 0 | `09 02 00 00` |
| 5 | 1 | `0x011F17E0` → **`0x27E8B0`** | 0 | `0B 01 00 00` |
| 6 | 3 | `0x011F12F0` → `0x26DB78` | 0 | `0D 01 00 00` |
| 7 | 7 | object `0x54CC7130` | 0 | `81 02 00 00` |
| 8 | 9 | object `0x54FC6AE4` | 0 | `85 02 00 00` |
| 9 | 0xA | object `0x54FC6AF8` | 0 | `86 02 00 00` |

Three of the ten name the *module's own* vtable (`0x011A63C8`) slots — entry 5 its slot
0, entries 3 and 6 slots 1 and 2 (`0x26DE98`, `0x26DB78`) — and the rest name objects
whose own word 0 is a vtable of the module (`0x01195448`, `0x011953A8`, `0x01195888`)
allocated outside it. Entry 5 is the one whose handler is the module's own slot 0,
`0x27E8B0` — and **an earlier reading of this file took that to mean the operations
stream's records are its**; §3.6 measures that it is not (its parse is a state transfer,
and the operations bodies are too short for it), so the tag byte's meaning is **[U]** and
the entry stays only as evidence that the registry keys one handler per tag.

### 3.4 The stream classes, and where their vtables come from

`tools/p2p_vtable_scan.py` finds **11** vtables whose slots resolve into the module, and
four of them are one class family, 14 slots each. They are **not** four `vid` classes:
they are the module's own stream objects, and the dump says which is instantiated.

| vtable | slot 0 | slot 4 | slot 8 | slot 9 | slot 10 |
|---|---|---|---|---|---|
| `0x01191AB8` | `0x263328` | `0x2633C8` | `0x269188` | `0x2691A0` | `0x2690F0` |
| `0x01191C68` | `0x263328` | `0x285210` | `0x285030` | `0x285160` | `0x284F78` |
| `0x01191CA8` | `0x263328` | `0x285670` | `0x285678` | `0x285698` | `0x2690F0` |
| `0x01191CE8` | `0x263328` | `0x2856E0` | `0x285978` | `0x285AA8` | `0x2690F0` |

Slots 1–3, 5–7 and 11 are identical across all four — `0x268930` at slot 1 is the
serializer's neighbour, `0x2632B0`/`0x2635F0` at 6 and 7 the reader pair — and slot 10
is `0x2690F0` for three of them and `0x284F78` for `0x01191C68` alone. So the four differ
in their *body handling* and share everything else, and slot 0 of all four is
`0x263328` — two instructions, `lbz r3, 4(r3) / blr`, a getter of the object's byte
`+4`. An earlier reading of this section called them "the four class-bit combinations the
wire type word carries"; the codec's class bits do select how a frame is *delivered*, but
**nothing read here ties a class bit to one of these four objects**, and the count of
live instances below is what that reading contradicted.

**They are the module's stream objects, and each one's vtable is installed from the
module's static block.** `tools/mgo2_static_use.py` resolves which function reads each
vtable word, and every reader loads the word from the block and *stores it into an
object it is building* — which is what makes the reader a class's builder rather than a
user of it. `0x263B70` is the first of them:

```
0x263B7C: lwz   r30, -0x7ab8(r2)   ; r30 = 0x011ae328, the anchor for this block
0x263B98: lwz   r9, -0x7ffc(r30)   ; = *(0x011a632c) = 0x01191ab8, the class's vtable
0x263BBC: stw   r9, 0(r3)          ; this->vtable = it
0x263BC0: stw   -1, 0x48(r3)       ; and the rest of the object's fixed part:
0x263BC4: stw   0, 0x3c(r3)
0x263BC8: stw   0, 0x38(r3)
0x263BCC: stw   0, 0x34(r3)        ; three region bases, filled by the three counts
0x263C40: stb   0, 4(r29)          ; the byte slot 0's getter reads
```

Its caller is the **session allocator**, `0x262D10`, which walks the module's slots and
builds one object per slot with the three region sizes `0x4000`, `0x2000`, `0x2000`:

```
0x262D54: r3 = the slot index; 0x262D6C..0x262D74: the instance's fields
0x262D9C: beq  -> the loop's end
0x262DB8: r4 = the object, r5 = 0x4000, r6 = 0x2000, r7 = 0x2000
0x262D84: bl   0x263b70
```

and the dump confirms the product exactly: **all 24 session slots carry `0x01191AB8`** —
`0x50000060 + index × 0x6D8`, word 0 = the vtable, byte `+4` = the peer's state (`8` for
the one live session, `0` for the 23 untouched — the same byte slot 0's getter reads).

| class | vtable | vtable word read from | read (and installed) by | live instances in the dump |
|---|---|---|---|---|
| base | `0x01191AB8` | `0x011A632C` | `0x263B70`, `0x263C70` | **24** (one per session slot) |
| B | `0x01191C68` | `0x011A6494` | `0x2849E8`, `0x284CB0`, `0x285030`, `0x2850C8`, `0x285160` | none |
| C | `0x01191CA8` | `0x011A64AC` | `0x2852D0`, `0x2854A0`, `0x285678`, `0x285698` | none |
| D | `0x01191CE8` | `0x011A64B4` | `0x285978`, `0x285A10`, `0x285AA8`, `0x286358`, `0x2868B0` | none |

The second vtable word beside each of those — `0x01191AF8`, held at `0x011A6330`,
`0x011A6490`, `0x011A64A8`, `0x011A64B8` — is the *shared secondary* vtable, read by the
same classes' own methods (`0x269130`, `0x269148`, `0x269188`, `0x2691A0` for the base),
which is how a class's constructor and its methods were told apart. B, C and D's
constructors have **no `bl` caller anywhere in the image** — they are reached only
through a stored pointer — which is why only the base is instantiated in a dump taken
from a client that never opened a second stream.

The static block is a region of statics, not a table of classes: the replay path's
object is held in it too — `0x011A6328` = `0x01856CA8`, which `0x263360` writes its first
argument at `+0` and 32 bytes at `+8` into (`tools/mgo2_static_use.py 0x011a6328
0x011a6338`; `UDP_P2P_PROTOCOL.md`'s note on `0x263360` is the same object, reached from
the function holding the `replay/replay0.dat` string). **`0x26AA28` is not a factory**
either, as an earlier reading of this section had it: it is three instructions in the
descriptor ring's *found-entry* path — `lwz r0, 0(r4) / stw r0, 0(r9) / blr`, called once
from `0x261D04` to hand back the entry's word 0 as the handle §3.2.1 describes. And
`0x26E790` is not a factory but the registry walk of §3.3.1.

The module object's own vtable `0x011A63C8` has **slot 0 = `0x27E8B0`**, the body
dispatcher, which the dump independently confirms by holding exactly the table
`0x27E8B0` indexes.

### 3.5 The body dispatch **is** a table, and the dump gives its entries

`0x27E8B0` (the module object's own vtable slot 0) reads one byte through the byte
reader at `0x26CB20`, scales it by four (`rlwinm r0, r0, 2, 0x16, 0x1D`) and loads
through a table — one-byte opcode, four-byte entries, a null entry that branches away:

```
0x27E8C4: lwz   r30, -0x7a88(r2)   ; r30 = 0x011AE46C
0x27E8E8: bl    0x26CB20           ; read 1 byte: the record's first body byte
0x27E8F4: lwz   r9, -0x8000(r30)   ; the table base = *(0x011A646C)
0x27E8FC: lwzx  r0, r9, r0         ; entry = *(base + opcode * 4)
0x27E914: lwz   r5, 0(r29)         ; the entry's word 0: the operation's FIELD COUNT
0x27E918: r5 = (r5 * 2 + 7) / 8 ; +1 ; /2      ; = ceil(count / 8) BYTES
0x27E938: bl    0x26CB20           ; read that many: a presence bitmap, one bit a field
0x27E964: for each bit index i below count:
0x27E980: r3 = the descriptor, r4 = i, r5 = the record
0x27E998: r9 = lwz 0x10(r29)       ; the descriptor's +0x10: the handler object
0x27E99C: r11 = lwz 0(r9)          ; its vtable (an OPD pointer)
0x27E9A0: r0 = lwz 0(r11) / r2 = lwz 4(r11) / bctrl   ; only for the bits that are set
```

Read through the dump, **the table is `0x0187E990`** with **26 live entries, indices
`0x00`–`0x19`**, and every one of them reaches the *same* code — the handler object
`0x0122A5C8`, vtable `0x011F17F0`, **slot 0 = `0x27EC48`**:

| index | descriptor | fields | presence map | state block (`+0x14`) | `+0x18` |
|---|---|---:|---:|---:|---:|
| `0x00` | `0x012298A8` | 55 | 7 B | `0xA8` (168) | `2` |
| `0x01`–`0x18` | `+0x1C` each | 164 | 21 B | `0x4E0` (1248) | `0x100`/`0x102` |
| `0x19` | `0x01229B64` | 44 | 6 B | `0x11C` (284) | `0` |

**The 26 entries are *states*, not the operations stream's opcodes.** Three things say
so, and each is a dump read rather than an inference:

* the lookup's mask is a **byte**, not 26: `rlwinm r0, r0, 2, 0x16, 0x1D` at `0x27E8F8`
is `(byte << 2) & 0x3FC`, and the bit range 22–29 is bits 2–9 of the word — the code is
written against a **256-entry** table while the module fills 26;
* the 24 entries `0x01`–`0x18` share one field table *and* one shape, and their state
blocks are **consecutive 1248-byte blocks** — `0x01876F88`, `0x01877468`, `0x01877948`
… every one exactly `0x4E0` after the last — which is a **per-slot** array, not a
per-opcode one (the session's own slot count is 24, §2);
* their maps are consecutive too: `0x0187E488` and `+0x29` each (41 B), the 24 of them
ending exactly where `0x19`'s map begins, `0x0187E860`.

So `0x00` is one state (168 B), `0x01`–`0x18` are the session's **24 slot states**
(1248 B each) and `0x19` a third (284 B). The descriptors are **in the image** —
`ff`/`dump` agree byte for byte at `0x012298A8`, because only their `+0x08`, `+0x0C` and
`+0x18` are run-time values — so the schema is data and the *code* every state runs is
the one address, `0x27EC48`. `0x27EC48` is what turns one set presence bit into one
field: it takes the descriptor and the bit's index, finds that index's entry in the
field table, and moves the field's bytes between the **state block** and the stream.
§3.6 reads the tables out and names its branches; `tools/mgo2_record_schema.py` prints
them.

The allocation the table lives in is **one block with three pointers into it**: the
26-entry table at `+0`, a **24-entry pool** at `+0x68` (`0x0187E9F8`, index 0 = `2`, the
other 23 words `0xffffffff`, which `0x27E748` walks and frees entry by entry), and a
third segment at `+0xD0` (`0x0187EA60`), neither of which is a descriptor array. The
statics `0x011A6470` and `0x011A6474` hold those two inner pointers.

### 3.6 The state schema: fields, their offsets, the flags, and what reads it

A **state's descriptor is 0x1C bytes** and a **field entry is 10**, and both live in
the image, so the whole schema is a static read-out:

| descriptor | | field entry | |
|---|---|---|---|
| `+0x00` | the number of fields (55 / 164 / 44) | `+0` u16 | the field's offset in the state block |
| `+0x04` | the field table: `count × 10` bytes | `+2` u16 | its length in bytes |
| `+0x08` | the state block (run time) | `+4` u16 | a multiplier the routine applies to the length |
| `+0x0C` | the state's own map, `count × 2` bits (run time) | `+6` u16 | flags — see below |
| `+0x10` | the handler object | `+8` u16 | spare |
| `+0x14` | the state block's size — **the check** | | |
| `+0x18` | a run-time word | | |

**The flags at `+6`, named from `0x27EC48`'s own branches.** The routine is called once
per set presence bit, with the descriptor in `r3`, the field index in `r4` and the
stream in `r5` (`0x27E980` passes the stream, so the same routine serves both
directions), and it does exactly this:

| test | at | meaning |
|---|---|---|
| `rlwinm r0, r9, 0, 22, 22` = **bit `0x200`** | `0x27EC98` | **the field is a container**: its own bytes are *not* moved; the routine walks the field table for entries whose offset falls inside this field's `[offset, offset+length)` and re-enters per nested entry with nonzero flags (`0x27ECA4`–`0x27ED20`, out through `0x27EE10`) |
| `rlwinm r0, r9, 0, 23, 23` = **bit `0x100`** | `0x27ED28` | **one contiguous block**: `length × multiplier` bytes are moved in a single call, at `state + offset` (`0x27ED5C`) |
| neither bit | `0x27EDBC` | **`multiplier` elements of `length` bytes**: the loop moves `length` bytes and advances the state pointer by `length`, `multiplier` times (`0x27EDE0`–`0x27EE08`); with `multiplier ≤ 0` nothing is moved at all (`0x27EDC8` falls to the map write) |
| — | `0x27ED68` | **every** path ends here, and it sets a **two-bit code `2`** for this field index in the state's own map at `+0x0C`: `base + i/4`, `2 << (2 × (i & 3))` — the map is `count × 2` bits, i.e. `ceil(count / 4)` = **41 bytes** per 164-field state, which is the `0x29` stride the 24 maps are allocated on |

The **low byte** of the flags (`0x24`, `0x25`, `0x35`, `0x40`, `0x64` recur) is read by
none of `0x27EC48`'s branches — and it is not unread: `0x27E578`, the one-field *setter*,
locates the same field entry, tests `0x0001` and `0x0040` of the flags
(`0x27E6A8`, `0x27E6BC`) and writes the field's slot in the state's own map with a
**two-bit code `3`** (`0x27E6E0`, `descriptor + 0x18 |= 1`). So the low byte is a
per-field **publication** gate — `0x0040` publishes the field, `0x0001` publishes it only
when the write changes it — and the map is a queue of pending transfers carrying a
*direction*: the reader path leaves `2`, the setter leaves `3`
(`docs/analysis/udp-handlers-and-senders.md` §2). The two tested bits are not rare: state
`0x00` has five container fields
(`0x0225` ×2, `0x0235` ×1) and two blocks (`0x0125` ×2), a slot state has three
containers (`0x0200`, `0x0210`, `0x0220`) and one block (`0x0125`), and state `0x19` has
neither — every one of its 44 fields is `0x0000` but one `0x0010`. `0x0010` itself is
read by no branch of `0x27EC48` either.

**The field tables check themselves against `+0x14`.** The three tables are back to back
in one span — `0x01229B80` (55 entries) → `0x01229DA6` (164, *shared by the 24 slot states
`0x01`–`0x18`*) → `0x0122A40E` (44) → `0x0122A5C6`, with the handler object at
`0x0122A5C8` — and for 25 of the 26 states the last field ends **exactly** at the
state block's size: 1248 for `0x01`–`0x18`, 284 for `0x19`, and 166 of 168 for `0x00`.
That is what makes the entry's `+0` a *state-block offset* rather than a wire offset, and
the fields a description of a state block rather than of a body:

```
state 0x00: 55 fields, table 0x01229b80, state block 168 bytes
   [  0] state+0         1 byte(s) x1  flags 0x0025
   [  1] state+1        16 byte(s) x1  flags 0x0125
   [  2] state+21       48 byte(s) x1  flags 0x0125
   [  3] state+69        1 byte(s) x1  flags 0x0025
   ...
   [ 54] state+165       1 byte(s) x1  flags 0x0000
   fields end at 166 of the 168-byte block

state 0x19: 44 fields, table 0x0122a40e, state block 284 bytes
   [  0] state+0       140 byte(s) x1  flags 0x0000
   [  1] state+0       140 byte(s) x1  flags 0x0010
   [  2] state+0        68 byte(s) x1  flags 0x0000
   [  3] state+68       68 byte(s) x1  flags 0x0000
   [  4] state+140      16 byte(s) x1  flags 0x0000
   [  5] state+156       4 byte(s) x1  flags 0x0000
   ...
   fields end at 284 of the 284-byte block
```

Three of these fields are worth naming for that alone: `0x19`'s first two are
a **140-byte block** at `state+0` — the size of the profile record the roster carries
(`0x1001`, len 140 in every capture) — and the same state carries a 68-byte and a
44-byte block behind it, which are the shapes a state block of 284 bytes has room for.

**Does the presence-map parse appear on the wire? Measured: no — it is a *state*
transfer, and the operations records are somebody else's.** The test is the schema's own
arithmetic, run over every record body of three captures decoded whole
(`tools/mgo2_state_wire_check.py`: `mgo2-game-joined-when-red-team-then-briefing`,
`mgo2-game1`, `mgo2-game2`): take the body's first byte as the state index, find the
descriptor, and compare `1 + ceil(count / 8) + Σ(length × multiplier over the set bits)`
with the body's length. **Distinct bodies** are what count, since a capture repeats one
body hundreds of times and a repeat is not a second fit:

| | bodies | distinct |
|---|---:|---:|
| read | 406 144 | 177 179 |
| whose first byte is an index the table has (`≤ 0x19`) | | 14 500 |
| consumed exactly by *some* index of the table | | 2 490 (1.41%) |
| …of those, against a state with no container field, where the sum is exact | | 1 268 |
| the operations identifier's records (`id & 0xfff == 0x254`), which fit | 2 214 | **0** |

The check counts a container field as if its own bytes moved (`0x27EC48` moves the fields
nested in it instead), which is exact for a state with no `0x200` field and can only
over-count otherwise; the two figures are printed apart for that reason. The 1.41% are
unrelated tick bodies (`0x0063`, `0x0081`, `0x8077` at 16–17 bytes) whose byte count
happens to land on a state's map plus a few fields.The operations identifier (`0x1254` / `0x1a54`, whose recorded shapes are the table
below) is the decisive case, and it is decisive in **both** directions: of its 2 214
records none fits, and the lengths show why at each end of the range. Its bodies are
**2–3 bytes** (the clock, the kill feed, the state write, the short record), while the
smallest presence map the table has is 6 bytes (`0x19`) and 7 for `0x00`, so a `01 …` body
is 3 bytes against 22 needed; and the **state record itself** — `0x00` and its 89 state
bytes, 26 of them in the three captures — carries 90 bytes where the parse of that same
first byte and its next seven as a map wants **28** (`02 02 00 02 02 00 00` is four set
fields, not a state). Neither too short nor too long: the wire's state block is not this
table's presence map.

The recorded shapes, by the stream's own opcodes (which the replay parser and
`MatchRecordBodiesUtils` name — `0x00` state, `0x01` clock, `0x02` state write, `0x04`
objective feed, `0x05` slot, `0x09` kill feed, `0x0b` briefing countdown, `0x0e`
round-over, `0x12` race goal, `0x13` rating, `0x0f` unnamed), are:

| opcode | host → joiner | joiner → host | a record |
|---|---|---|---|
| `0x00` | 24 B ×13 | — | `00 02 02 00 02 02 00 00 …` |
| `0x01` | 3 B ×586 | — | `01 80 00` |
| `0x02` | 4–6 B ×13 | — | `02 02 01 02` |
| `0x09` | 3 B ×243 | — | `09 02 01` |
| `0x0b` | 7 B ×267 | — | `0b 3c 00 40 00 00 00` |
| `0x0f` | 2 B ×13 | — | `0f 00` |
| `0x26` | 3 B ×43 | — | `26 02 06` |
| `0x1d` | — | 7 B ×10 | `1d 05 01 02 01 00 00` |
| `0x1f` | — | 3 B ×2 | `1f 05 01` |
| `0x22` | — | 22 B ×2 | `22 05 00 0b 16 00 0e 0e …` |

So the two levels are separate, and each is now read out:

* **The state level** — `0x27E8B0` and `0x27EC48`: a state index, `ceil(count/8)` bytes of
  presence map and the present fields, moved between a state block and a stream, with the
  two-bit code the routine leaves in the state's own map (`0x27ED68`). It is the path the
  26 descriptors describe, it *cannot* be the operations records' parse (above), and the
  object the registry walk hands it is an entry, not a record cursor (`0x26E9CC` passes
  `r1+0x74`).
* **The wire level** — the per-record handlers of the registry (§3.3.1), one per key, each
  reading its own body shape, and each *updating* the state level rather than being run by
  it. What the registry's own tick settles is that a *queued* record is dispatched by its
  first byte; whether the wire's records are the ones that queue carries is the one step
  left **[U]** (`udp-handlers-and-senders.md` §4). Live in the dump: key `0x08` → `0x26DE98`, which loops over `[u8 key][u16 value]`
  triples and calls `0x280A38` per one — a *stepper* over three-byte groups, which is the
  shape of several of the bodies above (`09 02 01`, `26 02 06`); key `0x0d` → `0x26DB78`,
  which reads one byte, calls `0x27E2B0` — one of the **14** functions that read
  `*(0x011A646C)`, i.e. the state table — then `0x27E578(…, 0x63, 1, byte)` and loads
  state `0x00`'s descriptor.

**The joiner's own three records (`0x1d`, `0x1f`, `0x22`) are named by neither table, and
the schema cannot decode them.** They are past the end of the state table — its indices
stop at `0x19`, and the lookup has no bound check, so `0x1d` resolves to
`0x0187E990 + 0x74` = the **24-slot pool's** fourth word, `0xffffffff`, which the code
would then dereference — and their bodies are 3, 7 and 22 bytes, none of which can hold a
presence map. The replay parser's opcode table has no entry for them either. What the
bodies do carry is the only evidence there is: each opens `op, 0x05, …`, the three keys
are the host's `0x0d`, `0x0f` and `0x02` with a high bit set (`0x1d = 0x0d | 0x10`,
`0x1f = 0x0f | 0x10`, `0x22 = 0x02 | 0x20` — the same idea as the registry's `0x81`/`0x85`
keys), and `0x22`'s 22 bytes decompose evenly as `[op][0x05][18 bytes]`. Which is a
**candidate** reading, not a decode: the host-side dispatch for a joiner's records is a
table *this* client never fills (the two inner pointers of §3.5's allocation are not
descriptor arrays in the dump), so what `0x1d`, `0x1f` and `0x22` say stays **[U]** and
should not be written into a server body until a host-side capture shows it.

## 4. What the dump does and does not resolve

| question | answer | how |
|---|---|---|
| the instance | `0x01856C10` | the word at `0x011A6308`, read at `guest + 0x300000000` |
| its state gate | `2` (`0x261FE0` proceeds) | word `+0` |
| the session array | `0x50000060`, 24 × `0x6D8` | word `+0x28`; slot 0 carries `0x01191AB8`, one of §3's dispatch vtables |
| the descriptor ring | `0x500CE4A0` | word `+0x34` |
| the **inbound** registry (`0x261438`'s table) | **18 entries, listed below** | `base + index × 16`, word 0 = `(index << 12) \| vid` |
| the **channel** registry (§3.1's keys) | **empty** | `base + 0x180000` and `base + 0x190000` are 64 KB of zeros each, and no 16-byte-aligned word anywhere in `0x50000000`–`0x50100000` carries a `0x18`/`0x19` key with bit 7 of `+0xA` set |
| the **state** table | `0x0187E990`, 26 state descriptors, one handler `0x27EC48` | §3.5, §3.6 |
| the **registry** the entries that call it live in | **1** live container, `0x501030D4`, 16 slots, **10 used** | word 0 = `0x01191B30` |
| the live container's keys and the code each reaches | ten keys, **seven** codes: `01`/`09`/`81` → `0x77FA20`, `06` → `0x77E320`, `85`/`86` → `0x7A43E0`, `07` → `0x282D80`, `08` → `0x26DE98`, `0B` → `0x27E8B0`, `0D` → `0x26DB78` | `udp-handlers-and-senders.md` §3 |
| the entry of that registry whose handler is the module's own slot 0 | entry 5, OPD `0x011F17E0` → `0x27E8B0` | §3.3.1 |
| the four stream classes' vtables | read out of the module's static block by their constructors | §3.4 |
| the record *class factory* an earlier reading looked for | **does not exist as such**: the vtables are installed by the classes' own constructors, and what a stream's records run is the registry entry's address | §3.3, §3.4 |

### 4.1 The inbound registry, entry by entry

`base = 0x500CE4A0`, `base + index × 16`, word 0 = `(index << 12) | vid`, and the vid is
the one the engine uses — the wire writes it with the `0x800` bit set, `vid | 0x800`, so
`0x0A54` on the wire is `0x1254` / `0x1A54` in the capture:

| idx | word 0 | vid | wire id(s) | what the docs call it |
|---:|---|---:|---|---|
| 0 | `0x00000002` | `0x002` | `0x0002` | — |
| 1 | `0x00001001` | `0x001` | `0x1001` / `0x5001` | player entry, keep-alive class |
| 2 | `0x00002A54` | `0xA54` | `0x1254` / `0x1A54` | **operations** |
| 3 | `0x00003A52` | `0xA52` | `0x1252` / `0x1A52` | — |
| 4 | `0x00004A61` | `0xA61` | `0x0261` / `0x0A61` | **rules roster** |
| 5 | `0x00005A50` | `0xA50` | `0x1250` / `0x1A50` | — |
| 6 | `0x00006A5A` | `0xA5A` | `0x125A` / `0x1A5A` | — |
| 7 | `0x00007A58` | `0xA58` | `0x1258` / `0x1A58` | — |
| 8 | `0x00008A55` | `0xA55` | `0x1255` | — |
| 9 | `0x00009A5C` | `0xA5C` | `0x125C` / `0x025C` | the one-byte class-4 record of `UDP_GAME_CAPTURE.md` §3.2 |
| 10 | `0x0000AADE` | `0xADE` | `0x12DE` / `0x1ADE` | **readiness report / match-start payload** |
| 11 | `0x0000BADF` | `0xADF` | `0x12DF` | — |
| 12 | `0x0000CA5D` | `0xA5D` | `0x125D` | — |
| 13 | `0x0000DA56` | `0xA56` | `0x1256` | was **[U]** |
| 14 | `0x0000EA67` | `0xA67` | `0x1267` | the `0x267`–`0x26B` family |
| 15 | `0x0000FA68` | `0xA68` | `0x1268` | same family |
| 16 | `0x00010A66` | `0xA66` | `0x1266` | same family |
| 17 | `0x00011A65` | `0xA65` | `0x1265` | same family |
| 18 | zero | — | — | the table's end |

**This is the client's answer to "which ids do you handle": 18 of them**, and it is the
list the [U] rows of `UDP_GAME_CAPTURE.md` §2 can be checked against. `0x1100`/`0x1101`,
`0x1100`'s neighbours and `0x5250`–`0x5256` are **not** here — they are not ids this
client registers, which is why no capture of it answers them.

**The channel registry being empty is a state, not a gap.** `instance + 0x2C` and `+0x30`
are both zero in the dump, and the capture that came with it is 477 KB — the session is
short. A client at state 2 with no channel objects has not opened its in-game channel
yet, so it has nothing to register a stream against, and the entry could not have named a
handler in any case: it is a key and a use count (§3.2.1).

## 5. How many UDP handlers there are

Counted at each level the chain actually has, because "a handler" is each of them:

| level | count | addresses |
|---|---:|---|
| ids in the client's inbound registry | **18** | `0x500CE4A0` + `index × 16` (dump) |
| entries in the state table | **26** (indices `0x00`–`0x19`) | `0x0187E990` (dump) |
| code the state table reaches | **1** | `0x27E8B0` → `0x27EC48` |
| the module's stream classes | **4** (one base, three derivations); **1** instantiated in the dump — 24 objects | `0x01191AB8` (live), `0x01191C68`, `0x01191CA8`, `0x01191CE8` |
| live registry containers | **1** | `0x501030D4` (vtable `0x01191B30`), 10 of 16 entries used |
| the module's own vtable, whose slot 0 is the dispatcher | **1** | `0x011A63C8` slot 0 = `0x27E8B0` |
| P2P vtables in the image that resolve into the module | **11** | `tools/p2p_vtable_scan.py` |
| channel-registry slots the walk can reach | `0x1000` per channel × 2 channels, **0 used** here | `base + 0x180000` / `+0x190000` |

So: **18 per-id handler slots** in the ring, **26 states** behind **one** dispatch
pair, **10** registered handlers in the one registry the dump has, and **4** stream
classes whose own slot 0 (`0x263328`) is a state getter — they are the module's buffers,
not a `vid`'s handler.

## 6. What this means for the gameplay host

The host's job is to write the records the client's *registry* hands to a handler, in the
order the client's state expects, and the registry is keyed by what the module registers
rather than by `vid` alone — so a record's bytes are still the only contract. That is why
the fix for a client that does not reach the briefing is in the *records and their order*
(`docs/analysis/join-stall-after-mode2.md`) and not in a handler address.

What §3.6 adds is the **state block behind every index**: 26 states, each with a
field table of (offset, length) pairs over a state block of a size the table fills
exactly, 24 of them the session's own slots. `docs/protocol/STATE_VECTOR.md` reads the
same object from the wire (its `0x02` is a patch of it by offset and count); this file
reads the client's own table for it. The host's bodies are the *recorded* host's bytes and
stay as they are — a rewrite on this evidence would be a change against the capture, and
§3.6 measures that the state parse does not even apply to them. What the schema *does*
settle is the other direction's boundary: the fields of what a client *sends* (`0x1d`'s
7 bytes, `0x1f`'s 3, `0x22`'s 22) are **not** in it, and until a host-side capture shows
their descriptors, a server body for them stays the recorded one.
`docs/analysis/udp-handlers-and-senders.md` reads what the client's own handlers and its
senders do with a body; it does not close this one either.
