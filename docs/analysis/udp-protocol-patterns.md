# The gameplay channel's design, read as one protocol

**What this file is.** The patterns that repeat across the captures, stated as a
design rather than as a list of records. The byte layouts stay in the protocol
documents (`UDP_GAME_CAPTURE.md`, `BRIEFING_FLOW.md`, `DATA_PHASE_DELIVERY.md`,
`STATE_VECTOR.md`); what is written here is *why* the records are shaped the way
they are, and which of those shapes a host has to honour. A claim a capture
settles is **[V]**, one inferred from the image is **[I]**, one nothing settles
yet is **[U]**.

Every timing below is in seconds from the session's first datagram, and is
reproducible from the capture it names with
`python3 tools/udp_session_records.py <capture> --peer <address>` — the decoded
dumps these numbers were read from are session output and are not committed.
The captures: `mgo2-game-joined-when-red-team-then-briefing`,
`mgo2-started-briefing-then-solo-game`, and the live `myserver16` with the
minidump taken during its freeze.

## 1. It is replication, not a command stream

No record is an instruction. Each one is a piece of state — a roster entry, a
state blob, a clock, a slot table — and the receiver is expected to hold it, not to
execute it. Three consequences follow, and every freeze this project has chased is
one of them.

- **The third field is a per-stream ordinal**, not a flag. It is the next number of
  that identifier's own sequence, and two frames with the same record and different
  ordinals are a repetition, while the same ordinal twice is a record already
  delivered. **[V]**
- **Acknowledgement is per stream.** A receiver answers with an *empty* record on
  the identifier it consumed — `0x5001 0x0c`/`0x0d` after a pair, `0x5254 0x0f`
  after a compressed frame — so a lost datagram costs the records it carried and
  never the stream. `ms16` shows it plainly at t+54.380: three acks, one per
  ordinal, in a single reply. **[V]**
- **Repetition is the repair.** Nothing is retransmitted in the TCP sense. The
  22-byte state blob, the countdown, the pair and the roster are all written again
  and again, so a client that missed one is corrected by the next. The reference
  republishes the pair at `02` at t+62.666, t+62.667, t+65.138 and t+67.733 — four
  times in five seconds — before anyone answers it. **[V]**

## 2. A phase is a pair of identifiers

Every payload travels under two identifiers `0x800` apart — `0x1ade`/`0x1a54` for
one generation, `0x12de`/`0x1254` for the next — and the `0x800` bit is the
*generation* on the report (`e1`), on the companion and on the run alike. The set
flips at the briefing→round boundary: the solo capture's report `e1 03` arrives on
`0x12de` at t+54.918 and again on `0x1ade` at t+167.992, and the round's first pair
follows on `0x1a54`. **[V]** A record answered on the wrong set is a record answered
to the wrong phase.

## 3. Two version-number handshakes, both stop-and-wait

The channel has no window and no timeout, so the two exchanges that cannot afford a
loss are handshakes on a **sequence byte the client names itself**.

**The room snapshot — `83`/`82`/`84` → `85`/`86` → `81`.** The pair is a 26-byte
snapshot of the room's 24-entry player table (sides and occupancy), and its second
byte is the generation:

```
client 83 NN <state> <flags>   (or 82 NN / 84 NN)   "give me the room at NN"
host   85 NN / 86 NN                                the pair at NN
client 82 NN / 84 NN                                "I have NN"
host   81 NN                                        confirms NN
host   0b 00 01 … 08 07 NN+1 + two companions       names NN+1
host   85 NN+1 / 86 NN+1                            the pair at NN+1
```

**The answer to an echo is the pair again, and it is repeated until answered.**
`joined-when-red-team`: the client asks at t+57.983, the host replies with the pair
at `02` at t+62.666, t+62.667, t+65.138 and t+67.733, and the client re-echoes
`82 02` at t+62.776 — it acknowledges the repetition, not a new generation. The solo
capture is the same shape: ask at t+55.312, pair at `7e` at t+57.626 and t+59.993.
**[V]**

**The advance is a separate act, and it is the room's, not the echo's.** The
confirmation lands long after the echo and when the room changes:

| capture | echo | `81 NN` | interval | what else is on the wire there |
| --- | --- | --- | --- | --- |
| joined-when-red-team | t+57.983 | t+72.560 | 14.6 s | the round's set begins: `1ade e0 03` and `1a54 0b 3c 00` |
| solo | t+55.312 | t+167.992 | 112.7 s | the report `e1 03` moves to `0x1ade`; the run follows |

In both, the advance is written into the instant the round opens — not into the
instant the client answered. That is the strongest reading of what gates the
confirmation: a phase change in the room, with the client's echo merely having
armed it. **[V]** on the coincidence in both captures, **[U]** on whether the
round could open without it.

**The client only names `NN+1` after `81 NN` is on the wire.** In the solo capture
the order after the confirmation is the group naming `7f` (t+170.366), the client's
own echo `82 7f` (t+174.032), then the pair at `7f` (t+174.371) — so the pair at
`NN+1` may accompany the confirmation or follow the client's echo of it. In
`joined-when-red-team` the confirmation of `02` and the pair at `03` go out in the
same millisecond (t+72.560). Both orders are the reference's. **[V]**

