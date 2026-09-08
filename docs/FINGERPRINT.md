# MGO2PC — Fingerprinting Research Notes

Reverse-engineering research on what the MGO2PC stack (launcher → game plugin → game)
collects from the console, and what actually leaves the device.

> Status: static analysis only. Everything below was read out of binaries/decrypted files,
> not from a live capture. "Verified" = directly observed in code/disassembly; "inferred" =
> reasoned from adjacent evidence. No network interception was performed.

---

## 1. TL;DR

| Component | Identifiers collected | Transmitted? |
|---|---|---|
| **Launcher** `EBOOT.ELF` (v00.30, `NPMG00010`) | Console **IDPS** (LV2 syscall 867 `sys_ss_appliance_info_manager`), console-ID syscall 870, PSN `act.dat` | **No.** IDPS/act.dat only feed *local* ECDSA/RIF license generation under `/dev_hdd0/exdata`. Network is GET-only file downloads. |
| **Game plugin** `plugin.sprx` ("MGOPlugin") | Console **MAC address** (`cellNetCtl`), **media ID** (syscall 879 `sys_ss_media_id`), embedded key material | **Not directly.** The plugin has *zero* `sys_net` syscalls. It bakes MAC+media-ID+keys into a Salsa20-derived token and **patches the running game** so the game's own sockets send it. |
| **Game** `MGO2.SELF` / `MGO2.ELF` (`NPMG00020`) | Runs the original Konami MGO2 login/network client (`?rule=`, `&gid=`, `&password=`, `&pid=%u`, HTTP engine) | **Yes** — this is where the actual login/auth bytes go to the MGO2PC servers. Exact packet format not yet extracted (game-side analysis outstanding). |

Server-visible, in any case: your **IP** and the fixed User-Agent
`Mozilla/5.0 (PLAYSTATION 3; 1.00)` (launcher downloads). The game plugin also references
`client` / `2.18.0` / `Ver.2.18.0` version strings and an `auth` token.

---

## 2. Files analyzed

| File | What it is | How obtained |
|---|---|---|
| `EBOOT.ELF` (3.2 MB) | MGO2PC Launcher v00.30 by TriggerHappy, based on PKGI-PS3 (PSL1GHT, stripped, section names erased) | Provided in workspace |
| `NPMG00010.zip` payload | Launcher self-update (EBOOT.BIN + PARAM.SFO/ICON0) | Downloaded from `files.mgo2.net` (now `mgo2pc.com/files/…`); live version `00.35` |
| `mgo.json` | Manifest of 1,471 game files (~8.1 GB) under `/dev_hdd0/game/NPMG00020`, with `[path, md5, mtime, size, …]` | Downloaded `mgo.zip` |
| Backblaze samples (`plugin.sprx`, `MGO2.SELF`, …) | Files the launcher downloads from `https://f002.backblazeb2.com/file/MGO2PC-CUSTOM-LAUNCH<path>` | Downloaded; md5 verified vs manifest |
| `plugin.sprx` → `plugin_dec.sprx` (152 KB) | Game plugin "MGOPlugin" | Decrypted with `scetool 0.4.0` + key set in `Desktop/MGO2/data/` |
| `MGO2.ELF` (19.6 MB) | Decrypted game executable (already on Desktop) | Provided in workspace (md5 of `MGO2.SELF` matches manifest) |
| Lobby stage files | `r_onlinelobby/resident_custom.dar`, `resident.dlz` | Downloaded; decrypted with `mgo2_crypto` scheme → `msf_def_mgo.mdn` font / `segs` DLZ archive → raw-deflate → 6.8 MB unpacked geometry (see §7) |

---

## 3. Launcher (`EBOOT.ELF`) — the fingerprint question, verified three ways

**Verdict: the launcher does not transmit IDPS or any device data.**

1. **Strings.** The download engine literally contains `"starting http GET request for %s"`
   and `"setting http offset %ld"` (HTTP Range resume). Every URL is a GET endpoint
   (`files.mgo2.net`, `f002.backblazeb2.com`). No POST/PUT/upload strings exist.
2. **Syscall census.** All 57 `sc` sites enumerated (`syscalls.txt`). Network activity is
   plain outbound `sys_net` client sockets inside the bundled libcurl. The IDPS read
   (syscall **867**) and console-id (**870**) sit in the DRM/RIF path; the IDPS buffer flows
   only into local ECDSA / RIF-generation code, never toward a socket.
