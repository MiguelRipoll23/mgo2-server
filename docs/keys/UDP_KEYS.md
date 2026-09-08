# UDP P2P — Keys & Constants (everything needed to build a client)

All values verified in `MGO2.ELF` (retail `NPMG00020`) and/or against **10 live
captures** of a real join dial unless marked **[N]** (Nomad). The full protocol
description lives in `../udp_p2p.md`; this file is the quick reference.

> **Correction:** the old "12-byte envelope" model in earlier revisions of this file was
> the *internal* queue-node header, not the wire format. On the wire a datagram is:
> `[ 2-byte scrambled header ][ 4-byte prefix ][ message ][ 10-byte unchained tail ]`
> (see "Wire prefix" below and `../udp_p2p.md` §3/§5).

## Handshake gate — the one constant the receiver validates

| Constant | Value | On the wire | Notes |
|---|---|---|---|
| `module_magic` | `0x4d258ab7` | **LE**: `b7 8a 25 4d` | the receiver (`FUN_00267d58`) compares the third message u32 (decoded datagram offset `0x0e`) against this; mismatch = silent drop. Stored at `*(0x0122a5cc)` in the ELF; the sender emits it via the reversed-copy helper `FUN_0026cc10`, hence little-endian. |

## Frame crypto constants

| Constant | Value | Use |
|---|---|---|
| LCG multiplier | `0x5d588b65` (+1 per step, mod 2³²) | keystream + header-scramble PRNG (`x = x*0x5d588b65 + 1`) |
| Pre-handshake seed XOR | `0x87103c2f` | `K = 0x87103c2f` for handshake frames (session state < 8) |
| Post-handshake key | `peer_base ^ own_base` | `K = session[+8] ^ session[+0x10]`; both directions use the same value |
| Counter-base derivation | `0x2b58de69` | `session+0xc = peer_counter_base ^ 0x2b58de69` |
| Compression gate | flag `0x200` in session flags word | LZSS on/off (off by default in this build) |
| Compression marker | bit 7 (`0x80`) of header byte 1 | on-wire "compressed" marker |

## Network constants

| Constant | Value |
|---|---|
| Default p2p port | `0x2bad` = **11181** (persistent socket bind when port arg == 0 — NOT the join-dial source; the dial comes from the client's own advertised port, e.g. 5730) |
| Receive buffer | `0x578` (1400) bytes |
| One-shot datagram size | `0x1000` (4096) bytes |
| Session slots | 24 (`0x18`), stride `0x6d8` |

## Wire prefix (what actually precedes the message)

The handshake receiver does **not** validate these bytes — zeros/constants are safe:

| Off | Size | Field | Endian | Notes |
|---|---|---|---|---|
| `0x00` | u16 | **scrambled header** — unscrambles to the per-frame counter (`LE` u16); seeds the XOR chain | LE | see `../udp_p2p.md` §5.1 |
| `0x02` | u16 | const tag `0x1000` | LE (wire `00 10`) | constant across all 10 captures; meaning unresolved |
| `0x04` | u16 | **message length** | LE (wire `1c 00` = 28 for the handshake) | the size of the message that follows |

## Handshake — decoded message layout (mixed byte order, verified)

After the §5 unscramble+chain, the 44-byte handshake datagram decodes to:

| Datagram off | Field | Size | Endian |
|---|---|---|---|
| `0x00` | hdr counter | u16 | LE |
| `0x02` | const tag | u16 | LE |
| `0x04` | message length | u16 | LE |
| `0x06` | `peer_id` | u32 | **LE** |
| `0x0a` | `counter_base` | u32 | **LE** |
| `0x0e` | `module_magic` | u32 | **LE** (`b7 8a 25 4d`) |
| `0x12` | `ver` | u8 | – |
| `0x13` | `unk` | u16 | **LE** |
| `0x15` | `count` | u8 | – |
| `0x16` | entry `ip` | u32 | **BE** |
| `0x1a` | entry `port` | u16 | **LE** |

Mixed byte order comes from the two copy helpers: `FUN_0026cc10`/`FUN_0026cc88`
(byte-reversed → **LE**) for everything except the entry `ip` u32s, which use
`FUN_0026cb98`/`FUN_0026cb20` (plain memcpy → **BE**).

`ver` = 2 in captures (the joiner's accept gate rejects `ver & 0x4` unless the session
flags word has bit `0x800` set, so send 2 or 3). `count` = 0..2; the entries are the
sender's OWN endpoints (public + private). `unk` = 1 in captures.

## Handshake acceptance — the gates a fake host must pass (joiner side)

A fake host's reply must satisfy **all** of these (verified in `module.bin`, `FUN_00268ba8`
queue-drain at `0x268e20..0x269068`); a failure discards the message and the joiner
keeps re-dialing every ~1.9 s:

