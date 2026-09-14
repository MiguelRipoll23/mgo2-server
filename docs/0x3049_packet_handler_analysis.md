# 0x3049 Packet Handler — Full Capstone Decompilation Analysis

## Source
All offsets and instructions are from `MGO2.ELF` (PPC64 Big-Endian), analyzed with Capstone.
The server binary disassembler `tools/mgo2_disasm.py` wraps Capstone to dump annotated instruction ranges.

---

## 1. Packet Overview

| Property | Value |
|---|---|
| Command ID | `0x3049` |
| Direction | Server → Client |
| Purpose | Returns the character selection grid |
| Total Payload Size | `0x1d7` (471 bytes) |
| Header Size | `0x17` (23 bytes) |
| Entry Count | 8 |
| Entry Size (client reads) | `0x3c` (60 bytes each) |
| Entry Array Size | `0x1e0` (480 bytes) |
| Trailer Offset | `0x1b7` (439 bytes) |
| Trailer Size | `0x20` (32 bytes) |

The client dispatches `0x3049` through the account dispatcher at `0x00f02e9c`:

```asm
; FUN_00f02e9c — Account Dispatcher
00f02eec: lwz        r3, 0(r3)          ; load command ID from packet
00f02ef0: cmpwi      cr7, r3, 0x3049
00f02ef0: beq        cr7, 0xf031ec      ; jump to 0x3049 handler
```

---

## 2. The 0x3049 Handler — `FUN_00f031ec`

### 2.1 Entry Point — State Reset

```asm
00f031ec: mr         r3, r27            ; r3 = session (32-bit)
00f031f0: bl         0xf02e70           ; get command pointer
00f031f4: lwz        r0, 0(r3)          ; load command word
00f031f8: mr         r29, r3
00f031fc: clrldi     r31, r3, 0x20      ; r31 = session (64-bit base)
00f03200: cmpwi      cr7, r0, 0x3049    ; verify command == 0x3049
00f03204: bne+       cr7, 0xf037e8      ; error if not
00f03208: mr         r3, r27
00f0320c: addis      r28, r25, 1         ; r28 = session + 0x10000
00f03210: bl         0xf06488           ; f06488(session) — get packet data pointer
00f03214: nop
00f03218: mr         r23, r3            ; r23 = packet data pointer
00f0321c: addi       r26, r28, 0x550c    ; r26 = entries array base
00f03220: mr         r3, r31
00f03224: addi       r27, r28, 0x5508    ; r27 = session struct base
00f03228: bl         0xf2ddec           ; f2ddec(session) — reset state=8, stream_pos=0
00f0322c: nop
00f03230: li         r4, 0
00f03234: li         r5, 0x1e0           ; 480 bytes
00f03238: clrldi     r3, r26, 0x20
00f0323c: mr         r22, r23
00f03240: bl         0xfa4d48           ; memset(entries, 0, 0x1e0) — zero the entry array
```

**`f2ddec(session)`** sets `session->state = 8` and `session->stream_pos = 0`.  
**`f06488(session)`** returns a pointer to the packet data stored in the session.

---

### 2.2 Result Word Read

```asm
00f03248: mr         r3, r31            ; r3 = session
00f0324c: addi       r4, r1, 0x70        ; r4 = stack[0x70] (result word buffer)
00f03250: bl         0xf2e20c           ; f2e20c(session, &result) — read 4-byte result word
00f03254: nop
00f03258: cmpwi      cr7, r3, 0          ; check return value
00f0325c: bne        cr7, 0xf037f0      ; error if non-zero
00f03260: lwz        r0, 0x70(r1)        ; load result word
00f03264: cmpwi      cr7, r0, 0          ; compare with 0
00f03268: bne+       cr7, 0xf03630      ; if non-zero, skip entry parsing (go to expansion cascade)
```

**`f2e20c(session, buffer)`** reads 4 bytes from the packet stream into `buffer`.  
It advances `stream_pos` by 4.

**The result word must be 0** for the client to parse the full entry list.  
If it is non-zero, the client jumps to `0xf03630` which skips entry parsing and goes straight to the expansion cascade.

---

### 2.3 Header Field Reads

