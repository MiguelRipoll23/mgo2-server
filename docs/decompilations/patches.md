# PATCHES.md — What the MGO2PC patches do (functional level)

*What the MGO2 plugin (`plugin.sprx`, module `MGOPlugin`) changes in the game
(`MGO2.ELF`, the MGO2PC EBOOT for `NPMG00020`), and what each change does to the game at
runtime. Companion docs: [`code-injection.md`](../code-injection.md) (the *mechanism*) and
[`code-injection-fingerprint.md`](../code-injection-fingerprint.md) (what the components read/transmit).*

Every claim below is anchored in machine-verified facts: the original instruction at each patched
address (read from the untouched ELF), the replacement word (read from the plugin), the
containing function (Ghidra decompiles in `mgo2_decomp/static_fn/`), and — for this rewrite —
the *surrounding control flow* of each patched function (its decompile), so the "what it does"
column describes behavior, not just byte changes.

Confidence tags: **[V]** verified (instruction/control-flow level) · **[I]** interpretation
(reasoned from code) · **[U]** unresolved statically.

---

## 1. What the patch set does, in one paragraph

The plugin makes the stock MGO2 executable behave like the MGO2PC client **without rebuilding
it**. Functionally it does five jobs:

1. **Lets the game pass its own online/security gates** so it boots into the online flow and
   accepts the MGO2PC login (stub flip, forced-success calls, the auth-token hook, version
   handshake hook, and disabling an XOR obfuscation the revival stack doesn't use).
2. **Lets custom content ride the stock loading pipeline** — custom maps, rules, fonts, skins,
   resources and replays are fed through the game's own asset/UI loaders by intercepting those
   loaders (60 call-site hooks) and adjusting how the stock resource code stores/discards data
   (a handful of NOPs in the resource-loading functions).
3. **Raises the game's internal ceilings** so the enlarged content fits: index bounds
   (`≤31 → ≤255`), sizes and counts (`30→80`, `96/128/192→256/256/512`, region sizes
   `2288→8192`, arena guards `511→4095`), plus relocating a field inside the game's big
   object-pool structures because the pool layout grew.
4. **Disables stock behavior the revival replaces**: whole routines are truncated (`blr`), per-item
   loops in object constructors are gutted (NOPs), state writes and diagnostics are removed.
5. **Retargets a handful of stock tables/pointers** to the plugin's own equivalents.

Sections 2–8 explain each job and its runtime effect. Section 9 is the complete per-address
technical catalog.

---

## 2. The online / auth gate (job 1)

### 2.1 The `0x13258` stub flip — "one blocked gate is told to succeed" [V]

- `0x13258` is an 8-byte stub: `li r3,-1 ; blr` (a "return **-1**" function). The game has a
  whole family of identical stubs (0x13238, 0x13258, 0x13260, 0x13268 …) and they are **not
  called directly** — they sit in a function-index table at `0x11e74e0…` (`{address,
  0x1222a00}` pairs; the same table style that lists the game's boot/init functions near
  `0x11e7358`). Callers reach them through pointers.
- The plugin writes `li r3,1` over **0x13258 only** (its siblings stay `-1`), then calls the
  patched stub through a saved pointer and confirms it returns 1.
- **Runtime effect:** whatever game flow consults that one registered function now gets
  "success" instead of "not implemented/denied". Because it is a *pointer-table* entry, the
  effect is global for that entry — one specific gate opens while its identical siblings remain
  closed (so the plugin is surgical, not blanket). **[I]** This sits right before/along the
  plugin's auth-token hook, so it reads as "the boot/online initializer the game calls is told
  it succeeded, so login proceeds" (see code-injection-fingerprint.md for what the login then sends).

### 2.2 The dispatch-thunk rewrite at `0xfbcfec` — one "external call" becomes a no-op success [V]

- `0xfbcfec` is the *tail* of a TOC-indirect call stub (the game's `0xfbcxxx–0xfbdxxx`
  dispatch-thunk cluster: load the target pointer, jump). Its single caller is `bl` at
  `0xaa0a50`, which passes two stack out-parameters.
- The plugin overwrites the thunk's 16-byte tail with: `stw 18,0(r4); li r3,0; blr` — i.e. "set
  out-param to 18, return **0**".
- **Runtime effect:** that one call site never reaches the function it used to invoke; the
  caller sees success and a fixed 18 in its out-buffer. **[I]** A service/validation call the
  revival doesn't (or can't) run is short-circuited to "fine, continue". (The target pointer
  itself lives in runtime-relocated RW data, so the pre-relocation ELF cannot tell us which
  function it was — **[U]**.)

