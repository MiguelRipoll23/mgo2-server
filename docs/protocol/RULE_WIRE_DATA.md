# What changes per rule on the MGO2 wire

**What this file answers.** A rule is chosen in the room's rotation, but the gameplay
channel has no per-rule packet family. The same identifiers carry the round: the
room's rotation and timer bank, the operations state block, the scoreboard, the kill
feed, the team arrays. A rule changes *which fields are meaningful*, *which optional
records the host emits*, and *how values are computed* — not the transport.

This file lists those differences rule by rule, with the ELF entry points, the
measured captures, and the current server's behavior. It is deliberately explicit
about what is measured **[V]**, inferred **[I]**, and still unknown **[U]**.

**Evidence used.**

- `MGO2.ELF`, the 1.36 image in the Ghidra project (sections to about `0x129c200`).
  Function names below are the project's descriptive names; entry points are the
  image's.
- `system.dll`, the PC port at `0x10000000`, for the parts the PS3 image does not
  expose symbolically (the score rows and the kill notice).
- The captures under `.pcapngs/`, especially `mgo2-started-on-game-round1`,
  `briefing-then-solo-game`, `mgo2-game1`, `mgo2-game2`,
  `rounded-ended-by-reaching-tickets-limit`, and `working-server-4-sessions`.
- The server's rule code: `MatchRuleUtils`, `MatchRotationUtils`, `MatchSettings`,
  `MatchStartRunService`, `MatchStateBodiesUtils`, `MatchScoreRulesUtils`,
  `MatchScoreboardFramesUtils`, `TeamArrayFramesUtils`,
  `RulesMemberListBodiesUtils`, and `BeatFramePlan`.

The longer recaps are in
[`../analysis/rule-instruction-texts.md`](../analysis/rule-instruction-texts.md),
[`../analysis/gameplay-data-functions.md`](../analysis/gameplay-data-functions.md),
[`../analysis/rotation-payload-corpus.md`](../analysis/rotation-payload-corpus.md),
[`../analysis/supervisor-opcode-builders.md`](../analysis/supervisor-opcode-builders.md),
[`STATE_VECTOR.md`](STATE_VECTOR.md), [`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md),
and [`MATCH_FLOW.md`](MATCH_FLOW.md).

## 1. The rule set

The lobby and the gameplay host use the replay decoder's ids. `MatchRuleUtils` is
the server's port of that table:

| id | name | sides | item boxes (host writes) | ticket limit in the bank |
| ---: | --- | --- | --- | --- |
| 0 | DM (Deathmatch) | no | yes | yes (bank word 10) |
| 1 | TDM (Team Deathmatch) | yes | yes | yes (bank word 8) |
| 2 | RES (Rescue) | yes | yes | no |
| 3 | CAP (Capture) | yes | yes | no |
| 4 | SNE (Sneaking) | no | yes | no |
| 5 | BASE | yes | yes | no |
| 6 | BOMB | yes | yes | no |
| 7 | TSNE (Team Sneaking) | yes | yes | no |
| 12 | SDM (Stealth Deathmatch) | no | yes | no |
| 13 | INTERVAL | no | yes | no |
| 15 | SCAP (Sneaking Capture) | yes | yes | no |
| 16 | RACE | no | yes | no |

**The sides column is measured from the rule, not the map.** `MatchRuleUtils.HasTeams`
is the server's expression of the client's `COM_svGetTeamType` model; the same map
can be free-for-all under DM and two-sided under TDM. The side values on the wire are
`0` (blue), `1` (red), `2`/orange for the third UI role, and `0xe0` for a slot the
room cannot place (`TeamArrayFramesUtils.TeamArrayNoTeam`).

**The item-box group is measured, not a rule gate.** The reference ticket capture
(`rounded-ended-by-reaching-tickets-limit.pcapng`) writes the same `0x1a50`/`0x1a5a`
pair in its round that Capture writes, so the old "all but TDM" gate was wrong: a
client sent no group keeps the stage's own RES props standing. The pair is the
generic ENVG (`0x8c`) and drum can (`0x71`), not an objective target; the stage
script's rule branches place those (`proc51`/`proc52`/`proc53`/`proc61`/`proc67`).
The host now writes the pair in every rule; TDM and CAP are the measured rows, and
the other rules' live box sets remain **[U]** beyond this generic opener.

COOP `8`, TRA `9`, OFFICER_TRA `10`, CLANROOM `11` and ANEST `14` are not rotation
rules in this server. Rule 14 is not accepted by the rotation parser.

## 2. The short answer: one channel, conditional records

A host playing DM and a host playing BOMB write the same kinds of records:

- the room/rotation settings before the round;
- the opening run and state block;
- the operations clock and the kill feed;
- the scoreboard;
- the team arrays / rules roster;
- the round result.

What differs is:

1. **The rotation triple and the timer/ticket bank** in the room settings;
2. **The score row** used to compute the player's `score` bit;
3. **The team model** — side groups, team arrays, and team ticket records;
4. **Optional objective records** — the `0x04` objective feed, the `0x12` race goal,
   the `0x13` rating array;
5. **The state offsets that mean something** — the two objective holders and two
   capture clocks at `state[40..43]`; they are sent in every rule but only CAP is
   measured reading them;
6. **The item-box/world-object group** (`0x1a50` / `0x1a5a`) — the server omits it
   for TDM and emits it for the other rules;
7. **The end condition** — clock, tickets, or an objective the host cannot see.

There is no separate "CAP packet" and no rule byte in the operations header. The
client gets the rule from the room's rotation, and its rule object interprets the
shared state.

## 3. Room settings: where the rule is stated

### 3.1 The lobby `0x4313` reply

The lobby's get-game-details reply carries the room's rotation as sixteen triples,
three bytes each at `0x0a8`:

```text
[rule, map, flags] per round
```

and the per-rule bank at `0x10c` as seventeen big-endian `u32`s:

```text
SNE t, r | CAP t, r | RES t, r | TDM t, r, tickets | DM t, tickets | BASE t, r | BOMB t, r | TSNE t, r
```

The exact offsets, the `t`/`r`/`tickets` positions, and the measured rooms are in
[`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md) §2. The gameplay channel carries the
*effect* of these settings: the round's clock is `t × 480` 125 ms units, and the
tickets are the count the team ticket record steps down.

