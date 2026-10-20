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

The tick form is the one `tools/mgo2_replay_parser.js` already walks — *"`len` is
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

**The client's own registry is now read, and it settles which of these ids are its.**
A live client's inbound table holds **18 vids** — `0x001`, `0x002`, `0x250`, `0x252`,
`0x254`, `0x255`, `0x256`, `0x258`, `0x25A`, `0x25C`, `0x25D`, `0x261`, `0x265`–`0x268`,
`0x2DE`, `0x2DF` — each with the `0x800` bit set in the engine's own numbering
(`docs/analysis/udp-record-dispatch.md` §4.1). Two consequences for the [U] rows below:
`0x1100` and `0x1101` are the only ids here whose **vid** the client does not register
(`0x100` / `0x101`), so they are not records these two clients' own tables answer for;
and the registry lists `0x1252`, `0x1255`, `0x125c`, `0x125d` and `0x1265`–`0x1268`, which
this capture inventory never shows — the inventory is what a capture carried, the
registry is what the client accepts, and the second is the larger set.

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
| `0x12de` | 23 | 2 | 2 / 6 / 14 | join report / match-start payload — §3.2 |
| `0x1a50` | 467 | — | 6 / 9 / 10 | **[U]** |
| `0x1a54` | 277 | 7 | 2 / 3 / 7 / 90 | **[U]** |
| `0x1a58` | 8 | — | 2 | **[U]** |
| `0x1a5a` | 374 | — | 6 | **[U]** |
| `0x1ade` | 29 | 1 | 1 / 2 / 6 / 14 | join report / match-start payload — §3.2 |
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
| `0x9250` | 10 | — | — | **[U]** |
| `0x9254` | 139 | — | — | **[U]** |
| `0x9256` | 3 | — | — | **[U]** |
| `0x92de` | 3 | — | — | **[U]** |
| `0x9a50` | 13 | — | — | **[U]** |
| `0x9a54` | 94 | 1 | — | **[U]** |
| `0x9a5a` | 2 | — | — | **[U]** |
| `0xd001` | **176** | 5 | 1 | **[U]** — see below |
| `0xda54` | 1 | — | — | **[U]** |

**Five of the `[U]` rows above are named by the replay parser's own grammar**, which
reads a session record's identifier low eleven bits as a stream and the payload's first
byte as an operation in it. `0x1a54` / `0x9a54` (`0x1254` / `0x9254` with them) are the
operations stream, low `vid = 0x254`: a three-byte `01 20 0d` is opcode `0x01`, the round
clock — 3 360 units of 125 ms, 420 s, the same value in every one of them — and a
ninety-byte body is opcode `0x00`, a state block whose two objective holders read
`0xff`, nobody. `0x1a50` / `0x9a50` / `0x1a5a` / `0x9a5a` are its world objects, low
`vid = 0x250` / `0x25a`, and the capture's head records are the parser's "a box appears"
case — code `0x74`, boxes `n = 1` / `2`, items `0x8c` / `0x71` — with position records
that read as world coordinates. The bodies fit that grammar byte for byte; the class bits
and the rest of each stream stay **[U]**. See
[the replay crosswalk](RPDT_REPLAY_RECORDS.md) for the rows.

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

## 3.2 `0x1ade` / `0x12de` — the client reports its join, and the host answers with the run [V] as a pairing, **[I]** as a meaning

