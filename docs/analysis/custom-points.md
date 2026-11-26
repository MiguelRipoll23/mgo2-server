# Custom points: a stage's objective markers, and which rules get them

**What this file answers.** The kind-`0x03` record this host wrote as "the room's
own stage" is **one objective point of the round's stage**: where the mode puts
the target it asks the player to take, blow up, trace or carry. Which points a
round gets is the rule's, not the room's — a Team Deathmatch round has no target
at all, and sending the recorded pair in one is what made KEROTAN and GA-KO appear
in a mode that has neither. This records the conversion, the four captures that
fix it, the rule ladder the task byte sits on, and the table every stage's points
come from.

**Reading order.** §2 is the byte-level evidence; §3 the conversion; §4 the rule
gate and its procs; §5 the table and how to regenerate it; §6 what the code does
with it now; §7 what stays open.

## 1. What the record is

The 22-byte kind-`0x03` body is a tag, one point written twice, and a sixteen-bit
tail (`[07 0f 00 03] [index] 00 00 [tag] [x y z] [x y z] fd 00`). The client's
record dispatch (`0x282D80`) hands it to the stage object's node 3, whose slot 1
parses the payload from `+7`; the native port's supervisor keeps custom points and,
per point, a list of actions (`sv_custom_point.cc` bounds-checks both with
`over custom point %d` and `over action %d`). So the record is a point *and* the
action it is placed for, which is why the point is written twice: one body per
side of the action, and the two sides of the recorded pairs differ in x and z
while y stays the ground height.

The `+4` byte is the point's index within its task, and the tag at `+7` repeats it
in its low nibble with the task in the high nibble — `0x00`/`0x01` for Capture's
pair, `0x40` for Bomb's single site, `0x50` for Sneaking Capture's, `0x60`/`0x61`
for Race's. The doubling and the tail are constants of the body; the point is the
stage's.

## 2. The measurements

Four captured bodies, each matched back to the prop its stage's script hands to the
target placement:

| capture | rule | body | decoded | the stage's own prop |
| --- | --- | --- | --- | --- |
| `briefing-then-solo-game` t+9.780 | Blood Bath, Capture | `070f000300000000840384033efd840384033efdfd00` | `(900, 900, -706)` | `PRP_RUGBY_TGT_01 (9000, 0.83, -7062.5)` |
| the same capture, beside it | Blood Bath, Capture | `070f0003010000013efd7d0307033efd7d030703fd00` | `(-706, 893, 775)` | `PRP_RUGBY_TGT_02 (-7062.5, -6.26, 7750)` |
| `mgo2-game1` | Tomb of Tubes, Bomb | `070f0003000000407cfc6c07acf47cfc6c07acf4fd00` | `(-900, 1900, -2900)` | `PRP_TGT_BOMB (-9000, 1000, -29000)` |
| `mgo2-game2` | Blood Bath, Sneaking Capture | `070f000300000050640014ec5700640014ec5700fd00` | `(100, -5100, 87)` | `PRP_SCAP_TGT_00 (1000, -6000, 875)` |
| `mgo2-game-failed-memoryviolation` | Blood Bath, Race | `070f0003000000603cf6940308073cf694030807fd00` / `…61 c409 8403 f8f8 …` | `(-2500, 916, 1800)` / `(2500, 900, -1800)` | `PRP_TRACE_TGT_A`, `PRP_TRACE_TGT_B` |

Every one of them is the same stage the capture is on, and the prop is the one that
rule's own proc places (§4). That is what rules out the earlier reading — a stage
banner would not be Capture's targets on one round and Bomb's site on another —
and it is what makes the rule the gate: one stage, six rules, six different sets
of points, and no set for the modes that place none.

## 3. The conversion