3. **Curl option scan.** Constants like `CURLOPT_POSTFIELDS` / `CURLOPT_UPLOAD` do appear in
   the binary — but only inside libcurl's own option-dispatch table (it contains every
   option constant regardless of use). The launcher's own `curl_easy_setopt` callers only set
   URL + resume offset + write callback.

DRM detail (stock PKGi behavior): reads `act.dat` from `/dev_hdd0/exdata`, reads the console
IDPS via syscall 867 right after printing `"Reading IDPS …"`, and writes `.rif` licenses to
`/dev_hdd0/exdata/…` so signed content runs on real consoles. All local.

Also observed (not this ELF): the custom MGO2PC `RPCS3.exe` phones
`mgo2pc.com/api/v1/update-emu/…/…` on its own — that is the emulator's updater with an
install token, unrelated to the launcher.

---

## 4. Game plugin (`plugin.sprx`) — device credential construction

### 4.1 What it is
A 152 KB PPC64 SPRX, module name `"MGOPlugin"`, loaded alongside the game. Adds the MGO2PC
custom maps (22 names + rule descriptions), replay viewer
(`/dev_hdd0/game/NPMG00020/USRDIR/o/replay`), HUD font refs (`msf_def_mgo`, `pipomask`),
and the online-client bits (`client`, `2.18.0`, `Ver.2.18.0`, `SaveMGO`, `auth`).

### 4.2 Complete syscall list (the plugin has NO network syscalls)
| Syscall | Number | Use |
|---|---|---|
| `sys_ss_media_id` | 879 | Read console/media ID into a buffer (×2 sites) |
| `sys_dbg_read_process_memory` | 904 | Cross-process memory read (patch engine) |
| `sys_dbg_write_process_memory` | 905 | Cross-process memory write (patch engine) |
| `sys_process_authenticate_program_segment` | 8 | Re-auth after memory writes |
| `sys_process_getpid` | 1 | Own pid |
| `sys_timer_usleep` | 141 | Sleep |

### 4.3 Auth-string chain (all addresses = plugin vaddrs, `vaddr = file-0xf0`)
1. **`FUN_00003750`** `formatMAC()` — MAC via `cellNetCtl`-style `FUN_00016564(2, buf)`;
   success → `"%02X:%02X:%02X:%02X:%02X:%02X"`; failure → literal fallback
   `"00:00:00:00:00:01"`.
2. **`FUN_00003810`** `buildAuthStr()` — `sys_ss_media_id(0x10001, buf)` (syscall 879,
   wrapper `FUN_00003700`) and tests whether the result starts with `"MGO"`.
   - "MGO" prefix present → auth string = `"%s;%s"` = `<media_id>;<MAC>`.
   - otherwise → base64-decode an embedded 30-byte blob (`DAT_17874`), walk a TLV container,
     find records matching the three embedded base64 markers, and produce
     `"%s;%s;%s"` = `<key1>;<key2>;<MAC>`.
   Embedded base64 markers:
   - `ZCkjPjonXyFkKT85OitcaSc7KC07L1Yj` → 24 bytes
   - `ZCkjPjonXyFkKT85OitcaSM+IhkrPFgnJw==` → 25 bytes
   - `ZCkjPjonXyFkNCM+YT1CLy8=` → 17 bytes
   (they share a 6-byte prefix — related key material)
3. **`FUN_00003d94`** `buildTokenGlobal()` (runs once, flag `DAT_183bc`) — `time()` seed +
   three `rand()` calls via host function pointer `*DAT_17840`, then Salsa20
   (`FUN_00004164` = Salsa20 key/IV init using the `"expand 32-byte k"` constant at
   `0x16f80`; `FUN_0000588c` = derive) → global token string `DAT_183bd` formatted
   `"%s\t%s"` (two tab-separated hex values).
4. **`FUN_00003520`** — module callback registered from the entry stub table (entry @0xa8);
   calls a host import (`*(*(0x17f04+0x44))()`) then `buildTokenGlobal`. → the token is
   produced when the host (launcher/RPCS3/game loader) invokes this callback.

### 4.4 Patch engine (how the credential is delivered)
- **`FUN_00002628`** = read wrapper: `sys_dbg_read_process_memory(getpid(), addr, buf, len)`
  + syscall 8 re-auth. **`FUN_000026d0`** = matching write wrapper.