### 2.3 XOR (de)obfuscation disabled — `0xd81860` truncated [V]

- `FUN_00d81860` XORs a buffer with the repeating 8-byte key `8b 75 2c 3a 5e 4d f1 90`.
  It belongs to the game's crypto/encode module: the function-index table at `0x1211818…`
  lists it among `0xd7e388–0xd819e8`, i.e. the same module as helpers `0xd81a18`/`0xd827c8`
  used right after it by its caller (code near `0xa48984`).
- Writing `blr` at its entry makes the whole routine return immediately: **the XOR step no
  longer runs**.
- **Runtime effect:** whatever blob that caller feeds through the crypto chain is handled
  without this layer. **[I]** Either the revival server/save format doesn't apply this
  obfuscation (so the game must stop expecting/adding it), or MGO2PC feeds data that is already
  in the format the XOR would corrupt. Exactly which data (save file, net payload, replay) is
  **[U]** — it is the *only* caller site `0xa48984` inside a routine that also calls the other
  `0xd8_xxxx` helpers.

### 2.4 Heartbeat/counter getter disabled — `0xe4ea8` truncated [V]

- `FUN_000e4ea8` (104 B) increments a counter (`[obj+0x7e0]++`) and returns `[obj+0x100]`. It is
  registered in a function-index table (`0x11ebbd0`) alongside `0xe3900–0xe5050` — a module of
  small functions of unknown purpose **[U]**.
- **Runtime effect:** that tick/counter routine does nothing when invoked through its table
  entry; its counter stops advancing and callers get whatever `r3` happened to hold. **[I]**
  Disabling a heartbeat/tick normally means the *consumer* of that counter is also neutralized —
  plausible target: an idle/keep-alive or watchdog path the revival doesn't need.

### 2.5 Two more registered routines stubbed — `0x2bc0e0` and `0x11a368` [V]

- `0x2bc0e0` (1744 B): a large routine registered in a function-index table
  (`0x11f2c98`); its code starts with `lis r3,0x32xx` and an argument `250`. **[U]** role.
- `0x11a368` (348 B): fills a config object with fields (`+0x1d4…+0x1fc`, sizes `0x206`,
  pointer `0x40a6c`). Its **direct caller is a switch-case handler at `0xfa718`** — the
  surrounding code is a chain of `bl <handler>; b <shared epilogue>` cases inside one big
  dispatcher (region `0xf9d8c–0xf9eb4`). So this is *one command case* in a game command
  processor.
- Both are truncated with `blr`.
- **Runtime effect:** when the boot/init walker or the dispatcher reaches these entries, the
  work simply doesn't happen. **[I]** Candidate: an RPC/command the revival's server never
  sends, or a stock init the plugin supersedes. **[U]** without runtime tracing.

---

## 3. Custom content through the stock pipeline (job 2)

This is the largest *functional* change: the game keeps its own loaders, and the plugin
redirects them so they load MGO2PC content. Detail per hook is in code-injection.md §3–4; here
is what the combination achieves in game terms:

| Content | How the game is steered to load it |
|---|---|
| **Custom maps + rules** | call-site hooks on the map/arena object-method family (`0x26cc10/0x26cc88/0x2666c8`…) and on the rule provider (`0x9bd6c8`, orig callee `0xaa8920`): the plugin's `FUN_11f50` supplies the rule strings ("Night (DP)", "Dust Enabled (DP)", "Storm Disabled"…) |
| **Custom resources (map packages, HD textures)** | the stock resource helper `0xcdc90` is replaced at 5 call sites by `FUN_10b2c`, which retargets lookups of names like `n006a…n024a`, `sm_dd`, `sm_ll`; sibling helpers `FUN_11808/11908` cover 6 more sites inside the most-patched game function `FUN_00bd8148` |
| **Resource-loading adjustments** | in the two loader functions `FUN_009a1808`/`FUN_009a261c` (which also get resource hooks), two result-byte stores are NOP'd (`0x9a1c78`, `0x9a2814` — "call the loader, ignore its status byte") and a returned-value mask is narrowed (`0x9a27f4`: keep 8 bits instead of 16) — **[I]** status/handling of *stock* resources the loader would complain about is made non-fatal, so custom packages stream without the stock checks tripping |
| **HUD font** | hook `0x8d1fe8` forces game states (0x90/0x91 → 0x8f) to load `msf_def_mgo` — the custom MGO font (the same file we decrypted from the lobby stage `resident_custom.dar`) |
| **UI skins** | hook `0x5d32d4` swaps `cbox_b_sk` / `cbox_c_sk` |
| **Replays** | hook `0x9c4264` opens `…/USRDIR/o/replay/` |
| **Save-data naming** | hook `0xb192c0` supplies "SaveMGO" + resource names to the save module |

**Net runtime effect:** when the game opens a map/lobby or draws UI, the stock loader asks for
an asset name, and a plugin replacement answers with the MGO2PC asset — the game never knows it
isn't Konami content.

---

## 4. Raising the game's ceilings (job 3)

The plugin lifts limits so the enlarged MGO2PC content (more maps, more records, bigger
packages) fits inside the stock data structures:

| What is widened | Sites | Effect |
|---|---|---|
| Nine identical 8-bit index checks | `0xa87eb0/0xa87e70/0xa87e30/0xa87df0/0xa87db0/0xa87c58/0xa87bd0/0xa87b90/0xa87b7c` in `FUN_00a87ab0` | each bound `≤31` → `≤255`: records/entries up to 8× larger accepted **[I]** (the function is an object update/parse state machine that walks item lists by a byte index) |
| A 15-slot list guard | `0x9c43b4` | `branch if index > 14` removed — indexes past 14 no longer abort. **[I]** sits ~0x150 bytes from the replay hook (`0x9c4264`): consistent with a 15-replay cap being lifted |
| Config-derived sizes | `0xab67e0/e8/f0` in `FUN_00ab6688`, `0xb20104` in `FUN_00b1fe18` | derived values raised 192→512, 128→256, 96→256, 30→80 |
| Arena/region sizes | `0x264310` (`0x8f0→0x2000`), `0x286758`/`0x285ff8` (guards `0x1ff→0x1000`) in the pool managers | allocation sizes and bounds for the game's big memory regions grow several-fold |
| Four literal-pool constants | `0x84c85c…0x84c868` | set to 320 (neighbors in the pool: 0x74/0xdc/0x140/0x198/−0x40 — a table of per-class sizes/counts) **[U]** |

### 4.1 What `FUN_00ab6688` actually is — and how the option gets *into* the settings menu [V]

