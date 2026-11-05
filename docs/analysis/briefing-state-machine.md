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

The unit's factory is `0x101614e0`, and **nothing in the image calls it**: a
scan of every section finds its address exactly once, in a 327-record table at
`0x110dda4c` (`0x110de174` is its record, index 229). That table is *not* a
factory registry, which an earlier reading of this file claimed: it is a
**function → name-hash** map used for debug text only. `0x10097670` binary-
searches it by the function pointer (`cmp [ebx+eax*8], edi` … `mov eax, [eax+4]`)
and `0x100976b0` returns the record's second word, which is what an assert at
`0x105c08c8` prints when a function has no name. The value at `0x110de174+4`,
`0xc53fa0`, is therefore the hash of the factory's own name, not a lookup key.

So the factory has no static caller at all: it is reached either through a
pointer the client builds at run time, or from code that this scan does not see.
**That is the open question of this file** — the step, the start and the builder
are all identified, and everything that would run after them is understood, but
the one edge into the unit is still missing.

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

Two functions are used for them: `0x10022370(label)` and `0x100222a0(label,
number)`. The briefing screen's builder uses both — `0x10118c20` pushes
`briefing` into `0x10022370`, and `0x10118c39` pushes `matching` and a count into
`0x100222a0`.

**Neither is a screen registrar.** The patcher was extended to probe both and
log every string they are handed; a whole session produced exactly one line:

```
game: screen label "COMcsl" by 0x22370 (call 1)
```

`COMcsl` is a script command name, so `0x10022370` belongs to the script engine's
name table, and `0x100222a0` was not called at all. What the two do with a match
label is therefore unmeasured, and the "0 label(s)" the patcher reports is not
evidence about which screens ran.

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
  battle-before unit**, *before* the game's UDP session exists. That is measured,
  not inferred: in `mgo2-game-started-on-briefing` the client's entry echo at
  t+55.312 is the session's first datagram, the host answers it with **nothing at
  all** until its run at t+58.255, and the client is on the briefing throughout.
  So no gameplay record can be the screen's trigger, and every wire comparison in
  `briefing-run-byte-comparison.md` was measuring the contents of a screen that
  has never been on screen.
* A client that is on the briefing when it joins the host — the shape every
  reference capture shows, the report arriving 0.2–0.5 s after the handshake —
  has already created the screen. Ours joins, reports 17 s later, runs the stage,
  and never creates it, which is exactly the black screen between the fade and
  the briefing.
* The trigger, therefore, is upstream of the UDP port: the client's own flow
  never instantiates the unit, and the one edge into the unit is not yet located
  (§4). Nothing the gameplay host writes can supply that edge, and nothing it
  writes can be blamed for the screen's absence until it is.

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

The next session (map `n005a`) repeated it, and it is the session that retires
the label probe: the only `screen label` line the whole run produced was
`"COMcsl"` from `0x10022370`, so that function belongs to the script engine and
not to the screen flow (§6). The same run also flooded `h_mgo2_collision`'s
`NAN ERROR: FROM … TO …` on line 402, which is the unmeasured-spawn symptom
`docs/analysis/join-stall-after-mode2.md` §71 already records for a map whose
placement was never captured — a second, independent fault, not the briefing's.

**The missing trigger is therefore upstream of the unit**: the client's room flow
never instantiates the `MGA_BattleBefor` screen at all.

**A dedicated host is not the difference.** An earlier revision of this file
suggested it was, on the reading that every briefing capture came from a room
hosted by a player's client. The endpoints say otherwise: the two captures the
briefing is measured from are hosted at `143.47.227.126:5720` and
`64.181.216.250:5720`, both cloud addresses (Azure and AWS), and
`mgo2-game-started-on-briefing.pcapng` — six players reporting into a briefing —
is one of them. A dedicated host shows the briefing.

What is left, measured rather than guessed:

* **The session's shape differs at its start.** In that capture the client sends
  its join report **0.4 s** after the handshake, because the map and the briefing
  are already up; here the report comes **17 s** after the handshake, after the
  map stage has loaded, and no briefing is built. The reference client therefore
  opens the game's socket *from* the briefing screen, while this one opens it from
  the room and loads the map afterwards.