- **`FUN_000019a8`** = patcher. Two modes:
  - small pid (RPCS3-like): `dbgWrite(0x13258, [li r3,1 = 0x38600001], 4)` — overwrites game
    code — then calls `*DAT_17830` and expects 1.
  - large pid (real PS3 path): probes 1-byte read/write at game address `0xfc0878`.
- **Game targets** (verified in decrypted `MGO2.ELF`, code segment 0x10000..0x118e348):
  - `0x13258`: a 2-instruction stub `addi r3,-1 ; blr` (**returns -1**); no direct branch
    targets it (reached indirectly). The plugin forces it to return **1** = makes some game
    availability/authorisation check succeed. The game contains 17 identical `return -1; blr`
    stubs (unimplemented/blocked functions).
  - `0xfc0878`: one byte inside a game string table ("FACE CAM", "KEROTAN", "OCTOCAM"…,
    i.e. MGO2 head/face cosmetic IDs — the 1-byte probe there is a generic self-test of the
    dbg-write path, not a semantic write).
- **How the plugin installs itself into the game** (module entry stubs @ `0x0..0x2ac0`): most
  stubs tail-call `FUN_00002ac8`, which computes a branch-relative displacement and
  `dbgWrite`s it — i.e. the plugin **overwrites `bl` instructions inside the game's code**
  at load time (`bl <original>` → `bl <plugin fn>`, 60 sites). One *different* hook uses a
  second primitive, `FUN_00002778`, which **relocates a whole 16-byte game stub** into plugin
  space and writes a jump to the plugin function over the original — the `0xfbb29c`
  auth/client-pair hook (step 4 below). Verified example targets in `MGO2.ELF`: `0x262e9c` =
  `b 0x26c6d8`, `0x262e78` = `b 0x26c9e0` … compiler-generated virtual-dispatch trampolines
  (`stw`+`b` thunks). Overwriting the branch redirects the game's virtual call to the
  plugin's own function: the plugin **hooks/overrides game C++ methods**.

### 4.5 Functional description of the token flow (see also §5)

The full chain, instruction-verified end to end:

1. **Boot: the game loads the plugin into its own process.** `MGO2.ELF` startup code
   (`FUN_10360`, tail at `0x10494–0x104d4`) calls `_sys_prx_load_module` (sc 480) +
   `_sys_prx_start_module` (sc 481) on `/dev_hdd0/game/NPMG00020/plugin.sprx`. The plugin
   shares the game's address space and debug rights, which is what lets it rewrite the game.
2. **Boot continues into the arena init where the token hook sits.** Right after the loader,
   the boot routine `FUN_000106a0` calls `FUN_00262d10` (an arena/region init that receives a
   `0x1c0000`-byte region). Inside `FUN_00262d10` the game originally executed `bl 0x26c6d8`
   at `0x262e9c`; the plugin replaced that call site with `bl FUN_00003520`.
3. **Token (re)build on that hooked path.** `FUN_00003520` first re-enters the game through
   the plugin's resolved game-API slot `[DAT_17f04+0x44]` (a game service pointer resolved at
   runtime; identity **[U]**, consistent with letting the original behavior run), then calls
   `FUN_3d94` `buildTokenGlobal()` → the **MAC + media-ID + embedded-keys → Salsa20** token
   is written to the plugin global `DAT_183bd` (two tab-separated hex values,
   `time()`+`rand()` salted).
4. **The token is read by the auth/client pair hook at game `0xfbb29c`.** Plugin
   `FUN_35a4`, installed over the game's 16-byte late-bound dispatch stub `0xfbb29c` by the
   `FUN_2778` trampoline primitive, runs the original stub (relocated into plugin space at
   `0x3574`) and then — when the caller passes a non-null second argument — calls the same
   dispatch twice more with the string-pair arguments `{"auth", &DAT_183bd}` (the token)
   and `{"client", "2.18.0"}` (verified strings at `0x16ea0/0x16ea8/0x16eb0`). In other
   words: every time the game builds one of these objects through `0xfbb29c`, the plugin
   attaches its **device credential as the `auth` field** and the client version as `client`.
5. **The plugin also forces the game's `0x13258` availability check to return success**, so
   the game proceeds with the online flow.
