# MGO2 — UDP command reference

Every UDP message type and record shape this project has evidence for, one heading each,
with a best guess at what it does and how much that guess can be trusted. Two sources
carry the detail:

- **`UDP_P2P_PROTOCOL.md`** — the wire format, crypto, handshake and data phase.
- **`src/Shared/Constants/UdpCommandConstants.cs`** — the constants the gameplay server
  dispatches on today.

The recorded evidence is the five survival-match replays in `tools/replays/`. Walking each
one's recorded UDP block — records framed `type u16 LE | len u8 | flags u8 | body`, with the
`<u32> <u16>` run markers skipped — yields **78 records per replay, byte-for-byte identical
across all five matches on four maps**, carrying exactly **two type values**: `0x0102` (once)
and `0x1001` (77 times). The replay is the *host's* recorded outbound stream, so client-only
types (`0x1000`, `0x5000`, `0x9001`) do not appear in it.

Confidence tags follow the protocol document: **[V]** verified in `MGO2.ELF` and/or a live
capture, **[I]** inferred, **[U]** unresolved.

---

## Frame message types

These are the values in the message header `type u16 LE | len u8 | flags2 u8 | body`, and the
bits that modify the frame header. The type word is `(id & 0xfff) | class-bits` in the
serializer (`FUN_00269860`), so `0x1000` is the *reliable class* and its low twelve bits an id.

### `0x0102` — roster-format descriptor

**Guess: a descriptor of the roster record format that follows, not of the match.** **[I]**

Seen **once per replay**, at file offset `0x14`, the first framed record in the file:
`type 0x0102, len 0x10, flags 0x39`, then a 16-byte body. Thirteen of its sixteen bytes are
constant across five matches on four maps; the two that vary are the per-match words at `0x0c`
and `0x10`, and it carries no map id and no mode. Its body reads big-endian
`0x0007` and `0x0032`, exactly the version byte and roster base of the `0x1001` records that
follow — which is why it reads as a format descriptor rather than match data. It has **no
confirmed wire counterpart**: the map the joiner plays is learned over TCP, so this looks like
replay-file bookkeeping (or a real message whose role is unknown). Not implemented; sending
16 bytes of half-guessed structure is worse than sending none.

### `0x1000` — handshake [V]

**Guess: session handshake — the first datagram of a session, one per direction.** **[V]**

The 44-byte first datagram of the p2p channel (§4). Carries `peer_id` (the sender's own
character id), the `counter_base` seed, `module_magic` (`0x4d258ab7`), `ver`, `count`, and the
sender's own public/private endpoints. Both directions use it. The receiver validates
`module_magic`; the dialer additionally gates on `peer_id` against its stored peer descriptor.
Sent pre-keyed (chain key `0x87103c2f`, not validated by the receiver). Also the base of the
reliable class used for acknowledgements — see `0x1000 | seq` below.

### `0x1000 | seq` — acknowledgement entry [V]

**Guess: a reliable-frame acknowledgement of outbound frame sequence `seq`.** **[V]**

Type `0x1000 | (ackedSeq & 0xfff)`, `len 1`, `flags2` = the send attempt (starts at 1, escalates
per re-send), body `0x00`. Cumulative: one ack covers every frame up to the acknowledged
sequence. A host acks each newly observed inbound sequence exactly once, and never acks a frame
whose only content is ack entries (acks of acks are consumed, never answered). In the live
capture the joiner's 17-byte `05 00 | 01 10 | 01 01 00` frames are its acks of host seq 1.
Implemented in `DedicatedHostService.ackInbound()`.

### `0x1001` — player / room record [V]

**Guess: the reliable room record — one player profile per record, re-sent until the room
answers.** **[V]**

The only other type the recorded stream ever carries: 77 of the 78 records in every replay.
It is the *reliable-class id 1*. The host answers a joiner's profile with the whole room
roster, one `0x1001` per player, host first. Its body has **four distinct shapes**, below.
The same tag covers the joiner's one-byte acks in some captures, which is why the handler
parses the body before answering. Implemented by `PlayerProfileHandler` /
`PlayerProfileRecordUtility`.

### `0x2000` — long-length class bit [V]

**Guess: a class bit on the type word, not a command — set when the message body exceeds a
single length byte.** **[V]**