The one `0x1x..` / `0x5x..` pairing in §3.1 that **does** hold is the one §3.1 did not
test, because it is not two families but one record read in two directions. The round's
payload record is also how a client announces its **join**: the room's map is loaded and it
is on the briefing screen. It is **not** the player's readiness — that is
`1f <slot> 01` on the operations stream, the record a room's round is opened on
(`MatchRecords/BriefingReadyBodiesUtils`, `MatchStartRunService.OpenWhenEveryPlayerIsReady`),
and the two are up to a minute apart in the capture that holds both
(`mgo2-game-joined-when-red-team-then-briefing`: the client's report at t+57.656 and its
ready at t+112.750, with the round's run at t+113.758).

```
IN  0x1ade  len=2  ord=0   e1 14        the client reports its join: opcode e1, then its own slot
OUT 0x5ade  len=0  ord=0                the record-level ack (0x1ade | 0x4000)
OUT 0x1ade  len=6  ord=0   e0 14 00 00 00 00   the host's payload: opcode e0, the same byte back
OUT … the 13-record match-start run, compressed
```

`e1` and `e0` differ only in that byte, and the byte after it is **echoed**: every
report/payload pair in the captures agrees on it — `e1 14` is answered `e0 14 …` in game1,
`e1 01` is answered `e0 01 …` in game2. The identifier follows the round, so the report
arrives under `0x1ade` in one round and `0x12de` (`| 0x0800`) in the next; a live client
additionally sets the payload-class bit and sends `0x9ade`.

**The parity is the client's, and the first round is not always `0x1a54`.** Every round in
the captures plays under the set a join report of its own arrived on — five reports, five
rounds, no disagreement — and the set is not a function of the round number: game1's first round opens
on the report `0x1ade` under `0x1a54`, and game2's first opens on `0x12de` under `0x1254`.
Read in capture order the eight rounds the two files open alternate without a break —
`0x1a54`, `0x1254`, `0x1a54`, `0x1254`, `0x1a54` (game1, t+46.4 s to t+765.8 s), then
`0x1254`, `0x1a54`, `0x1254` (game2, t+57.4 s to t+224.6 s) — so the client carries the
parity from the rounds it has already played rather than resetting it with the match. **[V]**
on the measurement; that the parity is a client-side counter is the reading **[I]** that
fits it. A host that opened every first round as `0x1a54` would answer game2's client with a
`0x5ade` marker where that client asked under `0x12de`, and would stream the round under a
set the client is not on, so a host takes its first round's set from the report
(`MatchRecordGenerationUtils.OfJoinReport`) and alternates from there.
`MatchStartRunService` opens its round that way.

**The report never arrives alone: it is paired with a one-byte record.** In all
five reports of the two captures the client puts a second record in the same
datagram, immediately before the report, on an identifier that splits the same way
the report's does and carrying the report's own byte:

| capture | report frame reads |
| --- | --- |
| game1 t+44.76 | `0x0a5c` len 1 class 4 `14` · `0x1ade` len 2 class 0 `e1 14` |
| game1 t+190.40 | `0x025c` len 1 class 4 `14` · `0x12de` len 2 class 0 `e1 14` |
| game2 t+56.72 | `0x025c` len 1 class 4 `01` · `0x12de` len 2 class 0 `e1 01` |
| game2 t+107.98 | `0x0a5c` len 1 class 4 `01` · `0x1ade` len 2 class 0 `e1 01` |
| game2 t+223.63 | `0x025c` len 1 class 4 `01` · `0x12de` len 2 class 0 `e1 01` |

Five of five, both captures, one rule. The host answers the companion in four
bytes under class 6 — game2 in every frame that carries it (`02 00 00 00`, its
minimal run included), game1 only in the steady stream and with six different
bodies (`00001000`, `00801000`, `00881000`, `40080000`, `40081000`, `10800000`),
never inside its round start. **[V]** the length, the classes and the pairing;
**[U]** the bodies and the meaning.

**Both identifiers decompose to one stream.** The replay parser reads a record's
low eleven bits as its stream (`low = vid & 0x7ff`) and names that family:
`0x254` the operations stream — the round clock `0x1a54` and its sibling `0x1254`
— `0x250`/`0x25a` the world objects, `0x256`/`0x257` targets, `0x258` notices,
`0x261` the rules (which is why `0x0a61` and `0x0261` are one pair), `0x267`–`0x26b`
gauges. The companion's two identifiers are `0x25c` under both, so the `0x0800`
bit is the round's generation on it exactly as it is on the report. The parser
has **no branch for `0x25c`** and names no operation on it. **[V]** the
decomposition; **[U]** the meaning.

**The lobby-slot array handshake, which the captures run in lock-step.** Four
record kinds and one loop, identical in both files:

| step | record | body | game1 | game2 |
| --- | --- | --- | --- | --- |
| 1 | host `0x9001`/`0x1001`, 26 B | `85 NN` / `86 NN` | seq `f3` | seq `00` |
| 2 | client `0x9001`/`0x1001`, 2 B | `82 NN` / `84 NN` | t+45.69 | t+0, t+4.24 |
| 3 | host `0x1001`, 2 B class 26 | `81 NN` | t+139.48 | t+47.29, t+49.52 |
| 4 | host, the pair again | `85 NN+1` / `86 NN+1` | `f4` | `01` |

**The confirmation is what advances it.** The client's answer to step 4 is
`82 01`/`84 01` in game2 and `82 f4`/`84 f4` in game1 — the byte after the one the
host confirmed, never the one it is already on. game1's host confirms `f3` 94 s
after the client answered it, when the second player fills a slot; game2's
confirms within 5 s. A host that writes the pair and never confirms gets one
answer and then silence. **[V]** on the loop and the ordering in both captures;
**[U]** why a slot filling is what makes game1's host confirm.

The host also publishes the next sequence beside the confirmation, in a pair of
records ending `0b 00 01 00 00 00 00 08 07 NN` — game1's carries `f4` and
game2's `01`, each the sequence its next pair uses. **[V]**

**The pair names the room's own players, and the host fills it in as they
join.** The bodies are 26 bytes: the type, the sequence, slot 0, and the
twenty-three slots behind it. A slot is marked taken in the occupancy array with
`07`, and the team array's byte at the same slot is then the side — `0` or `1`,
which is the only pair the replay parser's `teamArray()` accepts — while a free
slot is `7f`/`ff` and slot 0 is the host's own sentinel `fe`, held out of both.
Read out of game1, whose round is the long one. The times are §4's — counted
from the handshake of the peer the pair is written to, which is `10.2.0.2:5730`
for all of these:

| t | occupied slots, as (slot, side) |
| --- | --- |
| +46.4 | none — the run, and the three repeats behind it |
| +73.4 | `(14, 0)` |
| +185.2 | `(14, 0)`, `(15, 1)` |
| +220.2 | `(04, 1)`, `(14, 0)`, `(15, 1)` |
| +396.1 | `(04, 1)`, `(0b, 0)`, `(14, 0)`, `(15, 1)` |
| +442.7 | none again |
| +449.0 | `(0b, e0)` |
| +506.0 | `(06, e0)`, `(0b, e0)` |
| +517.5 | `(06, 0)`, `(0b, 1)`, `(14, 1)` |

The slots are the five joining players' keys in the 24-entry player table — `14`,
`0f`, `04`, `0b`, `06`, which are the ones §8.4 reads out of the same capture's
roster records, the host's own slot 0 excepted — and the sides are the raw `0`
and `1` a two-team room has. The pair is therefore a **live statement about the
room** and not a per-round constant: it gains a slot as a player joins, it goes
back to no slot taken at +442.7, it writes `e0` at +449.0 and +506.0 for slots it
will not name a side for yet, and at +517.5 it gives slot `14` the *other* side.
The `e0` pair follows the empty one **6.3 s** later, and the sides are named
**68.5 s** after that: a host un-marks every slot and re-marks the room with no
side before it settles the teams it will name. Nothing here says *why* those
three are 396 s into the round, so the sequence is recorded and not explained.
**[V]** for the marking, the sides and the `e0` placeholder; what the client does
with a slot that is not its own peer is **[U]**.

**A pair is told by its two index bytes, and they must differ by one.** The
fourth byte of each array's record is its index; in all three captured runs the
team array carries `07` and the occupancy array `08`, and the later pairs carry
the next values of the same counter — `0c`/`0d`, `15`/`16`, `1e`/`1f` for game1.
The replay parser pairs two arrays only when their times agree, their index bytes
are consecutive and their sequence bytes are equal, so a host that wrote the same
index on both records would publish a pair nothing can pair. **[V]**

**A scan of `MGO2.ELF` does not name the sender.** No site constructs either
identifier as a whole immediate (`0x1ade`, `0x12de`, `0x0a5c`, `0x025c`, `0x0a61`
all score zero against `li`/`addi`/`ori`/`lis`), so the ids are read from a table
and a value scan cannot reach them. The three `li … 0x25c` sites that exist —
`0x000a3e44`, `0x000a412c`, `0x000a4e5c` — are byte-identical argument setups for
one call (`r6 = 0`, `r7 = 0xff`, `r4 = 0x25c`, `r5 = 0`, `bl 0x2792f8`), and
`0xff` beside `0x25c` is a render setup, not a record id. The record's *sender* is
therefore still **[U]**; what the captures settle is its pairing, its identifiers
and its direction.

**The answer is written once, with the round.** The `0x5ade` above is the report's own
identifier with the answer bit set, so the record that opens the round and the answer to
the report are the same one, and the recorded host writes it once: game1's solo round
carries a single host `0x5ade`, at t+46.4155 and in the round's opening burst, and no
host frame between the report (t+44.76) and that burst carries it. Answering the report
in the pass that received it as well leaves a second `0x5ade` on the wire; see
`DATA_PHASE_DELIVERY.md`, which carries the delivery rule built on this pairing.

**What the client means by it is [I], not [V].** The pairing and the timing below are
measured. That the client's intent is specifically *"my map has loaded and I am ready"* is
the reading, and it is the one that makes the sequence coherent — a client that cannot
play yet says so, and the host starts the round for it. Nothing on the wire names it, and
the alternative reading (the client commanding the round) fits the same bytes. What the
server must not do is either way: it opens the round on this record and not on a clock.

### The report precedes every round, and no round opens without one [V]

The gaps are 0.6–5.5 s, which is processing, not scheduling:

| capture | report | round open | gap |
| --- | --- | --- | --- |
| game1 | `0x1ade e1 14` t+44.76 | t+46.42 | 1.66 s |
| game1 | `0x12de e1 14` t+190.39 | t+194.04 | 3.65 s |
| game2 | `0x12de e1 01` t+56.72 | t+62.18 | 5.46 s |
| game2 | `0x1ade e1 01` t+107.98 | t+109.12 | 1.14 s |
| game2 | `0x12de e1 01` t+223.63 | t+224.62 | 0.99 s |

**The round's clock is not the trigger.** game1's first run is at roster+42.25 s and its
first report at roster+40.59 s, close enough that a replay keyed to the roster could open
a round unprompted and look right — which is what this server did, and why a live client
sat in its ready state reporting twenty-three times over two minutes with every one logged
`no handler` and relayed to a room of one. A server that opens on a timer and never reads
the report gets the timing right and the behaviour wrong.

A client re-reports until it is answered (game1 reports twice, 2.29 s apart, and gets one
round), so the report is latched rather than counted: `MatchStartRunService` holds the
round open for as long as that round's own frames are going out and treats a report inside
that window as the same round. game1's next report comes 145.6 s after the first, long
after the first round's run had finished repeating, so it opens the next one.

What the echoed byte *means* stays **[U]**. game1 sends `0x14` and game2 `0x01` on the same
record, so it is not a constant.

**The fourth byte is the index the byte is read under, and the host rewrites it through
the round.** Sweeping the record in all three files rather than only reading the report
and its answer shows the host carrying values beyond the echo: game1 writes `e0 14` under
fourth byte 0 and `e0 0f` under fourth byte 1, and later `e0 0b c3 cf 00 00` under fourth
byte 7; game3 writes `e0 05` under 0 and `e0 0f` under 1 and `e0 0d 12 8a 00 00 00` under
2; game2 writes `e0 01` under 0 and `e0 02` under 1. The slot is therefore a value the
host owns per fourth byte, which the client's two-byte report names and the run's payload
echoes — not a round number and not a constant. **[V]** that it varies this way; what any
of the values counts is **[U]**. `MatchStartBodiesUtils.MatchStartPayload` carries the
client's own byte back rather than a value this server would have to invent.

**The payload's length is not settled, and the trailing bytes are zeros in every form.**
game1 answers in **six** bytes (`e0 14 00 00 00 00`) and game2 in **five**
(`e0 01 00 00 00`), on the same record and the same two-byte report; game1 later grows the
same record to **fourteen** bytes mid-round (`e0 06 00 00 00 00 …`). Nothing measured
separates the game1 and game2 forms — both captures hold four roster entries and neither
marks a slot occupied in the array that opens the run — so the rule is not a player count,
a slot count or the round length. The captures of a **fresh room's briefing**, which is
what this host serves first, hold an **eight**-byte form:
`mgo2-game-started-on-briefing` answers `e0 01 00 00 00 00 00 00` at t+56.123, and
`mgo2-joined-game-in-progress-last-round` and
`joined-final-round-solo-then-final-round-started-when-other-player-joined` grow the same
zeros further. `MatchStartBodiesUtils.MatchStartPayload` writes that eight-byte form, in
the run and in the publication beside the report. **[U]** for the length's meaning; the
trailing bytes themselves are **[V]** zeros.

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
  high byte reads `0x01` on all five of them and `0x00` on the host's own entry, whose id is
  `0x0000a001`. "Zero on the host, one on every joiner" is what the high byte of a u32 looks
  like; there is no field there at all. The code no longer writes one.

The host's own entry's value is **not [U]** either, and it is the same field as every other
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
and four of its columns remain **[U]**. The record itself is now **decoded** — the
`0b` payload is the replay console's `TYPE_SETTINGS`, the host's rotation and each
player's appearance and skills; see `UDP_SETTINGS_PAYLOAD.md`.

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

Yet **only two datagrams in the whole round carry the host's own `07 4c` entry**, both at
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

**The state blob is a one-field state transfer.** All 141 `0x0002` records in the
round are 22 bytes and all go server to client; with the class byte the `len` byte
counts, the payload is 23 bytes and parses exactly: one slot-state index, a 21-byte
presence map and one byte for the field the map's single bit names — field 150 of the
shared 164-field table, `state+287`, one byte. The `0x40` at body offset 18 is that
bit (`150 >> 3 = 18`, `1 << 6 = 0x40`); the map is read low bit first, which the
dispatcher `0x27E8B0` says in its own instructions. The last byte is the field's
only moving part: 100 for 56 of them, then 0, 94, 98, 99, and the 50 this burst
carries. `PlayerSettingsBurstService.BuildClosingStateBlob()` reproduces the
join-time blob exactly. The field is the host's per-player figure and what it
measures is **[I]**; the whole derivation is
`docs/analysis/join-stall-after-mode2.md` §28.7. **[V]** for the parse, **[I]** for
the name.

**`0xd001` is mostly the host's, not the peer's.** The round holds 181 of them:
**176 server to client, 5 the other way.** It was registered receive-only here on
the strength of the 5. Two go out inside this burst, with bodies `24` and `02`. **[V]****The payloads decode, and they are copied rather than built.** The `0b <slot>` bodies run 6 to 161 bytes and hold one character's equipment, skills and settings, which is the replay console's `TYPE_SETTINGS` record: slot `0x00` with 71 bytes or more is the host's own rotation of fifteen map/mode entries, and a slot of `0x01` or more with a length in 50…58 is a player's loadout (`UDP_SETTINGS_PAYLOAD.md`). The first four here match the replay's opening burst byte for byte — `0b 00`/161, `0b 01`/50, `0b 15`/54 twice, including the `45 2f 57 39 67 68` template run at body offset `0x1b` of `0b 01` — so a replay is a source for live payloads, not only for framing (`UDP_P2P_PROTOCOL.md` §6.6). `PlayerSettingsBurstService` no longer sends those recorded bodies as they stand. The rotation record's fifteen triples are written from the room's own rotation (`MatchSettings.Rotation`, `GAME_SETTINGS_WIRE.md`), because that is a row this host holds and the joiner has to be told which rounds the room plays; and each loadout's own fields — the slot byte, the side, the trailing appearance block and the skill pairs — are written from that player's character rows (`characters_appearance` and the equipped skills), because those are rows this host holds too. What nothing names in a loadout body stays the recorded body's, which is why the burst carries a recorded body of the player's own shape behind the fields it writes; `UDP_SETTINGS_PAYLOAD.md` carries the field-by-field account. The tag-plus-slot-plus-zeros body these replaced was a shape no peer had been shown to read. **[V]** for the reading and for the fields written; **[I]** for the two unnamed bytes a body keeps from the template.

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
`MatchRecordFramesUtils`; by themselves, the capture framing and generic P2P codec
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
four sizes `mgo2_replay_parser.js` maps to the compressed position and the yaw
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
`PlayerVitalsHandler` dispatches `0x0080` and `0x0880`, keeps the last value of
each player (`PlayerVitalsService`, whose entry the session reaper clears with
the player) and relays the record to the room's other peers as the unbound tick
path did before it, so a client still sees every other player's health. A change
in the value is logged at information; nothing is answered.

### `0x090c` is the addressed player's vitals slot [V]

**It is not a fixed identifier but a player's own record block, one below that
player's position record** — which is what resolves the actor an earlier reading
of this record left open. A player's tick records are one block of six, and the
replay parser's role table is that block: `role.set(vid, TYPE_POS)`,
`vid + VID_STATE` (`-2`), `vid + VID_HEALTH` (`-1`), `vid + VID_WEAPON` (`+2`),
`vid + VID_AIM` (`+4`) and `vid + VID_DAMAGE` (`-5`), so a health sample is filed
under its position stream's track `+1` (`rpdt.js`;
[RPDT_REPLAY_RECORDS.md](RPDT_REPLAY_RECORDS.md)). The captures agree record for
record, class byte and all — a tick record's class byte *is* its payload type —
and in the same `0x800` half the client's own block uses:

