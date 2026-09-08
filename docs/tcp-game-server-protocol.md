# MGO2 — TCP Game-Server Protocol (cross-verified)

The framed protocol the game uses on its **TCP connection to the MGO2 game server**
(matchmaking/session layer, "mgonet"), cross-verified between `MGO2.ELF` and the
open-source **`GHzGangster/Nomad`** server (SaveMGO) cloned to
`Downloads/nomad_server` (Java/Netty, TCP-only).

> Confidence tags: **[V]** = verified in `MGO2.ELF`; **[C]** = matches Nomad's server
> implementation 1:1; **[I]** = inferred. This document is about the **TCP** channel only —
> the player-to-player UDP channel is a separate investigation.

---

## 1. TL;DR

| Item | Value |
|---|---|
| Transport | TCP to game server |
| Whole-frame cipher | **XOR** with 4-byte key **`0x5a7085af`** (repeating), applied to header **and** payload |
| Frame checksum | **16-byte HMAC-MD5** at offset `0x08`, key = ASCII **`"Z7/biJ46TzGF-8yx"`** |
| Payload cipher | **Blowfish** (standard, π-init) on *selected* command ids only |
| Crypted command ids | `0x3003, 0x4310, 0x4320, 0x43c0, 0x4700, 0x4990` (0x43xx = session block, e.g. check-session) **[C]** |
| Max payload | `0x3ff` bytes |
| Nomad refs | `Util.KEY_XOR`, `Util.KEY_HMAC`, `Constants.CRYPTO_PACKET` / `CRYPTO_AUTH`, `Packet.java`, `PacketDecoder.java` |

---

## 2. Wire layout (Nomad `Packet.java`, matches game codec)

Frame = header(24) + payload, the **whole thing XOR'd** (`KEY_XOR`) on the wire.

| Offset | Size | Field | Notes |
|---|---|---|---|
| `0x00` | u16 BE | command id | on the wire: `cmd ^ (KEY_XOR >> 16)` |
| `0x02` | u16 BE | payload length | on the wire: `len ^ KEY_XOR`; max `0x3ff` |
| `0x04` | u16 | sequence | increments per direction; verified against last |
| `0x06` | u16 | (unused/pad region of header) | |
| `0x08` | 16 | **HMAC-MD5 checksum** | over the frame (see §4) |
| `0x18` | … | payload | max `0x3ff`; Blowfish for crypted ids |
| `0x18+len` | pad | pad to 8-byte boundary | only when payload is Blowfish-crypted and `len % 8 != 0` |

Header/checksum offsets: `OFFSET_COMMAND=0`, `OFFSET_PAYLOAD_LENGTH=2`,
`OFFSET_SEQUENCE=4`, `OFFSET_CHECKSUM=8`, `OFFSET_PAYLOAD=0x18` **[C]**.

---

## 3. The key constant block in the game (`vaddr 0xff9040`)

Located at file offset `0xfe9040` (segment-0 mapping: vaddr = foff + `0x10000`), directly
under the `mgonet_connect_timeo` / `mgo_connect_server_by_index()` strings **[V]**:

```
0xff9040  5a 70 85 af                                     KEY_XOR = 0x5a7085af   (4 bytes, BE)
0xff9044  00 00 00 00                                     pad
0xff9048  5a 37 2f 62 69 4a 34 36 54 7a 47 46 2d 38 79 78 "Z7/biJ46TzGF-8yx"      = HMAC key (16)
0xff9058  00 00 00 00                                     pad
0xff905c  24 3f 6a 88 85 a3 08 d3 13 19 8a 2e 03 70 73 44  ┐
0xff906c  a4 09 38 22 29 9f 31 d0 08 2e fa 98 ec 4e 6c 89  │ standard Blowfish P-array
0xff907c  45 28 21 e6 38 d0 13 77 …                        │ (π hex digits, 0x243f6a88…)
                                                           ┘
```

- `0xff9040` … `0xff9048` = the two frame keys **[C]** (Nomad `KEY_XOR`/`KEY_HMAC`).
- `0xff905c+` = **canonical Blowfish initialization constants** (P-array starts
  `243f6a88 85a308d3 …`, 4168 bytes total — 18 P + 4×256 S words, big-endian u32s,
  dumped verbatim to `keys/blowfish-pi-tables.bin`). The game builds its Blowfish schedule
  from these at runtime; Nomad instead ships the **pre-scheduled** tables in
  `Constants.CRYPTO_PACKET` (starts `B0 8E 37 6F 3D 82 D6 3E …`) and `CRYPTO_AUTH`
  (starts `E0 CC 5D 24 95 29 33 77 …`) — dumped byte-for-byte to
  `keys/blowfish-packet-key.bin` / `keys/blowfish-auth-key.bin` so a client can load the final
  schedule directly without running the key schedule **[C]**.

> **Key files (see `keys/tcp-keys.md`):** `tcp-xor-key.bin` (`0x5a7085af`),
> `tcp-hmac-md5-key.bin` (`"Z7/biJ46TzGF-8yx"`), `blowfish-pi-tables.bin` (canonical π),
> `blowfish-packet-key.bin` / `blowfish-auth-key.bin` (pre-scheduled Blowfish states).

