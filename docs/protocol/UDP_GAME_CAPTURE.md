# The `mgo2-game.pcapng` capture — a dedicated-server game, decoded

`docs/mgo2-game.pcapng` is a live capture of **one complete game on a real dedicated
server**. It is the first capture in this project of a *running* game: every earlier one
stopped at the join. This file records what the gameplay channel carries once the join is
behind us.

> The capture is **not committed** — `docs/mgo2-game.pcapng` is in `.gitignore`, because it
> is a recording of a real session. Everything below is measured out of it, and the parts
> that identify the session — the character names and the peer ids — are written here as
> **redacted placeholders**, so the figures in this file are the real ones and the labels
> are not. The counter bases and the session key derived from them are left as they are,
> because the tests decrypt the captured frames with them.

Every statement below is read out of the capture with the decoder in `tools/udp_frame.py`,
which is the offline twin of `FrameCryptoUtility` / `LzssUtility` / `MessageCodecUtility`
and was validated against the frame pinned in `LatestJoinerFrameTests`. No field is
inferred from plausibility: a record that does not walk to the last byte is reported as
unparsed rather than described.

> **For the join specifically, read `UDP_JOIN_FLOW.md` alongside this file.** That one
> walks the nine opening datagrams in order and ties each to the `MGO2.ELF` instruction
> that produces or consumes it. This file measures the channel; that one checks it
> against the binary, and it is where the handshake `[18]` field is re-read as a
> capability byte.

> Confidence tags match the rest of `docs/protocol/`: **[V]** verified against the bytes,
> **[I]** inferred, **[U]** unresolved.

---

## 1. Scope — which flow this file is about

The capture holds one game. It also holds the lobby, HTTPS, DNS and STUN traffic of the
same session, and three further peer flows that never established. Those are out of
scope here; this file is the gameplay channel between the two parties that played.

| | |
| --- | --- |
| joiner | `10.2.0.2:5730`, character **`PlayerOne`**, `peer_id` 51001 |
| dedicated server | `99.66.131.177:5731`, character **`Dedicated host`**, `peer_id` 51002 |
| session key | `K = 0x736773a6 ^ 0x3c8a65d6` = **`0x4fed1670`** |
| frames | 19 021, of which 19 018 verify their tail digest |
| compressed | 5 111 frames carry the `0x8000` marker and are LZSS |
| duration | one round, ~13 minutes of wall clock |

Both sides send capability byte `2` at handshake offset `[18]` and both advertise **two**
endpoint pairs. The joiner's pairs are `217.138.213.5:5730` (public) and `10.2.0.2:5730`
(private); the server's are `99.66.131.177:5731` and `10.104.10.28:5731`. So **both**
parties advertise the same public/private pair shape §4 of `UDP_P2P_PROTOCOL.md`
describes, confirmed on a session that ran to completion rather than to a join. **[V]**

> **Corrected.** This paragraph previously read "the joiner's pairs are `10.2.0.2:5730`
> twice … the server is reachable on two addresses and the client on one". That came from
> mistaking entry 0 for entry 1: entry 0 is the public address and only entry 1 is the
> LAN address. Re-read at the byte level in `UDP_JOIN_FLOW.md` §3, which also shows the
> `[18]` byte is a capability byte rather than the `ver` this file inherited from
> `UDP_P2P_PROTOCOL.md` §4 — the `ver = 2` in this file's preamble is the u16 at
> `[19..21)`, which is `2` for the joiner and `1` for the server.

**The three other peer flows** (`98.26.168.27`, `89.152.226.41`, `92.191.178.103`, 17–771
datagrams each) are one-sided: each sends exactly one handshake and never receives an
answer that decodes. All three carry `ver = 6`, and **`ver` bit 2 set is the gate §4 gate 3
of `UDP_P2P_PROTOCOL.md` describes as fatal** — a reply with bit 2 set is rejected unless
the session already carries flag `0x800`. These sessions therefore die at the handshake,
which is consistent with 19, 771 and 7 datagrams of unanswered traffic rather than a game.
**[V]** as an observation; the causal claim is **[I]** — nothing here proves the bit, only
that these three carry it and the one that established does not.

---

## 2. The record framing — two conventions on one wire

This is the finding that had to be settled before anything else could be read, and it
corrects the single-convention reading in `UDP_P2P_PROTOCOL.md` §6.2 and in
`MessageCodecUtility.ParseMessages`.

A content region is a run of records, and the fourth byte of a record is *not* read the
same way for every record:

| record | identifier | header | length byte is |
| --- | --- | --- | --- |
| session record | `id >= 0x1000` | `{id u16 LE, len u8, flags2 u8}` | the **body length** |
| in-game tick record | `id < 0x1000` | `{id u16 LE, len u8, class u8}` | the **body length plus one** |

The tick form is the one `tools/mgo2_replay_parser.py` already walks — *"`len` is
`data_len+1`"* — and it is the same shape the replay records use. The extra byte is the
**attribute class**, which the tick parser reads as a separate field and the session
parser reads as the message flags. One wire, two framings, told apart by the identifier.

**How this was established, and how firmly.** Reading every length as a body length walks
**291 of 19 018** frames to their last byte. Reading every length as `data + 1` walks
**17 423**. Partitioning by identifier walks **18 728 — 98.5%**. The residual 290 frames
are the honest remainder: their tail is one of **14 distinct 8-byte strings**, the commonest
being `ff cf ff 0f 01 02 06 00` (138 frames) and `00 98 06 0f 01 02 06 00` (105). Every one
ends `06 00`, and every one is reached from a record that would need a length one byte
longer than the frame holds. A constant 8-byte trailer of that shape is a strong candidate
for a frame terminator, but **it is not established** — no framing rule in the binary or
in the replay format accounts for it, and a rule that happens to fit 290 frames is not a
rule. Recorded as **[U]**.

`tools/udp_framing_solver.py` and `tools/udp_framing_table.py` reproduce both numbers, and
the first also reports the per-type vote, which is what shows the split is per-type rather
than an artefact of one rule fitting one direction.

### The frame header is a compression flag and a fifteen-bit counter [V]

The header is a little-endian `u16` at `[0..2)` of every datagram, and it is
**not** a set of flags. It is one bit and one counter:

```
bit 15      LZSS compression marker (§2's 5 111 compressed frames)
bits 14-0   a monotonic per-peer frame counter
```

Measured over all 19 018 verified datagrams:

| | server → joiner | joiner → server |
| --- | --- | --- |
| frames | 18 526 | 492 |
| counter range | 2 .. 18 665 | 1 .. 492 |
| step of +1 between consecutive frames | **99.4%** (18 418 / 18 525) | **100.0%** (491 / 491) |
| sequence values never seen | 107 | 0 |
| wraps | none — 18 665 < 2^15 | none |

So reading the sequence is `hdr & 0x7fff`, and the `0x8000` marker is the only
flag. That part was already right.

**Bits 12, 13 and 14 are counter bits, and reading them as flags is wrong.** They
were read three ways before being settled, and two of those readings were wrong
in ways that looked convincing:

- *"a uniformly random 2-bit field"* — the counter crosses 4096-multiples four
  times, which produces four equal-length epochs; that was mistaken for four
  equiprobable values. The field is in fact **constant for ~4 070 frames at a
  time** and changes only 4 times in the whole round, in the order
  `[0, 1, 2, 3, 0]`.