`FUN_00ab6688` is not a sizing function; it is a **settings-menu option handler**. Its single
reference in the whole image is a **data pointer at `0x1209018`** — i.e. it is reached only
*indirectly*, through a function-pointer slot in an options table (same `{fn, …}` table style as
the game's registered routines). So it is one **row handler** in the game's options menu: when
the player activates that row it runs this handler.

What the handler does:
1. reads the row's current value — u16 field `[config + 0x244e42]` of the big global config
   object (valid values 0x60/0x80/0xc0/0x100/0x200 = 96…512);
2. checks **availability** of related services before every transition
   (`FUN_00eaa9f8` / `FUN_00eac89c` / `FUN_00eaa8cc` / `FUN_00eac9f8` — the same service-gate
   family used around the online/auth code, §2);
3. writes a *derived* (next-in-ladder) value back into `[config + 0x244e42]`;
4. repaints the row's on-screen text via the message-ID/UI-text module
   (`FUN_00a0c930` msg-ids `0xa8/0xa9/0xaa`, text assembly at `config + 0x183450` via
   `FUN_00aab5c8`, current value via `FUN_00f9d840`/`FUN_00fa1c08`).

So the game's options menu is **data-driven**: each row = a config field + an availability gate
+ a handler that advances the value and asks the text module to redraw. No new menu code is
added — the plugin only changes the *values* one handler may produce.

**Runtime effect of the three patches** (`0xab67e0/e8/f0`: `addi 192/128/96 → 512/256/256`):
for three of the row's allowed states, the derived value written back is raised. **[I]** In the
menu, cycling that option now reaches values ~2.6× beyond Konami's cap — a slider/selector
whose top step was 192/128/96 now goes to 512/256/256. The choice is availability-gated, so it
only appears when the related (online) service is reachable. *Which option* (its menu label) is
**[U]** — it is one entry in the config-driven options table, differentiated by msg-ids
`0xa8/0xa9/0xaa` (its title/description/current-value strings), field `0x244e42`, and the
service gates it checks.

Related row plumbing the plugin also touches: the **options/rule record serializers** in the
0x26xxxx–0x27xxxx family (`FUN_00270e00`, `FUN_00275a90`, `FUN_00275718`, `FUN_00276e88` — see
code-injection.md §4) are the game code that packs option/rule rows into byte streams via the
appenders `0x26cc10`/`0x26cc88`; the plugin hooks six of those `bl` sites so every row the game
serializes passes through a plugin wrapper first (which mirrors the row byte into plugin globals
`DAT_75e58`/`DAT_75e5c` and re-enters the appender via game API `+0x88`/`+0x8c`). **[I]**
Together with the raised index caps (`≤31→≤255`, §4 table) this is how extra MGO2PC option/rule
entries ride the stock menu without adding UI code.

### 4.2 The object-pool field relocation — why the pool grew [V]

The game's arena/pool managers (`FUN_002856e0`, `FUN_00285b58`, `FUN_00286358`,
`FUN_002871b8`; they use linked free-lists via `FUN_00269230/40/80` and block ids) access one
of their descriptor fields at offset **0x8ec**. The plugin rewrites **all 15 access sites**
(loads *and* stores) to offset **0x170c** (+0x880), and raises the region size passed in
(`0x264310`: 2288 → 8192) and the region guards (`0x1ff → 0x1000`).

**Runtime effect:** the pool managers now read/write the field 0x880 bytes deeper in every
descriptor and tolerate regions 4–8× bigger — i.e. **the game allocates and manages memory
regions large enough for MGO2PC's content** while the stock logic (free lists, block reuse)
keeps working against the enlarged layout. This family is the same code region the map/arena
hooks and the auth hook live in (`FUN_00262d10` etc. — arena init functions that allocate
~0xc0000-byte regions in 0x8000-byte chunks). **[I]** These are the per-map/per-arena heaps;
enlarging them is what lets bigger custom stages load without the stock heap checks aborting.

---

## 5. Disabling replaced stock behavior (job 4)

### 5.1 Constructor loops gutted — `FUN_00aa38ec` (10 NOPs) and `FUN_00996e4c` (6 NOPs) [V]

Both are object constructors:
- `FUN_00aa38ec` allocates a **~774 KB object** (`0xc1ac0` bytes via `FUN_000be778`) and
  initializes it; it also calls `FUN_000bead8` — a function that the plugin *hooked elsewhere*
  (sites `0xb12144`, `0x8d1e6c` replace `bl 0xbead8` with plugin functions). The 10 NOPs remove
  the whole per-item body of a loop in it (alternating `stw …,8(r9)` + `bl …` all die).
- `FUN_00996e4c` (2136 B) is gutted the same way (6 NOPs).

**Runtime effect:** the constructor builds the big object but its loop no longer stores or
processes per-entry data. **[I]** Stock "register every sub-object/component" work is skipped;
the plugin provides the registration itself via its hooks on the same module — the object is
created empty and populated the MGO2PC way. ([U] on exactly which object — the 0xaa_xxxx
module region also holds the map-rule provider `0xaa8920`, so a stage/rule-related instance is
plausible.)

