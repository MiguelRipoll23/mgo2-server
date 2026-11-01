# The briefing screen's state machine: what builds it, and what our client never reaches

**Measured 1 November 2026** against `system.dll` of the pc client (preferred base
`0x10000000`, `.text` at `0x10001000`), the patcher's log of the 00:32–00:33 UTC
join, and the gameplay host's own log of that same session. Every address below is
a linked (file) virtual address; the client maps the module at `0x10070000`, so a
running address is the one quoted plus `0x70000`.

The purpose of this file is to answer one question the wire comparisons could not
answer: **what makes the client put the briefing screen on the display**, and
therefore which side of the connection that trigger is on.

## 0. The earlier reading of the flow probe was wrong

The probe that has been reporting *"the battle-before flow has stepped 0 time(s)
and started 0, its unit is 00000000 at state -1"* was never installed:

```
game: the battle-before flow's step at 101D1380 does not hold the expected prologue -- not hooked
game: the battle-before flow's start at 101D0B00 does not hold the expected prologue -- not hooked
```

Both patterns begin with an instruction that carries an **absolute address**
(`mov esi, [0x31864eb0]` and `cmp dword [0x31864eb0], 0`), and `system.dll` has a
`.reloc` section, so at load time the operand is rewritten to `0x318d4eb0`. The
byte-for-byte comparison the installer makes therefore failed on the running
image, and the two counters it fed stayed at zero because nothing was counting.
The hooks that *did* report zero — the loader, the two askers, the data-set
store and drop, the screen request — have no absolute address in their prologues
and are real.

Both patterns now carry a mask over the relocated operand (`MASK_FLOW_STEP`,
`MASK_FLOW_START`), and a probe was added on the screen's builder (§2). **No
statement about the unit's existence should be taken from a log written before
this change.**

## 1. The session that was measured

| source | fact |
| --- | --- |
| patcher log | 6 stages loaded, the last `n003a`; a stage script is running, 13 score writes, 1 check |
| patcher log | the briefing loader ran **0** times; the room's asker **0**; the one-block asker **0**; the data set is **0** (installed 0, dropped 0); the flow's labels **0** |
| patcher log | the flow's screen request: installed, never fired |
| host log, t+0:32:36 | peer connected, profile `PhilDunphy23` (character 2, slot 0/1), roster answered at 0:32:38 |
| host log, 0:32:40 | the settings burst: the rotation and 2 player loadouts |
| host log, 0:32:53 | the client's join report `0x1ade`, *"reports its map-loaded state with 1"* — 17 s after the handshake |
| host log, 0:32:57 | the opening run (12 records, count held at 3360 of 3360, no round state) |
| host log, 0:33:00 | the room-entry answer: the arrays, the countdown, the player's settings header |
| host log, 0:33:20 → 0:33:43 | *"Waiting up to 2147483647 ms for a round to open"*, six more slot checks, nothing else |
| host log, 0:33:48 | the countdown ended, the client had been quiet for 30 s, reaped at 0:33:49 with 178 records acknowledged |

So the client **is** on the map with the stage script running, **does** send the
join report the captures describe as "the room's map is loaded and it is on the
briefing screen", **does** acknowledge everything, and draws no briefing. The
report is therefore not evidence of the briefing screen; it only says the room's
map is loaded.

**This is further than the stall `join-stall-after-mode2.md` §20 describes.**
There the client sat on the black screen between the fade and the briefing; here
it is inside the map's own stage: the stage log names `n003a`, the stage script
is running and setting its score table (13 writes of the 14-entry table, one of
them the overflow the devcheck prints), and it stays there until it is reaped.
The briefing is not late in this session, it is **skipped**, which is the shape
of the gate in §5 rather than of a record that arrived in the wrong order.

## 2. The chain from "the flow wants a screen" to "the loader runs"

One path, four calls, each of which the probes now guard:

```
0x10161484  case 12 of the battle-before unit's step      (the only caller of ↓)
0x10119720  the screen's factory                          (the only caller of ↓)
0x10118e60  its constructor: mov [0x3181b038], esi        (installs the data set)
0x101152a0  the loader, gated on [0x3181b038] != 0        (the only caller of ↓)
0x10115810 / 0x101183f0   the room's asker / the one-block asker
```

