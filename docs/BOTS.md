# Bots

**What this is.** A server-side bot population for the gameplay host: configurable
sides, a centralized name sequence, a roster presence, and a decision stack that
reads the match as this host knows it. It is a foundation and not a playing
opponent — a bot perceives, decides and changes state, and sends nothing.

## What the evidence is, and what it is not

| fact | source |
| --- | --- |
| A dedicated host adds bots to its own room | `docs/GAMEPLAY_SERVER.md`, the ready-set reading: "a bot the host adds is ready in the record that adds it and never reports", over `mgo2-game-joined-when-red-team-then-briefing` |
| A fill holds every seat the players are not in unless `BOT_COUNT` names a count, gives a bot up at a join, and a room is the size the instance is configured for | `game-with-bots-workingserver.pcapng` answers a join with a count of `09` players in a room that holds more than nine; an eight-seat match that filled to eight wrote `08` and its client refused to place itself (capture of 2026-11-10 20:38), because a room advertised at eight cannot hold a host, seven bots and the player. The Free Battle room is `GameplayServerMaxPlayers` (16 by default), and that figure is the room's whole table: **the host holds a slot it never plays**, a seat the client never draws, so a room of sixteen shows the player fifteen slots. The fill is the table less the host's seat and less the players in it — fourteen bots beside one player — which the client admitted on 2026-11-11 13:52; a fill one bot larger put sixteen joiners in the room and the client called it full and went quiet (13:43), and the deployed room of 13:07 answered "game is full" the same way at seventeen slots — `MatchService.RoomCapacity`, `BotPopulationService.Capacity`, `BotPopulationUtils.TargetCount` |
| A fill's bots ride the **join answer**, not a run behind it | `game-with-bots-workingserver.pcapng` t+0.482: the host answers a join with its own entry, `BOT-01`…`BOT-07` at ordinals 1–7, the player at 8, the close at 9 and the room's count at 10, and adds nothing after it. Seating on the player's report instead wrote a second run 8 s into the briefing, and the client of 2026-11-10 answered none of its records — `BotPopulationService.MakeRoomForPlayerAsync` seats the fill in the pass that answers the profile |
| A death is announced by the **operations stream's kill feed**: `09 <killer> <victim>` under `0x1a54` | `game-with-bots-workingserver` writes `09 02 08` at t+311.03 against slot 8's health reaching `0` at t+310.81, `09 07 05` at t+313.75 against slot 5's at t+313.71, `09 06 01` at t+317.38 against slot 1's at t+317.34 — `MatchKillFeedBodiesUtils`; the room of 2026-11-10 21:00 wrote `02 <killer> <victim> 02` under `0x1a58` instead and the client announced **"Roger that"**, because that body is the **chat macro command** and not a kill feed |
| A dead actor's position record is **32 bytes and opens `0x22`**, with the three coordinates as 32-bit floats | `game-with-bots-workingserver` writes 15 810 of them over its two rounds, every one `22 1a 00 0b` then a counter and the floats — t+40.850 carries `24349.0`, `671.0`, `-6150.0` — where a living actor's record is 16 bytes and opens `0x21` or `0x41`; the room of 2026-11-11 wrote the living body for a corpse and the client drew it standing — `BotActorRecordUtils.BuildCorpseBody` |
| A corpse **takes no damage**: neither a bot's shot nor a client's own hit on it is relayed | `game-with-bots-workingserver` holds seven of the client's own hits on a body whose vitals already read `0` (t+41.25 slot 6, t+52.02/52.49/52.75 slot 3, t+83.35/83.59 slot 3, t+106.59 slot 2) and **not one is echoed**; `bots1.pcapng` relayed every hit after slot 1 fell at t+37.51, and a body that keeps taking damage is what the client drew — `BotActorPublishService.NoteDamageEcho` drops a hit whose victim is down, and a bot does not take a corpse as a target. **A blast is the one hit still written back to a corpse**, because it carries the victim's animation rather than a relay of damage (next row) |
| **A grenade's blast is answered with its own record, a fifth of a second behind it, and that answer is the animation the victim's client plays** | `.pcapngs/actions/actions.pcapng`: the client's 28-byte `13` record at t+160.68 names its own slot and the host writes the same record back at t+160.86; its t+167.38 blast lands on a victim the first one had already held at zero since 160.68 and is answered at t+167.57 all the same; the third blast, on slot 2 at t+376.77, is the capture's own client's and gets no answer because no other peer was near it. No other 28-byte class-`0x00` record exists in the dump. This host echoed a bullet on the next beat and dropped a blast outright, so the damage arrived and nothing played — `BotActorPublishService.NoteDamageEcho`, `BlastEchoDelayMilliseconds` |
| A death **spends a ticket**, and the side's remaining count is its own record | `game-with-bots-workingserver` answers every death with an eleven-byte `0x1001` body `0b 00 00 00 08 …` (raw team `0`) or `0b 00 00 00 40 …` (team `1`) whose count steps down one per death of that side: `19` at t+40.85, `18` at t+45.61, `17` at t+52.01, `16` at t+53.75 for team `0`, and `19` at t+55.26, `18` at t+80.75, `17` at t+87.88 for team `1`, against the twenty-ticket allotment its room body states at `0x5a`/`0x5e`. The room of 2026-11-11 wrote none, so both sides stayed at forty for the whole round — `TeamTicketBodiesUtils`, published by `ScoreboardPublishService` |
| **Corrected on `working-server-v2`: subtype `0x12` is a headshot, not "the blow that kills" — twelve of twelve credited headshots end on it, and none of the thirty-two other kills does; they end on an ordinary `0x11` of about 56 points (`BotShotUtils`, `MatchScoreService.Kills.cs`).** The headshot blow is its own damage record: subtype `0x12`, and its damage word is a whole health bar's blow — `7d 02` (250.02) on a victim whose vitals record read `68` (104) one beat earlier, so the client's bar reaches zero whatever it held | `game-with-bots-workingserver` writes nine `0x12` hits against 341 `0x11`, each within a fifth of a second of the feed naming the same victim — t+40.83 slot 6 `7d 08` (250.1), t+45.59 slot 7 `07 7d` (125.1), t+89.01 slot 6, t+310.81 slot 8 — where an ordinary hit is 33.8 to 68.8 points; `bots1.pcapng` wrote every death as an ordinary 50-point `0x11` hit and **no `0x12` at all** — `BotActorRecordUtils.FatalDamageSubtype`, `BotActorRecordUtils.BuildShot`. The feed also follows the blow rather than riding it: the working host's lethal hit on slot 8 lands at t+310.81 and `09 02 08` follows at t+311.03, while `bots1.pcapng` (2026-11-11 13:54) wrote slot 15's lethal hit, its vitals zero and `09 04 15` in one beat — the client acknowledged that beat at 44.7526 s and never sent another frame, its kill-log writer reading a victim whose attacker it had not filed (crash at `system.dll+0x224D7B`). The feed now waits `BotActorPublishService.FeedDelayMilliseconds` behind the death. Its victim keeps naming its killer up to the blow: slot 5 writes `44 06 01 …` on six beats and stands with `00 ff 00` only after the fatal hit, so the state record the client reads a death from names both slots of each other — `BotDeathUtils` |
| A death is counted by the **host's own kill record**, for a player as well as a bot | the run of 2026-11-11 13:01 logs `Player in slot 4 killed the player in slot 8; 1 kill(s), 0 death(s)`: the victim's death was left to the client's own vitals report, which cannot count it, because the host had already put that player at zero when its hit landed and the report of zero then reads as "still dead" (`PlayerVitalsHandler`). No victim delta and no ticket went out, and team 1 stayed at `40` for the whole round, where the working capture answers a player's death with both — `0b 00 00 00 40 … 13 00` and the victim's delta at t+55.26 — `BotActorPublishService.Kills` |
| **A client's damage record says what the blow is by its subtype, and only one of them spends health per point: `11` a body hit, `12` a headshot, `42` a stun, `91` a knife, `13` a blast** | `.pcapngs/actions/actions.pcapng`, the working host's dump of one round, over every class-`0x00` record its client wrote. `11 01 f0 1b 18 …` is 55.9 points of a 250 bar and five or six of them put an actor down; `12 01 10 7d 18 …` is the whole bar; **`42 01 10 7d 02` is the Mk.2's stun — the same 250.1 in its damage word and no health spent at all**, so a host that read that word as damage killed the actor it had been told was knocked out and announced a kill for a stun; `91 00 10 7d 01` is the ST KNIFE's whole-bar blow; and `13 00 d0 5d 34 …` is a **28-byte blast**, weapon `34` (52, the grenade), 187.6 of damage, its own point as three `f32` at `+14`, and **no shooter anywhere in the body** — the peer that reported it is the thrower, which is what makes friendly fire and a grenade suicide readable — `BotHitUtils` |
| **A hit is applied whichever side its victim stands on, and is credited to the slot that reported it — never to slot zero** | the same capture carries no "teammate" record: the client's hits name slots 1–15 and the only thing that says whether a victim was a friend is the side the room puts it on. Its feed names killers 1–16 (`09 08 07` at t+49.71, `09 0e 0a` at t+72.21) and never slot zero. This host dropped every hit whose reporting endpoint its roster had no row for — so an ordinary shot took no health and only the whole-bar headshot word could put a body down — and announced a death it could name nobody for with killer byte `00`, which the room draws on `server-99999`, the dedicated host's own character — `BotSimulationService.Damage`, `BotActorPublishService.Players`, `MatchScoreService.Kills`. **The killer byte is a lobby slot and nothing else**: the replay console reads byte +1 of the `09` record and resolves it through the roster's slot-to-player map built from the profile entries (`.gametools/replay-console/rpdt.js`, `slotPlayer`/`slot2idx`), so a byte no entry names resolves to no player and the kill is drawn on the host's row — a feed is therefore written only for a slot the room seats (`BotActorPublishService.Kills`) |
| **A CQC grab belongs to the grabber's client; the host's part is to stop writing the held actor as one that aims, fires or walks** | `.pcapngs/actions/actions.pcapng` writes `59 0f 00 33 01 61 00 c6 07` under its own state identifier from t+392.19 for about two seconds: the held actor's slot is at `+1` and the action is in the first byte's low six bits (`0x57`/`0x58`/`0x59` — 23/24/25 with the `0x40` acting bit, the same family the same client writes around t+157 and t+313), where the standing form puts `0xff` there. The host answers nothing, and its own records for the reference's actors stand. Our host kept simulating the grabbed bot — aiming at the player holding it, firing and walking out of the hold — so the grab never settled and the client refused it — `CqcHoldUtils`, `BotSimulationService.HoldSlot`; `HeldMilliseconds` is the one inferred number, the beat gap the hold has to survive |
| A hit that does not kill writes the **`c1 81` position head** for six or seven beats, about 200 ms | slot 6 at t+258.745–258.910 and every bot of the room at t+289.012–289.144, in `game-with-bots-workingserver`; `0xc1` is `0x41` with the `0x80` bit — `BotLocomotionService.NoteHit` |
| A bot **holding a target writes `0x44` on most beats and `0x47` on the shot's**, and the `41 81` head while engaged | `game-with-bots-workingserver`: slot 2 writes 292 `0x44`, 138 `0x47`, 70 `0x46` and 20 `0x48` over its round, and every one of its engaged position records is `41 81`; this host wrote `0x44` on every engaging beat and never a `0x47`, which is a bot that aims and never appears to shoot — `BotActorRecordUtils.BuildStateBody` |
| A bot fires a **burst of three rounds a fifth of a second apart**, and the pause behind it is three of the recorded five seconds a hit | `game-with-bots-workingserver` writes hits on one victim in runs of up to seven, 0.19–0.25 s apart — slot 2 takes eight from t+81.69 to t+83.03 and its 303 gaps have a tenth percentile of 0.232 s — where `bots1.pcapng` writes one hit per victim every five seconds, its longest run two, and never two inside 300 ms; the rate is kept, so a burst of three is followed by fifteen seconds — `BotBurstUtils` |
| Bots **patrol the whole map** between engagements, each slot on its own track | `game-with-bots-workingserver` slot 1 runs `x 20750..25160` and `z -23750..-14960` in 21.5 s in bursts of about 200 units, and every slot's range is its own; the room of 2026-11-11 stood on its spawns — `BotSimulationService.Patrol`, `BotMovementDestinationService.ChoosePatrol` |
| The beat's vitals record rides the peer's **own actor identifier** in this room, so `fa fa` written there every 33 ms overwrites every hit | `bots1.pcapng` slot 9 falls `250 → 217 → 184 → 151 → 118` in 30 ms and is back at `250` 30 ms after — `MatchRecordService.HeldVitalsBody`, `PlayerVitalsService.NoteDamage` |
| A bot **holds its enemy at about 1 400 units and holds its trigger from up to 6 000** | `game-with-bots-workingserver` writes the firing byte on 70% of a bot's acting beats, at 282–6 043 units from the enemy it holds (median 1 414), and keeps that enemy at 1 233–1 525 (median 1 414); the room of 2026-11-10 21:00 fired once or twice a minute from 336 units and stood at 200 — `BotSimulationService.ShotRange`/`EngagementDistance` |
| A bot's **height is the recorded stance and never a navigation cell's** | `bots-out-of-bounds.pcapng` (2026-11-10 20:49) writes every bot at `y = -1000` after its first step, 1800 under the spawn's `800`, and back to `800` at each respawn; `game-with-bots-workingserver.pcapng` keeps its bots at `980`–`1320`. The extracted cells are a lattice over the stage's box (`tools/stage-navigation`), so their `Y` is not a floor — `BotLocomotionService.Step` keeps the body's own height and a route moves a body across the stage only |
| A bot **walks at the working host's rate**: 150 stage units on each 33 ms beat | `game-with-bots-workingserver.pcapng` t+262.2–262.9 moves a bot fifteen wire units a beat without a gap, four and a half thousand stage units a second; `bots-out-of-bounds.pcapng` moved ten stage units a beat, a tenth of it, which reads as wading — `BotLocomotionService.WalkUnitsPerSecond` |
| A roster entry names its player by the room's **table slot**, at `0x04`, `0x05`, `0x07` and in the bot's `0x7f` character id | `game-with-bots-workingserver.pcapng`: `BOT-01`…`BOT-07` carry `01`…`07` at all four and the player `08`; `pcapng1`/`pcapng2` write the slot at `0x05`/`0x07` and a per-player id of their own at `0x04`; the host of 2026-11-10 wrote `0xe2`+roster index there and answered the bots as a second run — `RosterMember.BuildRecord` passes `SessionSlot` at all four |
| Bot roster entries are the short layout with `0x10` zero | `src/Shared/Utils/PlayerProfileRecordLayout.cs`: `0x00` on the 51 `BOT-01`…`BOT-11` entries and the ten name-less records |
| The recorded bots are named `BOT-01` … `BOT-11` | the same reading; this server writes the same shape, zero-padded to two digits |
| The recorded hosts add a bot to a live room as **one entry on the room's exchange counter**, then the close, then the room's count, then that bot's own loadout (`pcapng1` t+393.858: `BOT-11` entry ordinal 81, close 82, count 83, loadout 84; t+488.724: ten entries 21–30, close 31, count 32, loadouts 33–42) | `BotRosterPublishService`; a whole run restarting at zero is a run the client has already read and drops |
| A bot's look is a real character's appearance | `BotAppearanceUtils`: ten 23-byte blocks, the trailing block of `pcapng1`'s own bot loadouts at t+488.7, gender in field 12 |
| A bot's entry names it in the recorded `0x7f` slot family (`0x7F000000` or its slot), while its **row** supplies the look | `RosterMember.BuildRecord`; every captured bot entry writes `slot 00 00 7f` at `0x08` (`pcapng1` t+0.562). A real row id there is one the client has no model for — the run of 2026-11-09 13:08 announced rows and failed `netplayer/parts/pl_net_parts.cc:936: skel not found` with no bot in the map — so the row is read for the appearance only (`BotCharacterService`, `LobbyLoadoutService`) |
| A player in the match is an **actor block** on the tick channel: state at the slot's base, vitals at `+1`, position at `+2`, the weapon pair and byte at `+4`, aim at `+6`, and `0x0a` between one slot's base and the next | `game-with-bots-workingserver.pcapng`: the host writes actor records on the 33 ms match beat; slot 1's state is `0x084d` in the first generation and `0x004d` in its sibling |
| A bot that only holds a roster entry is on the briefing screen and not in the map | `BotActorPublishService` / `BotActorRecordUtils`; the reference's position bodies carry the map's spawn as a live client writes it — `x` at `+6`, stand height at `+8`, `z` at `+10`, each a tenth of a stage unit, and the heading at `+12` (`MapSpawnUtils`) — under the alive head `21 00 00 00`, and `fa fa` vitals |
| A bot's roster entries and loadouts are written **when it is seated**, never again at a round's start | `pcapng1` t+485.7 does append them where its own fill seats its bots; this host writes that append on the report that seats them, and repeating it at the round's start froze the client of 2026-11-09 12:48 — no frame for 15 s and no report, with the append the last thing it was sent |
| A bot is named in the round's **player list**, one two-byte record per player, before its blocks | `pcapng1` t+491.12, half a second before the run: ten records, ordinals 0–9, bodies `0007`, `0014`, `0012`, `000d`, `000b`, `0013`, `0006`, `0005`, `000f`, `0010` — every occupied key behind a zero byte, under the item-box stream with the `0x08` bit (`0x1a58`/`0x1258`); **not written here**: `game-with-bots-workingserver` writes no `0x1a58` at all in the four seconds around its round start, and its 29 records in ten minutes are `02 08 00 02` or `00 0f` — the round-start list this row measured on `pcapng1` is not the working host's shape, and a host that wrote it removed every player from the client's screen |
| A bot is given the round's own settings state, one record per bot, before its actor blocks | `pcapng1` t+485.74 writes sixteen `0b <key+1> 60 …` bodies, one per occupied key; the three whose keys are `BOT` entries (`01`, `03`, `07`) hold the same 34-byte body and differ only in the slot byte, so `BotRoundStateBodiesUtils.Build` writes it with the bot's key plus one — what the fields mean is not claimed |
| Every player of the room gets a **second settings body** behind its loadout — the one that carries its gear — and this host wrote none of them | `pcapng1` t+3.63: for each key both a fifty-byte `0b SS 02 …` body and a sixty-one- to ninety-one-byte `0b SS 62/42/60 … 50 02 <gear> …` body; the three keys that carry both shapes (`0x08`, `0x0e`, `0x13`) put the appearance block in the last twenty-three bytes and end the gear region on the `ff` before it, and nothing captured names the bytes between. `PlayerGearBodiesUtils.Build` writes the slot and the block into the recorded seventy-byte template; the gear list is the template's, since no capture maps saved gear onto it. The same body carries two stats blocks at `0x18` and `0x22`, five little-endian `u16` each, and the briefing table's three columns read kills, deaths and points from them: copied from the template they printed `009 010 0004` on **every** row of the 2026-11-09 13:25 screen, the template player's own `8`/`10`/`4` with the first column one above the byte it comes from (unexplained), so the builder clears both blocks and writes the caller's figures into them, every call starting a player from zero (`PlayerGearBodiesUtils.Build(…, PlayerStats)`); the LEVEL column is the same body's byte twenty-four from its end, written as half the character's level from `LevelUtils` — the record carries `0x07` and the client showed 14, and the host's fifty-byte body carries `0x05` where it showed 10 |
| A bot's stream starts after the peer's **opening run for that round**, never at the round's open alone | `pcapng1` writes the bot append six seconds before its run at t+491.58; the live run of 2026-11-09 had the round open at 12:45:21 and the run at 12:45:31, and the blocks written in between left the client on a black screen — `MatchRoundStateService.HasRunWritten` is the gate |
| The map's players come from the room's **entry records**, one per player, written when a client asks to be placed in a round | `pcapng1` t+10.19: fourteen 25-byte `07 12 00 04` entries with the close behind them, indices 0–13, one per slot the arrays mark; `TeamArrayService.EnterRoomAsync` writes one per member — the bot included, since no client asks for a bot's own |
| The game has its own bot AI, by mode and weapon | `docs/strings/codepaths.txt`: `pl_bot_loc_tdm_{ar,sm,sr,sg,rpg,mg,gr}.cc`, `pl_bot_mis_sne_{patrol,watch}.cc`, `pl_bot_mis_{def,mat,sup}.cc` and a `think_layer` per behaviour. It is the **port's**, and it names weapon classes and mission families rather than the five archetypes here |
| Weapon ids, and some classes | `docs/WEAPONS.md`: 141 rows at `0x1035834`, stride `0x2c`; the per-id category bitmask is in the table, **which is not in this repository** |
| The five roles | the task's design, not the game's — the port's families are evidence for roles existing, not for these five |