| capture | the player's own block (client → host) | host → client |
| --- | --- | --- |
| `mgo2-game1` | `0x090b` state, `0x090d` position, `0x090f` pair, `0x0911` aim | `0x090c fa fa` from t+73.4 s; and `0x010c fa fa` while the client's own block is `0x010b` |
| `mgo2-started-briefing-then-solo-game` | `0x0861`, `0x0863`, `0x0865`, `0x0867` (classes `1`, `2`, `6`, `5`) | `0x0862 fa fa` (class `3`) |
| `mgo2-started-on-solo-game` | `0x8063` position, the `0x8000` form of its own | `0x0062 fa fa` |
| `mgo2-game3` | `0x08e3`, `0x08e5`, `0x08e7`, `0x08e9` | `0x08e4 fa fa` |
| `mgo2-game2` | `0x004d`/`0x084d`, `0x004f`/`0x084f`, `0x0051`, `0x0053` | `0x004e`/`0x084e fa fa` |
| `joined-final-round-solo-then-final-round-started-when-other-player-joined` | `0x0075`/`0x0875`, `0x0077`/`0x0877`, `0x0079`, `0x007b` | `0x0076`/`0x0876 fa fa` |

Seven sessions and no counterexample: the identifier is the state record `+ 1`,
which is the position record `− 1`, and its `0x800` bit is the same round bit
the client's own block carries (`MatchRecordGenerationUtils`, which already flips
`0x090c`/`0x010c` by round). The block's low byte is a **per-session allotment**
(`0x0b`, `0x4d`, `0x61`, `0x75`, `0xe3` across five sessions) that nothing in the
handshake carries — the client's peer id is `0x0001232f` in every capture — so
which low byte a session uses stays **[U]**, and a host replaying a recorded
session can only carry that session's.

**What does not change.** The body is `fa fa` in all **4 298** observations of
`0x090c`, and every one is within 1 ms of a server-to-joiner `0x0a61`
transmission — the replay parser's `TYPE_HEALTH` shape after its leading `0x03`
discriminator, `[0x03, health, stamina]`. Unlike `0x0080` the value never moves,
including while `0x0080` reports damage and death, so what is established is the
record's **slot**, not a live vitals input: do not substitute a joining player's
live vitals into it. This server reads the players' own `0x0080` values into
`PlayerVitalsService` for its own use and leaves this record as the capture
writes it.

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
missing. The current `MatchRecordFramesUtils` emits only the two empty `0x0a61 fe`
records in its beat frame; it does not emit `0x0261` or the member-bearing updates
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

