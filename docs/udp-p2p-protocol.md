# MGO2 — UDP Player-to-Player Protocol

The wire protocol of the **UDP peer-to-peer** channel of `MGO2.ELF` (`NPMG00020`),
reverse-engineered from the game binary **and verified byte-for-byte against 10 live
captures** of a real join dial — and exercised end-to-end through the **data phase**
by a working fake host (2026-09-09, §6.2/§11). The TCP game-server channel is documented separately in
`tcp-game-server-protocol.md` and is **not** part of this channel — the mgonet XOR/HMAC/Blowfish
framing and the `0x2a`/`0x2d` stream readers that open `socket(2,1,0)` must not be
conflated with the p2p channel.

> Tags: **[V]** verified in `MGO2.ELF` and/or against live captures, **[I]** inferred,
> **[U]** unresolved statically.

---

## 1. Channel overview

| Item | Value |
|---|---|
| Transport | plain BSD-style UDP datagrams — no reliable-UDP library (no RUDP / `rdt_` / ENET / SCTP / AVE-TCP / Konami-net code or strings in the image) **[V]** |
| Persistent socket | port `0x2bad` = **11181**; one socket per session (fd at ctx+4) — the default bind **when the port argument is 0**; the join-flow dial does **not** use it (§2) **[V]** |
| One-shot senders | `0x28500`/`0x28c68`: fresh socket per datagram, one `0x1000`-byte datagram, close **[V]** |
| Datagram shape | `[ 2-byte scrambled header ][ chain region (len−0xc bytes from offset 2) ][ 10-byte unchained tail ]` (§3) **[V]** |
| Handshake | **44-byte scrambled + XOR-chained datagram**, first datagram of a session, one per direction — carries peer_id, counter base and the sender's own endpoints (§4) **[V]** |
| Frame crypto | homegrown **LCG-XOR stream + header scramble**, **no secret key** — captures decryptable offline, no brute force **[V]** |
| Compression | LZSS, **off by default**; self-describing marker bit when on (§7) **[V]** |
| Identity | per-session `counter_base` only; the device fingerprint (Salsa20 token) is **not** sent on this channel **[V]** |

---

## 2. Join flow — end to end (TCP → UDP) [V]

The join is driven by the **TCP game-server channel**; the UDP dial is the very next step.

```
joiner (RPCS3/PS3)                        game server                     fake host / real host
      │ 0x4320 joinGame                       │                                 │
      ├───────────────────────────────────────▶│                                 │
      │ 0x4321 {result u32, publicIp[16],     │                                 │
      │         publicPort u16, privateIp[16],│                                 │
      │         privatePort u16, +3B}         │  ← advertises the host endpoint  │
      │◀───────────────────────────────────────┤                                 │
      │                                        │                                 │
      │  UDP dial (44-byte handshake, scrambled) ───────────────────────────────▶│
      │  source = client's own p2p port (e.g. 5730)     destination = advertised │
      │  retried ~1.9 s until answered; ~40 s give-up → TCP 0x4322               │
      │◀─────────────────────────────────────── host's 44-byte handshake reply ──┤
      │  (joiner accepts when module_magic matches, records counter_base)         │
      │                                        │                                 │
      │  data frames, keyed seed = peer_base ^ own_base  ◀──────────────────────▶│
```

1. **TCP join request** — client sends `0x4320` (joinGame) to the game server; the
   server replies `0x4321` with the host's connection info:
   `result u32, publicIp char[16], publicPort u16, privateIp char[16], privatePort u16,
   trailing 3 bytes` (canRate/rule/map-style flags, meaning per-game **[U]**).
2. **The client stores the advertised endpoints** — the `0x4321` handler
   `FUN_00f12278` (game-side) parses the reply and stores:
   `game_ctx+0x19b40` publicIp, `+0x19b52` publicPort, `+0x19b54` privateIp,
   `+0x19b66` privatePort **[V]** (verified in `MGO2.ELF` with capstone).
3. **The connect driver starts a p2p session** — the joiner's connect FSM (tick at
   `0xaa1140`, dispatched through the game's function-index table at `0x1208bd0`)
   reads those stored endpoints via the getters `0xf0ccf4`/`0xf0cd10`/`0xf0cd30`/
   `0xf0cd4c`, converts the dotted IPs to binary, and calls `0x261fe0` → session init
   `FUN_00268148` **[V]**.