The third triple byte is the round's option, not a rule: `0x02` Drebin Points,
`0x04` Headshots Only, and `0x20` a second flag still unnamed. A rule's own identity
is the first byte; the option is how the rule is played.

### 3.2 The 161-byte room-state body

The `0x1001` room state carries the same rotation and the `0x55` current-slot byte.
The rule is read from the triple at `0x1a + 3 × slot`; the current slot is what the
native readers at `0x8EB0C0`/`0x8EB148` use. The 17 timer words at `0x65`–`0x86`
(field 29) are the gameplay channel's per-rule times in seconds:

- elements 0–7: DM, TDM, RES, CAP, SNE, BASE, BOMB, TSNE;
- elements 12–16: likely SDM, INTERVAL, ANEST, SCAP, RACE, in that order;
- elements 8–11: no shipped rule uses them; their values are not time.

The mapping is measured strongly for 12, 13 and 15 and supported for 16; no direct
ELF reader of the whole field 29 vector has been found, so the complete client use is
**[U]** (`UDP_SETTINGS_PAYLOAD.md` "Field 29").

### 3.3 What the server derives

`MatchSettings` stores a `MatchRotationEntry(Rule, Map, RoundOptions,
TimeLimitMinutes, Tickets)` per round. `MatchRotationUtils` reads the bank:

- `TimeIndex(rule)`: SNE 0, CAP 2, RES 4, TDM 6, DM 9, BASE 11, BOMB 13, TSNE 15;
- `TdmTicketsIndex = 8`; `DmTicketsIndex = 10`;
- `TimeLimitMinutes` falls back to the bank's first word when a rule has no index;
- `Tickets` returns a limit only for TDM and DM (a zero word is no limit).

Consequences the current server has to live with:

- The eight standard rules have a time source. SDM, INTERVAL, SCAP and RACE have
  no time index in this bank; their time in the gameplay field 29 tail is **[U]**,
  and a rotation that names them with `GAMEPLAY_SERVER_RULE_TIMERS` cannot state a
  positive time through this path.
- TDM and DM are the only rules the server gives a ticket limit. The other rules
  end on the clock in the current host, whatever their objective rules say.

## 4. The round: common data and rule-dependent data

### 4.1 Common to every rule

