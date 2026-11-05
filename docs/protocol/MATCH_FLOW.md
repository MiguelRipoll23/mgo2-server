# The match flow: briefing, round, and the game that holds them

**What this file is.** The measured shape of a room over time, from the fifteen
captures in `.pcapngs/`: what the room states about itself, the three phases a
client sees (briefing, round, and the end of a game), and the two kinds of
boundary between them. It is the companion to
[`BRIEFING_FLOW.md`](BRIEFING_FLOW.md), which reads one briefing in detail; this
file reads whole sessions.

Every claim is a measurement from a capture named beside it. Where a capture does
not settle a question it is marked **[U]** rather than answered.

**How it was measured.** Each capture's gameplay session is decoded with the
session key from its two handshake counter bases (`tools/udp_frame.py`), the
operations stream's clock record (`0x1254`/`0x1a54`, body `01 <u16>` in 125 ms
units) is read in order, and the room state is taken from the host's 161-byte
`0x1001` body. The script is the one `tools/udp_flow_summary.py` uses; the fields
below are the ones `docs/analysis/rotation-payload-corpus.md` names.

## 1. The room states its own place

The 161-byte room-state body carries three fields this file reads:

| body offset | field | values measured |
| --- | --- | --- |
| `0x4e` | **number of rounds** | `2` (`joined-on-game-solo`, `started-on-solo-game`), `3` (eleven captures), `6` (`mgo2-game2`) |
| `0x55` | **current rotation slot** | `0`–`14` across the corpus |
| `0x09` | phase | `2` briefing, `3` playing, `4`, `5` |
| `0x1a` | the sixteen rotation triples, three bytes each | rule, map, flags |

The corpus, one row per capture (slot and phase as the host first states them):

| capture | rounds | slot | phase |
| --- | ---: | ---: | ---: |
| `joined-final-round-solo-then-final-round…` | 3 | 0 | 3 |
| `joined-on-briefing-then-game` | 3 | 0 | 2 |
| `joined-on-game-solo` | 2 | 3 | 3 |
| `mgo2-game-failed-memoryviolation` | 3 | 0 | 2 |
| `mgo2-game-joined-when-red-team-then-briefing` | 3 | 0 | 5 |
| `mgo2-game-started-on-briefing` | 3 | 0 | 2 |
| `mgo2-game-started-team-selection-selected…` | 3 | 7 | 3 |
| `mgo2-game1` | 3 | 12 | 3 |
| `mgo2-game2` | 6 | 0 | 2 |
| `mgo2-game3` | 3 | 13 | 3 |
| `mgo2-joined-game-in-progress-last-round` | 3 | 14 | 4 |
| `mgo2-started-briefing-then-solo-game` | 3 | 13 | 2 |
| `mgo2-started-on-game-round1` | 3 | 9 | 4 |
| `mgo2-started-on-solo-game` | 2 | 0 | 3 |

`mgo2-game2` is the outlier and it is real: `6` rounds, a TSNE/SCAP rotation.
`0x4e` is the room's own setting, one value for the room — not a per-map count.

## 2. The rotation is a list of rounds, and a game's rounds repeat its entry

The sixteen triples are **one entry per round**. A game played for several rounds
appears as that many consecutive identical triples, and the room advances the
slot one entry per round — the client's own walk (`FUN_0027bb10`, `3*(slot+1)`,
wrapping past 14; `rotation-payload-corpus.md` §4).

`mgo2-started-on-game-round1` is the measurement. Its rotation, as the room
states it at t+3.3:

```
0:[12,5,0]  1:[3,1,0]  2:[5,1,0]  3:[9,5,0]  4:[10,1,0]  5:[17,1,0]
6:[7,5,0]   7:[4,1,0]  8:[6,1,0]  9:[6,1,0] 10:[6,1,0]
```

Slots **8, 9 and 10 are byte-identical** `[6,1,0]`, the room's stated round count
is **3**, and the room sits on slot 9. That is a game of three rounds of the same
rule on the same map, and it is written as three identical entries.

So a rotation that reads "map 1 twice, map 2 three times, map 3 once" is the six
entries `1,1,2,2,2,3` — the grouping exists on the wire as repetition, and a
reader that only takes `triples[slot]` still gets the right round.

## 3. Briefing

A briefing is a stretch in which the room writes the **operations clock as
`01 00 00`** (a played room) or as the coming round's whole time (a room that has
not played), and in which the clients write their own briefing rows and readies.