- *"a flag set on 12.2% of frames"* — bit 14 is set on exactly one contiguous
  run, t+688.1 → t+823.2. That is simply the counter passing 16 384.
- *"the high bits of a 14-bit counter stepping on wrap"* — closest, but no wrap
  occurs at all: the counter never reaches 32 768. The transitions at
  4096/8192/12288/16384 are ordinary carry propagation.

Two traps worth naming. A `0xF000` mask folds the compression bit into the field
and reports a value distribution that does not exist. And **correlating the bits
against record types is confounded by time** — `0x087f` shows bit 14 on 100% of
its 1 001 frames only because that type occurs solely after t+688, where bit 14
is permanently set. Every such correlation in this capture needs the time axis
before it means anything. **[V]**

The client's own code agrees on the width if not on the meaning: at `0xF20ABC` it
reads a byte and tests its bits individually, setting `0x8000`, `0x4000`,
`0x2000` and `0x1000`. That is in the **handshake** body, not in a frame header,
so those four bits are a handshake field — plausibly a capability mask — and are
**[U]**. Generalising them to per-frame flags, as an earlier draft of this file
did, is retracted.

### What this means for the server

`MessageCodecUtility.ParseMessages` reads `length = buffer[offset + 2]` for every record.
Against a live game that is **wrong for every tick record**: it over-reads by one byte and
desynchronises the rest of the content region. Our server only ever sees the join
sequence, where all records are session records, so the bug has never bitten — but the
first peer that sends a tick record alongside a session record will have its frame
mis-parsed from that record onward. This is the one change in this capture that is a
defect rather than a gap.

---

## 3. Session record inventory

Session records only (`id >= 0x1000`). Tick records are in §5.

| id | server → joiner | joiner → server | body | what it is |
| --- | --- | --- | --- | --- |
| `0x1000` | — | — | 28 | handshake (§4), both directions, pre-keyed |
| `0x1001` | **463** | **49** | 1–79 | roster / profile record — §4 |
| `0x1100` | 4 | — | 2 | **[U]** |
| `0x1101` | — | 4 | 2 | **[U]** |
| `0x1250` | 354 | — | 6 / 9 / 10 | **[U]** |
| `0x1254` | 268 | — | 2–90 | **[U]** |
| `0x1256` | 33 | — | 2 / 7 / 9 | **[U]** |
| `0x1258` | 5 | — | 2 | **[U]** |
| `0x125a` | 310 | — | 6 | **[U]** |
| `0x12de` | 23 | 2 | 2 / 6 / 14 | **[U]** |
| `0x1a50` | 467 | — | 6 / 9 / 10 | **[U]** |
| `0x1a54` | 277 | 7 | 2 / 3 / 7 / 90 | **[U]** |
| `0x1a58` | 8 | — | 2 | **[U]** |
| `0x1a5a` | 374 | — | 6 | **[U]** |
| `0x1ade` | 29 | 1 | 1 / 2 / 6 / 14 | **[U]** |
| `0x5000` | — | 1 | 0 | keep-alive — §4 |
| `0x5001` | 60 | **481** | 0 | **[U]** — see below |
| `0x5250` | — | 336 | 0 | **[U]** |
| `0x5254` | — | 372 | 0 | **[U]** |
| `0x5256` | — | 33 | 0 | **[U]** |
| `0x5258` | — | 5 | 0 | **[U]** |
| `0x525a` | — | 292 | 0 | **[U]** |
| `0x52de` | 3 | 25 | 0 | **[U]** |
| `0x5a50` | — | 428 | 0 | **[U]** |
| `0x5a54` | 6 | 321 | 0 | **[U]** |
| `0x5a58` | — | 8 | 0 | **[U]** |
| `0x5a5a` | — | 376 | 0 | **[U]** |
| `0x5ade` | 3 | 19 | 0 | **[U]** |
| `0x807f` | 3 | — | 4 | **[U]** |
| `0x80d9` | 1 | — | 4 | **[U]** |
| `0x810d` | — | 243 | 16 | **[U]** |
| `0x825c` | — | 5 | 2 | **[U]** |
| `0x890d` | — | 34 | 16 | **[U]** |
| `0x8a5c` | — | 2 | 2 | **[U]** |
| `0x9001` | 71 | **191** | 1–161 | **[U]** — §4 |
| `0x9250` | 10 | — | **[U]** |
| `0x9254` | 139 | — | **[U]** |
| `0x9256` | 3 | — | **[U]** |
| `0x92de` | 3 | — | **[U]** |
| `0x9a50` | 13 | — | **[U]** |
| `0x9a54` | 94 | 1 | **[U]** |
| `0x9a5a` | 2 | — | **[U]** |
| `0xd001` | **176** | 5 | 1 | **[U]** — see below |
| `0xda54` | 1 | — | **[U]** |

**`0x1001` is the only type both parties use in volume, and it is the one this project
already implements.** The roster it carries is the whole of the join.

### How delivery is acknowledged — and why the ack cannot name a frame [V]

Delivery is **not** signalled by a header bit. It is signalled by a record in
the content region:

```
type   = 0x1000 | (sequence & 0x0fff)   u16 LE
len    = 0x01
flags2 = send attempt
body   = 0x00
```

**There is no reliability flag in the frame header.** Bit 12 is set on 8 114 of
the 18 526 outbound frames, and 27 acknowledgements arrive — one per 301
flagged frames. A flag meaning "reliable" would not sit at a uniform ~44% across
the round. **[V]**

**Reliability is marked per record instead, in bit 12 of the record's type** —
the `0x1000` class bit that `UDP_P2P_PROTOCOL.md` §6.2 traces to `flags & 0x01`.
And the records that would flood if they were acknowledged never carry it:

| | records | with type bit 12 set |
| --- | --- | --- |
| position (`0x006d`…`0x08db`) | 15 392 | **0** |
| vitals (`0x0080`/`0x0880`) | 4 527 | **0** |
| every tick record (`id < 0x1000`) | 146 224 | **0** |

A position tick is therefore structurally outside anything the acknowledgement
mechanism addresses. **[V]**

**But bit 12 is very nearly just the tick/session boundary**, so it should not be
read as "reliable" on its own. Of the 6 444 records at or above `0x1000`, **288
have bit 12 clear** — every one of them in the `0x8xxx` group (`0x810d` 243,
`0x890d` 34, `0x825c` 5, `0x8a5c` 2, `0x807f`, `0x80d9`), almost all
joiner→server with 16-byte bodies. So the bit tracks the record family at least
as much as it tracks delivery. **[V]**

**Nothing in the client tests that bit.** Disassembling `MGO2.ELF` finds **zero**
occurrences of `rlwinm rX, rX, 0, 0xc, 0xc` — the instruction that would keep
bit 12 alone — across 18.3 MB, and no `andi` equivalent. The client's ack
enqueue is unconditional; see `UDP_P2P_PROTOCOL.md` §6.3. So whatever governs
which frames get answered is **[U]**, and the capture cannot supply it: the host
sent no acks at all, so no policy was ever exercised. **[V]**

**The whole round's 27 acknowledgements**, and what they measure:

| | |
| --- | --- |
| count | 27 |
| direction | **all joiner → server** |
| sequence named | **1** — every single one |
| span | t+66.06 → t+729.62 |
| acknowledgements sent by the server | **zero** |

