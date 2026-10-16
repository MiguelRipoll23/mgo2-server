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

### The identifier block one player's records come in

A player's records are **six `vid`s in one block**, and the parser's role table
is that block: a position stream's `vid` is the track, and every other role sits
at a fixed offset from it — `VID_DAMAGE` `-5`, `VID_STATE` `-2`, `VID_HEALTH`
`-1`, `VID_WEAPON` `+2` and `VID_AIM` `+4` (`rpdt.js`, where the health payload
is filed under track `vid - VID_HEALTH`, that is `vid + 1`):

| role | offset from the position `vid` | payload type the parser expects |
| --- | ---: | --- |
| damage | `-5` | `0x00` |
| state/action | `-2` | `0x01` |
| health | `-1` | `0x03` |
| position | `0` | `0x02` |
| weapon / incapacitation | `+2` | `0x04` / `0x06` |
| aim | `+4` | `0x05` |

**The GameplayServer UDP tick stream is the same block**, which is what makes
the crosswalk usable rather than merely suggestive: in
`mgo2-started-briefing-then-solo-game` one client writes `0x0861` (class `0x01`),
`0x0863` (class `0x02`), `0x0865` (classes `0x06` and `0x04`) and `0x0867`
(class `0x05`) — the block's state, position, weapon pair and aim slots, each
under this table's payload type — while the host writes `0x0862` (class `0x03`)
for that same player. Five more captures, and game1's `0x090b`…`0x0911` with
`0x090c`, repeat it; see
[the capture's own section](UDP_GAME_CAPTURE.md#0x090c-is-the-addressed-players-vitals-slot-v).
The block's low byte is per session; its `0x800` bit is the one a round's
identifier set flips.

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

For recognized loadout lengths 50–62, the parser maps the final 23 payload
bytes to appearance fields: head/chest/hand/waist/foot, two accessories, their
color bytes, gender, face, upper/lower items, face paint, voice/pitch, and
accessory colors. Skill pairs are aligned from the variable region before the
appearance block; the `0xff` end marker is checked, and a trailing `0x10`
suffix is removed when present. Not every color-byte ordering or raw item
attribute is claimed to be understood.

The length set is the pair count's arithmetic rather than a list of shapes:
`L = 50 + 2·pairs`, plus one when the region carries the `0x10` suffix, so the
byte at `0x16` is the constant `2` and the list end always sits 26 bytes from
the body's end. `52` — one pair — was missing from the set until 2026-10-11,
which is why a one-skill player had no shape to be written in. The parser's own
skill reader searches at most three pairs, so a four-pair body at `58` reads as
three and drops the first pair. **[D]**

The live gameplay server writes a loadout's own fields at these offsets — the
slot byte, the side, the appearance block and the pairs, over a recorded body of
the shape the player's skills have (`LoadoutFramesUtils.TryWrite`,
`UDP_SETTINGS_PAYLOAD.md`). The parser's mapping is what makes that possible and
is the check on it: a body the writer builds decodes under this grammar to the
values it was built from.

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
| `0x00` | State block; sufficiently long writes populate the 89-byte state vector. The parser reads the round result at state offset 2 (2 team 0, 3 team 2, 1 drawn, 6 no result, 255 = nothing written), the last round's result at 3, objective holders at 40–41, Capture clocks at 42–43, and a Team Sneaking countdown and lobby slot at 87–88. Nothing else in the vector is named. |
| `0x01` | Round clock: `u16` LE at payload `+1`, in 125 ms units. |
| `0x02` | State write: payload `[opcode, startOffset, byteCount, bytes...]`; writes the indicated bytes into the state vector. The Team Sneaking pair above arrives this way, as `02 57 02 05 <slot>`. |
| `0x04` | Objective feed: `[opcode, kind, lobbySlot]`. Kinds are mode-dependent; the parser emits timestamped objective-feed records. |
| `0x05` | `[opcode, lobbySlot]`; the slot is captured, but the operation's meaning is explicitly unresolved. |
| `0x09` | Kill feed: `[opcode, killerSlot, victimSlot]`. The feed is joined to health/position-derived deaths by victim slot. |
| `0x0e` | Round-over record; payload byte `+1` is emitted as a reason/result code. |
| `0x12` | Race goal update: flags at `+1` (bit 7 indicates a score, bit 0 team), goal at `+2`, spawn point at `+3`; scorer slot is correlated with the `0x04` objective feed. |
| `0x13` | Per-player signed `i16` LE rating array beginning at `+5`; output is a list of values. The parser labels this mapping probable. |