* **What decides that is the room channel, and it has never been captured.** The
  lobby's TCP answers (`0x4313` details, the `0x4321` join answer, and whatever a
  host broadcasts when a game starts) are what the client holds when it decides
  between "go to the briefing" and "go to the map". Every capture in this
  repository is the gameplay UDP channel alone; not one holds the room channel of
  a join that showed a briefing.

## 10. The next measurement

**A capture of the room channel.** The gameplay channel has been compared to
exhaustion and cannot create the screen; the client's decision is made with the
lobby's answers in hand, and those have never been recorded. What is wanted is
the TCP session on the lobby's game port (`5730`/`5733` in every capture here)
covering one join of a room whose host is about to start a round, from a client
that then shows a briefing — one file, both directions, the same ten seconds our
own sessions are logged for.

Two things it would settle at a glance: whether the client is told the room's
phase before it opens the game socket, and whether the free-battle room we
publish states a phase of its own at all.

The rebuilt patcher (masks on the two flow patterns, plus a probe on
`0x10119720`) answers the two remaining cases apart in one run:

| what the log shows | what it means |
| --- | --- |
| `the battle-before unit was started` | the unit exists; the step lines then say whether state 12 is reached |
| step lines reaching state 12 | the case runs; `the briefing screen was built` says the gate passed, and a `select_game_mode` of `9` says it did not |
| no `started` line at all | the unit is never instantiated: the flow that looks the screen up by name never asks for it, and the missing trigger is in that flow — not in anything the gameplay host writes |

## 11. What the join's own frame was missing

Re-reading `mgo2-game-started-on-briefing` from its handshake (the whole join,
rather than the briefing that follows it) settles one thing the gameplay wire was
doing differently, and it is a record the answer never carried.

**The reference host's roster answer is five records, not four.** At t+6.004 it
writes, in one frame under `0x1001`:

```
ord 0   host's own entry        57 B
ord 1   the joiner's entry      67 B
ord 2   the roster close         7 B   07 00 00 00 00 00 03
ord 3   the room's count        10 B   0b 00 00 04 00 00 00 00 00 02
ord 4   the peer's settings     54 B   0b 02 02 00 …
```

