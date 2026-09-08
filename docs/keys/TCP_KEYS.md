# TCP Game-Server — Keys & Constants (cross-verified: MGO2.ELF + Nomad)

## Frame keys

| Key | Value | File |
|---|---|---|
| Frame XOR key | `0x5a7085af` (4 bytes, BE; XORs the **whole** frame: 24-byte header + payload, byte `i ^ key[i & 3]`) | `tcp_xor_key.bin` |
| HMAC-MD5 key | ASCII `"Z7/biJ46TzGF-8yx"` (16 bytes: `5A 37 2F 62 69 4A 34 36 54 7A 47 46 2D 38 79 78`) | `tcp_hmac_md5_key.bin` |

Both live in the ELF at `vaddr 0xff9040` (KEY_XOR) and `0xff9048` (KEY_HMAC).

## Blowfish payload cipher

Three usable forms, all **4168 bytes** (18 P-array words + 4×256 S-box words = 1042 u32):

| File | What it is | Notes |
|---|---|---|
| `blowfish_pi_tables.bin` | canonical π hex constants, **big-endian u32 words** (P[0] = `0x243f6a88`) | the game builds its key schedule from these at init; a client can do the same (standard Blowfish key schedule, then use the P-array at offset 0x00 and S-boxes at 0x48 / 0x448 / 0x848 / 0xc48) |
| `blowfish_packet_key.bin` | **pre-scheduled** table for game packet payloads | byte dump of Nomad's `Constants.CRYPTO_PACKET`; ready to load directly as the Blowfish internal state — no key schedule needed |
| `blowfish_auth_key.bin` | **pre-scheduled** table for auth/login payloads | byte dump of Nomad's `Constants.CRYPTO_AUTH`; same layout |

Pre-scheduled word order: each 4-byte chunk is the u32 **big-endian** (matches the
PPC memory layout). Nomad loads these with `Crypto.instancePacket()` /
`Crypto.instanceAuth()` and uses them as plain 16-round Feistel over 8-byte blocks
(ECB per block, no chaining), padding payloads to 8 bytes.

## Which payloads are Blowfish-crypted

Only these command ids decrypt with the packet instance:
`0x3003, 0x4310, 0x4320, 0x43c0, 0x4700, 0x4990`.

The auth instance is used for login/account command payloads
(`Users.java: Crypto.instanceAuth().encrypt(...)`).

## Frame layout recap (see `../tcp_protocol.md` for the full protocol)

| Off | Size | Field |
|---|---|---|
| 0x00 | u16 BE | command id (on wire: `cmd ^ (KEY_XOR >> 16)`) |
| 0x02 | u16 BE | payload length (on wire: `len ^ KEY_XOR`), max 0x3ff |
| 0x04 | u16 | sequence |
| 0x06 | u16 | pad |
| 0x08 | 16 | HMAC-MD5 checksum |
| 0x18 | … | payload (Blowfish for the crypted ids; pad to 8 when crypted) |

## Source cross-reference (Nomad)

- `Util.KEY_XOR`, `Util.KEY_HMAC`
- `crypto/Constants.java` — `CRYPTO_PACKET` / `CRYPTO_AUTH`
- `crypto/Crypto.java` — instance selection (packet vs auth)
- `Packet.java` — frame assemble, XOR, HMAC, Blowfish