The serializer writes `(id & 0xfff) | class-bits`; `0x2000` marks the long-length form. It
lives in the same u16 as the type, so it must be masked off before dispatch.

### `0x5000` — keep-alive [V]

**Guess: an empty heartbeat; mirrored straight back.** **[V]**

`type 0x5000, len 0, flags2 0` — a 16-byte frame carrying no body. The joiner emits these
pre-keyed after each host reply (state 6). The host's **session-keyed** keep-alive is what
flips the joiner into the data phase (state 8), because its tail digest verifies with
`K ^ 0x2b58de69`. In the data phase the host mirrors them. Implemented by
`AcknowledgeKeepAliveHandler`.

### `0x8000` — LZSS compression marker [V]

**Guess: a bit in the frame header (not the type word) marking the content region as a raw
LZSS stream.** **[V]**

Set on the frame's `hdr` u16, not a message type. When set, `[2 .. len-0xa)` is a raw LZSS
stream with **no** wire message header; the `type | len | flags2 | body` messages materialize
only after decompression. Masked out of the seed by `hdr & 0x7fff`. The joiner's profile frame
arrives marked (`hdr 0x8001`).

### `0x9001` — join request [V]

**Guess: the tag a joiner opens the exchange with — its own player record, a request rather
than a roster entry.** **[V]**

Live capture (2026-10-01): `type 0x9001, len 0x95` holding the player record; `0x1001` is the
tag the host's roster answer uses. The handler registers the same profile parser under this
type, which is what a stalled join was missing. Not present in the replay, because the replay
is the host's stream and the host never sends it.

---

## `0x1001` body shapes

Four shapes share the type. Counts are per replay; they are identical in all five.

### Roster entry — 12 per replay, bodies 77–93 bytes [V]

**Guess: one player's room entry — name and clan.** **[V]**

Body layout (§6.4), little-endian: byte `0x00 = 0x07`, character id, `0x04 = 0x32 + roster
index`, `0x05 = 6 + roster index`, a per-player value at `0x08`, team flag at `0x0a`, an
11-column per-player block, then `0x43 = 0x03` when a clan follows, a 16-byte NUL-padded
ISO-8859-1 **name**, and a **non-terminated** clan name to end of record. The host's own entry
is first and is the only one with a **negative** index (`0x31`/`0x05`), i.e. roster index `-1`;
joining players fill `0`, `1`, `2`, … The per-player columns and the `0x08` value are
unresolved (`[U]`); the builder writes zeros there and a test asserts it.

### Roster close — 1 per replay, body 7 bytes [V]

**Guess: a terminator for the roster run.** **[V]**

`type 0x1001, len 7, body 07 00 00 00 00 00 03` at `0x480`, immediately after the twelve
entries. Establishes the end of the flat roster. Not otherwise decoded.

### Spawn-state — 48 per replay, 26-byte bodies [I]

**Guess: initial spawn state for players who have not spawned yet.** **[I]**

Forty-eight 26-byte records in runs of 4/6/2/2/2/4/10/4/2/… separated by `<u32> <u16>` markers
at `0x48b` onward. Only **two** distinct bodies alternate — `85 12 fe ff ff ff ff 00 …` and
`86 12 7f 7f 7f 7f 7f 07 …` — plus one variant differing in a single byte. The `fe`/`7f` and
`ff`/`07` runs read as a default-constructed bitfield between sentinels, hence "unspawned
player state". **Not implemented** — sending guessed structure would be worse than sending
none.

### Loadout — 16 per replay, bodies 25–161 bytes [U]

**Guess: a per-thing loadout record, not per-player.** **[U]**

Sixteen records from `0xa88` to `0xe5e` whose bodies all begin `0b <id>`, with `id` running
`0, 1, 6, 7, 8, … 24, 24` and lengths 161, 50, 54, … The 161-byte one lists many items; the
25-byte ones almost nothing. The ids are **not** the twelve roster slots, which is what reads
as per-thing rather than per-player. A cross-sample constant `45 2f 57 39 67 68` ("E/W9gh")
from a live join request occurs once in the whole replay, inside the `0b 01` record, so this
run shares a fixed template with the live request body. **Not implemented.**

---

## Gameplay record classes (recorded tick stream)