| # | Gate | Value required | Binary ref |
|---|---|---|---|
| 0 | **tail digest** — decoder computes §5.3 digest of the unscrambled datagram and memcmps the wire tail | computed, not zeroed — wrong tail = datagram dropped at the decoder, before anything else | `0x267274` (pre-hs path `0x266ff4`, keyed path `0x266948`; drop at `0x266918`) |
| 1 | message `peer_id` (msg[0..4)) == `session[0x18]` | **echo the joiner's own peer_id** (its character id from TCP `0x4101`, e.g. `2`) — NOT the host's id | `0x268ef4` |
| 2 | message `module_magic` (msg[8..12)) == `0x4d258ab7` | `b7 8a 25 4d` | `0x268fd8` |
| 3 | `ver & 0x4 == 0` unless session flags (`session+0x14`) have bit `0x800` | `ver=2` passes for dialing sessions | `0x269010` |

On acceptance the joiner stores the host's `counter_base` → `session[8]`,
`session[0xc] = base ^ 0x2b58de69`, sets session flags `0x2|0x4`, and re-sends its own
handshake (ping-pong); the receive loop routes a matched session (state 2..6) through
the sender's queue-drain, and an unmatched datagram through the global accept session
→ handshake receiver `FUN_00267d58` (which validates magic only).

## Frame crypto — verified implementation

Verified byte-for-byte: decoding each of the 10 live captures and re-encoding
reproduces the exact wire bytes. See `../udp_p2p.md` §5 for the annotated version.

```python
import hashlib

MULT = 0x5d588b65          # LCG multiplier
PRE  = 0x87103c2f          # pre-handshake XOR (handshake frames)
MAGIC = 0x4d258ab7         # module magic, LE on the wire
TAILK = 0x2b58de69         # tail-digest MD5 key (LE u32)

def lcg(x): return ((x * MULT) + 1) & 0xffffffff

def positions(n):          # header-scramble positions from datagram length
    x0 = lcg(n); x1 = lcg(x0); x2 = lcg(x1); x3 = lcg(x2)
    p0 = ((x0 >> 16) % (n - 2)) + 2
    p1 = ((x1 >> 16) % (n - 2)) + 2
    q0 = (x2 >> 16) % n
    q1 = (x3 >> 16) % n
    return p0, p1, q0, q1

def chain(b, hdr, keyed, encrypt):
    seed = ((((hdr & 0x7fff) * MULT) + 1) & 0xffffffff) ^ keyed
    prev, pos, rem = 0, 2, len(b) - 0xc
    while rem > 0:
        seed = (seed + prev) & 0xffffffff
        take = min(4, rem)
        w = int.from_bytes(b[pos:pos+take], "little")
        out = (w ^ seed) & 0xffffffff
        b[pos:pos+take] = out.to_bytes(take, "little")
        prev = w if encrypt else out        # plaintext feedback
        pos += take; rem -= take

def decode(raw, keyed):
    b = bytearray(raw); n = len(b)
    p0, p1, q0, q1 = positions(n)
    b[1], b[q1] = b[q1], b[1]               # header unscramble
    b[0], b[q0] = b[q0], b[0]
    b[1] ^= b[p1]
    b[0] ^= b[p0]
    hdr = b[0] | (b[1] << 8)                # LE u16
    chain(b, hdr, keyed, False)
    return hdr, bytes(b)

def tail_digest(b):        # b = UNSCRAMBLED, pre-chain datagram; returns 10-byte tail
    key = TAILK.to_bytes(4, "little")
    d1 = hashlib.md5(key + b[:len(b)-0xa]).digest()
    d2 = hashlib.md5(key + d1).digest()
    t = bytearray(d2[:0xa])
    for i in range(6):
        t[i] ^= d2[0xa+i]
    return bytes(t)

def encode(plain, hdr, keyed):
    b = bytearray(plain); n = len(b)
    p0, p1, q0, q1 = positions(n)
    chain(b, hdr, keyed, True)
    b[n-0xa:] = tail_digest(bytes(b))        # AFTER chain, BEFORE scramble
    b[0] ^= b[p0]; b[1] ^= b[p1]            # header scramble = inverse
    b[0], b[q0] = b[q0], b[0]
    b[1], b[q1] = b[q1], b[1]
    return bytes(b)
```

