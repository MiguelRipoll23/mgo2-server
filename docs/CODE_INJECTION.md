# CODE_INJECTION.md — How code gets injected into MGO2

*Reverse-engineering notes on the MGO2PC revival stack: the downloader launcher (`EBOOT.ELF`),
the game (`MGO2.ELF`, the MGO2PC EBOOT for `NPMG00020`), and the runtime plugin
(`plugin.sprx`, module name `MGOPlugin`).*

Companion doc: [`FINGERPRINT.md`](FINGERPRINT.md) covers what each component collects/transmits.
This doc explains the *mechanism* of code injection. All addresses are virtual addresses in the
respective binary. Artifacts referenced at the bottom.

---

## 1. TL;DR

There is **no injection from the downloader launcher**. The launcher (`EBOOT.ELF`) is a plain
PKGi-style installer/downloader — it fetches the game + plugin files over HTTP GET and installs
them under `/dev_hdd0/game/NPMG00020/`; it never touches the running game.

Code injection happens in **two stages, both inside the game process**, and the "launcher" that
does the injecting is the **game itself** (the MGO2PC-patched EBOOT), using legitimate PS3
prx-loading syscalls, plus debug-memory syscalls that work on CFW / RPCS3:

| Stage | Who runs it | How | What it installs |
|---|---|---|---|
| 1. Plugin load | Game boot code `FUN_10360` (added by MGO2PC) | `_sys_prx_load_module` (480) + `_sys_prx_start_module` (481) on `/dev_hdd0/game/NPMG00020/plugin.sprx` | Maps + starts `MGOPlugin` **inside the game process** |
| 2a. Static patches | `plugin.sprx` module entry | `sys_dbg_write_process_memory` (905) + `sys_process_authenticate_program_segment` (8) | ~70 individual instruction/pointer words rewritten in the game (mostly NOPs, a few `blr`/constant writes) |
| 2b. Call-site hooks | `plugin.sprx` module entry | same dbg-write primitive, 60 call sites (`FUN_2ac8`) | Every patched site: `bl <original>` → `bl <plugin function>` |
| 2c. Stub hook | `plugin.sprx` module entry | `FUN_2778`: relocates a 16-byte game dispatch stub, jumps to `FUN_35a4` | Game `0xfbb29c` now builds objects carrying the plugin's `auth` credential + `client` version |
| 2d. Token injection | plugin hook `FUN_3520` (site 0x262e9c) | hooked into a game method | Device credential (`<key1>;<key2>;<MAC>`, Salsa20-derived) computed on the boot path; attached as `auth` by 2c |

No bootloader/hypervisor tricks, no out-of-process code, no second executable. The plugin is
loaded *into* the game by the game, then rewrites the game's own memory with debug privileges.

---

## 2. Stage 1 — The game loads the plugin into itself

### The loader string
```
0x10074d5  "/dev_hdd0/game/NPMG00020/plugin.sprx"
```
This string is **inside the game EBOOT** and referenced by exactly one piece of game code
(Ghidra: `REF from 0x104c4 type=PARAM`, in the function body starting at 0x10360).