| record / field | where | what it is |
| --- | --- | --- |
| `0x00` state block | operations `0x1254`/`0x1a54` | 89-byte state vector; round result `state[2]`, match result `state[3]`, clock `state[87..88]` |
| `0x01` clock | operations | `u16` LE, 125 ms units; 480 = one minute |
| `0x02` state write | operations | patch `count` bytes at `offset`; offset is relative to the block's base two bytes before `state[0]` |
| `0x09` kill feed | operations | killer slot, victim slot |
| `0x0b` countdown | operations | briefing countdown and reporting set |
| `0x0e` round over | operations | one reason byte; the host's verdict |
| `0x0f` short record | operations | run's short record |
| scoreboard `0x0b` | `0x1001` | slot + 21-byte mask + values; bits 5 kills, 6 deaths, 8 score, 11 headshots, 20 rounds won, 47 combo, 152 last result, 153 streak (kill run after a kill, consecutive deaths after a death) |
| team arrays `0x85`/`0x86` | `0x1001` | 24 slot sides and occupancy; `0`/`1` sides, `0xe0` no side |
| rules roster | `0x1001`/roster run | member list, side groups `0x18`/`0x19`, flat `0xff` for no sides |
| round result | `state[2]` / `state[3]` | `2` team 0, `3` team 2, `1` drawn, `6` no result, `255` unwritten |

The state block is present in every rule. The values at `state[40..43]` are always
sent too, but their meaning is rule-dependent.

### 4.2 The offsets that only some rules read

`MatchStateBodiesUtils` names:

| state offset | field | sent by the server | measured meaning |
| --- | --- | --- | --- |
| 40 | objective holder 1 | `0xff` unclaimed | captured as a slot/flag in objective rounds; parser reads it |
| 41 | objective holder 2 | `0xff` unclaimed | same |
| 42 | capture clock 1 | `0xff` unclaimed | only the Capture mode reads it (parser) |
| 43 | capture clock 2 | `0xff` unclaimed | same |
| 87–88 | round clock | the round's clock | common |

The working server writes `ff ff ff ff` across 40–43 when nothing is held. The
`briefing-then-solo-game` capture wrote `1e` (slot 30 of a 24-slot table, naming
nobody); the current host's `0xff` is the parser's `HOLDER_NONE` and the correct
unclaimed value. **A wrong holder is the first thing to check when a Capture client's
objective HUD stays empty** (`STATE_VECTOR.md` §5).

Only Capture's use of 42–43 is named. Which of the two holders belongs to Rescue,
Base, Bomb, TSNE or SCAP is **[U]**; the fields exist in every rule, and a rule that
does not use them keeps them unclaimed.

### 4.3 Objective records

| opcode | shape | evidence | rule |
| --- | --- | --- | --- |
| `0x04` | `04 <kind> <slot>` | port builder `0x101efa40`; parser calls it a mode-dependent objective feed | objective rules; exact kinds per rule **[U]** |
| `0x12` | `12 <flags> <goal> <spawn>` | port builder `0x101ef340`; parser calls it a Race goal update | RACE |
| `0x13` | `13` + own slot + rating array | ELF `0xB2D6A8`; port `0x102ba9a0` | per-player rating; rule use **[U]** |

The `0x04` body's `kind` is the rule's objective event; the parser deliberately
does not name the kinds beyond "mode-dependent". The `0x12` Race record carries the
scorer's slot through the `0x04` feed correlation, not in the shown three-byte body.
The server currently writes none of `0x04`, `0x12` or `0x13`; it relays operations
it does not recognize.

### 4.4 Item boxes and world objects

The item-box family is `0x1a50` (box) and `0x1a5a` (state/devices), event kind 0
head, 1 item, 2 position, 3 tail. The server's opening pair is:

```text
Box 1: ENVG   (0x8c)
Box 2: DrumCan (0x71)
```

It writes the group in every rule. The earlier TDM exception was wrong: the
reference's ticket round writes the same `0x1a50`/`0x1a5a` pair, and a round sent
no group keeps the stage's own RES props standing on the map. The stage script
supports the same reading — `proc 23` constructs `NewTestItemboxManager` in every
gameplay stage — while the objective props are separate placements, chosen by
`proc 50`'s rule branches (`proc51`/`proc52`/`proc53`/`proc61`/`proc67`), not by
this group.