### 5.2 State/diagnostic writes removed — `FUN_008353f8` (4 NOPs), `FUN_009902b4`, `FUN_00c2e5c0`, `FUN_002871b8` [V]

- `FUN_008353f8` is float-heavy (animation/geometry math). The 4 NOPs remove stores of small
  status values {1, 4, 3, …} into `[obj+0xff4]` — i.e. code still *computes* the state but
  never *records* it. **[I]** whatever reads that field never sees those phases, so
  phase-dependent behavior elsewhere is skipped. (What the field gates: **[U]**.)
- `FUN_009902b4` (a multi-step teardown/setup that touches the giant config object at offsets
  like `+0x183444`) loses one conditional branch — a guarded sub-step now always runs.
- `FUN_00c2e5c0` loses two conditional exits.
- `FUN_002871b8` (pool family) loses one conditional return, and its `li r3,-1` error return is
  changed to `li r3,0` — pool operation failure no longer reported as failure.

**Net runtime effect:** several stock status/error signals are silenced so flow continues where
Konami code would have stopped or branched.

### 5.3 Two "force result" call replacements [V]

| Site | Function | Original | Patched | Runtime effect |
|---|---|---|---|---|
| `0x832c9c` (written twice) | `FUN_00831af0` (12 KB dispatcher) | `bl <subroutine>` | `li r3,1` | one big-dispatcher call is skipped, result forced to success **[I]** |
| `0xa01f30` | `FUN_00a01638` | `bl <subroutine>` | `li r3,0` | one call skipped, result forced to 0 (clean default) **[I]** |
| `0xac3788` | `FUN_00ac375c` | `cmpwi r3,0` (null-check on a 0x2ac0-byte allocation) | `cmpwi r4,0x2ac0` | comparison now tests `r4` against the allocation size constant **[U]** |

---

## 6. Retargeting stock tables and pointers (job 5)

### 6.1 Pointer high-words redirected (10 `lis` patches) [V]

In two functions of one module (`FUN_000ef010`, `FUN_000eb828`), groups of address constants
are repointed by rewriting only the `lis` high half:

- 6 sites: `lis r0/r10, 0xaf0` → `0xb80`  (stock `0x0af0xxxx`-family tables → `0x0b80xxxx`)
- 4 sites: `lis r9, 0xf510` → `0xf480`   (stock `0xf510xxxx`-family tables → `0xf480xxxx`)

**Runtime effect:** code that used to dereference two stock data tables now dereferences the
plugin's equivalents (low halves unchanged, so the redirect is a base-region swap). **[I]**
Static/config tables the stock module consults are pointed at MGO2PC-provided data. (What the
tables hold: **[U]** — candidates are per-map/per-mode constant tables, matching the module's
location among UI/option code.)

### 6.2 Pointer-list build suppression (see 5.1), plus `0x990390`/`0x2872cc` branch removals
Covered above.

---

## 7. What was deliberately NOT touched

- The downloader **launcher `EBOOT.ELF`** performs no in-game patching at all (code-injection.md §1).
- The plugin adds **no network code** to the game — the game's own sockets do all sending
  (code-injection-fingerprint.md).
- `0xfc0878` is only a 1-byte read/write **self-test** (proves dbg-mem works) — no effect.

---

## 8. Quick functional map