---

## 4. Crypto details

### 4.1 Frame XOR — `Util.xor(buffer, length, 0x5a7085af)`

Applied over the entire frame (24-byte header + payload) on both directions **[C]**.
Implementation xors 8 bytes at a time with `0x5a7085af5a7085af`, trailing bytes with the
4-byte key cycling. The game does the same in its codec (byte loop `buf[i] ^= key[i & 3]`,
key bytes from its module state) **[V]**.

### 4.2 HMAC-MD5 checksum

16 bytes at header offset `0x08`; `HmacMD5` keyed with `"Z7/biJ46TzGF-8yx"` (byte array
`5A 37 2F 62 69 4A 34 36 54 7A 47 46 2D 38 79 78`) **[C]**.

The game computes/compares this via `FUN_00f2ee64` (produce) and `FUN_00fa4e20` (compare)
in the receive path of the codec **[V]**.

### 4.3 Blowfish payload encryption (selected commands)

Only payloads of `{0x3003, 0x4310, 0x4320, 0x43c0, 0x4700, 0x4990}` are decrypted with the
`CRYPTO_PACKET` instance (16-round Feistel over 8-byte blocks, schedule layout: P-array at
`0x00`, four S-boxes at `0x48/0x448/0x848/0xc48`) **[C]**.

A second instance, `CRYPTO_AUTH`, is used for **auth payloads** (`Users.java:
Crypto.instanceAuth().encrypt(...)`), i.e. login/account command payloads **[C]**.

Corroborating game strings: `ptsys:invalid key type %d`, `ptsys:cbcblowfish length err`,
`ptsys:invalid keylength` (`vaddr 0xffa34f–0xffa388`), Konami's CBC-Blowfish `ptsys`
layer **[V]**.

---

## 5. Command ids

Nomad dispatches by `command` into helpers (e.g. `ChatMessage`, `Lobby`, `Game`,
`EventConnectGame` …); the hex `0xC0FFEE << 8` mask marks error responses. The exact
command-id → meaning mapping lives in Nomad's handler code — see
`src/main/java/savemgo/nomad/helper/*.java`. **The game's own dispatcher has now been
mapped in `MGO2.ELF`** (219 opcodes, see §5.1).

### 5.1 Game-side dispatcher (`FUN_00f0494c`) **[V]**

The game routes every decoded TCP frame through **`FUN_00f0494c`** (reached from the
connection pump at `0xf00f34`, slot-type 2; slot types 0/1 go to `FUN_00f01f48` /
`FUN_00f02e9c`). It reads the payload's first u32 as the opcode (via the payload
accessor `FUN_00f04920`) and runs a **binary-search switch over 219 command ids**
(`cmpwi`/`beq` chain, lowest `0x3004`, highest `0x4f18`). Each case is either a
3-instruction stub (`clrldi r3,r31; bl HANDLER; b epilogue`) that jumps to a dedicated
handler function, or an inline handler body. Unknown opcodes fall through to `0xf06188`.

Key handlers resolved to the game-flow commands (full map in `tools/mgo2_tcpdispatch.py`):

| Command | Handler | Role (cross-checked with Nomad) |
|---|---|---|
| `0x3004` | `FUN_00f06210` | session/keepalive block |
| `0x4101` | `FUN_00f08a18` | **getCharacterInfo** — first payload u32 (the character id) → `game_ctx+0x15710` (feeds peer_id, see below) |
| `0x4103/0x4105/0x4107` | `FUN_00f0b590` / `FUN_00f0b110` / `FUN_00f0a5b4` | personal stats |
| `0x4111/0x4113` | `FUN_00f073bc` / `FUN_00f072f8` | gameplay-options update / UI settings |
| `0x4120..0x4129` | `FUN_00f0a188`…`FUN_00f09384` | settings/UI/gear/skills/post-game info |
| `0x4131/0x4133` | `FUN_00f08d9c` / `FUN_00f09140` | updatePersonalInfo / + |
| `0x4140..0x4143` | `FUN_00f0c800` / `FUN_00f06af4` | gear/skill sets |
| `0x4301/0x4302/0x4303` | `FUN_00f0e0c0` / `FUN_00f117f4` / `FUN_00f0dfc4` | lobby entry |
| `0x4305` | `FUN_00f130bc` | host settings |
| `0x4311/0x4313` | `FUN_00f10ee4` / `FUN_00f1256c` | check/update host settings |
| `0x4317` | `FUN_00f12444` | createGame |
| `0x4321/0x4323` | `FUN_00f12278` / `FUN_00f0deb4` | join game |
| `0x4341` | `FUN_00f106ec` | **playerConnected** — reads targetId (character id), stores into game-side player slots (`stw` +0x8/+0xc) |
| `0x4343` | `FUN_00f105e0` | playerDisconnected (same shape as 0x4341) |
| `0x4345/0x4347` | `FUN_00f104d4` / `FUN_00f103c8` | setPlayerTeam / kickPlayer |
| `0x4381/0x4391/0x4393` | `FUN_00f0ddf0` / `FUN_00f0dd2c` / `FUN_00f0dc68` | stats (targetId first) / setGame |
| `0x4395/0x4399` | `FUN_00f0dba4` / `FUN_00f0dae0` | / updatePings (targetId+ping pairs) |
| `0x43a1/0x43a3/0x43a5/0x43a7` | `FUN_00f0da1c` / `FUN_00f0d958` / `FUN_00f0d894` / `FUN_00f0d584` | pass / round flow |
| `0x43b1/0x43c1/0x43c5/0x43c9/0x43cb` | `FUN_00f0d7d0` / `FUN_00f0d70c` / `FUN_00f0d648` / `FUN_00f0e578` / `FUN_00f0d4c0` | round/session flow (0x43cb = startRound) |
| `0x43d1…0x43f5` | `FUN_00f0844c` / `FUN_00f2cdfc`…`FUN_00f2c3dc` | rule/mode packets |
| `0x4502/0x4512` | `FUN_00f14e3c` / `FUN_00f1488c` | lobby list |
| `0x4581/0x4583` | `FUN_00f146ec` / `FUN_00f14320` | friends/blocked list |
| `0x4601/0x4602/0x4603` | `FUN_00f13b3c` / `FUN_00f13c84` / `FUN_00f13a40` | game roster packets |
| `0x4681..0x4687` | `FUN_00f06fa4`…`FUN_00f06c7c` | match history / stats |
| `0x4701` | inline at `0xf05618` | **updateConnectionInfo** — the game's own ip/port report (feeds the p2p module's address entries) |
| `0x4801..0x4881` | `FUN_00f24a68`…`FUN_00f23bd4` | clan packets |
| `0x4901..0x4a50`, `0x4b01..0x4b93`, `0x4d00`, `0x4e10..0x4e23`, `0x4f01..0x4f18` | dedicated handlers (see `tcp_dispatch.json`) | chat / friends / message blocks |