- Handshake frames: `keyed = PRE`. Data frames: `keyed = peer_base ^ own_base`.
- The chain covers bytes `[2 .. len-0xa)`; the last 10 bytes of every datagram are
  outside it.
- **The tail is a self-verifying MD5 digest, validated on every datagram** (decoder
  `0x266ff4`/`0x266948`, memcmp at `0x267274`); mismatch = silent drop before any
  field check. Computed on the **unscrambled pre-chain** datagram, so the sender
  applies it after the chain and before the scramble (§5.3 of `../udp_p2p.md`).
- `q0`/`q1` are mod `len` and can land on the header bytes themselves (44-byte
  handshake: `q0=3, q1=42`) — the swap order matters; encode is the exact inverse.

## Worked example (real live capture, frame 0 of the joiner's dials)

Raw wire (44 bytes):

```
2c 9e 2e 85 0c 87 2c 4c 2c 87 47 ff db c7 10 75 06 85 5c 8b 49 17 39 0a
59 dc db 1a 9a 4a 1a 11 78 9d f2 78 b8 c1 3e fb 30 c1 0a 51
```

Decoded (hdr `0x0000`):

```
0000 0010 1c00 02000000 77b3f740 b78a254d 02 0100 02
598110cb 6216 c0a80132 6216 | f278b8c13efb30c19e51
└hdr┘└─prefix─┘└ peer_id ┘└ counter ┘└ magic ┘    └ tail ┘
  hdr=0   tag=0x1000  peer=2     base=0x40f7b377  ver=2 unk=1 count=2
          len=0x1c    LE                          entries: 89.129.16.203:5730,
                                                    192.168.1.50:5730
```

Handshake body is byte-identical across all 10 retries; only the hdr counter (0..9)
and the tail digest vary (the digest covers the counter). The `tail_digest` formula
above reproduces the wire tail of every capture exactly.

## Reference implementation

`mgo2-server` (Deno) `src/tasks/udp-dump.ts` — fake-host mode: decodes the joiner's
handshake with the code above, logs it, and replies with an encoded handshake
(`P2P_ID`/`P2P_BASE`/`P2P_HOST` env tunables). The earlier `mgo2-udp-tools`
implementation used the wrong (plaintext/envelope) model and is superseded.

## Where these live in the binary

| Constant / helper | Location |
|---|---|
| `module_magic` value | `0x0122a5cc` (slot loaded via `-0x7fd8(r30)`) |
| Tail digest key `0x2b58de69` + MD5 (two-step) | decoder `FUN_002666c8` pre-handshake path `0x266ff4` (MD5 helpers `0xf99b00` init / `0xf99b60` update / `0xf99d28` final) |
| LCG / scramble constants | `FUN_002666c8` (decoder — the authoritative inverse), frame builder `FUN_00264c78` |
| Header scramble positions | decoder `FUN_002666c8` prologue (LCG chain from datagram length) |
| Handshake sender / receiver | `FUN_00268ba8` (send) / `FUN_00267d58` (receive) |
| LE cursor helpers (readers/writers) | `FUN_0026cc10` (writer) / `FUN_0026cc88` (reader) — `{pos, end, base}` cursor struct; each call advances `pos` by `len` |
| BE copy helpers | `FUN_0026cb98` / `FUN_0026cb20` |
| Queue node builder (receive) | decoder path at `0x266d48` (internal 12-byte node header; not the wire format) |
| Message reader (receive) | `FUN_0026b5c8` |
| Send gate | `FUN_00263530` (state byte > 1) |