| Feature | Patches involved |
|---|---|
| Boot into online flow / pass gates | `0x13258` flip, `0xfbcfec` thunk, token hook `0x262e9c` (`FUN_3520`), `0xfbb29c` stub hook (`FUN_35a4`: attaches `auth` token + `client=2.18.0`), "Ver.2.18.0" hook, `0xd81860` XOR off |
| Custom maps/rules/resources/font/skins/replays | the 60 hooks (map/arena + rule + resource + font + skin + replay) plus loader tweaks `0x9a1c78`, `0x9a2814`, `0x9a27f4`, replay-cap guard `0x9c43b4` |
| Bigger content fits | index bounds `≤31→≤255` (9×), size raises (`0x264310`, `0x286758`, `0x285ff8`, `0xab67e0/e8/f0`, `0xb20104`, `0x84c85c…`), pool field relocation (15 sites) |
| Stock behavior replaced by plugin equivalents | loop gutting (`0xaa4088…`, `0x9973e8…`), routine truncation (`0x2bc0e0`, `0x11a368`, `0xe4ea8`), dispatcher skips (`0x832c9c`, `0xa01f30`), status/error silencing (`0x8358c4…`, `0x2872cc/d4`, `0x990390`, `0xc2ef58/f8`) |
| Table/pointer redirection | `0xef4ec…`, `0xebc24…` (10 `lis` sites) |

---

## 9. Technical catalog (per-address, machine-verified)

Containing functions from the MGO2 Ghidra project; decompiles: `mgo2_decomp/static_fn/`.
Full raw rows: `eboot_analysis/plugin_analysis/static_patches_raw.txt`.

**Counts:** 83 distinct write targets in `FUN_784` (84 writes; `0x832c9c` twice) + the `0x13258`
stub flip in `FUN_19a8` + 60 hook-site redirections.

### 9.1 Routine truncations — `blr` at entry (4)

| Addr | Fn (size) | Function | Effect |
|---|---|---|---|
| `0xe4ea8` | `FUN_000e4ea8` (104) | tick/counter getter (increments `[g+0x7e0]`, returns `[g+0x100]`); table 0x11ebbd0 | counter never advances (2.4) |
| `0xd81860` | `FUN_00d81860` (240) | XOR-obfuscator, key `8b 75 2c 3a 5e 4d f1 90`; table 0x1211868 | XOR layer off (2.3) |
| `0x2bc0e0` | `FUN_002bc0e0` (1744) | large registered routine; table 0x11f2c98 | routine off (2.5) |
| `0x11a368` | `FUN_0011a368` (348) | config-object setup; **one command case** in dispatcher via `0xfa718` | command case off (2.5) |

### 9.2 Forced results & rewritten checks

| Addr | Fn | Orig → Patched | Effect |
|---|---|---|---|
| `0xfbcfec` | dispatch-thunk tail (caller `0xaa0a50`) | `…; lwz r0,0(r12); lwz r2,4(r12); …bctr` → 16B: `stw 18,0(r4); li r3,0; blr` | call → hardcoded success (2.2) |
| `0x13258` | `FUN_00013258` (8) | `li r3,-1; blr` → `li r3,1` | gate opens (2.1) |
| `0x832c9c` (×2) | `FUN_00831af0` (12 040) | `bl X` → `li r3,1` | dispatcher call skipped |
| `0xa01f30` | `FUN_00a01638` (3896) | `bl X` → `li r3,0` | call skipped, zero result |
| `0x2872d4` | `FUN_002871b8` (292) | `li r3,-1` → `li r3,0` | error → success |
| `0x9a27f4` | `FUN_009a261c` (2988) | `rlwinm r3,r3,0,16,31` → `0,24,31` | loader result masked to 8 bits |
| `0xac3788` | `FUN_00ac375c` (352) | `cmpwi r3,0` → `cmpwi r4,0x2ac0` | check re-targeted (5.3) |

### 9.3 NOP'd instructions (27)

