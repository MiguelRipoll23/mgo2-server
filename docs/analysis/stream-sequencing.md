# How a stream is sequenced, and where this host is not professional

What this answers: the peer's record stream is a reliable ordered channel with a
byte-wide sequence number and no tolerance for a gap. This host writes the numbers
but implements none of the recovery such a channel needs, and that is the whole of
why a scoreboard or a ticket reaches the screen only when the counter has come round.
The reader's rules are in `docs/GHIDRA.md` ("The record reader and its neighbours, by
name"); this note is about the sender's half.

## What the peer implements

| | |
| --- | --- |
| numbering | one byte per stream, keyed by the identifier's low twelve bits (`0x1001`, `0x5001`, `0x9001`, `0xd001` are one stream) |
| acceptance | **strict equality**: the record is taken only when its byte is the one the stream state holds, which starts at `0x00` and advances by exactly one (`P2P_ChannelStateInit`, `read_queued_p2p_message`) |
| refusal | the record is discarded and its value is lost — the state is not advanced, nothing is buffered, no negative answer is sent |
| answer | one record per accepted record, same stream, answer bit `0x4000`, body echoing the byte (`decode_p2p_datagram`'s answer branch) |
| retransmission | none in the peer's reader; the sender is expected to do it |

Because a record is accepted only in order, **an answer is a cumulative
acknowledgement**: an answer naming byte *W* proves every byte before *W* was taken.
That is a property worth stating plainly, because it is the only feedback this
protocol gives and it is enough to drive recovery — if the sender uses it.

## The professional pattern this is a stripped-down version of

A shipped game's reliable channel is the same idea with the four pieces that make it
survivable. None of them is exotic; all of them are older than this protocol.

1. **Channels are layered.** Unreliable (positions, vitals — latest-wins, drop freely),
   sequenced-unreliable (a stale one is discarded by comparison, never buffered), and
   reliable-ordered (what this stream is). The identifier's low twelve bits are the
   channel here, and the class bits say which behaviour it wants.
2. **A sequence number is assigned at enqueue and the entry stays in a send window.**
   The number belongs to the record, not to the transmission: a retransmission repeats
   it. That is what makes a retransmission idempotent for the receiver.
3. **A cumulative acknowledgement advances `snd_una`.** The window is freed up to the
   answered number and no further, so "what has the peer got" is a number, not a set.
4. **A gap triggers Go-Back-N, and silence triggers an RTO.** When an answer names a
   number past an unacknowledged one, the sender knows the peer refused the ones in
   between (here: because they never arrived, or arrived out of order) and re-sends
   from the oldest unacknowledged entry. When nothing is answered at all, a timer does
   the same. Retransmission is the mechanism; everything else is optimisation.
5. **Redundancy is a bonus, never the mechanism.** Sending a snapshot twice helps a
   client that missed the *latest* one. It does not close a hole behind it, because
   the copy carries the same number the peer is not expecting.
6. **A stream is never renumbered under a live reader.** A generation change is a new
   stream key, not a reset of an existing one; the peer's state cannot be told to
   forget.

## Where this host departs, and what it costs

Measured numbers are from `myserver.pcapng` and `.pcapngs/pcapng1`, `pcapng2` through
`tools/udp_session_records.py`.

**D1 — there is no send window.** `SendFrame` writes a record, notes the instant for a
round-trip measurement (`PeerSession.NoteSent`) and drops the body. The peer's answer
is recorded in a `HashSet` used for logging, the roster flag and RTT — and for nothing
else. So a value the peer refused is never written again by this host, and the stream
is stuck until the byte-wide counter comes round to it: 256 values, which at a
round's rate is minutes. The reference never relies on that: `pcapng1`'s stream
`0x001` writes 1 319 records for 256 distinct values and `pcapng2` 1 536 for 256 —
1 168 and 1 280 re-sends. Those captures are full of re-sent values because the
working host is doing item 4 above. This host's 1 304 re-sends are the scoreboard's
three blind copies, aimed at the newest value only.

**D2 — the reference itself drops and reorders, and survives.** `pcapng1`'s `0x1001`
stream runs `00 01 02 03 04 05 06 10 07 08 …` — `0x10` is written a value ahead of the
one before it — and its `0xd001` records run `01 02 03 05 06 …` with `04` missing. On a
strict-equality reader both are refusals. The session works because the missing values
are written again later. A host that only fixes its write order (this host now splits
every run in numbering order) removes one source of holes and none of the recovery
that makes a hole survivable.

**D3 — numbers are taken outside the write lock.** `ScoreboardPublishService`,
`TeamArrayService`, `PlayerSettingsBurstService` and the rest call
`RoomRosterService.TakeRunOrdinal()` and only then hand the records to
`RosterRunSendUtils.SendAsync`. Two publishers can take `0x32` and `0x33` between one
another and then reach the socket in the other order; the peer refuses the second
arrival and the first never recovers. The write path has a per-session gate
(`PeerSession.OutboundGate`) — the numbering has to happen inside it, not before it.

**D4 — the scoreboard's copies cannot do what they are asked to do.** Three copies at
600 ms are written for a delta, and a copy keeps its byte (correctly). The peer takes a
copy only when that byte is the one it expects, so the copies recover the *newest*
value's loss and nothing else. That is a reasonable optimisation on top of a window. It
is not one here, where it is the only recovery there is.

**D5 — the exchange was reset while a reader could still hold it.** The room-reset path
used to clear every stream (`MatchRecordSequenceService.Reset()`), so a reader at `0x97` met
a host restarting at `0x00` and refused everything for the rest of the wrap — item 6 above is
why professional stacks never renumber under a live reader.

Both halves of the measurement say the reference does not do it. Its host keeps counting
across a room that empties and rejoins: `pcapng1`'s `0x1254` runs 416 values in one ascending
line from start to finish and its `0x1a54` `00..11` across two round markers at t+281.97 and
t+337.74, with no backward step in either, while the *client* is the side that opens its own
stream at `00` for a new round (t+491.285, answered `0x5a54` `00..05` by the host, whose own
series went on `0c → 0d → 0e`). `myserver.pcapng` shows this host doing the opposite — its
`0x1254` goes `05 → 00` mid-session — which is the renumbering D5 describes. The clear has
been removed from the room-reset path; the counters now run on as the reference's do.

**D6 — the failure period is a property of the sequence space.** A byte-wide number
with equality comparison means the worst case is 256 values of dead stream, and every
hole has that price. Professional stacks use 16 bits or more precisely so that a
recovery can be missed without the channel dying. This protocol's space cannot be
changed — the peer decides it — so the recovery must be reliable instead.

## The sender, as the image implements it

The game lets a player host, so the client carries the sender too, and it is the
authority on the four pieces above. What it actually does (`docs/GHIDRA.md`, "The
sender"):

* **One record per call.** `P2P_SendRecord` carries a single record out; a run is a
  loop of calls, not one bulk write. The record is stamped by its builder
  (`P2P_SetRecordHeaderField`, selector `0` = the low nibble of the fourth byte,
  whose bit `0x01` is the "this one is sequenced" flag) and the send is refused when
  the retry count sits one behind the acknowledged byte — a record the peer has
  already taken is not written again.
* **The packer is word-aligned and reports a full datagram as an error.**
  `append_udp_datagram_to_send_buffer` advances `(3 + len) & 0xfffc` and returns
  `-1` when there is no room, which is what splits a long run; it zeroes the retry
  byte on every successful append.
* **There is a bounded record table per peer, not an unbounded window.** The tick
  walks 24 slots (`0x18`), and `P2P_SendRecord`'s guard is `0x17` — the last index of
  that table. So the sender's outstanding set is as large as its slot table and no
  larger, which is the image's own backpressure.
* **Receive before send, every frame.** `P2P_PhaseTick_SendOperations` calls
  `receive_p2p_datagrams()` first and only then builds that phase's records. Answers
  are folded in before the next write, which is the ordering this host now has too:
  an answer is processed on the receive path and the refused tail is written from
  there.

That is the whole of what our `StreamRetransmitService` has to mirror: a bounded
outstanding set per peer, every answer consumed before the next write, the refused
tail re-sent in order, and never re-sending what has already been taken.

## The shape of the fix

`src/GameplayServer/StreamRetransmitService.cs`, wired into the write and answer
paths:

* every numbered record written is held in a per-peer, per-stream window, in write
  order, with its body and attempt count;
* an answer frees the window up to the answered byte (item 3) — an answer is
  cumulative, so this is exact rather than optimistic;
* anything still held after that is a hole, and it is re-sent from the oldest entry,
  in order (item 4), before the next record of that stream;
* a write to a stream whose oldest entry has been held past `0x2bc` ms does the same,
  which covers the record whose answer never comes at all;
* attempts are bounded, so a peer that has genuinely stopped reading is not flooded.

Ordering (D3) and the reset (D5) are separate changes: numbering belongs under the
session's write gate, and a stream is not restarted under a live peer.

## The queue the drain is measured against

The image's outstanding set is a byte-counted record queue, and the sender's drain is a
fraction of that queue — so the thresholds are numbers of *bytes*, not of records.

| queue offset | field |
| --- | --- |
| `+0` | capacity, halfword |
| `+2` | used, halfword |
| `+8` | the `0x600`-byte handle table, 256 six-byte entries |
| `+0xc` | first entry: twelve-byte header then body |
| each entry | `(3 + size) & 0xfffc` |

`P2P_QueueRecordHandles` (`0x269ca8`) refuses with `0xffffffff` when
`capacity < used + size`, `P2P_CompactRecordQueue` (`0x269ea8`) rebuilds used as it drops
delivered entries, and `P2P_FlushChannelQueues` (`0x285b58`) starts freeing once
`used >= capacity >> 1`, walking from the oldest entry and subtracting each entry's size
until what remains is under `capacity >> 2`.

**No function writes the capacity.** `P2P_ResetRecordHandleTable` (`0x269808`) sets the
used halfword to `0xc` and clears the handle table; `initialize_p2p_session` (`0x268148`)
calls it on the three queues at `slot + 0x34`, `+0x38` and `+0x3c` and stores no capacity;
`create_p2p_session` (`0x261fe0`) only scans the 24 static slots (stride `0x6d8`) for one
whose byte `+4` is zero. The capacity therefore arrives with the queue buffer's own layout,
before any of that code runs, and the image's value for it was not read — `0x578` is the
receive path's buffer size (`receive_p2p_datagrams`, `0x2622a0`), and this host uses it as
the capacity of the same header.

**Where the queue sits.** A slot's three queues are pointers (`slot + 0x34`, `+0x38`,
`+0x3c`), not inline storage. `receive_p2p_datagrams` (`0x2622a0`) loads the one at `+0x3c`
and reads it as a header — capacity in halfword `[0]`, used in `[1]` — and ticks the session
when `used > (capacity * 3) >> 2`. That is a second threshold above the flush's half: three
quarters before a datagram is decoded, one half before the flush starts freeing. The network
state itself is the `0x20000` arena `P2P_InitializeNetwork` gives to `sceNpInit`, so the
capacity's value is not recoverable from the file.

**An answer is applied before anything the frame makes this host write.**
`receive_p2p_datagrams` (`0x2622a0`) decodes each arriving datagram, and that decode is where
an answer frees the sender's queue — `decode_p2p_datagram` (`0x2666c8`) marks the entry the
byte names — while the flush runs from the phase tick afterwards. This host freed late twice:
first after its own flush, then after the dispatch's answer pass, which writes records of its
own. Both re-sent entries the same frame was acknowledging. The free now happens at decode
time, before every writer.

**The check on that order is the answer list, not a frontier count.** Every write of the room
stream draws one answer carrying the same byte in the same position: the 2026-11-18 17:25
session carries 949 writes and 947 answers, all 947 aligned byte for byte, with no answer
timestamped before its write and only the final two writes unanswered. Counting "writes behind
the answered frontier" gave 122–270 per session, and that figure is an artefact — the frontier
was advanced across same-instant frames and across the byte-wide wrap — so it is not evidence
and is not quoted from here on.

`StreamRetransmitService` now accounts the window the same way: an entry costs its
12-byte header plus body, aligned by `(3 + size) & 0xfffc` — 40 bytes for the 26-byte team
pair — the window drains at 700 bytes and keeps freeing oldest-first until it is under 350.
That is roughly 18 held copies before a drain and 8 after, which is the image's own
backpressure rather than a record count chosen here.
