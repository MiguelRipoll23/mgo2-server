# Bots

**What this is.** A server-side bot population for the gameplay host: configurable
sides, a centralized name sequence, a roster presence, and a decision stack that
reads the match as this host knows it. It is a foundation and not a playing
opponent — a bot perceives, decides and changes state, and sends nothing.

## What the evidence is, and what it is not

| fact | source |
| --- | --- |
| A dedicated host adds bots to its own room | `docs/GAMEPLAY_SERVER.md`, the ready-set reading: "a bot the host adds is ready in the record that adds it and never reports", over `mgo2-game-joined-when-red-team-then-briefing` |
| A fill holds the **configured count**, keeps two seats of the room free, and a room is the size the instance is configured for | `game-with-bots-workingserver.pcapng` answers a join with a count of `09` players in a room that holds more than nine; an eight-seat match that filled to eight wrote `08` and its client refused to place itself (capture of 2026-11-10 20:38), because a room advertised at eight cannot hold a host, seven bots and the player. The Free Battle room is now `GameplayServerMaxPlayers` (17 by default, the figure the host's own settings row states) and the fill is `BOT_COUNT` bounded by the free seats — `MatchService.RoomCapacity`, `BotPopulationService.SeatsAFillKeepsFree`, `BotPopulationUtils.TargetCount` |
| A fill's bots ride the **join answer**, not a run behind it | `game-with-bots-workingserver.pcapng` t+0.482: the host answers a join with its own entry, `BOT-01`…`BOT-07` at ordinals 1–7, the player at 8, the close at 9 and the room's count at 10, and adds nothing after it. Seating on the player's report instead wrote a second run 8 s into the briefing, and the client of 2026-11-10 answered none of its records — `BotPopulationService.MakeRoomForPlayerAsync` seats the fill in the pass that answers the profile |
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
| `BOT_COUNT` | `0` | bots the room keeps, capacity permitting |
| `BOT_POPULATION_INTERVAL_SECONDS` | `5` | interval between population passes |

The stage navigation data is built, not configured: `tools/stage-navigation`
writes `src/GameplayServer/Bots/Stage/Data/stage-navigation.bin`, which the
server embeds (`stage-navigation.bin`). `scripts/run-gameplay-local.sh` turns the
room into a bot room — `BOT_TEAM=BOTH`, `BOT_POPULATION_MODE=FILL`, `BOT_COUNT=8`
— and needs no stage directory at all.

`BOT_COUNT` is the count the two documented settings do not state: a room with a
policy and a mode but no number holds no bots. Invalid values stop the process at
startup with a sentence naming the variable (`BotOptionsUtils.Validate`).

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