6. **The game sends.** The game (which owns all sockets/HTTP) serializes that record through
   its option/rule record appenders (`0x26cc10`/`0x26cc88` family — themselves hooked) into
   the login/account handshake request it builds with the stock Konami parameter builder
   (`?rule=`, `&gid=`, `&password=`, `&pid=%u`, …) and sends to the revival server.

Confidence: steps 1–5 are instruction/control-flow verified **[V]**; step 6's exact wire
format (which field the `auth` value lands in, and the precise request) is **[I]** — the
consumer of the `0xfbb29c`-built object is a runtime-resolved dispatch (OPD slot
`0x11af4c0 → 0xfc1a50`, code=0 in the file), so the *name* of the game method that serializes
it cannot be read statically. Everything between "token built" and "bytes on the wire" lives
in the game, not the plugin (the plugin has zero network syscalls — §4.2).

---

## 5. Where the identifiers end up (functional level)

| Identifier | Final destination |
|---|---|
| Console **IDPS** / act.dat | Launcher-local RIF generation only — never sent (verified: GET-only strings, client-socket-only syscalls, IDPS→local-crypto data flow) |
| Console **MAC** | Folded into plugin auth string → Salsa20 token → global `DAT_183bd`; read by `FUN_35a4` (hooked at game `0xfbb29c`) and attached as the **`auth`** key when the game builds the object that its login record is serialized from |
| Console **media ID** (`sys_ss_media_id`) | Same path as MAC (part of the auth string / "is this MGO" check) |
| **auth token** (`<key>;<key>;<MAC>`-derived, hex) | Written to plugin global `DAT_183bd` on the hooked arena-init path (`FUN_3520` → `buildTokenGlobal`); consumed at `FUN_35a4` which injects it as the **`auth` field** (with `client=2.18.0`) into the object built through game dispatch stub `0xfbb29c` — the record the game's login/account handshake then serializes and sends to the MGO2PC servers |
| Console **PSID** | The *game itself* reads it via syscall 872 `sys_ss_get_open_psid` (sites `0xd6eb34`, `0xd6faa4`) — original Konami auth code |
| **MAC hex** (no colons) | Konami's embedded HTTP/UPnP lib formats `%02X%02X%02X%02X%02X%02X` near `"KONAMI"`/`mrd_upnp_socket.c` (`0x1000228`) for its requests |
| **IP + UA** | Visible to servers whenever the launcher (GET, UA `Mozilla/5.0 (PLAYSTATION 3; 1.00)`) or the game (login) connects |

### How it gets from the console to the server (what we verified in `MGO2.ELF`)
- The game is the only component with a network path: it imports `cellHttp` and embeds its
  own HTTP/socket stack (source tags `uupdate.cc`, `uupnp.cc`, `mrd_upnp_http.c`,
  `mrd_upnp_socket.c`), plus the original MGO2 login parameter builder
  (`?rule=`, `&lang=`, `&r=`, `&gid=`, `&password=`, `&pid=%u`, `%02x` hex encoder at
  `0xfe7868–0xfe7a00`) and `mgo_connect_server_by_index() index=%d, type=%d`
  (`0xff9008` — server-list connect by index/type).
- **No MGO2PC/revival hostname is baked into `MGO2.ELF`** — the only HTTP URL default found
  is the dev string `http://127.0.0.1/` (+ `?prod=%d` version check, `0xfe8718/0xfe8728`).
  So the real destination is supplied at runtime (RPCS3 DNS/hosts redirection or launcher
  config) — consistent with the MGO2PC deployment model where the game reaches the revival
  servers via hosts entries.
- **Confidence**: that the *plugin-derived credential goes out through the game's login
  handshake* is strongly supported but not yet byte-verified; the exact request content and
  destination host need the game-side decompile (or a live capture) to be named precisely.

So: the raw MAC/media ID/IDPS never appear in a packet *as such* — the plugin derives an
encrypted/hashed credential from MAC+media-ID+keys and hands it to the game. The delivery
point is precise now: token global `DAT_183bd` is produced on the boot-time hooked arena init
(`0x262e9c` → `FUN_3520`), and it is consumed by the `FUN_35a4` hook on game dispatch stub
`0xfbb29c`, which attaches `{"auth": <token>}` and `{"client": "2.18.0"}` to the object the
game is building through that stub — the same record family the hooked appenders
(`0x26cc10`/`0x26cc88`) serialize. The game's login/account requests to the MGO2PC servers
are the exfil path for the device-bound identity; the game separately still reads PSID and can
send a plain MAC-hex value via Konami's own HTTP lib.