**Gameplay forwarding, now present but not established as sufficient.** The server relays inbound tick records below `0x1000` to its other established peers, preserving the type, body and fourth header byte (the relay frame is written plain). Unknown tick identifiers are forwarded too, since the capture has 121 tick types and this server only decodes a subset. Nothing is answered in return, and a peer that has not finished its handshake is not a recipient yet. The relay lives in `GameplayServerService.FrameDispatch`'s record loop; before it, an unhandled tick record was dropped quietly, so two joined clients could not see each other move — the capture cannot refute that (each of the three recordings has exactly one joiner), but the client does send its own `0x810d` / `0x890d` movement records to the host, 4 131 and 578 of them in game1, and a dedicated host forwards what it does not answer. The capture shows tick traffic in both directions, but does not prove this relay alone is enough to run a game, that this is the cause of a freeze, or that the dedicated host should synthesize authoritative ticks. The Ghidra check confirms the client serializer and parser shape; it does not identify a host-side simulation requirement.

**The host stream, now replayed — and re-derived.** What the relay above left out is that
the recorded host never stops writing. In the solo window — the thirteen minutes before the
second player arrives at t+154 s — it sends the joining client a stream of its own: 0x090c
`fa fa` and two 0x0a61 `fe` in every beat frame at a measured **30.0 frames a second, 33
ms**, a 0x1a54 / 0x9a54 operations record and a 0x0002 blob inserted among them, with a
sparse 0xd001 / 0x0002 phase and a **match-start run** at t+46.4 s (hdr 0x8019, 160 B
compressed) opening the round. A server that answers the join and then goes quiet hands the
client an open, silent channel.

**The period is the one measured quantity that moved.** Re-measured against this capture,
the beat is 33 ms and not 29: 2 478 vitals-carrying frames over 82.57 s, and 3 255 counter
steps over 109.58 s, both read 30.0 a second, and the replay console's own parser reports
**30.303 Hz** for three of the checked-in recordings. The 29 ms reading is the one thing
the file's timestamps could not settle, because a burst's frames are written under a single
timestamp — most host frames are 11–12 µs apart there and physically are not — so the
period has to come from the count over a span.

`MatchRecordFramesUtils` carries the measured bodies and order, and `MatchRecordService`
replays them per peer, started once after the roster answer. Every recorded body it writes
was checked byte for byte against the capture: the ninety-byte operations state block, the
two 26-byte entries, the close **triple** — the pair and the seven-byte roster close behind
them (§3.2) — the 0x1ade payload, both state-blob values, the
operations clock body `01 20 0d` and the beat's `fa fa` pair all match, and the sparse
phase's delay-and-body steps land on the capture's own frames (4.933 s, 9.729, 12.129,
12.130, 14.500 … 42.246). The insertions repeat on the times measured in the beat: the two
clock forms together every 1.48 s, a state blob every 5.98 s, a control every 4.3 s.

The compression marker follows the frame's content rather than the phase, as the capture
shows: a frame carrying a state blob goes out compressed (hdr 0x8648, 0x8019) and a
clock-only or control-only one uncompressed (hdr 0x0626, 0x062d, 0x0682), the 22-byte
mostly-zero blob being the only body in the stream that pays for it. Every uncompressed
frame the replay writes is **byte-identical in size** to the capture's (17 B control, 28 B
base, 35 B clock-inserted, 33 B control-inserted), and every frame round-trips through
`FrameCryptoUtility` and `MessageCodecUtility` with its types, bodies and fourth bytes
intact. The compressed frame sizes match the captured targets too: 25 B for a lone state
blob, 160 B for the match-start run, 40 B for the steady zero-valued blob, and 41 B for the
steady 100-valued blob, each with the beat and first rules-roster pair.