4. **First datagram = the handshake** — sent from the client's **own UDP socket**
   (source port = the client's advertised p2p port, **5730 in every live capture**)
   to the **advertised host endpoint** (e.g. `192.168.1.50:3578`). It is a 44-byte
   scrambled + XOR-chained datagram (§4/§5) — **not** plaintext. Re-sent every
   ~1.9 s until the host answers; on ~40 s of silence the client gives up and the
   TCP side fires `0x4322` (join failed) **[V]**.
5. **Host replies with its own handshake** — same 44-byte format, sent back to the
   joiner's source address. Two distinct receive paths handle an incoming datagram **[V]**:
   - **Incoming connection (no matching session):** the receive loop falls through to
     the global accept session and calls the handshake **receiver** `FUN_00267d58`,
     which validates `module_magic`, stores `counter_base` → `session+8`
     (`session+0xc = base ^ 0x2b58de69`), and creates the peer session (`0x261fe0`).
   - **Reply to an existing session (state 2..6):** the receive loop calls the handshake
     **sender's queue-drain** (`FUN_00268ba8`), which consumes the queued message and
     validates three gates (§4): `peer_id == session[0x18]` (the reply's peer_id is the
     **sender's own id** — for a host reply that is the **host's character id**, which the
     dial session stored at creation; see §4 gate 1), `module_magic`, and the
     `ver`/flags check. On acceptance it stores the host's
     `counter_base` → `session[8]`, sets session flags `0x2|0x4`, and re-sends its own
     handshake — the handshake ping-pong.
6. **Data frames** — both directions now use the **keyed seed**
   `((hdr & 0x7fff) * 0x5d588b65 + 1) ^ (peer_base ^ own_base)` (§6) **[V]**.

### Practical findings (from live testing)

- **The dial goes to exactly what the `0x4321` reply advertised** (in a private server
  that is the host's `character_connections` row). The advertised IP must be a **real,
  reachable LAN IP**:
  - `0.0.0.0` → the client's `sendto` fails outright (no datagram ever leaves) **[V]**.
  - `127.0.0.1` → dialed inside the emulator's guest loopback and **swallowed — never
    reaches the host machine** (RPCS3), so a host-side listener sees nothing **[V]**.
  - Working setups advertise the host machine's LAN IP, e.g. `192.168.1.100:5730` **[V]**.
- **Source port:** the join dial is sent from the client's own p2p port (5730 in the
  captures) — **not** from 11181. `0x2bad`/11181 is only the *default bind* of the
  persistent socket when the caller passes port 0 (`FUN_00262b00`); the join path
  passes the client's advertised port instead **[V]**.
- **Server-side TCP handlers that the join depends on** (beyond `0x4320/0x4321`):
  `0x4344`/`0x4346` must echo `{result u32, key u32}` — an empty ack stalls the host's
  peer FSM (30 s timeout → `0x4342` disconnect) — and `0x4322` (join failed) must be
  answered with `0x4323` so a joiner whose dial fails doesn't hang **[V]** (reference
  server behavior, cross-checked with the game's parsers).
- **Fake-host continuation (decoder decompile + live, 2026-09-08):** after the
  accepted reply (state 6) the joiner keeps re-dialing and pinging **pre-keyed** — it
  cannot reach state 8 on handshakes alone. The host must send **one session-keyed
  frame** (digest `K ^ 0x2b58de69`, chain `K`; an empty tag-`0x5000` keep-alive is the
  verified shape) — that is the handshake-phase second digest chance that sets
  `flags |= 9` (§6.1). And **all** host→joiner frames must draw from ONE outbound hdr
  counter: the joiner's seq window (§5, `session[0x42]` + reorder bitmask) drops
  anything outside `last+1 .. last+0x20`, so separate reply/data counters interleave
  and get silently discarded **[V]** (verified in simulation: two-counter scheme
  produces duplicate hdrs → drops).
  **Confirmed live 2026-09-09:** the session-keyed ESTABLISH OUT flipped the
  joiner into state 8 — the ~2 s handshake retries stopped and the first
  session-keyed data-phase frames were captured (§6.2).

---

## 3. Wire datagram format [V]

One datagram (any length `len`):

```
[ 0 .. 2)         scrambled header — unscrambles to a LE u16 counter (§5.1)
[ 2 .. 4)         message type (u16 LE) — 0x1000 = handshake/one-shot, 0x5000 = keep-alive,
                  0xc480 = reliable data, 0x1001 = control (§6.2)
[ 4 .. 5)         len (u8) — this message's body length
[ 5 .. 6)         flags2 (u8) — per-message flags byte
[ 6 .. 6+len)     message body — further messages (each with their own type/len/flags2/body)
                  follow immediately; the whole [2 .. len-0xa) region is XOR-chained (§5.2)
[ len-0xa .. len) 10-byte tail — NOT XOR-chained; a **self-verifying MD5 digest** (§5.3) that the
                   decoder validates on EVERY datagram; wrong tail = silent drop
```

- **Header** = the sender's per-frame counter, **little-endian u16** after the
  unscramble (§5.1). It seeds the XOR chain and lets a receiver correlate both
  directions of a session. In the 10 captured dials it ran `0,1,2,…,9` (one per
  retry) **[V]**.
- **Prefix** — a message header, the same shape every message on the channel uses
  (serializer `FUN_00269860`): `type u16 LE | len u8 | flags2 u8`. The handshake's
  `00 10 1c 00` = type `0x1000`, len `0x1c` (28), flags2 `0x00` (the older
  "u16 message length" reading is superseded, §13). The receiver does **not**
  validate it — a client/fake host can send the captured constant
  `00 10 1c 00` **[V]**.
- The old "12-byte envelope" from earlier static analysis (`FUN_0026b778` building
  `u32, u16, u16 len+0xc, 4×u8`) describes the **internal queue-node header**
  (payload at node+0xc, consumed by `FUN_0026b5c8`); on the **wire** the message is
  preceded only by the 2-byte scrambled header + this 4-byte prefix **[V]**.

---

## 4. Handshake — scrambled on the wire, both directions [V]

The first datagram of a session. **44 bytes**, sent via the same encoder as data frames
(the handshake receiver calls the data-frame decoder `FUN_002666c8` before parsing).
Decoded layout (offsets relative to the datagram start, after §5 unscramble+chain):

```
[ 0 .. 2)   hdr counter (LE u16)               — seeds the XOR chain
[ 2 .. 4)   u16 LE 0x1000                      — message type (not validated)
[ 4]        len u8 (= 0x1c = 28)               — handshake body length
[ 5]        flags2 u8 (= 0x00)
[ 6 ..10)   peer_id (LE u32)                   — the SENDER's own character id
                                               (joiner sends its own; a host reply
                                               carries the host's id — see §4 gate 1)
[10 ..14)   counter_base (LE u32)              — key material (§6)
[14 ..18)   module_magic (LE u32) = b7 8a 25 4d — validated; mismatch = silent drop
[18]        ver (u8; 2 in captures; receiver rejects bit 2, so send 2 or 3)
[19 ..21)   unk (u16 LE; 1 in captures)
[21]        count (u8, 0..2; 2 in captures)
[22 ..28)   entry 0 { ip[4], port u16 LE }
[28 ..34)   entry 1
[34 ..44)   tail — self-verifying MD5 digest (§5.3); NOT free bytes — a fake
            host must compute it or the decoder drops the datagram
```

- **Byte order is mixed**: `peer_id`/`counter_base`/`module_magic`/`unk`/`port` are
  **LE** (reversed-copy helpers `FUN_0026cc10`/`FUN_0026cc88`), the entry `ip` u32s are
  **BE** (plain-copy helpers `FUN_0026cb98`/`FUN_0026cb20`); `ver`/`count` are single
  bytes. Verified via the receiver's magic compare — wire bytes for `0x4d258ab7` are
  `b7 8a 25 4d` **[V]**.
- **Entries = the sender's OWN endpoints** (public + private): a live capture showed
  `89.129.16.203:5730` (public) and `192.168.1.50:5730` (private). The host replies to
  the joiner's source address and uses these entries to dial the joiner back **[V]**.
- **Ordering is enforced by the session state:** session init `FUN_00268148` starts the
  session by sending the handshake, so it is the first datagram; the session state byte
  (`session+4`) gates which XOR key applies (§6) **[V]**.

- `peer_id` — **the sender's own character id**, sourced game-side from the TCP
  game server (packet `0x4101`). Chain, verified end-to-end in the binary **[V]**:
  1. Game-side TCP handler `FUN_00f08a18` (opcode `0x4101` = getCharacterInfo)
     reads the packet's **first u32** into `game_ctx + 0x15710`.
  2. Accessor `FUN_00f02e04` returns `*(u32*)(game_ctx + 0x15710)`.
  3. `FUN_00aa0f48` builds the 5-word peer struct with that value as word 0
     (`stw r3, 0x70(r1)` after `bl 0xf02e04`), then calls the setter.
  4. Setter `FUN_002616d8` copies the 5 words → `module_base + 0x74..0x84`.
  That struct is the module's OWN identity: its word 0 is stamped into every
  outgoing handshake. Incoming handshakes are validated against the session's stored
  PEER descriptor: the accept-path receiver `FUN_00267d58` checks only
  `module_magic`, but the dial-session queue-drain gate 1 compares the reply's
  peer_id against `session[0x18]` (§4 gate 1) **[V]**.
- `counter_base` — the seed material for data frames (§6): the receiver stores it at
  `session+8` and derives `session+0xc = base ^ 0x2b58de69` (fixed constant) **[V]**.
- `module_magic` — validated by the receiver; a fake player learns it from the peer's
  frame and echoes it back **[V]**.

### A fake host reply

Mirror the joiner's shape, encoded with the inverse scramble (§5.1):

```
{ hdr counter, 00 10 1c 00, peer_id = the HOST's character id (sender's own id —
  the joiner gates on it; default 1 for the seeded NPC),
  our counter_base, module_magic b7 8a 25 4d, ver=2, unk=1, count=2,
  our endpoint ×2 (public+private, same value in a LAN test), tail = §5.3 digest }
```

Send it back to the joiner's **source address** (its UDP socket). This is exactly what
`mgo2-server`'s `src/tasks/udp-dump.ts` does (fake-host mode), verified end-to-end:
decode of the joiner's live dial → build → encode → joiner-side re-decode parses as a
valid handshake **[V]**.

### How the joiner accepts the reply (the gates that block fake hosts)

The joiner's reply is routed by the receive loop (`FUN_002620d8`) to its dial session
(source ip:port match), decoded, and consumed by the handshake sender's queue-drain
(`FUN_00268ba8`). Acceptance requires, in order **[V]** (all verified in `module.bin`):

0. **The tail digest matches (§5.3).** The decoder `FUN_002666c8` runs this check
   on EVERY datagram *before* anything else: `MD5(key||digest1)` derived from the
   unscrambled datagram is memcmp'd against the wire tail (`0x267274`); on mismatch
   the frame is dropped at `0x266918` and never reaches the queue-drain. A fake host
   that zeroes the tail gets silently ignored — the joiner just keeps re-dialing
   every ~1.9 s. This was the bug that made the peer_id/magic fixes look ineffective.
1. **`peer_id` (message[0..4)) equals `session[0x18]`** — the 5-word peer descriptor
   (`session+0x18..+0x2b`) copied from the session-creation argument when the dial
   session was created (decoder `0x26864c`; the only store to `+0x18` in the module).
   Wire peer_id is the **sender's own character id** (the joiner's handshake carries
   its own id, 2 in all captures — so the field is not an echo of the receiver). The
   gate therefore asks "is this reply from the peer I am dialing": for a joiner's
   dial session that is the **HOST's character id** (1 = the seeded NPC), NOT an echo
   of the joiner's id. Gate check at decoder `0x269340`
   (`lwz session+0x18; cmpw; beq accept-side / li r27,0 skip-side`).
   **A peer_id mismatch fails SILENTLY** — the record is skipped, state stays 5, the
   peer base is never stored, and the joiner keeps re-dialing every ~1.9 s — while a
   wrong `module_magic` would instead move the session to state 6. That asymmetry is
   diagnostic live: continued re-dialing after byte-perfect replies means peer_id,
   not magic, is the failing gate. This was the real fake-host blocker: echoing
   the joiner's id (the tempting, wrong reading) fails this gate silently.