---

## 6. Server / network map

- `http://files.mgo2.net/…` → 301 → `https://mgo2pc.com/files/…`
  - `latest.txt` (version), `mgo.zip` (manifest), `NPMG00010.zip` (launcher update)
- `https://f002.backblazeb2.com/file/MGO2PC-CUSTOM-LAUNCH<path>` — public Backblaze bucket
  hosting all 1,471 game files (MGO2.SELF, plugin.sprx, stage data, MPO+THEME.dbm, …)
- Game-side: original Konami MGO2 client with HTTP engine and login parameters
  (`?rule=`, `&lang=`, `&r=`, `&gid=`, `&password=`, `&pid=%u`, `%02x` hex encoder) in the
  game string table around `0xfe7868–0xfe7a00`; HTTP/updater strings at `0xff3a00–0xff3e80`
  (`GET`, `Range`, `bytes=%d-`, `httpcache`, `dl/p/ar/…`, `uupdate.cc`, `uupnp.cc`).
- Separate: custom `RPCS3.exe` phones `mgo2pc.com/api/v1/update-emu/<token>/2` (emulator
  self-update).

---

## 7. Stage files — the `segs` (DLZ) inner layer, cracked

The earlier "second layer" on the stage archives is **not encryption — it is raw-DEFLATE
compression per 16 KB chunk** inside the `segs` container (no zlib header, which is why a
`0x78 0x9c` scan found nothing). Format (verified by parsing + confirmed against
cipherxof/Haven's `Formats/Dlz/*`):

- File = consecutive `segs` **segments** located every `0x20000` bytes.
- Segment header: `"segs"`, flag u16, chunk-count u16, decompressed-size u32 (BE),
  compressed-size u32 (LE quirk).
- Index: chunk-count × `{sizeComp u16, sizeDecomp u16, chunkOffset u32}` (offset relative
  to segment start, minus 1).
- Each chunk = one 16 KB (0x4000) decompressed page, raw-DEFLATE compressed.

Result for the lobby file `o/stage/r_onlinelobby/resident.dlz`
(`resident.dlz` 3,801,088 B → mgo2-crypto layer → `segs` container):

| Stage | Size | Content |
|---|---|---|
| outer crypto (mgo2_crypto scheme) | → 3,801,088 B | `segs` DLZ container, 29 segments |
| raw-DEFLATE chunks | → **6,832,944 B** | lobby stage data: page-organized vertex/geometry streams (repeated `c9 73 c8 73 ff ff ff ff`-style vertex runs; 16 KB pages), no text/name table |

So the "encrypted payload" inside the downloaded stage files is fully decryptable and
decompressible with the two public schemes (oct0xor/mgo2_crypto + Haven's DLZ parser) — the
extracted blob is the raw lobby geometry data the game loads into memory.

## 8. Open items / honest caveats

- **Exact login packet bytes are NOT yet extracted.** The request is produced inside
  `MGO2.ELF` (the game), not in the plugin. Confirmed functional chain: plugin hooks game
  method thunks (e.g. branches at `0x262e78/0x262e9c`) → token built on hook call
  (`FUN_3520` → `buildTokenGlobal` → `DAT_183bd`) → token attached by the `FUN_35a4` hook at
  game `0xfbb29c` as the `auth` field (`client=2.18.0` beside it) → game proceeds past the
  `0x13258` check → game's own login code (Konami param builder +
  `mgo_connect_server_by_index`) sends the handshake. Remaining for byte-exactness: the
  name of the runtime-dispatched consumer behind stub `0xfbb29c` (OPD slot `0x11af4c0` is
  runtime-populated), and the request assembly inside the game (needs a live capture).
- **Server destination**: no revival hostname exists in `MGO2.ELF`; only dev default
  `http://127.0.0.1/`. The runtime target (mgo2pc.com via RPCS3/hosts/launcher config) was
  not observed in a live session.
- All conclusions are static. A live packet capture during login (custom RPCS3 + proxy, or
  console with a router tap) would confirm the exact wire format and destination.
- The plugin writes via `sys_dbg_*` with its own pid; whether it runs in the game process or
  a sibling (launcher/RPCS3) process on real hardware was not fully resolved — the hook
  targets are game addresses either way.
- The plugin's host-imported function pointers (`DAT_17830`, `DAT_17840`, `DAT_17844`,
  table at `0x17f04+0x44`) resolve to the loader's exports at runtime; their identities were
  inferred (rand/time-like) but not confirmed by name.

