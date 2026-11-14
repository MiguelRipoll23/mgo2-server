# Gameplay data functions in `MGO2.ELF`

**What this file answers.** Where the gameplay data the server replays is built in
the client, and which stage-script values feed it. It starts from the data phase,
not the handshake: the stage scripts, the GCL native registry and the command
names they carry.

**Binary note.** The open Ghidra program is the **1.36** image, not the release-day
disc build that [`../ADDRESSES.md`](../ADDRESSES.md) indexes. Its sections run to
about `0x129c200` (19.5 MB), and the registry entries below are derived from this
image. Do not apply the old release-day addresses to it.

## 1. How a GCL command becomes an ELF function

The stage scripts are decompiled by `.gametools/gcl-vscode`; every command refers
to a 24-bit name hash. `.gametools/gcl-vscode/data/commands.txt` maps the hash to
the command label. The ELF's GCX native registry is a sorted run of 8-byte
`{opd, nameHash}` entries. For example:

```
0x0121E618: 01 20 3E 78    OPD
0x0121E61C: 00 0F 4B 64    ItemMng_SetItemList
```

The OPD holds `{entry, toc}` in the PPC64 ELFv1 form. At `0x01203E78`:

```
00 8E B0 C0    entry
01 22 2A 00    TOC
```

So `ItemMng_SetItemList` is the function at `0x008EB0C0`. The other mappings below
were derived the same way. The names are the **registered command names**, not
claims about the wrapper body: several commands are thin wrappers over shared
operation-state accessors, and the wrapper's locals are deliberately left unnamed
where the body does not prove a field.

## 2. The mapped gameplay commands

| GCL command | hash | registry OPD | function entry | Ghidra name |
|---|---:|---:|---:|---|
| `ItemMng_SetItemList` | `0x000F4B64` | `0x01203E78` | `0x008EB0C0` | `Gcl_ItemMng_SetItemList` |
| `DP_SetActionPoint` | `0x00AFE7F0` | `0x011F86E8` | `0x00542BB8` | `Gcl_DP_SetActionPoint` |
| `COM_svGetRule` | `0x00F576C7` | `0x0120D9A8` | `0x00C50848` | `Gcl_COM_svGetRule` |
| `ComGetCurrentRule` | `0x001E226C` | `0x011F93D0` | `0x005AC628` | `Gcl_ComGetCurrentRule_A` |
| `ComGetCurrentRule` | `0x00A10D47` | `0x011EA498` | `0x00097C10` | `Gcl_ComGetCurrentRule_B` |
| `addrs` | `0x00CEA915` | `0x01204110` | `0x008EECD8` | `GcxAddrsNative_LoadHostOverrides` |

Each function carries a Ghidra plate comment with its hash, OPD and the source of
the name.

`GcxAddrsNative_LoadHostOverrides` is the only one whose fields are named with
confidence in this pass. Its locals are now:

| local | meaning |
|---|---|
| `override_file_descriptor` | descriptor returned by the `d/testhk` open |
| `host_override_table` | pointer to the override record |
| `bytes_read` | result of each 8-byte read, checked against 8 |
| `host_override_globals` | the native's global context |

The function reads two eight-byte records, validates both as complete reads, then
clears the table and its second record when either is short. The two host slots
and the port handling behind it are the `addrs` behaviour documented in
[`../HOSTS.md`](../HOSTS.md) §9.

## 2b. The data-phase producers

The gameplay channel's own builders are separate from the GCL commands. These are
the functions the data phase actually uses, from the instruction-level analysis in
[`udp-handlers-and-senders.md`](udp-handlers-and-senders.md):

| function entry | Ghidra name | what it does |
|---:|---|---|
| `0x00270E00` | `P2P_RunOpener_BuildAndSend` | builds the run body (state byte, six-byte record header, a 23-byte block, the marked `u16` table and two strings) and sends it |
| `0x002729D8` | `P2P_PhaseTick_SendOperations` | the per-phase tick: `0x01` clock, `0x0e <round-over>`, `0x0c` marked words, lone `0x00` |
| `0x0027E2B0` | `P2P_GetOperationStateBlock` | state index → the state's block through the 26-entry table |
| `0x0027E480` | `P2P_ReadOperationStateField` | validated read through the descriptor's field table |
| `0x0027E578` | `P2P_WriteOperationStateField` | validated write and publish marking |
| `0x0026CAD8` | `P2P_CursorOpen` | opens a body cursor with buffer and capacity |
| `0x0026CC10` | `P2P_CursorAppendBytes` | byte-loop append used by the body builders |
| `0x0026B778` | `P2P_SendRecord` | sends a cursor under a handle |
| `0x0026B2B0` | `P2P_AnswerRecord` | answers the record a handle names |
| `0x0026E308` | `P2P_SetRoundOverPendingVerdict` | sets/clears the pending round-over and `instance+0xC06` |
| `0x0026D7C0` | `P2P_IncrementPendingRoundOverCounter` | increments the pending round-over count |
| `0x002753C8` | `P2P_PublishPlayerRecord` | applies an incoming player record to a room entry and publishes ten of that player's state fields, the 24-byte settings header among them |
| `0x00276E88` | `P2P_ReadPlayerSettingsHeader` | reads a 0x164-byte settings body into a player entry in state `0x211`, including the header at `+0x24` |
| `0x0026FC80` | `P2P_RemovePlayerEntry` | the leave side: tears the entry down, republishes its owner and decrements the room's player count at `instance+0xD0` |

