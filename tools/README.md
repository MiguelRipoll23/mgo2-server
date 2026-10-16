# dev/tools

Everything here is run by hand. Nothing in this directory is mounted by `compose.yaml` or needed by
the running stack — that is `dev/runtime/`. These are the things you run to *change* what the stack
serves, to read something out of the game binary, or to watch a live session.

Two of them do real work against a console and are documented in full below: **`build_patch_round.py`**
(builds the in-game 1.0 → 1.36 patch) and **`testhk_editor.py`** (points the game at your server).
The rest are catalogued at the end.

Most tools need only Python 3. The patch tools additionally need `pycryptodome`
(`pip install pycryptodome`); `testhk_editor.py` needs the bundled `solideye/Solideye.exe`, which
is a Windows binary.

---

## `build_patch_round.py` — build and serve the in-game patch

Builds a complete, real 1.0 → 1.36 upgrade and stages it in the document root the probes serve, so
the game's **own** patch mechanism installs it. No file is placed on the console by hand.

The format itself is specified in **`dev/docs/PATCH_FORMAT.md`** — read that if you want to know
*what* is being built rather than how to run the builder. `dev/docs/PATCH_HANDOFF.md` is the
working state of the investigation.

### What you need first

1. **The 1.36 patch tree**, at `dev/PATCHES/PATCH 1.36/o/` (this path is the default; override with
   `--tree`). Its layout mirrors the install target, which is the point — what you see is where it
   goes:

   ```
   dev/PATCHES/PATCH 1.36/o/
       MGO2.SELF              the 1.36 executable
       dl/.p                  the DLT2 manifest describing the tree
       dl/.p.original.bak     Konami's pristine manifest, kept for reference
       dl/p/…                 659 data files in 8 subdirectories, ~2 GB
   ```

2. **The server armed.** `MGO2SERVER_CLIENT_VERSION=1.36` in `server.env`. This is the only switch —
   there is no separate patch toggle. Unset (or `1.0`) means the checkver reply is a single `0x00`
   "no update" byte and the builder will refuse to run.

3. **A regenerated checkver reply**, which is what actually offers the update:

   ```
   MGO2SERVER_CLIENT_VERSION=1.36 python3 build_checkver.py
   ```

### Building

The canonical full round — the real executable plus every data file:

```
MGO2SERVER_CLIENT_VERSION=1.36 python3 build_patch_round.py \
    --level 1.12 --blob --include-self --data all --zlib-level 1 \
    --mac 6d676f327365727665725f736c6f7438 --encrypt
```

Roughly 100 seconds; it streams, so memory stays flat regardless of the 2 GB input. It writes two
archives and two `.inf`s into `dev/runtime/www/us/mgo2/patch/1.36.0/` and **verifies each one by
replaying the client's read chain over it**, refusing to write anything that does not decode.

| flag | what it does |
| --- | --- |
| `--blob` | The authentic shape: one downloadable archive per record. `BLUS30109.<from>to<to>` carries the executable (it is disc-specific), `<from>to<to>` carries the data tree (shared across BLUS/BLES/BLJM). Without this you get one download per file, which is not how the real patch works. |
| `--data manifest\|root\|all` | What the generic archive carries besides the manifest. `manifest` is `.p` alone (fast, ~15 KB — good for testing placement); `root` adds the five files directly under `dl/p/`; `all` adds the whole 659-file tree. |
| `--include-self` | Include `MGO2.SELF`. Its source is `--self-file`, defaulting to the tree's own copy. |
| `--mac <hex>` | The key for the envelope's outer HMAC. **This must match what the client's key provider returns** — see the warning below. |
| `--encrypt` | Blowfish-CBC the archive body under the slot-7 key. Required; the client always stacks the decrypt filter. Implies `--container`. |
| `--zlib-level N` | Deflate level, default 6. Use **1** for `--data all`: the tree is mostly already-compressed audio, so 1 costs almost no ratio and several minutes less time. |
| `--level <ver>` | Which version-stamped subset to ship when not using `--blob`. Mostly vestigial for full rounds; it still names the round in the log. |
| `--dry-run` | Build and measure everything, print the plan, write nothing. |
| `--no-place` | Emit `MGO2.SELF` and `.p` as plain names so they land in `dl/p/` with everything else. This is **wrong** and exists only as the A/B control for the placement finding — see below. |
| `--no-compress` | Omit the zlib layer. Also wrong, also only a control. The client's inflate filter fails and the install silently extracts nothing. |