After the opening block the replay switches to the recorded data-phase stream, organised in
ticks. Each record there is `{id u16 LE, len u8, class u8, data}` — the same 4-byte field
*order* as a wire message header, but `len` counts the class byte (`len = data + 1`), where the
wire header's `len` is the body length. The low byte of `id` is `0x75 + 10*slot + OFF[class]`,
so it identifies a **player slot**, not a command id.

**No message-type record above carries health or position — both live only here.** Health is
the `class 0x03` family, position the `class 0x02` family; slot 0 is `0x0076` / `0x0077`,
slot 1 `0x0080` / `0x0081`, and so on (`0x75 + 10*slot + 1` / `+ 2`). The replay is **not a raw
capture** (it has no handshakes, `module_magic` or IP/port pairs) and the protocol document
scopes the UDP record shape to the opening block only, so whether these records go on the wire
verbatim is not established. Class meanings are from `tools/mgo2_replay_parser.py`.

| class | name | data | guess | tag |
| --- | --- | --- | --- | --- |
| `0x01` | state | 3 bytes (`00 ff 00` alive), 9 bytes (aim/fire) | alive/dead state; the 9-byte form is an aim/fire update (used to infer attackers) | [V] |
| `0x02` | transform | 16/17/18/32/38/44 bytes | world position (compressed 3×i16 z/y/x at ×10, verified <1 unit), body yaw, and in the 32/38/44 forms a second camera/aim yaw | [V] |
| `0x03` | vitals | 2 bytes | `{HP, STAMINA}`, each 0..250; HP drops are hits, HP→0 is a kill; stamina depletes and regenerates | [V] |
| `0x04` | ? | 5 bytes | unknown; per-player and often all-zero at spawn, so not health or position | [U] |
| `0x05` | ? | 6 bytes | three i16 values; its first component also splits by team at spawn, so it reads as a **direction/velocity vector** rather than position | [I] |
| `0x06` | ? | 1–2 bytes | unknown | [U] |
| `0x07` | ? | — | observed in the replays but not labelled by the parser | [U] |
| `0x08` | rare event | 13 bytes | rare match event | [I] |

The **attacker is never on the wire**: the vitals record names the victim only, and no field
co-varies with an HP drop, so a candidate attacker is inferred from an `0x01 len 10` aim/fire
record in the same tick.

### How health and position were pinned from the data

Tracking each class per player slot across the tick stream and watching how its values move
identifies both without needing a field name. `replay_360827_5.dat` (map 17, 8.4 MB):

**Position is class `0x02`.** Decoding the walking-form transform (`i16` at `6/8/10`, ×10) and
taking each slot's first sample splits the twelve players into **two groups of six at two
distinct spawn areas** — the strongest available evidence that the field is world position:

```
cluster A  z≈54,520…56,520   x≈107,890…108,890   y≈+300     slots 0, 2, 3, 9, 10, 11
cluster B  z≈144,420…147,420 x≈88,870…89,870     y≈−1,700   slots 1, 4, 5, 6, 7, 8
```

The clusters are ~90,000 units apart on `z` and ~19,000 on `x`, and every slot's trajectory
stays inside its own area (per-slot `x`/`z` spans are only a few thousand units over the whole
match). Six and six is the team split.

**Health is class `0x03`.** Every slot opens at `FA FA` = `250, 250`. The first byte drops on
hits (`0` on a kill) and jumps back to `250` on a respawn — 59 drops against 20 rises in this
match — while the slots that take no damage stay pinned at `250` (the control):

```
slot  1  HP 0..250  6 drops
slot  4  HP 0..250  7 drops   stamina 219..250
slot  6  HP 0..250  8 drops
slot  8  HP 0..250 10 drops
slot 10  HP 0..250  6 drops   stamina 0..250
slot  2  HP 250..250  0 drops
slot  3  HP 250..250  0 drops
```

So the first byte is HP (drops on hits to `0`, restored on respawn) and the second is stamina
(regenerates — 180 rises against 3 falls here — and a slot that never uses it reads a flat
`250`).

**`0x05` is not position, but behaves like a direction.** Its three `i16` values also separate
by team at spawn — slots 0/2/3/9/10/11 open with a **negative** first value (`−1082…−1538`) and
slots 1/4/5/6/7/8 with a **positive** one (`+3017…+3173`) — while its spans over the match are
larger and noisier than `0x02`'s. That reads as a facing/velocity vector rather than a second
position encoding. **[I]**

### Wire verification against `MGO2.ELF`