2. **`module_magic` (message[8..12)) == `0x4d258ab7`** (decoder `0x269420`:
   `lwz` of the magic global, `cmpw` against the message word; mismatch → `0x269524`,
   state → 6).
3. **`ver & 0x4 == 0`**, unless the session flags word (`session+0x14`) already has bit
   `0x800` set — i.e. a reply carrying `ver` bit 2 only passes for sessions without the
   `0x800` flag (decoder `0x269458`–`0x269474`: `rlwinm …,0x1d,0x1d` = mask `0x4`;
   when the bit is set the fallback path ORs `4` into the ver byte copy if flags bit
   `0x800` is also set). The fake host's `ver=2` has bit 2 clear and passes for
   dialing sessions.
4. On acceptance: session flags |= `0x2|0x4`, peer `counter_base` → `session[8]`,
   `session[0xc] = base ^ 0x2b58de69`, and the joiner re-sends its own handshake
   (the ping-pong); subsequent frames use the keyed seed (§6).

> **Plugin interaction (MGO2PC builds):** in the MGO2PC revival client the two decoder
> call sites inside the receive loop `FUN_002620d8` (`0x002623f4`, `0x00262528`) are
> hooked by `plugin.sprx` to a pass-through byte-counter wrapper (`FUN_15180`;
> v2.18.7 twin at plugin `0x1c300`) — it calls the original `FUN_002666c8` with unchanged
> arguments and only accumulates a write-only telemetry counter. It changes no wire byte,
> no key, and no validation gate, and cannot distinguish a real host from a fake one on
> this channel. See `code-injection.md` §3.4.

---

## 5. Frame crypto: LCG-XOR stream + header scramble [V]

`FUN_002666c8` (decoder) undoes, and the frame builder applies, the protection below.
All constants live in the binary; **there is no secret key** — obfuscation-grade
protection, not confidentiality. **Verified byte-for-byte**: decoding any of the 10
live captures with §5.1+§5.2 and re-encoding reproduces the exact wire bytes.

### 5.1 Header scramble — positions from the datagram length

```
LCG(x) = x * 0x5d588b65 + 1  (mod 2^32)

X0 = LCG(len);  X1 = LCG(X0);  X2 = LCG(X1);  X3 = LCG(X2)

p0 = ((X0 >> 16) % (len - 2)) + 2
p1 = ((X1 >> 16) % (len - 2)) + 2
q0 = (X2 >> 16) % len
q1 = (X3 >> 16) % len
```

**Decode** (receiver `FUN_002666c8`, exactly this order):

```
swap(buf[1], buf[q1]);  swap(buf[0], buf[q0])
buf[1] ^= buf[p1];      buf[0] ^= buf[p0]
hdr = buf[0] | (buf[1] << 8)      # LE u16
```

**Encode** (sender) = exact inverse order:

```
buf[0] ^= buf[p0];      buf[1] ^= buf[p1]
swap(buf[0], buf[q0]);  swap(buf[1], buf[q1])
```

Notes:
- `q0`/`q1` are mod `len`, so they can land **on the header bytes themselves** (for the
  44-byte handshake: `q0 = 3`, `q1 = 42`) — the byte swaps move header bytes into the
  body and vice versa, and the exact order matters. Encode and decode are **not**
  symmetric; use the inverse exactly as written **[V]**.
- This supersedes the old formula (`X1 = LCG(len+10)`, `% (len+8)`/`% (len+10)`) which
  was wrong **[V]**.

### 5.2 XOR chain — LE32 words, plaintext feedback

Covers bytes `[2 .. len-0xa)` (i.e. `len - 0xc` bytes from offset 2 — the prefix plus
the message region):

```
seed = ((hdr & 0x7fff) * 0x5d588b65 + 1) ^ K        (mod 2^32)
chain_0 = seed
for each LE32 word i:  C_i = P_i ^ chain_i
                       chain_{i+1} = chain_i + P_i   (mod 2^32)
```

- K is chosen by the decoder from the session state byte at `session+4` (the
  state machine, §6.1): state `2` → the **pre-handshake XOR `0x87103c2f`**;
  otherwise `session[8] ^ session[0x10]` = `peer_base ^ own_base` (§6) **[V]**.
- **Header sequence gate** (decoder `0x26712c`–`0x267198`): the incoming `hdr` (from
  the stack at `0x267130`) is compared to `session[0x42]` (u16, expected counter,
  init 0); a 16-bit signed difference `> 0x1f` (31) drops the datagram at `0x267e80`
  (too far ahead). In-order/duplicate handling uses a sliding-window bitmask in
  `session[0x44]`: bits mark received offsets; while bit 0 is set the mask shifts
  right and `session[0x42]` advances to the next missing offset
  (`0x267144`–`0x267198`). The joiner's retry counters (`0..9`) satisfy this
  trivially, and a reply should echo a nearby counter **[V]**.
- The chain always advances from the **plaintext** `P_i`, so encrypt and decrypt are
  distinct operations (both replayable from the seed) **[V]**.
- Trailing partial word (< 4 bytes): only the remaining bytes are transformed —
  equivalent to decrypting the full 4-byte word and keeping the low bytes **[V]**.
- The **last 10 bytes of every datagram** (`[len-0xa .. len)`) are **outside the chain**
  (the scramble positions may still touch up to two of them) **[V]**.

### 5.3 Tail digest — 10-byte self-verification on every datagram [V]

The tail is **not free bytes**: the decoder computes a digest of the datagram and
memcmps it against the wire tail (`0x267274`); on mismatch the frame is dropped at
`0x266918` before any field is parsed. Both decode paths (pre-handshake `0x266ff4`,
keyed `0x266948`) do this — verified byte-for-byte against all 10 live captures.

