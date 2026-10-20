# Why the client says "CONNECTION NOT STABLE" and disconnects

Two sessions against this host (`myserver17.pcapng`), one player alone in a briefing, both
ending with the client's own modal: *Disconnected from host. Exiting the game.* The same client
against the reference host (`mgo2-started-on-solo-game`, `mgo2-started-briefing-then-solo-game`)
waits out the same briefing without ever raising it.

Clock is seconds from the start of the client's UDP stream, as `tools/udp_session_records.py`
prints it. `IN` is client to host, `OUT` host to client.

## 1. The modal is the client's own watchdog, and the report arms it

The client reports ready once (`0x1ade e1 01`) and then waits. Nothing else on the wire is a
fault: the TCP session is up, keep-alives and the client's `0x9001 00` probes are answered
throughout, and the modal appears with no error record before it.

| | `ms16` | `ms17` |
| --- | --- | --- |
| client's ready report | 52.799 | 53.441 |
| our answer to it | run at 52.997, `0x5ade` included | 54.999, pair + `0x81 f3` |
| client re-reports | — | 68.473 (`0x12de e1 01`) |
| our answer to the re-report | — | **publication only, no `0x52de`** |
| client retransmits the re-report (`0x92de e1 01`) | — | 71.262 … 95.059, **8 times** |
| client's last datagram | 112.720 (**59.9 s** after the report) | 108.395 (**64.95 s**) |
| our round opens | 112.935 | 113.712 |

Both deadlines are the client's, ~60 s after the report it is still waiting on, and both land
just before the gate's own 60 s wait expires.

**The reference shows the 60 s is not the client's patience.** In
`mgo2-started-briefing-then-solo-game` the client reports at 51.6 and its round does not open
until 198.371 — 147 s later — and it is still connected and still polling. So something the
reference host sends resets the watchdog and ours does not.

## 2. The measured difference: the report that does not open the round is answered

The reference host answers a join report with the report's own record: identifier plus
`0x4000`, empty body. In `mgo2-game-joined-when-red-team-then-briefing` the client reports
`0x12de e1 05` at 57.656, the host writes `0x52de` at 60.273 in the same datagram as its
`0x12de e0 05` publication, and repeats the publication at 62.666. The round there opens much
later; that `0x52de` is an answer, not the round's marker.

Our host withholds that answer by construction: `GameplayServerService.FrameDispatch` excludes
join reports from the acknowledgement pass (`!IsJoinReport`), on the rule that the
round's marker *is* the report's answer. That rule holds for the report that opens the round —
answering it twice puts two markers on the wire — but it leaves the waiting report unanswered,
and `ms17` is what that looks like: the client re-sending the same record under `0x8000` eight
times over 24 s, the only capture in the set in which `0x92de e1 01` appears at all.

**Fixed** in `JoinReportHandler`: a report that does not open the round now sends its own bare
answer, beside the publication, which is where the reference writes it. The pass still skips
join reports, so a report that does open the round still draws exactly one marker.

**Why an unanswered record is fatal rather than late** is already read out of the image
(`docs/protocol/P2P_CONNECT_FSM.md` §7.1b): the session holds a 24-deep window over the records
it sends at `session+0x48`, `[0xc]` is that window's far end, and the only thing that drains it
is the answer path at `0x266f14` / `0x2676e0`, which moves `[0xc]` up off an inbound record's
fourth byte. The gate to the established state, `0x268f18`, asks whether the window has
drained. A record this host never names therefore does not age out of the window; it stays at
its far end, the peer re-sends it under `0x8000`, and the state those waits belong to is never
reached — which is why the modal comes from a missing answer and not from a slow one.

The dump carries the modal's wording (`CONNECTION NOT STABLE`, guest `0x7984a1e` and the text
table at `0x805d2b2`), but not a symbol to the guard: the string is reached through a table,
so the address pair scan (`_scratch/addruse.py`) finds nothing naming it directly, and the
mechanism above comes from the image rather than the dump.

## 3. What is not wrong

* **The transport.** The TCP session stays up for the whole wait in both sessions; no `0B09`,
  no disconnect before the modal.
* **The briefing countdown and the clock.** `0b 3c` held in both, and the reference solo holds
  the same value with one player. Our `0b 3c 00 00 00` differs from the two-player reference's
  `0b 3c 00 02 00` in the fifth byte only, which the reference writes once a second player is in
  the room; that is a room fact, not a fault.
* **The briefing run's bytes.** Ours and the reference's agree on every field that is not the
  room's own, including the phase byte.

## 4. Still open

1. **The run is written three times in one pass.** `ms16` at 52.997 carries the whole thirteen
   three times over — three `0x5ade`, three `0x5001`, three of everything — where every
   reference datagram carries one. It is the same outbound frame set re-sent inside a hundredth
   of a second, and no capture shows it. Not proven to be what the client objects to, but it is
   the largest unexplained difference on our wire and it should not be there.
2. **The publication rate.** `BriefingReadyPublishService` repeats the ready set every briefing
   tick (2.35 s, `MatchRoundStateService.BriefingTickMilliseconds`) for the whole wait (`ms17`: 68.5, 71.3, 79.5, 81.8, 84.9, 88.1, 91.4, 95.1, 107.6). The reference
   writes it twice, 2.4 s apart, and then nothing until its round. Whether the watchdog is reset
   by the publication, by the countdown, or by neither is what §2's fix is meant to settle.

## 5. The measurement that closes it

Re-capture one solo briefing and read three things:

* `0x1f` from the client within 60 s of its report, and the round's own run (`0x9254`, class
  `0x1d`) after it. That is the success line.
* `0x92de` in the client's direction: it must be absent, as it is in every reference.
* the run written once, not three times.

If `0x1f` still does not come with the answer in place, §4.1 is the next suspect, and the
`ms16` datagram at 52.997 is the reproduction.

```
python tools/udp_session_records.py myserver17.pcapng --peer <client-ip:port> > tmpout/ms.txt
python tools/udp_briefing_flow.py myserver17.pcapng        # countdown, ready state, round open
```
