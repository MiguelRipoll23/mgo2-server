# How long a peer may go quiet, and what happens when it comes back

**What this file is.** The measurement of the longest silence a live client keeps
in the reference captures, the idle window that measurement supports, and the
rejoin path a dropped peer now has. §3 answers the second question: what this
host sends in each phase that the briefing-start captures do not.

Every number below is read from the captures in the repository root with
`tools/pcap_gameplay_log.py` (`Replay`, `Frame.messages`), folding record
identifiers to their family first — `MatchRecordGenerationUtils.SiblingBit`
(`0x0800`), the host's answer bit (`0x4000`) and the payload class bit (`0x8000`)
— because the two reference families number the same record differently.

## 1. The longest silence, per capture

The client's own cadence is one datagram every five seconds: a one-byte
`0x9001` keep-alive. The gaps longer than that, for every capture that is not
this project's host (`myserver*`), largest first:

| capture | longest silence | where |
| --- | --- | --- |
| `mgo2-started-on-solo-game` | **22.44 s** | t+60.41 → t+82.85 |
| `mgo2-game-started-team-selection-selected-blue-then-game` | 8.05 s | t+56.20 → t+64.25 |
| `mgo2-game-started-on-briefing` | 7.42 s | t+239.82 → t+247.24 |
| `mgo2-started-on-solo-game` (next) | 7.27 s | t+52.56 → t+59.83 |
| `mgo2-game1` | 6.91 s | t+46.45 → t+53.35 |
| `mgo2-joined-game-in-progress-last-round` | 6.84 s | t+57.42 → t+64.27 |
| `mgo2-game2` | 5.54 s | t+85.04 → t+90.58 |
| `mgo2-game-failed-memoryviolation` | 5.22 s | t+40.20 → t+45.41 |
| `mgo2-game-joined-when-red-team-then-briefing` | 3.51 s | t+820.72 → t+824.23 |
| `mgo2-game3` | 5.21 s | t+48.28 → t+53.48 |
| `mgo2-started-briefing-then-solo-game` | 5.20 s | t+38.13 → t+43.33 |

**The 22.44 s is a load screen, not a departure.** The datagram that ends it
carries **298 records** — `0x5001` through `0x525a`, the whole acknowledgement
window the client had been behind on — and the next one carries 127 more. The
client was reading its map while its socket backlogged this host's records; it
was never gone.

**So the floor is 22.44 s and the window is 30 s.** `PeerSessionService`
dropped from sixty seconds to thirty, which is the first whole number above the
slowest measured load, and six times the client's own cadence. A peer is now
let go between 30 and 35 s after its last datagram (the reaper's tick is five
seconds, down from fifteen — with a fifteen-second tick the sixty-second window
was really a wait of up to seventy-five).

**A peer dropped by mistake is not lost**, because a session can be started over
§2. That is what makes the shorter window safe to choose from a measurement
rather than from patience: the cost of being early is one rejoin.

## 2. A peer that comes back

### 2.1 A re-sent handshake and a new one are told apart by the counter base

A client sends its handshake again every two seconds until the reply lands, so a
handshake arriving for an endpoint that already has a session is normal:

| capture | client handshakes | counter base |
| --- | --- | --- |
| `mgo2-started-on-solo-game` | t+0.0, 1.9, 3.9, 6.0 | `0x8557bdb7` in all four |
| `mgo2-game-failed-memoryviolation` | t+0.0, 2.1, 4.1 | `0x60024163` in all three |
| `mgo2-game-joined-when-red-team-then-briefing` | t+0.0, 2.1, 4.2 | `0xc0488f0b` in all three |
| the other eight captures | one to three each | one base per capture |

Every copy carries the base the first one did. A client that has restarted draws
a new base, because the value is chosen per run. `RestartPeerIfHandshakeStartsOver`
(`GameplayServerService.SessionRestart.cs`) therefore compares the base:

* **equal** — the client is waiting for an answer it has not had. Nothing is
  reset; the handshake is answered as before.
* **different** — the same client is joining again. The old session is retired,
  removed from `PeerSessionService`, and the handshake provisions a fresh one
  under the same endpoint, exactly as a peer that had never been seen.

### 2.2 Retiring is what ends the workers of the old session

Every writer of this host watches a `PeerSession` object: the run watcher, the
briefing's countdown, the ready-set repeat, the room's own publication and the
beat. They all ask `MatchRecordPeerUtils.IsAlive`, which now answers `false` for
a retired session whatever its clock says. Without that flag they would keep
watching an object the client has forgotten until the idle window ran out — and
the peer is *not* quiet, so they could go on writing to it.

### 2.3 The room state goes the way it goes on a reap

`GameplayServerService.DropReapedPeer` and the restart path now share
`ForgetPeerState`: the room roster entry at both endpoints, the briefing state,
the vitals, and the lobby-slot confirmation held by `TeamArrayService` (which is
keyed by endpoint and not by session, and whose ordinals belong to the
conversation that just ended). The room's round is reset when it has no players
left, the same rule the reaper applies (`MatchStartRunService.ResetWhenEmpty`).
The rejoining client then goes through the ordinary join: profile, roster,
report, entry echo.