```
key    = 0x2b58de69                    # LE u32, same constant as §6's base-derive

d1 = MD5(key(4, LE)  || datagram[0 .. len-0xa))     # UNSCRAMBLED bytes, pre-chain
D  = MD5(key(4, LE)  || d1)

tail[i] = D[i]  ^  (i < 6 ? D[10+i] : 0)      for i in 0..9
```

- Computed on the **unscrambled, pre-XOR-chain** datagram (header unscramble §5.1
  happens first in the decoder); the chain and field parse come after the check **[V]**.
- So the sender must compute it **after** the XOR chain and **before** the header
  scramble — the scramble may touch tail bytes (e.g. `q1 = 42` for len 44) **[V]**.
- The first 6 tail bytes are the digest XORed with `D[10..15]`; bytes 6..9 are `D[6..9]`
  plain **[V]**.
- **The key is state-dependent, not one constant**: the pre-handshake path
  (`0x266ff4`) uses the constant; the keyed path (`0x266948`) computes
  `session[+0xc] ^ session[+0x10]` = `(peer_base ^ 0x2b58de69) ^ own_base` =
  `K ^ 0x2b58de69` (§6). The joiner's 16-byte state-6 keep-alives (§6.1) are
  **pre-keyed** — they verify with the bare constant but never exercise the
  keyed path. **Confirmed live 2026-09-09:** the first state-8 frames decode
  with the chain key `K` and verify the tail digest with `K ^ 0x2b58de69`
  (§6.2), closing the question.
- Verified: all 10 captured dials round-trip (`decode → re-encode == wire bytes`),
  the fake host's reply passes the joiner's digest check, and the joiner's live
  keyed pings verify with the constant key **[V]**.

---

## 6. Key derivation — post-handshake [V]

| session field | meaning |
|---|---|
| `session+4` | session state (u8): 0 teardown, 2 socket-open, 3 handshake sent, 5 awaiting reply, 6 reply accepted, 8 data phase, 12 teardown (store sites in §6.1) |
| `session+8` | peer's `counter_base` (from the peer's handshake) |
| `session+0x10` | own `counter_base` (sent in our handshake) |
| `session+0xc` | `peer_base ^ 0x2b58de69` (derived) — confirmed directly in the
  binary: the session-accept helper stores `+8 = block[0]` and
  `+0xc = block[0] ^ 0x2b58de69` back-to-back (`0x26908c`/`0x269094`) |
| `session+0x14` | flags (u16): role bits `0x1`/`0x2` (state-8 gate needs both), `0x4` reply accepted, `0x8` session key established (`flags |= 9` path, §6.1), `0x100`/`0x400`/`0x800` session-init bits, `0x200` LZSS gate (§7) |
| `session+0x18..+0x2b` | peer descriptor (5 words; `peer_id` at `+0x18` — §4 gate 1) |
| `session+0x2c..0x30` | peer sockaddr — source ip/port match in the receive loop (§9.1) |
| `session+0x42` | header seq (u16) — expected incoming hdr counter (§5.2 gate) |
| `session+0x44` | sliding-window reorder bitmask (u32, §5.2 gate) |
| `session+0x48` | key slot: `-1` = pre-keyed; else session-keyed (decoder `FUN_002666c8` gate) |

The decoder computes the post-handshake key as `K = session[+8] ^ session[+0x10]`
(`xor r16, r9, r0` over ctx+8 / ctx+0x10 in `FUN_002666c8`). Both directions use the
**same value** (`peer_base ^ own_base`), so the session is symmetric — no per-direction
keying **[V]**.

Handshake frames always travel with the **pre-handshake constant**
`K = 0x87103c2f` (state < 8), even though the handshake itself is scrambled/chained
(the old "handshake travels plaintext" claim was wrong) **[V]**.

**The state < 8 → pre-key rule covers the WHOLE frame** — chain and digest
alike — and is confirmed live: the joiner's state-6 keep-alives (§6.1) decode
with the §5 pre-key and verify the §5.3 digest with the bare constant, while
their wire bytes stay identical across runs with different session keys. Any
session-keyed (`K`) frame sent to a session that hasn't reached state 8 is
undecodable garbage to it.

### 6.1 Live observation — the state-6 keep-alive (what acceptance looks like)

Once the fake host's handshake reply carried the correct `peer_id` (§4 gate 1),
the joiner's dial session began emitting **16-byte frames immediately after
each host reply**:

```
decoded: 01 00 | 00 50 | 00 | 00 | <10-byte tail>
hdr u16 LE, type 0x5000, len 0, flags2 0 — an empty keep-alive
```

Facts established from two runs with different joiner counter-bases:

- The frames are **pre-keyed, not session-keyed**: their wire bytes were
  byte-identical across the two runs (a session-keyed frame would differ, since
  `K` differs), and the chain algebra gives `plain[2..6) ^ K = 0x87106c2f` =
  the pre-handshake constant `0x87103c2f ^ 0x5000`. They decode with the §5
  pre-key and verify the §5.3 digest with the bare constant.
- They **never appeared** while the reply was being silently dropped (wrong
  `peer_id`), so they are the reliable live signal that the reply reached the
  dial session and passed the acceptance gates into state 6.
- The session is **not** in the data phase: handshake re-sends continue at the
  usual ~2 s cadence alongside the pings, and per the key rule above the
  session must reach **state 8** before session-keyed frames flow.

State machine (u8 at `session+4`; all store sites in the module): 2
(`0x268890`, `0x268b58`, `0x268ecc`, `0x269514`), 3 (`0x268b04`, `0x2693a0`),
5→6 acceptance (`0x268e00`; magic-mismatch also lands at 6, `0x269524`),
**8** (`0x2690a8`), 12 (`0x26966c`), 0 = teardown/unused. The state-8 store
sits in the session-accept helper at `0x268ffc` (called from the receive loop
at `0x2629a4`/`0x262a14`): entry requires `flags & 2`; states 2/3/4 branch to
their own handlers; otherwise it (re)stores `+8`/`+0xc` from the parsed
handshake block and sets **state = 8 only when `(flags & 3) == 3`** — both
role bits must already be set.