### The loader function
`FUN_10360` (also listed as `.opd.FUN_00010360`; 464-byte body starting 0x10360) is reached from
the game's module-init descriptor table in RW data (`0x11e7358 …`: entries of `{code addr,
0x1222a00}`), via trampoline `FUN_10230`. Its tail is instruction-verified:

```
0x10494  lis  r3, 0x100          ; r3 = 0x1000000
0x10498  ori  r3, r3, 0x74d5     ; r3 = &"/dev_hdd0/game/NPMG00020/plugin.sprx"
0x1049c  addi r4, r0, 0          ; flags = 0
0x104a0  addi r5, r0, 0          ; opt   = NULL
0x104a4  addi r11, r0, 480
0x104a8  sc                      ; sys_prx_load_module(path, 0, 0) → id in r3
0x104c4  extsw r3, r3            ; sign-extend module id
0x104c8  addi r4, r0, 0
0x104cc  addi r5, r1, 64         ; r5 = &stack module-info
0x104d0  addi r11, r0, 481
0x104d4  sc                      ; sys_prx_start_module(id, 0, &info)
```

Syscall numbers from the clienthax PS3 map:
- **480 = `_sys_prx_load_module`** — hypervisor/kernel loads the signed SPRX, resolves it,
  maps it into the **game's own process**.
- **481 = `_sys_prx_start_module`** — kernel runs the plugin's `module_entry`.

Because the plugin is loaded as a PRX in-process, its code shares the game's address space and
has the same CFW/RPCS3 debug rights as the game — which is exactly what Stage 2 exploits.

> Note: this loader is *not* in the stock Konami game. It is MGO2PC-added startup code; the
> original game had no `plugin.sprx`. This is how the revival ships code in a signed SELF without
> touching the game's own signature: the game (also re-signed by MGO2PC) invokes the standard
> kernel loader on a second, separately-signed SELF.

---

## 3. Stage 2 — plugin.sprx rewrites the game from inside

The plugin (`MGOPlugin`, decrypted 152 KB ELF) has a tiny syscall surface — no network at all:

| Syscall | # | Use |
|---|---|---|
| `sys_ss_media_id` | 879 | media-id query (`"MGO…"` check) for the credential |
| `sys_dbg_read_process_memory` | 904 | read game memory |
| `sys_dbg_write_process_memory` | 905 | write game memory (the injection primitive) |
| `sys_process_authenticate_program_segment` | 8 | re-authenticate before debug reads/writes |
| `sys_process_getpid` / `sys_timer_usleep` | 1 / 141 | misc |

Write chain (all logging via a string logger at `DAT_17f08`):

```
FUN_2bd8 (log word, then write)   → FUN_26d0  → FUN_2418 (dbgRead + auth) → FUN_2570 (dbgWrite + auth)
```

### 3.1 Static patches — `FUN_784` (bulk list) and `FUN_19a8` (probe)

`FUN_784` (`0x784`, 4524 bytes) executes a hard-coded list of **~70 individual 4-byte writes**
into the game. It allocates 4-byte buffers, fills them with constants, and pushes them through
`FUN_2bd8` → `FUN_26d0`. Representative writes:

| Game address | Value written | Effect (interpretation) |
|---|---|---|
| `0xfbcfec` | 16-byte blob from plugin const table `0x16c40` | flag/struct override |
| `0x9a1c78`, `0x9c43b4`, `0x9a2814` | NOP `0x60000000` | kill instructions right before hook sites `0x9a1c94`, `0x9a2830`, `0x9bd5c8` (make room / neutralize checks) |
| `0xe4ea8` | `blr` `0x4e800020` | turn a block of code into an early return |
| `0x9a27f4` | `0x5463063e` (`rlwinm …`) | single-instruction semantic rewrite |
| `0x8358c4`, `0x835ff0`, `0x835ed0`, `0x8362b8`, `0xaa4088`…`0xaa41e0` (10), `0x9973e8`…`0x9974c4` (6), `0xc2ef58`, `0xc2eff8`, `0x990390`, `0x2872cc` | NOP `0x60000000` | disable game self-checks / calls |
| `0xef4ec`, `0xebc24`, `0xef5c4`, `0xef99c`, `0xebd2c`, `0xebe10`, `0xef5c8`, `0xef9a0`, `0xebd30`, `0xebe14` | pointers from a local table (`0xa87eb0…0xa87b7c`…) | repoint entries in game pointer/string tables |
| `0xd81860` | `blr`-family value | early-return patch |

`FUN_19a8` (368 bytes) is a **self-test + targeted flip**:

1. If a probe (`FUN_164ac`, reads the media id) returns `< 0x1000`: write **`li r3,1` =
   `0x38600001`** over game address **`0x13258`**, then *call* the patched stub through a saved
   game pointer (`*DAT_17830`) and expect `1` (returns 4 if ok, 3 if not).
   - What `0x13258` is: `li r3,-1 ; blr` → a `return -1` stub. The game has ~17 identical
     stubs (unimplemented/blocked functions), reachable **only through function pointers**
     (we found zero direct `bl 0x13258` call sites). Rewriting the stub body in place makes
     *every* indirect dispatch through that pointer return success (1) instead of failure (-1).
   - Why: the game's online-auth path consults one of these "is this allowed/initialized?"
     handlers; the plugin forces it to succeed so online login proceeds.
2. Otherwise (probe ≥ 0x1000): read 1 byte at `0xfc0878`, write it back through the dbg
   primitives — a read/write self-test against a harmless byte in a game string table
   (cosmetics name data).

### 3.2 Call-site hooks — module entry + `FUN_2ac8` (60 sites) + one stub hook

The plugin's module entry (`0x0`, prologue then straight-line code) contains **60 inline hook
entries** that use `FUN_2ac8`, plus **one hook that uses a different primitive,
`FUN_2778`** (see “the `0xfbb29c` stub hook” below). Each `FUN_2ac8` entry is:

```
lis  r3, <hi>;  ori  r3, r3, <lo>   ; r3 = game call-site address
lis  r4, 0x1;   lwz  r4, off(r4)    ; r4 = plugin fn ptr from data slot 0x10000+off
bl   FUN_2ac8                       ; hook writer
```

`FUN_2ac8` builds a PPC branch word and writes it over the site:

```c
if (param_1 /* site */ < param_2 /* plugin fn */)
    word = (param_2 - param_1) + 0x48000001;      //  bl <plugin fn>