**x and z are the stage's world coordinate ÷ 10; y is the stage's y + 900**, both
truncated toward zero. The five measurements above all fit, and the division is
what makes a point fit in a signed sixteen at all: Jade Junction states
`z = -245 477`, which no sixteen-bit field holds. The lift is a constant of the
wire rather than of the stage: the four stages measured put their points at
`y = -6.26`, `1000`, `-6000` and `900`, and each record is that value plus 900, so
`MapCustomPoint` states the converted form and the table stores it.

## 4. Which rules place targets

The stage's `scenerio.gcx` dispatches on the rule from `proc 50`:

| rule | proc | rule | sets placed |
| --- | --- | --- | --- |
| 3 Capture | `proc 51` | Capture | `PRP_RUGBY_TGT_01`, `PRP_RUGBY_TGT_02` |
| 13 Interval | `proc 54` | Capture's pair | the same two props |
| 2 Rescue | `proc 52` | Rescue | `PRP_RES_TGT_01` |
| 7 Team Sneaking | `proc 53` | Team Sneaking | `PRP_TEAM_SNEAKING_TGT_01`, `…_02` |
| 6 Bomb | `proc 59` | Bomb | `PRP_TGT_BOMB` |
| 15 Sneaking Capture | `proc 61` | Sneaking Capture | `PRP_SCAP_TGT_00` |
| 16 Race | `proc 67` | Race | `PRP_TRACE_TGT_A`, `PRP_TRACE_TGT_B` |

**All 21 stage scripts have the same six placements, and the same dispatch to
them.** The proc numbers differ per stage (Capture is `proc 51` in `n001a` and
`proc 47` in `n007a`) but the rules that reach them do not: 2, 3, 6, 7, 13 and 15
route to the rescue, rugby, bomb, Team Sneaking, rugby and Sneaking Capture
placements, and 16 to the race pair. The table below names `n001a`'s procs as the
example.

The rest place none. Deathmatch, Team Deathmatch and Stealth Deathmatch have no
target; Base (`proc 60`), Sneaking (`proc 56`) and Stealth Deathmatch's variants
place *bases* instead, and a base is a character — `PRP_HOMEBASE_*` is a spawn
point, a kind-`0x04` record, not a custom point. So the set is empty for rules 0,
1, 4, 5, 8, 9, 10, 11, 12 and 14 — 12 places an SDM circle and 14 the Capture
goals (`PRP_RUGBY_GOAL_*`), neither of which is a point.