**How a dial session reaches state 8 — resolved from the decoder decompile
(2026-09-08):** the handshake-phase decode path has a **second digest chance**:
if the tail fails the constant key it retries with `K ^ 0x2b58de69`, and on
success sets `session flags |= 9` (bit `0x8` = "session key established" + bit
`0x1`) — `target_002666c8…c` lines 176–194. Bit `0x1` is one of the two role
bits the accept pump requires (`(flags & 3) == 3`; bit `0x2` is set by the
dialer's own handshake send), so **one session-keyed frame from the peer is
what flips a state-6 dial session into the data phase**. This is also the only
path that ever sets bit `0x8` — the pre-keyed paths never do. Practical proof:
the joiner's state-6 keep-alives (below) are pre-keyed, i.e. it had not
established the key from the handshake reply alone.

**Confirmed live 2026-09-09:** the fake host sent exactly one session-keyed empty
`tag-0x5000` keep-alive (ESTABLISH OUT — the default `P2P_SEND_KEYED_FRAME=
keepalive` continuation in `udp-dump.ts`) after its handshake reply; the joiner's
~2 s handshake re-dials stopped within one retry cycle and its next frames were
session-keyed. The key-establishment path is not just in the decompile — it works
on a real client. What the joiner sends next is documented in §6.2.

**Decryption (offline, no brute force):** decode the peer's handshake (§5) for its
`counter_base` → `peer_base`; own base is what we sent. Then per data frame: undo the
header scramble (§5.1), recover `hdr`, compute
`seed = (peer_base ^ own_base) ^ ((hdr & 0x7fff) * 0x5d588b65 + 1)`, and replay the
plaintext-feedback chain over `[2 .. len-0xa)` (§5.2). No secret, no brute force.

### 6.2 The data phase (state ≥ 8) — live captures [V]

First session-keyed frames captured **2026-09-09**, immediately after the fake
host's ESTABLISH OUT flipped the joiner's dial session to state 8 (§6.1). All
decode with the §5.2 chain key `K = peer_base ^ own_base` and verify the §5.3
tail digest with `K ^ 0x2b58de69` — the keyed path, live. Capture session:
`K = 0xbbe4ff9c = 0xa9d0a9e4 (joiner base) ^ 0x12345678 (host base)`.

**Message wire format** (serializer `FUN_00269860`, the same header shape as the
handshake's `00 10 1c 00`):

```
[0..2)    hdr u16 LE — shared per-peer counter (§6.2); bit 0x8000 = LZSS-compressed
[2..4)    message type u16 LE        (class bits 0x4000/0x8000 live in this word)
[4]       len u8 — body length
[5]       flags2 u8 — per-message flags byte
[6..6+len) body
[6+len..)  further messages, concatenated, then the 10-byte §5.3 tail
```

The §3 "u16 LE message length" reading is really `len u8 + flags2 u8` — it only
read as one u16 because handshake lens are < 256. (The dump tool's `msgLen` line
for data frames is likewise the `len|flags2` merge, e.g. `0x502b` for the frame
below — ignore it; parse per this layout.)

**Observed message types:**

- **`0xc480` — reliable, LZSS-compressed game data.** 89-byte frames with hdr bit
  `0x8000` set. Example (hdr `0x8002`):
  `02 80 | 80 c4 | 2b | 50 | 08 14 00 06 88 6c … 67 00 00 00 | <10B tail>`
  = type `0xc480`, len `0x2b` (43), flags2 `0x50`, body starting `08 14 00 06…`
  (`0x0814` BE = 2068 — the LZSS decompressed-size prefix, §7). The **identical
  body is re-sent on every subsequent compressed frame** (hdr `0x8003`, `0x8004`,
  `0x8006`, `0x8008`…) — a reliable message awaiting its ACK. The fake host must
  ACK it; the pump treats a message whose id (low 12 bits of its first u32) matches
  the pending reliable message as the ACK **[I]** — exact ACK bytes **[U]**.
  The compressed content (the joiner's first game message) is still undecoded **[U]**.
- **`0x1001` — control / window signaling.** 17-byte frames, marker bit clear.
  Example (hdr `0x0005`): `05 00 | 01 10 | 01 | 01 | 00 | <10B tail>` = type
  `0x1001`, len 1, flags2 1, body `0x00`. flags2 cycles
  `1,1,1,2,1,2,1,3,2,1,3,2,4,1,3,2,4,1,5,3,2,4,1` (1..5) across the run —
  send-window / credit grants **[I]**.

**Observed message types at a glance:**

| type (u16 LE) | len | flags2 | role |
|---|---|---|---|
| `0x1000` | `0x1c` | `0x00` | handshake (§4) |
| `0x5000` | `0x00` | `0x00` | keep-alive (§6.1) |
| `0xc480` | `0x2b` | `0x50` | reliable, LZSS-compressed game data (above) |
| `0x1001` | `0x01` | `0x01..0x05` | control / window signaling (above) |

Note: the 89-byte `0xc480` frames carry the 43-byte message above followed by ~30
further bytes (`04 40 3c 10 … 67 00 00 00`) that do not yet parse as a clean
second message — they repeat byte-identically on every re-send; structure **[U]**.

**Shared counter confirmed live.** The joiner's hdrs over the run form one
monotonic sequence `0x8002, 0x8003, 0x8004, 0x0005, 0x8006, 0x0007, 0x8008, 0x0009,
0x800a, 0x000b, 0x000c, 0x800d, 0x000e, 0x000f, 0x8010, … 0x0023` — every frame,
compressed or control, increments the same counter by exactly 1 (it starts at 2:
hdr 0 = handshake dial, 1 = keep-alive). The `0x8000` marker is per-frame and is
masked out of the §5.2 seed by `hdr & 0x7fff`. This is the live proof for §2's
one-outbound-counter rule: a host must share one counter or its frames fall
outside the joiner's `last+1..last+0x20` seq window and are dropped.

**Where the join stands:** the transport join is complete — state 8, session key
established, the joiner streaming K-keyed frames. What remains is game content:
the reliable `0xc480` message stays unacked and is re-sent forever, the `0x1001`
grants keep cycling. Decode the LZSS body and answer its reliable message to let
the room/host data exchange proceed **[U]**.

---

## 7. Compression — LZSS, off by default [V]

- **Off by default in this build:** the gate is
  `(*(u16*)(session+0x14) & 0x200)` (the flags word — decoder `lhz r0, 0x14(r25)`
  at `0x267118`). Every
  statically-reachable writer of that flags word — session init `FUN_00268148` (sets bits
  `0x100/0x800/0x400/0x2`) and the handshake `FUN_00268ba8` (bits 1+2) — **never sets bit
  `0x200`**; only the frame builder *checks* it. Frames are sent uncompressed unless some
  runtime-dispatched module entry sets the bit **[I]**.
- **Self-describing when on:** the builder toggles header byte `param_2[1] ^= 0x80`
  (bit 7) as the marker, and only keeps the compressed result if it is smaller than the
  input, so small frames stay literal even with the flag set.
- **Algorithm:** LZSS — a running MSB-first bit stream of **1 flag bit per token**;
  `0` = one literal byte, `1` = a back-reference of **9-bit offset** then **4-bit
  (length − 2)**; the history window is **512 bytes** (`& 0x1ff` ring, mirrored writes at
  `i` and `i+0x11`); match finding uses a hash-chain; inputs capped at `0x2000` bytes into
  an `0x800`-byte output buffer.
- **Confirmed on the wire 2026-09-09:** the joiner's data-phase stream is
  compressed — its frames set the marker (hdr `0x8002`…`0x801e`) and their
  bodies begin with a `u16 BE` output-size prefix (`0x0814` = 2068) followed by
  the LZSS bit stream (§6.2). Handshake and `0x1001` control frames never set
  the marker **[V]**; which runtime path sets the `0x200` gate flag for the
  data phase remains **[U]**.

---

## 8. Send paths

- **Persistent socket** `FUN_00262b00(port, peer_ip4, peer_port)` — `port == 0` →
  `0x2bad` (11181); `socket/bind/setsockopt`; fd → ctx+4; peer endpoint stored (IP →
  ctx+0x40..0x43, port → ctx+0x44). Reached indirectly by the connection driver, which
  announces endpoint fields over the record stream **[V]**. Note again: the join-flow
  handshake is sent from the client's own advertised port, not this default **[V]**.
- **One-shot helpers** `0x28500`/`0x28c68` — fresh socket per datagram; build a
  `0x1000`-byte message through the shared writer `_opd_FUN_00fa1af0(buf, 0x1000,
  fmt_or_state, 1, seq, arg1, game_id, 0xa0020)` where **`seq` = the per-module counter
  at ctx+4** (timebase-seeded, incremented per send) — the one-shot path's sequence
  number, distinct from the frame-header counter **[V]** (a string-writer whose exact
  template is runtime state **[U]**); `FUN_00fbc1fc(0x1f, …)` is only an integrity **gate** — its two
  4-byte outputs are never transmitted, `rc == 0` decides the send (dispatches through a
  runtime OPD `PTR_FUN_0119f550` **[U]**); then `send` the whole 0x1000 datagram and
  `close` **[V]**.
- **Paced frame builder** `FUN_00264c78` — rate-budgeted per-peer message aggregation in
  the §3 format, applies the §5 scramble, and sends via a transport reached through the
  runtime table walk **[V]**.

---

## 9. Function map & key constants

### 9.1 Protocol functions

| Role | Function | Notes |
|---|---|---|
| Persistent socket setup (sender) | `FUN_00262b00` | `socket(2,2,0)` + bind + setsockopt; default port `0x2bad` only when port arg == 0; fd → ctx+4; peer endpoint → ctx+0x40..0x44 **[V]** |
| One-shot datagram senders | `FUN_0028500` / `FUN_0028c68` | fresh socket per datagram; 0x1000-byte datagram; sign-gate → `send` → `close` **[V]** |
| Teardown | `FUN_0028708` / `FUN_0028eb0` | `shutdown(fd, 2)` (SD_BOTH) + `close`; closes fds across the slot table **[V]** |
| Session frame builder (encoder) | `FUN_00264c78` | rate-budgeted per-peer message aggregation → frame; applies LCG-XOR scramble (§5); optional LZSS (§7) **[V]** |
| **Message decoder** | `FUN_002666c8` | **undoes §5 on every incoming datagram, handshake included** — header unscramble (§5.1) + LE32 XOR chain (§5.2); post-handshake key from `ctx[+8] ^ ctx[+0x10]` (§6); also called by the handshake receiver **[V]** |
| Message serializer (encoder) | `FUN_00269860` | writes `type/len/flags2/body` per message; copies class bits from msg flags **[V]** |
| **Handshake accept (joiner side)** | `FUN_00268ba8` queue-drain (drain body `0x268a64`…`0x26953c`) | consumes a received reply queued by the decoder: gates on `peer_id == session[0x18]` (`0x269340` — silent skip on mismatch), `module_magic` (`0x269420` — mismatch → state 6), `ver&4`/flags (`0x269458`); acceptance path: handshake block pass → `0x268df4` state-5-only entry → `0x268e00` stores `state=6`, flags `|= 0x400`, retry counter reset → `0x268e1c` zero-then-enqueue message → `0x268edc` re-encode + send (the ping-pong re-send); on success peer base → `session[8]`, flags `|= 0x2|0x4` — the joiner's acceptance path, distinct from the host's receiver `FUN_00267d58` **[V]** |
| Per-peer queue drain | `FUN_00269230` / `FUN_00269240` / `FUN_00269280` | message queues consumed by the frame builder **[V]** |
| One-shot datagram writer | `_opd_FUN_00fa1af0` | string-writer for the 0x1000 datagram (`buf, 0x1000, fmt_or_state, 1, seq, arg1, game_id, 0xa0020`); exact template runtime state **[U]** |
| Handshake sender / reply processor | `FUN_00268ba8` | builds the §4 payload (peer_id, `session+0x10` base, magic, ver, unk, count, entries) → envelope/send path; ALSO drains the receive queue and runs the joiner-side acceptance gates (§4) — the sender is the reply consumer **[V]** |
| Handshake receiver (incoming connections) | `FUN_00267d58` | called by the receive loop only when **no** existing session matches the source (the global accept session path); gated on decoder `FUN_002666c8` returning nonzero; parses the §4 fields; reads wire counter base → `session+8`; `session+0xc = base ^ 0x2b58de69`; validates `module_magic` only, not peer_id; creates the peer session via `0x261fe0` **[V]** |
| Queue node builder (receive) | decoder path at `0x266d48` | builds the internal 12-byte-node queue entries (payload at node+0xc, length at node+6) that `FUN_0026b5c8` consumes — this internal node header is **not** the wire format (§3) **[V]** |
| Message reader (receive) | `FUN_0026b5c8` | node reader: `src = queue_entry + 0xc`, `len -= 0xc` **[V]** |
| LE cursor helpers | `FUN_0026cc10` (writer) / `FUN_0026cc88` (reader) | cursor-based `{pos, end, base}` readers/writers: each call copies `len` bytes at the cursor and **advances it** (`pos += len`; capped at `end`) — this is how the sender and the joiner's queue-drain walk the message fields. Byte-reversed copies — `peer_id`/`counter_base`/`module_magic`/`unk`/`port` are **LE** on the wire **[V]** |
| BE copy helpers | `FUN_0026cb98` (write) / `FUN_0026cb20` (read) | plain memcpy — entry `ip` u32s are **BE** on the wire **[V]** |
| Queue append (wire datagram) | `FUN_002696d8` | copies `[envelope][payload]` (length from envelope+6) into the connection send buffer **[V]** |
| Send gate | `FUN_00263530` | only sends when connection state byte > 1; zeroes envelope bytes +8 and +11 **[V]** |
| peer_id setter | `FUN_002616d8` | copies 5 words → `module_base+0x74..0x84` (peer_id, count, entries…); called by game-side `FUN_00aa0f48` (peer-struct builder) **[V]** |
| peer_id source (game side) | `FUN_00f08a18` (0x4101 TCP handler) → `game_ctx+0x15710` → `FUN_00f02e04` → `FUN_00aa0f48` → setter | **peer_id = the character id from the TCP `0x4101` (getCharacterInfo) packet's first u32** (Nomad: `bo.writeInt(character.getId())`) **[V]** |
| 0x4321 handler (game side) | `FUN_00f12278` | stores the advertised host endpoint: pubIp→+0x19b40, pubPort→+0x19b52, privIp→+0x19b54, privPort→+0x19b66; nonzero result skips the body **[V]** |
| Endpoint getters (game side) | `0xf0ccf4` / `0xf0cd10` / `0xf0cd30` / `0xf0cd4c` | read pubIp/pubPort/privIp/privPort; all four called by the joiner's peer-session builder at `0xaa1460..0xaa1528` → `0x261fe0` **[V]** |
| Session init | `FUN_00268148` | sets session flags word (bits `0x100/0x800/0x400/0x2`; never `0x200`); starts the session by sending the handshake **[V]** |
| Integrity gate | `FUN_00fbc1fc` | selector `0x1f`, 4-byte outputs never transmitted; `rc == 0` decides the send; dispatches via runtime OPD `PTR_FUN_0119f550` **[U]** |
| LZSS compression | `FUN_00efe048` | compress (0x2000 in / 0x800 out); hash-chain `FUN_00efdc40` (head) / `FUN_00efd5d0` (per byte) **[V]** |
| **Receive loop** | `FUN_002620d8` | reads the persistent UDP socket (fd at ctx+4) via `recvfrom` (`FUN_00fbb59c`) into a `0x578`-byte buffer, src sockaddr at stack; matches src ip/port against each session's peer sockaddr (`session+0x2c..0x30`, 0x18 sessions × 0x6d8); for a match in state 2..6 calls the decoder then the **handshake sender** `FUN_00268ba8` (reply consumption); with no match falls through to the global accept session (state 2) and calls the handshake **receiver** `FUN_00267d58`; loops **[V]** |

### 9.2 Net import stubs (socket primitives)

| Stub | Role |
|---|---|
| `FUN_00fbb75c` | `socket(af, type, proto)` |
| `FUN_00fbb65c` | `connect` |
| `FUN_00fbb6dc` | `setsockopt` |
| `FUN_00fbb77c` | `shutdown` |
| `FUN_00fbb85c` | `send` |
| `FUN_00fbb87c` | `recv` (MSG_WAITALL) |
| `FUN_00fbb59c` | `recvfrom` (fd, buf, len, flags, src sockaddr, addrlen) — used by the receive loop `FUN_002620d8` |
| `FUN_00fbb67c` | `close` |
| `FUN_00fbb83c` / `FUN_00fbb69c` | name resolution |
| `FUN_00fbb79c` | inet-format (ip → dotted string) |

### 9.3 Key constants

| Constant | Value | Use |
|---|---|---|
| P2P port | `0x2bad` = 11181 | default bind port of the persistent socket **when port arg == 0**; not the join-dial source **[V]** |
| LCG multiplier | `0x5d588b65` (+1 increment) | keystream & header-scramble PRNG (75+ uses in image) **[V]** |
| Pre-handshake seed const | `0x87103c2f` | fixed XOR for handshake frames (session state < 8, §5.2/§6) **[V]** |
| Counter-base derivation const | `0x2b58de69` | `session+0xc = base ^ const` (§6) **[V]** |
| Compression gate flag | `0x200` in session flags word | LZSS on/off (§7) **[V]** |
| Compression marker | bit 7 (`0x80`) of header byte 1 | on-wire "compressed" marker (§7) **[V]** |
| Wire prefix tag | `0x1000` (u16 LE) at `[2..4)` | the handshake/one-shot message type (§6.2); constant across captures **[V]** |
| Message type high bits | `0x4000`/`0x8000` class bits, `0x2000` wide-len candidate — set in `0xc480`; exact meaning **[U]** | message type word (§6.2) |
| Writer / gate selectors | `0xa0020`, `0x1f`, `0x18` | one-shot writer arg / `FUN_00fbc1fc` selectors — meaning unresolved **[U]** |
| **`module_magic`** | **`0x4d258ab7`** | the one constant the handshake receiver validates (LE on the wire: `b7 8a 25 4d`) — wrong value = silent drop. Stored at `*(0x0122a5cc)` **[V]** |

---

## 10. Worked decode (live capture, frame 0 of the joiner's dials) [V]

Raw wire (44 bytes, as captured by the host-side dump):

```
2c 9e 2e 85 0c 87 2c 4c 2c 87 47 ff db c7 10 75 06 85 5c 8b 49 17 39 0a
59 dc db 1a 9a 4a 1a 11 78 9d f2 78 b8 c1 3e fb 30 c1 0a 51
```

After §5 unscramble + chain (hdr recovered = `0x0000`):

```
00 00 00 10 1c 00 02 00 00 00 77 b3 f7 40 b7 8a 25 4d 02 01 00 02
59 81 10 cb 62 16 c0 a8 01 32 62 16 f2 78 b8 c1 3e fb 30 c1 9e 51

hdr=0          type=0x1000 len=0x1c (28)     flags2=0x00
peer_id=2      base=0x40f7b377          magic=0x4d258ab7 ✓
ver=0x02       unk=0x0001               count=2
entry0=89.129.16.203:5730               entry1=192.168.1.50:5730
tail = §5.3 digest (varies per retry because the digest covers the hdr counter)
```

The 10 captures differ only in the hdr counter (0..9) and the tail digest — the
handshake body is byte-identical across retries, which is what allowed the layout above
to be pinned **[V]**.

---

## 11. Implementation guide — building a fake host / p2p server [V]

A working, verified implementation of everything below lives in
`mgo2-server/src/tasks/udp-dump.ts` (`deno task udp`) and has taken a real
MGO2/RPCS3 client from the first dial through the session-keyed data phase. The
crypto formulas are exact — copy them from §5.

### 11.1 Listen

- Bind UDP on the port the TCP `0x4321` reply advertises as the host endpoint (the
  fake host's `character_connections` row). `udp-dump.ts` defaults to 5731.
- **Never** bind 11181 (`0x2bad`) — the joining client binds it for its own p2p
  socket and a collision aborts its session init before any datagram is sent. Also
  avoid the client's own source port (5730).
- Advertise a **reachable LAN IP**: `0.0.0.0` fails the client's `sendto`;
  `127.0.0.1` is swallowed by the emulator's guest loopback. Working setups
  advertise the host machine's LAN IP (§2).

### 11.2 Receive + decode the joiner's handshake (44 bytes)

1. `work = raw`; `hdr = unscrambleHeaderOnly(work)` — §5.1 receiver order
   (swap, swap, xor, xor).
2. **Verify the tail digest with the constant key** `0x2b58de69` (§5.3) — the
   decoder runs this before anything else. Fails → drop; the joiner re-dials
   every ~1.9 s.
3. `xorChain(work, hdr, 0x87103c2f, decrypt)` — §5.2 pre-handshake key.
4. Parse §4: `peer_id = u32LE[6]`, `counter_base = u32LE[10]`,
   `magic = u32LE[14]` (= `0x4d258ab7`), `ver = [18]`, `unk = u16LE[19]`,
   `count = [21]`, endpoint pairs at `22 + 6i` (`ip[4]` BE, `port u16 LE`).
5. NAT override for LAN tests: pair0 is the joiner's public (WAN) endpoint —
   overwrite it with pair1 (private) so any dial-back reaches the LAN address
   (`P2P_OVERRIDE_PUBLIC`).

### 11.3 Reply — the host handshake

Plaintext 44 bytes:

```
[0..2)   hdr — from your peer's single outbound counter (§11.5)
[2..6)   00 10 1c 00        — message header: type 0x1000 u16 LE, len 0x1c, flags2 0
[6..10)  peer_id = the HOST's character id — NOT an echo of the joiner's!
[10..14) our counter_base
[14..18) b7 8a 25 4d        — module_magic 0x4d258ab7, LE on the wire
[18]     0x02               — ver (bit 2 must be clear)
[19..21) 01 00              — unk u16 LE
[21]     0x02               — count
[22..28) our endpoint {ip[4] BE, port u16 LE}   (public)
[28..34) our endpoint                            (private — same in a LAN test)
[34..44) tail digest (§5.3) — computed by the encoder, not free bytes
```

**peer_id is the #1 fake-host bug.** The joiner's gate compares the reply's
`peer_id` to the peer descriptor its dial session stored at creation — the HOST's
character id (1 for the seeded NPC). Echoing the joiner's id fails **silently**
(state stays 5, re-dial forever); a wrong magic would at least move the session
on (§4 gate 1). `P2P_ID` overrides the default 1.

Send back to the joiner's **source address**:

```
encodeFrame(plain, hdr, chainKey = 0x87103c2f, digestKey = 0x2b58de69)
    = xorChain(encrypt) → computeTailDigest → header scramble (sender order)
```

### 11.4 Establish the session key (state 6 → 8)

After the reply is accepted the joiner sits at state 6 and emits pre-keyed 16-byte
keep-alives (type `0x5000`, len 0) — mirror them back pre-keyed on the same
counter. Handshakes and keep-alives alone can NEVER move it to state 8: the only
path that sets its key flag is the decoder's second digest chance — a frame whose
**tail digest verifies with `K ^ 0x2b58de69`** flips `flags |= 9`, and the accept
pump sets state 8 when `(flags & 3) == 3` (§6.1). So send exactly one
session-keyed frame right after the reply:

```
K     = joiner_counter_base ^ our_base
wire  = encodeFrame(buildKeepaliveFrame(hdr), hdr,
                    chainKey = K, digestKey = K ^ 0x2b58de69)
      // 16-byte plaintext: hdr | 00 50 00 00 | (tail added by the encoder)
```

Verified live: the joiner stops re-dialing within one retry cycle and starts
streaming K-keyed frames (§6.2). Without this frame the join never leaves state 6.

### 11.5 Outbound discipline — ONE counter

All host→joiner frames — handshake replies, keep-alive mirrors, the establish
frame, data frames — draw from **one per-peer `outHdr`**, incremented by exactly 1
per frame. The joiner's seq window (`session[0x42]` + reorder bitmask, §5.2)
drops anything outside `last+1..last+0x20`; two independent counters interleave
into the window and are silently discarded. The joiner's own stream is the live
proof: hdrs `0x8002, 0x8003, 0x8004, 0x0005, 0x8006, …` — one counter, the
compression bit OR'd per frame (§6.2).

### 11.6 The data phase — expect and do

- Decode inbound with the session key: unscramble → verify tail with
  `K ^ 0x2b58de69` → `xorChain(K, decrypt)`.
- Parse messages per §6.2: `type u16 LE | len u8 | flags2 u8 | body`, concatenated
  up to the 10-byte tail.
- `0xc480` = reliable LZSS message (hdr bit `0x8000`; body starts with
  decompressed-size u16 BE then the LZSS stream, §7). Re-sent byte-identical until
  ACKed — answer it (ACK bytes **[U]**, id-match semantics **[I]**, §6.2).
- `0x1001` = control/window signaling (`len 1`, `flags2` 1..5 cycling).
- Answer with session-keyed frames on the shared counter; the joiner's messages
  after its reliable exchange is acked tell you what the room/host handshake
  wants next.

### 11.7 Minimal code sketch (TypeScript, from udp-dump.ts)

```ts
const lcg    = (x: number) => (Math.imul(x, 0x5d588b65) + 1) >>> 0;
const PRE    = 0x87103c2f;   // pre-handshake chain key
const DIGEST = 0x2b58de69;   // tail digest constant
const MAGIC  = 0x4d258ab7;
const ourBase = 0x12345678;
let outHdr = 0;              // ONE shared outbound counter per peer

// §5.1 receiver order
function unscrambleHeaderOnly(raw: Uint8Array): number { /* swap,swap,xor,xor */ }
// §5.2 plaintext-feedback LE32 chain over [2 .. len-0xa)
function xorChain(b: Uint8Array, hdr: number, keyed: number, encrypt: boolean): void {}
// §5.3 verify on the unscrambled pre-chain buffer
function verifyTailDigest(unscrambled: Uint8Array, key: number): boolean {}
// chain(encrypt) → tail digest → header scramble (sender order)
function encodeFrame(plain: Uint8Array, hdr: number, keyed: number, digestKey = DIGEST): Uint8Array {}

// per datagram:
const work = raw.slice();
const hdr  = unscrambleHeaderOnly(work);
if (!verifyTailDigest(work, DIGEST)) continue;      // gate first
xorChain(work, hdr, PRE, false);                    // decode
const hs = parseHandshake(work);                    // §4; null → not a handshake
// 1) reply:    encodeFrame(buildHandshake(outHdr, hs), outHdr++, PRE)
// 2) establish: K = hs.counterBase ^ ourBase;
//               encodeFrame(buildKeepaliveFrame(outHdr), outHdr++, K, K ^ DIGEST)
// 3) mirror pre-keyed keep-alives; then data-phase handling (§11.6)
```

Env knobs in `udp-dump.ts`: `P2P_ID` (host character id), `P2P_BASE`,
`P2P_HOST` (advertised IP), `P2P_OVERRIDE_PUBLIC`, `P2P_SEND_KEYED_FRAME`
(keepalive | data | keyed | never), `P2P_ESTABLISH`, `P2P_FRAME_TYPE`/
`P2P_FRAME_BODY`, `UDP_PORTS`, `UDP_HOSTNAME`.

---

## 12. Open questions [U]

1. **Resolved: the wire crypto.** The handshake is scrambled + XOR-chained (§5), not
   plaintext; the header is a LE u16 counter; the scramble positions come from
   `LCG(len)` (§5.1). Verified by decode→re-encode round-trips on 10 live frames.
2. **Resolved: the join flow** — TCP `0x4320/0x4321` advertises the host endpoint, the
   joiner dials it with the scrambled 44-byte handshake from its own p2p port (§2).
3. **Resolved: the datagram prefix.** On the wire the message is preceded by a 4-byte
   prefix (`0x1000` + u16 LE length), not the 12-byte envelope of the earlier static
   analysis (that is the internal queue-node header). The `0x1000` tag's meaning is
   still **[U]**.
4. **Resolved: the receive loop and the reply routing** — `FUN_002620d8` reads the
   persistent socket via `recvfrom` (`FUN_00fbb59c`) into a `0x578`-byte buffer,
   matches the source against each session's peer sockaddr, and routes a matched
   session (state 2..6) through the decoder → handshake **sender** queue-drain, or an
   unmatched datagram through the global accept session → handshake **receiver**
   `FUN_00267d58` (§4). Still open: the exact dispatch table between the decoder and
   per-type message handlers beyond the handshake.
5. Exact datagram template behind `FUN_00fa1af0` (format/state pointer is
   runtime-initialized).
6. `FUN_00fbc1fc`'s resolved target (`PTR_FUN_0119f550`) and the meaning of
   `0x1f`/`0x18`/`0xa0020`.
7. Whether **mid-match game-data datagrams** differ from the `0x1000` one-shots.
   **Resolved (2026-09-09, live):** yes — data-phase frames carry message type
   `0xc480` (reliable, LZSS-compressed) and `0x1001` (control), with the §6.2
   header `type u16 LE | len u8 | flags2 u8`; the `0x1000` tag is just the
   handshake/one-shot message type. **Resolved for the tail and the keyed key:**
   state-8 frames verify the §5.3 tail with `K ^ 0x2b58de69` and the §5.2 chain
   with `K = peer_base ^ own_base` — now confirmed live, not only from the
   decompile (§6.2). Still open: the LZSS decompressor port (§7), the ACK wire
   format for reliable `0xc480` messages, and the compressed message's game
   content.
8. Which datagram template/payload type carries **voice** — expected to share this
   datagram path as one payload type among many **[I]**.

---

## 13. Revision history — superseded readings [V]

Readings that were **replaced** by the live-verified facts above; kept for anyone
landing on old captures or old notes.

| Date | Superseded reading | Corrected to |
|---|---|---|
| 2026-09 | handshake travels **plaintext** | handshake is scrambled + XOR-chained like every frame (§4/§5) |
| 2026-09 | scramble positions from `LCG(len+10)`, `% (len+8)` | real positions in §5.1 (`LCG(len)`, `% (len-2)` / `% len`) |
| 2026-09-07 | keyed §5.3 digest key collapses to the bare constant | `session+0xc = peer_base ^ 0x2b58de69`; keyed digest key = `K ^ 0x2b58de69` — confirmed live 2026-09-09 (§5.3/§6) |
| 2026-09 | reply `peer_id` should **echo the joiner's id** | reply carries the **host's** character id; the joiner compares it to its stored peer descriptor (§4 gate 1) |
| 2026-09-09 | wire prefix = `u16 LE 0x1000` + `u16 LE` message length | message header `type u16 LE | len u8 | flags2 u8` (§3/§6.2) |
| 2026-09-09 | header sequence gate at `0x2668e4..0x266914`, window `> 0x100` | gate at `0x26712c..0x267198`; signed 16-bit diff `> 0x1f` (31) drops (§5.2) |
| 2026-09-09 | key selection reads the state byte at `session[5]` | state byte at `session+4` (§5.2/§6.1) |
| 2026-09-09 | flags word read at `session+5` (u16) | flags word at `session+0x14` (§6/§7) |