## Settings

| variable | default | values |
| --- | --- | --- |
| `BOT_TEAM` | `ENEMY` | `NONE`, `BOTH`, `ENEMY` |
| `BOT_POPULATION_MODE` | `FILL` | `FILL`, `PERMANENT` |
| `BOT_COUNT` | `0` | bots the room keeps, capacity permitting; under `FILL` the size of the fill in players, so a joining player takes a bot's seat |
| `BOT_POPULATION_INTERVAL_SECONDS` | `5` | interval between population passes |

The stage navigation data is built, not configured: `tools/stage-navigation`
writes `src/GameplayServer/Bots/Stage/Data/stage-navigation.bin`, which the
server embeds (`stage-navigation.bin`). `scripts/run-gameplay-local.sh` turns the
room into a bot room — `BOT_TEAM=BOTH`, `BOT_POPULATION_MODE=FILL`, and no
`BOT_COUNT`, so the fill is the room's own size — and needs no stage directory at
all.

`BOT_COUNT` is the count the two documented settings do not state: a room with a
policy and a mode but no number fills every seat its players are not in — a
`FILL` of `0` is the room's own size, which is the default, so a number is stated
only when the operator wants a smaller fill than the room.
A `FILL` of a number is that many players: one bot leaves for each human who
joins, before the roster and the room's count are written to that human, so an
`BOT_COUNT=8` room is eight players and not eight bots beside a player. Invalid
values stop the process at startup with a sentence naming the variable
(`BotOptionsUtils.Validate`).