`0x273370` and `0x273600` are not separate functions: Ghidra resolves both into
`P2P_PhaseTick_SendOperations` (`0x002729D8`). They are the round-over and clock
arms inside that tick.

### Field semantics proven by these functions

* A record cursor is `+0` write position, `+4` capacity, `+8` buffer and `+2` the
  count written so far. Appending advances `+0` and `+2`, and a send does not move
  them, so the same cursor can be sent to every peer.
* A state field's flags are `0x0001` "publish only when the value changes" and
  `0x0040` "publish always". A field with neither is written but not marked.
* A mark is the two-bit code `3` in the state's transfer map; the inbound code is
  `2`. The map is therefore directional: `2` is wire → state, `3` is state → wire.
* The round-over byte on `0x0e` is `instance+0xC06`: a count of round-overs recorded
  while a verdict was pending, not a rule id. `instance+0xBCC` bit 9 is "verdict
  pending"; bit 10 is "count already incremented".
* The run opener writes `+0xC18 = 0x12C` on open, clears `+0xC06`, `+0xC08`,
  `+0xC14` and `+0xD0`, and registers handlers `0x0B`, `0x08` and `0x0D`.

## 2c. The supervisor's own actor commands

Six more of the commands the gameplay stages declare are the ones a dedicated host
has to answer, and they resolve exactly like the six in §2 — `commands.txt` hash →
registry entry → OPD → entry:

| GCL command | hash | registry | OPD | function entry | Ghidra name |
|---|---:|---:|---:|---:|---|
| `NewSupervisor` | `0x0099E3A0` | `0x0121EA9C` | `0x011F28B8` | `0x002ACD58` | `Gcl_NewSupervisor` |
| `NewTestItemboxManager` | `0x004B3368` | `0x0121E7E4` | `0x011FE650` | `0x0076F790` | `Gcl_NewTestItemboxManager` |
| `NewHeadUpDisplay` | `0x00D4EA76` | `0x0121ED44` | `0x011FE088` | `0x0075E120` | `Gcl_NewHeadUpDisplay` |
| `DP_SetMultiplier` | `0x004342CB` | `0x0121E77C` | `0x012092E8` | `0x00AC4D58` | `Gcl_DP_SetMultiplier` |
| `COM_svGetTeamType` | `0x009CD15B` | `0x0121EACC` | `0x011EFC50` | `0x00229A78` | `Gcl_COM_svGetTeamType` |
| `COM_svIsHost` | `0x003DC855` | `0x0121E754` | `0x011E8298` | `0x00036C18` | `Gcl_COM_svIsHost` |

`NewSupervisor` is the round's owner. Every stage builds it with `-player_proc`,
`-init_hud`, `-next`, `-exit`, `-respawn_wait`, the two penalties and the mode
timers, and it is the actor whose verdict ends a round — the wire side of that is
§5. `NewHeadUpDisplay` is the actor the supervisor is handed as `-init_hud`; the
script passes it the BGM table and the seven rank ladders, and its constructor
fills a `0x30`–`0x130` table from those arguments. It takes the stage's display
data as script arguments, and the result-banner row is not among them.
`COM_svIsHost` is the script's own host test and `COM_svGetTeamType` the rule's
team model — the two questions only a peer that owns the round can answer.

## 2d. The weapon, inventory and player-argument commands

The three biggest call counts in §3 belong to the weapon manager, and these six
resolve the same way:

| GCL command | hash | registry | OPD | function entry | Ghidra name |
|---|---:|---:|---:|---:|---|
| `WpMng_SetWeaponList` | `0x00D7CBE5` | `0x0121ED6C` | `0x011E8788` | `0x00047110` | `Gcl_WpMng_SetWeaponList` |
| `WpMng_SetAmmoCount` | `0x005D0CC1` | `0x0121E894` | `0x011FCD40` | `0x00717640` | `Gcl_WpMng_SetAmmoCount` |
| `WpMng_SetCustom` | `0x00538FD2` | `0x0121E844` | `0x011EA5A0` | `0x0009BFC0` | `Gcl_WpMng_SetCustom` |
| `GM_COM_InventoryWeaponSet` | `0x005AF36E` | `0x0121E874` | `0x011F82C8` | `0x00519200` | `Gcl_GM_COM_InventoryWeaponSet` |
| `ComNetPlayerSetArgs` | `0x004EF1F0` | `0x0121E824` | `0x011F3228` | `0x002D08D8` | `Gcl_ComNetPlayerSetArgs` |
| `ComHostingSmallRoom` | `0x0047E592` | `0x0121E7BC` | `0x011E8198` | `0x00035EB0` | `Gcl_ComHostingSmallRoom` |