else
    word = 0x4c000001 - (param_1 - param_2);      //  bl <plugin fn> (negative disp)
FUN_26d0(site, &word, 4);                          // → dbgWrite
```

Every target site in the game originally contained `bl <original>` (usually followed by a NOP),
verified against `MGO2.ELF` — e.g. `0x262e9c: bl 0x26c6d8;  nop`. After the plugin runs, control
that would have entered `<original>` instead enters the plugin function. The original is dropped
unless the plugin explicitly calls it back (several replacements re-enter the original through
the game-API table `DAT_17f04` — e.g. `FUN_32c8`/`FUN_33a4` only call through to the original
when a global enable flag `DAT_17838` is set).

**What plugin replacement functions do** (all call into the game through a resolved
game-API pointer table at `DAT_17f04`; offsets like `+0x44`, `+0x2c`, `+0x104` index game
functions). Evidence labels, from strings the replacements pass:

| Plugin fn | Hooked sites (game) | Evidence / purpose |
|---|---|---|
| `FUN_3520` | `0x262e9c` (in `FUN_00262d10`, orig `bl 0x26c6d8`) | **Auth token.** Calls game API `[DAT_17f04+0x44]` (runtime-resolved) then `FUN_3d94` → builds the MAC/media-id credential into global token (Salsa20). See FINGERPRINT.md |
| `FUN_35a4` | `0xfbb29c` (16-B dispatch stub, via `FUN_2778` trampoline, §3.3) | **`auth`/`client` pair injection** — runs the relocated original stub, then registers `{"auth", &token}` and `{"client", "2.18.0"}` on the object the game builds through that dispatch |
| `FUN_3674` | `0xa0ecc0`, `0xc07af8` | Passes **`"Ver.2.18.0"`** into game API `[+0x2c]` — client-version injection into a game handshake/UI call |
| `FUN_11f50` | `0x9bd6c8` (orig `bl 0xaa8920`) | **Custom map rules** — carries the whole MGO2PC rule set: `"Night (DP)"`, `"Dust Enabled (DP)"`, `"Storm Disabled"`, … |
| `FUN_12a18` | `0x9c4264` (orig `bl 0x246950`) | **Replay loader** — `"/dev_hdd0/game/NPMG00020/USRDIR/o/"` + `"replay/"` |
| `FUN_9490` | `0x8d1fe8` (in `FUN_008d1dd8`, orig `bl 0x8d0698`) | **Font module override** — forces game states (0x90/0x91 → 0x8f) to load **`"msf_def_mgo"` / `"msf_def_mgo_m"`** (the MGO font we decrypted from the lobby stage; `resident_custom.dar` → `msf_def_mgo.mdn`) |
| `FUN_10b2c` | `0xbd9518`, `0xb10ddc`, `0xb0fad4`, `0xb0faec`, `0xe99930` (all orig `bl 0xcdc90`) | **Resource-name translator** for `"n006a","n014a","n020a","n022a","n024a","sm_dd","sm_ll"` — swaps stock assets for custom ones at stream time |
| `FUN_11808` / `FUN_11908` | `0xbd99e8`,`0xbd9a20`,`0xbd9a58`,`0xbd9ad8` (orig `0x253b68`); `0xbdb50c`,`0xbdb52c` (orig `0x253be0`) — all inside **`FUN_00bd8148`**, the most-patched game function (8 sites) | same resource family (`"sm_ll"`); per-state overrides |
| `FUN_10724` | `0xb192c0` (orig `bl 0x36350`) | **`"SaveMGO"`** save-data + asset names |
| `FUN_a064` | `0x5d32d4` (orig `bl 0x5e68c8`) | **UI skin** swap — `"cbox_b_sk"`, `"cbox_c_sk"` |
| `FUN_32c8` / `FUN_33a4` | `0xfad50` / `0xf9de4` (in `FUN_000f9598`, orig `0x13d4f0`/`0x13f480`) | float field at `obj+0x460` scaled by `DAT_1783c`; calls original only if `DAT_17838` — a speed/scale wrapper |
| `FUN_2e4c` | `0x280860`, `0x28137c` (orig `bl 0xf0ea88`) | guard wrapper: zeroes a 0x400-byte buffer, calls game APIs `[+0x98]/[+0x9c]`, size/state checks |
| `FUN_a4a0` | `0x2770c4` (orig `bl 0x26cc88`) | game debug print `"null member??\n"` path |
| `FUN_12288` | `0x2a6c34`, `0x2a6e90` (orig `bl 0x2aec38`) | resource names `"n013a","n020a"` |
| 20+ others | see full table §4 | logic-only wrappers (no strings): API re-route, arg fixups, conditionals on globals |

Recurring original-calldown families in the game that get replaced at many sites:
- **`0xcdc90`** — shared asset/resource helper, hooked 5× (replaced by `FUN_10b2c`).
- **`0x26cc10` / `0x26cc88` / `0x2666c8` / `0x26c6d8`** — the `0x26xxxx` object/method family,
  hooked at 12+ sites (map/arena and process/self-check dispatchers, incl. the token hook).
- **`0x253b68` / `0x253be0`** — hooked 6× inside `FUN_00bd8148`.

The two far entries (`0xf1c688`, `0xf1bdfc`, orig `bl 0xeffa0c`) are installed from a second,
later init region (`0x12804`) rather than the module entry — i.e. hooks can also be staged.

### 3.3 The `0xfbb29c` stub hook — a second, 16-byte hook mechanism (`FUN_2778`)

Not every hook is a `bl` rewrite. Game address `0xfbb29c` is a **16-byte dispatch stub** (in
`MGO2.ELF`: `li r12,0; oris r12,0x11a; lwz r12,0xf4c0(r12); …` — i.e. it loads a function
pointer/OPD from data slot `0x11af4c0` and jumps through it; the slot is runtime-populated, so
its target `0xfc1a50` is an OPD descriptor whose code field is 0 in the file).

Instead of overwriting a branch, plugin `FUN_2778`:
1. **relocates the original 16-byte stub** from game `0xfbb29c` into plugin space (the
   `0x3574` region — which is why `FUN_3574` decompiles as an empty no-op sink: its real
   bytes are only written at load time), then
2. writes a **jump to the plugin replacement `FUN_35a4`** over the game stub.

`FUN_35a4` is the **auth/client pair builder** (see FINGERPRINT.md §4.5):
- it runs the relocated original stub (so the game's own dispatch still happens), then
- when the caller passes a non-null second argument, it calls the same dispatch twice more
  with the string-pointer pairs `{"auth", &token}` (token global `DAT_183bd`, built by
  `FUN_3520` on the hooked boot path) and `{"client", "2.18.0"}` (strings at plugin
  `0x16ea0`/`0x16ea8`/`0x16eb0`).

**Effect:** every object the game builds through the `0xfbb29c` dispatch carries the plugin's
`auth` credential (device token) and `client` version — the fields the game's login/account
record serializes (option/rule appenders `0x26cc10`/`0x26cc88`, themselves hooked) and sends.
`0xfbb29c` is the 61st hook site, installed with a different mechanism than the 60 `bl`
rewrites.

### 3.4 The P2P decoder hooks — telemetry wrapper, no wire change

Two of the 60 `bl`-rewrite sites sit in the **UDP p2p receive loop** (`FUN_002620d8`),
both replacing calls to the p2p **message decoder** `FUN_002666c8` (the function that
undoes the header scramble + XOR chain, validates the tail digest, and parses the
handshake — see `UDP_P2P.md` §5):

```
 game site    game fn (containing)      orig callee   plugin fn
 0x002623f4   FUN_002620d8 (recv loop)  0x002666c8    FUN_15180   matched-session path (return unused)
 0x00262528   FUN_002620d8 (recv loop)  0x002666c8    FUN_15180   accept/scan path (return gates queue-drain)
