# Where a match's rule, map, briefing time and kill limit live

**What this file answers.** A gameplay host needs the rule, the map and the
round's limits to open a round, and none of them is on the gameplay channel. They
are carried by the lobby's `0x4313` reply — the answer to `0x4312`, "get game
details" — and the gameplay channel only carries their *effect*, which is the
round's clock. This file names each field, gives the measured value from three
captures, and marks the two readings that are inference rather than measurement.

It revises one habit in the code: the operations record this server writes when a
round opens was named for the capture it came from (`RoundClockUnits` = 3 360,
one room's 7-minute TDM limit) as if it were a constant.It is a room's own setting for the round's rule — this instance's, for a dedicated
host — and the three captures below show three different values for it.

**Where the code reads each field.** A *player-hosted* room's fields are rows: the
client pushes its settings with `0x4312` and the lobby writes them into the room
row (`games`) and the hosting character's `characters_host_settings`. A dedicated
host — the gameplay server — receives no such push, so the settings it serves are
its own configuration and the room row is written *from* them rather than read
back. `MatchSettingsService` resolves this instance's options,
`MatchSettingsUtils` turns a rule name into a rule id and a map name into a map
id (`MatchRuleUtils`, `MatchMapUtils`), and `MatchService` writes that rotation
into `games.games` — which is the column the lobby's `0x4313` reply hands a joining
client, so the room a client sees and the round this host plays are one rotation
from one source.

| field | where it is read | setting |
| --- | --- | --- |
| game id | `games.id`, the row this host publishes | none: the row is the room |
| game name | this instance's own, and `server-<port>` when unset | `GAMEPLAY_SERVER_GAME_NAME` |
| rule | this instance's own, by the decoder's name | `GAMEPLAY_SERVER_RULE` — `TDM`, `TSNE`, … |
| map | this instance's own, by name or by stage; advanced one map per start | `GAMEPLAY_SERVER_MAP` — `Blood Bath`, `n007a` (the first start's map) |
| briefing time | this instance's own | `GAMEPLAY_SERVER_BRIEFING_MINUTES` |
| round length | this instance's own, for every rule | `GAMEPLAY_SERVER_ROUND_MINUTES` |
| the round's kill limit | this instance's own | `GAMEPLAY_SERVER_KILL_LIMIT`, unset for a clock-only round |

Both names are mapped through the replay console's own tables — `MODE_NAMES` and
`MAPS` of `docs/analysis/replay-console/rpdt.js` — rather than through a second
spelling of them, and a name neither table holds stops the process at startup
(`MatchSettingsUtils.Validate`): a room that runs a rule or a map no client was
told about is a room the client is not playing. Nothing on the gameplay channel
carries any of these fields, which is why the clock below is where their effect is
measured.

## 1. The `0x4313` reply

Layout from [`../PROTOCOL.md`](../PROTOCOL.md) §`0x4313` (the parser at
`0xD44388`, 372 bytes plus 28 per player), read against the decrypted TCP of the
three captures. Only the fields this file is about are repeated here.

| offset | size | type | meaning | measured |
| --- | --- | --- | --- | --- |
| `0x004` | 4 | u32 **BE** | game id | equals the id in the `0x4312` request and in the `0x4320` join |
| `0x008` | 16 | text | game name | `Best TDM [EU]`, `MAFIA TSNE`, … |
| `0x0a8` | 48 | u8×3 ×16 | **round rotation**: 16 triples of (rule, map, flags) | 15 rounds filled, the 16th zero |
| `0x0ea` | 1 | u8 | max players | 16, 17, 8, 12 |
| `0x0eb` | 1 | u8 | current player count | 1 in a room still filling |
| `0x0ec` | 4 | u32 **BE** | **briefing time** | 1 and 2 (minutes) |
| `0x108` | 4 | u32 **BE** | level-limit base | 22, the value `PROTOCOL.md` records |
| `0x10c` | 68 | u32 **BE** ×17 | **per-rule times and rounds** | see §2 |
| `0x160` | 4 | u32 **BE** | `PROTOCOL.md`'s regression guard | 46 = `0x2e`, as documented |