The server sent no acknowledgements at all. It received 492 frames from the
joiner and answered none of them. So in this session reliability ran one way
only. **[V]**

**The ack names 12 bits and the counter is 15, so it cannot identify a frame.**
The header counter reached 18 665 — past four full 4 096-wrap cycles — while
the ack's type field carries only the low twelve. Sequence `1` is therefore
ambiguous across **five distinct frames** in this round: sequences 1, 4 097,
8 193, 12 289 and 16 385. Counting how many frames share each 12-bit value
gives `{3 values shared by 3, 1 840 by 4, 2 199 by 5}` over the 4 096 values.

That ambiguity is also why the first acknowledgement at t+66.06 appears to
predate every frame carrying a twelve-bit value of 1 — the earliest of those is
at t+181.0. The frame the joiner was actually acknowledging is the **pre-keyed
handshake reply** at sequence 1, which never verifies under the session key and so
is in none of the frame counts above. **[V]**

This has a direct consequence for §3's inventory: **`0x1001` is two record kinds
in one type.** Its 512 records are 485 genuine ones (roster entries opening
`07 48`, `0b` payloads, `85`, `86`, `83`, `81`, `84`, `82`, `0c`, `02`) plus
**27 acknowledgements** — the one-byte `00` bodies. Any count of `0x1001` records
that does not separate those 27 is wrong, and the earlier reading of `0x1001` as
485 plus 512 came from exactly that conflation. **[V]**

This also corrects one implementation rule in `UDP_P2P_PROTOCOL.md` §6.3, which
said to acknowledge each inbound `hdr & 0x7fff`. Reading the sequence with
`0x7fff` is right, but the ack can only echo twelve bits of it, so for any
sequence at or above 4 096 the acknowledgement is ambiguous — it names the low
twelve bits and nothing more. **[V]**

**What all 27 naming sequence 1 means is [U].** Per-packet acknowledgement would
name 27 different sequences. A single repeated sequence over eleven minutes is
more consistent with a keepalive or window probe than with delivery
confirmation — but the capture settles that they are acks and not what they are
acknowledging.

**`0x5001` — not the acknowledgement, contrary to first reading [V] as a negative.**
An earlier draft of this file read it as the running game's acknowledgement tag, on the
strength of the shape alone. Measured against every one of the 541 records, that reading
is **refuted** on three counts, and it is therefore *not* implemented:

- **The identifier never varies.** All 541 records are `0x5001`; the low twelve bits, which
  §6.3 says carry the acknowledged sequence, are always `0x001`. A record that names no
  sequence cannot acknowledge one.
- **The fourth byte is a monotonic counter, not a send attempt.** It climbs to **194** on the
  joiner and **147** on the server over the round and never resets (416 of 480 joiner steps
  non-decreasing). §6.3's attempt field climbs `1→5` while a host stays silent and is small
  by construction; this is an unbounded per-session sequence.
- **The body is always empty.** `len=0` on all 541, where an acknowledgement entry is
  `len=1, body=0x00`.

It is also *not* a request/answer pair with the server's `0x1x..` family, which the counts
superficially suggest — see §3.1. What it is remains **[U]**.

**`0xd001`** — one byte, both directions, 176 from the server against 5 from the joiner, and
the value is a small counter (`02, 03, 04, 05, 06, 08`) rather than an identifier. Its
fourth byte is the same monotonic counter `0x5001` carries, so the two travel together. The
value's meaning is **[U]** and nothing here implements it.

---

## 3.1 A tempting pattern that is not one — the `0x1x..` / `0x5x..` families [V] as a negative

The counts invite a reading. The server sends a `0x1x..` family in volume and the joiner
sends a `0x5x..` family whose **low bytes and totals line up**: `0x56` 33 against 33,
`0x58` 13 against 13, `0x5a` 684 against 668, `0x50` 821 against 764. That is what a
request/answer protocol looks like in a table, and it would be a large finding if it held.

**It does not hold.** `tools/udp_pairing_probe.py` tests it both ways round, and neither
direction pairs: **0 of 821** server `0x1250`-class requests are followed by a joiner
`0x5250` within a second, and **0 of 764** the other way either. The two families simply
run *concurrently* for the whole round, first occurrences seconds apart and last
occurrences minutes apart. Two independent streams that both emit on the game tick will
produce matching totals whatever they mean; matching totals are not evidence of a pairing.
Recorded as a negative so the pattern is not rediscovered as a discovery. **[V]**

---

## 4. The join, as it actually happened

Byte for byte from the capture, both parties named. This is the part we implement.

**1 — handshake, both directions, pre-keyed.** Two 44-byte datagrams and a 16-byte keep-alive.

```
joiner -> server   10.2.0.2:5730 -> 99.66.131.177:5731   hdr 0x0000   t+0.000
  peer_id 51001  counter_base 0x736773a6  magic b7 8a 25 4d  ver 2  count 2
  entries 10.2.0.2:5730, 10.2.0.2:5730

server -> joiner   99.66.131.177:5731 -> 10.2.0.2:5730   hdr 0x0000   t+1.6681
  0x5000, empty — the key-establishing keep-alive, sent FIRST

server -> joiner   99.66.131.177:5731 -> 10.2.0.2:5730   hdr 0x0001   t+1.6681
  peer_id 51002  counter_base 0x3c8a65d6  magic b7 8a 25 4d  ver 2  count 2
  entries 99.66.131.177:5731, 10.104.10.28:5731
```

**The keep-alive leads, and it takes outbound counter 0.** The reply reads as the more
important of the two, so it is easy to send first — the live host does not, and the counters
are observable, so a client keying anything off them sees the order. **[V]** on the order and
the counters. The two go out **in the same millisecond** (t+1.6681), so there is no interval
to reproduce and nothing here delays a send.

**Both are pre-keyed, and the keep-alive is no exception.** Searching each frame's tail digest
over every derivation of the two counter bases and the two constants — `JB`, `HB`, `JB ^ HB`,
each of those exclusive-ORed with `0x2b58de69` and with `0x87103c2f`, and `0` — the 16-byte
keep-alive and the 44-byte reply verify with the **bare constant `0x2b58de69`** and with
`K ^ 0x2b58de69` at **neither**; the joiner's first session-keyed frame, 92 bytes at
`hdr 0x8001`, verifies **only** with `K ^ 0x2b58de69`. **[V]**

So the keep-alive described above as "key-establishing" does not carry the session key. It is
what the joiner counts as the host answering, and the joiner reaches its **keyed** state on
**accepting the handshake reply** — which it can only do after reading it. The **data phase** is
a second step, and it is the one a live client needs help with: `UDP_P2P_PROTOCOL.md` §6.1 and
§11.4 record that only a frame whose tail digest verifies with `K ^ 0x2b58de69` flips the
joiner's key flag, from a fake-host trial on 2026-09-09. §7.1's reading of this capture — that
the reply's acceptance is enough — does not hold for a live client: answered with these two
frames and nothing else, a joiner sat in its reply-accepted state and its connect FSM failed
the join 35.0 s after the dial with `0B09` (`P2P_CONNECT_FSM.md` §7). So one session-keyed
`0x5000` now goes out behind the reply, and the counters shift one place. What is settled here is
that this host sends no keyed frame *of its own accord*; and a host that marks its session
established **before** the handshake reply sends both opening frames keyed — which the joiner
then discards at the §5.3 digest gate before parsing a field, so it re-dials every ~1.9 s and
gives up. That is the other failure this server hit:
17 handshakes answered, 17 identical silent rejections, every one of them logged here as a
session established.