`WpMng_SetWeaponList` (2 291 calls) is the rule's weapon pool and
`WpMng_SetAmmoCount` (2 148) its starting ammunition — the stage-side twin of the
ELF's own 141-row weapon table ([`../WEAPONS.md`](../WEAPONS.md));
`WpMng_SetCustom` (693) adds the customisation slots, and
`GM_COM_InventoryWeaponSet` (21, once per stage) the larger inventory set.
`ComNetPlayerSetArgs` (693) is the per-player argument set the script writes before
a round — the script side of the player entry §2b's publisher fills — and
`ComHostingSmallRoom` (357) the branch taken when the room hosts itself.

## 3. What the stage scripts put into these commands

All twenty-one stage scripts in the client install use the same supervisor
skeleton. Each carries:

| directive | count per stage | what it configures |
|---|---:|---|
| `ItemMng_SetItemList` | 35 (33 in `n001a`) | the rule-specific item pool |
| `DP_SetActionPoint` | 17 | the rule's action-point IDs |
| `respawn_wait` | 1 | the respawn delay |
| `NewTestItemboxManager` | 1 | the item-box manager actor |
| `init_hud p:proc32` | 1 | the HUD proc |

The supervisor is `proc31` in `n001a`:

```gcl
command [99e3a0/NewSupervisor] \
    -player_proc p:proc21 \
    -init_hud p:proc32 \
    -next p:proc44 \
    -exit p:proc45 \
    -respawn_wait 8 \
    -penalty_die 3 \
    -penalty_max 9 \
    -TSneAlertSec 5 \
    -GOP_FFLock_sec 4 \
    -Liquid_GOP_sec 8 \
    -SneDogTagComplete 3
```

`-respawn_wait 8` is the measured eight-second respawn delay. The penalty fields
are the rule's death penalty and cap, and the TSNE/GOP fields are mode-specific
timers. The item-box manager is a separate `chara`:

```gcl
chara [4b3368/NewTestItemboxManager] [a05dc9]
```

### Item lists

`ItemMng_SetItemList` takes `-item_id` followed by item ids. `n001a` uses several
sets, including:

```
16 9 20 0 8 19       // RKnife, D.Eagle L, P90, empty, SIG GSR, Ingram
0 9 20 8 19          // empty, D.Eagle L, P90, SIG GSR, Ingram
16 9 0 8 19          // RKnife, D.Eagle L, empty, SIG GSR, Ingram
21 6 10 20 9 8 0     // Bizon, FIVESEVEN, THOR, P90, D.Eagle L, SIG GSR, empty
```

Those are protocol item ids from
[`../../src/Shared/Types/ItemIdentifier.cs`](../../src/Shared/Types/ItemIdentifier.cs),
not the ELF weapon ids. `n001a` writes **33 lists in seven distinct shapes**, and
every list is an item window of five or six slots — four fixed slots plus an
empty one, with the rule's own substitution:

| list | uses | ids and names |
|---|---:|---|
| `0 9 20 8 19` | 9 | Empty, D.Eagle L, P90, SIG GSR, Ingram |
| `16 9 0 8 19` | 8 | RKnife, D.Eagle L, Empty, SIG GSR, Ingram |
| `0 9 0 8 19` | 6 | Empty, D.Eagle L, Empty, SIG GSR, Ingram |
| `16 9 20 8 19` | 4 | RKnife, D.Eagle L, P90, SIG GSR, Ingram |
| `16 9 20 0 8 19` | 4 | RKnife, D.Eagle L, P90, Empty, SIG GSR, Ingram |
| `0 9 20 0 8 19` | 1 | Empty, D.Eagle L, P90, Empty, SIG GSR, Ingram |
| `21 6 10 20 9 8 0` | 1 | Bizon, DEagle, Thor, P90, D.Eagle L, SIG GSR, Empty |

The five fixed ids are the rule's floor: **SIG GSR (8) and Ingram (19)** in every
list, a knife or nothing in slot 0, and a handgun in slot 1 — D.Eagle L (9)
almost always, the plain DEagle (6) only in the seven-slot Bizon list. What the
rule varies is slot 2 and the knife: P90 (20) or nothing, RKnife (16) or nothing,
and one site's Bizon/Thor pair. Every site carries a numeric `-team` as well
(0–13 in `n001a`, 0–14 in `n002a`, `n013a` and `sm_dd`), and the next subsection is
what that number is — it is **not** a side. The `@proc40` test that splits several
lists is the branch that adds the Drebin-only slots.

The dedicated server writes the captured opening box pair rather than deriving a
list from the stage, and that is right: these lists are the **player's item
window**, not the boxes' contents. What a box holds is the `0x1a50` event's item
id (§4).

### What `-team` on a loadout command is, and what names a side

The loadout commands — `WpMng_SetWeaponList`, `WpMng_SetAmmoCount`,
`ItemMng_SetItemList` — take `-team N`, and `N` runs **0–13 in `n001a` and 0–14 in
`n002a`, `n013a` and `sm_dd`**: fifteen rows at most, each one a complete loadout
of three weapon groups plus an item window and its ammunition:

```gcl
command [d7cbe5/WpMng_SetWeaponList] -team 3 -group 0 -wp_id 9 0 27 0 23 0 20 0 ...
command [d7cbe5/WpMng_SetWeaponList] -team 3 -group 1 -wp_id 10 0 7 0
command [d7cbe5/WpMng_SetWeaponList] -team 3 -group 2 -wp_id 52 0
command [0f4b64/ItemMng_SetItemList]  -team 3 -item_id 0 9 20 8 19
```