> **peer_id provenance (cross-ref to `udp-p2p-protocol.md` §4):** the character id delivered by
> `0x4101` lands at `game_ctx+0x15710`; accessor `FUN_00f02e04` reads it; the peer-struct
> builder `FUN_00aa0f48` puts it in word 0 and calls the p2p module's setter
> `FUN_002616d8` → `module_base+0x74` = the UDP handshake's `peer_id`.

---

## 6. Game-side codec map (implementation)

| vaddr | Role |
|---|---|
| `FUN_00f2e904` | Send path (encoder): byte-order header fields, XOR-encrypt whole frame (`KEY_XOR`), compute 16-byte digest (`FUN_00f2ee64`), send via socket helper `FUN_00f2e878` **[V]** |
| `FUN_00f2ead4` | Receive path (decoder) / state machine: read 24-byte envelope (`FUN_00fbb87c`, flag `0x80`), XOR-decrypt, parse BE fields, HMAC verify, then read `len` payload bytes; length caps `0`…`0x400` **[V]** |
| `FUN_00f2ee64` | 16-byte MAC/digest computation — HMAC-MD5 with the §3 key **[V]** |
| `FUN_00fa4e20` | digest compare **[V]** |
| `FUN_00f2e878` | mgonet socket send wrapper **[V]** |
| `FUN_00fbb87c` | `recv` (MSG_WAITALL) — reads the 24-byte envelope **[V]** |
| `FUN_00f00778` | `mgonet_connect_timeo` — async connect op with timeout (state machine, error codes) **[V]** |
| `FUN_00f00890` | `mgo_connect_server_by_index(index, type)` — resolves a server table entry (0x34-byte entries at +0x7b8) and starts the framed connection **[V]** |
| `FUN_00f04920` | payload accessor — returns pointer to the frame payload; opcode is the first u32 **[V]** |
| `FUN_00f0494c` | **packet dispatcher** — opcode binary-search switch over 219 command ids, stub-dispatch to per-command handlers (§5.1) **[V]** |
| `FUN_00f08a18` | `0x4101` getCharacterInfo handler — stores first payload u32 (character id) at `game_ctx+0x15710` **[V]** |
| `FUN_00f106ec` | `0x4341` playerConnected handler — reads targetId, stores it into the game-side player roster **[V]** |
| `FUN_00f02e04` / `FUN_00f06488` | peer_id accessors — `*(u32*)(game_ctx + 0x15710)` **[V]** |
| `FUN_00aa0f48` | peer-struct builder — copies the module's 20-byte peer struct, overwrites word 0 with the character id, calls setter `FUN_002616d8` **[V]** |
| ctx anchors | `PTR_PTR_0121c51c` (codec keys/state), `PTR_PTR_0121c4d0` (connection manager) |
| state field | conn-state at ctx+0x1c (`0` init / `1` connecting…) |

Codec error codes observed: `-0x18` (bad args/index), `-0x68` (bad state), `-0x66`
(busy), `-0x65`/`-0x6f`/`-0x70`/`-0xce` (recv/again paths), `-0xd2` (send fail),
`-0x32`/`-0x31`/`-0x30`/`-0x2f` (recv/frame/MAC errors) **[V]** — sign convention:
negative = error.