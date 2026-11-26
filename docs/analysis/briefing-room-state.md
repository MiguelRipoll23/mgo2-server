# The room-state records a briefing carries

**What this file answers.** The reference hosts write three roster-shaped records
this host did not: a **22-byte** record opening `07 0f` and a **pair of 54-byte**
records opening `07 2f`. They ride with the close pair (`07 0d`) the run already
carries, in the round's opening run and again through a briefing. This records
what is measured about them and what was written.

## 1. The bytes, and they are the stage's own targets rather than a session's

| capture | the 22-byte record | the two 54-byte entries |
| --- | --- | --- |
| `mgo2-started-briefing-then-solo-game` | `070f000300000000840384033efd840384033efdfd00`, and `070f0003010000013efd7d0307033efd7d030703fd00` beside it | `072f000100000000260100ec2c…3c7d`, `072f000101000001260100983a…807f` |
| `mgo2-game1` | `070f0003000000407cfc6c07acf47cfc6c07acf4fd00` | `072f000100000000260100ec2c…3c7d`, `072f000101000001260100983a…807f` |
| `mgo2-game2` | `070f000300000050640014ec5700640014ec5700fd00` | — |

The first row was written from a body that is **not in that capture**: decoding
`briefing-then-solo-game` end to end gives the two records above at t+9.780,
t+11.579, t+13.379 and again in the round's opening run at t+147.650, and no
`…40 7c fc 6c 07 ac f4…` body anywhere. The pair's own points are
`(900, 900, -706)` and `(-706, 893, 775)` with the payload's tag repeating the
record's index, which is what `CustomPointRecordBodiesUtils` writes; the two
`0x40`/`0x50` bodies are other stages' and are left as the rows below quote them.

So the three bodies are **byte-identical across two captures that share no
origin, no client and no session** — `mgo2-game1` and
`mgo2-started-briefing-then-solo-game` — and differ in `mgo2-game2`. That makes
them a fact about the stage and the rule the room is playing rather than about
the session replaying it.

**The 22-byte record is one of that rule's objective points, and this section's
earlier reading of the 54-byte pair as "the room's stage" did not survive the
props.** `0x40` is Tomb of Tubes' Bomb site, `0x50` Blood Bath's Sneaking Capture
target, and the two `070f 00 03` bodies above are Blood Bath's Capture pair
`PRP_RUGBY_TGT_01/02` in the wire's `x`/`z` ÷ 10, `y` + 900 form;
`docs/analysis/custom-points.md` is the conversion, the four captures and the
per-stage table. **A rule that places no target is written no kind-`0x03` record at
all**, which is the change §6 records: the pair is not replayed on a stage, it is
drawn from the round's rule and the round's map.

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

* `MatchRecordBodiesUtils` carries the 54-byte bodies (`RoomStateEntryFirst`,
  `RoomStateEntrySecond`) and a shared `RosterShapedRecord` builder for the
  version and length prefix they have in common with the close pair.
* `CustomPointRecordBodiesUtils` carries the 22-byte kind-`0x03` body, built from
  a point and its task; `CustomPointUtils`/`CustomPointTableUtils` say which
  points a round's rule places and where they are on its stage.
* `MatchStartRunFramesUtils.BuildRun` writes one kind-`0x03` record per point
  between the arrays and the close pair, and none for a rule that places none, so
  the run's `0x1001` family is the capture's records with the capture's ordinals
  (`RosterOrdinalCount` takes the point count).
* `BriefingCountdownService` writes the next round's points beside the countdown
  on the room's 2.35 s tick, for a room whose round has played, which is the one
  thing this host writes through a briefing. A fresh room's first briefing writes
  none of them: the group is the room's state rather than the phase's, and the
  reference that joins an unplayed room carries neither record before its round
  (`docs/analysis/join-stall-after-mode2.md` §10).

## 4. Limits

* The 54-byte bodies are still the **recorded stage's** in the one sense that
  matters: they are written as recorded, from the capture rather than from a
  property of this room. The 22-byte body is not — it is drawn from the round's
  rule and map (`CustomPointTableUtils`).
* The two live bytes §1 finds are written as recorded too; whether they are what
  kind `0x03`'s `task` nibble is, this section does not claim.
* **Not verified live.** No client has been sent these records yet; what a
  client does with them is what would confirm them, and the two live captures
  that lacked them are the baseline it is measured against.