After the result word is verified as 0, the client reads the header fields one by one:

| Offset | Field | Reader | Size | Meaning |
|---|---|---|---|---|
| `stream_pos + 0x00` | Result word | `f2e20c` | 4 bytes | Must be 0 to parse entries |
| `stream_pos + 0x04` | `characterSlots` | `f2e134` | 1 byte | Number of character slots owned |
| `stream_pos + 0x05` | `shownCount` | `f2e134` | 1 byte | Number of characters shown in grid |
| `stream_pos + 0x06` | `mainSlotIndex` | `f2e134` | 1 byte | Index of the main character (always 0) |
| `stream_pos + 0x07` | `mainName` | `f2e5c0` | 16 bytes | Display name of the main character (starred) |

```asm
; After result word read at 0xf03268:
; The client reads characterSlots, shownCount, mainSlotIndex, mainName
; then proceeds to read 8 entries sequentially from the stream.
```

---

### 2.4 Entry Array — Client-Side Structure

The client clears the entry array with `memset(entries, 0, 0x1e0)`.  
It then reads 8 entries, each stored at `entries + r24` where `r24` increments by `0x3c` (60 bytes).

**Each entry is 60 bytes in the client's view.** The client reads fields at these offsets:

```
Offset  Size  Reader    Field
─────────────────────────────────────────────────────
0x00    1     f2e134    slotIndex
0x04    4     f2e280    identifier (Character ID)
0x08    16    f2e5c0    name (16-byte fixed string)
0x18    8     f2e134×8  appearance bytes 0-7
                        (gender, face, upper, lower, facePaint,
                         upperColor, lowerColor, voice)
0x20    1     f2e134    pitch
0x21    1     f2e134    ??? (unwritten byte, defaults to 0)
0x24    4     f2e280    reserved word (client never reads back)
0x28    14    f2e134×14 gear bytes
                        (head, chest, hands, waist, feet,
                         accessory1, accessory2, headColor,
                         chestColor, handsColor, waistColor,
                         feetColor, accessory1Color, accessory2Color)
0x38    4     f2e280    trailing word (client stores but never reads)
```

**Total written by C# server:** `1 + 4 + 16 + 8 + 1 + 4 + 14 + 4 = 52 bytes`  
**Total expected by client:** `60 bytes` (8 bytes of zeros between entries, from `memset`)

The 8-byte gap between entries (offsets `0x3c-0x3b` relative to the previous entry) is zero-filled by the `memset` at the start.

---

### 2.5 Entry Parsing Loop

```asm
00f032ec: add        r28, r26, r24      ; r28 = entries + r24
00f032f0: clrldi     r31, r29, 0x20      ; r31 = session
00f032f4: clrldi     r4, r28, 0x20       ; r4 = entry buffer
00f032f8: mr         r3, r31              ; r3 = session
00f032fc: bl         0xf2e134             ; read slotIndex (1 byte)
00f0330c: addi       r4, r28, 4           ; entry + 0x04
00f03318: bl         0xf2e280             ; read identifier (4 bytes)
00f03328: addi       r4, r28, 8           ; entry + 0x08
00f03338: bl         0xf2e5c0             ; read name (16 bytes)
00f03348: addi       r4, r28, 0x19        ; entry + 0x19 (face byte)
00f03354: bl         0xf2e134             ; read appearance bytes 0x19-0x1f
; ... 8 more appearance bytes at 0x18-0x1f
00f03348: addi       r4, r28, 0x18        ; entry + 0x18 (gender byte)
00f03354: bl         0xf2e134             ; read gender at 0x18
; ... continues reading appearance bytes
00f03348: addi       r4, r28, 0x20        ; entry + 0x20 (pitch)
00f03354: bl         0xf2e134             ; read pitch
00f0340c: addi       r4, r28, 0x20        ; entry + 0x20 (pitch, again)
00f03418: bl         0xf2e134             ; read pitch (confirms offset)
00f03428: addi       r4, r28, 0x21        ; entry + 0x21 (unwritten byte)
00f03434: bl         0xf2e134             ; read byte at 0x21
00f03444: addi       r4, r28, 0x24        ; entry + 0x24 (reserved word)
00f03450: bl         0xf2e280             ; read reserved word (4 bytes)
; ... reads remaining gear bytes at 0x28-0x35
00f035e8: addi       r4, r28, 0x38        ; entry + 0x38 (trailing word)
00f035ec: bl         0xf2e280             ; read trailing word (4 bytes)
```