---

## 9. Artifacts

- `FINGERPRINT.md` — this research note
- `eboot_analysis/README.md` — launcher + manifest + stage-file analysis index
- `eboot_analysis/strings_all.txt`, `strings_launcher.txt`, `string_code_map.txt`, `urls.txt`, `syscalls.txt`
- `eboot_analysis/downloads/` — `latest.txt` (`00.35`), `mgo.zip`/`mgo.json`, `NPMG00010.zip`, `MPO+THEME.dbm`, verified CDN samples, decrypted lobby stage files
- `eboot_analysis/plugin_analysis/plugin_dec.sprx` — decrypted plugin (2.18.0, 152 KB ELF) — the previously analyzed version
- `eboot_analysis/plugin_analysis/plugin_dec_v2.elf` — decrypted **current** plugin (2.18.7, 205 KB ELF)
- `eboot_analysis/plugin_analysis/plugin_v2_delta.md` — old-vs-new plugin diff (see §11)
- `eboot_analysis/plugin_analysis/decompiled/f_*.c` — plugin decompiles (formatMAC 0x3750,
  buildAuthStr 0x3810, buildTokenGlobal 0x3d94, Salsa20 init 0x4164, dbg read/write
  wrappers, hook installer 0x2ac8, patch engine 0x19a8, media-id wrappers, …)
- `eboot_analysis/plugin_analysis/lobby_resident_dlz_unpacked.bin` — lobby DLZ fully
  unpacked (6.8 MB, raw geometry pages)