**That `N` is a loadout row, not a side.** Where the script wants a side it asks
the rule by name:

```gcl
command [9cd15b/COM_svGetTeamType] \
    -team blue
```

and compares the answer (`== 0` in `n001a`'s OctoCamo branch), so a side is the
*word* `blue` or `red`; the numeric row is what the rule's branch then fills.
`proc 34` opens with `if (7 == COM_svGetRule)` and writes rows from 0 upward, so
which row a rule writes follows the rule and not the room's side numbering, and
the same row number means a different loadout under each rule.

Two further places name a side, and both of them are numeric and both stop at
`2`: a `chara` spawn (`proc 62` writes `-team 0 -prp PRP_HOMEBASE_A` and
`-team 1 -prp PRP_HOMEBASE_B`, one home base each), and `COM_svGetTeamType`'s
own body, where the argument at hash `0x0025FF48` is read and `0`, `1` and `2`
set bit 16, bit 18 and neither of them at match `+0x394`. Three values, three
colours — that is the side enumeration, and it is the same one the wire carries.

| stage | rows given a weapon list and an item window | rows given ammunition only |
|---|---|---|
| `n001a` | 0, 4, 5, 8, 11 | none |
| `n002a` | 0, 4, 5, 8, 11 | 2, 3, 6, 7, 9, 10, 12, 13, 14 |
| `n013a` | 0, 4, 5, 8, 11 | 2, 3, 6, 7, 9, 10, 12, 13, 14 |
| `sm_dd` | 0, 4, 5, 8, 11 | 2, 3, 6, 7, 9, 10, 12, 13, 14 |

The rows are written by rule, not by side, and the stages agree on it to the
row: `proc 34` writes row 0 **inside** `if (7 == COM_svGetRule)` and then calls
`proc 36`–`proc 39` whatever the rule, which fill rows 4, 5, 8 and 11; the row-5
proc is itself guarded by `if (({5e8ea5} & 2) || (9 == COM_svGetRule))`. The
ammunition procedure of the stages that have one (`proc 108` in `n002a`,
`proc 100` in `n013a`, `proc 117` in `sm_dd`) writes **2, 3**, **6, 7**,
**9, 10** and **12, 13** as pairs of the same weapon ids at different counts,
plus 14 alone — variants of a pool, not a side each.

So a row reads as a **team type**: one team of one rule, and the fifteen of them
are the teams the stage's rules can field. Which team type a rule paints blue,
red or orange is **[U]** — it is the client's rule table's, not the stage's, and
nothing in these scripts names it. The arithmetic (15 = 3 × 5) is not evidence
for a three-side layout of the row space; the only three-value enumeration the
files hold is the side one above.

**The room's three sides and where each is numbered.** `RoomSideUtils` holds
**blue `0`, red `1`, orange `2`**. The wire names a side in exactly two places,
and neither of them is the script's row:

| where | coding | evidence |
|---|---|---|
| the `0x85`/`0x86` team arrays | `0` and `1` per slot, `0xe0` for a slot on no side | measured, `GAME_SETTINGS_WIRE.md` §4 |
| the result, `state[2]` / `state[3]` | `2 + side` — `2` blue, `3` red, `4` orange | measured as `2` and `3`, `MatchRecordBodiesUtils.ResultCode` |

So there is **no `-team 0..14` ↔ side mapping on the wire at all**: the number is
a client-side loadout row, chosen by the rule's branch, and the side is only ever
the `0`/`1`/`2` the getter answers, the `0`/`1` a `chara` spawn names, or the
`0`/`1`/`0xe0` the team arrays carry. What a host can use from these blocks is
the rule's weapon pool and item window, per row — which is what §3's list table
records — and a host that wanted to write per-side lists would first have to
read the team type a side takes, which these files do not state.

### The rule's score rows

Every gameplay stage's `proc 24` switches on `COM_svGetRule` and writes that
rule's two score rows — a fourteen-value row on `[ede912]`
(`Gcl_Score_SetRuleRow`) and a shorter one on `[adb9e7]`
(`Gcl_Score_SetRuleSummary`). Neither hash has a name in `commands.txt`, so the
Ghidra names are descriptive:

| rule | `-score` (14) | second row |
|---|---|---|
| DM `0` | `3 -2 3 -2 1 0 0 0 0 5 0 0 0 2` | `5` |
| TDM `1` | `3 -2 2 -1 1 0 -5 7 2 5 1 0 0 2` | — |
| RES `2` | `7 0 7 0 1 5 -5 10 0 0 2 0 3 2` | `0 1 0 5 0` |
| CAP `3` | `3 0 3 0 0 5 -5 7 5 0 1 0 7 2` | `10 1` |
| SNE `4` | `3 -2 2 -1 1 5 -5 7 5 2 1 0 0 2` | `3 5 5 3 0 6 4 5` |
| BASE `5` | `3 0 3 0 0 5 -5 7 5 0 1 0 0 2` | `1 5 20` |
| BOMB `6` | `3 0 3 0 0 5 -5 7 5 0 1 0 0 2` | `15 15 10 1 1` |
| TSNE `7` | `5 0 5 0 0 5 -5 10 5 0 2 0 3 2` | `3 0 5 0 10` |
| SDM `12` | `7 0 3 0 1 0 0 0 0 5 0 0 0 2` | `5` |
| SCAP `16` | `3 0 3 0 0 5 -5 7 5 0 1 0 7 2` | `2 5 3` |
| INTERVAL `15`, default | fourteen zeros | — |

**Column 0 is the kill value and column 1 the death value** — the two the
gameplay channel can support. They agree with the measured score in a capture (a
TDM kill is worth 3) and they are what `MatchScoreRulesUtils` reads. The other
twelve columns read counters this channel does not carry (headshots, stuns,
wakes, combo, assists, hacks, objectives, the team-win bonus), so their names are
not claimed here.

**Two rows of the server's table disagreed with these and were corrected:**
Capture pays **3** per kill, not 5, and Bomb pays **3**, not 0 — `proc 24` has a
`case 6` for Bomb, so the earlier note that "no stage script emits 6" was wrong.
SDM (`7`) and SCAP (`3`) were missing from the table's range and are now
covered.

### Action points

`DP_SetActionPoint` takes one `-point` value. `n001a` uses points 0, 1, 2, 3, 4,
12 and 15 at different sites; the other stages use the same seventeen-call shape.
The command name says they are Drebin/action points, but the exact score or HUD
consumer is not yet traced, so the values are recorded as script evidence only.

### HUD

`proc32` builds `NewHeadUpDisplay` and the weapon-performance manager. It carries
the BGM table and the seven rank ladders:

```
-dmg_rank 1000 500 301 251 201 1
-slp_rank ...
-stn_rank ...
-dst_rank ...
-pnt_rank ...
-stb_rank ...
-rld_rank ...
```

It then writes the long `-rankset` row table. This is the client's HUD/weapon
display data, not a field the gameplay server sends on the wire. It is useful for
naming a value seen in a capture only after the same value is found in one of
these rows.

### What the stages call

Counting the registered commands across the twenty-one gameplay scripts gives the
vocabulary the data phase is built from:

| command | calls | what it is |
|---|---:|---|
| `WpMng_SetWeaponList` | 2291 | the rule's weapon pool |
| `WpMng_SetAmmoCount` | 2148 | starting ammunition |
| `GM_COM_MaterialSetMtr` | 1722 | stage materials |
| `COM_svGetRule` | 1327 | the rule's own values (§2) |
| `ItemMng_SetItemList` | 733 | the item pool (§3) |
| `WpMng_SetCustom` | 693 | weapon customisation |
| `ComNetPlayerSetArgs` | 693 | per-player network arguments |
| `WpMng_AddBullet` | 460 | placed ammunition |
| `DP_SetActionPoint` | 357 | Drebin action points (§3) |
| `ComHostingSmallRoom` | 357 | the room-hosting branch |
| `WpMng_AddWeapon` | 231 | placed weapons |
| `COM_svGetTeamType` | 231 | the rule's team model (§2c) |
| `NewRepressBase` | 147 | base and spawn suppression |
| `GM_COM_TrapSwitchCommand` | 105 | trap switches |
| `COM_svIsHost` | 63 | the host test (§2c) |

The counts are across all twenty-one stages, which is why they read as multiples
of the per-stage numbers in §3 (`ItemMng_SetItemList` 733 ≈ 35 × 21).

## 4. Spawns and item boxes

### Player spawns are stage points, one pair per side

The room's 25-byte player entry carries **the stage's own `HOMEGATE_A01` and
`HOMEGATE_B01`**, not a random coordinate. `MapSpawnUtils` holds the pair for each
map; Blood Bath is `(-23750, 20750)` and `(20750, -23750)`, exactly the two
`HOMEGATE` placements in its GEOM. The heading is the side across, `0x5FFF` on
the smaller-`x` side and `0x0000` on the other.

A stage has more than one gate: `PRP_HOMEGATE_A01`…`A16` and `B01`…`B16`, plus
mode families such as `PRP_TEAM_DEATHMATCH_*`, `PRP_RESCUE_*`, `PRP_BOMB_*` and
`PRP_SCAP_*`. The room entry uses the `01` pair. **Whether a live supervisor
chooses another gate at random or by mode is still open**; the current server does
not randomize it, and no capture used for the entry shows a random gate.

### The recorded opening boxes are fixed points

`ItemBoxRecordBodiesUtils` writes the two boxes the measured round opens with:

```
Box 1: ENVG       (-1092, 1800, 1650)
Box 2: DrumCan    (550, 130, -1700)
```

These are stage coordinates, not a random draw. The box is created by the
four-record event under `0x1a50` (head, item, position, tail); the recurring
six-byte pair is only the "still there" state. Later box events are live-round
play and are not replayed. The stage's own `PRP_CBOX` effects are the candidate
positions for a live supervisor, but the selection code is not located yet.

`MatchRuleUtils.PlacesItemBoxes` is the rule gate: Team Deathmatch places no
boxes, while Capture and the other objective rules do. That is why a TDM beat
carries no box group even though the group's filler is otherwise periodic.

### The stage places twelve boxes, and the manager only times them

The two positions above are not the stage's whole set. `n001a`'s `proc 98`
builds the boxes the round plays with:

```gcl
proc 98 {
    command [b577cd/FakeCBox] \
        -props CBOX \
        -count 12
}
```

**Twelve boxes at stage-placed points**, so the positions are the stage's props
and not a draw: a box can only appear where its `CBOX` placement is. The item the
box carries is chosen per box — ENVG (`130`) and DrumCan (`107`) in the measured
round — which is where the per-round variation lives.

`Gcl_NewTestItemboxManager` does **not** choose positions or items. Its body reads
seven global settings through a `get-with-default` helper (defaults `180000`,
`0xffffffff`, `0`, `0`, `0`, `0`), scales each by `3000` — milliseconds to the
game's units — stores six of them at `+0x20`…`+0x34`, copies the first to the
**24 per-box timers at `+0x3c`**, and writes a per-player countdown byte at
`+0x8b` as `(value / 0x32 + 0x3b) / 0x3c`. So it is the box **respawn timer**
configuration, one timer per box, and the `180000` default is three minutes. A
stage that wanted boxes on a different rhythm sets those settings; nothing here
moves a box.

Whether the *item* in each of the twelve boxes is re-drawn every round is still
open: the manager does not draw it, and the box record a round opens with carries
the item per box. The measured round has two boxes of the twelve, ENVG and
DrumCan, so a host replaying twelve boxes needs the stage's `CBOX` positions,
which are in the stage's own data rather than this repo.

## 5. Round end and the final round

The measured round end is not a client report. It is the host's own clock and
state write:

| record | meaning |
|---|---|
| `0x01` on the operations stream | the round clock in 125 ms units; 480 units = one minute |
| clock `0` | the round is over |
| `0x0e <reason>` | the host announces the round-over reason |
| `state[2]` | the round winner: `2` raw team 0, `3` raw team 2, `1` drawn, `255` not written |
| `state[3]` | the match result, same coding |

`joined-when-red-team` writes `state[2] = 2` at the first two round ends and
`state[3] = 2` with the second, matching the client note's 2–0 match. A round
inside a stage opens the next round on the sibling generation **without a
briefing**; the last round of a stage returns the room to a briefing with clock
`0`. `MatchStartRunService.End` is the server's equivalent close, and
`MatchRoundStateService.Close` writes the zero clock.

### What the client is doing behind the WAIT screen

A client that reports from its briefing screen then shows its wait — the
`PLEASE WAIT` line is the stage table's own text (id `4229`, with its French, German,
Italian and Spanish rows at `4230`–`4233`) — while the host
assembles the round. What it is waiting for is **the rest of the room**, not a
timer: the reference opens a round on the *last* client's departure report, and
only one of those instants is the countdown's own end.