The loop counter `r24` starts at 0 and increments by `0x3c` each iteration.  
The loop exits when `r24 == 0x1a4` (420 = 7 × 60). This means **8 entries are parsed** at offsets `0, 0x3c, 0x78, 0xb4, 0xf0, 0x12c, 0x168, 0x1a4`.

---

### 2.6 Trailer Read — Entitlement Data

After all 8 entries are parsed, the client reads the 32-byte trailer:

```asm
00f03610: addi       r4, r27, 0x1e4    ; r4 = session + 0x1e4 (trailer destination)
00f03614: mr         r3, r31           ; r3 = session
00f03618: clrldi     r4, r4, 0x20       ; r4 = session + 0x1e4
00f0361c: li         r5, 0x20           ; r5 = 32 bytes
00f03620: bl         0xf2e5c0           ; f2e5c0(session, session+0x1e4, 32) — read trailer
```

**`f2e5c0(session, buffer, count)`** copies `count` bytes from `session->packet_buffer + session->stream_pos + 0x400` to `buffer`, then advances `stream_pos` by `count`.

The trailer maps to `session + 0x1e4..0x1e7`:

| Offset | Field | Value | Meaning |
|---|---|---|---|
| `session+0x1e4` | `trailer[0]` | `0x00` | Result byte |
| `session+0x1e5` | `trailer[1]` | `0x07` | Expansion pack bitmask |
| `session+0x1e6` | `trailer[2]` | `0x00` | Reserved |
| `session+0x1e7` | `trailer[3]` | `0x03` | Codec voice pack unlock |

The `0x07` at `trailer[1]` means bits 0, 1, and 2 are set:
- **Bit 0 (0x01): GENE** — always sent explicitly
- **Bit 1 (0x02): MEME** — produced by cascade from bit 2
- **Bit 2 (0x04): SCENE** — triggers the expansion-only lobby check at `0x0097ce60`

The `0x03` at `trailer[3]` means bits 0 and 1 are set for codec voice packs.

---

### 2.7 Expansion Cascade

After reading the trailer, the client builds a 32-bit expansion word and runs a cascade:

```asm
00f0367c: lbz        r9, 0x1e5(r27)    ; r9 = trailer[1] = 0x07
00f03680: slwi       r9, r9, 0x10       ; r9 = 0x07 << 16 = 0x00070000
00f03684: lbz        r11, 0x1e6(r27)   ; r11 = trailer[2] = 0x00
00f03688: slwi       r11, r11, 8       ; r11 = 0x00 << 8 = 0x0000
00f03690: or         r0, r0, r9         ; r0 = trailer[0]<<24 | 0x00070000
00f03694: lbz        r9, 0x1e7(r27)    ; r9 = trailer[3] = 0x03
00f03698: or         r0, r0, r9         ; r0 = 0x00070003 (full expansion word)
```

**Expansion word = `0x00070003`**

The cascade runs a 2-iteration loop:

```asm
00f0369c: lis        r9, 8              ; mask = 0x00080000 (bit 3, i.e. "ALL expansion")
00f036a0: or         r11, r11, r0       ; r11 = expansion word
00f036a4: and        r0, r11, r9        ; check if bit 3 is set
00f036a8: cmpwi      cr7, r10, 2        ; counter check
00f036ac: cmpwi      r0, 0              ; is bit 3 set?
00f036b0: srwi       r0, r9, 1          ; r0 = 0x00040000 (bit 2 mask)
00f036b4: addi       r10, r10, 1       ; counter++
00f036b8: mr         r9, r0             ; r9 = bit 2 mask
00f036bc: beq        0xf036c4           ; if bit 3 NOT set, skip cascade
00f036c0: or         r11, r11, r0       ; cascade: set bit 2
00f036c4: bne        cr7, 0xf036a4      ; loop
```