**2 — the joiner opens with its own profile**, `0x1001`, 140-byte body, opening `02 78 05
78 05` and carrying its own character name at the end. It is sent **compressed** (`hdr 0x8001`) and
**re-sent byte for byte** on `hdr 0x8002` — the reliable re-send §6.5 documents, and it is
answered by nothing until the server's roster arrives.

**3 — the server answers with the roster**, `hdr 0x8002`, three records:

| record | body | what |
| --- | --- | --- |
| `0x5001` len 0 | — | bare notify |
| `0x9001` len 83 | opens `07 4c`, ends **`Dedicated host`** | the host's own entry |
| `0x1001` len 79 | opens `07 48`, ends **`PlayerOne`** | the joiner's entry, echoed back |
| `0x1001` len 7 | `07 00 00 00 00 00 03` | the roster close |

**3b — the run is sent again, without its head**, `hdr 0x8003`, **117 µs** later, the same
three records byte for byte.

**3c — and one bare `0x5001` closes the exchange**, `hdr 0x0004`, t+2.4758, **158 µs** after
the repeat. The whole run, its repeat and the trailer leave in **202 µs**, which is too fast
for any timer to have scheduled them — they are three back-to-back sends. It repeats the type
that opened the run rather than closing it. What it says is **[U]** — the type is the one §3
shows is not the acknowledgement despite the resemblance — and it is sent because the live
host sends it.

The host's own entry travels under **`0x9001`**, the tag the joiner itself opened the
exchange with, while the joining players and the close travel under `0x1001`. Both roster
frames (`hdr 0x8002` and its re-send `0x8003`) do this, so the head of the run is not
merely a `0x1001` record like the rest of it. **Implemented** as
`RoomRosterService.HostEntryType`, which `PlayerProfileHandler` sends the head under, and
**logged at information level every time it is used** — the tag reads as a joiner's, so
seeing it go out is worth having in a log rather than discovering from a client that
ignores the roster. This corrects §6.4's "the host never answers with one", which was right
about the tag's origin and wrong about the host never sending it.

The three record lengths — **83, 79, 7** — follow from the names, and both captured records
carry **no clan name**: the host's record ends one byte after the NUL that closes
`Dedicated host`. A test pins those three lengths for a 14- and a 10-character name.

**Where the capture supersedes the replay-derived layout [V] — resolved, implemented.** §6.4
puts the roster index at offsets `0x04`, `0x05` and `0x07` as `0x32+index`, `6+index`,
`6+index`. This capture contradicts all three, and the live capture is now the reference.

The capture holds **six** roster records, not two: the host and five joining players at
roster indices `-1` and `0` to `4`, as further players joined the round (the five arrive at
t=4s, 156s, 208s, 387s and 494s). Sorted by index they give the rule directly:

| offset | host (`-1`) | `0` | `1` | `2` | `3` | `4` | rule |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `0x04` | `0x00` | `0xe2` | `0xe3` | `0xe4` | `0xe5` | `0xe6` | **`0xe2 + index`**, host a plain zero |
| `0x05` | `0x00` | `0x14` | `0x0f` | `0x04` | `0x0b` | `0x06` | per-player value, **not** an index |
| `0x07` | `0x00` | `0x14` | `0x0f` | `0x04` | `0x0b` | `0x06` | the same byte, repeated |
| `0x08` u32 | `0x0000a001` | `0x0001xxxx` | `0x0001xxxx` | `0x0001xxxx` | `0x0001xxxx` | `0x0001xxxx` | **character id**; the first and last are the same character |
| `0x0a` | `0x00` | `0x01` | `0x01` | `0x01` | `0x01` | `0x01` | the **high byte of the id at `0x08`**, not a field |

Four things follow. `[0x04]` is `0xe2 + index` and the host is written as a **distinct
zero**, not as `0xe2 - 1`. `[0x05]`/`[0x07]` are the same byte in all six and follow no
order, so they are not the second copy of the index the replay reading claimed. Their
values match the member bytes in the later `0x0261`/`0x0a61` rules-roster-shaped tick
records, making them likely opaque per-player handles **[I]**; how those handles are
allocated remains unknown. `[0x08]` is the **character id**, and `[0x0a]` is its high byte
rather than a flag of its own — see below.

### `[0x08]` is the character id, and `[0x0a]` is not a field [V]

`[0x08]` was read as a u16 per-player value of unresolved meaning, and the byte at `[0x0a]`
as an unresolved flag that is `0` on the host and `1` on every joiner. Both readings are
wrong, and the same reading is wrong about both: `[0x08]`–`[0x0b]` is one **u32 little-endian
character id**.

The values themselves are redacted, as this file's preamble promises: the four
characters that played are written here as `0x0001xxxx` and the tests carry stand-ins. What
matters is the shape and the stability, not the numbers.

Three things make it the character id rather than a coincidence of the roster order.

- **It is stable per character, not per slot.** The capture holds **fifteen** player entries
  across the round, and every entry of one character carries the same id — including the
  rejoin written at roster index `4` under handle `0x06` where that character's first entry at
  index `1` carried handle `0x0f`. The name is also stable; the endpoint pair describes the
  peer's announced network addresses, not the character's identity.
- **It matches the other channel.** The local player's id here is the same number as the one
  the TCP `0x4101` character record of the same session carries — the cross-check that names
  the field rather than merely numbering it.
- **It explains `[0x0a]`.** Every captured character id is in the `0x0001xxxx` range, so its
  high byte reads `0x01` on all five of them and `0x00` on the room record, whose id is
  `0x0000a001`. "Zero on the host, one on every joiner" is what the high byte of a u32 looks
  like; there is no field there at all. The code no longer writes one.

The room record's own value is **not [U]** either, and it is the same field as every other
one: `0x0000a001` is the **dedicated host's character id**. The TCP cross-check above
generalises to the host as well as to the joiner — the gameplay-lobby flow at
`15.204.239.231:5733` carries `00 00 a0 01` (big-endian `0xa001`) at offsets 176, 164 and 372
of commands `0x0700`, `0x0f00` and `0x4313`, with the name `Low Level Host` in the same record
**four bytes later**, and the same number is the dedicated server's UDP handshake `peer_id`
(40961). It is a character id and it is on the character record; it is **not** a room
identifier, and the earlier reading of it as "not this host's character id" was wrong.

### `[0x13]`–`[0x1e]` are the character's handshake endpoints [V]

These twelve bytes are not appearance data. They are two six-byte IPv4 endpoint pairs:
the public address at `0x13`–`0x16`, its little-endian port at `0x17`–`0x18`, the private
address at `0x19`–`0x1c`, and its little-endian port at `0x1d`–`0x1e`. Each roster entry
matches the public/private endpoint pairs that character announced in its handshake,
including the host's own entry. They locate the peer; the capture does not establish
them as character identity or appearance fields.