`NONE` wins over the mode. `ENEMY` is resolved per pass from the sides the room's
human rows hold — no side number is written anywhere in the bot system, and the
two sides themselves come from `RoomSideUtils`, which reads the range
`LobbySlotService` already enforces. A room whose humans hold no side has no side
to oppose, and no bot is placed.

## The pieces

```
BotPopulationService        reconciles the room: count, sides, roster, ready state, ticks
  BotIdentityService        BOT-<nn>, monotonic; a released number is never reissued
  BotTeamAssignmentService  ENEMY/BOTH resolved against the sides the players hold
  BotRoleAssignmentService  round-robin archetypes, ordered by the round's rule
  BotCharacterService       get-or-create the character a bot plays as, and its captured appearance
  RoomRosterService         RegisterBot: a member with the short layout and that character, no exchange ordinal
  BotRosterPublishService   the mid-briefing announcement: entry, close, count, the bot's own loadout
  BotActorPublishService    the round's writer: every bot's actor block, every 33 ms
  BriefingStateService      NoteBriefingReady under the bot's roster key

BotController               one tick for one bot
  BotPerceptionService      roster + lobby-slot sides; no positions, no health
  BotObjectiveService       rule → mode objective (TDM, RES, CAP, SNE, BASE, TSNE)
  BotDecisionService        highest utility, ties by action order
  BotStateMachineUtils      action → body state
  BotCombatService          weapon profile + target, and why it does not fire
  BotNavigationAgent        route request, or the provider's refusal
```