| instant | `joined-when-red-team` | what it is |
|---|---|---|
| all-ready zero | t+113.758 | `0b 00 00 ea 01 …` — the briefing's closing record, with 21 s still on the count; it opens nothing |
| last departure report | t+123.774 | the room's last client leaving its screen; the round opens here |
| round's first state write | t+123.8 | `state[2] = 0xff`, the open |
| run | t+132.791 | the `0x00` run behind the open, 9 s later |

Our host follows the same two gates: `BriefingCountdownService.HasEnded` (the
closing zero) and `MatchStartRunService`'s count of the room's departures, then
`MatchRecordTimingUtils.RoundOpenDwellMilliseconds` (900 ms) and
`RoundStartRunGapMilliseconds` (1 643 ms). **A room of one therefore opens almost
at once**, because its own report is both the zero and the last departure — which
is the measured solo shape (`briefing-then-solo-game`: zero t+145.098, round
t+146.007) and not a defect. A room of several opens as soon as its last client
leaves, however quickly that happens, so a briefing that arrives with every
client already reporting is short by design. The client's wait screen ends when
the round's open writes its state, and that is the earliest this host may write
it.

The kill limit is a room setting, not a wire field; the host derives the score
from its own kills/deaths and ends the round at the clock or the ticket limit.
The client sends its own position/state and no "I killed" record, so a relay-only
host cannot decide a winner.