Appearance-like data is carried separately. A `0b <slot> 02 …` record (50–56 bytes)
arrives about 90 ms after roster entries and carries a repeated structure ending in a
per-character tail. The local player's 140-byte join request carries the same tail
(`1e 45 2f 57 39 67 68 0e 0e 00 0e 0e 00 0b 16 00 0e 0e 07 0f 00 00`), which the
server re-broadcasts. That record is **not implemented**: its slot byte is neither the
roster handle (`0x14 0f 04 0b`) nor the character id, slots are reused across rejoins,
and four of its columns remain **[U]**.

**Implemented.** `PlayerProfileRecordUtility` writes `0xe2 + index` (or zero for the
host), writes zero at `0x05`/`0x07`, writes the **character id** as a u32 at `0x08` —
there is no `0x0a` field — and copies the handshake endpoint pairs at `0x13`–`0x1e`.
`PlayerProfileRecordParseUtils` reads the profile fields, and `RoomRosterService` answers
a peer with its id and announced endpoints rather than values it invented. The separate
appearance-like `0x0b` record remains unimplemented. `PlayerNumber` is renamed
`PlayerValue` and is reported raw, because it is no longer an index. The block has a
constant `0x02` at `0x12`; `0x18` and `0x1e` are endpoint-port bytes, not structural
constants.

The builder is written against the six captured records verbatim, with same-length placeholder
names. The replay-derived vectors are gone: the live capture is the reference.

Two things here are worth stating because they correct the docs. The roster close
`07 00 00 00 00 00 03` was previously known only from a replay (§6.6) and is here on the
wire, closing a **two-entry** roster — host first, joiner second, so it is not tied to a
roster size. It is **implemented**: `PlayerProfileRecordUtility.BuildRosterClose()` builds
it and `PlayerProfileHandler` sends it last in the run, to the joiner and to the peers
already in the room alike. And **`0x9001` carries the
host's own roster entry**, not the join request: §6.4 reads `0x9001` as "a request to join,
the host never answers with one", and here the host sends it *as* the roster head. Both
sides send `0x9001` (71 and 191). The tag is shared, and which of the two shapes a body
carries is not settled by the type alone. **[V]** on the bytes, **[U]** on the rule.

**Also observed.** The **`0x5001` len 0** at the head of the roster run is present in both
roster frames and is now **sent**, as `UdpCommandConstants.RosterNotify`, because the host
does not invent records a peer never wrote. What it says is still unresolved — §3 shows
`0x5001` is not the acknowledgement — so it is sent as an empty record and nothing more is
claimed for it. The same type arrives from a peer in volume, so it and `0xd001` are
registered against `InGameControlHandler`, which logs them at debug and **does not answer
them**: they are normal traffic rather than unknown commands, but nothing decoded them, and
answering an unread record is the trap this file has declined twice.

The **fourth byte** of the roster records runs `0, 0, 1, 2` across the run and repeats
identically on the re-send — so it is positional, not a send attempt, but what it positions
is unknown. We send `0`.

**The host sends the run twice.** The re-send is not a mirror of the run to a second
recipient: it goes to the same peer, carries the same three record bodies byte for byte, and
**omits the empty `0x5001` head**. The join itself:

```
t+2.4756  counter 0x8002 (seq 2)  0x5001 0, 0x9001 83, 0x1001 79, 0x1001 7
t+2.4757  counter 0x8003 (seq 3)              0x9001 83, 0x1001 79, 0x1001 7
t+2.4758  counter 0x0004 (seq 4)  0x5001 0
```

Three of the eleven host-entry datagrams in the capture are doubled like this — at t+154.30,
t+208.98 and t+494.97 — and the gap varies: **0.9 ms, 0.7 ms, 0.8 ms**. The join itself is
the exception at 117 µs. **[V]** on the shape and the fact of it.

**What schedules the second copy is [U], and two candidate explanations are refuted.**
It is **not a retransmission of a lost frame**: the two go out on consecutive outbound
sequences 56 to 68 frames apart, with the tick stream filling the gap, so the second is a
new datagram rather than the same frame sent again. It is **not acknowledgement-driven**:
the joiner acknowledged sequence `0x001` twenty-seven times over the round and sequence
`0x002` — which is what the run travels as — **never once**, and the first acknowledgement
of any kind arrives at **t+68.53**, some 66 ms after the run above. Neither is proof that no
other mechanism schedules it: the host's own acknowledgements never cover these sequences at
all, and the joiner's first substantive reply is at t+2.53, so the capture cannot see what
ends the repetition. It is implemented as an **unconditional** second copy for that reason,
matching what is visible rather than a mechanism that is not.

**4 — the game runs.** From `hdr 0x8005` onward the channel is tick records in both
directions and nothing else of substance: 20 852 `0x0261`, 15 954 `0x0a61`, 10 932 `0x00dd`
and so on. The join is over in about **two and a half seconds**.

**A note on the clock.** Every interval in this file comes from
`tools/pcap_conversations.py`, which resolves the capture's `if_tsresol` (9, so
**nanoseconds**). An earlier reading of that reader assumed microseconds and made
every interval here a thousand times too large — the round looked like 9.5 days
rather than its actual **825.6 s**. The figures that predate that were taken
against a working clock and are correct; only ones measured through the broken
reader were wrong.

**The room grows, and the growth does not use the roster record.** Six characters
play the round, each named by its position record, and they arrive well after the
join:

| record | first position | | record | first position |
| --- | --- | --- | --- | --- |
| `0x00db` | t+192.3 s | | `0x0081` | t+563.8 s |
| `0x006d` | t+218.5 s | | `0x0881` | t+764.1 s |
| `0x00b3` | t+394.4 s | | `0x08b3` | t+764.1 s |

Yet **only two datagrams in the whole round carry the `07 4c` room record**, both at
t+2.476 with a single `07 48` player entry — the join itself. Every later arrival
reaches this peer as `0x9001` + one `0x1001` entry + trailing records, never as a
second entry beside the first:

```
t+154.301  122B  0x9001, 0x1001, 0x90c, 0xa61, 0xa61      (repeated at .302, 156.5, 158.9, 161.1)
t+384.976  193B  0x1001 x2, 0xd9, 0xdb, 0xdd x2, 0xdf, 0x1001, 0xda, 0x10c, 0x261 x2
t+494.973  123B  0x9001, 0x1001, 0x1a54, 0xa61, 0xa61
```

So the notification that a second player has joined is **not** the roster run this
server re-sends, and it is **[U]**: the leading `0x9001`+`0x1001` pair looks like a
single entry re-sent while the trailing records (`0x90c`, `0xa61`, `0x1a54`,
`0x10c`, `0x261`) change with each arrival, but nothing here decodes them.
`PlayerProfileHandler` broadcasting the whole run is therefore **unverified against
this capture** — the capture contains no datagram that would justify it.

### The post-join burst — one datagram, sent once [V]

At t+4.9929, 2.52 s after the roster answer, the host writes a 366-byte datagram
on outbound counter 5 (t+5 in the frame listing below). It is the only datagram
in the whole round of that shape. **[V]**

| order | type | body | what it is |
| --- | --- | --- | --- |
| 1 | `0xd001` | `24` | one-byte control |
| 2 | `0x9001` | 161 B, opens `0b 00` | host's own payload |
| 3 | `0x1001` | 50 B, opens `0b 01` | a joining player's payload |
| 4 | `0x1001` | 54 B, opens `0b 15` | unexplained |
| 5 | `0x1001` | 54 B, opens `0b 15` | byte-identical to record 4 |
| 6 | `0x0002` | 22 B | state blob |