Roles are weights over the same actions (`BotRoleCatalogUtils`), so a role changes
what a bot leans towards and never becomes a second implementation.

## What is not implemented, and why

* **Movement.** A route runs over the stage the map is played on, and the stage's
  files are **not** a runtime dependency. `tools/stage-navigation` reads each
  stage's GEOM once (Haven's layout, `Formats/Geo`), resolves its `PRP_*` names
  through Haven's `dictionary.txt`, and writes one embedded file holding the box
  each stage states, its placements by mode family, and the centre of each of its
  own spatial cells. `StageBotNavigationProvider` routes with A* over those cells
  and `BotMovementDestinationService` takes a spawn placement of the round's
  family, so a host needs a build and nothing else — no install, no mounted
  directory.

  The route's height is coarse and that is stated rather than hidden: a cell is a
  box, so a route runs over space the stage occupies rather than along a floor
  anything sampled. A map the build carries no data for is `Unavailable` with the
  stage named, and never a straight line dressed as a route.

* **Where a bot's position comes from.** It is simulated. `BotLocomotionService`
  owns the body, starts it at the side's spawn and walks it along a route at the
  working-server capture's pace (fifteen tenths of a stage unit per 33 ms beat),
  and the beat writes that pose. A player's position is read back from the
  client's own record (`PlayerPositionService`), which is what a bot walks
  towards.
