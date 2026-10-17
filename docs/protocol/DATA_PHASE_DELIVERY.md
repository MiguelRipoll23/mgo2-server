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
record (`0x1ade` / `0x12de`, body `e0 NN …`), written together with the thirteen
records behind them: the two empty `0x5001` roster heads, the operations state
block and its two-byte companion, the two lobby-slot arrays, the close triple
(two twenty-byte records and the seven-byte roster close), the round's clock, the
payload and the rules pair. Decompressed, `mgo2-game1.pcapng`'s run is that
thirteen, in that order, and `mgo2-game-started-on-briefing.pcapng`'s is the
same thirteen with a live item-box group in front of them. Both captures show it only after the client's readiness
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

### The two records a briefing is sent, and the beat is not their only writer

The rules pair is above as the beat's, and it is not the beat's alone: a host
writes it from the roster answer onward and on every tick of a briefing, and
the beat takes it over when the round opens. `mgo2-game-started-on-briefing`
writes `0x0261 fe` under both classes 12 to 26 times per ten seconds from its
roster answer through its whole 198 s briefing — the pair rides inside its
sparse controls and blobs and beside its state blobs — and both captures that
hold a join write it in the roster run itself, ahead of the roster entries.
`MatchRecordFramesUtils.RulesPair` is that record, and
`BriefingRoomPublishService` writes it for as long as the room is briefed.

The second is the room's own count: a two-byte `0x1258`/`0x1a58` record whose
second byte is how many players the room holds. It is not an answer either —
`udp_response_probe.py --session-only` lists it among the records that never
followed a client silence longer than two seconds — and the Lobby's own details
for the same room settle what the byte counts: `started-on-briefing` joins
`Best TDM [EU]`, whose `0x4313` reply reads **one player of a limit of
seventeen**, and that host writes `00 01` beside the join and `00 02` once a
second player is in the room; `joined-when-red-team` writes `00 05` into a room
of five joiners. So the byte is the room's count and not its limit, and the
host's own seat is not in it (`MatchRecordBodiesUtils.RoomCount`,
[`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md) §1). **[V]** on the byte; **[I]**
on the room's count being the same population the roster holds.

## The round start is written for the report, and never before one

The marker and the run — and the run repeated behind it, one to three sets in the
captures' own flows (`MatchStartRunFramesUtils.RepeatCount`) — go out on the
report, the run's payload echoing the byte that report carried, and on nothing
else. There is no
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

**The answer's first byte is derived now, and it is the room's reporters in the
order they reported.** The measured values settle it: `mgo2-game-started-on-briefing`
answers `02` for its first reporter, then `06`, `0e` and `1e` as the other three
report — each answer written within half a second of the report it follows and
carried through the briefing, long before the round opens at t+198.4 — and then
`16` once the **third** of the four has left. Under one bit per reporter that is
bit 1, then bits 1–2, 1–3 and 1–4, then bits 1, 2 and 4: the set grows by a bit
per report and loses the bit of the reporter that left.
`mgo2-game-joined-when-red-team-then-briefing`'s `2a` is bits 1, 3 and 5 — the
first, third and fifth reporters of a room whose second and fourth never did.
Read as the players' *lobby slots* the bits do not fit: the slots that same
capture's own arrays name are `0x0b`, `0x0f` and `0x14`, and none of the three is
a bit of a byte.

So it is not the hard-coded one-player body any more: `PlayerReadyService.PresenceMask`
writes the room's own set, and a room of one writes `02` because its only
reporter is first. What is still **[U]** are the trailing three bytes, which have
never moved in any capture and stay the recorded zeros.

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

Six record kinds, one loop, measured in both captures:

```
host   0x9001 26B  85 NN      the team array
host   0x1001 26B  86 NN      the occupancy array
client 0x9001  2B  82 NN      the client answers
client 0x1001  2B  84 NN
host   0x1001  2B  81 NN      the host confirms   ← the only thing that advances
host   0x9001 11B  0b 00 01 00 00 00 00 00 08 07 NN+1   the next sequence, named beside it
host   0x1001 10B  0b 00 01 00 00 00 00 00 00 00        its two companions, per side
host   0x1001 10B  0b 00 01 00 00 00 00 00 00 01
host   0x9001 26B  85 NN+1    the pair again, under the next sequence
```

game1 runs it at the roster: the client answers `82 f3`/`84 f3` and the host
confirms `81 f3` 94 s later, when a second player fills a slot and the sequence
steps to `f4`. game2 runs it tighter — `81 00` twice, and the client's next answer
is `82 01`/`84 01`. **The client only moves to the next sequence after the host
confirms the one before it.** **[V]**

**The next sequence is named beside the confirmation, and it is the confirmation's
other half.** A `0b 00 01 00 00 00 00 00 08 07 NN+1` record carries the sequence
the pair is about to be republished under — its last byte is that sequence, and
the two before it are the pair's own index bytes — with two ten-byte companions
that differ only in their last byte, `00` and `01`. Every reference capture writes
the group the moment it confirms and the client answers `NN+1` only after it:
`mgo2-game1` at t+185.2, `mgo2-game2` at t+104.6,
`mgo2-started-briefing-then-solo-game` at t+170.4 and
`mgo2-game-started-on-briefing` at t+193.6. A host that confirms and republishes
the pair without the group gets one answer and then silence — which is what the
live captures hold. **[V]** on the shape and the order in all four;
**[U]** on what the two companions' names are.

`TeamArrayService` answers the confirmation, writes the group and republishes the
pair under the next sequence. Before it, the live client sent `82 f3`/`84 f3`
once and never again, because the sequence had nowhere to go.

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