> **The `--mac` key is not universal.** It is whatever the client's crypto slot provider returns for
> slot 8. The value above is the ASCII string `mgo2server_slot8`, which is what *this* project's
> modified `MGO2.SELF` returns — a stock retail executable would demand Konami's real key. If you
> are working with a different client build, this value is yours to discover, not to copy.

### Placement, because it is the non-obvious part

Where a file lands is decided by its **name**, not by any flag. A plain name goes to `dl/p/<name>`;
a name of the form `..N/<rest>` goes N directory levels up from there first. The builder applies
this automatically:

```
MGO2.SELF  ->  ..2/MGO2.SELF  ->  o/MGO2.SELF     (what the loader reads)
.p         ->  ..1/.p         ->  o/dl/.p         (what the archive reader opens)
everything else                ->  o/dl/p/…       (its actual home)
```

A round built with `--no-place` installs all 660 files perfectly and accomplishes nothing, because
the two files whose location matters are both in the wrong directory. That failure looks exactly
like success.

### Deploying and verifying

The probes serve `dev/runtime/www` from a read-only mount, so new files are live immediately — but
the checkver reply is only regenerated by `build_checkver.py`, so re-run that if you changed
versions or hosts.

Always verify before running a client:

```
MGO2SERVER_CLIENT_VERSION=1.36 python3 verify_patch_round.py
```

This fetches over HTTP exactly as the client would, streams each archive back through the crypto
chain, walks the extract list, and hashes every member against its source file. It catches the
things the builder's own check cannot: a stale file in the document root, a truncated write, a
declared size that disagrees with what the server sends, or the probe answering with a fallback
page. Add `--no-content` to skip the per-member hashing (much faster), or `--local` to read the
document root directly rather than over HTTP.

### Before each console run

The install writes a lock file and removes it on success. **A failed install leaves it behind, and
it poisons every subsequent attempt** until deleted.

```
U="…/dev_hdd0/game/BLUS30109/USRDIR"
H="…/dev_hdd1/caches/BLUS30109_BLUS30109"      # note: RPCS3 gives hdd1 a per-title cache root
find "$U/o/dl" "$H/o/dl" -name .l -delete
mkdir -p "$U/o/dl/p/ar" "$H/o/dl/p/ar"
```

For a genuinely clean test also remove the previous run's output (`$U/o/MGO2.SELF`, `$U/o/dl/`) so
that what appears afterwards is unambiguously this run's work.

### Why an up-to-date client is not offered the patch again

The client's own version gate only checks that it is **at least** the record's FROM version — it
never compares against the TO version, so it cannot tell it is already current. `http_probe.py`
therefore parses the packed version out of the checkver POST and answers a bare `0x00` when the
client is at or above what the reply would offer. The decision is logged:

```
checkver body: b'19136512,BLUS30109,1180'
client is 1.36.0, offer is 1.36.0 -- answering 0x00, already up to date
```

The TO version is read back out of the reply itself, so this cannot drift from what
`build_checkver.py` wrote. If the body cannot be parsed it **fails open** — serves the offer
and says so in the log — rather than silently locking a 1.0 client out of patching.

### Reverting to normal play

Blank `MGO2SERVER_CLIENT_VERSION` in `server.env` and re-run `build_checkver.py`. The client
then gets the `0x00` "no update" byte and the version-check screen advances as usual.

---

## `strres.py` — disc string ids to text, in six languages

Resolves an MGO2 string-resource id to the sentence a player actually reads. Needs the lobby dump
produced by `gcx.exe -res` (see `dev/docs/ASSETS.md`); point `MGO2_STRRES` at it if it is not in
the default location.

    python3 dev/tools/strres.py 10337              # by index file
    python3 dev/tools/strres.py --id 2f0293 554    # by gcl group + id  -> "Friendly Fire"
    python3 dev/tools/strres.py --name CLAN_SUBJECT # by ELF resource name