The capture's match-start run carries one more operations body, `0f 00`. It is the only
opcode in the stream this table does not name — every other one the capture carries is
above — so it stays **[U]** in `MatchRecordBodiesUtils` rather than being guessed at.

## Comparison with this server's P2P UDP parser

| GameplayServer UDP message(s) | Current C# interpretation | RPDT relationship |
|---|---|---|
| `0x0080`, `0x0880` | Two-byte body `[health, stamina]` (`PlayerVitalsRecordUtility`) | Strong semantic match to RPDT `TYPE_HEALTH` `[0x03, health, stamina]`; RPDT has its discriminator in the body and a different record container. |
| `0x090c` | Two-byte body `[0xfa, 0xfa]`, tick attribute class `3`, repeated in the steady host stream | A tick record's class byte **is** its payload type under the replay framing, so class `3` is `TYPE_HEALTH` itself — the discriminator RPDT writes inside its own health payload — and the two bytes behind it are that type's two values. The body size and class therefore agree with the confirmed vitals shape, and all captured samples are synchronized with `0x0a61`. Its actor is the peer the stream is written to: `0x090c` is that player's block's health slot, one below its position record (`0x090d`) and one above its state record (`0x090b`), which the client writes itself — see [the block table](#the-identifier-block-one-players-records-come-in). |
| `0x0081`, `0x0881`, `0x00b3`, `0x08b3`, `0x006d`, `0x00db` | Variable P2P position bodies (`PlayerPositionRecordUtility`) | Strong semantic match to RPDT `TYPE_POS` coordinates/state, but IDs, lengths, offsets, and surrounding framing differ. |
| `0x0261`, `0x0a61` | Tick bodies with class `0x00` or `0x01`; capture bodies include `fe` and `ff <members> fe fe` | Strong structural match to replay `RULES_VIDS`. A tick record's class byte **is** its payload type under the replay framing, so prepending its discriminator to the UDP body makes every captured record parse under the replay parser's rules-roster grammar: class `0x00` is `TYPE_DAMAGE`, the compact group/member list, and class `0x01` is `TYPE_STATE`, the empty-list form that is the list-end byte alone. The replay separates the same two records by vid (`RULES_VIDS = 0x261` / `0xa61`, a `0x800` bit apart) where the capture writes both under `0x0a61` and distinguishes them by class; `MatchRecordFramesUtils` follows the capture. Member bytes match roster profile offsets `0x05`/`0x07`; group meaning and membership policy remain unresolved. See [the live-capture comparison](UDP_GAME_CAPTURE.md#0x0261-and-0x0a61-carry-rules-roster-shaped-records-i). |
| `0x1001`, `0x9001` | Profile/join records parsed by `PlayerProfileRecordParseUtils` | Conceptual roster match to RPDT `TYPE_ROSTER`; do not apply RPDT offsets to the P2P profile. |
| `0x1000`, `0x5000`, `0x5001` | Handshake, keep-alive and roster head handling | No direct equivalent is decoded by `rpdt.js`. |
| `0xd001` | One-byte in-game control record, bodies `0x02`, `0x03`, `0x04` and `0x25` | **Present in the parser's model and decoded as nothing.** Its low vid is `0x001`, the stream the parser reads roster records and settings scoreboard deltas from, so its one body byte is a payload type: `0x02`, `0x03` and `0x04` are `TYPE_POS`, `TYPE_HEALTH` and `TYPE_WEAPON`. The shapes do not follow — it needs 58 bytes for the first and 23–45 for the second, and no position, health or weapon body is one byte — so the parser decodes none of them and the bodies stay as recorded. |
| `0x0002` | Fixed-length state blob handling | **Not decoded.** Its low vid is `0x002`, which the parser's record table does not dispatch, and the record is not even in the indexed half of its model. The 22-byte body is built from the capture's own measurement (eighteen zeros, a marker, a zero, a big-endian value) rather than from a mapping. |
| `0x1a54`, `0x9a54`, `0x1254`, `0x9254` | The operations record the replay writes as the round clock and the match state block, replayed by `MatchRecordFramesUtils` | **Mapped, and written by name.** The parser keeps the operations stream under low `vid = 0x254`, and both bodies the capture carries are its opcodes: a three-byte body `01 20 0d` is opcode `0x01` — the round clock, a `u16` LE at `+1` in 125 ms units, so 3 360 units = 420 s, the same value in every one of the round's clock records — and the ninety-byte body is opcode `0x00`, a state block whose vector `MatchRecordBodiesUtils` now builds field by field at the parser's own offsets: the round result at `state[2]`, the objective holders at `state[40]` and `state[41]` (`0xff`, its `HOLDER_NONE`, nobody) and `state[87..88]`, the round clock's 3 360 units. Everything outside those offsets stays the recorded vector, because the parser names nothing there. The state block it builds was checked against the capture byte for byte. |
| `0x1a50`, `0x9a50`, `0x1a5a`, `0x9a5a` | The item-box state group, replayed by `MatchRecordFramesUtils` | **Mapped, and now partly in the replay.** The parser's world-object streams are low `vid = 0x250` / `0x25a`, and its grammar is type 0 length 6 head, type 1 length 6 item, type 2 length 10 position, type 3 length 9 tail. The capture's head is code `0x74` with `n = 1` / `2` and items `0x8c` / `0x71` — the parser's "a box appears (n = box 1 / 2, item 0x8c / 0x71), each round" case — its position records read as world coordinates, and its all-zero pairs are the parser's filler. The replay writes that six-byte filler pair — the box's index and five zero bytes — under `0x1a50` and `0x1a5a`, in one beat frame in every eight from 6 frames after the match-start run; the spawn bodies and the moving position records stay the live round's own and are not replayed. |
| `0x5ade`, `0x1ade` | Match-start marker and payload replayed by `MatchRecordFramesUtils` | Low `vid = 0x2de` under the `0x1000` / `0x4000` class bits (`0x12de` and `0x52de` are the same low vid); the parser's table names no such stream, so both stay **[U]**. |

The two records above that this server now both reads *and* writes are the
scoreboard delta and the kill feed: `MatchScoreboardFramesUtils` builds and parses
`0x0b <slot+1> <21-byte mask> <values>`, `OperationsStreamHandler` reads the operations
stream's opcode `0x09`, and `ScoreboardPublishService` writes each delta behind
the change that caused it (`GAMEPLAY_SERVER.md` §3.1).

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
| Identity and processing | The registered UDP IDs distinguish the measured character records; handlers observe state keyed by the sender endpoint. Tick records below `0x1000` with no handler of their own are relayed to the other established peers unchanged, in `GameplayServerService.FrameDispatch`. | The replay parser derives player tracks from its `vid` stream roles and joins health samples to position tracks within the replay. | The record-to-player association is protocol-specific; the numeric IDs/VIDs do not map directly. |

The position layouts above are the layouts actually accepted by the current
server codec, not the RPDT layouts listed earlier. The server intentionally
returns no parsed position for the observed 44-byte UDP form because no
coordinate offset has been placed for it; its handler logs and ignores that
sample. The RPDT parser has its own supported length/version table.

The `0x0261`/`0x0a61` match is based on the replay parser's dedicated rules-roster
grammar, not on a numerical VID coincidence: all captured P2P bodies fit exactly
after adding the replay-only `0x00` payload discriminator. It still does not reveal
what group `0xff` represents or how its member set is assigned; the member bytes
are the per-session handles measured in
[`UDP_GAME_CAPTURE.md` §8.4](UDP_GAME_CAPTURE.md#84-the-roster-handle-is-a-per-session-host-side-allocation-v),
whose allocation rule the captures do not contain. Likewise, the
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