| Addr | Fn | What was removed | Functional note |
|---|---|---|---|
| `0xaa4088, a8, d4, f4, 0xaa4120, 40, 6c, 90, bc, e0` | `FUN_00aa38ec` (2872) | whole per-item loop body (bl + stw pairs) | constructor no longer populates list (5.1) |
| `0x9973e8, 0x997408, 0x997448, 68, 0x997494, c4` | `FUN_00996e4c` (2136) | same pattern | sibling constructor (5.1) |
| `0x8358c4, 0x835ff0, 0x835ed0, 0x8362b8` | `FUN_008353f8` (3244, float code) | `stw r0, 0xff4(r10)` of {1,4,3,…} | status phases never recorded (5.2) |
| `0x9a1c78` | `FUN_009a1808` (3604) | `stb r3,0(r28)` after loader call | loader status discarded (3) |
| `0x9a2814` | `FUN_009a261c` (2988) | same | loader status discarded (3) |
| `0x9c43b4` | — | `bc` after `cmplwi r9,0xe` | replay/list cap >14 lifted (4) |
| `0x2872cc` | `FUN_002871b8` (292) | conditional return | (5.2) |
| `0x990390` | `FUN_009902b4` (300) | conditional branch in teardown | guarded step now always runs (5.2) |
| `0xc2ef58, 0xc2eff8` | `FUN_00c2e5c0` (3520) | two conditional exits | (5.2) |

### 9.4 Limit/capacity raises & constant data

| Addr | Fn | Orig → Patched | Effect |
|---|---|---|---|
| `0xa87eb0/70/30/df0/db0/c58/bd0/b90/b7c` (9) | `FUN_00a87ab0` (2524) | `cmplwi r0,0x1f` → `0xff` | index cap 31→255 |
| `0xab67e0/e8/f0` | `FUN_00ab6688` (660, options handler) | `addi r0,192/128/96` → `512/256/256` | derived option values raised (4.1) |
| `0xb20104` | `FUN_00b1fe18` (1940) | `addi r0,30` → `80` | size/count raised |
| `0x264310` | `FUN_00264270` (300, pool) | `addi r3,0x8f0` → `0x2000` | region size 2288→8192 |
| `0x286758` | `FUN_00286358` (1368, pool) | `cmplwi r27,0x1ff` → `0x1000` | guard 511→4095 |
| `0x285ff8` | `FUN_00285b58` (2036, pool) | `cmplwi r28,0x1ff` → `0x1000` | guard 511→4095 |
| `0x834b9c` | `FUN_00831af0` | `cmplwi r0,0x2` → `0x0` | null/size check const changed |
| `0x84c85c…c868` | data (literal pool) | 4 words → `0x140` (320) | constants overridden (4) |

### 9.5 Pool-field relocation — `0x8ec` → `0x170c` (15 sites)

`0x2856e0, 0x285f30, 0x286024, 0x286074, 0x286310, 0x286564, 0x2865a8, 0x2865d4,
0x286634, 0x286670, 0x28669c, 0x286784, 0x2867d4, 0x286810, 0x28686c` —
all `lwz/stw rX, 0x8ec(rY)` → `lwz/stw rX, 0x170c(rY)` in the pool managers
(`FUN_002856e0`, `FUN_00285b58`, `FUN_00286358`, `FUN_002871b8`). See 4.2.

### 9.6 Pointer high-word redirects (10 `lis`)

`0xef4ec`, `0xebc24` (`lis r0 0xaf0→0xb80`); `0xef5c4/0xef99c/0xebd2c/0xebe10`
(`lis r10 0xaf0→0xb80`); `0xef5c8/0xef9a0/0xebd30/0xebe14` (`lis r9 0xf510→0xf480`) in
`FUN_000ef010` / `FUN_000eb828`. See 6.1.

### 9.7 The 60 hook-site redirections

See the full table in code-injection.md §4 (every site: containing game function, original
callee, plugin replacement) and `eboot_analysis/plugin_analysis/hook_map.txt`. Functional
grouping: §3 above.

---

## 10. Method & artifacts

- Patch list: `FUN_784` decompile + raw disassembly (every `FUN_2bd8(target, buf, 4)` with its
  exact word); stub flip from `FUN_19a8`; hooks from the module-entry table.
- "Original" bytes from the untouched `MGO2.ELF`; every row is original→patched machine-verified.
- Function roles from Ghidra decompiles (`mgo2_decomp/static_fn/*.c`), function-index tables in
  RW data, and caller chains (raw `bl` scans + Ghidra).
- Confidence tags separate facts from interpretation; items marked **[U]** genuinely need
  runtime tracing (RPCS3 + breakpoints on the patched sites) to name exactly.