**A placement names its marker as `propM` and its points as `prop0`…`propn`.**
`proc 47` (Capture) passes `-propM PRP_RUGBY_TGT_00 -prop0 …TGT_01 -prop1 …TGT_02`,
which is why the round sends two records and not three: `TGT_00` is the marker
object, and on Blood Bath it sits at `(1000, -6000, 875)` — the same square
Sneaking Capture's target uses. `proc 48` (Rescue) is `-propM PRP_RES_TGT_00
-prop0 PRP_RES_TGT_01`, one point; `proc 49` (Team Sneaking) is the mini marker
plus `prop0`/`prop1`; `proc 57` (Sneaking Capture) and `proc 55` (Bomb) pass a
single `-prop`.

**The one other branch, `ComHostingSmallRoom`, swaps the placed object and not
the point.** Every placement that has a `MINI_*` twin picks it when the room is
small enough to be hosted by a player (`proc 43`), and the SCAP proc picks
`PRP_SCAP_TGT_00` there and `_01` otherwise. Checked across all 21 stages: the two
SCAP props are at the same coordinates everywhere, `PRP_MINI_TGT_BOMB` equals
`PRP_TGT_BOMB` except on Gronznyj Grad, and `PRP_MINI_TRACE_TGT_A/B` differ on
Gronznyj Grad by a great deal and on Eastern Europe and Perilous Palace by a few
hundred units. A
dedicated host is not a player-hosting client, so the table takes the full-room
props — the same choice the recorded bodies' stage/rule pairs admit.

**The task byte's high nibble is measured for four of the six.** Capture `0`,
Bomb `4`, Sneaking Capture `5`, Race `6`; the ladder those sit on puts Rescue on
`1` and Team Sneaking on `2`, and that is **[I]** — no capture holds a record for
either rule. What would settle it is a capture of either rule on any stage.

## 5. The table

`CustomPointTableUtils` holds all six sets for the 21 stages the rotation can name,
keyed by `MatchMapUtils`'s map id:

| map | stage file | map | stage file |
| --- | --- | --- | --- |
| 1 | `n001a` Forest Firefight | 12 | `n012a` Midtown |
| 2 | `n002a` Ambush Alley | 13 | `n013a` Winter Warehouse |
| 3 | `n003a` Urban Ultimatum | 14 | `n014a` Virtuous Vista |
| 4 | `n004a` Gronznyj Grad | 15 | `n015a` Outer Outlet |
| 5 | `n005a` Coppertown | 17 | `sm_dd` Desert Duel (`n021a.geom`) |
| 6 | `n006a` Hazard House | 18 | `sm_ll` Lethal Leviathan (`n019a.geom`) |
| 7 | `n007a` Blood Bath | 19 | `n024a` Perilous Palace |
| 8 | `n008a` Tomb of Tubes | 20 | `n022a` Quaint Quarrel |
| 9 | `n009a` Ravaged Riverfront | 21 | `n023a` Jade Junction |
| 10 | `n010a` Silo Sunset | 22 | `n020a` Eastern Europe |
| 11 | `n018a` Icebound Inferno | — | — |

The two stages whose directory name is not their geometry file — `sm_dd` ships
`n021a.geom`, `sm_ll` ships `n019a.geom` — are read from the file they ship. Map
16 has no row: the rotation's placeholder is not a stage, and a round on it gets no
points rather than another map's.

**To regenerate.** The props are the six names above resolved in each stage's
`.geom` through Haven's dictionary (`tools/stage-navigation/Stage/StageGeomFileUtils.cs`
reads the tree; the position is `GeoEffectLayout`'s), and the row is
`(trunc(x / 10), trunc(y + 900), trunc(z / 10))`. A stage whose props move is a row
a new capture verifies, not a value to hand-tune.

## 6. What the code does with it

* `CustomPointUtils` answers a round's points and its task byte from
  `(map, rule)`, and `[]` for a rule that places none. That empty list is the
  gate: `MatchStartRunService` passes it to `MatchStartRunFramesUtils.BuildRun`,
  which writes one kind-`0x03` record per point and none at all for a mode
  without targets. This is the fix for KEROTAN and GA-KO in Team Deathmatch.
* The record body is `CustomPointRecordBodiesUtils.Build(index, task, point)`,
  which replaces `RoomStateRecordBodiesUtils` and its recorded pair.
* The roster ordinal counts follow the point count, because they are one counter:
  a payload run is `4 + N + 3` values and a fresh briefing `4 + N + 1`. With two
  points those are the captured 9 and 7; with none they are 7 and 5.
* `BriefingCountdownService.PublishCustomPointsAsync` writes the **next** round's
  points — the round the briefing precedes — on the room's 2.35 s tick, and
  writes none when the rule places none.
* `UdpCommandConstants.PlayerProfile` is `RoomRecord`: the identifier is the
  exchange a record travels in, and the kind byte is its sub-command. A client's
  own roster entry is the one body that is a profile.

## 7. Open

* **Rescue's and Team Sneaking's task bytes** — `0x10` and `0x20` are inferred from
  the measured ladder, not read from a capture.
* **Whether the two triples are ever different.** Every measured body repeats the
  point, and the 29-byte form (two records exist) carries seven more bytes behind
  it; what those seven state is unread.
* **The small-room branch is read, not exercised.** The table takes the full-room
  props; no capture is known to be a small room, so the `MINI_*` twin of a point is
  never sent by this host.
* **Not verified live.** No client has been sent a rule-gated set yet; the
  confirmation is a Team Deathmatch round in which KEROTAN and GA-KO no longer
  appear, and a Capture round in which the objective is still drawn.