The factory allocates `0x3a0` bytes, fills the object's two vtables
(`0x10118560`, `0x101185f0`) and runs the constructor; the constructor is the
**only writer in the image** that stores a nonzero value at `0x3181b038` (the
drop at `0x1011874c` is the only writer of zero). The loader returns immediately
while that word is zero, and both askers are gated on it as well. The screen's
per-frame builder `0x10118a40` — the one that registers the `briefing` label and
walks the room's players — starts with `mov esi, [0x3181b038]; test esi, esi;
je out`, so it is gated on the same word.

That word being zero in every session so far means **the briefing screen has
never been constructed in this client**, and no amount of wire content can
change that: the records fill a screen that does not exist.

## 3. The battle-before unit and its thirteen states

The unit is a `0x460`-byte object whose address lives in one global,
`0x31864eb0`. Its step function is a switch on `[unit+0x60]`:

```
mov esi, [0x31864eb0]
mov eax, [esi+0x60]
cmp eax, 0xc            ; above twelve returns
jmp [eax*4 + 0x101614ac]
```

| state | handler | what it does |
| --- | --- | --- |
| 0 | `0x1016139b` | `0x10160970`: checks `0x10037ac0() == 1`, then stages a label pair, then **state 5** |
| 1 | `0x101613a2` | **state 2** |
| 2 | `0x101613ac` | `0x10161300`: requires screens `0x42` and `0x3d` to exist (asserts at `ac_lb_main.cc:501/503` otherwise), then **state 5** |
| 3 | `0x101613b3` | **state 4** |
| 4 | `0x101613bd` | `0x10101970`, `0x10051580`, `0x10160d70(0)`, then **state 5** |
| 5 | `0x101614a8` | waits |
| 6 | `0x101613e6` | `0x10160a00`: locks a shared object (`0x102de160`) into `+0x440/+0x444`, then **state 7** |
| 7 | `0x101613f2` | if that lock no longer matches the weak slot at `+0x448`, **state 8** |
| 8 | `0x10161430` | `0x102de240`, then **state 0** |
| 9 | `0x101614a8` | waits |
| 10 | `0x10161445` | `0x10160a60`: locks another object (`0x102de5b0`) into `+0x450/+0x454`, then **state 11** |
| 11 | `0x10161451` | when that lock has expired, **state 12** |
| 12 | `0x10161484` | **builds the briefing screen** (§2), then **state 6** |

The module is named by its own asserts: `0x10fcd5a8` is
`.\online\lobby\ac_lb_main.cc`, `0x10fc505c` is
`..\game_flow\MGA_BattleBefor\MGA_BattleBefor_Briefing.cpp`. States 6–8 are one
"wait for an object to be destroyed, then go round again" helper and 10–11 is a
second one, so the briefing is entered on the first pass or after one wait —
either way through case 12.

## 4. What creates the unit, and what it does first

The unit's factory is `0x101614e0`. **Nothing calls it directly**: its address
appears once in the image, as a function pointer in a table in `.data`
(`0x110de174`, an 8-byte `{function, hash}` record among neighbours
`0x101615c0`, `0x1014b300`, `0x1010f420` …). The unit is therefore instantiated
**by name** through that registry, which is the shape of a data-driven screen
list rather than of a call somebody wrote.

Its start (`0x10160b00`) refuses a second unit while `[0x31864eb0]` is set,
allocates `0x2c988` bytes, stores the unit and a 64-bit token in
`0x31864eb0/eb8/ebc`, and then picks the first state at `0x10160cff`:

```
call 0x10037420        ; root/game_data/tatical_order/is_active
neg al / sbb eax,eax / and eax,-2 / add eax,0xc
mov [ecx+0x60], eax    ; 10 when a tactical order is active, 12 otherwise
```

So a created unit reaches case 12 on its first step unless an object is still
alive to wait for. **The only way the briefing does not appear once the unit
exists is the gate inside case 12**, next.

## 5. The gate in case 12: `select_game_mode == 9`

```
0x10161484  push 0
0x10161486  call 0x10037660        ; the app singleton
0x1016148b  mov ecx, eax
0x1016148d  call 0x10031e40        ; get root/game_data/system_category/select_game_mode
0x10161492  cmp al, 9
0x10161494  je  0x1016149b         ; ← the briefing screen is not built
0x10161496  call 0x10119720        ; ← the briefing screen is built
0x1016149b  mov ecx, [0x31864eb0]
0x101614a1  mov dword [ecx+0x60], 6
```

* `0x10031e40` is the getter for the config key
  `root/game_data/system_category/select_game_mode` (read out of `.rdata` at
  `0x1059e4a8`); `0x10031e90` is its setter.
* The setter is called from four places: `0x1010a157` (a screen saving and
  restoring the value around itself), `0x101cec30` (**hardcoded `9`** on one
  branch), `0x10204820` (the `MGA_BattleBefor_Customize` destructor restoring the
  room's mode — it also registers the labels `customize` and `customize-time`
  and rewrites the camera/radio options), and `0x10338e6c` (from a byte of the
  object at `+0x7c`).
* `select_game_mode` is the mode of the match the client believes it is in, and
  it is **a different id space from `lobby_game_types`** (Free Battle `1`,
  Automatching `2`, Tournament `3`, Survival `4`, hosts `5`/`6`, Basic Training
  `7`, Combat Training `8`, Tournament registration `10` — no row carries `9`).
  Which of the client's own mode enumerations reaches `9` is not established
  here; what is established is that the value is read at this one branch and that
  `9` means "no briefing".

## 6. The flow's labels, and the two registrars

The six labels sit together in `.rdata`:

| label | address |
| --- | --- |
| `matching_after` | `0x10fc55f0` |
| `Mission_Rule_Loading` | `0x10fc5600` |
| `briefing-matching` | `0x10fc5618` |
| `matching` | `0x10fc562c` |
| `briefing` | `0x10fc5638` |

Two functions register them: `0x10022370(label)` and `0x100222a0(label, number)`.
The briefing screen's builder uses both — `0x10118c20` pushes `briefing` into
`0x10022370`, and `0x10118c39` pushes `matching` and a count into `0x100222a0`.
The patcher's label probe watches `0x100222a0` **only**, so a zero count from it
means "the screens that register a *pair* never ran", not "no screen was
entered". `0x10022370` is not probed at all.

The same builder is where the two branches the captures hinted at live:

* id `1` (`0x10118be5`) walks the room's players — `0x10115890`, the path filler
  `0x10118880` (which is where `root/matching/briefing/player_data/team_data%d/…`
  is composed), `0x10117cc0`, `0x10118150`, `0x10118080`, `0x10117fb0` — then
  registers `briefing` and `matching`.
* id `10` (`0x10118a82`) registers `Mission_Rule_Loading` when
  `0x10037ac0() == 5` and `briefing-matching` otherwise.
* any other id (`0x10118c4e`) writes the id into `root/matching/…` and, for
  `1`/`10`, a page word.

## 7. Where the briefing's own UI text comes from

Not from the image. `Are you ready to go into battle?`, `BRIEFING`, `GAME START`,
`READY` and the rule blurbs are strings inside each map stage's
`game/stage/<map>/scenerio.gcx` (`n003a`: `0x471bd`, `0x4f290`, `0x46a17`,
`0x469f5`). That is why the briefing's background is the map and why the screen
is drawn over a loaded stage: the stage supplies the briefing's own UI, and the
`MGA_BattleBefor` screen supplies the room's data.

## 8. What this means for the server

* The briefing screen is created **inside the client, by the client's own
  battle-before unit**, before or as it opens the game's UDP session. The
  gameplay channel's records (the run, the publication, the countdown, the item
  boxes) cannot create it; they only fill it once it exists. Every wire
  comparison in `briefing-run-byte-comparison.md` was therefore measuring the
  contents of a screen that has never been on screen.
* A client that is on the briefing when it joins the host — the shape every
  reference capture shows, the report arriving 0.2–0.5 s after the handshake —
  has already created the screen. Ours joins, reports 17 s later, runs the stage,
  and never creates it, which is exactly the black screen between the fade and
  the briefing.
* The trigger, therefore, is upstream of the UDP port: either the client's own
  flow never instantiates the unit (its registry entry is name-driven), or the
  unit is instantiated and case 12 takes the `select_game_mode == 9` branch.

## 9. What the rebuilt probe measured

With the masks in place both flow hooks install and the builder is probed, and
the answer did not change:

```
game: hooked the battle-before flow's step at 101D1380, 7 bytes into trampoline 060E0000
game: hooked the battle-before flow's start at 101D0B00, 8 bytes into trampoline 060F0000
game: hooked the briefing screen's builder at 10189720, 6 bytes into trampoline 06250000
…
state: 6 stage(s) loaded, the last "n004a"; a stage script is running …
  the briefing loader has run 0 time(s); the room asked 0 time(s) and the one-block ask 0;
  the data set is 0 (installed 0, dropped 0); the flow's last screen was "none" (0 label(s));
  the battle-before flow has stepped 0 time(s) and started 0, its unit is 00000000 at state -1