**The u32s are big-endian**, which `PROTOCOL.md` does not say and which is worth
stating: every one of the rows above reads as a sensible small number only in
big-endian — `0x0ec` is `00 00 00 01`, `0x108` is `00 00 00 16` (22), `0x160` is
`00 00 00 2e` (46) — and read little-endian they are `0x01000000` and so on. Both
`0x4312` and `0x4320` agree: the game id they carry is the big-endian reading of
the `0x4313` reply's own id. The rotation's three bytes are single bytes and
depend on no order.

## 2. The rule, the map and the clock

The rule and the map are one triple per round, `0x0a8 + 3 × round`:

```
Best TDM [EU]   round 0..5   rule 1 TDM   maps 5, 4, 3, 2, 10, 6    flags 0x02
MAFIA TSNE      round 0..2   rule 15 SCAP, 7 TSNE, 7 TSNE          maps 4, 10, 7
SNE LOBBY       round 0..14  rule 15 SCAP then 7×14 SNE            maps Blood Bath, Ambush Alley, …
```

Rule ids are the replay decoder's set (`MODE_NAMES` in `rpdt.js`): `0` DM, `1`
TDM, `2` RES, `3` CAP, `4` SNE, `5` BASE, `6` BOMB, `7` TSNE, `12` SDM, `13`
INTERVAL, `15` SCAP, `16` RACE. Map ids are its `MAPS` table: `1` `n001a` Forest
Firefight … `15` `n015a` Outer Outlet, `17` `sm_dd` Desert Duel, `19` Perilous
Palace, `22` `n020a` Eastern Europe. Both tables were checked against the game's
own stage names in the comments and room names the players wrote
(`MAFIA TSNE` plays TSNE, `SNE LOBBY` plays SNE), which is what makes them worth
reusing rather than re-deriving.

**The per-rule block**, `0x10c`, is seventeen big-endian u32s in the order
`PROTOCOL.md` gives: `SNE t, r | CAP t, r | RES t, r | TDM t, r, tickets | DM t,
tickets | BASE t, r | BOMB t, r | TSNE t, r`. **`t` is minutes and `tickets` is
the round's kill limit.** Measured:

| room | rule in play | its `t` | its `tickets` | the gameplay clock |
| --- | --- | --- | --- | --- |
| `Best TDM [EU]` | TDM | 8 | 55 | **3 840 units = 480 s = 8:00** |
| `vs bots` | TDM | 5 | 50 | **2 400 units = 300 s = 5:00** |
| `MAFIA TSNE` | TSNE | 4 | — | not played in the capture |

The two rooms whose round is captured agree with their own setting to the
second, and `MAFIA TSNE`'s comment says *"4 min rounds"* against its TSNE `t` of
4, which is a third, independent reading of the same field. The gameplay
counterpart is the operations clock: opcode `0x01`, a little-endian u16 of
125 ms units, first written when the round opens and counted down behind it (see
[`BRIEFING_FLOW.md`](BRIEFING_FLOW.md) §3). **480 units are one minute**, so

```
clock units = the round's rule time in minutes × 480
```

The bank's own order is what a *player-hosted* room's time comes from: the client
pushes its timers and the lobby reads the round's rule's word out of them. A
dedicated host has no push and so no bank to read, and its round's length is
`GAMEPLAY_SERVER_ROUND_MINUTES` for every rule; its kill limit is
`GAMEPLAY_SERVER_KILL_LIMIT`. Neither is ever zero, because zero is a round that
ends as it opens. The crosswalk between the two orders is still a fact about this
reply — the rotation names the rule (`0` DM, `1` TDM, `2` RES, `3` CAP, `4` SNE,
`5` BASE, `6` BOMB, `7` TSNE, `15` SCAP) and the bank states its time at that
rule's own index in the seventeen words above: SNE 0, CAP 2, RES 4, TDM 6, DM 9,
BASE 11, BOMB 13, TSNE 15, with SCAP reading the capture time it is played as —
and the reader that uses it is the lobby that receives the push.

**[U]** what the flags byte of a rotation triple means: `0x02` on every round of
`Best TDM [EU]`, `0x00` on most of `MAFIA TSNE`'s and `0x20` on its last two (the
two stages it repeats). No record on either channel repeats it.

**[U]** the u8 timer bank at `0x166` (SDM, INT, DM, SCAP, RACE), the only place
the rarer rules' times appear. Its SCAP value reads `0x63` = 99, which no unit
makes a round length, so the bank is not read here.

