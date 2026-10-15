# The room-state records a briefing carries

**What this file answers.** The reference hosts write three roster-shaped records
this host did not: a **22-byte** record opening `07 0f` and a **pair of 54-byte**
records opening `07 2f`. They ride with the close pair (`07 0d`) the run already
carries, in the round's opening run and again through a briefing. This records
what is measured about them and what was written.

## 1. The bytes, and they are the room's stage rather than a session's

| capture | the 22-byte record | the two 54-byte entries |
| --- | --- | --- |
| `mgo2-game1` | `070f0003000000407cfc6c07acf47cfc6c07acf4fd00` | `072f000100000000260100ec2c…3c7d`, `072f000101000001260100983a…807f` |
| `mgo2-started-briefing-then-solo-game` | **identical** | **identical** |
| `mgo2-game2` | `070f000300000050640014ec5700640014ec5700fd00` | — |

So the three bodies are **byte-identical across two captures that share no
origin, no client and no session** — `mgo2-game1` and
`mgo2-started-briefing-then-solo-game` — and differ in `mgo2-game2`. That makes
them a fact about the stage the room is playing rather than about the session
replaying it, which is why this host writes the recorded bytes, exactly as the
close pair beside them already does (`MatchRecordBodiesUtils.MatchStartCloseFirst`):
**[U]** what they state, **[V]** the bytes and their agreement.

`mgo2-game1` also writes a *changed* form of the pair at t+765.794 and t+768.155,
where the fourth and fifth bytes of each entry move (`…26 01 00…` →
`…26 00 00…`), so at least those two bytes are live state and not a constant of
the stage.

## 2. Where they ride

| capture | the group | when |
| --- | --- | --- |
| `mgo2-started-briefing-then-solo-game` | 22 B + both 54 B | t+57.626, t+60.0, **t+174.371 — the round's opening run** |
| `mgo2-game2` | 22 B | t+62.2 (×3), t+64.4, **t+109.120 — the run**, t+224.6 |
| `mgo2-game1` | 22 B + both 54 B | t+466.0, 470.5, 472.6, 565.5, 568.0, 765.8, 768.2 |
| `mgo2-game1` | the `07 0d` pair alone | t+46.4 (×2, **the run**), 48.8, 51.3, 56.4 (×4), 194.0 (×4, **the second run**), 196.4 |

Two things follow, and they are what the change is built on:

* **The run carries them.** `mgo2-started-briefing-then-solo-game`'s run numbers
  its `0x1001`-family records `0x16`…`0x1d` — the two arrays, the 22-byte record,
  the two 54-byte entries, the `07 0d` pair and the roster close — and every one
  of them is written in the run at t+174.371. This host's run wrote eleven
  records where the capture writes fourteen.
* **A briefing carries them.** The three records are written 2.2 to 2.6 s apart
  in groups of two or three through a briefing (and, in `mgo2-game1`, through a
  round as well), which is the cadence of the operations tick this host already
  writes its briefing countdown on
  (`BriefingCountdownBodiesUtils.PublishPeriodMilliseconds`). **Whether the
  group belongs to the tick or to an event this reader has not identified is
  [U]** — nothing in the captures marks it, and the gaps between groups are 45 s
  to 197 s where they are longer than 2.6 s — so this host writes the three
  records where it already writes, and the capture is what would move them.

## 3. What was changed

* `MatchRecordBodiesUtils` carries the three bodies (`RoomStateRecord`,
  `RoomStateEntryFirst`, `RoomStateEntrySecond`) and a shared
  `RosterShapedRecord` builder for the version and length prefix they have in
  common with the close pair.
* `MatchStartRunFramesUtils.BuildRun` writes them between the arrays and the
  close pair, so the run's `0x1001` family is the capture's eight records with
  the capture's ordinals (`RosterOrdinalCount`).
* `BriefingCountdownService` writes them beside the countdown on every tick of a
  room whose round has played, which is the one thing this host writes through a
  briefing. A fresh room's first briefing writes none of them: the group is the
  room's state rather than the phase's, and the reference that joins an unplayed
  room carries neither record before its round
  (`docs/analysis/join-stall-after-mode2.md` §10).

## 4. Limits

* The bodies are the **recorded stage's**, not this room's: a room on the stage
  `mgo2-game2` plays is served `mgo2-game1`'s bytes. Nothing maps a map to these
  bytes here.
* The two live bytes §1 finds are written as recorded too.
* **Not verified live.** No client has been sent these records yet; what a
  client does with them is what would confirm them, and the two live captures
  that lacked them are the baseline it is measured against.