The third form is the useful one when working from the binary: resource names in the ELF hash
(rot-5-add 24-bit, `0xD25D0`) straight to a record, so you never need to know the id.

**Why a tool rather than counting files.** The dump is index records plus a string pool, not a flat
list, and languages that share text share a pool entry — so walking records drifts. That drift
produced a run of ids uniformly wrong by 37 before the index format was decoded. `ASSETS.md` has
the format.

## `testhk_editor.py` — point the game at your server

MGO2's twelve server hostnames and two ports are **not** in `MGO2.SELF`. They are string resources
in `o/stage/lobby/scenerio.gcx`. But the loader opens **`d/testhk`** first, and if that file yields
a valid 16-byte header it takes the entire address table from the file and never reads the disc
data. So repointing the game needs no patching at all — you write one file, and deleting it
restores stock behaviour exactly.

This is the supported route and is confirmed working. `dev/docs/HOSTS.md` covers the alternatives
and why they are worse.

### Usage

With no arguments it opens a GUI; every field is editable and it shows what each slot feeds.
Headless is usually what you want:

```
python3 testhk_editor.py --no-gui \
    --point-at 192.168.1.200 \
    --http \
    --install "/mnt/d/rpcs3-…/dev_hdd0/game/BLUS30109/USRDIR"
```

| flag | what it does |
| --- | --- |
| `--point-at HOST` | Replace every host/authority in the table with yours. The one flag most runs need. |
| `--http` | Downgrade every `https://` to `http://`. |
| `--install USRDIR` | Encrypt and write `o/d/testhk` under that USRDIR, backing up any existing file. |
| `--plain FILE` | Write the **plaintext** form here instead — useful for inspection, not loadable by the game. |
| `--read FILE` | Start from an existing plaintext table rather than the stock one. |
| `--region US\|EU\|JP` | Which stock table to start from. Default `US`, which is what BLUS30109 selects. |
| `--gate-port` / `--stun-port` | Override the two ports. |
| `--skip-psn` | Set header bit 1. **On 1.36 this skips the PSN / MGO-Shop entitlement check** that otherwise blocks login with dialog `0x5012`. No known effect on the disc build. |
| `--flag-bit0` | Set header bit 0. Effect not established. |

### Things that will waste a live test

- **The file is encrypted.** `d/testhk` is opened through the path-keyed asset opener, which derives
  its key from the directory component of the path (`d`). A plaintext file simply will not load.
  This tool builds the plaintext and then calls `solideye/Solideye.exe` to encrypt it — a Windows
  binary, so on Linux you need WSL or Wine.
- **Ports in the file are big-endian**, which is the *opposite* of how the disc stores them. 15731
  is `3D 73`. The tool handles this; hand-editing is where it bites.
- **Failure is silent but safe.** A missing file, a short header, or any truncated read falls
  through to the disc table, which overwrites every slot. There is no half-loaded state — but also
  no error telling you your file was ignored.
- The disc build reads **12** records, 1.36 reads **16**. A table built for one is not the other.

---

## Everything else

### Patch tooling

**`build_checkver.py`** writes `dev/runtime/www/us/mgo2/patch/checkver.html` — the reply that
offers the patch — plus the release note beside it. It is the switch that turns the whole thing on:

```
python3 build_checkver.py                                 # 0x00 "no update", normal play
MGO2SERVER_CLIENT_VERSION=1.36 python3 build_checkver.py  # offer 1.0.0 -> 1.36.0
```

**Run it after any change to the version, host or keys.** Despite the `.html` extension the file
is binary, and the client POSTs to it rather than GETting it. The probes mount the document root
read-only, so the new reply is live immediately.

The reply carries the base URLs, the version-range records, and the two key blobs the client files
into slots 7 and 8 — **encrypted**, because the client's key fetch is a decrypt, not a copy.
`MGO2SERVER_PATCH_HOST`, `..._FROM`, `..._TO` and `..._DISC_ID` override the host, range and disc
id.

Announcing a record commits you to serving a real `.inf` and archive beside it: a record with no
files on disk makes the client fetch the probe's fallback page, which fails invisibly from the
server side. `build_patch_round.py` builds those.

