# Expansion Pack Entitlements & Lobby Login Check (093F:FFFFFE6B)

## Overview

When attempting to connect to an expansion-gated lobby (`LOBBY_EXPANSION_ONLY=true`), the Metal Gear Online 2 client displays the following error dialog:

```text
You cannot login to the lobby.
In order to login, you need to purchase the Expansion Pack from the MGO Shop.(093F:FFFFFE6B)
```

(Found in game translations under key `ord:20658`).

This document details the reverse engineering of the client binary (`MGO2.ELF`), how expansion state is transmitted over TCP, what the bitmask controls in the game, and the server-side fix.

The disassembly behind sections 2 and 4 was produced with the scripts added in `tools/`:
`tools/mgo2_disasm.py` (range disassembler over the ELF's `PT_LOAD` segments) and
`tools/mgo2_xrefs.py` (branch-xref finder used to locate every reader of the session struct).

---

## 1. Client Error Trigger & Check Flow

In `MGO2.ELF`, when a player selects a lobby to join, the game executes:

```assembly
0x00994350: clrldi  r3, r25, 0x30
0x00994354: bl      0x97cea8        ; Check if selected lobby requires expansion
0x00994358: nop    
0x0099435c: cmpwi   cr7, r3, 0
0x00994360: beq     cr7, 0x994380   ; Not expansion-only -> skip check
0x00994364: bl      0x97ce60        ; Check if player owns expansion pack
0x00994368: nop    
0x0099436c: cmpwi   cr7, r3, 0
0x00994370: bne     cr7, 0x994380   ; Player owns expansion -> proceed
0x00994374: li      r3, 0x93f       ; Category code: 0x093F
0x00994378: li      r4, -0x195      ; Error code: -405 (0xFFFFFE6B)
0x0099437c: b       0x994598        ; Format with "(%04X:%08X)" and show error dialog
```

A secondary check with identical logic exists at `0x00a7d3e0`–`0x00a7d40c` for direct match/room join.

### Lobby Expansion Flag Check (`0x0097cea8`)
The function iterates over the lobby list received from the gate server (packet `0x2003`) and matches the lobby ID (`0x2e(r3)`):
```assembly
0x0097cf20: lbz     r0, 0x30(r3)           ; Read restriction byte
0x0097cf24: rldicl. r9, r0, 0x3d, 0x3f      ; Check bit 3 (0x08 -> ExpansionOnly)
0x0097cf28: beq     0x97cf34
0x0097cf2c: li      r3, 1                  ; Returns 1 if lobby is expansion-only
```

### Account Expansion Ownership Check (`0x0097ce60`)
```assembly
0x0097ce6c: bl      0x280418               ; Get global session
0x0097ce78: bl      0xf02ae8               ; Get player sub-struct (+0x15508)
0x0097ce8c: lbz     r0, 0x1e5(r3)          ; Read byte at offset 0x1e5
0x0097ce90: rldicl  r0, r0, 0x3e, 0x3f     ; (r0 >> 2) & 1 -> checks bit 2 (0x04)
0x0097ce94: extsw   r3, r0
0x0097cea4: blr
```

`0x0097ce60` is one of twenty readers of the same struct; `FUN_00f02ae8(session)` is a bare
`session + 0x10000 + 0x5508` accessor, so offset `0x1e5` above is the second byte of the trailer
that packet `0x3049` writes at `session + 0x15508 + 0x1e4` (see section 2).

If bit 2 (`0x04`) of byte `0x1e5` is not set, `0x0097ce60` returns `0`, triggering `093F:FFFFFE6B`.

---

## 2. Packet `0x3049` (`GetCharacterListResult`) Payload

The state at `0x1e5` is populated when the Account Lobby Server answers the character list request (`0x3048`) with `0x3049`.
The client parses it in the inline handler at `0x00f031ec`, inside the account dispatcher
`FUN_00f02e9c` (slot-type 1 of the connection pump at `0xf00f34`), and writes straight into
the session struct returned by `FUN_00f02ae8`, i.e. `session + 0x15508`.

### 2.1 Stream layout

| Offset | Size | Field | Client destination |
|---|---|---|---|
| `0x00` | 4 | result word | the list is parsed only when this is `0` |
| `0x04` | 1 | character slots | `struct + 0x00` |
| `0x05` | 1 | character count | `struct + 0x01` |
| `0x06` | 1 | main slot index | `struct + 0x02` |
| `0x07` | 16 | main character name | `struct + 0x20c` |
| `0x17` | 7 × 52 | character entries | `struct + 0x04`, stride `0x3c` in memory |
| `0x183` | 32 | entitlement trailer | `struct + 0x1e4` |

The entry loop is the key detail:

```assembly
0x00f032e8: li      r24, 0
0x00f032ec: add     r28, r26, r24        ; destination = struct + 4 + r24
...
0x00f03604: cmpwi   cr7, r24, 0x1a4      ; 0x1a4 = 7 * 0x3c
0x00f03608: addi    r24, r24, 0x3c       ; memory stride is 60, the stream stride is 52
0x00f0360c: bne     cr7, 0xf032ec
0x00f03610: addi    r4, r27, 0x1e4       ; trailer -> struct + 0x1e4
0x00f0361c: li      r5, 0x20             ; 32 bytes
0x00f03620: bl      0xf2e5c0             ; read bytes
```

So the grid holds **seven** entries, and the trailer of the stream starts at
`23 + 7 * 52 = 387` (`0x183`), for a total payload of `419` (`0x1a3`) bytes. The client's
session array is eight records wide (its `memset` at `0x00f03240` clears `0x1e0` bytes) and its
main-slot search walks eight records, but only seven are ever filled from the packet.

### 2.2 Entry layout

The client reads each entry with a packed 52-byte layout and unpacks it into its own 60-byte,
four-byte aligned record:

| Stream offset | Size | Client record offset | Meaning |
|---|---|---|---|
| `0x00` | 1 | `0x00` | slot index |
| `0x04` | 4 | `0x04` | character identifier |
| `0x08` | 16 | `0x08` | name |
| `0x15` | 9 | `0x19` | gender, face, upper, lower, face paint, upper/lower colour, voice, pitch |
| `0x1e` | 4 | `0x24` | reserved word, never read back |
| `0x22` | 14 | `0x28` | head, chest, hands, waist, feet, accessories 1/2 and their colours |
| `0x30` | 4 | `0x38` | deletion cooldown in seconds |

### 2.3 Trailer and the entitlement word

The trailer is copied verbatim, so its byte indices are session offsets:

- `trailer[0]` → `0x1e4`
- `trailer[1]` → `0x1e5` (**expansion pack bitmask**)
- `trailer[2]` → `0x1e6`
- `trailer[3]` → `0x1e7` (**codec voice pack unlock**)
- `trailer[4..31]` → `0x1e8..0x203` (no reader found in the image)

### Client Expansion Cascade Logic (`0x00f03674`–`0x00f036ec`)
Immediately after reading the trailer, the client processes the 32-bit big-endian word
`(trailer[0]<<24 | trailer[1]<<16 | trailer[2]<<8 | trailer[3])`:
```assembly
0x00f0369c: lis     r9, 8                  ; Mask: 0x00080000 (bit 3 of trailer[1])
0x00f036a0: or      r11, r11, r0
0x00f036a4: and     r0, r11, r9
0x00f036ac: cmpwi   r0, 0
0x00f036b0: srwi    r0, r9, 1
0x00f036b8: mr      r9, r0
0x00f036bc: beq     0xf036c4
0x00f036c0: or      r11, r11, r0           ; If higher bit is set, cascade to lower bit
0x00f036c4: bne     cr7, 0xf036a4
```

The loop runs twice, so it only cascades
- bit 3 (`0x08`, ALL expansion packs) → bit 2 (`0x04`, SCENE)
- bit 2 (`0x04`, SCENE) → bit 1 (`0x02`, MEME)

Bit 0 (`0x01`, GENE) is **not** produced by the cascade and has to be sent explicitly, which is
why the trailer byte carries `0x07` rather than `0x08`. The modified word is then written back
into `0x1e4..0x1e7`.

---

## 3. Features Controlled by the Bitmask

Setting `trailer[1]` (`0x1e5`) to `0x07` (`0b00000111`) sets:
- **Bit 0 (`0x01`)**: **GENE** expansion
- **Bit 1 (`0x02`)**: **MEME** expansion
- **Bit 2 (`0x04`)**: **SCENE** expansion

Disassembly reveals the full extent of features activated by this bitmask:

| Feature | Checked At | Required Mask | Unlocked Content |
|---|---|---|---|
| **Lobby Login** | `0x00994364`, `0x00a7d3f0` | `0x04` (Bit 2) | Access to expansion-only lobbies |
| **Game Rules / Modes** | `0x009b0458`, `0x009b0480` | `0x01`, `0x06` | • GENE (`0x01`): Team Sneaking (`TSNE`), Capture (`CAP`)<br>• MEME/SCENE (`0x06`): Base (`BASE`), Bomb (`BOMB`), Race (`RACE`) |
| **Expansion Maps** | `0x009b6088`–`0x009b60b0` | `0x01`, `0x02`, `0x04` | • GENE: *Coppertown Conflict*, *Tomb of Tubes*, *Virtuous Vista*<br>• MEME: *Silhouetted Stream*, *Forest Firefight*, *Midtown Maelstrom*<br>• SCENE: *Hazard House*, *Outer Haven*, *Ravaged Riverfront* |
| **Special Characters** | `0x00ba8ce0`, `0x00ba98e8`, `0x00baa888` | `0x04` (Bit 2) | IDs `443`–`445`: Meryl Silverburgh, Johnny (Akiba), Mei Ling, Liquid Ocelot |
| **Expansion Gear & Camo** | `0x00ba8cb0` | `0x04` (Bit 2) | Expansion-exclusive clothing items, headgear, and camo patterns |
| **UI Emblems / Badges** | `0x009855cc`, `0x009867a8` | `0x07` | Displays GENE / MEME / SCENE badges (`name_on`, `midasiB_st`) on nameplates |
| **Codec Voice Packs** | `0x00bc6144`, `0x00bc7b00` | `trailer[3]` (`0x1e7`) bits `0x01`, `0x02` | Additional radio voice presets and lines |

---

## 4. Root Cause and Fix

### Bug
The client reads its entitlement word from stream offset `387` (`0x183`), but the server used to
pad the grid to **eight** slots, which pushed its trailer to offset `439` (`0x1b7`):

```csharp
// Previous code:
private const int ListSlots = 8;                                        // -> trailer at 23 + 8 * 52 = 439
private const int ListPayloadSize = 0x1d7;                              // 471 bytes
```

The client therefore read its 32-byte trailer out of the eighth slot region, which is padding:

1. `trailer[1]` (`0x1e5`) arrived as `0x00`, so the game client saw **0 expansion packs**, and
   every gate listed in section 3 stayed closed.
2. `trailer[3]` (`0x1e7`) arrived as `0x00`, keeping the codec packs locked.

### Fix
The layout now lives in `src/AccountLobbyServer/Commands/CharacterListPayloadBuilder.cs`, which
describes the grid the client actually walks and writes the entitlements at their real trailer
indices:

```csharp
public const int SlotCount = 7;                                       // client loop bound: 0x1a4 / 0x3c
public const int TrailerOffset = HeaderSize + (SlotCount * EntrySize); // 0x183
public const int PayloadSize = TrailerOffset + TrailerSize;            // 0x1a3

private static void WriteTrailer(PacketWriter writer)
{
    writer.WritePadding(TrailerOffset - writer.Size);
    writer.WriteUInt8(0);
    writer.WriteUInt8(ExpansionEntitlements); // index 1: 0x07 (GENE + MEME + SCENE)
    writer.WriteUInt8(0);
    writer.WriteUInt8(CodecEntitlements);     // index 3: 0x03 (codec pack)
    writer.WritePadding(TrailerSize - 4);
}
```

`GetCharacterListHandler` keeps to the session checks, the main-first ordering (shared with the
select and delete handlers) and the send, and it only loads the appearance of the characters that
fit the grid.

The header byte at offset `0x06` is the slot index the client searches the entries for
(`FUN_00a3a148` resolves it as `struct + 4 + index * 0x3c`, and the handler at `0x00f03658`
scans the eight records for a matching slot byte). Because the account's main character is
served first and the server writes each entry's slot byte as its position in the list, the main
slot index is always `0`.