## 3. Which limits a round ends on — and why a relay cannot end one

`tickets` is the round's kill limit, and a round ends on whichever of the two
limits comes first: the clock (`0x01` reaching zero) or a team reaching the
tickets. `vs bots` played three rounds of a 50-ticket, 5-minute TDM and all three
ended on the clock — the note's *"40 kills to win"* does not appear on the wire
for that room (its `tickets` reads 50); the note is the player's recollection and
the field is the measured value.

**Nothing on the gameplay channel counts kills for a host.** Every scoreboard
record in the captures travels host → client, and so does the kill feed
(`0x254` opcode `0x09`, two bytes). The clients send their own position and
state (`0x8077`/`0x8877`) and nothing that says "I killed". A host that only
relays the records cannot know a round's winner, which is the finding
`BRIEFING_FLOW.md` §6 leaves open.

**What this server does with it.** It keeps its own count and publishes it
(`MatchScoreService`, `docs/GAMEPLAY_SERVER.md` §3.1): a death when a player's
own vitals report health `0`, a kill and its death when a kill feed record
arrives (`OperationsStreamHandler`), and a side's total from the opposing side's deaths
until a feed names one (`MatchScoreService.TeamLimits`). A round ends on a side reaching the kill limit
this host is configured with, and a room that states none plays its clock out.

## 4. The scoreboard, if a host has a score to publish

The per-player scoreboard rides the roster identifier as payload type `0x0b`:

```
[0x0b, lobby slot + 1, 21-byte bit mask (LSB first), one value per set bit]
```

with each value a little-endian u16 except bit 1 and bits 128..152, which are one
byte — `rpdt.js`'s rule, and it holds on 10 135 of 10 301 replay records. Decoded
from the live captures it moves exactly as a scoreboard should: in
`joined-when-red-team`, slot 1's `kills` (bit 5) steps 1, 2, 3, 4, 5 while the
players it kills (`0x1001 0x0b` on slots 6, 7, 8) take `deaths` (bit 6), and its
`score` (bit 8) climbs 3, 7, 13, 20, 28. Named bits: **5** kills, **6** deaths,
**8** score, **11** headshots, **152** last-result code, **153** streak.

A team's total is the sum over the slots the lobby-slot arrays assign to it: the
`0x85`/`0x86` pair (`0x1001`, 26 bytes, `TeamArrayFramesUtils`) says which slot
holds which team and which slots are occupied. That pair is the only place on the
gameplay channel a team is named; the round's *result* is `state[2]` of the
operations state block, coded `2` = raw team 0, `3` = raw team 2.

The pair is written from the room's own rows — `game_players.team` for the side,
the roster's session slot for the slot (`LobbySlotService`) — and it names the
plain raw values the captures write: `0` and `1` for a player whose side is known,
and `0xe0` for one the room puts on no side. What a *scoreboard* is summed
against is therefore in place, and the counting is this host's own:
`MatchScoreboardFramesUtils` builds the body, `MatchScoreService` counts, and
`ScoreboardPublishService` writes each delta to the peer it came from and to the
room (`GAMEPLAY_SERVER.md` §3.1; what is left of the score is its §5 item 4).

## 5. Reading this from a capture

```bash
python3 tools/pcap_decrypt.py CAPTURE.pcapng --type tcp --json > tcp.json
# 0x4313 replies: 372-byte settings block + 28-byte player entries
python3 tools/pcap_decrypt.py CAPTURE.pcapng --type udp   # the gameplay channel
python3 tools/udp_briefing_flow.py CAPTURE.pcapng         # briefing, reports, round open
```

`pcap_decrypt.py --json` emits the frame's `cmd`, `rel`, direction and full
payload as hex, which is what the field offsets above are read against.

## 6. Open

* **[U]** the rotation's flags byte (§2).
* **[U]** the u8 timer bank at `0x166`, and therefore the round length of SDM,
  INTERVAL, SCAP and RACE rooms as the *client* states it. A dedicated host no longer
  needs it (its round length is its own setting), and a player-hosted room's use of
  it is the lobby's.
* **[U]** whether the first round a client plays is always the rotation's round
  0 — no capture joins a room early enough to see a rotation advance.
* **[I]** that a rule's `tickets` ends a round on its own; no capture holds a
  round that ended on kills.