**What the replay reproduces from the run on.** The match-start run is written **five
times** in the 12.5 s after it — frames 25, 81, 137, 193 and 249, every 56 frames (1.85 s)
— once whole and four times as the 11-record form with the two `0x5001` heads dropped and
the operations pair moved behind the roster records. The item-box world objects of §3 ride
in **one beat frame in every eight** (0.26 s) from 6 frames after the run, as the two boxes'
state under `0x1a50` / `0x1a5a` (the box's index and five zero bytes). And the **vitals
record joins the beat frame only from t+73.4 s** — 27.0 s, 818 beat frames, after the run —
the rules-roster pair alone carrying the round's first 27 s, which is what the replay now
writes.

**A round ends and the next one opens.** The capture's first round runs from its run at
t+46.42 s to the next round's run at t+194.04 s — **147.6 s** — and ended when a team
reached the goal, not on the round's timer: the state block's own clock is 3 360 units of
125 ms, **420 s**, and no clock record in the round ever moves off it. The second round then
plays the same stream under the **sibling identifier set** — every identifier a 0x800 bit
along: operations `0x1a54` → `0x1254`, payload `0x1ade` → `0x12de`, rules `0x0a61` →
`0x0261`, vitals `0x090c` → `0x010c`, marker `0x5ade` → `0x52de`. A host swaps to the
sibling set when it opens its next round; the replay opens none, so `MatchRecordService`
holds the first set for the whole stream.

**The run is written for the client's join report and the round opens on the room's
readies.** §3.2 measures the pairing: every round in every capture begins within a second
or so of a client's own record, and its run is answered to that client's `e1 NN` report on
the round's own payload record. The set the round plays under is the one that report
arrived on, which is why the first round's is read from the report rather than fixed
(§3.2). What the host opens the round *on* is the other record: in
`mgo2-game-joined-when-red-team-then-briefing` the room's last player readies `1f 05 01`
at t+112.750 and the round's run is on the wire at t+113.758, one second later, where that
same client's join report was 55 s before it. So the sparse phase stops at its last state blob and
the writer of the capture then writes the marker and the match-start run, and writes the run
again four times *inside* the beat that follows (one every 56 frames, counters
32849/32905/32961/33017) — echoing the byte the client sent, as every captured payload does.
The repeats are never sent ahead of the beat: the recorded host keeps the 33 ms frame moving
after the run and writes the run-shaped frame in place of the beat at those four indices.
**None of it is written by the replay.** `JoinReportHandler` recognises the report and
answers nothing, the marker and the run are not built, and the sparse phase runs straight
into the beat, which holds the first identifier set for the whole stream. See
`DATA_PHASE_DELIVERY.md`, "The round start is not written", for what a live client does with
a run written before its report; the 147.6 s goal-ended length is kept as a measurement of
the capture and decides nothing here.

**What the replay still leaves out**, measured now rather than assumed:

- The item boxes' **later** events: the round's opening pair is replayed (below), but the
  events that re-place a box after it is taken arrive irregularly around the eight-frame state
  group and are the live round's own play, so they are not.

**The round's opening item-box events are replayed too [V].** The group's real content is not
its six-byte pair: a box is *created* by an event of four records under `0x1a50` — sub-type
`00` the head (`00 74 01 00 00 00` and `00 74 02 00 00 00` for the two boxes), `01` the item,
`02` a ten-byte position, `03` a nine-byte tail — and the all-zero pair the replay console
calls the filler is the statement that a box is still there. Measured from game1's first
round, decoded with `tools/udp_frame.py`: the host writes its two boxes' events in the round's
first two groups, 0.2 s and 0.46 s after the run, and the filler pair alone every group after
them — `0x801f` carrying `0x1a50` ordinals `0`-`3` and `0x1a5a` `0`-`1`, `0x8027` carrying
`4`-`7` and `2`-`3`, and `0x802f` onward the filler pairs at `8,9` / `4,5`, `10,11` / `6,7`, …,
so one counter per identifier advances by the records each group wrote. Reproduced byte for
byte by `ItemBoxFramesUtils.ItemBoxGroup` and its four-record opening groups.
- The second round's extra state group (`0x00d9`–`0x00df`), its member-bearing rules body
  (`ff 0f fe fe`) and its 25-byte roster entry, which the sibling identifier set alone does
  not account for.
- The occasional 0x5001 / 0x1001 insertions (five and one in the run's first 81 s), the
  0x0a5c / 0x1a58 records seen once each, and the per-player position, objective and
  0x1a50-spawn frames the host's own character produces.
- The steady control's cadence is the capture's mean, not a period it holds: over the solo
  window its gaps run from 1 to 337 frames between one insertion and the next.

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
python3 tools/pcap_gameplay_log.py   mgo2-game1.pcapng --limit 20  # as the server logs it
python3 tools/pcap_roster_acks.py    mgo2-game1.pcapng        # §8.6, roster acknowledgements
python3 tools/compare_roster_exchange.py   # §8.6, ours beside the reference's
```

`tools/pcap_conversations.py` is a stdlib pcapng reader — there is no tshark on this
machine, and none is needed. The session key is derived from the two handshake
counter bases rather than configured, so the tools work on any capture of this
protocol.

---

## 8. What the second and third captures map

`mgo2-game2.pcapng` and `mgo2-game3.pcapng` are further rounds against a **second
dedicated host** (`143.47.227.126`, ports `5721` and `5720`) with the same joiner
character as `mgo2-game1.pcapng`. They were decoded with the same
`tools/udp_frame.py` walk of the handshakes and the roster records, and they
settle three fields this server writes without knowing what they are. Everything
in this section is measured from the three captures; nothing is inferred from
plausibility. It reproduces with
`python3 tools/map_roster_fields.py mgo2-game1.pcapng mgo2-game2.pcapng mgo2-game3.pcapng`,
which prints every handshake, roster entry and rules-roster record beside the
character it belongs to.

### 8.1 The handshake `peer_id` is the character id, on every party [V]

| capture | party | `peer_id` | that character's roster entry |
| --- | --- | --- | --- |
| game1 | joiner `10.2.0.2:5730` | `0x0001232f` | `MyPlayerName`, `0x0001232f` |
| game1 | host `99.66.131.177:5731` | `0x0000a001` | the host's own entry, `0x0000a001` |
| game2 | joiner `10.2.0.2:5730` | `0x0001232f` | (same character) |
| game2 | host `143.47.227.126:5721` | `0x00012448` | — |
| game3 | joiner `10.2.0.2:5730` | `0x0001232f` | — |

The joiner's `peer_id` is byte-for-byte its own character id, and the host's is
byte-for-byte the character id written in the host's own entry — including in game2, where the host
is a different character from game1's. So `peer_id == character id` holds on both
roles and across rounds, which is what `P2P_CONNECT_FSM.md` §7.2 requires of our
reply. **[V]**

### 8.2 The host may advertise no endpoints at all [V]

`mgo2-game2.pcapng`'s host answers the joiner's 44-byte handshake with a **32-byte
reply that carries `count = 0`** — no endpoint entries at all, capability byte
`03` — and the round still ran. `mgo2-game3.pcapng` has no host reply at all.

This retracts the reading that our reply's duplicated public endpoint is what
stops the join. The reference host in game1 advertises a public/private pair and
this one advertises nothing, so a working join does not depend on the endpoint
pair's contents, and a decode that assumes a 44-byte, two-entry reply simply finds
only the joiner's half of game2 — which is the note §7.1c already carries beside
these captures. The endpoint divergence recorded in `P2P_CONNECT_FSM.md` §7.1b
stays a difference from the reference, but it is **not** the gate. **[V]**

### 8.3 The handshake role field is `2` for a joiner and `1` for a host [V]

The u16 at handshake `[19..21)` — `handshake_layout.py`'s `mode`, read from
`module_base + 0x7a` — is **not** a per-build constant. Measured on every 44-byte
handshake in the three captures:

| capture | party | capability `[18]` | mode `[19..21)` | count `[21]` |
| --- | --- | --- | --- | --- |
| game1 | joiner | `02` | **2** | 2 |
| game1 | host | `02` | **1** | 2 |
| game2 | joiner | `02` | **2** | 2 |
| game2 | host | `03` | **1** | 0 |
| game3 | joiner | `02` | **2** | 2 |
| game1, three dead flows | joiner re-dials | `06` | `2` | 2 |

The joiner is `2` and the host is `1` in every readable handshake, on both hosts;
the three one-sided flows of game1 (`ver = 6`) are the same joiner re-dialling
with the same value, so the mode does not track the capability byte either.
The live join of 2026-10-08 recorded in `P2P_CONNECT_FSM.md` §7.1b sent
**capability `03`, mode `1`** from the *joining* client, and the same section
traces the promotion path: `0x268f40` reads the session's mode byte, and on `1` it
stores state **3** instead of following the reliable-window path to state **8**.
A client that presents itself with the host's mode is therefore sent down the
branch that never reaches the data phase, which is the `0B09` stall. That reading
was `[U]` when §7.1b was written; the two additional rounds make the value
role-bearing rather than build-constant, and leave the open question narrower:
*what in our server's `0x4321` reply, `0x4313` player list, or the host's own entry tells a
joining client it is the host.* **[V]** for the measurement, **[I]** for the
mechanism.

### 8.4 The roster handle is a per-session host-side allocation [V]

The byte at roster offsets `0x05`/`0x07` (`PlayerProfileRecordUtility.PlayerValue`,
which we write as zero) and the member bytes of the `0x0261`/`0x0a61` rules-roster
records are the same value. Across the readable rounds:

| character | round | handle |
| --- | --- | --- |
| host's own entry (`0x9001` head) | game1 | `0x00` |
| host entry | game2 | `0x02` |
| `MyPlayerName` | game1 | `0x14` |
| `MyPlayerName` | game2 | `0x01` |
| `SOLID KYO!` (first join) | game1 | `0x0f` |
| `SOLID KYO!` (rejoin) | game1 | `0x06` |
| `Libertine` | game1 | `0x04` |
| `Pancor3000` | game1 | `0x0b` |

and the member sets the rules-roster records carry are exactly the handles
currently live: `{14}`, `{14,0f}`, `{14,04}`, `{0b}`, `{06}`, `{06,14}` in game1,
and `{01}`, `{02}`, `{01,02}` in game2.

Three things follow, and the third is why this server must not fill the field:

- **The same character gets a different handle every session.** `MyPlayerName` is
  `0x14` in game1 and `0x01` in game2; `SOLID KYO!` is `0x0f` then `0x06` in one
  round. It is not a property of the character.
- **It is not the roster index, the character id, the peer address or the port.**
  The five game1 handles (`0x14, 0x0f, 0x04, 0x0b, 0x06`) were checked against
  each of those and match none; the host's own handle is `0x00` in game1 and
  `0x02` in game2.
- **It is allocated per session, on the host side.** No client record announces
  it — the 140-byte join request does not carry it — and the host's roster entry,
  its `0x0261`/`0x0a61` membership sets and its own handle all agree. The
  reference's particular numbers are therefore **not recoverable from these
  captures**. What is recoverable is what the field *is*, and the server now
  writes that: each live player is given its own key in the table's range
  (`0`…`23`, the host taking `0`), fixed for as long as the player stays and
  released when they leave, and the record carries it at `0x05` and `0x07`
  (`RoomRosterService`). The client reads the key it is sent, so a host's own
  allocation is what the field needs; it is not a copy of the reference's.

**What the client binary adds.** The captures leave the *allocation rule*
unrecoverable, but `MGO2.ELF` does name the table the value indexes. The lookup
`0x26d5a8` walks a **24-entry table** of `0x74`-byte records at `net + 0xd4`: each
record's first byte is its key, `0xff` marks an empty entry, and the indexed form
returns the record whose key equals the argument (`lbz r8, 0(r3)` /
`cmpw r8, r10`). The peer pass at `0x280b68` (hook site `0x28137c`) then scans the
keys `0`…`0x17` in order, looks each one up with that call, and reads its options
through `0x27e2b0(key + 1)` — a per-slot store whose `0x104`/`0x108` entries are
the two occupancy masks it tests with `1 << key` and `0x114` a 4-byte id. Every
handle the three captures carry — `0x00 0x14 0x0f 0x04 0x0b 0x06` in game1 and
`0x02 0x01` in game2 — lies in `0`…`0x17`, the table's key range, and none reaches
`0x18`. The byte is therefore the player's **key in the host's 24-entry network
player table** — a per-session player number — which is why it repeats at `0x05`
and `0x07` and matches the rules-roster member bytes. **[V]** for the
disassembly, **[I]** for linking it to the roster byte: no instruction read so far
copies the slot straight into the record. What the captures still do not give
is the **selection rule** — `0x280b68` scans the keys, it does not allocate them,
and `0, 20, 15, 4, 11, 6` in one round follow no order a client announces.

The practical gap that leaves is narrower and now precisely stated: our roster
entries carry each live player's key, but the steady stream still emits only the
**empty** `0x0a61 fe` list, so nothing tells the client which handles are live.
The records it would need are the member-bearing `0x0261`/`0x0a61` ones, which
this server never emits (its steady frame carries only the empty `0x0a61 fe`
bodies). **[V]** for the measurement; whether the client needs them to run a game
is not established by any of the three captures.

### 8.5 The lobby TCP channel, our frames beside the reference's [V]

The same capture also holds the lobby flow the client joined
(`10.2.0.2:65217` ↔ `15.204.239.231:5733`), and decoding it settles what our own
lobby writes differently. The frame layout is the same on both sides —
`cmd(2) | len(2 BE) | counter(4) | checksum(16) | payload(len)`, with the payload
at offset 24 — so the differences below are content, not framing:

| command | reference | ours |
| --- | --- | --- |
| `0x4301` list start | 4-byte `00 00 00 00` | 4-byte `00 00 00 00` — same |
| `0x4302` list page | a **770-byte page carrying many rooms**, each record `63 00 05 <id> <name×16> … 63` | a **55-byte page carrying one room**, the same record shape, ending `63` |
| `0x4303` list end | 4-byte body | 4-byte body — same |
| `0x4312`/`0x4313` details | request `00 05 a8 22`, reply `877` bytes: result, id, name, comment, 372-byte fixed part, then `28`-byte roster entries | request `00 00 00 bd`, reply `400` bytes: the same structure with **one** roster entry, the host (`0000001a` + `server-5731`) |
| `0x4320`/`0x4321` join | reply carries the host's public **and** a distinct LAN endpoint | reply carries the host's public address **in both endpoint fields** |

**The reference packs several frames into one TCP segment; we write one frame per
segment.** Its `0x4301` segment is 850 bytes holding three frames back to back
(`0x4301` 28 B, `0x4302` 794 B, `0x4303` 28 B). Both are legal streams — the client
frames per record, not per segment — so this is recorded as a difference and not
a defect.

**The room-list record shape agrees**, which is the part worth keeping: our 55-byte
`0x4302` page and the reference's per-room records both open with the room id and
carry a 16-byte name, and both end on the `0x63` terminator. And **the details
record places the host first in its roster**, as `RoomBrowserHandlers` intends,
with the host's character id followed by its name — the reference's own record
ends with its roster of `28`-byte entries in the same arrangement. Neither of
these differences explains the role byte in §8.3, which is set from the client's
own room object and not from a byte in any of these replies.

**And it is now traced to its source** (`P2P_CONNECT_FSM.md` §7.1b): the byte is
the client's own role code at `connection+0xa18`, written by the client's own
UPnP/NAT module and read back over an internal message, classified by
`0x00d7d530` into "guest" (`0x30`, `0xd0`) or "host" (`0`, `2`, `0x10`, `0x90`).
No field of this channel takes part in it, so the row above is a difference of
content that stays open for another reason or none. **[V]**

### 8.6 Which roster records the client actually acknowledged [V]

§4 reads the roster as the shape the host answers with, and §8.4 reads the
handle out of it. Neither says whether the client *accepted* any of it — which
is the part `RoomRosterService` depends on, since a joining client re-sends its
profile until the host answers and an unanswered roster is a join that stalls
with nothing logged on either side.

Pairing every roster record against the answers the joiner sent, using the rule
`PeerAcknowledgementUtils` carries (same identifier with `0x4000` set, same
fourth byte, body length following the `0x8000` class bit):

```
python3 tools/pcap_roster_acks.py mgo2-game1.pcapng
```

In **game1** the client's opening answers pair with the host's opening run
exactly: `0x9001` ordinal 0 answered by `0xd001` ordinal 0, and `0x1001`
ordinals 1 and 2 answered by `0x5001` ordinals 1 and 2, 56 ms after the run. The
next exchange, 2.6 s later, is answered the same way — ordinals 3 to 6 in one
frame. Across the round **8 of 18 named roster records were answered and 10 were
not**. Every unanswered one is a `0x9001`; both `0x1001` entries that name a
player were answered. The `0x5001` head and trailer are never answered by
anything, which is consistent with §3's finding that this type names no
sequence.

So the reference client **does** acknowledge the roster it is sent, and does so
selectively: the identifier it opened the exchange with is answered for the
first exchange and not for later ones, while the roster identifier is answered
throughout. That asymmetry is **[U]** — the captures show which records drew an
answer, not what the client decides on.

**Our own roster, measured the same way.** `tools/compare_roster_exchange.py`
reads our run back out of `scripts/run-gameplay-local.sh`'s log and puts it
beside the reference's:

| | reference host | ours |
| --- | --- | --- |
| run | `0x5001` `0`, `0x9001` `0`, `0x1001` `1`, `0x1001` `2` | same four records, same ordinals |
| repeat | `0x9001` `0`, `0x1001` `1`, `0x1001` `2` | same three records, same ordinals |
| own entry | `0x9001`, **83 bytes** | `0x9001`, **80 bytes** |
| answered by the client | **yes**, all three | **no**, none |

The shapes match record for record and ordinal for ordinal. **The client
acknowledged none of ours**, and it acknowledged the reference's on the same
join. Our run's client answered exactly one thing in the whole log, and it was
not a roster record.

That makes our roster the **leading candidate for the `0B09` stall** recorded
in `P2P_CONNECT_FSM.md` §7.1b — a join that completes the handshake and is then
never confirmed. **[V]** for the measurement.

### 8.7 What the server now reports about it

The comparison above was re-measured against the wire rather than the log's
record summaries, by decrypting our own outbound frames out of the run's log
(`scripts/run-gameplay-local.sh`) and diffing them field by field against the
reference's. The roster run, its repeat and its trailer are **structurally
identical**: same records, same identifiers, same fourth bytes, same counters,
same compression flags, and every byte of the host's own entry agreeing from
`07 4c` through the name marker. The only content differences are the character
ids, the names, the player handle and the endpoint pairs. **The roster is not
what the client rejected**, and §8.6's "leading candidate" does not survive the
measurement.

What the endpoint pairs showed is a real defect. This host advertised

```
ours       192.168.1.15:3333, 192.168.1.15:3333
reference  99.66.131.177:5731, 10.104.10.28:5731
```

— the same address in both slots, where both captured hosts carry two distinct
endpoints. `LocalAddressUtils.TryGetPrivateAddress` was asked for the machine's
private address and handed back the one already advertised as public, because on
a single-address machine running with a LAN address configured as its advertised
one those are the same value. Detection now excludes the advertised address and
returns nothing rather than repeating it, and `HostIdentityService` says so at
startup, naming `GameplayServerPrivateAddress` as the setting that fixes it. The
wire still carries the advertised address in the second slot, because a zeroed
pair would be no address at all — so this is a defect made visible, not one that
changes what a client is sent.

It is **not** the deployed run's cause: the host that raised `0B09` registered
`89.129.16.203:5731`, a public address, so its two pairs already differed
(`P2P_FSM_STATE.md` §1).

What the server now reports is the fact this section turned on. An inbound
record-level answer used to reach the dispatcher as an unhandled record and be
logged as a dropped message, so the one acknowledgement that says a roster was
accepted was invisible and a join whose roster drew none looked identical to one
that did. Answers are now recognised (`PeerAcknowledgementUtils.IsRecordAcknowledgement`),
recorded per session, and summarised when the peer is reaped:

```
[udp:5731] Peer 89.129.16.203:5730 acknowledged 625 record(s) before going quiet; roster acknowledged: yes
```

That count is the reference host's own view of `game1`, where the roster is
acknowledged. The same line reading `roster acknowledged: no` is the stalled
join, stated by the server rather than reconstructed afterwards — the
measurement `P2P_FSM_STATE.md` §3 records as still missing.

### 8.8 The first attempt with a distinct private address

Re-run locally at 16:21 with the endpoint fix in place, the host advertised

```
public 192.168.1.15:3333, private 172.17.0.1:3333
```

and the roster entry carried that same pair, against the reference's
`99.66.131.177:5731` / `10.104.10.28:5731`. The roster run, repeat and trailer
were unchanged in every other respect, and **the client acknowledged none of
it**: the same `0x9001` series with rising ordinals, each answered with a
`0xd001`, and no roster answer for the length of the run.

That is the second measurement of the same thing, from the other side of the one
variable §8.7 identified, and it is what closes the roster as a candidate: with
the endpoints now shaped like the reference's, the roster matches it in
structure *and* in address shape, and is still not acknowledged. **[V]** for both
runs.

It also turned up a defect in the reporting itself, which is worth recording
because it would have made the next run look better than it was. The first pass
counted the client's opening **keep-alive** as an acknowledgement: `0x5000` is
`0x1000 | 0x4000`, so it matched the answer test and the log read
`acknowledged record 0x1000 ordinal 0` — an acknowledgement of the handshake,
which nothing ever answers that way. The keep-alive is id 0 of the same `0x5x..`
class as `0x5001`, the real answer to a `0x1001` record, so the two are told
apart by identifier and not by class. Corrected, the local run reads **zero**
acknowledgements and the reference still reads 624 and `yes`.

### 8.9 Which advertised pair the host answers at

One difference between the two hosts is on the wire and is not about the roster
at all. game1's joiner advertised two pairs — `217.138.213.5:5730` and then
`10.2.0.2:5730` — and the host sent **18 528 of 18 528 datagrams to the
second**, which is also the endpoint the handshake arrived from. This host took
the *first* pair, the public address, for every reply.

That is a live hazard rather than a stylistic one. A peer on our own network
advertises its public address first and its LAN address second, so answering the
public one means sending out through the router and back in — and a router
without hairpinning drops it, silently, with nothing logged on either side. The
LAN address is one datagram away and is known to work.

`AcceptHandshakeHandler.AdvertisedEndpointOf` now prefers an advertised pair
equal to the endpoint the datagram actually arrived from, and falls back to the
first valid pair when none matches. That reproduces the recorded host's choice on
game1 exactly, keeps the across-the-internet case correct (a peer behind NAT is
observed at its public address, so the public pair still wins), and cannot
regress what came before: a pair matching the source is reachable by
construction, and where none matches the old rule applies unchanged. **[V]** for
the measurement and the rule; **[I]** for whether it is among the reasons the
local join stalls, which it does not obviously explain — that run's roster did
arrive, since the client stopped re-sending its profile.

### 8.10 The roster, compared byte for byte — and what the client does instead

Both rosters were decoded onto the same axes and diffed position by position.
The run frame, the repeat frame and the trailer all match in structure, in
record types, in lengths and in fourth bytes, and the bodies differ in **four
regions only**:

| offsets | reference | ours | what it is |
|---|---|---|---|
| `[0x08]` u32 | `0x0000a001` / `0x0001232f` | `0x00000020` / `0x00000002` | the character id |
| `[0x05]`, `[0x07]` | `0`, `0x14` | `0`, `0x01` | the player's session slot |
| `[0x13]`–`[0x1e]` | host's two pairs | ours | the endpoint pairs |
| `[0x44]` on | `Low Level Host`, `fargraven2` | `server-3333`, `PhilDunphy23` | the names |

Everything else — the version byte, the sub-type, the roster index, both
unresolved bytes, both block constants, the thirty-seven zero bytes and the clan
marker — is identical at every offset. **[V]**

Two of the four are worth stating rather than leaving as "different". **The
character id is the sender's own handshake id, echoed back**, which is what the
recorded host does: the reference joiner's roster entry carries `0x0001232f` and
its handshake's first u32 is `0x0001232f`. Ours carries the peer's `2` and
echoes `2`. The values are small because this deployment's character ids are
small, not because the field is being written wrongly — though it does break the
observation in §4 that `[0x0a]` reads `0x01` on every captured record, which
follows from those ids all being in the `0x0001xxxx` range. **The player-table
key is free**: across game1 the host used `0`, `20`, `15`, `4`, `11` and `6` for
six characters, so `1` is as valid as any of them.

**What the client does is where the two runs part.** Read off the client side:

| | reference | ours |
|---|---|---|
| frames before the roster | handshake, keep-alive + profile, **profile re-sent** | handshake, keep-alive + profile |
| roster arrives | t+4.17 s | t+0 s |
| client's next frame | **t+4.23 s — 56 ms later** | t+6 s |
| what that frame carries | `0xd001` ×2, `0x5001` ×4 — **an answer to every roster record** | nothing |
| then | `0x9001` body `0x0a` | `0x9001` body `0x00` |
| after that | `0x9001` `0c 01 00 00 00`, then `0x00` | `0x9001` `0x00`, ordinals 1…15, ~5 s apart |
| re-sends an unacknowledged record | yes, repeatedly | never |

**[V]** on all of it. The reference client's first `0x9001` is `0x0a` and lands in
the same frame as its answers to the roster; ours never sends `0x0a` at all and
sits at `0x00` for the length of the run. Its fourth byte climbs a step per
record and it never retransmits, because each one is answered — so this server is
moving the client's window and the client is still not joining.

**The answer body is not an echo of the request.** Measured across all three
captures, pairing each length-1 client `0x9001` with the host's `0xd001` of the
same fourth byte:

- game1: request `0x00` answered `02, 03, 04, 05, 06, 08, 09, 10, 25, 26, 27,
  28, 49`; the single request `0x0a` answered `0x24`.
- game2: request `0x00` answered `01` through `13`; the single request `0x0a`
  answered `0x0b`.

So the body is the acker's own state code and it moves, not a function of what
was asked — which is what §7.1c already concluded and what `PeerAcknowledgementUtils`
says. This host writes one fixed value, `0x24`, for every request. **[V]** on the
measurement; **[I]** on whether a moving value would advance the client, since
there is no capture of a host that answered `0x00` with the same value twice to
compare against.

**Together with §7.1d of `P2P_CONNECT_FSM.md` this locates the divergence before
the roster.** Our client announces `Mode 1` where the recorded client announces
`Mode 2`, and it is the client's own FSM that picks the value. That is earlier in
the exchange than anything this server controls, and it is the reason no roster
change is indicated: the bytes here match the recorded host's.

### 8.11 The same record read by a second parser — what the replay console names

`rpdt.js`'s `rosterPlayer` reads this identical record type — `0x07`, the
character id u32 at `+8`, the name at `+68` — from the replays SaveMGO's console
plays back. The record is the same one there: cutting this repository's own five
replays (`tools/replays/`) at chunk and record boundaries with `rpdt.js`'s own
framing yields **seven** roster records, every one of them a `0x48` player entry
or `0x4c` room entry of 79 or 83 bytes with the name at `+68` and the id u32 at
`+8` — the same subtypes, lengths and offsets as this capture. **[V]**

Beside §8.10's diff that adds three names and one variant:

| offset | `rpdt.js` | this repository | note |
|---|---|---|---|
| `[0x05]`, `[0x07]` | `slot` | `SessionSlot` | **shape agrees.** One small byte per player, repeated at `[0x07]` in all seven replay records and all six captured ones, and always below `24`: the replays carry `2, 5, 7, 9, 13, 17, 21` and this capture `4, 6, 11, 15, 20`. `rpdt.js` names it the character's slot in the session's 24-entry table, which is the table §4 already identifies from `0x26d5a8`. **[V]** |
| `[0x1f]` u16 | `clanId` | zero in every captured record | this capture's host leaves it zero, so no value is missing — a *name* where §4 had only a zero run. **[I]** |
| `[0x44]` on | `nick`, then `clan` | character name, then clan name | identical. **[V]** |
| `[0x04]` | `joinCounter` | `0xe2 + roster index` | **shape disagrees.** See below — neither name is adopted. **[U]** |

**`[0x04]` is the one offset the two worlds write differently, so no rename is
warranted from it.** In the five replays it reads `0x31`, `0x33`, `0x35`,
`0x3d`, `0x41`, `0x44`, `0x4f` — *including on the host's own entry*, which this
capture writes as `0x00`. This capture's players read `0xe2`…`0xe6`, one step
per join. So the replay's byte is not `0xe2 + index`, and it is not a count that
reads zero on a host's own entry either; this capture's byte is not a counter
that reaches `0x4f`. The two hosts at `[0x04]` are not writing the same thing,
and neither reading is entitled to the other's name.

It also carries a **second roster layout, twelve bytes shorter**: the name at
`+56` and the clan id at `+19` instead of `+31`, measured there on replays
recorded from 2026-10-07, and told apart per record by walking back from the end
to whatever precedes the nick's NUL. Nothing in these three captures is that
shape — every record here is the 79/83-byte form — and
`PlayerProfileRecordUtility.Build` writes only that form. The reader does not
implement the short layout either, but a short record fails its marker test at
`[0x43]` and is then read by the trailing fallback, which recovers the nick from
the end exactly as `rosterNameOff` does. **[I]** on that fallback covering it:
no short record has been through this reader.

**`[0x01]` is not a sub-type. It is the record's own length, and the length is
the name's.** Every roster record in all four captures reads `[0x01] = L - 7`:
`0x4c` on the host's own 83-byte entry, `0x48` on a 79-byte player entry, `0x47` on
the 78-byte one, and `0x32`/`0x38`/`0x39`/`0x3b`/`0x3c` on the 57/63/64/66/67-byte
entries of §8's second and third captures — fourteen distinct (byte, length)
pairs, no counterexample, and `0x00` on the 7-byte close record, which `0 - 0`
also satisfies. The value follows the *name*: `fargraven2` (10 chars) makes a
79-byte entry read `0x48`, `Libertine` (9) makes a 78-byte entry read `0x47`. That
resolves what §4 recorded as an unexplained sub-type “`0x47`, one bit apart, what
the bit selects being **[U]**”: the two entries differ by one character in the
name, not by a flag. **[V]**

**It also means §4's sub-type reading is superseded, and that this server writes
the byte wrongly.** `our-server-test.pcapng` holds this server's own roster run:
its host entry is 80 bytes carrying the reference's `0x4c` where 73 (`0x49`) is
correct, and its player entry 81 bytes carrying `0x48` where 74 (`0x4a`) is. Both
are self-inconsistent — the byte describes a longer record than the one it is in
— because `PlayerProfileRecordUtility.Build` writes a constant where the
reference writes `L - 7`. Whether the client reads that byte is untested and it
is the one deviation this diff had missed, so it is the first thing to correct.
**[V]** on the bytes; **[I]** on it being the reason the roster goes
unacknowledged (§8.10's diff compared the records' contents, not their lengths).

**Does any of this leave the roster incomplete? No.** The only new names are
fields the reference host also writes as zero, and every offset `rpdt.js` reads
for a player — the slot, the character id, the nick, the clan — is one both
sides already exchange. What it adds is meaning, not bytes.

### 8.12 The item-box group's fourth byte is the record's own index, and the client answers every one [V]

`rpdt.js` reads the fourth byte as an index rather than a flag. Its `records()` walker
takes the identifier's low eleven bits (`vid & 0x7ff`) for the stream and hands an
*indexed* record's fourth byte on as `b[a - 1]` — *"The record INDEX is the 4th framing
byte of an indexed record, at `a-1`"* — and `teamArray` pairs the two records of a team
array by requiring the second's index to be the first's `+ 1`. §3 reached the same shape
from the measurement (*"a monotonic counter"*). Read together, the byte is **the record's
own position in its stream, and the stream is the identifier rather than the connection**:
game1's first group carries `0x1a50` under `0`, `1`, `2`, `3` and `0x1a5a` under `0`, `1` in
the same datagram, so the families index independently.

**Both captures answer every index the host named.** Host to joiner, one pass per family
per file:

| capture | family | records | indexes | client's answers | indexes left unanswered |
| --- | --- | --- | --- | --- | --- |
| game1 | `0x1a50` | 467 | `0`–`0x37` | 428 × `0x5a50` | **0** |
| game1 | `0x1a5a` | 374 | `0`–`0x2f` | 376 × `0x5a5a` | **0** |
| game1 | `0x1250` | 354 | `0`–`0x53` | 336 × `0x5250` | **0** |
| game1 | `0x125a` | 310 | `0`–`0x47` | 292 × `0x525a` | **0** |
| game2 | `0x1a50` | 88 | `0`–`0x35` | 80 × `0x5a50` | **0** |
| game2 | `0x1a5a` | 48 | `0`–`0x25` | 48 × `0x5a5a` | **0** |
| game2 | `0x1250` | 178 | `0`–`0x37` | 170 × `0x5250` | **0** |
| game2 | `0x125a` | 146 | `0`–`0x27` | 146 × `0x525a` | **0** |

No family in either file has an index the client left unanswered, and the answer is the
peer's own identifier with `0x4000` set and the same fourth byte — §8.6's rule, applied to
this group. **[V]**

**Our live run pinned the byte to `0`, and the client answered one record and stopped.** Its
whole beat writes the group as `0x1a50/6 B` and `0x1a5a/6 B` under ordinal `0` — **221
frames, 442 records of each, not one with a non-zero fourth byte** — and the client's only
answers are `acknowledged record 0x1a50 ordinal 0` and `acknowledged record 0x1a5a ordinal
0`, both at `00:22:25`, with nothing for either family afterwards. A stream whose index
never moves names the same record over and over: the client answers it once and every copy
behind it is that same record, so the family's window never drains. **[V]** on the
divergence alone.

**What the client's silence does not explain is the black screen.** The patched client
reached the round — it loaded `stage/n001a` and printed `CheckRuleOpt_Branch = 00` at the
round's own second — stayed alive and rendering, and closed its socket a second after the
window was closed (`RPCS3.log`). It also opened a `cellOskDialog` text dialog two seconds
into that round, which the otherwise identical stage load earlier in the same log does not:
a join-time prompt and this index are two candidates for one symptom, and neither is
settled. **[U]**

**Fixed in the replay.** `ItemBoxFramesUtils.ItemBoxGroup` now takes the round's two
item-box sequences and numbers its records from them, one counter per identifier,
advancing by the records written (`BeatCounters`), so a stream's groups run `0`, `1`
then `2`, `3` then `4`, `5` … per family — the capture's own run for `0x1a5a`, and for
`0x1a50` the contiguous form of it, its two omitted spawn records being the difference.

**Still not replayed: the item-box events themselves.** `rpdt.js` names them — *"One event
= up to four records in one tick: type 00 LEN 6 HEAD, type 01 LEN 6 ITEM, type 02 LEN 10
POSITION (s16 X, Y, Z …), type 03 LEN 9 tail. All-zero pairs are filler."* The group this
server writes is that **all-zero filler pair** (`00 00 00 00 00 00`, `01 00 00 00 00 00`),
which the console discards, and the head/item/position/tail records of a real spawn are the
live round's own and are left out, as `MatchRecordFramesUtils` says. That reading also
answers what our six-byte bodies are: filler, which the client answers regardless. **[V]**
on the filler reading; **[U]** on whether a client needs a spawn event to draw a box.