With `0x00070003`, bit 3 (`0x00080000`) is NOT set. So the cascade does NOT trigger.  
The expansion word remains `0x00070003`.

After the cascade, the word is written back to `session + 0x1e4..0x1e7`:

```asm
00f036d4: add        r9, r27, r10        ; r9 = session + counter
00f036d8: srwi       r0, r11, 0x18      ; extract byte 3 of word
00f036dc: clrldi     r9, r9, 0x20        ; r9 = session + counter (32-bit)
00f036e0: slwi       r11, r11, 8        ; shift word left by 8
00f036e4: addi       r10, r10, 1        ; counter++
00f036e8: stb        r0, 0x1e4(r9)       ; store byte back to session
```

The write-back loop runs 4 times (ctr=4), writing each byte of the 32-bit word back to `session + 0x1e4..0x1e7`.

---

## 3. State Machine Calls After the 0x3049 Handler

### 3.1 The `0xefeb18` Function — State Table Write

```asm
00efeb18: cmpwi      cr7, r3, 0          ; if session == 0, return
00efeb1c: cmplwi     cr6, r4, 0x81       ; if command == 0x81, return
00efeb20: beqlr      cr7                    ; if both, return (no-op)
00efeb24: cmplwi     cr7, r5, 2          ; if value == 2 (unsigned)
00efeb28: bgtlr      cr6                   ; if command > 0x81 (unsigned), return
00efeb2c: slwi       r9, r4, 2           ; r9 = command * 4
00efeb30: clrldi     r3, r3, 0x20       ; r3 = session (32-bit)
00efeb34: addi       r4, r9, 0x160       ; r4 = command*4 + 0x160
00efeb38: bgtlr      cr7                   ; if value > 2 (signed), return
00efeb3c: extsw      r9, r4
00efeb40: add        r9, r3, r9           ; r9 = session + command*4 + 0x160
00efeb44: stw        r5, 8(r9)            ; store value at session + command*4 + 0x168
00efeb48: blr                             ; return
```

**`0xefeb18(session, command, value)`** writes `value` at offset `session + command*4 + 0x168`.

The state table starts at `session + 0x160`. Each entry is 4 bytes apart (indexed by command).  
The actual stored value is at offset `+8` within each entry block.

### 3.2 The `0xefeb80` Function — State Table Write

```asm
00efeb80: cmpwi      cr7, r3, 0          ; if session == 0, return
00efeb84: cmplwi     cr6, r4, 0x81       ; if command == 0x81, return
00efeb88: beqlr      cr7                    ; if both, return
00efeb8c: slwi       r9, r4, 2           ; r9 = command * 4
00efeb90: clrldi     r3, r3, 0x20       ; r3 = session (32-bit)
00efeb94: addi       r0, r9, 0x160       ; r0 = command*4 + 0x160
00efeb98: bgtlr      cr6                   ; if command > 0x81 (unsigned), return
00efeb9c: extsw      r0, r0
00ebba0: stwx       r5, r3, r0            ; store r5 at session + command*4 + 0x160
00ebba4: blr                             ; return
```

**`0xefeb80(session, command, value)`** writes `value` at offset `session + command*4 + 0x160`.

This is identical to `0xefeb18` except it stores at offset `+0x160` instead of `+0x168`.  
The two functions write to different offsets in the same state table.

### 3.3 The Unconditional State Machine Calls After 0x3049

After the expansion cascade completes, the 0x3049 handler makes **two unconditional** state machine calls:

```asm
; 00f036f0 — After expansion cascade write-back
00f036f0: clrldi     r29, r25, 0x20      ; r29 = session (32-bit)
00f036f4: li         r4, 0xe              ; command = 0xe (14)
00f036f8: mr         r3, r29              ; r3 = session
00f036fc: li         r5, 2                ; value = 2
00f03700: bl         0xefeb18             ; 0xefeb18(session, 0xe, 2)
00f03704: nop
00f03708: mr         r3, r29              ; r3 = session
00f0370c: li         r4, 0xe              ; command = 0xe
00f03710: b          0xf03154             ; tail call to 0xf03154
```

