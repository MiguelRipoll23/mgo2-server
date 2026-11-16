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

**D5 — the exchange is reset while a reader may still hold it.**
`MatchRecordSequenceService.Reset()` clears every stream, and the room-reset path calls
it while the session is still up. A reader that is at `0x97` and a host that restarts
the stream at `0x00` refuses every record for the rest of the wrap. Item 6 above is the
reason professional stacks never do this: the peer cannot be told.

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
