# The team ticket, from the scoreboard to the socket

What this answers: every step a team-ticket record takes in this host, from the body
being built to the bytes leaving the socket, and where its fourth byte can be spent
without being written. The bug that made the panel ignore the scores lives in that
last part, and it is one step, not a layer.

## The chain, in order

| # | where | what happens to the ticket |
| --- | --- | --- |
| 1 | `ScoreboardPublishService.PublishAsync` → `TicketRecordsAsync` | a death moved a side's count, so `TeamTicketBodiesUtils.Build(team, remaining)` runs |
| 2 | `TeamTicketBodiesUtils.Build` | 11-byte body: `0b 00 00 00 <08|40> 00 00 00 00 <lo> <hi>` |
| 3 | `ScoreboardPublishService` | `new RosterRecord(0x1001, ticket, roster.TakeRunOrdinal())` — **the value is taken here** |
| 4 | `MatchRecordSequenceService.Take(0x1001)` | stream key `0x001`; returns the current byte and increments it |
| 5 | `RosterRunSendUtils.SendAsync` → `Pack` | `FrameBuilderUtility.MessageOf(type, body, ordinal)` → `UdpMessage.Create(type, body, flags = ordinal)` |
| 6 | `PeerContext.SendRecords` | the delegate built in `GameplayServerService.MessageDispatch.cs` → `SendMessages(session, records)` |
| 7 | `GameplayServerService.SendMessages` | `NumberAtWrite` (only `AutoNumbered` records take a value here), then `retransmit.NoteWriting` (window), then `SplitByFrameSize` |
| 8 | `SendFrame` | takes `session.OutboundCounter` (15-bit, wraps at `0x7fff` because bit 15 is the compression marker) |
| 9 | `FrameBuilderUtility.BuildMessageFrame` → `MessageCodecUtility.SerializeMessages` | writes the record: `[id lo][id hi][len][flags][body]` — **`flags` is written verbatim at offset 3** |
| 10 | `FrameCryptoUtility.EncodeFrame` | scrambles the frame and fills the tail digest (the only thing that changes the bytes after step 9) |
| 11 | `socket.Send` to `session.DialBack` | on the wire |

Read at both ends, nothing in steps 5–10 rewrites the fourth byte: `SerializeMessages`
copies `message.Flags` into offset 3 and no other stage touches it. **The byte the
client sees is the byte step 4 handed out.** So the ticket's problem cannot be in the
frame, the counter or the crypto — it can only be in which value step 4 handed out and
whether every value before it also reached the client.

## The three places a value is spent without being written

A stream's client-side expectation starts at `0x00` and advances by exactly one per
record **it takes**. Anything that advances this host's counter without a record the
client takes is a permanent one-value drift, because nothing rewinds it:

1. **Taken in the caller, before the write.** Steps 3–5 happen outside the session's
   write gate (`PeerSession.OutboundGate`). Two publishers can take `0x32` and `0x33`
   and reach the socket in the other order, and an ordering that inverts one pair
   starts the drift. This is the open item (D3 in `stream-sequencing.md`).
2. **Taken for a record that is then not written.** A builder that takes a value and
   drops the record — a delta filtered to zero, a ticket for a round with no allotment,
   a run the beat skips — spends the value and never sends it.
3. **Written out of order.** Any run whose records are packed in a different order from
   the one they were numbered in. The join's roster did exactly this: measured on
   `myserver.pcapng` t+4.06 it wrote `… 0x06 0x11 0x12 0x07 …`, so the close and the
   count arrived ahead of `0x07` and the client refused both, and the stream never
   recovered. This one is fixed: every run now goes out in numbering order
   (`RosterRunSendUtils`), which is what the two-value loss cost.

## The measurement that separates our streams from the reference's

The fraction of +1 steps in a stream's own byte sequence, per identifier, on the host's
writes (the client's answers mirror them exactly):

| stream | `pcapng1` | `myserver.pcapng` | what it means |
| --- | --- | --- | --- |
| `0x1001` (roster, scoreboard, tickets) | 64.6% | **65.3%** | as healthy as the reference |
| `0x1254` | 100.0% | 92.0% | fine |
| `0x1250` / `0x125a` | 98.2% | **76.5%** — `00 01 02 03 04 05 01 02` | **rewound** |
| `0xa54` (operations) | — | **14.3%** — `00 01 02 03 02 03 03 01` | **rewound twice** |

A stream that runs `… 04 05 01 02` has been restarted at `0x01` while its peer was still
reading. The peer's expectation cannot be rewound, so every record after the rewind is
refused for the rest of the wrap; a stream that keeps being rewound never recovers at
all, which is the `0xa54` row. This is the ordinal fault that survives everything else,
and the guard on `MatchRecordSequenceService.Restart` is its fix: a stream that has
handed out a value keeps counting. A round that wants a fresh stream gets one from its
generation's identifier, whose low twelve bits differ and whose peer-side expectation
therefore starts at zero by itself.

## Why writing the body across the whole ordinal space works

Because the drift is a *byte*, not a *record*. Spraying one ticket body under every
value guarantees one copy carries the value the client is holding, so it is taken and
the panel draws the count. It also proves the body is right: the same bytes are accepted
the moment the byte matches, so the layout was never the fault.

## The backwards jump is the game's own re-send

The game hosts matches too, and its receive path writes records as well: in
`decode_p2p_datagram` (`0x2666c8`, annotated in the project) receiving a record of stream
`S` walks the send queue and appends **every queued entry whose container id sorts below
`S`**, clearing each entry's retry byte (`+0xb`) as it goes. That is a Go-Back-N
retransmission of the older values, and it is why the reference's capture steps
backwards — `pcapng1` at t+3.63 runs `0x11 → 0x01 → 0x12`: `0x01` there is a re-sent
queued entry, and `0x12` is the counter continuing. The same function's `0x4000` branch
is the answer reader, which advances the window base to the answered byte.

So a backwards step on the wire is expected behaviour and is not a counter restart. What
this host must not do is the reverse: spend a value and never write it. The re-send loop
is what makes the reference able to spend a value early (it seats a close two values
ahead) without dying — this host now does not spend one early at all.

**And this host now re-sends the same way, from the same place.**
`StreamRetransmitService.NoteReceived` is called once per inbound keyed frame, before
anything that frame makes this host write, and writes again every held record of that
peer whose oldest entry is more than one frame old. That is the game's receive-driven
shape rather than a timer's: the trigger is a frame arriving, not an answer landing and
not this host's next write to the stream. The one deliberate difference is the gate —
the game orders its queue by container id and re-sends what sorts below the record that
arrived, while this host holds no such queue and asks for the same effect by age, so a
peer beating at thirty frames a second draws at most one extra copy a frame.
