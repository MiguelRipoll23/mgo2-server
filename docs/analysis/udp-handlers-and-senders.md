# The operations records' handlers and the client's own senders

**What this file answers.** The UDP documents carry a set of **[U]** items that are
about a *record's body*: what a body's fields mean, which of the two directions writes
one, and where a byte a host sends comes from. Every one of them is a question about a
function in `MGO2.ELF` — the handler that parses the body, or the sender that builds it.
This file reads those functions out, and says which of the [U] items are closed and
which are not.

**What was read, and with what.** The image's own instructions, with the Capstone
readers in `tools/` and `_scratch/` — `mgo2_disasm.py` over a range, `_scratch/dasm.py`
for a range that steps over non-code, `_scratch/callgraph.py` for a function's callers,
`_scratch/offsetscan.py` for every load or store at one object offset, and
`_scratch/opcode_handlers.py` for the registration sites. The one run-time value used is
`rpcs3.dmp`, read through its **Memory64List** by `_scratch/live_handlers.py` (the
container of §3.3.1 of `udp-record-dispatch.md`). No decompiler was used and none is
needed: every claim below is an instruction, cited by address.

**Confidence.** **[V]** an instruction says it. **[I]** a reading of several
instructions that could be argued differently. **[U]** not established.

**Superseded draft.** An earlier write-up of the same questions — the §3.7 text held in
`_scratch/edit_dispatch.py`, which would have tagged the container's key byte as "an
operations opcode" — is **not applied**. Its tag-byte claim is the one that could not be
confirmed, so `udp-record-dispatch.md` §3.3.1 still leaves the tag byte **[U]** and this
file's §1–§3 replace the draft. §4 and §6 state what they leave open.

## 1. The write side: a cursor, and a send under a handle

A body is assembled in a cursor and sent under a *handle*; this is the pair every
sender in §5 uses.

| function | signature | what it does |
|---|---|---|
| `0x26CAD8` | `(cursor, buffer, capacity)` | opens it: `+8 = buffer`, `+0 = 0`, `+4 = capacity`, `+2 = 0` |
| `0x26CB98` | `(cursor, source, count)` | appends `count` bytes through the image's `memcpy` (`0xF9AC38`), then advances `+0` and `+2` |
| `0x26CC10` | `(cursor, source, count)` | the same append as a byte loop — the one the body builders call |
| `0x26CB20` | `(cursor, destination, count)` | takes up to `count` bytes *out* at `+0` — the byte reader |
| `0x26CC88` | `(cursor, destination, count)` | the same read-out as a byte loop — what the handlers in §4 call |
| `0x26CD00` | `(cursor, other)` | appends another cursor's whole content |
| `0x26B778` | `(handleSlot, cursor)` | **sends**: the handle at `*handleSlot` is resolved to its entry (`0x261438`) and the descriptor ring's entry carries the record out |
| `0x26B2B0` | `(handleSlot, a, b)` | the other half: *answers* the record the handle names instead of opening one |

The cursor's `+0` is the read/write position, so a send does not move it for a second
caller: the same cursor is handed to every peer in §5.2 and each send carries the same
bytes. **[V]**

## 2. The state access layer: `0x27E2B0`, `0x27E480`, `0x27E578`

Every handler and every sender in this file reaches the operation state through three
functions, and the three settle what the state's **field entries** and their **flags**
are for — the [U] that `udp-record-dispatch.md` §3.6 left on the flags' low byte.

**`0x27E2B0(index)` returns the state block.** **[V]**

```
0x27E2C4: lwz   r9, -0x8000(r30)   ; r9 = *(0x011A646C) = the 26-entry table
0x27E2C8: lwzx  r0, r9, r3         ; r0 = table[index]
0x27E2D0: clrldi r9, r0, 0x20      ; the descriptor
0x27E2D8: lwz   r11, 8(r9)         ; r11 = descriptor + 8  -- the state block
```
So the index a caller passes is the *state index* of §3.5, and what comes back is the
block of §3.6's `+0x08`, not the descriptor. Every offset the callers below pass is a
**byte offset into that block**: state `0x00`'s 168 bytes, a slot state's 1248, state
`0x19`'s 284.

**`0x27E480(block, offset, count, destination)` reads `count` bytes at `block + offset`.**
**[V]** It finds the descriptor whose `+8` is `block` (walking `0x1A` entries, falling
back to the last), then walks that descriptor's field table — 10 bytes an entry, `u16`
offset at `+0`, `u16` length at `+2`, `u16` multiplier at `+4` — and requires

