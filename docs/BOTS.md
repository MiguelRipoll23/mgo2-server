# Bots

**What this is.** A server-side bot population for the gameplay host: configurable
sides, a centralized name sequence, a roster presence, and a decision stack that
reads the match as this host knows it. It is a foundation and not a playing
opponent — a bot perceives, decides and changes state, and sends nothing.

## What the evidence is, and what it is not

| fact | source |
| --- | --- |
| A dedicated host adds bots to its own room | `docs/GAMEPLAY_SERVER.md`, the ready-set reading: "a bot the host adds is ready in the record that adds it and never reports", over `mgo2-game-joined-when-red-team-then-briefing` |
| Bot roster entries are the short layout with `0x10` zero | `src/Shared/Utils/PlayerProfileRecordLayout.cs`: `0x00` on the 51 `BOT-01`…`BOT-11` entries and the ten name-less records |
| The recorded bots are named `BOT-01` … `BOT-11` | the same reading; this server writes the task's `BOT-1` shape, unpadded |
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
  BotIdentityService        BOT-<n>, monotonic; a released number is never reissued
  BotTeamAssignmentService  ENEMY/BOTH resolved against the sides the players hold
  BotRoleAssignmentService  round-robin archetypes, ordered by the round's rule
  RoomRosterService         RegisterBot: a member with the short layout, no exchange ordinal
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

* **Where a bot's position comes from.** Nothing on the wire states a bot's
  position, so a route starts from the round's own placement when the bot has none;
  the bot is not sent a position and its route is not sent to anyone.
* **Aiming, firing, reloading.** No builder for those operations records has been
  measured for a host, so `BotCombatService` resolves the weapon and the target
  and refuses to shoot with the reason. Writing records the client never sent is
  the invented mechanic the project forbids.
* **Positions and health in perception.** Not read on this path, so
  `BotPerceptionSnapshot.PositionsKnown` is false and `OwnHealth` is null.
* **Most weapon classes.** Four ids are classified; the rest are `Unknown` until
  the ELF's master table is read. Ammunition and range are unread for every id.
* **Mode asymmetry.** TSNE's attacker/defender side is not on the rotation's
  triple and no capture read here names it, so both behaviours are offered and
  neither is chosen by the bot's side.
* **Bot peer sessions.** A bot has no endpoint and receives no state records of
  its own; it exists in the roster, the sides and the ready set, which is what the
  recorded hosts' bots do and all this host writes.

## Tests

`tests/Mgo2Server.Tests/BotFoundationTests.cs`: the two settings' parsing and
validation, the name sequence and its no-reuse rule, side resolution for `ENEMY`
and `BOTH`, objective distribution, and role distribution. They are the pieces the
project's test rule keeps — data and identity — and not the decision scores, which
are design parameters rather than protocol facts.