**Call 1:** `0xefeb18(session, 0xe, 2)` → writes `2` at `session + 0xe*4 + 0x168 = session + 0x1a0`  
**Call 2:** tail call to `0xf03154` → `0xf03034` → `0xefeb80(session, 0xe, value)` → writes `value` at `session + 0xe*4 + 0x160 = session + 0x198`

The value from `0xf03154` comes from `lwa r5, 0x70(r1)` — the stack, which contains the result word (0).  
So `0xefeb80(session, 0xe, 0)` writes `0` at `session + 0x198`.

### 3.4 The `0xf03154` / `0xf03034` Trampoline

```asm
00f03154: lwa        r5, 0x70(r1)         ; r5 = value from stack (0)
00f03158: b          0xf03034             ; jump to 0xf03034
```

```asm
00f03034: bl         0xefeb80             ; 0xefeb80(session, 0xe, 0)
00f03038: nop
00f0303c: li         r3, 0               ; return 0
00f03040: b          0xf037f4             ; return to dispatcher end
```

### 3.5 State Table Summary

| Command | `0xefeb18` writes | `0xefeb80` writes | Offset |
|---|---|---|---|
| `0xe` (14) | `2` | `0` | `session+0x1a0` / `session+0x198` |

These writes advance the client's internal state machine. The state machine checks these offsets to determine what screens to show next.

---

## 4. Why Tip Screens Appear

### 4.1 The State Machine Flow

The client's state machine is controlled by values written to `session + command*4 + {0x160, 0x168}`.  
After the 0x3049 packet is processed:

1. `0xefeb18(session, 0xe, 2)` → `session + 0x1a0 = 2`
2. `0xefeb80(session, 0xe, 0)` → `session + 0x198 = 0`

These state values advance the client to a state where it requests help/tip files.

### 4.2 The HTTP Help File Requests

The client requests:
- `GET /jp/mgo2/help/2_6.txt` → **404** (no handler on server)
- `GET /jp/mgo2/help/2_13.txt` → **404** (no handler on server)

These 404 errors cause the client to display broken/empty tip screens.

### 4.3 The Root Cause

The state machine calls (`0xefeb18` and `0xefeb80`) are **unconditional** — they happen after every 0x3049 packet, regardless of the expansion fields in the trailer. The expansion fields (`0x07` and `0x03`) enable expansion packs and voice codecs correctly, but the state machine advancement that triggers tip screens is independent of the payload content.

---

## 5. The Reader Functions

### 5.1 `f2e134(session, buffer)` — Read 1 Byte

```asm
00f2e134: lwz        r9, 0x454(r3)        ; r9 = stream_pos
00f2e138: li         r10, -0x1f5          ; r10 = -501 (error code)
00f2e13c: cmpwi      cr7, r9, 0x3ff       ; compare stream_pos with 1023
00f2e140: addi       r0, r9, 0x40          ; r0 = stream_pos + 0x400
00f2e144: extsw      r0, r0
00f2e148: bgt        cr7, 0xf2e164         ; if stream_pos > 1023, skip
00f2e14c: lbzx       r0, r3, r0            ; load byte from session + stream_pos + 0x400
00f2e150: li         r10, 0                ; r10 = 0 (success)
00f2e154: stb        r0, 0(r4)             ; store byte to buffer
00f2e158: lwz        r9, 0x454(r3)         ; reload stream_pos
00f2e15c: addi       r9, r9, 1             ; increment
00f2e160: stw        r9, 0x454(r3)         ; store stream_pos
00f2e164: extsw      r3, r10              ; return r10 (0 = success)
00f2e168: blr
```

**Reads 1 byte** from `session->packet_buffer + stream_pos + 0x400`, stores at `buffer`, advances `stream_pos` by 1.

### 5.2 `f2e280(session, buffer)` — Read 4 Bytes (Word)