A seventh record follows 300µs later on counter 6: `0xd001` with body `02`. **[V]**

**The state blob is fully pinned.** All 141 `0x0002` records in the round are 22
bytes and all go server to client. Eighteen are zero, then `40 00`, then a
sixteen-bit big-endian value — the only part that moves: 100 for 56 of them, then
0, 94, 98, 99, and the 50 this burst carries. `PostJoinBurstService.BuildStateBlob()`
reproduces the join-time blob exactly. What the value counts is **[U]**. **[V]**

**`0xd001` is mostly the host's, not the peer's.** The round holds 181 of them:
**176 server to client, 5 the other way.** It was registered receive-only here on
the strength of the 5. Two go out inside this burst, with bodies `24` and `02`. **[V]**

**The payloads do not decode, and are not copied.** The `0b <slot>` bodies run 6 to
161 bytes and hold one character's equipment, skills and settings. The first four
here match the replay's opening burst byte for byte — `0b 00`/161, `0b 01`/50,
`0b 15`/54 twice, including the `45 2f 57 39 67 68` template run at body offset
`0x1b` of `0b 01` — so a replay is a source for live payloads, not only for
framing (`UDP_P2P_PROTOCOL.md` §6.6). Their contents are still **[U]**, and the
burst is built with each payload as its tag, its slot and zeros. **[I]**

**Three things about this burst are worth not assuming.** **[V]**

- **It is not a two-player event, though it looks like one.** It does go out when
  the room holds the host and its first joiner, and no peer handle but that one has
  been announced at t+4.99 — the second arrives at t+154.3. But the `0b` payloads
  are not gated on that: 580 of them appear over the round, in 470 datagrams,
  through to the last second at t+825.6, and **504** of them after four peer
  handles are known. What is one-shot is the burst, not the payload family.
- **It is not one record per player.** Three of the four `0b` payloads travel under
  `0x1001` for a single joiner, and two of them are byte-identical. One payload
  per joining player is a reading of the shape, not a measurement.
- **The record types are not payload-specific.** `0x9001` and `0x1001` carry a
  `07 48` roster entry on one datagram and a `0b` payload on the next. Across the
  round `0x1001` bodies begin `0b`, `07`, `85`, `86`, `83`, `00`, `0c`, `81`,
  `84`, `82`, `02` — the leading byte separates the shapes, not the type. The
  `0b` payloads also ride on `0x1a54`, `0x087f`, `0x00b1`, `0x00df` and others.

**The fourth header byte is positional noise here.** It climbs monotonically to 180
over the round and *repeats* within frames: the roster answer carries `0, 0, 1, 2`,
this burst `1, 3, 4, 5, 6, 21`, and a frame at t+192.3 `36, 37, 34, 35, …`. So it is
not a roster index and not a per-record counter, and the burst does not set it
deliberately. **[V]**

---

## 5. Tick records

The game itself. `id < 0x1000`, length byte is `data + 1`, fourth byte is the attribute
class. There are **121 distinct ids**; the busiest are below. The table records wire facts,
not application meanings; cross-source readings and their confidence are documented after it.

| id | count (server → joiner) | body lengths | attribute-class byte |
| --- | --- | --- | --- |
| `0x0261` | 20 852 | 1, 4, 5 | 0, 1 |
| `0x0a61` | 15 954 | 1, 4, 5 | 0, 1 |
| `0x00dd` | 10 932 | 1, 5 | 4, 6 |
| `0x010c` | 9 997 | 2 | 3 |
| `0x00b5` | 8 752 | 1, 5 | 4, 6 |
| `0x0083` | 6 173 | 1, 5 | 4, 6 |
| `0x0081` | 3 087 | 16, 17, 32, 38, 44 | 2 |
| `0x0080` | 3 269 | 2 | 3 |
| `0x007f` | 3 087 | 3, 9 | 1 |
| `0x00b1` | 4 377 | 3, 9 | 1 |
| `0x087f` | 1 140 | 3, 9 | 1 |
| `0x0881` | 1 140 | 16, 32, 44 | 2 |

These are the literal fourth-byte values from the tick framing, not inferred
operation names. In particular, `0x0a61`'s one-byte `fe` records use classes 0
and 1; its four- and five-byte bodies use class 0. The steady host stream's
`0x090c fa fa` record uses class 3. These values match the records emitted by
`HostStreamFramesUtils`; by themselves, the capture framing and generic P2P codec
do not explain their application meaning. The RPDT cross-check below supplies
structure for the `0x0261`/`0x0a61` bodies and a candidate for `0x090c`.

The stripped `MGO2.ELF` has a static lookup entry mapping `0x0a61` to selector
`0xbe` (nearby entries include `0x0a60 -> 0xbd`, `0x0a62 -> 0xbc`, and
`0x0a63 -> 0xbf`). The entry does not name the selector's consumer, and the
generic P2P serializer/receiver only establishes record framing and routing.
The RPDT grammar reads `fe` as an empty rules-roster list for these IDs, but the
selector consumer and group membership policy remain unresolved; they are not
enough to synthesize the required live membership state.

**`0x0081` is the position record.** Its 16-, 32-, 38- and 44-byte bodies are the
four sizes `mgo2_replay_parser.py` maps to the compressed position and the yaw
pair, at the same offsets. The capture also has three 17-byte bodies whose meaning
remains unresolved. The capture confirms the replay parser's transform table on
live wire bytes rather than on a recorded file. **[V]**

### `0x0080` is health, and `0` is death [V]

The one tick record in this capture that needs no inference at all. Its body is two bytes —
**health, then stamina** — under attribute class `3`, and 3 269 of them arrive in one round,
server to client, in seven distinct bodies between them:

| body | count | |
| --- | --- | --- |
| `fa fa` | 2 893 | untouched: health 250, stamina 250 |
| `00 fa` | 316 | **dead**: health 0 |
| `46 fa` `7f fa` `c2 fa` `a0 fa` `0e fa` | 60 | health on the way down |

**250 is full and 0 is death**, and both ends of that are in the capture rather than assumed:
health sits at `0xfa` between fights, falls only when the character is hit, reaches `0x00`,
and is back at `0xfa` after — three deaths and three restores over the round. The ladder
between the ends is `250 → 194 → 160 → 127 → 70 → 14 → 0`, steps of the game's own choosing
rather than round numbers, which is why the values in between are listed but not named.

**Stamina is the second byte, and that half is an inference [I].** What is measured is that
it read `250` in every one of the 3 269 records while the first byte is the one that fell and
rose — health first, the still one second. Calling the second one stamina goes on that and on
nothing else: no record in the capture shows it moving, so `250` is a ceiling it was never
seen to exceed rather than one it was seen to reach. The health half above is **[V]**; this
half is not, and nothing in the code depends on it being right.

`0x0880` is the same two bytes for a second player, one `0x800` bit along — the same bit the
recorded tick stream uses for the second slot. Four other families (`0x006c`, `0x00b2`,
`0x00da`, `0x010c`) are two bytes long and always `fa fa`; `0x00b2` and `0x08b2` do carry
damage (`186`, `58`, `153`, `220`) but never reach zero, so they are **[U]** and are not
claimed as vitals.