* **Aiming, firing, reloading.** Aiming and firing are on the wire now, measured
  from `.pcapngs/game-with-bots-workingserver.pcapng`: the nine-byte state record
  (`0x40 | action`, the target slot, a one when firing, the aim point),
  the six-byte aim point, and the fifteen-byte damage record three below the
  victim's state identifier. `BotSimulationService` holds one enemy per bot and
  lands a hit about every twelve seconds within 336 stage units — the working
  host's 362 hits over its 640 s round, none beyond 336 units, about five a
  minute on a given victim — and applies a client's damage record to the bot it
  names (`BotSimulationService.NoteIncomingDamage`) so a bot's vitals fall and it
  goes down. The actor block carries the weapon pair `00 18 00 00 0f` the capture
  writes for a live bot. Reloading has no measured record and is not written.
* **Positions and health in perception.** Not read on this path, so
  `BotPerceptionSnapshot.PositionsKnown` is false and `OwnHealth` is null.
* **Most weapon classes.** Four ids are classified; the rest are `Unknown` until
  the ELF's master table is read. Ammunition and range are unread for every id.
* **How long a stun lasts.** The stun's record says what it costs and not how long
  it holds: the capture's `42` spends the whole bar in its damage word, the victim's
  vitals never move, and no record carries "up".
  `BotSimulationService.StunMilliseconds` is three seconds, and it is the one number
  in this path a capture here does not state.
* **Mode asymmetry.** TSNE's attacker/defender side is not on the rotation's
  triple and no capture read here names it, so both behaviours are offered and
  neither is chosen by the bot's side.
* **A bot's body is published every beat.** The block carries the simulated
  pose: the walking head `41 81 00 00` or the standing `21 00 00 00`, the three
  coordinates at a tenth of a stage unit, the heading, the aim point, and the
  vitals the simulation holds — `fa fa` alive, health falling by each hit, zero on
  a death, and whole again eight seconds later. A hit the bot lands on a player is
  written as that player's damage record and vitals, which is the pair the
  working-server host writes.
* **Bot peer sessions.** A bot has no endpoint and receives no state records of
  its own; it exists in the roster, the sides, the ready set and — once it is
  seated on the briefing report, where the client that was answered the roster is
  already listening — its own loadout record, which is what the recorded hosts'
  bots do and all this host writes.

## Tests

`tests/Mgo2Server.Tests/BotFoundationTests.cs`: the two settings' parsing and
validation, the name sequence and its no-reuse rule, side resolution for `ENEMY`
and `BOTH`, objective distribution, and role distribution. They are the pieces the
project's test rule keeps — data and identity — and not the decision scores, which
are design parameters rather than protocol facts.