**`build_torrent.py`** writes the `.torrent` for the P2P path into the docroot beside the payloads:

```
MGO2SERVER_CLIENT_VERSION=1.36 python3 build_torrent.py
```

**Re-run it after every `build_patch_round.py`.** The torrent is a hash of the payload bytes —
116,282 SHA-1 piece digests over the current ~1.9 GB round — so a stale one describes files that no
longer exist, and nothing warns you: the client fetches a perfectly valid torrent, fails every
piece, and it looks like a network fault. It prints an `info_hash`, which is how you check that the
two halves agree:

```
build_torrent.py    -> info_hash: ec9a1f0c8c63cd261da70f23aafb3ca47b5b1ffc
probe-bt at startup -> seeding info_hash ec9a1f0c…, 1905156512 bytes across 116282 piece(s)
```

Different hashes mean the served torrent and the seeder describe different bytes. `probe-bt`
recomputes from the payload files at startup, so restart it after rebuilding.

The P2P path has never completed a transfer: the client fetches and parses the torrent correctly
and connects to the peer, then stalls, and HTTP is what actually delivers a round. It is kept
working because that stall is an open question, not because anything depends on it.

| tool | what |
| --- | --- |
| `build_inf_stub.py` | The `.inf` layout and crypto envelope, importable and runnable standalone. `build_patch_round.py` builds on it. |
| `verify_patch_round.py` | Replays the client's read chain over what the **server is actually serving**, and hashes every extracted member against its source. |
| `build_torrent.py` | Writes the `.torrent` for the P2P path — **re-run after every `build_patch_round.py`**, see below. |
| `parse_dl_manifest.py` | Parses the `DLT2` manifest at `o/dl/.p` — names, sizes, versions, digests. |

### Reading the game binary

| tool | what |
| --- | --- |
| `extract_keys.py` | Pulls the crypto constants out of your own copy of `MGO2.elf`, with offsets. |
| `dump_error_table.py` | Regenerates `dev/docs/ERRORS.md` — all 556 codes and the sentence each produces. |
| `trace_dialog_paths.py` | Traces every `result code → dialogId` path in the binary. |
| `analyze_mgo2.py` | PPC64 BE instruction decoding and data-flow tracing, read-only. |

### Decoding live traffic

**Reading a pcapng capture of the game.** There is no `tshark` dependency — `pcap_conversations.py`
is a stdlib pcapng reader, and the UDP tools on top of it are the offline twin of
`FrameCryptoUtility` / `LzssUtility` / `MessageCodecUtility`, so a capture decodes to exactly what
the server would have made of it.

```
python3 pcap_conversations.py ../../docs/mgo2-game.pcapng          # every flow, by volume
python3 pcap_flow.py           ../../docs/mgo2-game.pcapng 5733      # one flow's wire bytes
python3 udp_flow_summary.py    ../../docs/mgo2-game.pcapng --peer IP:PORT
python3 udp_frame.py          ../../docs/mgo2-game.pcapng --peer IP:PORT --limit 40
python3 pcap_gameplay_log.py  ../../docs/mgo2-game.pcapng --limit 20  # as the server logs it
python3 pcap_roster_acks.py   ../../docs/mgo2-game.pcapng             # roster acknowledgements
python3 udp_loadout_bodies.py ../../docs/mgo2-game.pcapng             # the loadout records we write
python3 udp_briefing_flow.py  ../../docs/mgo2-game.pcapng             # briefing, reports, round open
python3 pcap_decrypt.py       ../../docs/mgo2-game.pcapng --type tcp  # the lobby channel, including 0x4313
```

`pcap_gameplay_log.py` is the offline twin of the gameplay server's own log.
Run `scripts/run-gameplay-local.sh` for a live session and this over a capture
of the same protocol and the two are line-for-line comparable — same
`[HH:MM:SS DBG] [udp:PORT] IN …` shapes, same hexdumps, same digest-gate
wording. Two things it will not invent: the server dials back to the endpoint a
handshake advertised, which a capture does not show, so `dial-back` reads as the
observed source; and the `answering N reliable records` line is written only when
the host's next outbound frame really carries those answers, so a host that
should have answered and did not gets a warning naming what went unanswered.