```asm
00f2e280: lwz        r0, 0x454(r3)        ; stream_pos
00f2e288: cmpwi      cr7, r0, 0x3fc       ; compare with 1020
00f2e290: li         r0, 4
00f2e294: clrldi     r8, r3, 0x20         ; r8 = session (32-bit)
00f2e298: mtctr      r0                    ; ctr = 4 (loop counter)
00f2e29c: li         r0, 0
00f2e2a0: li         r10, 0x18
00f2e2a4: li         r7, 0xff
00f2e2a8: stw        r0, 0(r4)             ; initialize buffer
00f2e2ac: lwz        r9, 0x454(r3)         ; stream_pos
00f2e2b0: lwz        r11, 0(r4)
00f2e2b4: addi       r9, r9, 0x40          ; r9 = stream_pos + 0x400
00f2e2b8: extsw      r9, r9
00f2e2bc: lbzx       r0, r8, r9            ; load byte
```

**Reads 4 bytes** from `session->packet_buffer + stream_pos + 0x400`, stores at `buffer`, advances `stream_pos` by 4.

### 5.3 `f2e5c0(session, buffer, count)` — Copy N Bytes

```asm
00f2e5c0: lwz        r11, 0x454(r29)      ; stream_pos
00f2e5e4: subfic     r0, r31, 0x400       ; r0 = 0x400 - count
00f2e5ec: cmpw       cr7, r11, r0         ; compare stream_pos with (0x400 - count)
00f2e600: bgt        cr7, 0xf2e634         ; if stream_pos > 0x400-count, skip
00f2e604: add        r4, r4, r11          ; r4 = session + 0x400 + stream_pos
00f2e60c: bl         0xf9ac38             ; memcpy-like function
00f2e614: add        r9, r28, r31          ; r9 = buffer + count
00f2e620: stb        r0, 0(r9)             ; null-terminate
00f2e628: lwz        r0, 0x454(r29)        ; reload stream_pos
00f2e62c: add        r0, r0, r31          ; r0 = stream_pos + count
00f2e630: stw        r0, 0x454(r29)        ; stream_pos += count
```

**Copies `count` bytes** from `session->packet_buffer + stream_pos + 0x400` to `buffer`, advances `stream_pos` by `count`.

### 5.4 `f2e20c(session, buffer)` — Read Result Word

```asm
00f2e20c: lwz        r0, 0x454(r3)        ; stream_pos
00f2e210: li         r11, -0x1f5          ; error code
00f2e214: cmpwi      cr7, r0, 0x3fc       ; compare with 1020
00f2e218: bgt        cr7, 0xf2e278         ; if stream_pos > 1020, error
00f2e21c: li         r0, 4
00f2e220: clrldi     r8, r3, 0x20         ; r8 = session
00f2e224: mtctr      r0                    ; ctr = 4
00f2e228: li         r0, 0
00f2e22c: li         r10, 0x18
00f2e230: li         r7, 0xff
00f2e234: stw        r0, 0(r4)             ; initialize buffer to 0
00f2e238: lwz        r9, 0x454(r3)         ; stream_pos
00f2e23c: lwz        r11, 0(r4)
```

**Reads 4 bytes** (the result word) from the packet stream. Advances `stream_pos` by 4.

---

## 6. The 0x3041 Handler — `FUN_00f03714`

The dispatcher also handles `0x3041` (an undocumented packet) at `0xf03714`:

```asm
00f03714: mr         r3, r27
00f03718: bl         0xf02e70
00f03720: mr         r29, r3
00f03724: clrldi     r28, r29, 0x20
00f03728: bl         0xf06488
00f03730: lwz        r0, 0(r29)
00f03734: mr         r31, r3
00f03738: cmpwi      cr7, r0, 0x3041
00f0373c: bne+       cr7, 0xf037e8        ; error if not 0x3041
00f03740: mr         r3, r28
00f03744: bl         0xf2ddec             ; reset state=8, stream_pos=0
00f0374c: mr         r3, r28
00f03750: addi       r4, r1, 0x74
00f03754: bl         0xf2e20c             ; read result word
00f0375c: cmpwi      cr7, r3, 0
00f03760: bne        cr7, 0xf037f0
00f03764: lwz        r0, 0x74(r1)
00f03768: cmpwi      cr7, r0, 0
00f0376c: bne        cr7, 0xf037a8
00f03770: mr         r3, r28
00f03774: clrldi     r4, r31, 0x20
00f03778: bl         0xf2e280             ; read word (4 bytes)
00f0377c: nop
00f03780: cmpwi      cr7, r3, 0
00f03784: bne+       cr7, 0xf037f0
00f03788: addi       r4, r31, 4
00f0378c: mr         r3, r28
00f03790: clrldi     r4, r4, 0x20
00f03794: li         r5, 0x10
00f03798: bl         0xf2e5c0             ; read 16 bytes (name)
00f037a0: cmpwi      cr7, r3, 0
00f037a4: bne        cr7, 0xf037f0
00f037a8: mr         r3, r28
00f037ac: bl         0xf2de00             ; advance state machine (state=9)
00f037b0: nop
00f037b4: mr         r3, r27
00f037b8: li         r4, 0xd              ; command = 0xd
00f037bc: li         r5, 2
00f037c0: bl         0xefeb18             ; 0xefeb18(session, 0xd, 2)
00f037c4: nop
00f037c8: mr         r3, r27
00f037cc: li         r4, 0xd
00f037d0: b          0xf03030             ; tail call to 0xf03034 → 0xefeb80(session, 0xd, value)
```