## 5b. The four outcomes, and the text the client draws for them

The wire carries a **side**, not a phrase. `state[2]` is the round's result and
`state[3]` the match's, in one coding: `2` is team `0` (red), `3` team `2`
(blue), `1` a round nobody won, `6` a round that ended with no result, and `255`
the value an unwritten result is left at
([`../protocol/STATE_VECTOR.md`](../protocol/STATE_VECTOR.md) §3). A host that
has no score writes nothing rather than naming a side.

The client turns that code into a banner of its own, and the text is in the
**stage scripts**, not in the PS3 image. Every gameplay stage's resource table
(`game/stage/<stage>/scenerio/resources.gcl`) declares the same block at the same
ids, and every entry is followed by its French, German, Italian and Spanish
translation — the five-language arrangement
[`rule-instruction-texts.md`](rule-instruction-texts.md) describes for the rule
panels. `n001a` declares them as:

| id | English | French | German | Italian | Spanish |
|---:|---|---|---|---|---|
| `3432` | `MISSION COMPLETE!` | `MISSION ACCOMPLIE !` | `EINSATZ BEENDET!` | `MISSIONE COMPLETATA!` | `¡MISIÓN CONSEGUIDA!` |
| `3437` | `RED TEAM WON!` | `L'ÉQUIPE ROUGE A GAGNÉ !` | `SIEGER: ROTES TEAM!` | `VINCE LA SQUADRA ROSSA!` | `¡GANA EL EQUIPO ROJO!` |
| `3442` | `BLUE TEAM WON!` | `L'ÉQUIPE BLEUE A GAGNÉ !` | `SIEGER: BLAUES TEAM!` | `VINCE LA SQUADRA BLU!` | `¡GANA EL EQUIPO AZUL!` |
| `3447` | `DRAW!` | `ÉGALITÉ` | `UNENTSCHIEDEN!` | `PAREGGIO!` | `¡EMPATE!` |
| `3452` | `RED GOAL` | `BUT ROUGE` | `ROTES TOR` | `META ROSSA` | `META ROJA` |
| `3457` | `BLUE GOAL` | `BUT BLEU` | `BLAUES TOR` | `META BLU` | `META AZUL` |
| `3462` | `SNAKE WON!` | `SNAKE A GAGNÉ !` | `SIEGER: SNAKE!` | `VINCE SNAKE!` | `¡GANA SNAKE!` |
| `3467` | `SNAKE LOST!` | `SNAKE A PERDU !` | `VERLIERER: SNAKE!` | `SNAKE PERDE!` | `¡PIERDE SNAKE!` |
| `3472` | `ROUND SET` | `DÉFINIR TOUR` | `RUNDE VORBEI` | `FINE ROUND` | `FIN DE LA RONDA` |
| `3477` | `GAME SET` | `DÉFINIR JEU` | `SPIEL VORBEI` | `FINE PARTITA` | `FIN DE LA PARTIDA` |