The session key is **derived from the two handshake counter bases**, not configured, so these work
on any capture of this protocol. A frame whose tail digest does not verify is reported as unparsed
rather than described — nothing here is printed that the bytes do not support.

`udp_framing_solver.py` and `udp_framing_table.py` reproduce the record-framing finding in
`dev/docs/protocol/UDP_GAME_CAPTURE.md` §2: two length conventions share the wire and the record
identifier is what tells them apart.

| tool | what |
| --- | --- |
| `pcap_conversations.py` | Stdlib pcapng reader; per-flow packet and byte counts, ordered by volume. The entry point for any capture. |
| `pcap_decrypt.py` | **Decrypts both channels of a capture** — the TCP lobby/game-server frames (XOR, HMAC-verified, Blowfish-decrypted payloads) and the UDP gameplay datagrams — with `--type` to pick one and `--json` for mechanical reading. `--json` is what the `0x4313` field crosswalk in `docs/protocol/GAME_SETTINGS_WIRE.md` is read against. |
| `pcap_gameplay_log.py` | **Replays a capture's gameplay session in the server's own log wording** — the same `IN`/`OUT`, hexdump, digest-gate, dispatch and `answering N reliable records` lines `scripts/run-gameplay-local.sh` writes, so a capture and a live run read against each other line for line. The capture is read as the host's side, because the host is the side this project implements. `--limit/--skip/--until` narrow it. |
| `pcap_roster_acks.py` | Which roster records a captured session acknowledged. Records are grouped by what they say, with the layout they parsed from and whether the peer answered; a capture whose session never keyed says so rather than reporting the unreadable frames as unanswered. |
| `export_acked_packets.py` | **Builds the packet-replay dataset the gameplay server's console lists.** Writes the host frames a captured client acknowledged — decoded, decompressed, deduplicated, wire bytes dropped — to `src/GameplayServer/PacketReplay/acked-packets.json`. A roster record is rewritten to name the fixed player `phildunphy23` with character id `2` (never the captured identity), and the live host's endpoints are written in at send time; the committed data therefore holds no real name or character id. Regenerating it needs no rebuild: the file is copied beside the executable. |
| `compare_roster_exchange.py` | Puts our own roster — read back out of the gameplay server's log — beside a captured host's, record for record, with the acknowledgement column on both sides. `--opening N` limits the captured side, since a whole round holds 236 frames carrying a roster identifier. |
| `probe_roster_byte_diff.py` | Diffs a live roster against a captured one **offset by offset**, printing only the runs where they disagree. Our side comes out of the gameplay server's log, so it reads the frames that actually went out; records are matched by type and length, so a name or an address of a different length does not shift the alignment. It is what says whether a roster difference is ours to fix or is all our own name and address — `docs/protocol/UDP_GAME_CAPTURE.md` §8.10. |
| `probe_client_nat.py` | Reports what the **client's** NAT module learned, from a capture taken on the client's own host. The client's handshake mode is decided by one value — the P2P port its connection record publishes — and `0xd7d530` resolves anything outside `{0,1,2,16,144}` to "guest"; a record with no port resolves to mode 1. Nothing the server sends takes part, so the client is the only place to look. Decodes the game's own NAT protocol, which is **not standard STUN**: cookie `6c02e061`, opaque transaction id, attributes renumbered. `--port` is the client's P2P port, so the verdict line can be checked against what the mode actually was. |
| `pcap_flow.py` | One flow's packets as wire hex, for when the framing tools do not apply (the TCP lobby channel, STUN, DNS). |
| `udp_frame.py` | Decodes the peer-to-peer channel: header scramble, XOR chain, tail digest, LZSS, records. Prints messages with bodies. |
| `udp_loadout_bodies.py` | **Checks the loadout record the gameplay server writes against the capture.** Decodes every loadout body a capture's host sent with `udp_settings_semantics`, re-encodes each with a port of `LoadoutFramesUtils.TryWrite` using the recorded templates read out of the source, and prints every byte that differs — which is how the claim "only the bytes nothing names differ" is checked rather than asserted. |
| `udp_flow_summary.py` | Session identity, the derived key, and a message-type inventory with direction split and body lengths. |
| `udp_framing_solver.py` | The per-type vote behind the two-conventions finding. |
| `udp_framing_table.py` | Derives and validates the per-type length table; prints the frame counts each rule walks. |
| `udp_unresolved_probe.py` | The unresolved session records in time order, with what shares their frame. |
| `udp_pairing_probe.py` | Tests the `0x1x..`/`0x5x..` families as request/answer, both directions, with the latency distribution. |
| `udp_response_probe.py` | Measures which outbound records a captured host sent **only** after a client record, with the inbound type that preceded each. It is what says the match-start run is the data phase's one gated record and the beat is not; `--session-only` drops the tick stream. See `docs/protocol/DATA_PHASE_DELIVERY.md`. |
| `udp_join_replay.py` | Replays the opening exchange in time order with every record decoded, for diffing against the server's own answer. |
| `udp_briefing_flow.py` | **The briefing and the round lifecycle in one timeline**: the client's readiness reports (`e1`/`1d`, with the lobby slot they carry), the host's publication, the briefing countdown, the operations clock counting down, and the moment the round opens as the operations identifier's generation flips. Measured and written up in `docs/protocol/BRIEFING_FLOW.md`. |
| `udp_start_run.py` | **Checks the round's opening run against a capture**, record for record, by role rather than by identifier — the team array travels as `0x9001` in one capture and `0x1001` in the other, and the payload is six bytes in one and eight in the other, while the roles are the same thirteen. Decompresses the round-open frame, prints it, and says which records the host is missing, out of order, or writing that the capture does not. It also reports the one thing the two captures disagree about: the order of the run's records. `docs/analysis/replay-findings.md` §1.4. |
| `udp_session_records.py` | **Lists an MGO2 peer session's records in wire order**, with each record's identifier, length, fourth byte and body — the listing behind `docs/analysis/replay-findings.md` §5, where the six captures are read against each other. `--flags2` prints only the host's fourth bytes in order, which is how a stream's fourth byte is read as a rolling sequence rather than a constant; `--id 0x1a54`, `--from`/`--to` (seconds from the flow's own first datagram) and `--peer IP:PORT` narrow it. Reuses `udp_frame`'s decoder, so a frame whose tail digest does not verify is skipped rather than parsed. |
| `udp_state_vector.py` | **Reconstructs the operations stream's 89-byte state vector** from a capture: folds every opcode-`0x00` vector and every opcode-`0x02` write into the state a reader replaying the stream holds, then reports it per offset — which bytes move, which hold one value in every round, and what each capture's round-open vector carries. `--writes` lists every vector and every write in arrival order, which is what shows the result block and the round-over opcode. This is the tool behind `docs/protocol/STATE_VECTOR.md` and behind the state block no longer being a recorded blob. |
| `mgo2_record_schema.py` | **Read the 26-state schema `0x27E8B0` indexes out of `MGO2.ELF`.** Each descriptor carries a field count, a table of `(state offset, length)` pairs, and the size of the state block those fields address — which is the check that makes the read-out trustworthy, since each table ends exactly at its state's block (1248 bytes for `0x01`–`0x18`, 284 for `0x19`, 168 for `0x00`). The 24 states `0x01`–`0x18` are the session's slots: their blocks are consecutive 1248-byte allocations, which is what says the table is a *state* table and not the operations stream's opcode table. `--op 0x01` prints one state's fields and the flags legend. `docs/analysis/udp-record-dispatch.md` §3.6; the wire side of the same state is `docs/protocol/STATE_VECTOR.md`. |
| `mgo2_state_wire_check.py` | **Test the state schema against the record bodies a capture carries** — the measurement behind §3.6's answer that the presence-map parse is not the operations records' parse. It takes each body's first byte as a state index and asks whether `1 + ceil(count/8) + Σ(length × multiplier over the set bits)` equals the body's length, counting **distinct** bodies because a capture repeats one body hundreds of times. Over 406 144 bodies from three captures (177 179 distinct): 14 500 name an index the table has, 2 490 distinct (1.41%) fit *some* index, and **none of the operations identifier's 2 214 records fits** — neither the 2–3-byte clock and kill feed nor the 90-byte state record, whose own map would want 28 bytes. Reads `.pcapng`s whole with `udp_frame` (the listings cut a body at 24 bytes, which is exactly where the long cases live) and falls back to `tmpout/*.txt`. |
| `mgo2_static_use.py` | **Which function reads a given static.** The module reaches its statics as `offset(rN)` from a base a TOC slot holds, so the address is never materialised and only carrying the register finds the reader: this walks every function named by an OPD entry and tracks registers loaded from the TOC against the values the image stores in those slots. That is how the module's four stream-class vtables were tied to their constructors (`0x011A632C` → `0x263B70` and its siblings) — the class map of `docs/analysis/udp-record-dispatch.md` §3.4. Takes a little under two minutes over the whole image. |
| `p2p_vtable_scan.py` | **Find the peer-to-peer module's vtables and name their slots.** A vtable slot holds an OPD address, not a code address, so the scan walks the data section for runs of OPD pointers, dereferences each through its entry, and keeps the runs with a slot inside the module's code range. 11 of 3 418 runs reach it, including the module object's own vtable at `0x011A63C8` — whose slot 0 reads one byte and indexes the 26-state table, which is the state side of the split §3.6 draws between the wire's per-record handlers and the state they write. Resolve one vtable in detail with `ppc_vtable_dump.py`. `docs/analysis/replay-findings.md` §3.1. |
| `handshake_layout.py` | **Verifies the 44-byte handshake field by field against the wire bytes** and exits non-zero when the structure stops holding — the `16 + 6*count` identity, the magic, the tail digest, and that each entry's four bytes really are an address in dotted form. Every offset is pinned to the `MGO2.ELF` instruction that writes it, so the binary and the capture are checked against each other rather than a prose copy of one of them. Run it after touching `FrameBuilderUtility`; `--elf MGO2.ELF` also re-derives the two crypto constants from the binary and catches a decoder pointed at the wrong build. The layout itself is walked step by step in `docs/protocol/UDP_JOIN_FLOW.md` §3. |

| tool | what |
| --- | --- |
| `decode_settings.py` | Decodes a `0x4310` host-settings blob into its known fields. |
| `decode_stats.py` | Decodes a `0x4390` end-of-round stat report into labelled slots. |
| `watch_4390.py` | Live-captures and decodes stat reports (or any command) out of the lobby log. |
| `watch_command.sh` | Watches the lobby DEBUG log for a command id and captures its payload as it arrives. |
| `capture_logs.sh` | Appends every game-lobby container's log to a file that survives container recreation. `docker compose up -d --build` discards container logs, and two live sessions were lost that way. |

### Environment and checks

| tool | what |
| --- | --- |
| `ipswap_editor.py` | Editor for RPCS3's IP swap list — the host → IP redirect map. An alternative to `testhk`, and a worse one. |
| `stun_selftest.py` | Asserts the STUN reply format against a running responder. A regression there produces no error, just a hung game. |
| `check_deploy_source_addresses.py` | Parses every `deploy/` manifest and asserts the source-address and rollout invariants: each Service's traffic policy against its protocol, and that every `externalTrafficPolicy: Local` Service has a probe (`startupProbe` or `readinessProbe`) and the rollout settings that make `Local` survivable. Run it after touching anything in `deploy/` — see `deploy/README.md`. |
| `upnp_probe.py` | Answers the client's own UPnP discovery. |
| `field_scoreboard.py` | Derives the field-mapping coverage numbers from `dev/proto/` directly. Run it rather than quoting a number from memory. |
| `seed.sql` | Inserts the lobby rows, a test account and a news item. Required — an empty `lobby` table is a silent dead end. |
| `dnsmasq.conf` | A logging DNS server. **DNS is not needed to play** — this is for discovering which hostnames a different disc or region asks for. |
| `gcx/`, `solideye/` | Third-party binaries for the disc's own asset containers. `Solideye.exe` is what encrypts `d/testhk`. |
| `retired/` | Superseded implementations, kept because they document a working approach. |