The rule targets are item ids **188 KEROTAN** and **189 GAKO**
(`docs/protocol/ITEM_AND_DAMAGE_TABLE.md`), not the ENVG/DrumCan pair. In the
measured TDM round whose client kept those props, the round run carried **no
`0x1a50`/`0x1a5a` record at all**; the stage's Rescue placements (`proc 52`) are
what remained standing. No captured item-box event carries 188 or 189. A host that
wants a real Capture/Rescue/TSNE objective cannot infer it from the generic pair.

### 4.5 Teams

`COM_svGetTeamType` in the ELF is the side model. Its decompiled body reads the
`-team` argument and sets match bits:

- `0` (blue): clears both `0x10000` and `0x40000`;
- `1` (red): sets `0x10000`;
- `2` (orange): sets `0x40000`.

The wire side values are not the loadout row. The `-team 0..14` argument on
`WpMng_SetWeaponList` / `ItemMng_SetItemList` is a client-side loadout row, not a
side; the side is the `0`/`1`/`0xe0` in the team arrays and the `2 + side` result
code.

The host's team-dependent records:

- **team arrays `0x85`/`0x86`** — every room gets the pair; a team rule fills `0`/`1`,
  a free-for-all leaves the occupied slots at `0xe0` (or `0`/`1` if the room did
  assign them);
- **rules member list** — `RulesMemberListBodiesUtils` groups members under
  `0x18 + side`; a room with no sides writes the flat `0xff` group;
- **team ticket records** — the `0x1001` body
  `0b 00 00 00 <08|40> 00 00 00 00 <left> 00`; TDM (and DM's bank word) only in the
  current server;
- **round result** — `state[2]`/`state[3]` names a side, or the drawn/no-result code.

### 4.6 Tickets

The per-rule bank has ticket words only for TDM (word 8) and DM (word 10). The
working capture's panel starting count is the same limit the host uses
(`GAME_SETTINGS_WIRE.md` §4). A death spends the victim's side's ticket; the
`0x1001` ticket record steps the count down and the round ends when a side reaches
zero. The server's `MatchResultService.TicketsReachedAsync` implements that for the
sides the room assigns.

DM is free-for-all (`HasTeams` false), so its bank ticket word has no two-sided
consumer in the current host; the ticket record's side byte has no FFA meaning
measured. Treat DM tickets as **[U]** until a DM capture with a ticket end is read.

### 4.7 Score

The client's score row is a 14-value `-score` row per rule, written by the stage
script's `proc 24` through `Gcl_Score_SetRuleRow`. Column 0 is the **kill score
award** — the points one kill adds — and column 1 the **death score penalty** — the
points one death deducts, written as a negative (or zero) coefficient. Neither is
the streak: bit 153 carries the unbroken kill run after a kill and the
consecutive-death count after a death. The other twelve columns read counters the
gameplay channel does not carry.

The server's `MatchScoreRulesUtils` applies:

| rule | kill award | death penalty | headshot award | combo award |
| --- | ---: | ---: | ---: | ---: |
| DM | 3 | −2 | 1 | 1 |
| TDM | 3 | −2 | 1 | 1 |
| RES | 7 | 0 | 3 | 0 |
| CAP | 3 | 0 | 3 | 0 |
| SNE | 3 | −2 | 2 | 0 |
| BASE | 3 | 0 | 0 | 0 |
| BOMB | 3 | 0 | 0 | 0 |
| TSNE | 5 | 0 | 0 | 0 |
| SDM | 7 | 0 | 0 | 0 |
| SCAP | 0 | 0 | 0 | 0 |
| INTERVAL | 0 | 0 | 0 | 0 |
| RACE | 3 | 0 | 0 | 0 |

The full 14-value stage rows, including the columns this host does not compute, are
in [`../analysis/gameplay-data-functions.md`](../analysis/gameplay-data-functions.md)
§3. The score is recomputed from the live counters and clamped to `[0, 65535]`, not
banked; the server writes the result to scoreboard bit 8. The award and penalty are
`MatchScoreRulesUtils.KillScoreAwardCoefficients` and
`DeathScorePenaltyCoefficients`; the streak counters are `MatchScoreService`'s
`KillStreak` and `ConsecutiveDeaths`.

**Index note, settled by the stage script.** `n001a`'s `proc 50` routes rule 15 to
`proc61` (`PRP_SCAP_TGT_00/01`) and rule 16 to `proc67`; `proc 24` gives rule 15
fourteen zeros and rule 16 the CAP-like row. So 15 is SCAP and 16 is RACE, and
`MatchScoreRulesUtils`'s array (0 at 15, 3 at 16) is right. The old
`gameplay-data-functions.md` label `SCAP 16` was the error and is corrected there.

### 4.8 What ends a round

The rules' own instruction texts (from the PC client's stage data, not a binary)
state the endings:

| rule | the rule's ending | what the host can see on the gameplay channel |
| --- | --- | --- |
| DM | score / clock | score, clock |
| TDM | a side's tickets reach zero | tickets, deaths, score, clock |
| RES | GA-KO to its goal, holder at timer 0, or all enemies down | no explicit objective report; position/health only |
| CAP | both targets in goals (timer resets), or most targets at 0 | holders/capture clocks in state; no explicit pickup/delivery report |
| SNE | Snake's dog tags, kills on Snake, or most kills at 0 | position/health; no dog-tag record located |
| BASE | all bases, or most bases at 0 | no base-ownership field located |
| BOMB | a planted bomb detonates, or defenders hold and clear all bombs | no plant/defuse report; `0x1a5a` devices are the candidate |
| TSNE | all enemies down or a target brought home / both targets kept | state fields are candidates; no measured end |
| SDM | last player standing in a shrinking area | no host-side area/end field located |
| INTERVAL | no competitive result | clock only |
| SCAP | a target taken to checkpoints a set number of times | no checkpoint record located |
| RACE | race goal/checkpoint progress | `0x12` goal, `0x13` rating |

The current server only decides a round on the clock or a TDM/DM ticket limit. A
clock-ended CAP/RES/BASE/BOMB/TSNE/SNE/SCAP round is written with the score winner
(`MatchResultService.ClockResultAsync`), which is the host's own reading and not the
rule's objective. The round result is still `state[2]`; the client's banner is the
side or the drawn/no-result code.

## 5. Rule-by-rule matrix

Columns: **Settings** = rotation/timer/ticket data; **State** = rule-specific state
fields; **Records** = optional operations/world records; **Sides** = team data;
**End** = host-visible end.

| rule | Settings | State | Records | Sides | End |
| --- | --- | --- | --- | --- | --- |
| DM | rule 0, time DM, tickets DM | score/result only | kill feed, scoreboard, boxes | flat `0xff`; `0xe0` slots | clock/score; DM ticket consumer **[U]** |
| TDM | rule 1, time TDM, tickets TDM | score/result only | kill feed, scoreboard, tickets, boxes | side arrays, side groups, ticket records | tickets or clock |
| RES | rule 2, time RES | holders/clocks present, semantics **[U]** | items; objective feed candidate `0x04` | side arrays/groups | objective/clock, not host-visible |
| CAP | rule 3, time CAP | state[40..43] holders and capture clocks | boxes; `0x04`; target props **[U]** | side arrays/groups | objective/clock; host falls back to score |
| SNE | rule 4, time SNE | result/score | boxes; kill feed; dog-tag record **[U]** | flat/`0xe0` | dog tags/kills/clock; not host-visible |
| BASE | rule 5, time BASE | holders/clocks candidate **[U]** | boxes; `0x04` candidate | side arrays/groups | bases/clock; not host-visible |
| BOMB | rule 6, time BOMB | holders/clocks candidate **[U]** | boxes/devices; `0x04` candidate | side arrays/groups | bomb/clock; not host-visible |
| TSNE | rule 7, time TSNE | state[87..88] clock; TSNE pair **[U]** | boxes; `0x04` candidate | side arrays/groups | targets/wipe/clock; not host-visible |
| SDM | rule 12; time from field 29 tail **[U]** | result/score | kills; boxes; area **[U]** | flat | last player/clock; not host-visible |
| INTERVAL | rule 13; time field 29 **[U]** | result/score | minimal | flat | no competitive result |
| SCAP | rule 15; time field 29 **[U]** | holders/checkpoints **[U]** | boxes; `0x04`/`0x12` candidates | side arrays/groups | checkpoints; not host-visible |
| RACE | rule 16; time field 29 **[U]** | rating **[U]** | `0x12` goal, `0x13` rating | flat | race goal; not host-visible |