```

The replacement (`FUN_15180` in v2.18.0; its relocated twin at `0x1c300` in the current
v2.18.7 plugin, game-API slots shifted +4: `+0x60`/`+0x84`/`+0x7c` vs `+0x5c`/`+0x80`/
`+0x78`) is a **pure pass-through wrapper** — it calls the original game decoder with the
**same `(session, buffer, length)` arguments in both branches** and never touches the
decode result:

```c
// pseudocode (FUN_15180)
ctx  = table[0x5c]();                       // v2: +0x60 — getter, no args
obj  = table[0x80](ctx);                    // v2: +0x84 — accessor
if (obj == 0 || *(u32*)(obj + 4) != session) {
    return table[0x78](session, buf, len);  // v2: +0x7c — ORIGINAL decoder, return passed through
} else {
    before = *(u16*)(session + 0xb0);       // per-session message counter
    table[0x78](session, buf, len);         // ORIGINAL decoder again
    if (before < *(u16*)(session + 0xb0)) DAT_0x77288 += delta;  // telemetry accumulate
    return (len == 0) ^ 1;                  // 1 only for zero-length datagrams
}
```

Verified effects (both plugin images):

- **No wire change.** The decoder still performs the header unscramble, LE32 XOR chain,
  tail-digest validation and message parse; keys, scramble, digest and handshake layout
  are untouched. The plugin's syscall census remains zero-network (§3).
- **The byte counter is write-only telemetry.** `DAT_0x77288` accumulates the delta of the
  per-session counter at `session+0xb0` and is referenced by **no other code in the
  plugin** (raw-instruction scan: the wrapper itself is the only accessor). Nothing reads
  it and nothing transmits it. What `session+0xb0` counts: session init `FUN_00268148`
  zeroes it, and the decoder adds its per-datagram frame/ordering count (`sVar15`-based
  reorder accounting) to it — so the wrapper is accumulating a **per-session "frames
  received" total** (sampled as a delta per decode call). That is netcode instrumentation
  (cf. the v2 "Experimental Netcode" toggle), **not an anti-cheat mechanism** — most
  plausibly a leftover diagnostics counter from MGO2PC's p2p reliability work that was
  never wired to anything.
- **Return-value caveat.** The original decoder returns 1 on success / 0 on failure, and
  the receive loop gates the handshake queue-drain on that return in the accept-scan path
  (site `0x00262528`; site `0x002623f4` ignores it). In the matched branch the wrapper
  returns `0` for any real datagram (`len != 0`) instead of the original's `1`. This
  cannot be what blocks a fake player:
  - the branch fires identically for a real host's reply — the wrapper has no way to tell
    them apart, because the p2p channel carries no credential (the Salsa20 `auth` token
    travels over TCP/HTTP only, see `FINGERPRINT.md`, and `UDP_P2P.md` §1 confirms the
    fingerprint is not sent on this channel); and
  - even in the worst case the reply is **not lost**: the receive loop falls through to
    the global accept session, whose receiver `FUN_00267d58` calls the decoder again at
    `0x267db0` — a direct `bl 0x002666c8` to the **original, unhooked decoder** (verified
    in `MGO2.ELF`; the plugin's two hook sites are the only rewrites in this flow) — and
    validates only `module_magic`. So a byte-correct reply is always decoded and
    processed, wrapper return or not. Real MGO2PC joins complete with this wrapper in
    place; a wrapper that discriminated would break them equally.

**Bottom line:** the plugin's only p2p-path footprint is this transparent counter wrapper.
It changes no packet, no key, no validation gate. A fake player whose handshake reply is
byte-correct per `UDP_P2P.md` §4–§5 is indistinguishable from a real host on this channel.

---

## 4. Full hook map (60 `bl`-rewrite sites + the `0xfbb29c` stub hook)

Columns: hook site (game vaddr) · containing game function · original callee that was
replaced · plugin function now called. For the 60 rows the rewrite is `bl <orig>` →
`bl <plugin fn>`; the last row (`0xfbb29c`) is the `FUN_2778` 16-byte stub replacement
(§3.3). All cross-checked between the
plugin image and `MGO2.ELF`; machine-readable copy in
`eboot_analysis/plugin_analysis/hook_map.txt`.

```
 game site    game fn (containing)      orig callee   plugin fn
 0x009b9fb8   FUN_009b970c              0x00fa4d48    FUN_10590
 0x00b2022c   FUN_00b1fe18              0x00b30be8    FUN_0eaa0
 0x00b192c0   FUN_00b19208              0x00036350    FUN_10724   "SaveMGO", skins
 0x00ba0d1c   FUN_00ba08d8              0x00b30e60    FUN_10988
 0x00ba17a0   FUN_00ba13e0              0x00ba08d8    FUN_1087c
 0x00ba280c   FUN_00ba1e40              0x00ba08d8    FUN_1087c
 0x00262e9c   FUN_00262d10              0x0026c6d8    FUN_3520    ★ token
 0x00bd9518   FUN_00bd8148              0x000cdc90    FUN_10b2c   resource names
 0x00b10ddc   FUN_00b10d28              0x000cdc90    FUN_10b2c
 0x00b0fad4   FUN_00b0f1f8              0x000cdc90    FUN_10b2c
 0x00b0faec   FUN_00b0f1f8              0x000cdc90    FUN_10b2c
 0x00bd99e8   FUN_00bd8148              0x00253b68    FUN_11808   "sm_ll"
 0x00bd9a20   FUN_00bd8148              0x00253b68    FUN_11808
 0x00bd9a58   FUN_00bd8148              0x00253b68    FUN_11808
 0x00bd9ad8   FUN_00bd8148              0x00253b68    FUN_11808
 0x00bdb50c   FUN_00bd8148              0x00253be0    FUN_11908
 0x00bdb52c   FUN_00bd8148              0x00253be0    FUN_11908
 0x008d0e9c   FUN_008d0698              0x000c5ab0    FUN_976c
 0x00e99930   FUN_00e991e8              0x000cdc90    FUN_9904
 0x00e9ac9c   FUN_00e9a968              0x00e991e8    FUN_9814
 0x00e9b110   FUN_00e9a968              0x00e991e8    FUN_9814
 0x00e9c5b4   FUN_00e9b378              0x00e991e8    FUN_9814
 0x00e9e57c   FUN_00e9e238              0x00e991e8    FUN_9814
 0x00e9ea00   FUN_00e9e238              0x00e991e8    FUN_9814
 0x008d1fe8   FUN_008d1dd8              0x008d0698    FUN_9490    ★ msf_def_mgo font
 0x00b12144   FUN_00b12088              0x000bead8    FUN_11760
 0x008d1e6c   FUN_008d1dd8              0x000bead8    FUN_96c4
 0x009bd6c8   FUN_009bd284              0x00aa8920    FUN_11f50   ★ map rules
 0x009a2830   FUN_009a261c              0x00a0ccb8    FUN_11e98
 0x009a1c94   FUN_009a1808              0x00a0ccb8    FUN_11e98
 0x009bd5c8   FUN_009bd284              0x00a0ccb8    FUN_11e98
 0x000fad50   FUN_000f9598              0x0013d4f0    FUN_32c8    scale wrapper
 0x000f9de4   FUN_000f9598              0x0013f480    FUN_33a4    scale wrapper
 0x00f00f34   FUN_00f00d30              0x00f0494c    FUN_1591c
 0x005d32d4   FUN_005d31e8              0x005e68c8    FUN_a064    cbox skins
 0x00a0ecc0   FUN_00a0e65c              0x00ead038    FUN_3674    "Ver.2.18.0"
 0x00c07af8   FUN_00c07828              0x00245968    FUN_3674
 0x00fbb29c   dispatch stub (16 B)      OPD 0x11af4c0  FUN_35a4    ★ auth+client pair (FUN_2778 trampoline, see §3.3)
 0x002a6c34   FUN_002a6908              0x002aec38    FUN_12288   n013a/n020a
 0x002a6e90   FUN_002a6908              0x002aec38    FUN_12288
 0x00f0188c   FUN_00f01810              0x00f2ddd0    FUN_15384
 0x00f0189c   FUN_00f01810              0x00f01668    FUN_1546c
 0x00f018a8   FUN_00f01810              0x00f00a00    FUN_1510c
 0x00280860   FUN_002806f0              0x00f0ea88    FUN_2e4c    guard wrapper
 0x0028137c   FUN_00280b68              0x00f0ea88    FUN_2e4c
 0x002623f4   FUN_002620d8              0x002666c8    FUN_15180
 0x00262528   FUN_002620d8              0x002666c8    FUN_15180
 0x00f098dc   FUN_00f09854              0x00f2e20c    FUN_a1bc
 0x00271450   FUN_00270e00              0x0026cc10    FUN_a2bc
 0x002770c4   FUN_00276e88              0x0026cc88    FUN_a4a0    "null member??"
 0x00275b34   FUN_00275a90              0x0026cc10    FUN_a644
 0x00275c34   FUN_00275a90              0x0026cc10    FUN_a6f0
 0x00275784   FUN_00275718              0x0026cc88    FUN_a864
 0x002758c0   FUN_00275718              0x0026cc88    FUN_a91c
 0x00811bf4   FUN_00811ab0              0x0026cc10    FUN_12ca4
 0x0080f4ec   FUN_0080f490              0x0026cc88    FUN_12d84
 0x003c6eb8   (no function)             0x00333608    FUN_1484c
 0x009e97d4   (no function)             0x00270e00    FUN_12920
 0x009c4264   (no function)             0x00246950    FUN_12a18   ★ replay path
 0x00f1c688   FUN_00f1c5a0              0x00effa0c    FUN_1253c   (staged later)
 0x00f1bdfc   FUN_00f1bcfc              0x00effa0c    FUN_125e0   (staged later)