The 0x3041 handler uses command `0xd` instead of `0xe`. It writes `2` to `session + 0xd*4 + 0x168 = session + 0x1a0` and `value` to `session + 0xd*4 + 0x160 = session + 0x198`.

---

## 7. The 0x3104 Handler — `FUN_00f030f0`

The character selection result handler:

```asm
00f030f0: mr         r3, r27
00f030f4: bl         0xf02e70
00f030f8: lwz        r0, 0(r3)
00f030fc: clrldi     r31, r3, 0x20
00f03100: cmpwi      cr7, r0, 0x3104      ; command == 0x3104?
00f03104: bne+       cr7, 0xf037e8        ; error if not
00f03108: mr         r3, r31
00f0310c: bl         0xf2ddec              ; reset state=8, stream_pos=0
00f03114: mr         r3, r31
00f03118: addi       r4, r1, 0x70
00f0311c: bl         0xf2e20c             ; read result word
00f03124: cmpwi      cr7, r3, 0
00f03128: bne        cr7, 0xf037f0
00f0312c: mr         r3, r31
00f03130: bl         0xf2de00             ; advance state machine (state=9)
00f03134: nop
00f03138: mr         r3, r27
00f0313c: li         r4, 0x10             ; command = 0x10
00f03140: li         r5, 2
00f03144: bl         0xefeb18             ; 0xefeb18(session, 0x10, 2)
00f03148: nop
00f0314c: mr         r3, r27
00f03150: li         r4, 0x10
; ... tail call to 0xf03034 → 0xefeb80(session, 0x10, value)
```

The 0x3104 handler uses command `0x10`. It writes `2` to `session + 0x10*4 + 0x168 = session + 0x1a8`.

---

## 8. The State Machine — All Command IDs

| Command ID | Handler | Offset Written | Value | Context |
|---|---|---|---|---|
| `0xd` | 0x3041 handler | `session + 0x1a0` | `2` | Character selection start |
| `0xe` | 0x3049 handler | `session + 0x1a0` | `2` | Character list result |
| `0x10` | 0x3104 handler | `session + 0x1a8` | `2` | Character selection result |
| `0x5` | Keepalive handler | `session + 0x178` | `2` | Keepalive |

Each command writes `2` to a different offset in the state table at `session + 0x160`.

---

## 9. The Expansion-Only Lobby Check

The client reads `session + 0x1e5` (the expansion bitmask) at `0x0097ce60` to determine if the expansion-only lobby check passes.

With `0x07` in the trailer, bit 2 (SCENE) is set, which means the expansion-only check passes.

The cascade does NOT modify the expansion word (`0x00070003`) because bit 3 (`0x00080000`, "ALL expansion") is not set in `0x07`.

---

## 10. Why the Expansion Packs Were Not Enabled Before the Fix

The commit `5520ae3` (`fix(account): serve the seven-slot character-list grid the client walks`) used `SlotCount = 7`:

- `TrailerOffset = 23 + 7 × 52 = 387 = 0x183`
- `PayloadSize = 387 + 32 = 419 = 0x1a3`