**The `0x04` row is the one to watch.** It is the only operations record explicitly
described as an objective feed, and its `kind` is mode-dependent. A host that wants
to support CAP/RES/BASE/BOMB/TSNE/SCAP endings needs captures that pair the rule's
objective event with the `0x04` kind and the state offsets; the current corpus does
not have that pairing for every rule.

## 6. What the host sends now

The server's current per-rule behavior is narrower than the protocol:

| feature | implementation | gap |
| --- | --- | --- |
| rotation / rule / map / options | `MatchSettings`, `MatchRotationUtils`, `MatchStartRunService` | none for the eight standard rules |
| per-rule clock | `TimeLimitUnits` from the bank | SDM/INTERVAL/SCAP/RACE have no time index |
| per-rule score | `MatchScoreRulesUtils` | only kills/deaths/headshots/combo; SCAP/RACE indices settled above |
| team sides | `TeamArrayService`, `RulesMemberListBodiesUtils`, `MatchScoreService.TeamScores` | FFA/DM ticket behavior **[U]** |
| team tickets | `TeamTicketBodiesUtils`, `MatchResultService.TicketsReachedAsync` | TDM only in practice; DM's FFA ticket has no measured consumer |
| item boxes | `MatchStartRunService.ItemBoxes`, `BeatFramePlan` | measured generic pair written in every rule (TDM included); objective targets are the stage script's own props, not this group |
| objective state | `MatchStateBodiesUtils` writes `0xff` at 40–43 | no rule writes holders/clocks |
| objective feed | none | `0x04` not written |
| race goal / rating | none | `0x12` / `0x13` not written |
| round end | clock or TDM/DM ticket | objectives not host-visible; clock uses score |
| result | `state[2]`/`state[3]` | side/drawn/no-result only |

The server's run does carry the room's rotation and state; it does not need a new
packet type to add a rule. It needs the rule's **values** and **optional records**,
which is why the gaps above are data/behavior gaps rather than transport gaps.

## 7. Ghidra evidence

| ELF entry | project name | what it establishes |
| ---: | --- | --- |
| `0x00C50848` | `Gcl_COM_svGetRule` | the stage command every rule's script branches on; creates the rule object (`+0x60`) |
| `0x005AC628` / `0x00097C10` | `Gcl_ComGetCurrentRule_A/B` | the current-rule getters |
| `0x00229A78` | `Gcl_COM_svGetTeamType` | reads the `-team` argument and sets match bits `0x10000`/`0x40000`; blue/red/orange coding |
| `0x002ACD58` | `Gcl_NewSupervisor` | constructs the round's supervisor; stage arguments include `-TSneAlertSec`, `-GOP_FFLock_sec`, `-Liquid_GOP_sec`, `-SneDogTagComplete`, `-respawn_wait`, `-penalty_die`, `-penalty_max` |
| `0x000470E8` | `Gcl_Score_SetRuleRow` | the stage's `-score` row (`0xEDE912`); column 0 kill score award, column 1 death score penalty |
| `0x00B20B98` | `Gcl_Score_SetRuleSummary` | the shorter rule-score row |
| `0x0076F790` | `Gcl_NewTestItemboxManager` | per-box respawn timers; it does not choose box positions or items |
| `0x008EB0C0` | `Gcl_ItemMng_SetItemList` | the rule's item window |
| `0x00047110` | `Gcl_WpMng_SetWeaponList` | the rule's weapon pool; `-team` here is a loadout row, not a side |
| `0x00270E00` | `P2P_RunOpener_BuildAndSend` | the host's run/state body |
| `0x002729D8` | `P2P_PhaseTick_SendOperations` | the phase tick's `0x01` clock, `0x0e` round-over, `0x0c` words, lone `0x00` |
| `0x0027E2B0` | `P2P_GetOperationStateBlock` | state index → the 1248-byte slot block |
| `0x0027E578` | `P2P_WriteOperationStateField` | validated state field write and publish marking |
| `0xB2D6A8` | `0x13` rating builder | per-player rating array on the operations stream |
| port `0x101efa40` | `0x04` builder | objective feed: opcode + two bytes |
| port `0x101ef340` | `0x12` builder | race goal: one byte + reads a pair |
| port `0x101efc20` | `0x07` builder | host-only three bytes; no rule name measured |
| port `0x101efd00` | `0x08` builder | host-only two four-bit fields; no rule name measured |