Checked 2026-10-01 with Capstone over the image's first `PT_LOAD` (`vaddr 0x10000`, 17.5 MB).
The verdict is split, and the split matters:

**Confirmed — the UDP transport exists in the ELF.** The `module_magic` `0x4d258ab7` is present
exactly once, at file `0x121a5cc` = vaddr **`0x0122a5cc`**, the address the protocol document
cites. The outbound chain is all here and all reachable: frame builder `0x264c78`, message
serializer `0x269860`, queue append `0x2696d8`, send gate `0x263530`, queue drains
`0x269230/240/280`, and **direct `send` syscall** calls in the `0x28xxx` block (37 of them). The
receive side is `recvfrom` at `0x2622e0` / `0x2624a4`. So the engine does serialize messages into
frames and hand them to a UDP send syscall.

**Not confirmed — that these records are the payload.** Three negatives, each checked:

- The replay's record id encoding is **absent as data**: neither the base table
  `75 7f 89 93 9d a7 b1 bb…` (`0x75 + 10*slot`) nor that sequence exists anywhere in the image, in
  either endianness. The four `mulli …,10` sites are all date/byte-swap arithmetic.
- The module's outbound queue (`0x2696d8`) is appended to **only from inside the module**
  (`0x26xxxx`–`0x28xxxx`); no game-side `bl` site feeds it directly. The game must reach it through
  a module entry point, and that entry point is not identified here.
- The **only** message type ever seen on the wire in a live capture is `0x1001` — a measure of how
  thin the live evidence is, not proof of absence, since no capture has been taken of a match
  already in progress.

**What this means.** `MGO2.ELF` proves the channel can send, not what it sends. The replay is an
**RPDT save file** — a game-produced recording format — and its per-tick records are the only
place these health and position encodings are seen. Whether the server would ever receive them on
the p2p socket is therefore **not established**; the byte evidence that they ride the wire does
not exist yet. Closing it needs one live capture of a match in the data phase, or the module entry
point the game calls to enqueue outbound messages.

---

## Channel B command ids — recovered from `MGO2.ELF`

`docs/COMMANDS.md` records a second, independent stack — **Channel B**, the in-game host↔peer
link — with its own id space, and says its enumeration "lived in a `/tmp` file that is gone"
and would need re-extraction from the dispatcher it names as `0xD78CC8`. **That address is
stale:** today it disassembles as byte-copy code (`lbz`/`stb` runs), not a dispatch. The table
is at **`0xF4A248`**, a leaf function (no stack frame; the next function starts at `0xF4B970`).

**What it is.** A binary-search resolver keyed on the id in `r3`. The entry loads a default from
the TOC, pivots on `0x2121`, `0x3616` and `0x407D`, and sub-pivots; every terminal arm is
`lwz r9, -0xNNNN(r2)` followed by a branch to `0xF4A324`, which returns `r9 & 0xffffffff`. A scan
of the function body finds **312 `cmpwi …, r3, imm` sites against 312 distinct ids** — 1:1, so
the set below is the whole table, not a sample.

**Why this is Channel B and not Channel A** — checked, not assumed. The ranges match what
`COMMANDS.md` gives for B (`0x1101`–`0x1918`, `0x2101`–`0x240c`, `0x3001`–`0x3632`, `0x4004`–
`0x4080`, `0x52xx`/`0x56xx`), and against a list of Channel A's ids the **only** member of A
that appears here is `0x3004` — the one value `COMMANDS.md` documents the two spaces as sharing.
Every other id is unique to B. [V] for the extraction; [I] for the channel label.

**How this relates to the transport types above — unresolved.** These are 16-bit ids in a
`0x11xx`–`0x56xx` space. The frame type words this server implements (`0x1000`, `0x1001`,
`0x5000`, `0x9001`) are a *different* space, and the serializer splits the type word rather than
using it whole. How the two nest — whether a B id rides inside a `0x1001` body, or B is a
separate envelope — is **not established**. They must not be encoded as message types until it
is.

Individual arm targets are `-0xNNNN(r2)` TOC entries and are relocated at load (in the file they
read as noise), so this recovers the id set only, not a handler or name per id.