**Implemented** as `PlayerVitals` (the values, the 250 ceiling and `IsDead`) and
`PlayerVitalsRecordUtility` (the record), written against the bodies above.
`PlayerVitalsHandler` dispatches `0x0080` and `0x0880` and logs each record at
debug, with a death or a revive logged again at information; nothing is answered.

### `0x090c` is a health/stamina-shaped host record [I]

The steady stream's `0x090c` has the same two-byte body shape and class `3` as the
verified `0x0080` vitals record. Its body is `fa fa` in all **4 298** observations.
This is also the replay parser's `TYPE_HEALTH` shape after its leading `0x03`
discriminator: `[0x03, health, stamina]`. All 4 298 `0x090c` observations are within
1 ms of a server-to-joiner `0x0a61` transmission, tying it to the repeated live-game
stream rather than to roster exchange.

That is a strong format and timing correlation, not proof that `0x090c` is a vitals
update. Unlike `0x0080`, its values never change, including while `0x0080` reports
damage and death. It could be a second actor's full vitals or a heartbeat/state marker
whose bytes happen to match full vitals. Neither the record's actor nor its purpose is
identified by the capture, replay parser, or the currently traced ELF path. Keep the
record as `0x090c [fa fa]` until one of those sources resolves the identity and
semantics; do not substitute a joining player's live vitals.

### `0x0261` and `0x0a61` carry rules-roster-shaped records [I]

The replay parser registers both VIDs as `RULES_VIDS`. For payload type `0x00`, it
parses a compact group/member list: a group marker, zero or more member bytes, `0xfe`
to end that group, and a final `0xfe` to end the list. The UDP tick has already selected
the message type, so its body does not repeat the replay payload's leading `0x00`.
Prepending that discriminator to every captured body makes every one of the
**20 852 `0x0261`** and **15 954 `0x0a61`** records parse exactly under that grammar.
This is a strong cross-source structural match, not a full semantic decoding.

| UDP id | class 0 | class 1 | observed class-0 member sets |
| --- | ---: | ---: | --- |
| `0x0261` | 10 426 | 10 426 | empty 4 395; `{0f}` 272; `{0f,14}` 124; `{04,14}` 429; `{0b}` 56; `{06}` 534; `{14}` 4 532; `{06,14}` 84 |
| `0x0a61` | 7 977 | 7 977 | empty 7 018; `{14}` 862; `{06,14}` 97 |

Every class-1 record is the empty-list body `fe`. Class 0 carries either that same
empty list or a group `ff` followed by the listed member bytes and two `fe` terminators;
for example, UDP body `ff 06 14 fe fe` becomes replay payload
`00 ff 06 14 fe fe`. The parser retains `ff` as a group key and the following bytes as
members. It does **not** name the group or explain why the two UDP IDs have different
membership sets. The values cannot be translated into a team or another game concept
from these bytes alone.

The member-byte values exactly match the five joiners' per-player bytes at profile
offsets `0x05` and `0x07`: `14`, `0f`, `04`, `0b`, and `06`. Those are distinct from the
roster-index field at offset `0x04` (`e2` through `e6`), and the capture shows a
rejoining character can receive a different member byte. This makes them strong
per-player-handle candidates, but does not reveal how the game allocates them or which
handles belong in each group.

**This is not yet enough to replace the captured records with generated ones.** The
GameplayServer's `RosterMember` stores roster slots, not these handles or group
assignments; `PlayerProfileRecordUtility` writes zero at offsets `0x05`/`0x07`.
Roster slots must not be reused as handles, and deriving group membership from roster
order would invent semantics absent from all three sources. The RPDT grammar is
straightforward to encode, but valid live inputs for its members and groups are still
missing. The current `HostStreamFramesUtils` emits only the two empty `0x0a61 fe`
records in its steady frame; it does not emit `0x0261` or the member-bearing updates
seen for either ID in the capture. The present fixed replay is therefore incomplete,
not just semantically opaque.

### The 3D position, and the bit that says a character is dead [V]

The record is a tick record under attribute class `2`, and every character has
one: its identifier is **one above** the identifier carrying its health, so
`0x0080`/`0x0081` are one character's vitals and position, `0x00b2`/`0x00b3`
another's, and so on. The `0x800` bit names a second character rather than a
second copy — the two share no position anywhere in the capture.

It comes in four lengths that place the same five trailing words differently:

| length | facing | z | y | x | second facing | |
| --- | --- | --- | --- | --- | --- | --- |
| 16, 17, 18 | 4 | 6 | 8 | 10 | 12 | walking; 14 748 of the 15 142 alive records |
| 32 | 20 | 22 | 24 | 26 | 28 | **every dead record, and one alive** |
| 38 | 26 | 28 | 30 | 32 | 34 | aiming |
| 44 | **[U]** | | | | | 51 records; no offset in it lands on any player's path |

Each coordinate is a signed 16-bit number **at a tenth of the world unit**. The
scale is not assumed: the 32-byte form carries its position a second time in
single precision at offset 8, and at ten units per step the compressed `x` and
`z` land within 10 units of it — 391 of 442 such records, the 51 exceptions being
the 44-byte form. **`y` does not agree**: it reads 3 800 where the other reads
3 973.7, and it holds still while the other moves. Which of the two is the
height is **[U]**; the coordinate is carried either way.

**Bit 1 of the first byte is death.** Every first-byte value the capture carries
— `0x01`, `0x20`, `0x21`, `0x41`, `0x61`, `0x81`, `0xa1`, `0xc1`, `0xe1` — has
that bit clear, and the one value that has it set, `0x22`, appears 390 times and
belongs to a character whose health record was reading zero at that moment. The
shape changes with it: dead records are the 32-byte form, alive ones are 16, 38
or 44.

**A dead character comes back at its own spawn point.** Three deaths are in the
capture with the whole sequence visible, and the order is the same every time:

| | player `0x0081` | player `0x0881` |
| --- | --- | --- |
| dies at | t+621.6 s, at `(1 610, 3 800, 35 100)` | t+802.9 s, at `(39 870, 3 800, -24 750)` |
| health back to 250 | t+626.2 s, 4.6 s dead | t+810.6 s, 7.7 s dead |
| position alive again | t+628.6 s at `(-24 500, 2 800, -17 500)` | t+810.8 s at `(29 000, 3 290, 3 000)` |
| its own spawn point | `(-30 000, 1 790, -5 000)` | `(33 470, 3 300, -470)` |

So the character reappears **near its own spawn** 2–7 s after dying — within
about 5 500 units of where it started — and then walks the rest of the way in,
reaching the spawn point itself about 15 s later. That is the expected respawn
behaviour, and it is what makes the spawn points below usable as anchors.

**The spawns are on two sides.** Taking each character's first position after the
round boundary (the capture has one 130 s gap in the position stream, at
t+433–564 s) puts two of them near `x = -30 000` and two near `x = +33 500`,
**63 000 units apart**, each pair within 2 000 units of itself:

| | first position after the boundary | |
| --- | --- | --- |
| `0x0081` | `(-30 000, 1 790, -5 000)` | west |
| `0x08b3` | `(-30 840, 1 790, -10 260)` | west |
| `0x00b3` | `(33 500, 3 290, -500)` | east |
| `0x0881` | `(33 470, 3 300, -470)` | east |