Not reset, deliberately: the settings burst is scheduled once per host
(`PlayerSettingsBurstSchedulerService`), because the capture holds one burst in
a whole round and none for the peers that join afterwards.

## 3. What a phase sends that the briefing-start captures do not

`_scratch/phase_diff.py` in this session compared `myserver21.pcapng` — the
newest run of this host that reached a briefing — against the three captures
whose client is on the briefing screen, folding identifiers as above and aligning
on the client's first join report (`e1 <byte>`).

### 3.1 Pre-briefing: join to the first report

| record | live (59.6 s) | `started-on-briefing` (55.8 s) | `started-briefing-then-solo` (54.9 s) | `joined-when-red-team` (42.1 s) |
| --- | --- | --- | --- | --- |
| rules `0x0a61 fe` | 44 | 104 | 0 | 87 |
| room count `0x1a58 00 NN` | 1 | 2 | 0 | 3 |
| state blobs `0x0002` | 8 | 7 | 9 | 22 |
| roster `0x1001` | 37 | 33 | 19 | 59 |
| handshake `0x1000` | 2 | 5 | 4 | 6 |
| answers to the client's early join state | 2 (see §3.2) | 0 | 0 | 0 |
| clock `0x1a54` | 0 | 0 | 0 | 25 |

The blobs, the roster and the handshake replies differ only in count, by
population and by how many handshakes the client re-sent. The rules pair and the
room count are written only by the references that also write them later in the
round — the corpus splits into a **fast rules family** (55 records a second:
`game1`, `game3`, `started-briefing-then-solo-game`, `joined-final-round`, …) and
a **slow one** (2 a second: `started-on-briefing`, `game2`,
`joined-when-red-team`). This host writes 52 a second, which is the fast family's
rate, and it writes the count once per answered peer, which both families do.
`joined-when-red-team` writes its clock 25 times before the report and the live
run writes it none before — the reverse of a record this host adds.

### 3.2 The one record no reference is ever asked for

At t+57.083 the live client sends `0x8a5c 01 de` on its own — the join state
record, under the payload class bit, **2.5 s before its report** and 1.2 s before
its entry echo. The generic answer pass answers it with the two records a
`0x8000`-class record draws (`0xd001 24` and `0xca5c 24`, class 4).

Every reference client sends that record *in the same datagram as its report*
(`0x025c 01` beside `0x12de e1 01` in `started-on-briefing`, t+55.838), where the
report's own answer covers it. No reference host therefore ever writes an answer
to it standing alone, and there is no captured shape for this host to match.

It is left as it is, and the reason is the measurement's shape and not
convenience: the client asks with the class bit that the protocol's answer rule
is written for (`P2P_CONNECT_FSM.md` §7.1c), the two records are that rule's
shape, and the run in which they were written went on to echo its entry and
report normally. Withholding them would be this host ignoring a record the
client marks as needing an answer, on the strength of a capture in which the
question never came up.

### 3.3 The briefing and the round

No record kind is written in either phase that the references do not write, and
none the references write is missing except the players' own tick blocks
(`0x004e` … `0x0085`), which a dedicated host has no other players to relay. The
rates that differ are the fast/slow rules split of §3.1 — the live 52/s against
`started-on-briefing`'s 2/s is the family difference, not a defect — and the
clock, written 65 times in the sixty seconds after the report where
`started-on-briefing` writes it 15 times.

## 4. Files

| file | change |
| --- | --- |
| `Shared/Udp/PeerSessionService.cs` | `DefaultIdleTimeout` 60 s → 30 s; the tick is `ReapInterval` = 5 s |
| `Shared/Types/PeerSession.cs` | `Retired`, so a replaced session's workers end |
| `GameplayServer/MatchRecords/MatchRecordPeerUtils.cs` | `IsAlive` answers `false` for a retired session |
| `GameplayServer/GameplayServerService.cs` | the reaper's window comes from the constant; `ForgetPeerState` shared with the restart path |
| `GameplayServer/GameplayServerService.SessionRestart.cs` | new — a handshake with a new counter base retires the old session |
| `GameplayServer/GameplayServerService.Answers.cs` | new — the record-level answer pass, extracted so the dispatch file is back under 400 lines |
| `GameplayServer/Rooms/TeamArrayService.cs` | `Forget`, so a rejoining peer is served the next confirmation run and not the last one |
| `docs/GAMEPLAY_SERVER.md` | the window, the rejoin path, and the cleanup entry in the file map |

## 5. What would settle the rest

* **A capture of a real rejoin.** Every reference capture holds one handshake
  base per client, so the restart path has no capture of its own: it is derived
  from the base being per run. A run in which the client is closed and reopened
  would show whether it re-handshakes with a new base and whether it accepts the
  fresh counter space, or whether it expects the old session to be continued.
* **The protocol's own answer to `0x8a5c`.** §3.2 holds because nothing captured
  answers it; a capture of a reference host receiving it alone would settle
  whether the two records this host writes are the recorded answer.
