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
  accepted reply (state 6) the joiner keeps re-dialing and pinging **pre-keyed**. A
  host that sends **one session-keyed frame** (digest `K ^ 0x2b58de69`, chain `K`; an
  empty tag-`0x5000` keep-alive is the verified shape) exercises the handshake-phase
  second digest chance, which sets `flags |= 9` (§6.1). Note the `0x1` in that nine:
  §6.1's correction shows the accept path sets bit `0x1` itself, and the reference
  host sends no keyed frame, so this is a defence rather than a requirement. And **all** host→joiner frames must draw from ONE outbound hdr
  counter: the joiner's seq window (§5, `session[0x42]` + reorder bitmask) drops
  anything outside `last+1 .. last+0x20`, so separate reply/data counters interleave
  and get silently discarded **[V]** (verified in simulation: two-counter scheme
  produces duplicate hdrs → drops).
  **Superseded (see §6.1's correction):** the 2026-09-09 trial had the session-keyed
  ESTABLISH OUT go out beside the reply and the joiner then reach state 8 — the ~2 s
  handshake retries stopped and the first session-keyed data-phase frames were
  captured (§6.2). The acceptance is what flips the session; the reference capture in
  `P2P_CONNECT_FSM.md` §7.1 has the host send no keyed frame and the joiner keyed
  26 ms after the reply.
  > **A real host sends both opening frames pre-keyed.** The trial above used a
  > fake host, and a session-keyed frame is accepted. The dedicated server in
  > `docs/mgo2-game.pcapng` does not send one: its 16-byte `0x5000` keep-alive
  > and its 44-byte handshake reply verify with the bare constant
  > `0x2b58de69` and with `K ^ 0x2b58de69` at neither, while the joiner's first
  > session-keyed frame verifies only with `K ^ 0x2b58de69`. The joiner reaches
  > its keyed state on **accepting the handshake reply**, so a host that marks
  > the session established before sending it hands the joiner two frames it
  > drops at the §5.3 digest gate, and watches it re-dial for ~40 s. The capture
  > is the reference here; `AcceptHandshakeHandler` sets the flag after the
  > reply for that reason. See `UDP_GAME_CAPTURE.md` §4. **[V]**

---

## 3. Wire datagram format [V]

One datagram (any length `len`):

```
[ 0 .. 2)         scrambled header — unscrambles to a LE u16 counter (§5.1)
[ 2 .. 4)         message type (u16 LE) — `id (12 bits) | class (top nibble)` (§6.2);
                  0x1000 = handshake/one-shot, 0x1001 = reliable record, 0x5000 = keep-alive
                  id 0, 0x5001 = id 1 in the same class; frames with the compression marker
                  carry a raw LZSS stream from [2) instead of a header (§6.2/§7)
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
[18]        capability (u8; 2 in captures) — computed from the SENDER's own
            session flags word (session+0x14) and can only ever be 2, 3, 6 or 7;
            the peer folds it back into its own flags on receipt. NOT a version:
            the send path contains no version number — see UDP_JOIN_FLOW.md §4
[19 ..21)   u16 LE, taken from the peer struct at +6 (2 joiner / 1 host in
            captures); meaning unresolved [U]
[21]        count (u8, 0..2; 2 in captures)
[22 ..28)   entry 0 { ip[4], port u16 LE }
[28 ..34)   entry 1
[34 ..44)   tail — self-verifying MD5 digest (§5.3); NOT free bytes — a fake
            host must compute it or the decoder drops the datagram
```

- **Byte order is mixed**: `peer_id`/`counter_base`/`module_magic`/the u16 at `[19..21)`/`port`
  are **LE** (reversed-copy helpers `FUN_0026cc10`/`FUN_0026cc88`, which walk the cursor
  *backwards* from `src+len-1`), the entry `ip` u32s are
  **BE** (plain-copy helpers `FUN_0026cb98`/`FUN_0026cb20`, a forward copy); the
  capability byte and `count` are single bytes. Verified via the receiver's magic compare —
  wire bytes for `0x4d258ab7` are `b7 8a 25 4d`, and against a live capture's bytes at
  every offset — see `UDP_JOIN_FLOW.md` §3 **[V]**.
- **Entries = the sender's OWN endpoints** (public + private): a live capture showed
  `89.129.16.203:5730` (public) and `192.168.1.50:5730` (private). The host replies to
  the joiner's source address and uses these entries to dial the joiner back **[V]**.
  - **Behind a load balancer the source address is not the joiner.** The entries are
    the only statement of where the joiner is listening, and the source is a
    connection-tracking address on a port the joiner's socket is not reading. A host
    that answers the source sends every reply into a translation table entry the
    joiner never reads, and the session dies silently after the handshake. The host
    therefore answers the first advertised pair and falls back to the source only
    when the joiner advertises nothing usable. The same is true in the other
    direction: a host behind a balancer must put its **configured** advertised
    address in its own handshake, because the address its datagrams arrive from is
    the balancer's and is not dialable. `ServerOptions.GameplayServerAdvertisedAddress`
    is the single source for both, so the address a joining client is handed in the
    join result and the address a peer is told to answer on cannot drift apart
    **[V]**.
  - **Answering the advertised pair does not make the session reachable from it.** A
    session is filed under the endpoint the handshake *arrived from*, and the frames
    that follow arrive from the endpoint the joiner was *answered at*. Those are the
    same pair for a joiner out on the internet, where both name its NATed address,
    and different wherever something rewrites the source in between — a load
    balancer, or a router that loops a reply addressed to its own public address back
    into the LAN it shares with the host. A lookup by the observed source alone then
    misses, and because the tail digest cannot be verified without the session key
    the frame is discarded: a live trial logged `answering 89.129.16.203:5730`
    followed by nothing but `Undecodable datagram from 89.129.16.203:5730`, with the
    session reported established throughout. The lookup therefore falls back to a
    session that dials back to the arriving endpoint, which is what makes both
    directions work at once where answering only one of them does not **[V]**.
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
`mgo2-server`'s `DedicatedHostService` (accept-handshake handler) does, verified
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
   of the joiner's id. Gate check at `0x268ef4`
   (`lwz session+0x18; cmpw; beq 0x268f58 / li r27,0 skip-side`).
   **A peer_id mismatch fails SILENTLY** — the record is skipped, state stays 5, the
   peer base is never stored, and the joiner keeps re-dialing every ~1.9 s — while a
   wrong `module_magic` would instead move the session to state 6. That asymmetry is
   diagnostic live: continued re-dialing after byte-perfect replies means peer_id,
   not magic, is the failing gate. This was the real fake-host blocker: echoing
   the joiner's id (the tempting, wrong reading) fails this gate silently.
2. **`module_magic` (message[8..12)) == `0x4d258ab7`** (`0x268fd8`:
   `lwz` of the magic global, `cmpw` against the message word; mismatch → `0x2690d0`,
   which stores **state 6** at `0x2690d8` and calls `0x26b1c8`).
3. **The capability byte's bit 2, against our own flags bit `0x800`** (`0x269008`–`0x269020`:
   read one byte, test `& 0x4`; if clear, test `session+0x14 & 0x800`; `bne-` to the same
   failure path as gate 2). The condition is a **disjunction**: reject only when the peer's
   bit 2 is clear *and* our `0x800` is set. It is symmetric with the builder, which sets
   that bit exactly when our own `0x800` is set — so it is a negotiated capability being
   checked for agreement, not a version gate. **[V]**

   > The mask rule these two tests depend on is in `UDP_JOIN_FLOW.md` §0. Read the other
   > way round, `MB`/`ME` come out as bits 29 and 20 against a zero-extended `lhz` and the
   > branch is unreachable; complemented they are `0x4` and `0x800`, which the builder
   > confirms independently.

   **The field at `[18]` is therefore not a version.** It is computed from the sender's own
   flags word on the way out (`2`, or `3` when `flags & 0x100`, with bit 2 set when
   `flags & 0x800`) and folded back into the receiver's flags on the way in. "ver" was the
   wrong name for it throughout this file. **[V]**

   Note what this means for a peer: a host that rejects every handshake whose capability
   has bit 2 set rejects peers a real host answers. In the capture the three flows that
   never establish both sent `0x06` — bit 2 set — and were still answered. **[V]**
