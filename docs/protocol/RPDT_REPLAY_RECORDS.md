# SaveMGO `.dat` Replay Records and Gameplay UDP Crosswalk

## Finding

The replay console does **not** parse captured UDP datagrams out of the `.dat`
file. The page loads an `RPDT` replay container and its `rpdt.js` decoder turns
the container's timestamped records into player tracks, roster entries, and
events. The record identifiers and framing described below belong to that
replay format; they are not the P2P message identifiers in this server's UDP
codec.

There is useful semantic overlap:

| Replay record | Matching GameplayServer concept | Match |
|---|---|---|
| `TYPE_HEALTH` (`0x03`) | UDP vitals `0x0080` / `0x0880` | Health and stamina, in that order. Replay payload adds its own leading type byte. |
| `TYPE_POS` (`0x02`) | UDP position types `0x0081`, `0x0881`, `0x00b3`, `0x08b3`, `0x006d`, `0x00db` | Both carry player position/state. Their record IDs and variable layouts are different; do not reuse offsets. |
| `TYPE_ROSTER` (`0x07`) | UDP join/profile and roster records `0x9001` / `0x1001` | Both describe roster identity, but the RPDT roster record has its own format and is not a P2P profile body. |

The decoder does not identify its RPDT records as P2P frames, and it does not
map the UDP handshake, keep-alive, reliable-record answer, frame acknowledgement,
or P2P connect-FSM exchange. Treat the overlaps above as cross-checks of
meaning, not a translation between protocols.

## Sources and scope

