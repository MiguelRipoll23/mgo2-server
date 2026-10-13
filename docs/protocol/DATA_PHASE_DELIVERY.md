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
sent went unrepeated. The run is the one record the captures gate, so the rule
this server holds is that it is written **for** a report and never before one:
`MatchStartRunService`, driven by `PlayerReadyHandler`, opens the round the
moment the report arrives.

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
run. **[V]**

**Written it is three records, and the report's byte is what the third echoes.**
`e1 14` is answered `e0 14 …` and `e1 01` is answered `e0 01 …`, so the run's
payload cannot be built before the report arrives: there is no report to echo, and
the byte the capture shows is the client's own. A run that goes out first carries
the recorded host's `0x14` as a stand-in — the fallback this server used — under a
client that reported `0x01`.

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

## The round start is written for the report, and never before one

The marker, the run and its four repeats go out on the report — the run's
payload echoing the byte that report carried — and on nothing else. There is no
round on a clock: the sparse phase closes at its measured 42 240 ms and the beat
follows it, and the round opens when the client asks. `PlayerReadyHandler`
recognises the report and hands it to `MatchStartRunService`, which latches the
round for as long as that round's own frames are going out.

What the earlier measurements decided, and still decide:

| what | observed |
| --- | --- |
| the beat alone keeps the channel moving | the client ACKed the beat's records and stayed in the session |
| the phase can close before the report arrives | the phase ended on the measured 42.2 s; that client's first report came at 45.5 s |
| a run written **before** the report is not the client's round | the run echoed the stand-in `0x14` while the report carried `0x01`; the client ACKed the run's records, never entered the round and reported 38 times |

The third row is the whole reason this rule is about *when* rather than *whether*.
The run is the host's answer to a record, so it can only be written once the
record has arrived — and that is a different thing from not writing it. The
earlier state, where the report was recognised and dropped, left a live client
sitting in its ready state with `CONNECTION NOT STABLE` and no round: the map
loaded, the roster was acknowledged, the beat kept moving, and the round never
opened because nothing opened it.

## The report arrives with a presence record, and the run answers both

The readiness report is never sent alone: in five of five reports across the two
captures the client puts a one-byte record in the same datagram, on an
identifier that splits the same way (`UDP_GAME_CAPTURE.md` §3.2). The byte it
carries is the reporting player's own lobby slot — the same byte the run's
payload echoes back — so it says which player has arrived rather than what that
player has decided, and it is named for that now (`PlayerPresenceHandler`,
`BRIEFING_FLOW.md` §1). So the round answers two records, not one, and
`MatchStartRunService` writes the presence record's answer inside the run where
`MatchStartRunFramesUtils` places it — behind the two arrays, the one position a
capture measures.

**The answer's first byte is measured, and it is the room's players.** It is a
bit set that grows as the room's players report: `mgo2-game-started-on-briefing`
answers `02` for its first reporter, then `06`, `0e` and `1e` as the other three
report — each answer written within half a second of the report it follows and
carried through the briefing, long before the round opens at t+198.4 — and
`mgo2-game-joined-when-red-team-then-briefing` answers `2a`, bits 1, 3 and 5 for
the slots its reporting players hold. What is not measured is the trailing three
bytes, and what this host writes is game2's one-player body, `02 00 00 00`,
marked **[U]** on `MatchStartBodiesUtils.PlayerPresenceAnswer`: a room of more
than one needs the set recomputed as its players report and leave, which this
host does not do yet. If a run written with it is rejected, that body is the
first thing to change — the report's own answer, which is what gated the round,
does not depend on it.

Inbound, the presence record has a handler of its own
(`PlayerPresenceHandler`) so that the relay it needs is a decision rather than
the default an unnamed gameplay record falls into.

## The beat starts at the round open, not at the sparse phase's close

Everything the beat writes is measured as an offset from the **run**, not from
the sparse phase: the first item-box group 2.36 s after it, the vitals 27.0 s
after it, and the two roster records that ride the vitals onset at exactly that
instant. A beat that starts when the sparse phase closes spends its two opening
item-box groups before the client has reported itself ready — measured on a live
client, seven seconds early — so the client is told about boxes it is not yet in
a round for and never hears about them again, and the vitals land four seconds
early with those two records against the wrong window.

`MatchRoundStateService` is the one thing both writers share: the beat waits on
it, the round opener writes it. The wait is bounded at 30 s — every capture
reports within 4.1 s of the sparse phase closing and the live client within
6.8 s — so a client that never reports cannot leave the channel silent.

## The lobby-slot array handshake, which only advances on a confirmation

Four record kinds, one loop, measured in both captures:

```
host   0x9001 26B  85 NN      the team array
host   0x1001 26B  86 NN      the occupancy array
client 0x9001  2B  82 NN      the client answers
client 0x1001  2B  84 NN
host   0x1001  2B  81 NN      the host confirms   ← the only thing that advances
host   0x9001 26B  85 NN+1    the pair again, under the next sequence
```

game1 runs it at the roster: the client answers `82 f3`/`84 f3` and the host
confirms `81 f3` 94 s later, when a second player fills a slot and the sequence
steps to `f4`. game2 runs it tighter — `81 00` twice, and the client's next answer
is `82 01`/`84 01`. **The client only moves to the next sequence after the host
confirms the one before it.** **[V]**

`TeamArrayService` answers the confirmation and writes the pair again under the
next sequence. Before it, the live client sent `82 f3`/`84 f3` once and never
again, because the sequence had nowhere to go.

**What the pair carries is the room's, and it is read per publication.**
`LobbySlotService` pairs the room's own roster — which holds each player's key in
the 24-entry player table, because the host is what handed it out — with the side
`game_players.team` holds, read through the games domain's service. Both writers
name it: the run publishes the pair it was built with, and a confirmation reads
the room again, so a briefing in which a player changed sides republishes the
room as it now is rather than as the run first said it. `UDP_GAME_CAPTURE.md` §3.2
has the measured shapes — slots marked `07` in the occupancy array, sides `0` and
`1`, `e0` for a player the room puts on no side, and the pair's two consecutive
index bytes, which is what the replay parser tells a pair by.

## Where this sits

- `UDP_GAME_CAPTURE.md` §3.2 is the byte-level pairing of the readiness report
  and the round start; this file is the delivery rule built on it.
- `RPDT_REPLAY_RECORDS.md` maps the bodies these records carry.
- The replay itself: `src/GameplayServer/MatchRecords/MatchStartRunService.cs`
  (the round opener), `MatchStartRunFramesUtils.cs` (its measured frames) and
  `MatchRecordService.cs` (the per-peer beat).