and it writes **the same 54-byte body again** in the burst 2.1 s later
(t+8.125, `0x1001`/54 B with the host's `0xd001` control ahead of it). This host
wrote the first four and left the fifth to the burst alone, so the join's answer
described the room — its population, its close — and never the client it was
addressed to. The burst's ordinals followed from the same gap: it started at 4
where the reference starts at 5, and its own records read 4, 5, 6 against the
reference's 5, 6, 7.

That is now written where the reference writes it
(`RoomRosterFramesUtils.BuildRun`, `PlayerSettingsBurstService.AnsweringLoadoutBody`),
and the exchange's counter is seeded one value further on so the burst continues
the run rather than repeating a value it already used.

Two neighbouring differences were checked and left alone, because both are
ordinary and neither is a gap:

* **The keep-alives.** The reference's frame opens on an empty `0x5000` and two
  `0x0261` one-byte records before the head; ours writes the head first. The same
  capture's other frames interleave those at random, and they carry no state.
* **Compression.** This capture's host compresses nothing and `mgo2-game1`'s
  compresses most of what it writes, so the mode is the host's choice and the
  peer answers both.

Whether the missing payload is what the client's flow reads before it builds the
`MGA_BattleBefor` unit is **not** measured. It is the one record of the join that
was demonstrably absent, and the fix puts it where the only host that shows a
briefing puts it.

**And it was not the trigger.** The join that ran on the build carrying this fix
was answered with the five-record run — the live pod log's frame 2 reads
`5001/0, 1001/80, 1001/81, 1001/7, 1001/10, 1001/50` at ordinals 0…4, and its
burst opens on the rotation at ordinal 5 — and the client's own log from that
same run still says `the battle-before flow has stepped 0 time(s) and started 0`
beside a running `n006a` script. So the record was genuinely missing and is now
written where the reference writes it, and the screen is still built by nothing.

What that leaves is measured too: during a briefing a reference client receives
only record families this host also writes — the keep-alives, the `0x1001`
payloads, the room's count, the seven-byte countdown, the `1250`/`125a` state
pairs, the join report and its publication, the `0x0002` blobs and the `d001`
controls. The `0x08xx` tick family does not start until the round plays
(t+290.649 in `mgo2-game-started-on-briefing`), so it is not a briefing input.
Nothing in the briefing window is missing on the wire any more.

## 12. The unit has no reachable creator in this build, and the gate is the tutorial's

**Measured 4 November 2026** by disassembling `system.dll` itself
(`pefile` + `capstone`, preferred base `0x10000000`, `.text` at `0x10001000`,
5 853 054 bytes of code). This section corrects the expectation in §4, §5 and §10
that a rebuilt probe could answer the case; it does not change §8's conclusion,
it strengthens it.

**The factory is never called and never pointed at.** `0x101614e0` — the battle-before
unit's factory, the one that also contains the only call to its start `0x10160b00`
at `0x10161551` — has **zero** `E8 rel32` call sites in the whole of `.text`. Its
address occurs exactly once in the file, as the first half of entry 229 of the
table at `0x110dda4c`–`0x110de484` (327 records of eight bytes: a code pointer and a
name hash):

```
225  0x110de154 -> 0x10161980   hash 0x00c43019
228  0x110de16c -> 0x101615c0   hash 0x00c4bf5a
229  0x110de174 -> 0x101614e0   hash 0x00c53fa0     <- the unit's factory
232  0x110de18c -> 0x10160880   hash 0x00c77991
```

No instruction addresses the table or that entry as an operand: the base and the
count are a pair of runtime globals, `0x316c39a4` and `0x316c39a8`, and the only
readers are the name helpers — `0x10097670`, a binary search by code pointer
(`ecx` count, `ebx` table, `edi` key), and `0x100976b0`, which calls it with those
globals and returns `[record+4]`, the **name hash**, or asserts at `0x105c08c8`
line `0xc0`. So the only thing anything in the image does with the factory's
address is look up its debug name. Its neighbours in the same table (228, 232) are
uncalled the same way, which makes this a retained table of the whole module and
not a hole around one function.

**The unit's step is virtual, and the object it steps is the factory's.**
`0x10161380` (the state switch of §3) has zero direct callers too; its address
occurs only inside `.text`, in a table of code addresses — a class table, so the
object is dispatched through it, and the object exists only if the factory ran.
The briefing screen's own factory `0x10119720` has **one** caller, `0x10161496`,
which is case 12 of that same unreachable step.

**So the two probe counters cannot move on this build.** `started 0` and
`stepped 0` in §9 are not a host's failure and not a stall: with no static
reference to the factory and a name table that only reads it, no path in this
image instantiates the unit, whatever a server sends. The instrument was measuring
a function this build never runs, which is why two rebuilt probes changed nothing.

**The gate's one hardcoded `9` is the tutorial's, not a room's.** The branch at
`0x101cec16` writes `select_game_mode = 9` only when `0x10344130` returns non-zero
and not `2`, and that query is:

```
0x10344130  mov eax, [0x3187726c]      ; the tutorial object, or null
            test eax, eax / je -> 0
            cmp dword [eax+0x60], 1 / jne -> 0
            mov eax, [eax+0x68]       ; 1 or 2, from tuterYN
```

and its object names itself: `root/tuter/toFlash/timeout` (`0x10ffcba0`),
`root/tuter/toGame/tuterYN` (`0x10ffcb84`), the label `tutorial_entry2011`
(`0x10ffcb60`), whose destructor stores zero back into `0x3187726c`
(`0x10344229`). The three other setters of the mode do not invent a value either:
`0x1010a157` reads it and writes it back around a screen, `0x10204820` restores the
value saved before `MGA_BattleBefor_Customize`, and `0x10338e6c` copies
`[room_object+0x7c]`. **Nothing on the room channel writes `select_game_mode`**, so
§5's gate is open for an ordinary join and was never the blocker.

**What that leaves, and it is a different screen than §2 assumed.** The briefing
the reference captures show is drawn over a loaded map, its text comes from the
map's own `scenerio.gcx` (§7), and this build cannot build the `MGA_BattleBefor`
screen at all — so the screen on display is the **stage's** briefing, drawn by the
stage script the patcher already watched running (`n003a`, `n004a`, score table
writes, no briefing). The conditions, in the order they must hold:

| # | condition | state |
| --- | --- | --- |
| 1 | the client joins from the lobby's room channel, not from a map | met — the join answer is what opens the socket |
| 2 | the room's map stage loads and its script runs | **met** — the patcher names the stage and the script's writes |
| 3 | `select_game_mode != 9` | met — only the tutorial writes `9` |
| 4 | the stage script enters its briefing proc | **not met**, and no gameplay record is an input to it |
| 5 | the client's per-team, per-player room data is filled for that proc | [U] — the only filler measured is inside the unreachable screen builder (`0x10118880`) |

The next thing to read is therefore the stage script of the map the room plays —
`stage/<map>/scenerio.gcx`, which is where `BRIEFING`, `GAME START`, `READY` and
the rule blurbs live — and the gate inside its briefing proc. `n003a`'s file is
**not** among the five stages extracted in this tree (`init_n`, `lobby`,
`mga_boot`, `nt_mgsetup`, `r_sna01_n`); the download manifest names it at
`stage/n003a/scenerio.gcx`, 1 489 521 bytes, entry `0x03d4d`, so it can be
obtained and decompiled with the same `gcx.js` recipe used for the lobby script.
Until then the honest answer to "what stops the screen" is: **not the gameplay
host** — measured three times now — and in this build not the battle-before unit
either, because it does not exist here.

## 13. No script draws the briefing; the stage supplies its strings and its map

**Measured 4 November 2026** against the one stage script the client ships decompiled
(`game/stage/lobby/scenerio.gcx`: 2 821 lines, 26 procs, 33 000 resources), the
client's `game/afp` UI definitions, and `docs/strings/strings.txt` (142 114 runs).

The screen photographed on the working side shows `BRIEFING` as a tab, `GAME START`
beneath the tab strip, the panels `RULE Sneaking Mission`, `MAP Urban Ultimatum`,
`ROUND 02`, the column headers `PLAYER NAME` and `LEVEL`, a row highlighted
`START READY`, and a right-hand column of `OK»`.

**Every one of its labels is a resource of the stage table, and the script never
reads one.** In the lobby table: `GAME START` is 2 808, `RULE` 14 904, `MAP` 14 757,
`LEVEL` 14 864 and again 23 454, `ROUND` 17 155 and again 23 520, `START` 14 923 and
again 24 050, `PLAYER` 14 814, and the two values in the right panel are there as
well — `Sneaking Mission` 3 386, `Urban Ultimatum` 3 687. The doubled `LEVEL`/`ROUND`/
`START` are the room screen's set and the briefing's own. The script does not touch
them: `strres` appears **0** times in its 2 821 lines, and its whole command
vocabulary is the ten calls its own decompile lists — camera, light, fog, sound, two
colours and one variable move. **A stage script cannot raise a
screen.**

**`BRIEFING` is not a string at all.** Zero runs in `docs/strings/strings.txt` are
exactly `BRIEFING`, and the stage table carries none either, so the tab is drawn and
not spelled. The screen's chrome is the asset pair `game/afp/afp_battle-start.afp`
(3 767 184 B) with `battle_start.xml`, whose own text carries `YOUR TEAM` and
`RIVAL TEAM` — the two panels a briefing roster shows. **[I]** that this pair is the
screen's own chrome: what is measured is that it is the only UI asset in the client
whose text names two teams, and that no `briefing`-named UI file exists.

**The rows are the HUD unit's.** `.\online\hud_mgo2\h_mgo2_briefing.cc` (0x10FEFFB0)
sits in `.rdata` beside `%s - %s` (0x10FEFFD8) and the row assert `p_info != NULL`
(0x10FEFFE0), and the unit owns a second key set written with the team and player
**literal**: `root/matching/briefing/player_data/team_data0/player_data0/{user_name,
user_class,user_type,shop,areacode,medal_data%d}` at 0x10FC5900–0x10FC5A60, beside
the `%d` form at 0x10FC45E0 that §7 of the other file tabulates.

**So the conditions are native and there are four of them:**

| # | condition | state | who owns it |
| --- | --- | --- | --- |
| 1 | the room's map stage is loaded and its script runs | **met** on our join (`n007a`) | the room's map |
| 2 | the room's rule, map and round are in hand | met — they arrive on the lobby channel, and the reference client holds them before it opens the game socket | the lobby's `0x4313`/`0x4321` |
| 3 | the HUD's per-player rows are filled (`p_info != NULL`) | **[U]** — the only filler in the image is the path filler `0x10118880`, inside the builder §12 shows nothing calls | `MGA_BattleBefor_Briefing.cpp` |
| 4 | the HUD unit is activated | **[U]** — no script, no key and no record in any capture gates it | `h_mgo2_briefing.cc` |

**What the photograph adds that no capture could: which stage it is drawn over.**
The room shows `MAP Urban Ultimatum`, `ROUND 02`, `RULE Sneaking Mission`, and the
repository's own map table names it — `.gametools/replay-console/rpdt.js` `MAPS`
reads `3: ["n003a", "Urban Ultimatum"]` — so the briefing on display is drawn over
stage **`n003a`**, the same script whose briefing strings its own decompile holds at
`0x471bd`, `0x4f290` and `0x46a17`. The join that did not brief ran **`n007a`**
instead — the patcher log's sixth stage, `7: ["n007a", "Blood Bath"]` in that same
table, its textures `map_n007a_1F_alp`, `map_n007a_B1F_alp` and `map_n007a_grid_alp`
at `0x10FF027B`–`0x10FF02BF`. So the two
sessions are on two different maps, and the screen is not a property of one stage
directory: what was compared is the same native HUD over two maps, one briefing, one
not.

**This retires the last script-shaped hypothesis.** The next thing to read is no
longer a stage proc — the scripts carry no briefing logic at all — but the activation
of `h_mgo2_briefing.cc` and the `MGA_BattleBefor_Briefing.cpp` code that fills its rows.
Both are native, both are in `system.dll`, and the only path into the second is the
factory §12 shows that nothing in the image calls.

## 14. The two logs cannot separate the clients, and the working room channel is decoded

Both patcher logs are in hand: `briefing/myserver/mgo2-patcher.log` (7 777 B) and
`briefing/working server/working-server-log.log` (30 228 B), from the same probe set.

**Their probed state is the same sentence, over and over, on both sides.** Our log's
final summary and every 30-second summary of the working log read alike: *the briefing
loader has run 0 time(s) — never … the room asked 0 time(s) and the one-block ask 0;
the data set is 0 (installed 0, dropped 0); the flow's last screen was "none" (0
label(s)); the battle-before flow has stepped 0 time(s) and started 0, its unit is
00000000 at state -1*. The working log says it across **17 stages** (`init_n` …
`n007a`) and several played rounds, in a session whose client demonstrably briefed
(its `1d` rows are in the same folder's capture at t+512.883).

So those zeros are not a host's fault and not a stall: they are a measurement of
functions this build never runs (§12), now confirmed on a client that **did** brief.
Any conclusion resting on those counters is void in both directions, including the
reading that our client's `0` is the defect.

**The working log's stage order says where the briefing lives.** It runs
`init_n`, `nt_mgsetup`, `r_sna01_n`, `mga_boot`, `lobby`, `n006a`, then back to
`lobby`, then `n012a`, `lobby`, `r_sneak_n`, `n015a`, `r_sna01_n`, `lobby`, `n007a` — the
client alternates between the lobby stage and a map. The room's briefing belongs to that
lobby stretch: it is the room screen, drawn while `lobby` is the loaded stage, and the
map follows when the round opens. Our log runs `lobby` → `n007a` and never returns to
`lobby` inside the capture.

**What the working room channel says.** `briefing-then-solo-game.pcapng` is the only
file here that holds the lobby's TCP (`10.2.0.2:61542 ↔ 15.204.239.231:5733`), and it
decodes:

| record | value |
| --- | --- |
| `0x4312` request | game id 381 523 |
| `0x4313` reply, 877 B | `MOUSE ROOM`, rotation `(5,12,0) (6,4,0) (3,5,0) (16,2,0)`, max 17 / current 1, **briefing 1 minute**, level-limit 22, per-rule `5 2 5 2 5 2 5 2 50 5 30 5 2 5 2 5 2`, ping 46, dedicated 0, subtype 1, 18 roster entries |
| `0x4320` join → `0x4321` | public `64.181.216.250:5720`, private `10.0.0.171:5720`, gate 0, rule 3, map 7 |

**Our own reply still cannot be compared**: `myserver.pcapng`'s TCP is the internet
only (`199.232.34.172:80`, `23.14.137.196:443`) and holds no `57xx` frame at all. The
comparison §5 and §10 have asked for now has a target — these field values — but not
yet a result.

**The HUD's trigger is lobby-side.** The briefing HUD's code and strings sit at
`0x102bbc..` and `0x102bd..` (`h_mgo2_briefing.cc`, `%s - %s`, `p_info != NULL`), and
the only call sites into that region are `0x10152421`, `0x101525dd`, `0x101547c7`,
`0x101b53c7` and `0x101b70c3` — all in the lobby's own code, none in the map's. Its row
keys are pushed at `0x10119251` (`team_data0/player_data0`), inside the battle-before
builder. So the screen is raised by the client's lobby code and the map plays no part
in raising it.

**Root cause, as far as these four files carry it.** Both clients are the same build,
both load a map stage and run its script, and only the reference one stays on the
room's briefing long enough to publish its `1d` rows and repeat its `e1` report; ours
reports once, never readies, and is on `n007a` 9.9 s after its socket opens. Nothing on
the gameplay channel gates that, the probes cannot see it, and the one channel that can
is the lobby's room channel — whose working values are now known field for field and
whose values on our side have never been captured. The channel was captured afterwards
and the defect is §15.

## 15. The root cause: the details reply stated the lobby's database key as the room's game type

Our lobby logs every full frame at DBG, so the reply §14 lacked came out of the pod
(`OUT 0x4313 (424 bytes): 43130190 …`), decoded against the working capture's 877-byte
reply with the same byte offsets:

| field | ours, room 309 (`server-5731`) | reference, room 381 523 (`MOUSE ROOM`) |
| --- | --- | --- |
| briefing minutes `@0x0ec` | **1** | **1** |
| level limit `@0x108` | **22** | **22** |
| per-rule bank | `5 2 5 2 5 2 5 2 50 5 30 5 2 5 2 5 2` | identical, word for word |
| rotation | nine rounds, `(5,12,0) (6,4,0) (3,5,0) (16,2,0) …` | four rounds, the same first four |
| password `@0x098` | 0 | 0 |
| dedicated `@0x099` | 1 | 0 |
| **lobby subtype `@0x09a`** | **10** | **1** |
| max / current players | 8 / 1 | 17 / 1 |
| ping | 100 | 46 |
| roster entries | 1 | 18 |

Everything the round's *play* depends on already agreed: the briefing time, the level
limit, the per-rule bank and the rotation's first four rounds. The one field that was
wrong **in kind** is the subtype. `RoomBrowserHandlers.GetGameDetailsHandler` wrote
`session.LobbyIdentifier` — the lobby row's primary key, `10` for the free-battle
lobby — into a byte that carries a `LobbySubtypeConstants` value. `10` was not even
unrecognised: it is `TournamentRegistration`, the game type whose flow registers teams
and plays no round, so the client was told the room it was joining was a registration
room that has nothing to brief.

The room row held the right answer throughout: `games` row 309 stores `lobby_id = 10`
**and** `lobby_subtype = 1`, the latter written by `MatchService` as
`room.LobbySubtype = lobby.SubtypeIdentifier`. Only the writer read the wrong column.
It now writes `game.LobbySubtype`, which is the free-battle `1` the reference states.

The differences left are properties of the room rather than defects: a dedicated host
occupies no playing slot (§7's `0x099`), its ping is its own reported figure, its size
is its own setting, and its nine rounds are its own rotation. This change does not
prove that a client whose subtype is right will brief while `dedicated` is 1 — that is
one join away — but it removes a field that said the room had no game to play.

**Checks run.** `dotnet build src/GameLobbyServer/Mgo2Server.GameLobbyServer.csproj`:
0 warnings, 0 errors. The database read that the fix rests on:
`select g.id, g.name, g.lobby_id, g.lobby_subtype, l.subtype_id from games g join
lobbies l on l.id = g.lobby_id` → `309 | server-5731 | 10 | 1 | 1`. No protocol codec
changed, so the suite is untouched.