```
field.offset <= offset < field.offset + field.length * field.multiplier
field.length == count
(offset - field.offset) % count == 0
```
then `memcpy(destination, block + offset, count)` at `0x27E564`. **[V]**

**`0x27E578(block, offset, count, source)` writes the same way** — the same walk, the
same three conditions (`0x27E620`–`0x27E664`) — and then decides whether to *mark* the
write. This is the whole of the low byte's meaning:

```
0x27E684: bl    0xFA4E20        ; is (block + offset) already equal to source?
0x27E690: bne   -> r24 = 1      ; the value changed
0x27E73C: bl    0xF9AC38        ; memcpy: the write happens either way
0x27E6A4: lhz   r9, 6(r31)      ; the field's flags
0x27E6A8: r0 = r9 & 0x0001
0x27E6B0: beq   -> 0x27E6BC     ; bit 0 clear: ask the other bit
0x27E6B4: cmpwi r24, 0
0x27E6B8: bne   -> mark         ; bit 0 set and the value changed: mark
0x27E6BC: r0 = r9 & 0x0040
0x27E6C4: beq   -> return       ; bit 6 clear: write, but do not mark
mark(0x27E6C8):
0x27E6D0: r10 = r28 << 1, & 0x6      ; field index -> the map's two-bit slot
0x27E6E0: r0 = 3 << r10
0x27E6E4: map[field_index / 4] |= r0
0x27E6F0: descriptor->+0x18 |= 1
```

So the flags' **`0x0040`** is *publish this field*, the flags' **`0x0001`** is *publish it
only when the write changes it*, and a field carrying neither is written into the block
and never marked. The mark is a **two-bit code `3`** in the state's own map at
`descriptor + 0x0C`, the same map whose reader path (`0x27EC48`, §3.6) leaves a code
**`2`**. The map is therefore a queue of pending field transfers with a direction: **`2`
= inbound (the wire into the state), `3` = outbound (the state onto the wire)** — which
is what the second half of `0x27EC48` leaves behind, and why the map is the thing the
`0x02` patch and the client's own `0x0C` record (§5.2) are built from. **[I]**

One gate sits above the mark: `0x26D540` is called at `0x27E694` and a zero return
skips the marking entirely (`0x27E6A0`). What it tests is **[U]**.

## 3. The container's ten keys, and the seven handlers they reach

`udp-record-dispatch.md` §3.3.1 reads the live container out of the dump. Resolving each
entry's handler — an OPD's code word, or an object's vtable slot 0 — with
`_scratch/live_handlers.py` gives this:

| entry | `+0` | key | `+4` | handler |
|---:|---:|---|---|---|
| 0 | 6 | `01` | object `0x54CC711C` | vtable `0x01195448` slot 0 `0x011FEC10` → **`0x77FA20`** |
| 1 | 5 | `06` | object `0x54CC6A60` | vtable `0x011953A8` slot 0 `0x011FEB38` → **`0x77E320`** |
| 2 | 4 | `07` | OPD `0x011F1928` | **`0x282D80`** |
| 3 | 2 | `08` | OPD `0x011F1310` | **`0x26DE98`** |
| 4 | 8 | `09` | object `0x54CC7144` | vtable `0x01195448` slot 0 `0x011FEC10` → **`0x77FA20`** |
| 5 | 1 | `0B` | OPD `0x011F17E0` | **`0x27E8B0`** |
| 6 | 3 | `0D` | OPD `0x011F12F0` | **`0x26DB78`** |
| 7 | 7 | `81` | object `0x54CC7130` | vtable `0x01195448` slot 0 `0x011FEC10` → **`0x77FA20`** |
| 8 | 9 | `85` | object `0x54FC6AE4` | vtable `0x01195888` slot 0 `0x011FF810` → **`0x7A43E0`** |
| 9 | 10 | `86` | object `0x54FC6AF8` | vtable `0x01195888` slot 0 `0x011FF810` → **`0x7A43E0`** |

Ten keys, seven codes: `0x77FA20` serves `01`, `09` and `81`; `0x7A43E0` serves `85`
and `86`. The `+0` word is the entry's rank in the sorted container, and the keys the
registration sites of `opcode_handlers.py` write — `0x07`, `0x08`, `0x0B`, `0x0D` — are
the four pairs the two read-outs share, entry for entry. **[V]**

## 4. What each handler does with a body