- Replay console supplied in the request:
  [replay console, match 69278](https://eovsstone.com/games/mgo2-replay-console-beta/?match=69278)
- Parser loaded by that page:
  [`rpdt.js?v=ins193`](https://eovsstone.com/games/mgo2-replay-console-beta/rpdt.js?v=ins193)
- Page loader:
  [`app.js?v=ins207`](https://eovsstone.com/games/mgo2-replay-console-beta/app.js?v=ins207)
- The loader's match metadata identifies `replay_373735_6.dat.zip` as the replay
  for match 69278. This document maps the decoder implementation; it does not
  claim an independent packet capture or byte-for-byte inspection of that
  replay.

Field offsets below are relative to the start of an RPDT record's **payload**
(the byte the parser calls `a`), unless identified as chunk or record-header
offsets. The leading payload type byte is included in those offsets.

## RPDT container and record framing

The parser walks this hierarchy:

1. **File header:** 12 bytes, beginning with ASCII `RPDT`. The map identifier
   is the low byte of a big-endian 32-bit value at file offset `+0x04`;
   format version is the byte at `+0x06`.
2. **Chunk:** little-endian `u32 tick`, followed by little-endian `u16 raw
   size`. The low 15 bits give payload size; the high bit indicates an
   extended header. An extended header adds a one-byte chunk kind before the
   payload.
3. **Record:** little-endian `u16 vid`, one-byte payload length `L`, then the
   payload. When `vid & 0x1000` is nonzero, a fourth framing byte precedes
   the payload; the parser uses it as a record index on applicable records.
   Otherwise the payload begins immediately after the three-byte record
   header.

The payload's first byte is generally a `TYPE_*` discriminator. `vid`, the
optional record index, chunk kind, and payload type are separate fields; none
is the UDP frame header or UDP message type. In particular, the RPDT parser's
indexed bit must not be confused with UDP message class bits or the UDP frame
compression marker.

Recognized extended chunk kinds:

| Kind | Parser use |
|---|---|
| `0x02` | Starts a replay roster/session snapshot; its records are scanned for `TYPE_ROSTER`. |
| `0x0a` | Reads settings records for rotation, player teams, appearance, and skills. |
| Other / absent | Ordinary timestamped record chunks; other kinds are counted but not given these special interpretations. |

## Player record types

`TYPE_*` values are payload discriminators in `rpdt.js`; lengths below are
payload lengths, including the discriminator byte.

| `TYPE_*` | ID | Accepted form and fields the parser maps |
|---|---:|---|
| Damage | `0x00` | Length 12 or 16. `+1` subtype; `+3` `u16` LE damage divided by 128; `+5` weapon ID; `+6,+8,+10` signed `i16` LE impact X/Y/Z; length 16 adds `u16` LE bearing at `+14`, scaled to one turn. The class is derived from the subtype high nibble (lethal, non-lethal, gas, or unknown). Attacker identity is inferred from trajectories/bearing, not directly decoded from this record. |
| State/action | `0x01` | Length 4 or 10. `+1 & 0x3f` action; bit `0x40` of `+1` is an aim-origin-side flag whose side meaning is unresolved; `+2` target lobby slot (`0xff` means none). Length 10 adds signed `i16` LE target-point X/Y/Z at `+4,+6,+8`. |
| Position | `0x02` | Version- and length-dependent. Reads signed `i16` LE X/Y/Z and `u16` LE yaw from the selected position block. The parser also carries payload `+1` as `stateFlags`, `+2` as `motionState`, and in selected v2 forms `+5` as `knockdownCount`. Longer forms can add anchor/ragdoll vectors or an unresolved trailer/partner slot. See [position layouts](#position-layout-details). |
| Health | `0x03` | Length 3. `+1` health and `+2` stamina, each on a 0–250 scale; normalized values are interpolated onto position samples. The associated player track is resolved from `vid`, not from the payload. |
| Weapon/item | `0x04` | Length 6. Track key is derived from `vid`; payload `+2` is item/weapon ID, `+5` ammo, `+3` raw item attribute, and `+1` raw item flag. Attribute and flag meanings are not named by the parser. |
| Aim | `0x05` | Length 7. Three signed `i16` LE values at `+1,+3,+5`, treated as an aim ray in a stage-local frame; it is only translated to world coordinates when the parser can fit that frame. |
| Incapacitation | `0x06` | Length 2 or 3. If bit 0 of `+1` is set, level is `(+1 >> 3) & 0x0f`. A third byte is checked against `0x02` for coverage but is not otherwise interpreted. |
| Roster player | `0x07` | Parsed in roster chunks, or in a narrow legacy late-roster case. Fields include slot at `+5`, join counter at `+4`, character ID `u32` LE at `+8`, clan ID `u16` LE at `+31` (or `+19` for the shorter layout), then NUL-terminated nickname and clan strings. Nickname starts at `+68` or `+56` according to the record layout. |
| Throwable launch | `0x08` | Length 14. `+1` item ID; signed `i16` LE launch X/Y/Z at `+2,+4,+6`; signed `i16` LE velocity X/Y/Z at `+8,+10,+12`. The parser associates the launch with the track inferred from `vid`. |
| Settings / scoreboard | `0x0b` | Several record families; interpreted by chunk kind and `vid`, detailed below. |

### Position layout details

For `TYPE_POS`, the coordinates are not at one universal offset:

| Format version | Short coordinate block | Position bytes the parser uses |
|---|---|---|
| 0 | lengths 14, 15, 16 | X/Y/Z/yaw start at payload `+4` |
| 1 | lengths 14, 15, 16 | X/Y/Z/yaw start at payload `+4` |
| 2 | lengths 17, 18, 19 | X/Y/Z/yaw start at payload `+7` |
| Long forms | lengths 33, 36, 39, 45, 67 | X/Y/Z/yaw start at `L - 10` |

Length 41 is explicitly excluded as not a position record. Only the base
lengths 14 and 17 become the main position stream; other decoded lengths become
position variants. Coordinates are scaled by 10, Y is adjusted by the body
offset, and yaw is converted from a 16-bit turn to degrees. `alive` is derived
from sampled vitals; `health01` and `stamina01` come from the separate health
track. The parser's 27 output cells are named in `POSITION_CELL`:

`x, y, z, headingDeg, alive, health01, stamina01, stateFlags, motionState,
aimX, aimY, aimZ, itemId, ammo, action, targetSlot, incapLevel, targetPointX,
targetPointY, targetPointZ, motionName, motionGroup, aimOriginSide, itemAttr,
knockdownCount, itemFlag`.

These RPDT lengths and offsets are not interchangeable with
`PlayerPositionRecordUtility`, which parses P2P message bodies.

### Settings, teams, appearance, and skills

The same `TYPE_SETTINGS` (`0x0b`) discriminator is interpreted in two
container contexts:

| Context | Conditions | Mapped fields |
|---|---|---|
| Extended settings chunk (`kind=0x0a`) | `L >= 3`, type byte `0x0b` | Slot 0 with `L >= 71`: 15 rotation triples beginning at payload `+26` (three bytes each). Slot byte `+1 >= 1`: team byte `+2`; qualifying loadout forms additionally supply appearance and skills. |
| Indexed ordinary record, low `vid=0x001` | `23 <= L <= 45`, slot marker `+1` from 1 through 23 | Scoreboard delta: slot marker minus one, 21-byte LSB-first bit mask at `+2`, then changed values. Bits 5/6/8/11 are named kills/deaths/score/headshots; bit 152 is a last-result code, bit 153 a streak. Bits 131–136 carry three skill ID/level pairs. Unnamed bits remain unnamed. |

For recognized loadout lengths 50–58, the parser maps the final 23 payload
bytes to appearance fields: head/chest/hand/waist/foot, two accessories, their
color bytes, gender, face, upper/lower items, face paint, voice/pitch, and
accessory colors. Skill pairs are aligned from the variable region before the
appearance block; the `0xff` end marker is checked, and a trailing `0x10`
suffix is removed when present. Not every color-byte ordering or raw item
attribute is claimed to be understood.

## Indexed system and object streams

These are selected by `vid` in addition to the payload type. They are replay
record streams, not separate UDP command IDs.

| `vid` / stream | Payload interpretation in the page parser |
|---|---|
| Low `vid=0x254` (operations) | Payload byte 0 is the opcode; remaining bytes are opcode-specific. See [operation opcodes](#operation-opcodes). |
| Low `vid=0x250` / `0x25a` (world objects) | Groups same-tick records: type 0 length 6 head, type 1 length 6 item, type 2 length 10 position, type 3 length 9 tail. Position uses signed `i16` LE X/Y/Z at `+1,+3,+5`; X/Z scale by 10, Y is millimetres. `0x1250` covers item boxes/dropped things; `0x125a` covers the bomb/device. |
| Low `vid=0x258`, payload type `0x28` or `0x29`, length 4 | Notice fields `[type, slot, kind, count]`; newer recordings use `0x28`, older ones `0x29`. The parser associates these with streak notices. |
| Low `vid=0x258`, payload type `0x13` or `0x16`, length 2 | Type and lobby slot; parser treats them as destabilizer-bought / destabilizer-gone announcements. |
| Low `vid=0x256` / `0x257` (targets) | Payload type 0, length 2: slot/take event. Type 1, length 9: dropped target coordinates; X/Z are signed `i16` LE scaled by 10, Y is millimetres. |
| Low `vid=0x001`, payload type `0x03`, length 2; low `vid=0x267`–`0x26b` | One-byte gauge value at payload `+1`. |
| Indexed payload types `0x85` / `0x86`, length 26 | A team/occupancy array. The indexed record's fourth framing byte is retained as its index; the page does not decode a complete set of named fields from the 26-byte array. |
| `vid=0x261` / `0xa61`, payload type `0x00` | Special rules-roster records; parsed by a separate compact roster grammar when recognized. |

### Operation opcodes

For the operation stream (`low vid=0x254`), the parser retains payload type
as the opcode and interprets only the listed shapes:

| Opcode | Payload mapping |
|---:|---|
| `0x00` | State block; sufficiently long writes populate the 89-byte state vector. The parser reads objective holders from state offsets 40–41, Capture clocks at 42–43, result at 2, and selected other mode fields. |
| `0x01` | Round clock: `u16` LE at payload `+1`, in 125 ms units. |
| `0x02` | State write: payload `[opcode, startOffset, byteCount, bytes...]`; writes the indicated bytes into the state vector. |
| `0x04` | Objective feed: `[opcode, kind, lobbySlot]`. Kinds are mode-dependent; the parser emits timestamped objective-feed records. |
| `0x05` | `[opcode, lobbySlot]`; the slot is captured, but the operation's meaning is explicitly unresolved. |
| `0x09` | Kill feed: `[opcode, killerSlot, victimSlot]`. The feed is joined to health/position-derived deaths by victim slot. |
| `0x0e` | Round-over record; payload byte `+1` is emitted as a reason/result code. |
| `0x12` | Race goal update: flags at `+1` (bit 7 indicates a score, bit 0 team), goal at `+2`, spawn point at `+3`; scorer slot is correlated with the `0x04` objective feed. |
| `0x13` | Per-player signed `i16` LE rating array beginning at `+5`; output is a list of values. The parser labels this mapping probable. |

## Comparison with this server's P2P UDP parser

| GameplayServer UDP message(s) | Current C# interpretation | RPDT relationship |
|---|---|---|
| `0x0080`, `0x0880` | Two-byte body `[health, stamina]` (`PlayerVitalsRecordUtility`) | Strong semantic match to RPDT `TYPE_HEALTH` `[0x03, health, stamina]`; RPDT has its discriminator in the body and a different record container. |
| `0x090c` | Two-byte body `[0xfa, 0xfa]`, tick attribute class `3`, repeated in the steady host stream | Same body size and class value as the confirmed vitals shape, and RPDT `TYPE_HEALTH` uses discriminator `0x03`; all captured samples are synchronized with `0x0a61`. Likely a second actor's full vitals or a heartbeat/state marker, but its meaning and actor are unresolved. |
| `0x0081`, `0x0881`, `0x00b3`, `0x08b3`, `0x006d`, `0x00db` | Variable P2P position bodies (`PlayerPositionRecordUtility`) | Strong semantic match to RPDT `TYPE_POS` coordinates/state, but IDs, lengths, offsets, and surrounding framing differ. |
| `0x0261`, `0x0a61` | Tick bodies with classes 0/1; capture bodies include `fe` and `ff <members> fe fe` | Strong structural match to replay `RULES_VIDS` payload type `0x00`: prepending its discriminator to the UDP body makes every captured record parse under the replay parser's rules-roster grammar. Member bytes match roster profile offsets `0x05`/`0x07`; group meaning and membership policy remain unresolved. See [the live-capture comparison](UDP_GAME_CAPTURE.md#0x0261-and-0x0a61-carry-rules-roster-shaped-records-i). |
| `0x1001`, `0x9001` | Profile/join records parsed by `PlayerProfileRecordParseUtils` | Conceptual roster match to RPDT `TYPE_ROSTER`; do not apply RPDT offsets to the P2P profile. |
| `0x1000`, `0x5000`, `0x5001`, `0xd001`, `0x0002` | Handshake, keep-alive, roster head/control, and state blob handling | No direct equivalent is decoded by `rpdt.js`. |
| `0x1a54`, `0x9a54`, `0x5ade`, `0x1ade` | Slot, match-start, and control records replayed by `HostStreamFramesUtils` | The RPDT parser does not name these as its `TYPE_*` records or map them to its body fields. A superficially equal numeric byte is not evidence of an ID match. |

The P2P decoder in this repository reads a two-byte message type and the
network message framing defined in `UDP_P2P_PROTOCOL.md`. `rpdt.js` reads an
RPDT `vid`, a replay record length, an optional index, then a typed payload.
Even where the health byte order agrees, the wire structures are not the same.

### Check against the registered handlers and codecs

This comparison was checked against `PlayerVitalsHandler`,
`PlayerPositionHandler`, their record utilities, and the UDP dispatch path:

| Field/behavior | GameplayServer UDP path | Replay parser | Verified relationship |
|---|---|---|---|
| Health and stamina | The `0x0080`/`0x0880` handler accepts exactly two body bytes and reads `[body[0], body[1]]` as health and stamina. Values are clamped to 0–250 when building; parsing itself preserves the received byte values. | A `TYPE_HEALTH` payload is exactly three bytes: `[0x03, health, stamina]`. It reads bytes `+1` and `+2`, joins them to a player track selected from `vid`, and normalizes each by 250 for display. | **The two value bytes and their order match.** The replay discriminator is not present in the UDP body; the message type and UDP record framing carry that role separately. |
| Position coordinates | The handler accepts body lengths 16, 17, 18, 32, and 38. It reads signed little-endian 16-bit X/Y/Z at offsets 10/8/6 for lengths 16–18, 26/24/22 for length 32, and 32/30/28 for length 38, then scales each by 10. It does not decode the facing words. | `TYPE_POS` chooses coordinate offsets from replay version and payload length, reads signed little-endian X/Y/Z plus a 16-bit yaw, scales coordinates by 10, and applies its replay-specific Y/body-origin adjustment. | **Both encode position coordinates, but layouts and coordinate interpretation are not byte-for-byte interchangeable.** RPDT has additional yaw/state fields; the server does not parse RPDT `TYPE_POS`. |
| Death/alive | For a successfully parsed UDP position body, the handler treats bit `0x02` of `body[0]` as dead. Vitals also report dead when health is zero. | The parser derives `alive` from the latest health sample (`health > 0`); it separately exposes position payload byte `+1` as `stateFlags` without claiming that it is the server's death bit. | **Both can describe life state, but they use different evidence.** The RPDT `stateFlags` field is not verified as equivalent to the UDP body's death bit. |
| Identity and processing | The registered UDP IDs distinguish the measured character records; handlers observe state keyed by the sender endpoint. Tick records are then relayed to other established peers unchanged. | The replay parser derives player tracks from its `vid` stream roles and joins health samples to position tracks within the replay. | The record-to-player association is protocol-specific; the numeric IDs/VIDs do not map directly. |

The position layouts above are the layouts actually accepted by the current
server codec, not the RPDT layouts listed earlier. The server intentionally
returns no parsed position for the observed 44-byte UDP form because no
coordinate offset has been placed for it; its handler logs and ignores that
sample. The RPDT parser has its own supported length/version table.

The `0x0261`/`0x0a61` match is based on the replay parser's dedicated rules-roster
grammar, not on a numerical VID coincidence: all captured P2P bodies fit exactly
after adding the replay-only `0x00` payload discriminator. It still does not reveal
what group `0xff` represents or how its member set is assigned. Likewise, the
`0x090c` body agrees with a known health/stamina encoding but never changes in this
capture, so it is not safe to treat it as a live player's vitals input.

## What this crosswalk is safe to use for

- Use the replay parser's health, position, and roster concepts to corroborate
  field meaning and gameplay behavior.
- Use its documented lengths and offsets only when decoding RPDT `.dat` replay
  records.
- Keep P2P transport framing, IDs, acknowledgement logic, and body offsets
  anchored to packet captures and this repository's UDP protocol documentation.
- Keep fields marked unresolved or inferred as such; the viewer's derived
  events (for example geometric attacker attribution) are not raw command
  fields.