**The network layer is rule-agnostic.** The ELF's `P2P_*` builders append state and
opcodes; the rule object and the stage script decide the values. That is why the
same `0x00`/`0x01`/`0x09`/`0x0b`/`0x0e` records appear in every captured rule.
The rule-specific part is data selected before the send, not a different sender.

## 8. Why `KILL COUNT +3` is the score award, not the streak

The latest first kill in `myserver.pcapng` is:

```text
34.980  kill feed 16 -> 3
34.998  slot 16: {5:1, 8:4, 11:1, 152:5, 153:1}
```

- `5` is kills: **1**.
- `8` is score: **4** — a TDM kill is 3 and the first kill was a headshot (+1).
- `11` is headshots: **1**.
- `153` is the kill streak: **1**.

The client's kill notice is built by `FUN_102b7810` → `FUN_102984e0`. The handler
increments `this+0x8a8` when the killer is the local player and clears it when the
local player is the victim; `FUN_102984e0` writes that value to
`root/hit_opponent/kill_combo` and the literal `1` to `root/hit_opponent/kill_num`.
The first kill therefore has `kill_combo = 1`, `kill_num = 1`.

The **+3** is the kill's base score award, not the streak. A normal kill is worth 3;
a headshot is 4. The HUD's "KILL COUNT" text is the score/kill-award line; the
streak is the client's `0x8a8`/scoreboard `153` value. The death side of the same
bit-8 score is the **death score penalty**: DM and TDM deduct 2 per death, which is
inside the score value, not in bit 153. On a death bit 153 states `1` for the
victim's first death (`slot 3: {6:1, 8:0, 152:2, 153:1}`), the value the server
keeps as `ConsecutiveDeaths`; it is a counter, not points. That is why the server's
fields are named `KillScoreAwardCoefficients` / `DeathScorePenaltyCoefficients` for
the score terms at bit 8, and `KillStreak` / `ConsecutiveDeaths` for the bit-153
counters. The stale bot-feed replay that
put a previous round's feeds into the result screen was the defect that could make
the local `0x8a8` jump; the current host clears the bot feed queue at the close and
does not write it on the result screen. There is no further server-side change to
make a first kill show +1: the `153` value is already 1, and reducing the score to 1
would diverge from every captured host.

## 9. Open questions

1. The exact `0x04` `kind` values for CAP, RES, BASE, BOMB, TSNE, SNE, SCAP and
   RACE. The port builder exists; the corpus has not paired every kind with its
   rule.
2. Which state holder (`state[40]` vs `state[41]`) belongs to which objective, and
   whether Base/Bomb/TSNE reuse the Capture clocks.
3. The target props' record. The generic item-box pair is settled (ENVG/DrumCan),
   but the objective props are the stage script's own `PRP_*` placements, chosen in
   `proc 50`'s rule branches (`proc51`/`proc52`/`proc53`/`proc61`/`proc67`). Which
   host record, if any, creates them is **[U]**.
4. The DM ticket consumer: the bank has a DM ticket word, but DM is free-for-all
   and the current ticket records are side-based.
5. The time source for SDM, INTERVAL, SCAP and RACE in field 29's tail; no direct
   ELF reader of the whole vector has been located.
6. The `0x07`/`0x08` host-only bytes. They are real and host-only, but no rule name
   or field meaning has been measured.

## 10. Reproducing the readings

```bash
# state block and clock on the operations stream
python3 tools/udp_state_vector.py .pcapngs/pcapng1/mgo2-game1.pcapng

# the room's rotation and timer bank
python3 tools/pcap_decrypt.py .pcapngs/pcapng1/mgo2-game1.pcapng --type tcp --json

# the gameplay records around a round
python3 tools/udp_frame.py .pcapngs/pcapng1/mgo2-game1.pcapng --flow ...

# Ghidra
#   Gcl_COM_svGetRule       0x00C50848
#   Gcl_COM_svGetTeamType   0x00229A78
#   Gcl_Score_SetRuleRow    0x000470E8
#   P2P_PhaseTick_SendOperations 0x002729D8
```

The capture names and the exact measured offsets are in
[`GAME_SETTINGS_WIRE.md`](GAME_SETTINGS_WIRE.md),
[`STATE_VECTOR.md`](STATE_VECTOR.md), and
[`../analysis/rotation-payload-corpus.md`](../analysis/rotation-payload-corpus.md).
