# Expansion Pack Entitlements & Lobby Login Check (093F:FFFFFE6B)

## Overview

When attempting to connect to an expansion-gated lobby (`LOBBY_EXPANSION_ONLY=true`), the Metal Gear Online 2 client displays the following error dialog:

```text
You cannot login to the lobby.
In order to login, you need to purchase the Expansion Pack from the MGO Shop.(093F:FFFFFE6B)
```

(Found in game translations under key `ord:20658`).

This document details the reverse engineering of the client binary (`MGO2.ELF`), how expansion state is transmitted over TCP, what the bitmask controls in the game, and the server-side fix.

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

If bit 2 (`0x04`) of byte `0x1e5` is not set, `0x0097ce60` returns `0`, triggering `093F:FFFFFE6B`.

---

## 2. Packet `0x3049` (`GetCharacterListResult`) Trailer Mapping

The state at `0x1e5` is populated when the Account Lobby Server answers the character list request (`0x3048`) with `0x3049`.

In `MGO2.ELF` (`0x00f031ec`–`0x00f03700`):
1. The packet parser reads the 23-byte header and 8 character slots (52 bytes each, totalling 416 bytes).
2. At offset 439 (`0x1b7`), the parser reads a 32-byte trailer into `session + 0x1e4`:

```assembly
0x00f03610: addi    r4, r27, 0x1e4         ; Destination address
0x00f03614: mr      r3, r31                ; Packet stream
0x00f0361c: li      r5, 0x20               ; Length: 32 bytes (TrailerSize)
0x00f03620: bl      0xf2e5c0               ; Read bytes
```

This maps the trailer bytes directly into client session memory:
- `trailer[0]` $\rightarrow$ `0x1e4`
- `trailer[1]` $\rightarrow$ `0x1e5` (**Expansion pack bitmask**)
- `trailer[2]` $\rightarrow$ `0x1e6`
- `trailer[3]` $\rightarrow$ `0x1e7` (**Codec voice pack unlock**)

### Client Expansion Cascade Logic (`0x00f03674`–`0x00f036ec`)
Immediately after reading the trailer, the client processes the 32-bit big-endian word `(trailer[0]<<24 | trailer[1]<<16 | trailer[2]<<8 | trailer[3])`:
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

This loop performs backward cascade:
- If bit 3 (`0x08`, ALL expansion pack) is set $\rightarrow$ sets bit 2 (`0x04`).
- If bit 2 (`0x04`, SCENE) is set $\rightarrow$ sets bit 1 (`0x02`).
- If bit 1 (`0x02`, MEME) is set $\rightarrow$ sets bit 0 (`0x01`).

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
In `src/AccountLobbyServer/Commands/GetCharacterListHandler.cs`:
```csharp
// Previous code:
writer.WritePadding(ListTrailerOffset - writer.Size);
writer.WriteUInt8(EntitlementsIndex1Default); // (0x07) -> written at trailer[0] (0x1e4)
writer.WriteUInt8(0);                         // (0x00) -> written at trailer[1] (0x1e5) !!
writer.WriteUInt8(EntitlementsIndex3Default); // (0x03) -> written at trailer[2] (0x1e6)
writer.WritePadding(TrailerSize - 3);         // (0x00) -> written at trailer[3] (0x1e7) !!
```

Because of an off-by-one index mismatch:
1. `trailer[1]` (`0x1e5`) was sent as `0x00`, so the game client saw **0 expansion packs**.
2. `trailer[3]` (`0x1e7`) was sent as `0x00`, keeping the codec pack locked.

### Fix
Align the bytes with their intended trailer indices:
```csharp
writer.WritePadding(ListTrailerOffset - writer.Size);
writer.WriteUInt8(0);
writer.WriteUInt8(EntitlementsIndex1Default); // Index 1: 0x07 (GENE + MEME + SCENE)
writer.WriteUInt8(0);
writer.WriteUInt8(EntitlementsIndex3Default); // Index 3: 0x03 (Codec pack)
writer.WritePadding(TrailerSize - 4);
```