4. On acceptance: session flags |= `0x2|0x4` (`0x268f6c`, `0x268fb0`), the peer's
   capability byte is OR'd back into `session+0x14` (`0x269064`), peer `counter_base` →
   `session[8]` and `session[0xc] = base ^ 0x2b58de69` (`0x268c30`–`0x268c40`), and the
   joiner re-sends its own handshake (the ping-pong); subsequent frames use the keyed
   seed (§6). `session[0xc]` is stored **pre-XORed** — that is why it exists, and it makes
   the tail-digest key one XOR away. **[V]**

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
| `session+0xc` | `peer_base ^ 0x2b58de69` (derived) — stored **pre-XORed** so that the tail-digest key is one XOR away (§6): the accept path writes `+8 = base` then `+0xc = base ^ 0x2b58de69` back-to-back at `0x268c38`/`0x268c40` |
| `session+0x14` | flags (u16): role bits `0x1`/`0x2` (state-8 gate needs both), `0x4` reply accepted, `0x8` session key established (`flags \|= 9` path, §6.1), `0x100`/`0x400`/`0x800` session-init bits, `0x200` LZSS gate (§7). The peer's handshake `[18]` byte is OR'd into this word on acceptance (`0x269064`), so it is partly negotiated rather than purely local — see `UDP_JOIN_FLOW.md` §4 **[I]** |
| `session+0x18..+0x2b` | peer descriptor (5 words; `peer_id` at `+0x18` — §4 gate 1) |
| `session+0x2c..0x30` | peer sockaddr — source ip/port match in the receive loop (§9.1) |
| `session+0x42` | header seq (u16) — expected incoming hdr counter (§5.2 gate) |
| `session+0x44` | sliding-window reorder bitmask (u32, §5.2 gate) |
| `session+0x48` | key slot: `-1` = pre-keyed; else session-keyed (decoder `FUN_002666c8` gate) |

The decoder computes the post-handshake **chain** key as `K = session[+8] ^ session[+0x10]`
(`xor r16, r9, r0` over ctx+8 / ctx+0x10 at `0x266b7c` in `FUN_002666c8`). Both directions
use the **same value** (`peer_base ^ own_base`), so the session is symmetric — no
per-direction keying **[V]**.

> **There are two keys, not one — and the second one is easy to miss.** The **tail
> digest** key is a *separate* XOR in the same function: `session[+0xc] ^ session[+0x10]`
> at `0x26695c`, which is `K ^ 0x2b58de69` precisely because `session[+0xc]` was stored
> pre-XORed (§4 gate 4). The pre-keyed path uses the bare constant `0x2b58de69` as the
> digest key (`lis r0,0x2b58` / `ori r0,r0,0xde69` at `0x267010`) against the pre-keyed
> **chain** key `0x87103c2f`. So a frame has two independently-derived keys, and the
> offline decoder's `digest_key_for()` is the offline twin of that second site, not of
> the first. **[V]**, confirmed on a live capture by decoding every opening frame against
> all four candidate keys — frames 1–3 verify only with `0x87103c2f`, frames 4+ only with
> `K`, and none verify under two (`UDP_JOIN_FLOW.md` §6).

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

State machine (u8 at `session+4`). Store sites re-verified in the accept path:
**2** at `0x2690c8`, **3** at `0x268f50`, **6** at `0x2690d8`, **8** at
`0x268c58`.

> **Corrected.** This list previously read "5→6 acceptance (`0x268e00`;
> magic-mismatch also lands at 6, `0x269524`)" and put the state-8 store at
> `0x268ffc`. Neither address holds the store it was credited with. What the
> code does: **acceptance does not go through state 6.** State 6 is written at
> `0x2690d8`, which is the *failure* target of both the `module_magic` gate and
> the capability gate (`0x268fdc`/`0x269020` branch to `0x2690d0`, which stores 6
> and calls `0x26b1c8`). Success instead sets `flags |= 0x4` then `flags |= 0x2`
> (`0x268f6c`, `0x268fb0`), and state 8 is reached independently at `0x268c58`
> once `(flags & 0x3) == 3` — both role bits — per the test at `0x268c44`.

States 12 and 0 are not re-checked here and their earlier addresses stand unverified.

**How a dial session reaches state 8 — resolved from the decoder decompile
(2026-09-08):** the handshake-phase decode path has a **second digest chance**:
if the tail fails the constant key it retries with `K ^ 0x2b58de69`, and onsuccess sets `session flags |= 9` (bit `0x8` = "session key established" + bit `0x1`)
— `target_002666c8…c` lines 176–194. This is the only path that ever sets bit
`0x8` — the pre-keyed paths never do. Practical proof: the joiner's state-6
keep-alives (below) are pre-keyed, i.e. it had not established the key from the
handshake reply alone.

> **Corrected — bit `0x1` has a second source, and the reference host relies on
> it.** The reply's drain sets `flags |= 4` (`0x268f6c`) and `flags |= 2`
> (`0x268fb0`), rejoins the state dispatch (`0x269078: b 0x268c04`), and there
> sets `flags |= 1` (`0x26907c`–`0x26908c`: `ori r0, r11, 1` →
> `sth r0, 0x14(r28)`) before storing state 8 at `0x268c58`. So acceptance alone
> satisfies `(flags & 3) == 3` in one call, and the claim that *only* a
> session-keyed frame can move a dial session to state 8 is wrong. Measuring the reference join in
> `docs/mgo2-game.pcapng` settles it: the dedicated host sends exactly two
> frames, both pre-keyed (counters 0 and 1), its next frame is the keyed roster
> run 2.5 s later, and the joiner is already sending session-keyed frames 26 ms
> after the reply. `P2P_CONNECT_FSM.md` §7.1 has the frame-by-frame table. The
> fake-host trial recorded below still observed what it observed — the re-dial
> cadence stopped on the keyed frame — but the capture says the mechanism is the
> accepted reply. **The handshake-phase keyed frame is therefore optional, not
> required**; only a live run with it removed can say which the client needs. **[V]**

**Confirmed live 2026-09-09:** the fake host sent exactly one session-keyed empty
`tag-0x5000` keep-alive (ESTABLISH OUT) after its handshake reply; the joiner's
~2 s handshake re-dials stopped within one retry cycle and its next frames were
session-keyed. The key-establishment path is not just in the decompile — it works
on a real client. What the joiner sends next is documented in §6.2.

**The real host does not do this**, and the difference is the whole point. In
`docs/mgo2-game.pcapng` the dedicated server's keep-alive and handshake reply are
**both pre-keyed** — measured over every derivation of the two counter bases and
the two constants, they verify with the bare constant `0x2b58de69` and with
`K ^ 0x2b58de69` at neither (`UDP_GAME_CAPTURE.md` §4). So the state-<8 → pre-key
rule above is absolute and covers the opening exchange: the joiner cannot read a
session-keyed frame until it has accepted a handshake reply, and it accepts that
reply only after reading it. A host that marks its session established before
sending the reply therefore gets both frames dropped at the digest gate
(`0x266918`), before a field is parsed, and the joiner re-dials until it gives up. **[V]**

**Decryption (offline, no brute force):** decode the peer's handshake (§5) for its
`counter_base` → `peer_base`; own base is what we sent. Then per data frame: undo the
header scramble (§5.1), recover `hdr`, compute
`seed = (peer_base ^ own_base) ^ ((hdr & 0x7fff) * 0x5d588b65 + 1)`, and replay the
plaintext-feedback chain over `[2 .. len-0xa)` (§5.2). No secret, no brute force.