| capture | briefing clock | what it is |
| --- | --- | --- |
| `mgo2-started-briefing-then-solo-game` t+57.6 … t+170.4 | `2400` held, `01 60 09` | fresh room: the whole time of the round it precedes |
| `mgo2-started-on-game-round1` t+191.6 … +211.6 | `0`, six writes | after a played round |
| `mgo2-started-on-game-round1` t+526.0 … +598 | `0`, ten writes | after a played round |
| `mgo2-started-on-game-round1` t+1242.2 … +1270.2 | `0` | after a played round |

The other records of a briefing are in [`BRIEFING_FLOW.md`](BRIEFING_FLOW.md) §1–§3:
the client's report (`e1 <slot>`), its five `1d <slot> 00..04` rows and the
22-byte `22 <slot>` row behind them, the host's `e0` publications one per player
in report order, the `0x0b` countdown, and the run the briefing is answered with.

**The round opens on the report that follows the briefing, not on a ready and not
on the clock.** In `mgo2-game-joined-when-red-team-then-briefing` the room readies
`1f 05 01` at t+112.750 and the host's countdown writes its zero at t+113.758 —
the ready set complete, and no round — while that briefing's run lands at
t+132.791, after the client's own records; its join report was at t+57.656. A room
of one arms no deadline at all: its count is held and its round waits for its own
post-briefing report (`BRIEFING_FLOW.md` §2).

## 4. Round

A round opens under the **sibling identifier set** — every identifier of the
briefing's set with bit `0x800` flipped
(`MatchRecordGenerationUtils`) — and writes the coming round's whole clock, which
then counts down in real time on the same stream.

`mgo2-started-on-game-round1`, round after round:

| t | clock | |
| --- | --- | --- |
| +215.6 | `2400` | round opens |
| +522.0 | `32 → 0` | 306 s later: the round's clock runs out |
| +601.4 | `2400` | next round opens |
| ~+901 | `2400` | next round opens — **no briefing between** |
| +1238.2 | `32 → 0` | |
| +1353.3 | `2400` | next round opens |

The clock's own arithmetic is the round's length: 2 400 units × 125 ms = 300 s,
and the open at +215.6 has ended by +522.0.

## 5. The two boundaries

**A round boundary inside a game.** The round ends (its clock reaches 0), the
next round opens on the sibling set, and **no briefing runs**: the t+601.4 →
+1238.2 stretch of `mgo2-started-on-game-round1` is 637 s and covers two
300-second rounds with **no zero-clock stretch anywhere between them**. This is
the round pair the client shows as "ROUND 2/3" then "FINAL ROUND": the game is
still the same, and nothing is re-briefed.

**A game boundary.** The game's last round ends and the room returns to a
briefing: clock `0`, ready set and rows from the clients, the `0x0b` countdown,
and the run the briefing is answered with; the next game then opens on its own
whole clock. The three zero-clock stretches of
`mgo2-started-on-game-round1` are these, and `BRIEFING_FLOW.md` reads the same
shape out of `joined-when-red-team` (two briefings and two games in 930 s).

**What the corpus does not settle [U].** The largest capture's briefings do not
fall on a strict every-third-round grid — one of them follows a single 5-minute
round (t+522.0) — so a room can also re-brief inside a game, which the capture
does not distinguish from a game boundary that arrived early (a player joining,
a rotation change). What **is** settled: a briefing is not owed to every round
(the 637 s pair proves it), every briefing after a played round writes clock
`0`, and a briefing's countdown and ready set are what a round opens from.

## 6. What this host does

`MatchStartRunService.End` closes the round and then **always** begins a
briefing (`briefingStates.BeginNextBriefing()` + `BeginBriefing()`), which makes
every round its own game: a room whose rotation repeats an entry — the wire's
form of a multi-round game — is briefed between two rounds that the reference
plays back to back.

The model this file supports, and that the host now follows:

- a **game** is a maximal run of identical rotation triples, so
  `MatchSettings.IsLastRoundOfGame(n)` is "the entry after this round names a
  different rule or map";
- a round that is **not** its game's last leaves the ready set standing and opens
  the next round on it, with no briefing, no countdown and no `0` clock;
- a round that **is** its game's last returns the room to a briefing, whether or
  not a round has played before (the clock it is held on is `0` once one has).

## 7. Open

- whether a client inside a game is re-briefed when a player joins mid-game [U];
- the `0x4e` value the host writes for a rotation whose runs are of different
  lengths — the wire's `Number of Rounds` is one value per room, while the
  rotation can state runs of 1, 2 and 3 rounds side by side
  (`mgo2-started-on-game-round1`) [U];
- the phase byte at `0x09` for a game boundary as opposed to a round boundary:
  the corpus shows `2`, `3`, `4` and `5` and none of them is tied to a boundary
  yet [U].
