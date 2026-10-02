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

Both sides send `ver = 2` and both advertise **two** endpoint pairs. The joiner's pairs
are `10.2.0.2:5730` twice; the server's are `99.66.131.177:5731` and `10.104.10.28:5731`.
So the server is reachable on two addresses and the client on one — the public/private
pair shape §4 of `UDP_P2P_PROTOCOL.md` describes, confirmed on a session that ran to
completion rather than to a join. **[V]**

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

**1 — handshake, both directions, pre-keyed.** Two 44-byte datagrams.

```
joiner -> server   10.2.0.2:5730 -> 99.66.131.177:5731   hdr 0x0000
  peer_id 51001  counter_base 0x736773a6  magic b7 8a 25 4d  ver 2  count 2
  entries 10.2.0.2:5730, 10.2.0.2:5730

server -> joiner   99.66.131.177:5731 -> 10.2.0.2:5730   hdr 0x0001
  peer_id 51002  counter_base 0x3c8a65d6  magic b7 8a 25 4d  ver 2  count 2
  entries 99.66.131.177:5731, 10.104.10.28:5731
```

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
| `0x0a` | `0` | `1` | `1` | `1` | `1` | `1` | **[U]** host `0`, every joiner `1`; not the team flag |

Three things follow. `[0x04]` is `0xe2 + index` and the host is written as a **distinct
zero**, not as `0xe2 - 1`. `[0x05]`/`[0x07]` are the same byte in all six and follow no
order, so they are a per-player value of unresolved meaning rather than the second copy of
the index the replay reading claimed. And `[0x0a]` is `0` on the host and `1` on every
joiner — **unresolved**, and explicitly *not* a team flag: the capture shows the pattern
but nothing that says what the byte means, so the code calls it `FlagValue` and says so
rather than borrowing a name the bytes do not support.

**Implemented.** `PlayerProfileRecordUtility` now writes `0xe2 + index` (or zero for the
host), writes zero at `0x05`/`[0x07]`, and recovers the index with `RosterIndexOf`, which
reads a zero as the host. `PlayerNumber` is renamed `PlayerValue` and is reported raw,
because it is no longer an index. The per-player block also carries **three** non-zero
constants — `0x02` at `0x12` and `0x16` at both `0x18` and `0x1e` — against the replay's
one, and the builder writes all three.

The test vectors are the six captured records verbatim, with same-length placeholder names,
and `PlayerProfileRecordTests` is rewritten against them. The replay-derived vectors and
their assertions are gone: the live capture is the reference.

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

**4 — the game runs.** From `hdr 0x8005` onward the channel is tick records in both
directions and nothing else of substance: 20 852 `0x0261`, 15 954 `0x0a61`, 10 932 `0x00dd`
and so on. The join is over in about four seconds.

---

## 5. Tick records

The game itself. `id < 0x1000`, length byte is `data + 1`, fourth byte is the attribute
class. There are **121 distinct ids**; the busiest are below. Nothing here is named beyond
the class, because nothing here could be named honestly.

| id | count (server → joiner) | body lengths | class byte |
| --- | --- | --- | --- |
| `0x0261` | 20 852 | 1, 4, 5 | 1, 2, 3 |
| `0x0a61` | 15 954 | 1, 4, 5 | 1, 2, 3 |
| `0x00dd` | 10 932 | 1, 5 | 1, 2 |
| `0x010c` | 9 997 | 2 | 1 |
| `0x00b5` | 8 752 | 1, 5 | 1, 2 |
| `0x0083` | 6 173 | 1, 5 | 1, 2 |
| `0x0081` | 3 087 | 16, 32, 38, 44 | 1, 2, 3 |
| `0x0080` | 3 269 | 2 | 1 |
| `0x007f` | 3 087 | 3, 9 | 1, 2 |
| `0x00b1` | 4 377 | 3, 9 | 1, 2 |
| `0x087f` | 1 140 | 3, 9 | 1, 2 |
| `0x0881` | 1 140 | 16, 32, 44 | 1, 2, 3 |

**`0x0081` is the position record.** Its body lengths are **16, 32, 38 and 44** — exactly
the four sizes `mgo2_replay_parser.py` maps to the compressed position and the yaw pair, and
at the same offsets. The capture confirms the replay parser's transform table on live wire
bytes rather than on a recorded file. **[V]**

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

**Not established.** The 8-byte trailer on 290 frames (§2); the meaning of every
`[U]` row in §3; why the three `ver = 6` flows died.

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