```

Note: the game ELF is stripped (`FUN_xxxx` = SN-compiler names, symbols absent). "Game fn" is
the function *containing* the hook site; the *original callee* column shows what would have run
if the plugin hadn't replaced the call. Purpose tags come from strings the replacement passes or
neighboring evidence — unlabelled rows are logic-only wrappers that route through the game API
table `DAT_17f04` (saved originals / game services).

---

## 5. End-to-end timeline

1. **Launch** — user runs the MGO2PC launcher EBOOT (`EBOOT.ELF`): downloads & installs
   `NPMG00020` (game EBOOT + `plugin.sprx` + stage files), then boots the game (RPCS3 or CFW).
2. **Game boot** — the MGO2PC-patched `MGO2.ELF` starts; its module-init table
   (`0x11e7358 …`) reaches `FUN_10230` → `FUN_10360`.
3. **Stage 1** — `FUN_10360` calls `_sys_prx_load_module("/dev_hdd0/game/NPMG00020/plugin.sprx")`
   (sc 480) then `_sys_prx_start_module` (sc 481). Kernel maps + starts `MGOPlugin` in-process.
4. **Stage 2a/2b** — plugin `module_entry` (vaddr 0x0) runs:
   - `FUN_784` bulk-patches ~70 instruction/pointer words (NOPs, `blr`, constants, table repoints);
   - the 60 inline `bl FUN_2ac8` entries rewrite 60 game call sites `bl <orig>` → `bl <plugin fn>`;
   - `FUN_2778` relocates the game's 16-byte dispatch stub `0xfbb29c` into plugin space and
     installs `FUN_35a4` (the `auth`-token/`client`-version pair builder) over it;
   - `FUN_19a8` flips the `0x13258` stub to `li r3,1` and self-tests the dbg-mem primitives.
5. **Runtime** — every time the game reaches a hooked call site it runs the plugin replacement,
   which calls back into game services via `DAT_17f04`, swaps resources/UI/rules, and — at the
   hooked `0x262e9c` path (arena init, reached from boot routine `FUN_000106a0`) — (re)builds
   the **device credential** (`FUN_3520` → `buildTokenGlobal` → global `DAT_183bd`: media id +
   MAC + embedded keys, Salsa20). When the game later dispatches through stub `0xfbb29c`,
   `FUN_35a4` attaches that token as the **`auth`** field (with `client=2.18.0`) to the object
   being built — the record the game's own login code serializes and transmits (see
   `FINGERPRINT.md`).

---

## 6. Why this design (functional notes)

- **Signature-safe payloads**: everything MGO2PC adds ships as *signed SELFs* (`plugin.sprx`,
  and the re-signed EBOOT). The kernel's own loader does the mapping — no unsigned memory tricks.
- **In-process, post-boot patching instead of a pre-patched game**: the game EBOOT is only
  lightly touched (loader + init table); the large body of changes lives in the plugin, so
  fixes/servers don't require re-signing the 16 MB game ELF.
- **Debug syscalls require the runtime environment to allow them** (CFW on a real PS3, or RPCS3
  in its default "debug" posture). On a stock OFW console the dbg writes fail and the plugin
  degrades (its `FUN_19a8` probe checks this and reports failure).

---

## 7. Artifacts

| File | Contents |
|---|---|
| `FINGERPRINT.md` | what each component reads/transmits (MAC, media ID, IDPS, tokens) |
| `eboot_analysis/plugin_analysis/hook_map.txt` | 60-row machine-readable hook table |
| `mgo2_decomp/INDEX.txt` | hook sites → containing game fn; original targets |
| `mgo2_decomp/site_*.c` | decompiled game functions containing hook sites |
| `mgo2_decomp/target_*.c` | decompiled original callees that hooks replaced |
| `mgo2_decomp/plugin_loader.txt` | `FUN_10230` trampoline decompile + caller map |
| `plugin_decomp/f_784_FUN_00000784.c` | static patch list (decompile) |
| `plugin_decomp/f_2ac8_FUN_00002ac8.c` | hook writer (branch-word encoder) |
| `plugin_decomp/f_19a8_FUN_000019a8.c` | 0x13258 stub-flip + dbg self-test |
| `plugin_decomp2/f_*.c` | 87 decompiled plugin functions (incl. all replacements) |
| `eboot_analysis/plugin_analysis/plugin_dec.sprx` | decrypted `MGOPlugin` ELF |

*Method note: hook sites were extracted from the plugin's inline table
(`lis/ori r3` game addr + `lwz r4` plugin slot + `bl FUN_2ac8`), decoded to exact `bl` writes,
and each game address cross-checked against `MGO2.ELF` (original `bl <orig>` + NOP present).
Syscall numbers verified against the clienthax PS3 syscall table.*