**The ids are the stage table's indices and are identical in every stage.** All
twenty-one gameplay stages carry the block at `3432`–`3477`; the lobby, boot and
init stages do not, because they are not stages a round plays in. The two `GOAL`
rows are Capture's and Team Sneaking's goal calls rather than results, and they
sit between the team results and Snake's for that reason.

The PC client repeats the English of five rows in its own module, `savemgo.dll`,
adjacent in `.rdata` — this is the copy a player sees on screen, in the order
`SNAKE LOST!` (`0x0046E074`), `BLUE TEAM WON!` (`0x0046E080`), `RED TEAM WON!`
(`0x0046E08F`), `SNAKE WON!` (`0x0046E09D`), `MISSION COMPLETE!` (`0x0046E0A8`)
(virtual addresses `0x1046F474`–`0x1046F4A8`, `docs/strings/strings.txt`; the
download build carries `BLUE TEAM WON!` at `0x1045B8BB` and `RED TEAM WON!` at
`0x1045B8CA`). It does not repeat `DRAW!`, so a search of the binaries alone
misses the draw [I] — the stage table is the complete set.

Read against the coding:

* **`RED TEAM WON!` and `BLUE TEAM WON!` are the side codes.** Red is the result
  `2` (team `0`), blue the result `3` (team `2`); the words are the client's and
  not another field on the wire.
* **`DRAW!` is the drawn code, `state[2] = 1`.** It is a banner of its own and not
  a missing one: the string is in every gameplay stage and simply absent from the
  PC module's copy.
* **`SNAKE WON!` and `SNAKE LOST!` are the same two side codes in Sneaking
  Mission.** The round still carries a side, and the mode names the side that
  holds Snake instead of a colour. Which team id Snake holds is **[U]**; the
  server's ladder (`MatchRecordBodiesUtils.ResultCode`, `2 + side`) writes the
  same codes for its sides. `SNAKE LOST!` is the hunters' win, not a distinct
  result.
* **`MISSION COMPLETE!` is not a side at all.** It is the objective that ends a
  round — a target delivered, a bomb detonated — drawn instead of a team, the same
  distinction the rule panels draw between a ticket win and an objective win.
* **`ROUND SET` and `GAME SET` are the end banners that go with `state[2]` and
  `state[3]`.** A round's close announces the round, a match's close the game;
  they are the text side of the two result bytes §5 lists.
* **The ids are not in the binaries.** Neither the PS3 image nor `savemgo.dll`
  holds `3437` as a literal, so the lookup is the stage resource table's own, and
  the native that picks a row is not located yet [U].

### What ends a round, by rule

The panels in [`rule-instruction-texts.md`](rule-instruction-texts.md) are the
client's own statement of every rule's ending, and they are worth having beside
the wire coding because only two of them are things a relay-only host can see:

| rule | the panel's ending |
|---|---|
| TDM | a side's tickets reach zero |
| RES | GA-KO brought to its goal, the attacking team holding it at `0`, or every enemy down |
| CAP | both targets in goals (the timer resets), or the most targets at `0` |
| SNE | Snake's dog-tag count, a set number of kills on Snake, or the most kills at `0` |
| BASE | every base held, or the most bases at `0` |
| BOMB | a planted bomb detonates, or the defenders hold and every bomb is eliminated |
| TSNE | every enemy down or a target brought home (sneaking), every enemy down or both targets kept (defending) |
| SDM | the last player standing, inside a shrinking area |
| SCAP | a target taken to the checkpoints a set number of times |
| DM / INTERVAL | score |

