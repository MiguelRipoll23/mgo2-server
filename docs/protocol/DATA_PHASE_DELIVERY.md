# What the data phase answers, and what it sends on its own

Once the join is over, the gameplay channel is a data phase: records that
describe a running round rather than a roster. The replay in
`src/GameplayServer/MatchRecords` writes those records, and this file is the rule
for *when* — which of them are the host's answer to a client record and must wait
for it, and which the recorded host sends unprompted.

It exists because sending all of them on a clock gets the bytes right and the
behaviour wrong. A match-start run written before the client reports itself ready
is a round opened on nobody; the live client then sat in its ready state and
reported twenty-three times over two minutes while the run it had already been
sent went unrepeated. The fix is not a different body but a different gate.

## Method

`tools/udp_response_probe.py` measures the pairing instead of assuming it. For
every outbound record it reports how long the host had been quiet — how long since
the last inbound record — before each occurrence, and the inbound type that most
often preceded it:

```
python3 tools/udp_response_probe.py mgo2-game1.pcapng --window-ms 2000 --session-only
python3 tools/udp_response_probe.py mgo2-game2.pcapng --window-ms 2000 --session-only
```

`--session-only` drops the in-game tick records (`id < 0x1000`) from both
directions, so a pairing is not drowned by the 30 Hz stream. Without it the tool
reads the tick traffic too, which is what shows the beat records are *not* gated.

A type listed `ALWAYS` had no occurrence after a client silence longer than the
window. That is the whole of the test: a record that never went out unprompted is
a response; one that did went out on its own.

## The one record that waits for a client packet

**The match-start run.** It is the marker (`0x5ade` / `0x52de`) and the payload
record (`0x1ade` / `0x12de`, body `e0 NN …`), written together with the twelve
records behind them. Both captures show it only after the client's readiness
report — the same payload record read the other way, body `e1 NN` — and
`UDP_GAME_CAPTURE.md` §3.2 measures every round in every capture beginning
0.6–5.5 s after one and none beginning without one. **[V]** on the pairing, **[I]**
on the client's intent.

**Its answer is the marker, and it goes out once.** The marker's identifier is
the report's own with the answer bit set — `0x5ade` for `0x1ade`, `0x52de` for
`0x12de` — so the record that opens the round and the answer to the report are
the same record. A host that also answers the report in the pass that received
it puts two of them on the wire: the live run logged `answering 1 reliable
records: 0x5ade` and sent `0x5ade` at counter 34, then wrote the run's own marker
`0x5ade` at counter 36, ahead of the `0xd001` the capture writes first. game1's
solo round carries one host `0x5ade` for its report, at t+46.4155 and with the
run. So the dispatcher leaves the report unanswered in that pass
(`GameplayServerService.IsReadinessReport`,
`MatchRecordGenerationUtils.IsReadinessReportIdentifier`) and the writer's marker
is the one answer. **[V]**

The probe finds no other record our replay writes that is gated this way in both
captures. `--session-only` on game1 — the quieter client, 492 frames against the
host's 18 526 over the round — lists the host records that never followed a
client silence longer than two seconds as `0x1250`, `0x1258`, `0x12de`, `0x52de`,
`0x9250`, `0x9256`, `0x92de`, `0x9a5a` and `0xda54`, and of those the replay
writes only the `0x12de` / `0x52de` payload. game2 lists the same round pair plus
more of the world-object family, because its client is chatty enough that almost
everything lands within two seconds of something. **[V]** as a measurement of the
two captures.

## The records that do not wait

These are sent after long client silences in game1, so they are not answers and
must not be held for one:

| record | what | longest client silence before it (game1) |
| --- | --- | --- |
| `0x090c` | the beat's health/stamina-shaped record | 31 256 ms |
| `0x0a61` ×2 | the beat's rules-roster pair | 31 256 ms |
| `0x0002` | the state blob | 29 251 ms |
| `0xd001` | the one-byte control | 4 805 ms |
| `0x1a54` / `0x9a54` | the operations round clock | 31 256 ms |
| `0x1a50` / `0x1a5a` | the item-box state group | 29 788 ms |

They tick on the measured 33 ms beat and the capture's insertion periods
(`MatchRecordFramesUtils`). Holding the beat for a client packet would stall the
channel the recorded host keeps moving; writing the round start on that beat is
the mistake this document exists to prevent. **[V]**

## The round state this is built on

`MatchRoundService` owns the room's rounds; a peer's writer
(`MatchRecordService`) follows them. The state is:

| field | what it is |
| --- | --- |
| current round | counted from one; zero before the first opens |
| maximum rounds | the room's own rule: team deathmatch's round word in the host's saved settings (`MatchRoundSettingsUtils`), zero meaning the room plays on |
| generation | the identifier set the round plays under — the first round's, then the same set a `0x800` bit along, alternating |
| round boundary | `MatchRecordFramesUtils.RoundLengthMilliseconds`, 147.6 s, the measured gap between game1's first run (t+46.42 s) and its second (t+194.04 s) |

A round is **due** when it is the first or when the boundary has elapsed, and it
opens only when **every peer in the room has reported itself ready** — the
barrier that keeps a run off a loading screen. The reports are taken when it
opens, so a client that reports again while the round goes out queues one for the
round after it, which is the capture's own cadence (game1's client reports 1.66 s
before each of its rounds, not continuously).

**A peer that joins mid-round joins the round.** Its entry round is remembered;
it beats the round in progress under that round's generation and takes the run
when the room opens the next one. That is game1 exactly: the second player joins
at t+154.3 s, its records keep the first generation until t+194.0 s, and the run
it then receives is the second round's. Writing the run to a late arrival would
reset the stream under a client already in it.

## Where this sits

- `UDP_GAME_CAPTURE.md` §3.2 is the byte-level pairing of the readiness report
  and the round start; this file is the delivery rule built on it.
- `RPDT_REPLAY_RECORDS.md` maps the bodies these records carry.
- The replay itself: `src/GameplayServer/MatchRecords/MatchRecordService.cs` (the
  per-peer writer) and `MatchRoundService.cs` (the room's rounds).