**Not in the replays.** Checked directly (2026-10-01): parsing all five replays' tick records
yields ids in the slot-encoded range only — `0x75 + 10*slot + OFF[class]`, plus the `+0x800`
re-send form, 205 distinct ids all below `0x0900` and 96% of the ~158k records. No Channel B id
appears as a structured record. The scattered `0x11xx`–`0x56xx` values a resyncing parser emits
are 0.4% of records, have low bytes that violate the record-id rule, and include ids from other
channels (`0x4102` is Channel A's personal-stats id), plus round junk (`0x3fff`, `0x2f00`) —
misparse, not data. A raw byte scan is no evidence either: in a 12 MB file every 2-byte value
occurs ~180 times by chance. So the replay carries the player-state stream, not this layer.

| band | count | ids |
| --- | --- | --- |
| `0x11xx` | 6 | `1100 1101 1102 1110 1111 1112` |
| `0x12xx` | 2 | `1200 1201` |
| `0x13xx` | 16 | `1300 1301 1303 1304 1305 1306 1307 1308 1309 1310 1311 1312 1313 1318 1319 1320` |
| `0x14xx` | 28 | `1400 1402 1403 1404 1405 1407 1408 1409 1410 1411 1412 1413 1414 1415 1416 1418 1419 1421 1422 1424 1425 1426 1427 1429 1430 1431 1432 1433` |
| `0x15xx` | 48 | `1500 1501 1502 1503 1504 1506 1507 1508 1509 1510 1511 1512 1513 1514 1515 1517 1518 1519 1520 1521 1532 1533 1541 1542 1543 1545 1546 1547 1548 154a 154b 1551 1552 1554 1555 1561 1562 1563 1564 1565 1566 1567 1568 1569 1570 1571 1572 1573` |
| `0x16xx` | 9 | `1600 1601 1602 1603 1604 1605 1607 1608 1609` |
| `0x17xx` | 9 | `1750 1751 1752 1753 1754 1756 1758 1759 1760` |
| `0x18xx` | 12 | `1800 1801 1802 1804 1805 1806 1807 1809 1810 1811 1812 1813` |
| `0x19xx` | 14 | `1900 1901 1902 1903 1904 1910 1911 1912 1913 1914 1915 1916 1917 1918` |
| `0x21xx` | 13 | `2100 2101 2103 2104 2105 2106 2107 2108 2109 210a 2120 2121 2122` |
| `0x22xx` | 13 | `2200 2201 2202 2203 2205 2206 2207 2211 2213 2214 2215 2216 2217` |
| `0x23xx` | 25 | `2300 2301 2302 2303 2304 2305 2328 2329 232a 232b 232d 232e 232f 2330 2332 2333 2334 2335 2336 2337 2338 2339 238d 238e 2390` |
| `0x24xx` | 11 | `2400 2401 2402 2404 2405 2406 2407 2409 240a 240b 240c` |
| `0x30xx` | 8 | `3001 3002 3004 3008 3010 3020 3040 3080` |
| `0x31xx` | 1 | `3100` |
| `0x32xx` | 1 | `3200` |
| `0x33xx` | 4 | `3301 3302 3303 3304` |
| `0x34xx` | 5 | `3400 3401 3402 3404 3405` |
| `0x35xx` | 2 | `3500 3501` |
| `0x36xx` | 29 | `3601 3611 3612 3613 3614 3615 3616 3618 3619 361a 361b 361d 361e 3621 3622 3624 3625 3626 3627 3628 3629 362a 362b 362c 362d 362e 3631 3632 3633` |
| `0x40xx` | 19 | `4004 4009 400b 400c 400d 4016 4068 406a 406f 4070 4072 4073 4074 4075 4077 4078 407d 407f 4080` |
| `0x52xx` | 18 | `5209 520a 520b 520d 520e 520f 5210 5212 5213 5214 5215 5216 5217 5218 5219 521a 521b 521c` |
| `0x55xx` | 14 | `55f1 55f2 55f3 55f4 55f5 55f6 55f7 55f8 55f9 55fa 55fc 55fd 55fe 55ff` |
| `0x56xx` | 5 | `5600 5601 5602 5603 5604` |
| **total** | **312** | |

The two callers of the resolver are both logging helpers (`0xF4BAB8` and the loop at `0xF56620`),
which is why what it returns is passed as a `printf`-style argument — so the table reads as the
per-id name/handler lookup for this channel's trace output, and its membership is the channel's
command set. Whether any single id is *sent* or *parsed* is not decided by this table.