Only the first is implemented as a decision here (`MatchResultService`,
`MatchRuleUtils`), and the clock's own decision is this host's reading of the
score rather than a measured rule (`docs/GAMEPLAY_SERVER.md` §5 item 3). The
objective endings cannot be seen on this channel: the client sends its position
and state and no "I planted" or "I delivered" record.

## 6. Field notes from the data phase

| field | evidence |
|---|---|
| rotation triple `[rule, map, flags]` | lobby `0x4313`; flags `0x02` Drebin, `0x04` Headshots Only |
| per-rule clock | rule time in minutes × 480, from the room's seventeen-word bank |
| `0x1a50` / `0x1a5a` | item boxes / devices, four-record events plus six-byte state |
| `0x0e` | round-over announcement, byte is the reason |
| `state[2]` / `state[3]` | round and match results (the write is `02 02 01 02`; the index it names is open, §7) |
| six `0x18` bytes | the client's 24 player slots — the entry table is 24 × `0x74` bytes at `instance + 0xD4` |
| `0x0b` | briefing countdown and the reporting set |
| `0x090c` | health/stamina-shaped beat record |

The state-vector offsets that remain unnamed are listed in
[`../protocol/STATE_VECTOR.md`](../protocol/STATE_VECTOR.md) §3; this file does
not assign meanings to them.

## 7. What is still open

* The **round-end decision function** in the ELF is not yet located. The stage
  scripts do not carry a `svRoundEnd` command, and the 1.36 image has no
  `sv_round` string. The measured wire shape is known; the native that decides
  "clock or tickets" is not. What the eight functions at `0x289438`–`0x289E98`
  turn out to be is the **player-leave path**: each clears a player's pending bit
  and, when the last awaited player is gone, clears the pending verdict
  (`P2P_SetRoundOverPendingVerdict(0)`); they are score-accumulator bookkeeping
  around a departure, not the winner writer. The winner writer was searched for
  both as a literal `state[2]`/`state[3]` write and through the `0x02` record's
  `offset − 2` convention, and the only `offset 2` writes in the network module
  are the 24-byte settings header (`P2P_PublishPlayerRecord`), so the shared
  block's result write is still unlocated.
* **Random spawn selection** is not proven either way. The room entry uses the
  `A01`/`B01` pair; a stage has up to sixteen gates per side, and the supervisor or
  mode class may choose among them. This needs a `GetInitialPosDir`-style function
  trace, not a script read.
* **Item-box position selection** is likewise open for live rounds. The opening
  pair is fixed; later positions are play events. The `PRP_CBOX` stage effects are
  the input to whatever chooses them.
* The **full 237-entry registry** is not renamed: eighteen commands are, the six
  of §2, the six of §2c and the six of §2d, chosen because the stages call them
  and because a dedicated host has to answer them. The rest walk the same way with
  `commands.txt` and the `{opd, hash}` table, and §3's call counts are where the
  next tranche should start.
* The **round-result banner's row is not picked by any code located so far.** The
  stage table's ids (`3432`–`3477`) appear in no script and in neither binary as a
  literal — `3432` and `3437` are absent as 32-bit values, the HUD actor's
  constructor takes only the BGM and rank arguments, and the `RESULT` string's one
  reference is UPnP. Five of the banners are also in `savemgo.dll`, which suggests
  the PC client draws them from `state[2]` itself [I], but the picker is still
  unlocated.
* **The twelve boxes' items are not traced.** The stage places twelve `CBOX`
  props and the manager times them, but which item each box is given in a live
  round — and whether that changes from round to round — is not in the manager.
  The measured round's two items are ENVG and DrumCan; the other ten boxes, and
  the draw behind them, are the next thing a capture of a longer round would
  settle.
* **Which colour a loadout row is painted is not stated anywhere in the stages.**
  `-team N` on the three loadout commands is a row (0–14), and the same five rows
  carry a weapon list and an item window in all four stages while the other nine
  carry ammunition only (§3). That makes a row a team type — one team of one
  rule — and the rule's own table, which no stage script holds, is what decides
  whether that team is blue, red or orange. The wire's sides are `0`/`1`/`0xe0`
  in the team arrays plus `2 + side` in the result, and the only three-value
  enumeration the files hold is `COM_svGetTeamType`'s `0`/`1`/`2`.
* **Which vector index a `0x02` write names.** The builder appends at
  `[0x84] + offset` while the `0x00` block appends from `[0x84] + 2`, so an offset
  names the vector's index two lower
  ([`../protocol/STATE_VECTOR.md`](../protocol/STATE_VECTOR.md) §5,
  [`supervisor-opcode-builders.md`](supervisor-opcode-builders.md)). The result
  write's `02 02 01 02` is therefore `state[0]` by that reading and `state[2]` by
  the direct one, and the captures cannot separate them: the bytes sent are the
  same either way. Our host sends the capture's own body, and the label is the
  only thing left open.