**The `0b … 08 07 NN+1` group names the sequence the pair is next published
under.** Its last byte is that sequence, the two before it are the pair's own index
bytes, and its two ten-byte companions differ only in their last byte (`00`/`01`).
It is **not** written at every advance: `joined-when-red-team` writes it naming
`04` at t+116.192 and naming `07` at t+850.167, but the `02`→`03` advance at
t+72.560 carries no such record at all. What makes an advance worth naming is
**[U]**; the shape and the sequence are **[V]** on both occurrences and in `ms16`.

**What the live capture holds.** `ms16` had the whole loop and still froze:

| t | record | direction |
| --- | --- | --- |
| +52.997 | pair `f3`, three times | host → client |
| +53.557 | `82 f3` / `84 f3` | client → host |
| +53.580 | ack `0c`/`0d`, `81 f3`, group naming `f4`, pair `f4` | host → client |

The client never echoed `f4`; it only polled `0x9001 00` every five seconds
(t+61.532 … t+112.720), acked our burst `0x20`–`0x27` at t+113.043, and sent nothing
again. So the slot handshake was answered exactly as the reference answers it and
the client still went silent — which is why the stall is not in this handshake.

**The briefing report — `e1`/`e0`, the same shape one record wide.** The client
reports its presence, the host publishes one `e0` record per reporting player under
a publication index, and both sides repeat until answered. **[V]**

## 4. A round opens with a gamestate baseline

The run the round opens with is one compressed frame holding the whole world's
starting state — the payload marker, two empty roster heads, the 89-byte operations
state vector, the lobby-slot pair, the entity and roster blocks, the round clock and
the rules pair. A client that has not received it cannot render, and it is written
per peer, not per room, because the payload echoes the byte that peer reported.
`ms16` writes it into the reply to the report at t+52.997. **[V]**

## 5. Time is ticks, and the host owns the clock

Every clock on this channel is in **125 ms units** — an 8 Hz tick. The operations
state vector carries the round's limit that way (480 units = one minute), the beat
counts it down, and the run's own clock record (`1a54 0x02` → `01 20 0d` in `ms16`)
opens it. The briefing's countdown (`0b`) is the one exception: it is in **seconds**,
and it is the host's statement of how long the phase has left, not a clock the
client runs. **[V]**

## 6. The phase machine: the client is authoritative about one thing

The client owns exactly one fact — *"I have loaded"* — and everything else is the
host's: the reporters, the countdown, the deadline that opens the round, the ready
set. That asymmetry is what makes the briefing a phase rather than a timer:

- a briefing's time is armed by its **first report**, not by the join, because the
  join can be a minute ahead of the client that has to read it; **[V]**
- the count is held at the room's whole briefing time while **one** player is in
  the room, and starts moving at the second report — a solo room is deliberately
  not on a clock; **[V]**
- a round opens on the ready records or on the briefing's deadline, never on a
  fixed wait, and a round opened inside the briefing holds its clock until the
  briefing's own end. **[V]**

## 7. The failure mode of this design is a stall, never an error

Because nothing is retransmitted and no wait has a timeout, a client that is
missing one state stays alive and silent: heartbeats continue, the map is drawn,
the audio and the simulation stop. `ms16`'s dump is what that looks like from
inside the console:

```
+0x000  00000002  state gate (proceeds only at 2)   ← satisfied
+0x02c  00000000  channel A object                  ← never created
+0x030  00000000  channel B object                  ← never created
+0x0c8  00000001  own slot byte                     ← the client holds its slot
+0xbb4  00000000  phase object                      ← never created
+0xc18  00000000  phase timer                       ← never armed
```

The gate is open, the room knows the client and gave it a slot, and the phase has
not been entered. The host is not busy either: of 83 threads, 81 wait in `ntdll`,
one runs the message loop, and one — the emulated guest thread, RIP
`0x215cbaf37a2` outside every module — the dump cannot read. So there is no spin
and no host lock to find; the client is simply not being told to move on. **[V]**
on the dump, **[I]** on the phase being what the missing records would create.

The two records every reference client sends about two seconds after the pair, and
that `ms16`'s never sent, are the briefing entry: `1d 03 …` five times (one per
player, second byte the slot) and `22 03 …` once. In the solo capture they arrive
at t+57.304, 2.0 s after the echo; `ms16` has not one `1d` body in 113 s of
capture. **[V]** on the absence; **[U]** on which host record would provoke them —
no instruction in the image stores `0x1d` as a constant, so their writer sits
outside the peer-to-peer module.

**So the test of a host change is not "does the client error" but "does it send the
next record".** A capture that ends in polls and acks is a stall at the last state
the client never named, and that state is what the next capture should be read for.

## 8. What is still unread

- **`1d` / `22`** — the client's briefing-entry group. What provokes them, and what
  the host owes them, is **[U]**. This is the most direct question left: the
  handshake, the run and the roster are all delivered in `ms16` and the group is
  still absent.
- **When the `0b … 08 07 NN+1` group is written.** Present at two advances in
  `joined-when-red-team` and in `ms16`, absent at the `02`→`03` advance. **[U]**
  on the trigger; the two companions' names are **[U]** too.
- **What gates the advance.** In both measured captures the confirmation lands
  on the instant the round's set opens, and the echo precedes it by 14.6 s and
  112.7 s. Whether the round's opening is the trigger, or merely what the
  confirmation travels with, is **[U]** — `ms16` confirmed 23 ms after the echo,
  long before its round, and that is the one ordering no reference capture shows.
- **What the client does with the pair's `e0` side.** A slot marked `e0` is a
  player the room puts on no side, and the reference writes it before it writes a
  side; whether the client requires the `e0` pass first is **[U]**.