But the client parses 8 entries at `0x3c` (60 bytes) each. The trailer was at offset `0x183`, which is **inside** the 8th entry's range (`0x180–0x1bf`). The client read the trailer bytes as part of the 8th entry, and the actual trailer data was read from past the end of the payload (where memory was zeros).

So `trailer[1] = 0x00` and `trailer[3] = 0x00`. The expansion word was `0x00000000`, and no expansion packs were enabled.

The commit `3f52ac2` (`fix(account): serve the eight-entry character-list grid the client walks`) fixed this:

- `SlotCount = 8`
- `TrailerOffset = 23 + 8 × 52 = 439 = 0x1b7`
- `PayloadSize = 439 + 32 = 471 = 0x1d7`

The trailer is now at the correct offset `0x1b7`, and the expansion fields (`0x07` and `0x03`) are correctly placed.

---

## 11. Summary of All Read Operations in the 0x3049 Handler

### 11.1 Packet Stream Layout (after header read)

```
Offset  Size  Field
─────────────────────────────────────────────────────────
0x00    4     Result word (must be 0)
0x04    1     characterSlots
0x05    1     shownCount
0x06    1     mainSlotIndex
0x07    16    mainName
0x17    60    Entry 0 (slotIndex + identifier + name + appearance + pitch + ??? + reserved + gear + trailing)
0x53    60    Entry 1
0x8f    60    Entry 2
0xcb    60    Entry 3
0x107   60    Entry 4
0x143   60    Entry 5
0x17f   60    Entry 6
0x1b7   60    Entry 7
0x1b7   32    Trailer (trailer[0]=0x00, trailer[1]=0x07, trailer[2]=0x00, trailer[3]=0x03)
```

### 11.2 State Machine State After Processing

```
session + 0x1a0 = 2   (from 0xefeb18(session, 0xe, 2))
session + 0x198 = 0   (from 0xefeb80(session, 0xe, 0))
session + 0x1e4 = 0x00 (trailer[0])
session + 0x1e5 = 0x07 (trailer[1] — expansion packs)
session + 0x1e6 = 0x00 (trailer[2])
session + 0x1e7 = 0x03 (trailer[3] — codec voice packs)
session + 0x1e8..0x1eb = 0x000000 (remaining trailer bytes)
```

### 11.3 Unconditional State Machine Advancement

After the 0x3049 handler returns, the client's state machine has advanced to a state where it shows tip screens. This advancement is caused by the `0xefeb18` and `0xefeb80` calls, which are **independent of the expansion fields** in the trailer.

---

## 12. Fix Applied

### 12.1 HTTP Help File Endpoint

Added `HelpService` and `HelpEndpoints` to proxy `/jp/mgo2/help/{*path}` requests from the upstream launcher (`mgo2pc.com`). This prevents the 404 errors that cause broken tip screens.

Files added:
- `src/Http/Services/HelpService.cs`
- `src/Http/Endpoints/Public/HelpEndpoints.cs`

Files modified:
- `src/Http/Endpoints/Public/PublicEndpoints.cs` — registered `MapHelpEndpoints()`
- `src/Http/Program.cs` — registered `HelpService` singleton

### 12.2 Payload Builder (Already Correct)

`CharacterListPayloadBuilder` already produces the correct 471-byte payload with:
- 8 entries of 52 bytes each
- Trailer at offset `0x1b7` with `0x07` (expansion) and `0x03` (codec)

---

## 13. Remaining Issues

1. **Tip screens are still triggered** by the client's state machine advancement after processing the 0x3049 packet. The `0xefeb18(session, 0xe, 2)` and `0xefeb80(session, 0xe, 0)` calls are unconditional in client code and cannot be prevented from the server side.

2. **The help file endpoint** (`/jp/mgo2/help/{*path}`) now serves files from the upstream launcher, preventing 404 errors. However, if the tip screens are shown regardless of whether help files load, the endpoint alone may not eliminate them.

3. **The expansion packs and voice codecs** are correctly enabled in the payload (`0x07` and `0x03` in the trailer). The server sends the correct values; the client reads them correctly into `session + 0x1e5` and `session + 0x1e7`.