```

No `battle-before step`, no `the battle-before unit was started`, no `the briefing
screen was built`, and no `the flow asked for screen` — with every one of those
hooks confirmed installed. The client walked `init_n` → `nt_mgsetup` →
`r_sna01_n` → `mga_boot` → `lobby` → `n004a` and ran the map's script, and the
whole briefing module stayed untouched.

**The missing trigger is therefore upstream of the unit**: the client's room flow
never instantiates the `MGA_BattleBefor` screen at all. Whether it asks for a
different screen by name, or never reaches the flow that asks, is what the two
screen-label registrars (`0x100222a0`, `0x10022370`) now record — every label
either of them is handed is logged as `screen label "…"`.

## 10. The next measurement

One more join in a started Free Battle room, with the client launched through
`patcher\mgo2-patcher.exe`. The lines that matter are the `screen label` ones:
they are the client's own list of the screens it entered, and the absent one says
where the flow turns off.

The rebuilt patcher (masks on the two flow patterns, plus a probe on
`0x10119720`) answers the two remaining cases apart in one run:

| what the log shows | what it means |
| --- | --- |
| `the battle-before unit was started` | the unit exists; the step lines then say whether state 12 is reached |
| step lines reaching state 12 | the case runs; `the briefing screen was built` says the gate passed, and a `select_game_mode` of `9` says it did not |
| no `started` line at all | the unit is never instantiated: the flow that looks the screen up by name never asks for it, and the missing trigger is in that flow — not in anything the gameplay host writes |