Two and two, on opposite sides — which is what a briefing screen's two teams
look like, and it fits the screen better than anything else in the capture. It is
**[V]** that the split is two and two on this axis and **[I]** that the two
groups are the teams, since nothing in the capture names them. It also bears on
§6.1: the roster entry's `0x10` column splits **three against one**, so that
column is not the team.

**Implemented** as `PlayerPosition` (the three coordinates and the ten-unit
scale) and `PlayerPositionRecordUtility` (the record, the death bit and the
per-length offsets), written against the captured bodies. The 44-byte form is
declined rather than guessed at.

`PlayerPositionHandler` dispatches the six position identifiers the capture
carried — `0x0081`, `0x0881`, `0x00b3`, `0x08b3`, `0x006d`, `0x00db` — and logs
every decoded record at debug in world units, with a death or a revive logged
again at information. The 44-byte form has no position to report, so it is
logged with its bytes instead, which is the only way to see the form during a
session. Registration is on the measured identifiers rather than on the
attribute class, so an identifier the capture never contained still reaches the
"no handler" warning: nothing here predicts a character the capture did not have.

The per-player slot decode `id & 0xFF = 0x75 + 10*slot + OFF[class]` is a **replay-format**
rule. These ids are `0x80`, `0x83`, `0xdd`, `0x0a61` — none is in the `0x75 + 10n` family,
so **that formula does not apply to this capture** and the ids here are not slot-decoded.
**[V]** as a negative.

---

## 6. What this changes

**One defect.** `MessageCodecUtility.ParseMessages` mis-reads every tick record by one byte
(§2). Fixed by the identifier test, with the live frames as the test.

**Four confirmed additions to the join**, all from this capture and all now implemented:

1. The **roster close** `07 00 00 00 00 00 03` — closing a two-entry roster, so not a
   function of roster size. `PlayerProfileHandler` now closes the run with it.
2. The **host's own entry travels under `0x9001`**, not `0x1001`, while the joiners and the
   close travel under `0x1001`. `RoomRosterService.BuildRosterRun()` tags each record, a
   test pins the type of every record in the run, and the handler logs the `0x9001` use.
3. The **empty `0x5001` head** of the roster run, sent as `RosterNotify`.
4. The record **lengths** 83 / 79 / 7 for the two names captured, and that neither captured
   record carries a clan name.

The profile record's name field is the **character** name, not the account name the host
logs in with — the capture shows the joining player's own character name echoed back, and
`ServerOptions` keeps the two apart. `HostIdentityService.CharacterName` now sources
`GameplayServerCharacterName`; it previously read `GameplayServerAccountName` and put an
account name where a character name belonged.

**One conflict resolved.** The live server writes `0xe2 + index` at offset `0x04` and a plain
zero for the host, where the replay-derived layout put `0x32 + index` and `0x31`; and it
writes per-player values at `0x05`/`0x07` that are not the index (§4). **The live capture is
the reference**: the builder, the parser and the test vectors now follow it, and the
replay-derived reading is recorded as superseded rather than kept.

**One earlier reading retracted.** `0x5001` was provisionally read as the running game's
acknowledgement tag. The capture refutes that (§3): its identifier never varies, its fourth
byte is a monotonic counter reaching 194 rather than a send attempt, and its body is always
empty. It stays unimplemented — on better evidence than before, which is to say on evidence
that says *no*.

**One gap left open deliberately.** `0xd001` and the `0x5x..`/`0x1x..` families (§3.1) are
still **[U]**. Nothing was implemented on a resemblance, which is how `0x43CA`/`0x43CB` and
`0x4442` went wrong in this project already.

**Gameplay forwarding, now present but not established as sufficient.** The server relays inbound tick records below `0x1000` to its other established peers, preserving the type, body, fourth header byte and compression choice. Unknown tick identifiers are forwarded too, since the capture has 121 tick types and this server only decodes a subset. The capture shows tick traffic in both directions, but does not prove this relay alone is enough to run a game, that this is the cause of a freeze, or that the dedicated host should synthesize authoritative ticks. The Ghidra check confirms the client serializer and parser shape; it does not identify a host-side simulation requirement.

**The host stream, now replayed.** What the relay above left out is that the recorded host
never stops writing. In the solo window — the thirteen minutes before the second player
arrives at t+154 s — it sends the joining client a stream of its own: 0x090c `fa fa`, two
0x0a61 `fe` per frame at a measured 29 ms, 0x1a54 / 0x9a54 `01 20 0d` and 0x0002 blobs
inserted among them, with a sparse 0xd001 / 0x0002 phase and a **match-start run** at
t+46.4 s (hdr 0x8019, 160 B compressed) opening the round. A server that answers the join
and then goes quiet hands the client an open, silent channel.

`HostStreamFramesUtils` carries the measured bodies and order, and
`HostStreamService` replays them per peer, started once after the roster answer. The sparse
delays are measured from the capture; the steady insertions repeat on measured frame
periods. The compression marker follows the frame's content rather than the phase, as the
capture shows: a frame carrying a state blob goes out compressed (hdr 0x8648, 0x8019) and a
periodic-tick-only or slot-only one uncompressed (hdr 0x0626, 0x062d, 0x0682),
the 22-byte mostly-zero blob
being the only body in the steady stream that pays for it. Every uncompressed frame the
replay writes is **byte-identical in size** to the capture's (17 B control, 28 B base, 35 B
slot, 33 B control-inserted), and every frame round-trips through `FrameCryptoUtility` and
`MessageCodecUtility` with its types, bodies and fourth bytes intact.

The compressed frame sizes now match the captured targets: 25 B for a lone state blob,
160 B for the match-start run, 40 B for the steady zero-valued blob, and 41 B for the
steady 100-valued blob, each with the beat and first tick pair. The encoder emits valid
LZSS and the decoded records survive the normal frame codecs. The steady stream's
**occasional `0x5001` and `0x1001` insertions** (five and one in 81 s) are not reproduced.
And **whether the stream resolves the reported freeze is untested** — it is sent because
the recorded host sends it, on the same grounds as the roster and the burst.

**Not established.** The 8-byte trailer on 290 frames (§2); the meaning of every
`[U]` row in §3; why the three `ver = 6` flows died; whether the stream or tick relay
resolves the reported freeze.

---

## 7. Reproducing this

```
python3 tools/pcap_conversations.py docs/mgo2-game.pcapng      # every flow, by volume
python3 tools/udp_flow_summary.py  docs/mgo2-game.pcapng --peer 99.66.131.177:5731
python3 tools/udp_frame.py        docs/mgo2-game.pcapng --peer 99.66.131.177:5731 --limit 40
python3 tools/udp_framing_solver.py docs/mgo2-game.pcapng       # the per-type vote
python3 tools/udp_framing_table.py  docs/mgo2-game.pcapng       # the 18728/19018 figure
python3 tools/udp_unresolved_probe.py docs/mgo2-game.pcapng     # 0x5001 and 0xd001, in order
python3 tools/udp_pairing_probe.py   docs/mgo2-game.pcapng     # the 0x1x/0x5x negative
python3 tools/udp_join_replay.py     docs/mgo2-game.pcapng     # the join, frame by frame
```

`tools/pcap_conversations.py` is a stdlib pcapng reader — there is no tshark on this
machine, and none is needed. The session key is derived from the two handshake counter
bases rather than configured, so the tools work on any capture of this protocol.