**The key *is* the record's first byte, and the container's own tick says so.**
`0x26E7D0` — the walk §3.3.1 of `udp-record-dispatch.md` reads the registry out of — opens
a 0x100-byte cursor (`0x26CAD8`), pops **one record** off the queue at `container + 0x14`
(`0x26B5C8`, `0x26E84C`), reads that record's **first byte** through the cursor reader
(`0x26E874`), copies the entry array to the stack (`0x26E8A0`, `count × 16` bytes), and then
compares the byte with each entry's own:

```
0x26E8C0: lbz   r11, 0x70(r31)   ; the record's first byte
0x26E8C4: lbz   r0, 0xc(r27)     ; this entry's key
0x26E8CC: blt   -> 0x26E91C      ; the byte is below this key: no entry owns it
0x26E910: beq   -> 0x26E9D0      ; equal: dispatch
0x26E9CC: 0x27A180(container, entry, cursor)   ; the cursor is already past the key
```

So the entry's key is the record's first byte, the walk is a sorted lookup rather than a
scan, and **a handler's own first read is the record's second byte** — which is what makes
the `0B` entry below read a byte the wire's `0b` bodies cannot supply. **[V]**

**Key `07` → `0x282D80`: a six-byte, four-command state message.** **[V]** It reads
`u16` (`0x282DB4`), `u8` (`0x282DC8`), `u16` (`0x282DDC`) and `u8` (`0x282DF0`) from the
body through the cursor reader `0x26CC88`, indexes the state table with the `u8`
(`0x282DFC`, `rlwinm r0, r0, 2, 0x16, 0x1D; lwzx`) and then switches on the last byte:

| the trailing `u8` | what it does |
|---|---|
| `0` | calls the state descriptor's **vtable slot 1** with `(descriptor, body, u16)` (`0x282E30`–`0x282E58`) |
| `1` | `container->+0x88 |= 0x100` — mark it (`0x282EA8`) |
| `2` | reads a second `u16` and calls the **vtable's slot 2** of the *found sub-object*: it walks a list at `container->+0x80` for the entry whose byte `+0` equals the `u8` state index and whose `u16 + 2` equals the first `u16`, and passes that entry's `+8` (`0x282EF4`–`0x282F28`) |
| `3` | `container->+0x88` bit `0x100` cleared (`0x282E94`–`0x282EA4`) |

**Key `08` → `0x26DE98`: a run of `(u8, u16)` pairs, terminated by a zero.** **[V]** It
reads `u8` into `r1+0x70` (`0x26DEF0`); a nonzero byte is followed by `u16` into `r1+0x72`
and `0x280A38(u8, u16)` is called with the pair (`0x26DECC`, `0x26DED4`). A **zero**
first byte ends the pair loop and the rest of the body is consumed one byte at a time
with `0x280A38` called per byte (`0x26DF3C`, `0x26DF30`); a cursor that runs out inside
a pair takes the same tail. `09 02 01` and `26 02 06` are two of these pairs, their
`u8` first. **[I]**

**Key `0B` → `0x27E8B0`: the state transfer** — a state index, `ceil(count / 8)` bytes of
presence map and the present fields, read out in `udp-record-dispatch.md` §3.5–§3.6. The
handler's own first act is the state index (`0x27E8E8`), and the walk above has already
consumed the key, so the index is the record's *second* byte. A wire `0b` body (7 bytes,
`0b 3c 00 40 00 00 00`) would make that index `0x3c` — a state the 26-entry table has no
entry for — so a wire `0b` record is **not** one of these queued records. **[I]**

**The seam: the key's identity as the wire body's first byte stays [U].** Everything above
is a read of the *container's own queue*, and nothing in it ties that queue's records to the
wire's bodies. The lengths hedge rather than settle it. The state dispatcher's floor — the
index byte and its presence bytes with no field present — is **7** bytes when the state is
`0x19`, **22** when it is a slot state and **8** when it is `0x00`, over the 44, 164 and 55
fields `tools/mgo2_record_schema.py` reads; the wire's `0b` record is **7** bytes, which is
`0x19`'s floor and no slot state's. So its length alone does not exclude the wire's `0b`
bodies from being state transfers. What excludes them is the byte that would be the index,
`0x3c` — and that byte is the index only if the first byte is the key, which is the
assumption in question; leave the leading byte in the payload and the index is instead that
byte, `0x0b`, a slot state whose floor is three times the record. Two readings, neither
closed: this queue does not carry the wire's `0b` bodies, or the key is not the wire body's
first byte.