- `eboot_analysis/plugin_analysis/plugin_trace.txt`, `plugin_callers.txt`, `README_trace.md`
- Ghidra project `/tmp/plugin_ghidra` (program `plugin_dec.sprx`, PowerPC:BE:64:A2ALT-32addr)
- `scripts/` — helper scripts (`elf_*.py`, `find_xrefs.py`, `map_string_uses.py`,
  `self_info.py`, `mgo2_stage_decrypt.py`, `dlz_unpack.py`, `ghidra/*.java`,
  `GetAdaptersInfo.cs` — replicates the Windows `GetAdaptersInfo` enumeration order
  that RPCS3's `discover_ether_address()` consumes)

---

## 10. Addendum — emulator-side sources of the fingerprint (verified on this host)

Follow-up to §4, answering three questions against the `rpcs3` source clone and the
live MGO2 install in this workspace: **is the MAC the host machine's? where is the
media ID? are the keys auto-generated?**

### 10.1 The "console MAC" is the host PC's adapter MAC (default)

Verified in the rpcs3 clone: `np_handler::discover_ether_address()`
(`rpcs3/rpcs3/Emu/NP/np_handler.cpp:613`).

- `Derive MAC from PSID` (`cfg::_bool derive_mac_from_psid`, default **false**,
  `Emu/system_config.h:336`):
  - **false (default)** → read the host's real adapter MAC:
    `GetAdaptersInfo` on Windows (takes `info[0]` = **first adapter in enumeration
    order**, not the active one), `getifaddrs` on macOS/BSD, `SIOCGIFHWADDR` on Linux.
  - **true** → synthesize from `Console PSID`: first 6 bytes of the PSID,
    `byte0 = (byte0 & 0xFE) | 0x02` (locally-administered unicast MAC).
- Delivery to the game: `cellNetCtlGetInfo(code=CELL_NET_CTL_INFO_ETHER_ADDR /* 2 */)`
  copies `np_handler::ether_address` (`Emu/Cell/Modules/cellNetCtl.cpp`); the plugin's
  `FUN_00016564(2, buf)` → `formatMAC` reads it and formats `%02X:%02X:…` (fallback
  `00:00:00:00:00:01` only if the call fails).

**This host, verified live** (`scripts/GetAdaptersInfo.cs`, compiled with the .NET
Framework csc):

| # | Adapter | MAC |
|---|---|---|
| **[0] ← used** | Realtek PCIe GbE Family Controller | `6C-02-E0-5A-7F-7A` |
| [1] | Bluetooth Device (PAN) | `20-4E-F6-71-4C-F6` |
| [2] | Realtek RTL8822CE 802.11ac Wi-Fi | `DE-0C-29-4A-14-1A` |
| [3][4] | Microsoft Wi-Fi Direct Virtual Adapters | `22-4E-…`, `FE-4E-…` |

So the fingerprint MAC = **`6C:02:E0:5A:7F:7A`**. Details: OUI `6C:02:E0` is
registered to **HP Inc.** (MA-L); NIC part `5A:7F:7A`; it belongs to a **Realtek
PCIe GbE chip on an HP board**. Note it is the *disconnected* Ethernet adapter, not
the active Wi-Fi — `GetAdaptersInfo` enumeration order decides, not connectivity.

Live log evidence (`MGO2/log/RPCS3.log`): the game reads it repeatedly from the
`MGS4 MAIN` thread:
`cellNetCtl: cellNetCtlGetInfo(code=0x2 (INFO_ETHER_ADDR), info=*0xd00394b0)`.

Local MGO2 config (`MGO2/config/config.yml`): `Derive MAC from PSID: false` → host
MAC path active; `Console PSID: 0x0B0A9C5A3BDAEB85226099D6064FB816`
(high=8561373911397118002, low=15917819144873275107).

### 10.2 Media ID — where it comes from (stock vs MGO2PC fork vs PS3)

Read by `buildAuthStr` via `sys_ss_media_id(0x10001, buf)` (**syscall 879**), then
tested for an `"MGO"` prefix.

- **Stock RPCS3 (this clone):** syscall 879 is `NULL_FUNC` — `lv2.cpp:852` — it logs
  "Unimplemented syscall" and returns 0 **without writing the buffer**. The plugin
  zeroes its buffer first, so the value is all zeros → never `"MGO"` → always the
  embedded-keys branch (`key1;key2;MAC`).
- **MGO2PC custom fork** (`rpcs3.exe v0.0.39-18636-52539287`, savemgo-rebase7):
  implements syscall 879 (`MediaIdRva = 0x32F9100` in the spoofer's anchors) and
  serves a **32-hex install ID** persisted at `%APPDATA%\DigitalData\id`, randomly
  generated once and only regenerated if the file is deleted (`mgo2-id-spoofer`
  `MediaIdFile.cs`). So on the fork it is a per-install random ID, not a real
  console/disc media ID. Fork log shows the calls: `⁂ sys_ss_media_id [2]`.
  Because the value is hex (0-9a-f), it can **never** start with `"MGO"` — the
  `<media_id>;<MAC>` branch is unreachable on RPCS3.
- **Real PS3:** the hypervisor returns the console's actual media ID; only there can
  the `"MGO"`-prefixed branch trigger.

### 10.3 The auth keys are static (hardcoded), not auto-generated

The keys are **fixed blobs embedded in `plugin.sprx`** — identical for every client;
that is what the server verifies. `buildAuthStr` base64-decodes `DAT_17874` (30
bytes), walks the TLV container (`FUN_000166ec`/`FUN_00016724`/`FUN_0001667c`) and
pulls the records matching the hardcoded markers:

| marker (base64) | bytes | hex |
|---|---|---|
| `ZCkjPjonXyFkKT85OitcaSc7KC07L1Yj` | 24 | `64 29 23 3e 3a 27 5f 21 64 29 3f 39 3a 2b 5c 69 27 3b 28 2d 3b 2f 56 23` |
| `ZCkjPjonXyFkNCM+YT1CLy8=` | 17 | `64 29 23 3e 3a 27 5f 21 64 34 23 3e 61 3d 42 2f 2f` |
| `ZCkjPjonXyFkKT85OitcaSM+IhkrPFgnJw==` | 25 | `64 29 23 3e 3a 27 5f 21 64 29 3f 39 3a 2b 5c 69 23 3e 22 19 2b 3c 58 27 27` (near-duplicate, unused in the final format) |

auth string = `<key1>;<key2>;<MAC>` (or `<media_id>;<MAC>` when the `"MGO"` branch
fires). The only randomized part is the **Salsa20 layer on top**: `time()` seed +
3×`rand()` → token global `DAT_183bd` (`hex\thex`) — per-boot salt over static
identity material.

### 10.4 MGO2PC fork identity seams (mgo2-id-spoofer)

- Runtime anchors in `rpcs3.exe` v0.0.39-18636-52539287 (image base 0x140000000):
  `MediaIdRva 0x32F9100`, `EtherAddressRva 0x3860C20` (the MAC getter,
  `lea rax,[rcx+0x1D0]; ret` — pattern `48 8D 81 D0 01 00 00 C3`),
  `VmBaseRva 0x5C0DBF0`, entry `0xEE4520`.
- IDPS constant served by `sys_ss_appliance_info_manager(0x19003)`:
  `00 00 00 01 00 89 00 0B 14 00 EF DD CA 25 52 66` (stock; DECR variants in debug
  mode). The spoofer replaces this 16-byte constant in the running process, patches
  the MAC getter to return a fake 6-byte MAC, writes `config.yml` PSID, and writes
  `%APPDATA%\DigitalData\id`.
- Spoof profile on this host (`%APPDATA%\mgo2-id-spoofer\default.json`, created
  `2026-09-04T11:26:10Z`): MAC `00:18:82:46:7D:2C`, media ID
  `a23e2c1d0338fac9c2cff85ee752d766`, PSID `76d01c365a1f3032dce7742d11156ee3`,
  IDPS `000000010089000b11a287bd2b76c241`. Only applied when launching through the
  spoofer; a plain `rpcs3.exe` run uses the host values above.

### 10.5 Bottom line for this host

| Piece | Unspoofed value | Source | Auto-generated? |
|---|---|---|---|
| MAC | `6C:02:E0:5A:7F:7A` | host `GetAdaptersInfo[0]` = HP/Realtek GbE (config: derive=false) | No — host-bound (spoofer patches the getter) |
| media ID | fork: `%APPDATA%\DigitalData\id` (32-hex install ID); stock: zeros | syscall 879 — implemented in fork, `NULL_FUNC` in stock | On the fork: yes, once, persisted |
| auth keys | static blobs above | embedded in `plugin.sprx` | No — universal |
| Salsa20 token | `hex\thex` per boot | time()+3×rand() over MAC+keys(+media) | Yes, every boot |

The MAC is the only genuinely host-bound identifier; the media ID is install-bound
on the fork; the keys are universal.

---

## 11. Addendum 2 — plugin update (2.18.0 → 2.18.7): no new fingerprinting

The `plugin.sprx` on the CDN / local install was updated (manifest `/plugin.sprx`,
md5 `f2aef9ace29d3a62f79ff8767b8354ac`). Re-decrypted and diffed against the version
documented above; full detail in
`eboot_analysis/plugin_analysis/plugin_v2_delta.md`.

**Bottom line: the device-credential chain is unchanged. Only the client version
string moved 2.18.0 → 2.18.7. The additions are local, user-facing features.**

- The on-disk SELF shrank (52,644 B) only because the new SELF stores compressed
  segments — the decrypted plaintext **grew** 152,312 B → 205,232 B (more code,
  not less).
- **Syscall census identical** — still zero network syscalls. Same set/count:
  879 media-ID ×2, 904/905 dbg read/write, 8 ×2, 141 usleep (+1 boot stub). No
  IDPS, no `sys_net`.
- **Auth chain byte-identical**: MAC `%02X:%02X:…` + `00:00:00:00:00:01` fallback,
  the three embedded base64 key markers, Salsa20 `expand 32-byte k`, `%s\t%s` token,
  `auth`/`client` injection — only `client`/`Ver.` now read **2.18.7**.
- **Game patch targets unchanged** (`0x13258`, `0xfc0878`, `0xfbb29c`, `0x262e9c` at
  the same code spots); the local `MGO2.SELF` it hooks is unchanged too (md5
  `1af7a441…` = manifest).
- **What's new (all local)**: import of `cellGcmSys` (GPU) only; a graphics/perf
  settings UI persisted to `/dev_hdd0/game/NPMG00020/USRDIR/o/online/settings.bin`
  (Shadow Distance / Stage Quality / Experimental Netcode / FPS Limit (PS3 only) /
  Anti Aliasing, with help text and a float defaults table); a new `Rush`/`Rush (DP)`
  map rule + `CheckRuleOpt_Branch = %02X` debug string in the map/rule table.
- No new identifier reads, no URLs/hostnames added, no new file paths beyond the
  local `settings.bin`. Exfil path is still the game's own login/HTTP code, and the
  transmitted credential is the same MAC+media-ID+keys Salsa20 token (now labelled
  `client` 2.18.7).