The heartbeat keeps the same class but changes id once the session is keyed: the
state-6 keep-alive above is `0x5000` (id 0), and `0x5001` is id 1 in that class.
Nothing in the decoder answers either one: an id-0 body of 0..3 bytes is skipped
outright (`0x267b84`) and there is no id-1 branch at all. **[V]** What `0x5001`
itself carries is unresolved and is **not** answered — see §6.2's table and
[`UDP_GAME_CAPTURE.md`](UDP_GAME_CAPTURE.md) §3, where a live capture shows it with
a monotonic fourth byte rather than the shape of a heartbeat.

### 6.2 The data phase (state ≥ 8) — live captures [V]

First session-keyed frames captured **2026-09-09**, immediately after the fake
host's reply was accepted and the dial session reached state 8 (§6.1, and its
correction: the acceptance flipped it, not the keep-alive that went out beside
it). All
decode with the §5.2 chain key `K = peer_base ^ own_base` and verify the §5.3
tail digest with `K ^ 0x2b58de69` — the keyed path, live. Capture session:
`K = 0xbbe4ff9c = 0xa9d0a9e4 (joiner base) ^ 0x12345678 (host base)`.

**Message wire format** (serializer `FUN_00269860`, the same header shape as the
handshake's `00 10 1c 00`):

```
[0..2)    hdr u16 LE — [bit 0x8000 = LZSS-compressed][bits 14-0 = a monotonic
                    per-peer frame counter].  Not a set of flags: bits 12-14 are
                    counter bits and change only as the counter carries past
                    4096/8192/12288/16384 (UDP_GAME_CAPTURE.md §2). The counter
                    is 15 bits, so it must wrap at 0x7fff: letting it reach
                    0x8000 marks a plaintext frame as compressed. **[V]**
[2..4)    message type u16 LE        (class bits 0x4000/0x8000 live in this word)
[4]       len u8 — body length
[5]       flags2 u8 — per-message flags byte
[6..6+len) body
[6+len..)  further messages, concatenated, then the 10-byte §5.3 tail
```

The §3 "u16 LE message length" reading is really `len u8 + flags2 u8` — it only
read as one u16 because handshake lens are < 256. (The dump tool's `msgLen` line
for data frames is likewise the `len|flags2` merge — ignore it; parse per this
layout.) **This header only exists on UNCOMPRESSED frames**: when the hdr
`0x8000` marker is set, the frame's `[2 .. len-0xa)` region is a raw LZSS stream
instead (§7), and the message header(s) materialize only after decompression.

**The type word is `id | class`, not one opaque number** (serializer
`FUN_00269860`, `0x269860`..`0x269a2c`) **[V]**. The serializer takes the low
twelve bits of the message's own first word as the **id**, then ORs in the
**class** bits from the message's flags byte at `+9` and from its length:

| source | type bit |
|---|---|
| `flags & 0x01` | `0x1000` reliable (`0x2698bc`) |
| `flags & 0x02` | `0x4000` (`0x269a18`; with `0x1000` gives `0x5000`) |
| body length `> 0xff` | `0x2000` long form (`0x2698e8`) |
| `flags & 0x20` | `0x8000` LZSS-compressed (`0x2699b4`) |

**The first row does not reproduce the live wire.** On the dedicated server's
game, bit 0 of a record's fourth header byte agrees with bit 12 of its type on
only **44.8%** of 152 668 records — barely better than chance. On that wire the
fourth byte is a **running counter**: 13 distinct values (0–21) across tick
records, 202 (0–201) across session records, climbing monotonically over a
round. Either the dedicated server is a different build from the one
`FUN_00269860` was read in, or the flags byte is a field this reading is not
looking at. The `id (12 bits) | class (top nibble)` shape itself holds. **[V]**

So every tag on this channel reads as `id (12 bits) | class (top nibble)`, and
the two halves move independently: id 1 appears in three classes (`0x1001`
reliable, `0x9001` reliable+compressed, `0x5001` reliable+`0x4000`), and the
`0x5000` class carries two ids (`0x5000` id 0, `0x5001` id 1). Whether the class
selects a handler is **[U]**. An earlier reading here said the receiver's per-id lookup
(`0x2614f8`) "compares `word & 0x7f00fff`, which drops bits 12-15". That is withdrawn on
two counts. The mask constant is `0x7f000fff`, not `0x7f00fff`:

```
00261568: lis   r7, 0x7f00
00261578: ori   r7, r7, 0xfff      ; r7 = 0x7f000fff
00261550: clrlwi r9, r8, 0x10      ; incoming word & 0xFFFF
00261544: addi  r4, r9, 0x18       ; 0x18 + (slot matched ? 1 : 0)
00261554: slwi  r0, r4, 0x18
0026155c: or    r6, r0, r9         ; r6 = (r4 << 24) | (word & 0xFFFF)
002615a8: lwz   r3, 0(r3)
002615ac: and   r0, r3, r7         ; entry id & 0x7f000fff
002615b0: cmpw  r0, r6
```

and the class bits are **relocated** into bits 24–27 rather than dropped. Taken
literally this could not dispatch `0x5001` at all, whose class nibble is non-zero — so
`r3` is evidently not the raw type word at that point and the function's real selector is
not established. The observation that stands is the one from the capture: `0x5000` and
`0x5001` behave as two distinct commands rather than a value and its sequence number
**[V]**; the mechanism that separates them is **[U]**.

> **Correction, from a live dedicated-server game.** The layout above is the
> **session-record** framing and it
> is **not the only one on this wire**. A record whose identifier is **below `0x1000`** is
> an in-game tick record and its `len` byte counts the **body plus the `flags2`/class byte**
> — `len = data + 1`, exactly the framing `tools/mgo2_replay_parser.py` and §6.6 below
> already walk. Reading a tick record the session way over-reads it by one byte and
> desynchronises the rest of the content region. Measured over the 19 018
> digest-verified frames of that game: session framing
> throughout walks **291** frames to their last byte, tick framing throughout **17 423**,
> and choosing by identifier **18 728**. See
> [`UDP_GAME_CAPTURE.md`](UDP_GAME_CAPTURE.md) §2 and `UDP_COMMANDS.md`,
> "Record framing". Implemented in `MessageCodecUtility.ReadBodyLength`.

**Observed message types (post-decode):**

- **`0x1001` — the data-phase record type.** Two shapes in the capture:
  - **Compressed (89-byte frames, hdr `0x8002`…):** the LZSS stream
    (`plain[2 .. len-0xa)`, 77 bytes) decompresses to **94 bytes = 4 + 0x5a**
    (header + declared length, exactly) ending at the LZSS EOF marker — one
    `type 0x1001, len 0x5a` record whose body is the joiner's **player
    profile**: `02 00` (peer_id 2 = the joiner's character id), 12 zero bytes,
    status bytes `0d 00 a7 00 1e`, 6 bytes `45 2f 57 39 67 68`, small integer
    fields, then the NUL-terminated **account name `phildunphy23`** at offset
    `0x51`. Key offsets:
    `0000 01 10 5a 00 02 00 …` / `0051 70 68 69 6c 64 75 6e 70 68 79 32 33 00`.
    (An earlier decoding pass produced `phildun\0y23` — that was the wrong LZSS
    ring-index/length-bias model, not a string-table split; the corrected §7
    model resolves the real name byte-for-byte. The name is pure ASCII, so
    ISO-8859-1 vs UTF-8 was never a factor.)
    The **identical stream is re-sent on every subsequent compressed frame**
    (hdr `0x8003`, `0x8004`, `0x8006`, `0x8008`…) — a reliable record awaiting
    its ACK (§6.3).
    (The previously reported "type `0xc480`, len `0x2b`, flags2 `0x50`" was a
    misreading of stream bytes as a wire header: no `0xc480` immediate exists
    anywhere in the image, and `08 14` is stream data, not a decompressed-size
    prefix — 43 stream bytes can hold at most ~338 output bytes under this
    format, never 2068.)
  - **Uncompressed (17-byte frames, marker clear):** hdr `0x0005`:
    `05 00 | 01 10 | 01 | 01 | 00 | <10B tail>` = type `0x1001`, len 1,
    flags2 1, body `0x00`. These are the joiner's **ACKs of the host's
    frames** (§6.3) — the flags2 value is the send attempt (1..5 escalating on
    the re-sends that pile up while the host stays silent).

**Observed message types at a glance:**

| marker | decoded content | id | class | role |
|---|---|---|---|---|
| — | `0x1000`, len `0x1c`, flags2 `0x00` | 0 | `0x1000` | handshake (§4) |
| — | `0x5000`, len `0x00` | 0 | `0x5000` | keep-alive (§6.1) |
| — | `0x5001`, len `0x00` | 1 | `0x5000` | **[U]** unresolved; a live capture refutes the keep-alive reading (§6.1, and the capture §3) |
| `0x8000` | `0x1001`, len `0x5a` (LZSS; 94-byte profile record) | 1 | `0x9000` | reliable game data — joiner's player profile (above) |
| — | `0x1001`, len `0x01`, flags2 `0x01..0x05` | 1 | `0x1000` | ACK of the host's frame seq 1 (§6.3; flags2 = attempt) |

**`0x5001` — [U], and not mirrored.** An earlier reading called this the keyed
data-phase keep-alive and had the host echo it. A live capture refutes that: over
all 541 records of the type in one round the identifier never varies, the fourth
byte climbs monotonically to 194 on the joiner and 147 on the server without ever
resetting, the body is always empty, and the *server* sends 60 of them as well as
receiving them. A keep-alive has nothing to count and no one side to mirror from.
It is registered as a recognised in-game control record and not answered; it is
only *sent*, at the head of a roster run, because the live host sends it there.
See [`UDP_GAME_CAPTURE.md`](UDP_GAME_CAPTURE.md) §3.

**Shared counter confirmed live.** The joiner's hdrs over the run form one
monotonic sequence `0x8002, 0x8003, 0x8004, 0x0005, 0x8006, 0x0007, 0x8008, 0x0009,
0x800a, 0x000b, 0x000c, 0x800d, 0x000e, 0x000f, 0x8010, … 0x0023` — every frame,
compressed or control, increments the same counter by exactly 1 (it starts at 2:
hdr 0 = handshake dial, 1 = keep-alive). The `0x8000` marker is per-frame and is
masked out of the §5.2 seed by `hdr & 0x7fff`. This is the live proof for §2's
one-outbound-counter rule: a host must share one counter or its frames fall
outside the joiner's `last+1..last+0x20` seq window and are dropped.

### 6.3 The ACK mechanism — reliable frames [V]

Resolved 2026-09-10 from the binary (decoder enqueue path `0x266d48..0x266e04`,
receiver pump `FUN_0026e790`, serializer `FUN_00269860`) and the capture.

The serializer's "type" halfword is `(id & 0xfff) | class-bits` (`0x1000` =
reliable class, `0x2000` = long-len, set when the body exceeds 0xff) — the
`0x1001` record is reliable-class id 1. A frame is acknowledged with a **single
entry message**:

```
type   = 0x1000 | (ackedSeq & 0xfff)   u16 LE   (reliable-class, acked frame seq)
len    = 0x01
flags2 = send attempt (starts at 1, escalates per re-send)   ← does not hold, below
body   = 0x00
```

Sent as an ordinary session-keyed frame drawing the host's shared outbound
counter. Wire evidence from the capture: the joiner's 17-byte control frames
(`05 00 | 01 10 | 01 01 00`) are its ACK of **our seq 1** (the establish +
keep-alive mirror both drew seq 1) — cumulative, one ACK covers both.

**The escalating `flags2` does not hold.** The round's 27 acknowledgements carry
fourth bytes of `15, 18, 24, 25, 29, 31, 34, 38, 40, 45, 46, 47, 48, 50, 61, 99,
106, 113, 114, 116, 125, 132, 138` — scattered through the same running counter
the session records use, not a per-ack retry stamp that climbs 1→5. **[V]**

Binary evidence:

- **Decoder enqueue (`0x266e00`):** every accepted keyed frame writes the
  received hdr counter into an outbound ACK envelope template
  (`session+0x3c+4`) — the acked seq is the received frame's hdr.
- **Serializer (`0x26607c`):** reliable outbound envelopes get `session+0x88`
  (a per-message attempt/retry stamp) written into the envelope's `[4..6)`
  field, and `ori 0x20` (`0x2655c0`) marks re-sent envelopes — flags2 is the
  attempt count.
- **Receiver pump (`FUN_0026e790`):** matches inbound ACK entries against the
  session's pending (offset, size) message slots — first byte = slot id,
  envelope[4] = start, `[0xc]` = end — and frees them (`bl 0x27a180`); the
  duplicate gate `0xbcb & 1` collapses re-sent acks. Acks of acks are consumed,
  never answered — the host must not ACK a frame whose only content is ACK
  entries (verified in the replay: no ping-pong).
- **Parser (`FUN_0026a0e8`):** walks the frame content as
  `type u16 LE | len u8 | flags2 u8 | body` — the §6.2 layout — and registers
  (offset, size) slots per message.

**Re-read against `MGO2.ELF`, and what that settles.** The four addresses are
in the image this ELF covers and were disassembled to check rather than taken on
trust. Three hold exactly as written, and one claim does not. **[V]**

| claim | disassembly |
| --- | --- |
| `0x266e00` stamps the received counter into the ack template | `lwz r9,0x3c(session)` / `lhz r0,0x293e(r1)` / `stw r0,4(r9)` — confirmed |
| `0x26607c` writes `session+0x88` into the envelope's `[4..6)` | `lwz r0,0x88(session)` / `sth r0,4(r31)` — confirmed |
| pending message slots, matched per acknowledgement | `0x266cd8`–`0x266d44` is a **lowest-set-bit scan** of a 32-bit mask at `session+0x44` (`srwi`/`clrlwi` loop) — a slot allocator, as described |
| an acknowledgement is gated on the record being reliable-class | **refuted** |

**There is no bit-12 test anywhere in the binary.** `rlwinm rX, rX, 0, 0xc, 0xc`
— keep bit 12 and nothing else — has **zero** occurrences across 18.3 MB, and no
`andi`-form equivalent of it either. The enqueue at `0x266e00` sits
*unconditionally* in the accepted-frame path, so the client **prepares** an ack
for every frame it takes, and whatever decides whether to send it is downstream
and is not a class test.

That matches the capture: 492 inbound frames, 27 acknowledgements, every one of
them naming sequence 1. A per-frame "ack this because it was reliable" policy is
not what this code does. The gate is **[U]**; a window or a
retransmit-while-unconfirmed loop fits the observed pattern, and the 27 identical
sequence values favour the latter.

**What the protocol does.** These are the rules a host implementing this channel
follows. **mgo2-server implements none of 1–3** — see *What this server does*,
below, which is why they are kept separate rather than merged.

1. ACK each newly observed inbound seq (`hdr & 0x7fff`) exactly once —
   type `0x1000 | seq`, len 1, flags2 1, body `[0]` — on the shared counter.
   **But note the limit:** the header counter is fifteen bits (§6.2) and this
   type carries only twelve, so the acknowledgement is ambiguous for any
   sequence at or above `0x1000`. In a live round the counter passed 18 665 and
   sequence `1` named five different frames (1, 4097, 8193, 12289, 16385). The
   capture's own 27 acknowledgements all named sequence 1
   (`UDP_GAME_CAPTURE.md` §3). **[V]**
2. Cumulative: the joiner's single `0x1001` covers both host seqs 0 and 1;
   duplicate frames (re-sends of an acked record, seq ≤ lastInSeq) are not
   re-acked.
3. Never ack a frame whose only message is an ACK entry (no ack-of-ack).
4. Keep ONE outbound counter for data frames and acks alike (§2/§6.2).

**What this server does instead.** Rules 1–3 are deliberately not implemented on
the gameplay server, and rule 4 holds for the same reason — it has a single
outbound counter, it simply never draws from it for an acknowledgement. **[V]**

- **No acknowledgements are sent at all.** The recorded host received 492 frames
  in a live round and acknowledged none, and nothing in the binary gates one on
  the reliable class. Each acknowledgement would also spend an outbound
  sequence, shifting every later sequence number away from the capture's.
- **Unreliable tick records are relayed, not simulated.** The gameplay dispatcher
  forwards inbound records below `0x1000` to the other established peers, keeping
  their type, fourth header byte, body and source compression choice. It forwards
  unknown tick identifiers too: the capture has 121 such identifiers and the
  server only decodes a small subset. The capture shows tick traffic flowing in
  both directions, but does not prove that this relay is sufficient to run a
  round or that the host should synthesize gameplay state; see
  `UDP_GAME_CAPTURE.md` §6.
- **Inbound acknowledgements are still recognised**, by a stricter test than the
  class bit alone: `(type & 0xf000) == 0x1000`, a one-byte body, and body `0x00`.
  They are logged and deduplicated, and never answered.

If a client ever turns out to need acknowledgements to advance, this is the first
thing to change: `UdpCommandConstants.AcknowledgementTypeOf` already builds the
type correctly, masking the sequence to the twelve bits it can carry. **[V]**

mgo2-server implements this in `DedicatedHostService.ackInbound()`
(`src/infrastructure/udp/services/dedicated-host-service.ts`).

**Where the join stands:** the transport join is complete — state 8, session key
established, the joiner streaming K-keyed frames — and the LZSS layer is now
decoded (§7): the repeated compressed frame carries the joiner's `0x1001`
player-profile record (§6.2). **Resolved (2026-09-10): the ACK wire format**
(§6.3). **Resolved (2026-09-29): what the host has to answer with** (§6.5) —
the room roster, host entry at index -1 and the joining players after it, not a
single record.

### 6.4 The `0x1001` player-profile record — layout [V]

The profile record, read off the records a real host wrote into a recorded
survival match (`tools/replays/replay_360827_5.dat`). Twelve records were
recovered from the roster block at file offsets `0x52`..`0x484`; they sit
back to back, each carrying its own player's name, and parse to exactly their
declared length under the layout below — which is what pins the offsets: a
field one byte out shifts the name and the arithmetic stops adding up. **[V]**

Body, little-endian:

```
[0x00] u8        0x07 in every captured record
[0x01] u8        record sub-type: 0x48 (or 0x47) on a player's entry, 0x4c on
                 the room's own record
[0x02] u16       zero
[0x04] u8        0xe2 + roster index, or 0x00 for the host
[0x05] u8        per-player value, repeated at 0x07
[0x06] u8        zero
[0x07] u8        the same per-player value as 0x05
[0x08] u32       character id — the byte at 0x0a is its high byte, not a field
[0x0b..0x42]     per-player block; two IPv4 handshake endpoint pairs are at
                 0x13..0x1e (four address octets + little-endian port per pair) —
                 see UDP_GAME_CAPTURE.md §4; the 140-byte join profile does not
                 contain them at those offsets
[0x43] u8        0x03 when a clan name follows, 0x00 when none does
[0x44] char[16]  character name, NUL-padded — ISO-8859-1, the encoding the TCP
         char[]   character list uses for the same field (`0x3049`'s
                  `selected_name`), not UTF-8 and not the console's own code page
         char[]   clan name, running to the end of the record
```

Three things are worth stating because they are easy to get wrong:

- **The host is not on the players' count at all.** It sits at roster index
  **-1**, and it writes a plain `0x00` at offset 4 — not `0xe2 - 1`, which
  would be `0xe1`. The joining players fill 0, 1, 2 and so on in the order they
  arrive, written `0xe2`, `0xe3`, `0xe4`… So reading the index as
  `field - 0xe2` puts the host at -226 and every other reading puts it
  somewhere else; zero is the host. **[V]**
- **Offsets 5 and 7 are a per-player value, not the index.** The same byte
  appears at both, and across a live roster it runs `0x00`, `0x14`, `0x0f`,
  `0x04`, `0x0b`, `0x06` for indices -1 to 4 — which follows no order. Its
  meaning is **[U]** and the builder writes zero. **[V]** that it is not an
  index, **[U]** as to what it is.
- **The name is NUL-terminated; the clan name is not**, because the record ends
  there. Every record ends exactly on the last clan byte. **[V]**

> **Superseded by the live capture.** This layout was first read off a recorded
> replay (`tools/replays/replay_360827_5.dat`) and put offset 4 at
> `0x32 + roster index` and offsets 5/7 at `6 + roster index`, with the host at
> `0x31`/`0x05`. A live dedicated-server game
> (`docs/mgo2-game.pcapng`, written up in [`UDP_GAME_CAPTURE.md`](UDP_GAME_CAPTURE.md)
> §4) contradicts all three: `0xe2 + index`, a zero for the host, and
> per-player values at 5/7. The replay reading is kept above only as the
> history of where the error came from; **the live capture is the reference**
> and the builder, the parser and the tests follow it. **[V]**
The record a joining client opens the exchange with has the same shape but a
different leading byte: the live capture in
`docs/protocol/UDP_SERVER_LOG.txt` carries `0x02` where a roster record carries
`0x07`. It is a request to join, not a roster entry, so the host never answers
with one. **[V]**

**[U] Unresolved:** the per-player block at `0x0b..0x42` and the value at
`0x08`. Neither is opaque, though. Column by column over the twelve recorded
records of the in-repo match:

- **Eight columns differ per player** — `0x10`, `0x1f`, `0x20`, `0x23`, `0x2d`,
  `0x2e`, `0x37`, `0x39`. What the host puts in them is unresolved, so the
  builder writes zeros and a test says so. **[V]**
- **One column is constant and not zero** — `0x12` reads `0x02` in all twelve.
  The builder writes it. **[V]**
- **The other forty-seven are zero in all twelve.** The builder writes zeros,
  which is therefore correct rather than a placeholder. **[V]**

Widening the same count to **sixty records across five matches on four maps**
(§6.7) leaves `0x12 = 0x02` holding — it is still the only non-zero constant —
and widens the per-player set to **eleven** columns: the eight above plus
`0x26`, `0x31` and `0x3b`. So the block is eleven per-player columns, one
constant and forty-four zeros, on the wider evidence. **[V]**

So the builder reproduces every byte of all twelve in-repo records apart from
the eight per-player columns it cannot supply. The
value at `0x08` differs for every player and stays unresolved; the builder
writes `0`.

The columns at `0x1f`/`0x20` read as a per-player score — `0x1c21` for the host
down to `0x00ae` for the player who left early — but nothing cross-checks that
against the match result, so it is not claimed. **[I]**

Implemented in `src/Shared/Utils/PlayerProfileRecordUtility.cs`
(`Build`/`Parse`).

### 6.5 What the host sends to continue the join — the roster [V]

The joiner does not go quiet once the host answers once. The live capture shows
it re-sending the *same* `0x1001` body, byte for byte, on a fresh frame counter
every round, indefinitely: it is waiting for the room, not for an
acknowledgement. The acknowledgement is already in place (§6.3), so what is
missing is the roster. **[V]**

The answer is a run of `0x1001` records, host entry first and then one per
joining player, in slot order — the same shape as the roster block the host
wrote into the recorded match, which is what the twelve back-to-back records
between `0x52` and `0x484` are. Until the client has that sequence it has no
slot of its own and no way to place the players around it. **[V]**

`RoomRosterService` (`src/GameplayServer/Rooms/`) holds the roster and builds
that run; `PlayerProfileHandler` answers every inbound profile with it. Slots
are keyed by remote endpoint, so a client that re-sends its profile keeps the
slot it was first given instead of walking along the roster on each retry.

The same run also goes to the peers **already** in the room (`PeerContext.Broadcast`).
A player announced only to the peer that just joined is never announced to the
ones that were there first, and they would carry on playing without knowing the
room has grown. The recorded match is one flat roster rather than per-join
deltas, so the whole roster is what is sent.

**The type is shared, and only one of the two shapes is a profile.** The whole
of the live capture in `docs/protocol/UDP_SERVER_LOG.txt` is 36 datagrams, and
after the handshake and the keep-alive every one of them is a `0x1001`:

| body | flags | count |
|---|---|---|
| 90 bytes | `0x00` | 11 |
| 1 byte, `0x00` | `0x01`..`0x05` | 23 |

The one-byte ones are not profiles and carry nothing a roster could be built
from. **They are not answered** — each roster is one record per player, sent to
the sender *and* to every peer in the room, so answering all 23 would turn a
host with two players into well over a hundred datagrams. `PlayerProfileHandler`
parses the body and stays quiet unless it holds a name. **[V]**

**The client never acknowledges anything the host sends.** Not one inbound
`0x1000 | sequence` appears in the capture, so a host's outbound reliable
frames are never closed by the peer. That is not a stall: nothing waits on the
closure, and the host acks the client on every inbound frame regardless. **[V]**

### 6.6 The rest of the opening burst, after the roster [V] / [U]

The host's opening burst in the recorded match runs `0x0c`..`0x13ed`; the tick
stream starts at `0x13ed`. Between the roster and the ticks there are two more
runs of `0x1001` records, and neither is decoded. What is established is where
they start, how many there are and what they look like. **[V]**

- **`0x480`** — one seven-byte record, body `07 00 00 00 00 00 03`, closing
  the roster run. Seen on the live wire too, closing a two-entry roster
  (`UDP_GAME_CAPTURE.md` §4), so it is not tied to a roster size; now
  implemented as `PlayerProfileRecordUtility.BuildRosterClose()`. **[V]**
- **`0x48b`** — a six-byte marker `<u32> <u16>`, `5b 00 00 00 78 00`. Fourteen
  more of these follow at `0x509`, `0x5c3`, `0x605`, `0x647`, `0x689`, `0x707`,
  `0x839`, `0x8b7`, `0x8f9`, `0x93b`, `0x97d`, `0x9bf`, `0xa01`, and `0xa7f`
  (nine bytes there). **[V]**
- **`0x491`..`0xa7f`** — **forty-eight** records of twenty-six bytes, in runs of
  4, 6, 2, 2, 2, 4, 10, 4, 2, 2, 2, 2, 2, 4 between the markers. Only **two**
  distinct bodies occur, alternating:

  ```
  85 12 fe ff ff ff ff 00 01 00 00 01 01 01 01 01 00 00 00 ff ff ff ff ff ff ff 01
  86 12 7f 7f 7f 7f 7f 07 07 07 07 07 07 07 07 07 07 07 07 7f 7f 7f 7f 7f 7f ff
  ```

  and one variant of the first that differs only in its last byte. The `fe`/`7f`
  and `ff`/`07` runs read as a default-constructed object — a bitfield of ones
  between sentinels — so these are most likely initial spawn state for players
  who have not spawned. **[I]** They are **not** implemented: sending twenty-six
  bytes of guessed structure would be worse than sending none.
- **`0xa88`..`0xe5e`** — sixteen records whose bodies all begin `0b <id>`, with
  `id` running `0, 1, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 24, 24` and
  lengths 161, 50, 54, 54, 57, 57, 55, 56, 55, 56, 56, 55, 55, 55, 25, 25. The
  161-byte one lists many items, the 25-byte ones almost nothing, which reads as
  a per-thing loadout rather than per-player — the ids are not the twelve roster
  slots. **[I]**

  **This list is not replay-only.** The first four entries — `0b 00` at 161
  bytes, `0b 01` at 50 and `0b 15` at 54 twice — are byte for byte the four
  `0b` payloads the live dedicated server sends in its post-join burst
  (`UDP_GAME_CAPTURE.md` §4), and the cross-sample constant `45 2f 57 39 67 68`
  below sits at the same body offset in the live `0b 01`. The replay's opening
  burst and the live gameplay burst are the same records on two channels, which
  is worth knowing: a replay is now a source for live payloads rather than only
  a source for framing. **[V]**

  In the live round these payloads are **not** confined to the opening burst.
  There are 580 of them, in 470 datagrams, from t+4.99 to the last second at
  t+825.6, spread across twelve different record types — `0x1001`, `0x9001`,
  `0x1a54`, `0x087f`, `0x00b1`, `0x00df` and others — and 504 of them go out
  after four peer handles have been announced. What is a one-shot is the *burst*
  that opens with them, not the payload family: the round contains exactly one
  burst of that shape and the three later joiners do not each get one. **[V]**

  The record types are polymorphic rather than dedicated to these payloads: a
  `0x9001` or `0x1001` carries a `07 48` roster entry on one datagram and a
  `0b` payload on the next, and `0x1001` bodies across the round begin `0b`,
  `07`, `85`, `86`, `83`, `00`, `0c`, `81`, `84`, `82` and `02`. The leading
  byte is what tells the shapes apart, not the record type. **[V]**

  **Not implemented, for the same reason as above**: the *contents* are one
  character's equipment, skills and settings, and sending those to any other
  player would be wrong. The burst's shape is built — see
  `PostJoinBurstService` — with each payload as its `0b` tag, its slot and
  zeros, which is the most this server can say without inventing a loadout.
- **`0xe5e`..`0x113a`** — not walked.

**A cross-sample constant.** The six bytes `45 2f 57 39 67 68` ("E/W9gh") sit
inside the body of the join request a real client sent
(`docs/protocol/UDP_SERVER_LOG.txt`, offset `0x11`) and occur **once** in the
whole 8.4 MB replay, at `0xb4d` — inside the `0b 01` record, at body offset
`0x1b`. The replay record is the live request's body prefixed by `0b 01`, byte
for byte after that. So this run is a fixed template section, not per-player
data, and the two record kinds are the same payload with and without a
`0b <id>` prefix. **[V]**

**The tick stream is confirmed.** The framing the replay parser documents —
`{id u16 LE, len u8, class u8, data}`, `len` = data + 1, and
`id & 0xFF` = `0x75 + 10 * slot + OFF[class]` — walks ten consecutive records
from `0x140b` with every one landing exactly on the next boundary, and the
first ten give slots 0 then 1 with `OFF` matching for classes 01, 02, 04, 05 and
06. **[V]**

### 6.7 The block ahead of the roster [V] / [U]

The host's opening block does not start at the roster. It is a **fixed
seventy-byte prologue**, `0x0c`..`0x52`, and the roster starts at `0x52` in
every sample. That was only visible once there was more than one sample; the
earlier reading of `0x0c` as "the size of the first record" was wrong, because
it varies while the roster start does not. **[V]**

Five replays, four maps, fetched from `/api/v1/download-replay/<id>`:

| file | map | `0x0c` | `0x10` | `0x1e` |
|---|---|---|---|---|
| `replay_360821_7` | 10 | 38 | 99427 | `0x9e43` |
| `replay_360824_6` | 10 | 40 | 99452 | `0xa08c` |
| `replay_360825_7` | 4 | 44 | 99456 | `0x9e46` |
| `replay_360827_5` | 17 | 58 | 99446 | `0xa08f` |
| `replay_360828_5` | 7 | 38 | 99450 | `0x9e47` |

The sixteen-byte record is the same in all five — type `0x0102`, length `0x10`,
flags `0x39`, at `0x14` — and reads:

```
[0x00] u16  0x0007    constant
[0x02] u16  0x0032    constant
[0x04] u32  0         constant
[0x08] u16  varies    0x9e43 / 0xa08c / 0x9e46 / 0xa08f / 0x9e47
[0x0a] u16  0         constant
[0x0c] u16  0x0201    constant
[0x0e] u16  0         constant
```

**Thirteen of the sixteen bytes are identical across five matches on four maps,
and the record carries no map and no mode at all.** That refutes the earlier
guess that this was the match description. What it is instead is close to
readable: `0x0007` and `0x0032` are exactly the version byte and the roster base
of the `0x1001` roster records that follow (§6.4), so the record is a
**descriptor of the roster format that comes next**, not of the match. **[I]**

The map identifier lives only in the replay *file* header at offset `0x07`, and
that is the file's own bookkeeping rather than anything the host put on the
wire — the joiner learns the map over TCP with the rest of the room, before it
ever opens a peer session.

**The two words at `0x0c` and `0x10` are per match** and vary independently of
the map, the tick count and the file size. `0x10` sits in a narrow band around
99450 across all five, which reads as a serial or a seed. **[U]**

**Nothing here is implemented, and now there is a reason not to.** With thirteen
of sixteen bytes constant, the descriptor could be written — but the two words
that would have to be invented are exactly the per-match ones, and the
descriptor is replay-file material rather than a thing a joiner is shown to be
waiting for. The host sends the roster alone. **[U]**

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
- **Algorithm** (decoder `FUN_00efd840`, instruction-exact, reproduces the
  capture's profile record byte-for-byte including the account name):
  LZSS — a running MSB-first bit stream of **1 flag bit per token**; `1` = one
  literal byte (8 bits follow), `0` = a back-reference of a **9-bit absolute
  ring offset** then a **4-bit length field, len = field + 2 (2..17)** — the
  copy loop's exit check is off by one against its emit placement, so the
  final preloaded byte is still copied; the compressor symmetrically stores
  `len − 2` in the field. Matches read byte *j* from `ring[(off + j) & 0x1FF]`
  (first byte directly from `ring[off]`); every emitted byte, literal or
  copied, is also written to `ring[w++ & 0x1FF]` where the write index
  **starts at 1** (`li r31, 1`, `0x00efd894`) — `ring[0]` stays zero, so ring
  position *k* holds output byte *k−1*, i.e. `off` is effectively 1-based:
  byte *j* of a match copies `output[off + j − 1]`. Matches may overrun the
  write position (reading unwritten ring = zeros), so zero runs encode as
  RLE with `off = n`. Offset **0 is the EOF marker** (stream ends; any
  remaining flag bits are padding). No size prefix, no mirrored ring writes,
  and the window is a **separate 512-byte ring**, not the output buffer.
  Inputs capped at `0x2000` bytes into an `0x800`-byte output buffer; match
  finding uses a hash-chain (`FUN_00efdc40` head / `FUN_00efd5d0` per byte).
- **On the wire (2026-09-09 live, format resolved 2026-09-10):** the stream is
  the whole `[2 .. len-0xa)` region of the frame — everything after the 2-byte
  scrambled header, before the 10-byte tail (the tail is outside the chain and
  outside the stream; the EOF marker stops the decompressor first). Data-phase
  frames set the marker (hdr `0x8002`…`0x801e`) **[V]**. The earlier "u16 BE
  output-size prefix `0x0814` = 2068" reading was wrong — those bytes are simply
  the first stream bytes, and a 43-byte stream could never expand to 2068 under
  this format.
- **The join request is tagged `0x9001`, not `0x1001`** (live capture).
  A captured join carries `type=0x9001 len=0x95` holding the player record, and
  `0x1001` is the tag a roster answer uses. Registering only the roster tag is
  what left a join with no handler, and the handler had nothing to log because
  an unregistered type is a drop, not an error. §6.4's offsets also do not
  carry: the join request's per-player block is longer, so its names sit at the
  end of the record rather than at `0x44`.
- **The joiner's profile frame IS marked, and it is not a `0x1001` control
  frame** (live capture). The frame that carries the join request arrives
  with hdr `0x8001` and decompresses to `type=0x1001 len=0x95`, holding the
  player record — the same §6.4 shape, with the leading `0x02` that marks it a
  request rather than a roster entry. An earlier reading here said handshake
  and `0x1001` control frames never set the marker; that held for the
  one-byte `0x1001` *acknowledgements* of §6.3, which are short enough that the
  builder prefers the literal form, and was wrongly generalised to the profile.
  So the runtime path that sets the `0x200` gate flag is the data phase after
  all, and the question this section left **[U]** is answered.
- **The stream ends by exhaustion, not by the EOF marker** (live capture).
  The marker-offset rule above is what the decoder implements, but a captured
  joiner frame runs out of input mid-token with no offset-0 ever written. A
  decoder that treats exhaustion as an error returns nothing for the frame and
  the whole join request is lost with no error anywhere: the digest still
  verifies, the frame still arrives, and the host answers a session it has no
  profile for. That is what this server did — it read the capture as an empty
  content region, logged no profile and no unknown message type, and left the
  joiner re-sending the same body forever (§6.5). Exhaustion must yield the
  bytes decoded so far, not a discarded frame.
- **The capture annotations in `UDP_PACKET_DUMP.txt` predate any working
  decompressor.** Its `tag=0xc480 msgLen=20523` lines are a misparse of
  compressed bytes read as if they were message headers, not a format finding;
  read §7's algorithm instead. The header counters and the handshake-phase
  observations on those lines are sound.

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
| **Handshake accept (joiner side)** | `FUN_00268ba8` queue-drain (drain body `0x268a64`…`0x26953c`) | consumes a received reply queued by the decoder. Gates: tail digest (`0x266918`, in the decoder); `peer_id == session[0x18]` at `0x268ef4` (`lwz`/`cmpw`/`beq 0x268f58`) — **silent skip** on mismatch, `li r27,0`, state unchanged; `module_magic` at `0x268fd8` — mismatch → `0x2690d0`, which stores **state 6** at `0x2690d8`; a capability test at `0x269008` reading the peer's `[18]` byte against our own `flags & 0x800`. On success: peer sockaddr → `session+0x2c..0x30` (`0x268f70`), flags `\|= 0x4` (`0x268f6c`) and `\|= 0x2` (`0x268fb0`), peer base → `session[8]` and `session[0xc] = base ^ 0x2b58de69` (`0x268c30`–`0x268c40`), peer's capability byte OR'd into `session+0x14` (`0x269064`), then re-encode + re-send (the ping-pong) around `0x268ea0`/`0x268eb8`. State **8** is reached separately at `0x268c58` once `(flags & 0x3) == 3` — not at acceptance. The joiner's acceptance path is distinct from the host's receiver `FUN_00267d58` **[V]** |
| Per-peer queue drain | `FUN_00269230` / `FUN_00269240` / `FUN_00269280` | message queues consumed by the frame builder **[V]** |
| One-shot datagram writer | `_opd_FUN_00fa1af0` | string-writer for the 0x1000 datagram (`buf, 0x1000, fmt_or_state, 1, seq, arg1, game_id, 0xa0020`); exact template runtime state **[U]** |
| Handshake sender / reply processor | `FUN_00268ba8` | builds the §4 payload — peer_id (struct+0), `session+0x10` base, the magic global, the computed capability byte, the u16 at `[19..21)` (struct+6), count (struct+4), then per entry {ip BE 4, port LE 2} — at `0x268cc4`…`0x268e1c`, `16 + 6×count` bytes; the same body is emitted by four other builders at `0x267e6c`, `0x2684e0`, `0x268804`, `0x268aa0`; ALSO drains the receive queue and runs the joiner-side acceptance gates (§4) — the sender is the reply consumer **[V]** |
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
| Replay path string site | function containing `0x26e670` | loads the string `replay/replay0.dat` (vaddr `0xfc2b80`) and passes it to `0x261f28` → `0x263360`, which stores its first argument at object `+0` and copies 32 bytes of the path to `+8`; the function also sets a `+0x28`/`+0x2c` pair, and sits beside the receiver pump `FUN_0026e790`. Who is on either end (a reader, a writer, or both) is **not** established **[V]** |
| Replay (RPDT) header object ctor | `0x2849e8` and `0x284cb0`; the only two `RPDT` references are `0x284bdc` / `0x284ea4` | Two **byte-identical** constructors. Each allocates a `0x2400` object with an `0x2000` buffer, initialises cursors, and hands the 4-byte `RPDT` string (vaddr `0xfc2b98`) plus two further 4-byte table entries to a method on an object. `0x284cb0` is reached from the transport block at `0x264378`, after a `0x6e8` allocation. **Verified:** the `RPDT` string is referenced only from these two functions, and no `lis`/`ori` anywhere builds `0x52504454`, so nothing in this image compares the magic; the method's object descriptors (`0x1856ca8`…) lie outside the mapped segments (`0x10000`–`0x12a6c00`), so its body is not in this image. **Not established:** whether this ctor records or reads — it authors the magic into a buffer, which reads as a recorder **[I]**, but the I/O method is out of image and no reader was found **[U]** |
| gfio file-op site | function containing `0x2873c0` (entry `0x2872e0`) | the only reference to the `gfio_ps3.cc` assert string (`0xfc2ba0`); calls SDK `0xfbxxxx` file primitives (`0xfbd67c` with `0x7d0`/`0x4000`/1, `0xfbd6dc` with `0x20`). **Not established** that it serves the replay file specifically **[V] for the calls, [U] for the role** |
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
| Frame marker | hdr bit `0x8000` | LZSS-compressed frame; `[2..len-0xa)` is a raw stream, no wire message header (§6.2/§7) **[V]** |
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
`mgo2-server`'s UDP stack (`src/core/udp/` + `src/infrastructure/udp/`, started by
`src/main.ts` on port 5730) and has taken a real
MGO2/RPCS3 client from the first dial through the session-keyed data phase. The
crypto formulas are exact — copy them from §5.

### 11.1 Listen

- Bind UDP on the port the TCP `0x4321` reply advertises as the host endpoint (the
  host's `character_connections` row). The mgo2-server `DedicatedHostService`
  defaults to 5730 (`UDP_PORT`).
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
- Frames with hdr bit `0x8000` = LZSS-compressed: decompress `plain[2 .. len-0xa)`
  per §7 (1=literal / 0=match, 9-bit absolute ring offset + 4-bit len field
  with len = field + 2, ring write index starting at 1, offset 0 = EOF); the
  output is ordinary `type|len|flags2|body` messages. The capture's
  repeated compressed frame holds the joiner's `0x1001` player-profile record —
  re-sent byte-identical until ACKed (§6.3).
- Uncompressed `0x1001` frames = the joiner's ACKs of your frames (§6.3;
  `len 1`, `flags2` = its re-send attempt).
- **ACK each inbound seq once** (§6.3: `type 0x1000 | seq, len 1, flags2 1,
  body [0]`, on the shared counter) — but never a frame whose only content is
  ACK entries.
- Answer with session-keyed frames on the shared counter; the joiner's messages
  after its reliable exchange is acked tell you what the room/host handshake
  wants next.

### 11.7 Minimal code sketch (TypeScript, from the mgo2-server UDP stack)

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

Env knobs: `UDP_PORT` (default 5730), `UDP_HOSTNAME` (bind address). The host
identity (peer id / counter base) comes from `udp-host-identity-constants.ts`.

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
   `FUN_00267d58` (§4). The per-type dispatch site is `0x2614f8`, but **what it selects on
   is [U]** — see §6.2; the mask is `0x7f000fff` and the class bits are relocated rather
   than masked out, so the earlier "by the twelve-bit id with the class bits masked out"
   reading is withdrawn. The table itself is built on the module
   instance at runtime (`0x1856c10`, outside the mapped segments) so the id-to-handler
   entries cannot be read from this image either.
5. Exact datagram template behind `FUN_00fa1af0` (format/state pointer is
   runtime-initialized).
6. `FUN_00fbc1fc`'s resolved target (`PTR_FUN_0119f550`) and the meaning of
   `0x1f`/`0x18`/`0xa0020`.
7. Whether **mid-match game-data datagrams** differ from the `0x1000` one-shots.
   **Resolved (2026-09-09, live):** yes — data-phase frames are `0x1001` records,
   either uncompressed (control, `len 1`) or LZSS-compressed (hdr marker
   `0x8000`, raw stream per §7); the `0x1000` tag is just the handshake/one-shot
   message type. **Resolved for the tail and the keyed key:** state-8 frames
   verify the §5.3 tail with `K ^ 0x2b58de69` and the §5.2 chain with
   `K = peer_base ^ own_base` — confirmed live (§6.2). **Resolved (2026-09-10):
   the LZSS decompressor (§7) and the compressed content** — the repeated
   compressed frame holds the joiner's `0x1001` player-profile record
   ("phildunphy23", §6.2). **Resolved (2026-09-10): the ACK wire format for
   reliable records** — §6.3: `type 0x1000 | seq, len 1, flags2 = attempt,
   body [0]`, cumulative, one shared outbound counter, no ack-of-ack.
8. Which datagram template/payload type carries **voice** — expected to share this
   datagram path as one payload type among many **[I]**.

---

## 13. Revision history — superseded readings [V]

Later entries were added after this section was first written and are listed newest-first
below the original block.

Readings that were **replaced** by the live-verified facts above; kept for anyone
landing on old captures or old notes.

| Date | Superseded reading | Corrected to |
|---|---|---|
| 2026-09 | handshake travels **plaintext** | handshake is scrambled + XOR-chained like every frame (§4/§5) |
| 2026-09 | scramble positions from `LCG(len+10)`, `% (len+8)` | real positions in §5.1 (`LCG(len)`, `% (len-2)` / `% len`) |
| 2026-09-07 | keyed §5.3 digest key collapses to the bare constant | `session+0xc = peer_base ^ 0x2b58de69`; keyed digest key = `K ^ 0x2b58de69` — confirmed live 2026-09-09 (§5.3/§6) |
| 2026-09 | reply `peer_id` should **echo the joiner's id** | reply carries the **host's** character id; the joiner compares it to its stored peer descriptor (§4 gate 1) |
| 2026-09-09 | wire prefix = `u16 LE 0x1000` + `u16 LE` message length | message header `type u16 LE \| len u8 \| flags2 u8` (§3/§6.2) |
| 2026-09-09 | header sequence gate at `0x2668e4..0x266914`, window `> 0x100` | gate at `0x26712c..0x267198`; signed 16-bit diff `> 0x1f` (31) drops (§5.2) |
| 2026-09-09 | key selection reads the state byte at `session[5]` | state byte at `session+4` (§5.2/§6.1) |
| 2026-09-09 | flags word read at `session+5` (u16) | flags word at `session+0x14` (§6/§7) |
| 2026-09-10 | the 17-byte `0x1001 len 1` frames are control/window signaling with cyclic flags2 | they are **ACKs** of the host's frames — `flags2` is the send attempt, escalating while unacknowledged (§6.3) |
| 2026-09-10 | "exact ACK wire format is the last transport-side unknown" | resolved: `type 0x1000 \| seq, len 1, flags2 = attempt, body [0]`, cumulative, no ack-of-ack (§6.3) |
| (pre-capture) | the wire type is one opaque u16 a handler table keys on | it is `id (12 bits) \| class-bits`, the class derived from the message flags (`flags&1`→`0x1000`, `flags&2`→`0x4000`, `flags&0x20`→`0x8000`, body > `0xff`→`0x2000`) and masked out of the per-id lookup — so `0x5000`/`0x5001` are two ids, not a sequence (§6.2) |