**Key `0D` → `0x26DB78`: one byte, into state `0x00` at offset 99.** **[V]** It reads one
byte (`0x26DBA4`), then

```
0x26DBB8: bl    0x27E2B0        ; r3 = 0 -> state 0x00's block
0x26DBD0: bl    0x27E578        ; (block, 99, 1, &byte)
0x26DBE0: instance->+0xBCC |= 8
```
so the byte lands at byte 99 of the 168-byte round state and a flag bit goes with it.
Offset 99 falls in one of state `0x00`'s fields, so the write is a marked one (§2).

**Keys `01`, `09`, `81` → `0x77FA20`; `06` → `0x77E320`; `85`, `86` → `0x7A43E0`: a
fan-out.** **[V]** All three have one shape:

```
0x77FA2C: lwz   r10, 0xc(r3)      ; the object's own state word
0x77FA30: r0 = r10 & 1
0x77FA38: beq   -> 0x77FAA0       ; bit 0 clear: one child
0x77FA3C: r0 = lwz 0x10(r3)       ; an offset
0x77FA44: r3 = lwz 8(r3)          ; a base
        r9 = *(base + offset) + r10 - 1
        r10 = *(r9)               ; the child (the list's tail)
0x77FA74: r0 = lwz 0(r10)         ; its vtable
0x77FA84: bctrl                   ; child->slot0(base + offset, r4, r5)
```
They call a child object with the record's cursor and length and do nothing else, so
keys `01`, `06`, `09`, `81`, `85` and `86` are *routed* to the object that owns them
(one per session, by the list) rather than parsed here. Which body each child then
reads is not in this path. **[U]**

## 5. The client's own senders

### 5.1 The senders are the callers of `0x26B778`

`_scratch/callgraph.py callers 0x26b778` lists 59 sites, and `callers 0x26b2b0` 55. The
two that build an operations body are the per-phase tick and the run opener below.

### 5.2 The per-phase tick `0x2729D8`, and its twin `0x273370`: three bodies

**`0x01`, one byte, to every live handle.** **[V]** `0x273600` opens a 0x100-byte cursor,
appends the single byte `1` (`0x27361C`–`0x273630`), and then sends the *same cursor*
under the instance's own handle and under all 24 slot handles:

```
0x273640: lwz   r0, 0xbb8(instance)     ; the instance's own handle
0x273648: beq   -> skip                  ; -1: it holds none
0x273660: bl    0x26B2B0                 ; answer what that handle names
0x273674: bl    0x26B778                 ; send the 01 body
0x273090: r31 = instance + 0xD4          ; then the 24 slots, 0x74 bytes each
0x273098: every slot whose  u16 +2 != 0  and  lwz +0x10 != -1
0x2730C8: bl    0x26B2B0 / 0x2730D8: bl 0x26B778
```
So the client's own clock record is **one byte, `0x01`, with no value**, and it is sent
to every peer. **[V]** (A capture of a *server* shows `01 80 00`; a client's is one
byte.)

**`0x0e <byte>`, the round-over.** **[V]** At `0x273270` the same tick tests
`instance + 0xC06`; when it is nonzero it appends `0x0e` and then the byte itself
(`0x273298`), clears the byte (`0x2732B0`), resets `instance + 0xBBC` and sends under
the instance's handle (`0x2732D4`) and then under every live slot handle
(`0x2732EC`–`0x273368`). **So the byte the wire's `0e` record carries is
`instance + 0xC06`.** **[V]**

**`0x0c <4 bytes>`, one record per marked word.** **[V]** The same two writers
(`0x2734CC` and `0x273500`) append `0x0c` and then four bytes taken from
`0x2776E8(out, 1)` — a two-slot mailbox whose `which = 1` copies the word at `+0` of the
object `*(0x011A6434)` and clears the bit 0 that marks it dirty (`which = 0` reads `+8`
and bit 1). One record per mark, then the same fan-out over the 24 slots.

**A lone `0x00`** is appended when `instance + 0xC06` is zero (`0x273534`, `0x2734B0`),
so the same cursor goes out with `00` as its body in that case. **[V]**

### 5.3 Where the `0x0e` byte comes from

`_scratch/offsetscan.py 0xc06 --store` gives nine sites, three of them the writers above.
The two setters are a pair of leaf functions that keep a **round-over counter**:

```
0x26E308(the argument):
  0x26E320: w = instance + 0xBCC
  0x26E324: if (w & 0x400000) return          ; bit 9: one is already pending
  0x26E32C: if (w & 1) return                 ; bit 0: this peer does not hold the verdict
  0x26E340: w |= 0x400000                     ; mark one pending
0x26E350(the argument == 0):
  0x26E35C: if ((w & 0x400000) == 0) return
  0x26E370: 0xC06 = 0  and  bit 9 cleared     ; the pending one is answered as zero
0x26D7C0 (the tick's own):
  0x26D7D0: w = instance + 0xBCC
  0x26D7DC: if (bit 9 set) and (bit 10 clear)
  0x26D7F8: 0xC06 = 0xC06 + 1   and   bit 10 set
```
So `0xC06` is **the count of round-overs recorded while a verdict was pending**, not a
rule code, and bit 9 of `instance + 0xBCC` is "a round-over is pending". **[V]**

### 5.4 The run opener `0x270E00`: one body of up to 0x174 bytes

`0x270E00` receives `(session, object, handleParameter, record)`, memsets the
instance's `+0xD4` slot array to zero (`0x270EC8`, `0xAE0` bytes = 24 × `0x74`), sets
`+0xC18 = 0x12C`, `+0xBCC = handleParameter`, `+0xBB4 = object` and clears `+0xC06`,
`+0xC08`, `+0xC14`, `+0xD0` (`0x270EF0`–`0x270F14`), registers the phase's handlers
(`0x2711B0`, `0x2711D0`, `0x2711F0` — keys `0x0B`, `0x08`, `0x0D`), and — when
`handleParameter` is odd — builds one body and sends it under its own handle
(`0x2714FC`). The body is assembled by append calls in this order: **[V]**

| bytes | source |
|---:|---|
| 1 | byte 0 **of the object** `param_2` — the same object the function stores at `instance + 0xBB4` |
| 2 | the record's `+0` |
| 2 | `+2` |
| 1 | `+4` |
| 1 | `+5` |
| 1 | `+6` |
| 4 | `+8` |
| 4 | `+0xC` |
| `0x17` | `+0x10`, through the memcpy append |
| 1 | `+0x27`, then `+0x29`, `+0x2B`, `+0x2D`, `+0x2F` in turn; each of `+0x28`, `+0x2A`, `+0x2C`, `+0x2E`, `+0x30` follows its lower neighbour **only when that byte was nonzero** |
| 1 | a presence bitmap: `0x7F` `u16`s at `+0x34`, one bit each |
| `2` each | the `u16`s the bitmap marks, in order |
| `strlen(+0x132) + 1` | the first string, with its NUL |
| `strlen(+0x14A)` | the second, **without** one (it is the body's last byte) |

Before the body it writes two of state `0x00`'s fields through the setter of §2 — byte
offset `0xA2` from a `u16` the module's statics hold and offset `0xA1` from
`instance + 0xBCB` (`0x270F94`, `0x270FE0`) — and reads state `0x19`'s byte offset
`0xB6` into the stack (`0x271000`–`0x271024`), which `0x261620(3, that u16)` then takes
(`0x271034`). So the opener sends a *profile-shaped* body: a presence map and two
strings over a record struct, sent under the handle the instance holds at `+0xBB8`.
**[I]**

### 5.5 The opcode-byte writers elsewhere in the image

The module's own tick is not the only writer of an operations body. Every body's first
byte is written the same way — `li rX, op`, `stb rX, d(r1)`, then an append (`0x26CC10` or
`0x26CB98`) — so `_scratch/bodywriters.py` scans the whole code segment for that pair and
prints each site. It finds **96** sites; the ones whose constant is an operation the UDP
documents name are these:

| opcode | site | the body it writes | against the capture |
|---|---|---|---|
| `0x1f` | `0x7551A4` | `1f` + `0x26D588()` + the writer's third argument (`0x7551A0`–`0x7551E4`) | the capture's `1f 05 01` — `0x26D588()` is `byte(instance + 0xC8)`, the client's **own slot** |
| `0x22` | `0x754308` | `22` + `0x26D588()` + the fields that follow (`0x7554330` onwards) | the capture's `22 05 …` |
| `0x0d` | `0x75502C` | `0d` + `0x748E00(slot)` (`0x755038`, `0x75504C`) | — |
| `0x0c` | `0x7550B4` | `0c` + one `0x748E00(slot)` byte for each of slots `0`–`0x17` — the same writer's other arm, taken when the slot byte is `0x18` | — |
| `0x20` | `0x75513C` | `20` + `0x748E00(slot)` — the arm taken when `0x26D540()` is zero | — |
| `0x13` | `0xB2D6A8` | `13` + `0x26D588()`, then the rating array | `STATE_VECTOR.md`'s "per-player rating" |
| `0x05` | `0xB2D6CC` | built inline as the two bytes `05` + `0x26D588()` and handed to `0xB6D670` — **the `0x05` row of `STATE_VECTOR.md` §1 is this: the writer's own slot** | — |
| `0x26` | `0xB2A5C4` | the two bytes `26` + `0xB2F920()`, which is `byte(obj + 0x188)` of the module's own object (`0xB2F920`); the writer sends right after them (`0xB2A600`) | the capture's is three bytes (`26 02 06`), so a second `0x26` writer exists or the recorded one is this writer's other arm |
| `0x0b` | `0x79F594` | `0b` + the slot byte (`0x79F544`, `0x79F5A0`) | not the 7-byte countdown |
| `0x0b` | `0x811D40` | `0b` + `u32 obj + 0x64` + `u16 obj + 0x70` + `u32 obj + 0xD4` + `u32 obj + 0xD8` (`0x811D4C`–`0x811DE4`), sent to the handle at `obj + 0x36C` | not the 7-byte countdown |

Two of the residue's three items are answered by this table, and the third is bounded:
`0x1f` and `0x22` **are** written literally, by the match code's own record builders, and
`0x26D588()` — `byte(instance + 0xC8)` — is the slot byte they lead with. `0x1d` has **no
site at all**: over the whole code segment, `li rX, 0x1d` followed by a `stb` of that
register is written nowhere, where `0x1f` and `0x22` are. The capture's `1d` bodies are 7
bytes and its `1f`/`0x22` bodies share their second byte with it, so the record exists — it
is its *first byte* that no instruction in this image stores as a constant. **[V]** for the
table, **[U]** for `0x1d`'s writer.

## 6. What this closes, and what it does not

| item in the UDP documents | state now |
|---|---|
| the flags' low byte in the state schema, `[U]` in `udp-record-dispatch.md` §3.6 | **closed**: `0x0040` = publish this field, `0x0001` = publish when the write changes it; neither, and the write is not marked (§2) |
| what the state's map at `descriptor + 0x0C` records, `[U]` there | **read**: a two-bit code per field, `2` inbound and `3` outbound (§2) |
| the `0x0e` round-over record's reason byte, `[U]` in `GAMEPLAY_SERVER.md` §7 | **closed**: it is `instance + 0xC06`, a counter of round-overs recorded while a verdict was pending (§5.3) |
| what a client itself sends as `0x01` / `0x0c` / `0x0e` | **read**: `01` alone, `0c` plus a marked word, `0e` plus the counter (§5.2) |
| `0x0b` in `GAMEPLAY_SERVER.md` §7 — "what a recurring per-entity countdown is" | **still [U]** for the countdown's unit, but narrowed: no sender in the module's per-phase tick writes `0x0b` (§5.2), and the two the scan finds in the match code (§5.5) write a *slot* and a five-field status body, neither of them the server's 7-byte countdown — so no sender anywhere in this image writes the record the host sends |
| the key byte, and whether it is the record's first byte | **the mechanism is a read, the identity is not**: the container's own tick pops a record, reads its first byte and looks it up against the sorted keys, handing the handler the cursor past that byte (§4). That is the *container's* queue, though, and the key's identity as the **wire** body's first byte stays **[U]**: the state dispatcher's floor is 7 bytes for state `0x19`, 22 for a slot state and 8 for `0x00` against a 7-byte wire `0b` record, and the byte that would exclude it, the index `0x3c`, is the index only under the assumption in question (§4) |
| `0x1d` / `0x1f` / `0x22` | **partly read, and open where no writer exists**: `0x1f` (`0x7551A4`) and `0x22` (`0x754308`) are written literally by the match code's own record builders, each leading with `byte(instance + 0xC8)`, the writer's own slot — which is what the capture's `1f 05 01` and `22 05 …` show. `0x1d` stays **[U]**: the literal is stored at no site in the image, where `0x1f` and `0x22` each have one (§5.5). What the bodies of all three *mean* stays open too, and none of them comes from the module's per-phase tick, whose bodies are `01`, `0c`, `0e` and a lone `00` (§5.2) |
| `0x05` in `STATE_VECTOR.md` §1 — `[slot]`, "names one player" | **read**: the two bytes `05` + `byte(instance + 0xC8)`, built inline by the `0x13` writer (`0xB2D6CC`) — the record names its *writer's* slot, not its subject's |
