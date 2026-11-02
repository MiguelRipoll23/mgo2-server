# The `0x1001` family's kind records, read through the client's own handler

**What this file settles.** The run this host writes carries two records nobody
had named: the twenty-byte **close pair** (`07 0d 00 02 00 00 00 …`) and the
twenty-two byte **stage pair** (`07 0f 00 03 00 00 00 …`). Their bodies are now
the recording's, byte for byte, but their fields were `[U]`. This reads the
client's handler for the family, with Capstone over `MGO2.ELF`, follows the
registry entry that invokes it, and settles which byte of the record its branch
keys on — the question an earlier pass left open.

| source | path |
| --- | --- |
| image | `C:\Users\migue\Desktop\MGO2RE\MGO2.ELF` (19 615 992 bytes; the repo's tools default to `docs/MGO2.ELF`, which is not in the tree) |
| the handler's identity | `docs/analysis/udp-record-dispatch.md` §3.3.1 — the live registry's entry 2 carries tag `07` and OPD `0x011F1928`, whose code is `0x282D80` |
| the dispatcher, and what it consumes | `0x26E790` (`docs/analysis/udp-handlers-and-senders.md` §4) |
| the invoker | `0x27A180` (`docs/analysis/udp-record-dispatch.md` §3.3) |
| replay grammar | `docs/analysis/replay-console/rpdt.js`, `rosterPlayer` |
| bodies | `briefing-then-solo-game.pcapng`, t+9.780 |

The PC client's binaries were checked as a second opinion and are not one:
`mgo2.exe` (441 KB) is a launcher whose only stage string is `/game/stage/`,
`savemgo.dll` is the savemgo network patch (stage *config paths*, no record
code), and `system.dll` (19.8 MB) holds engine config strings but no symbols.
None of the three contains the peer-to-peer module, so the ELF stayed the source.

## 1. The cursor the handler is handed, and the six bytes it reads

`0x282D80` opens with four sequential reads from a cursor, then a table lookup
and a branch. `0x26CC88` is the cursor reader — a `{u16 pos; u16 end; u32 base}`
struct whose `+0` is copied from and advanced by the count — so the four reads
advance the cursor by `2/1/2/1` from wherever it stood:

```
00282d94: addi  r4, r1, 0x72     ; read 2 bytes -> 0x72
00282d9c: li    r5, 2
00282db4: bl    0x26cc88
00282dbc: addi  r4, r1, 0x70     ; read 1 byte  -> 0x70
00282dc8: bl    0x26cc88
00282dd0: addi  r4, r1, 0x74     ; read 2 bytes -> 0x74
00282ddc: bl    0x26cc88
00282de4: mr    r3, r31
00282de8: addi  r4, r1, 0x71     ; read 1 byte  -> 0x71
00282df0: bl    0x26cc88
00282df8: lbz   r0, 0x70(r1)     ; the single byte indexes a table on the object
00282e00: rlwinm r0, r0, 2, 0x16, 0x1d   ; (byte & 0xFF) * 4
00282e04: lwzx  r28, r9, r0      ; r9 = the object the invoker passed in r3
00282e08: cmpwi cr7, r28, 0      ; nothing registered there -> return
00282e0c: beq   cr7, 0x282e60
00282e10: lbz   r0, 0x71(r1)     ; the second single byte selects the branch
00282e14: cmpwi cr7, r0, 1
00282e18: beq   cr7, 0x282ea8
00282e84: cmpwi cr7, r0, 2
00282e88: beq   cr7, 0x282eb4
00282e8c: cmpwi cr7, r0, 3
00282e90: bne   cr7, 0x282e60    ; anything else: return
```

**Where the cursor stands is settled by the dispatcher, not assumed.** The
records of this family are queued by `0x26E790`, which reads the record's first
byte with the same reader before it looks an entry up, compares it against each
entry's key byte, and only then calls the invoker:

```
0026e868: mr    r3, r26          ; the cursor
0026e874: bl    0x26cc88         ; 1 byte -> r31+0x70
0026e87c: lbz   r0, 0x70(r31)    ; <- the record's first byte
0026e8c4: lbz   r0, 0xc(r27)     ; this entry's key
0026e8cc: blt   -> 0x26E91C
0026e910: beq   -> 0x26E9D0
0026e9cc: 0x27A180(container, entry, cursor)
```

So the `07` every one of these bodies opens with **is** the key, and it has been
consumed when the handler runs. The six bytes `0x282D80` reads are the record's
`+1` to `+6`:

| record + | what the read fills | what it is |
| ---: | --- | --- |
| 1, 2 | `0x72`,`0x73` | the record's length (`L - 7`) and the `0x00` beside it |
| 3 | `0x70` | **the kind** — the byte that indexes the node table |
| 4, 5 | `0x74`,`0x75` | the record's index and the player-table slot |
| 6 | `0x71` | **the branch key** — the header's last byte, the first payload byte |

The `+6` column of `docs/protocol/UDP_COMMANDS.md`'s header table takes exactly
the values this branch tests — `0x00` on the roster entry and kinds `0x01`–`0x03`,
`0x01` on the nine-byte shape, `0x03` on the roster close — which is the same
fact read from the captures rather than from the code.

## 2. The registry entry, and why the branch byte cannot move

The live registry `0x501030D4` holds sixteen entries; entry 2 is this stream:

| entry | +0 | +4 | +8 | tag `+0xC` | flag `+0xD` |
| ---: | ---: | --- | --- | --- | --- |
| 2 | 4 | OPD `0x011F1928` → **`0x282D80`** | `0x50103340` | `07` | `01` |

`0x27A180` is the invoker. It reads the entry's byte `+0xD` and takes the
"entry `+4` is an OPD, called with the entry's `+8`" path when bit 1 is clear —
which is entry 2's `0x01`:

```
0027a194: lbz   r0, 0xd(r11)     ; r11 = the entry
0027a1a0: rlwinm r0, r0, 0, 0x1e, 0x1e
0027a1e8: lwz   r9, 4(r11)       ; the OPD
0027a1f0: lwz   r3, 8(r11)       ; the handler's r3: the stream's own object
0027a1f4: clrldi r5, r6, 0x20
0027a1f8: lwz   r0, 0(r9)        ; the OPD's code, r2 from +4
0027a208: bctrl
```

Two facts come out of that:

* the handler's `r3` is **the entry's `+8`** — the key-`07` stream owns the
  object at `0x50103340`, which is the stage object §4 describes;
* the handler's `r4` is the invoker's **third argument**, forwarded untouched,
  and `0x27A180`'s whole body (`0x27A180`–`0x27A21C`) holds no call but the
  dispatch itself. Nothing between the dispatcher's key read and the handler
  can advance the cursor again.

So the branch byte is the record's `+6` at every entry point, and no caller
state changes it. The one `u16` the `== 2` branch reads afterwards is `+7`–`+8`.

## 3. The branches

* **`+6 == 0`** — sets bit `0x100` in the object's word at `+0x88`, then calls
  the node's **vtable slot 1**:
  ```
  00282e28: lwz   r0, 0x88(r27) ; ori r0, r0, 0x100 ; stw
  00282e34: mr    r3, r11       ; r11 = the node
  00282e3c: lwz   r9, 0(r11)    ; its vtable
  00282e40: lhz   r5, 0x74(r1)  ; index | slot << 8
  00282e44: lwz   r11, 4(r9)    ; slot 1's OPD
  00282e58: bctrl               ; r4 = the cursor, now at record +7
  ```
* **`+6 == 1`** — sets the same bit and falls into the list walk.
* **`+6 == 2`** — the list walk alone.
* **`+6 == 3`** — clears bit `0x100` (`rlwinm r0, r0, 0x17, 1, 0x1f` then
  `rotlwi r0, r0, 9`) and returns `0`.
* **anything else** — returns `0` without touching the object.

The list walk (`0x282EB4`) reads a `u16` into `0x76`, then walks the list at
`object + 0x80` through `+0xC` as its next pointer, matching a node's byte `+0`
against the record's `+3` and its `u16` `+2` against the `u16` the third read
produced (index, slot); on a match it calls that node's **vtable slot 2** with
`r3` = the looked-up node, `r4` = the matched node's `+8`, `r5` = the `u16` just
read.

## 4. Where the payloads go: the stage object's node, slot 1

The node is the object's entry for the record's **kind**, so each kind has its
own consumer at the object's own index:

| record | kind `+3` | index, slot `+4`/`+5` | branch `+6` | what the client does |
| --- | ---: | --- | ---: | --- |
| roster entry | `0x00` | roster index, slot | `0x00` | node `0`, slot 1, cursor at `+7` — the name and fields |
| nine-byte shape | `0x00` | index, slot | `0x01` | flag `0x100` set, then the list walk (§3) |
| roster close | `0x00` | index, slot | `0x03` | flag `0x100` cleared, **payload consumed by nobody** |
| kind `0x01`, 54 B | `0x01` | index, `0x00` | `0x00` | node `1`, slot 1, cursor at `+7` |
| close pair, 20 B | `0x02` | `0`, `0` | `0x00` | node `2`, slot 1, cursor at `+7` — the eight-byte value and the count |
| stage pair, 22 B | `0x03` | `0`, `0` | `0x00` | node `3`, slot 1, cursor at `+7` — the tag, the two triples, `fd 00` |
| spawn point, 25 B | `0x04` | index, slot | `0x00` | node `4`, slot 1, cursor at `+7` |

The kinds are `RoomRecordKind` in `src/Shared/Types/RoomRecordKind.cs` —
`RosterEntry`, `QuadAndItems`, `Box`, `CustomPoint` and `RespawnPoint` — so a
record's consumer is named at the place the byte is written rather than only in
these tables.

That is the stage/geometry subsystem the triples belong to: the object the key
`07` registry entry carries (`0x50103340`), which keeps a node per kind and hands
each record's payload to that node's slot 1, with `index | slot << 8`. The
object's class is **not** in the image — it is heap — so the consumer is
identified by its position in the dispatch, not by name, and slot 1 for kind
`0x03` is the parser the triples belong to.

Two details are worth recording rather than resolving:

* the kind index is scaled as `(byte & 0xFF) * 4` with no bounds check against
  the object's own state at `+0x80` (the list head) and `+0x88` (the flag word).
  Every capture's kind is `0x00`–`0x04`, so the table it addresses needs five
  entries and the state sits well clear of it; a kind above `0x1F` would index
  into that state.
* the branch byte is the record's `+6`, not one of the six header fields the
  offset tables list as fixed — which is why that byte is `0x00` on four of the
  five kinds and takes `0x01` / `0x03` on the two roster-side shapes.

## 5. What the replay grammar says, and what it does not

`rpdt.js` parses the same discriminator, `TYPE_ROSTER = 0x07`, in a REPLAY
chunk — the `.dat` format, not this channel. Its `rosterPlayer` refuses every
body shorter than `ROSTER_MIN_LEN`, which is `ROSTER_NAME_OFF_SHORT + 2` = 58,
because it reads a character name:

```js
if (L < ROSTER_MIN_LEN || b[a] !== TYPE_ROSTER) return null;
var nameOff = rosterNameOff(b, a, e);
if (L < nameOff + 1) return null;
...
return { slot: b[a + 5], joinCounter: b[a + 4],
         charId: dv.getUint32(a + 8, true), ... };
```

So it names the roster entry — `slot` at `+5`, `joinCounter` at `+4`,
`charId` at `+8`, clan id at `+31`/`+19`, nick at `+68`/`+56` — and returns
`null` for the close pair (20), the stage pair (22) and the 54-byte kind `0x01`
record alike. The replay console is not a second opinion on these three kinds;
it is silent on them by construction. It does agree on the header the client
reads: version `0x07` at `+0`, the slot at `+5`, and (from `rpdt.js`'s own walk)
the record length at `+1` rather than a sub-type.

## 6. What the two records are, as far as the evidence goes

| record | header | payload, measured | consumer |
| --- | --- | --- | --- |
| close pair, 20 B | `07 0d 00 02 00 00 00` | `00` at `0x07`, `02` at `0x08`, an eight-byte value at `0x09`–`0x10`, a three-byte count `00 00 00` at `0x11`–`0x13` | node `2` slot 1, cursor at `+7`, word `0x0000` |
| stage pair, 22 B | `07 0f 00 03 00/01 00 00` | `00` tag at `0x07`, a six-byte triple written twice — `84 03 84 03 3e fd` (900, 900, −706) for index 0, `3e fd 7d 03 07 03` (−706, 893, 775) for index 1 — then `fd 00` at `0x14`–`0x15` | node `3` slot 1, cursor at `+7`, word `0x0000` |
| kind `0x01`, 54 B | `07 2f 00 01 idx 00 00` | a quad of four signed sixteens, `00 00 04`, then four eight-byte items | node `1` slot 1, cursor at `+7`, word `0x0000` |

The triples are three signed sixteen-bit values with the x and z pair negated
between the two indices and y reading as a ground height — the reading
`UDP_COMMANDS.md` §"Kinds `0x01`–`0x04`" already carries as **[I]** from
`mgo2-game-failed-memoryviolation`'s mirrored pair. What the client settles is
*who consumes them*: a per-kind node of the stage object, handed the payload at
`+7`. The values stay the recording's bytes in `MatchRecordBodiesUtils`, since
nothing here maps a map to a triple.

## 7. What the native port names

`system.dll` in `C:\Users\migue\Desktop\pc-client` is the game itself rebuilt for
x86: 286 mangled exports, 152 MSVC RTTI descriptors and `__FILE__` paths, linked
at `0x10000000` (the base its own log prints). It has no symbols for these
records, but the file paths and the assert vocabulary name the subsystem they
belong to — the same one the ELF's key-`07` stream belongs to.

| evidence | what it names |
| --- | --- |
| `.\online\supervisor\net\sv_net.cc` with `HOST `, `Host call `, `client call `, `to me? %d `, `invalid member %d `, `actual size over %d %d ` | the supervisor's record stream: per-member records, host to client, each size-checked — the module this host writes |
| `.\online\supervisor\sv\sv_base.cc`: `Supervisor::Supervisor`, `teamno %d `, and the path `...\user\katagai\online\supervisor\sv\sv_base.h` | the class behind the ELF's `Supervisor` / `NT_HANDLER<Supervisor>` |
| `.\online\supervisor\slot\sv_slot.cc`: `SVSlotMan::SVSlotMan`, `SVSlotMan::ssJoin( %d ) no member ` | the player table the header's `+5` indexes: a slot manager, one entry per slot |
| `.\online\supervisor\sv\sv_host.cc`: `svhShareGamePhase( NT_DATA_GAME_P )`, `game_phase%d != Phase%d `, `svRestart() ` | the host half — `SupervisorH` in the RTTI |
| `.\online\supervisor\sv\sv_client.cc`: `client member ID %d != localID %d ` | the client half — `SupervisorC` |
| `sv_rule.cc`, `sv_mission.cc`, and the RTTI `SVRule`, `SVMission*`, `SRRule`/`SRTdm`/`SRDm`/`SRRes` | the rule and mission objects whose state these records announce |
| `.\online\supervisor\respawn\sv_respawn.cc`: `SV_GetInitialPosDir Failed :: teamno %d` | **the initial position and direction, per team** — what a spawn-point record carries |
| `.\netplayer\pl_net_respawn.cc`: `PL_NetSetRespawn` at `0x1026eaa0`, its stores `player+0x830` (position) and `player+0x840` (`u16`), and `IRREGAL RESPAWN POSITION %f %f %f >>> FLOOR NOT FOUND !!!!` | the client's side: it stores the pair and checks the position against the stage's floor |
| `.\online\supervisor\cp\sv_custom_point.cc`: `action %d `, `over action %d `, `over custom point %d ` | **custom points, each with actions**, bounds-checked as two counted collections |
| `.\object\itembox_mng.cc`, `.\online\other\online_itembox.cc` (`itemname`, `second_itemname`) | the item boxes |
| RTTI `.??AVSupervisor`, `.??AVSupervisorC`, `.??AVSupervisorH`, `.??AVSVNet`, `.??AVSVSlotMan`, `.??AVNT_DISPATCHER`, `.??AVNT_FACTORY`, `.??AVNT_HANDLER@V...`, `.??AVCM_NET_FACTORY`, `.??AVPL_NET_PLAYER` | the same class family the ELF shows in PPC form |
| `NT_DATA_PLAYER`, `NT_DATA_GAME_P`, `NT_DATA_GAME_MAP_MAX`, `NT_DATA_GAME_MISSION_MAX`, `NT_DATA_SHARE_ID_MAX` | the net-data structures these records fill |

Two field readings follow from those names rather than from the bytes:

* **Kind `0x04`'s payload is a respawn point and its direction. [V]** The
  client's setter is `PL_NetSetRespawn` at `0x1026eaa0`. It takes
  `(handle, position*, u16, action)` and stores **16 bytes straight from the
  position pointer** into `player+0x830` (`movups xmm0,[ebx]` /
  `movaps [esi+0x830],xmm0`) and the `u16` into `player+0x840`
  (`mov word ptr [esi+0x840],cx`), then sets the presence byte at `player+0x842`
  and the action state at `player+0x824` from 1 or 2. Called with no position it
  asks for one: `player+0x1` is the team, and `0x101abab0(team, &u16,
  &position)` fills the same pair — the sibling of the routine the supervisor's
  `SV_GetInitialPosDir` (`0x1017a3b0`) calls at `0x101aba80`, which **adds 800.0
  to the `y`** it received (`addsd` of the double at `0x10a53f0`) before it
  publishes. So `0x09` is the team both routines take as their first argument,
  `0x0a` the direction the client keeps beside the position, and `0x0c`/`0x10`/
  `0x14` the position in the order the 16-byte copy preserves. The middle value
  tracking the ground is now the client's floor check plus this 800 lift, not an
  inference from the captures.
* **Kind `0x03`'s payload is a custom point plus its action.** `sv_custom_point`
  counts two collections — points and actions — and bounds-checks each, which is
  the shape a 22-byte record with a tag, an index and a pair of triples has: one
  point, one action, the point written twice. The mirror in x and z with y
  unchanged is what a point looks like stated for the two sides of an action
  (approach and hold), not a rotation. **[I]**, and the bytes stay the
  recording's.

The close pair reads the same way from the captures' side: `UDP_COMMANDS.md`
reads kind `0x02` as **a box with a count of corners** — an eight-byte header, a
count, then that many eight-byte corners — and the 20-byte record's payload is a
count of zero. So the "close" is a kind-`0x02` box that states **no corners**:
the host closing a region rather than describing one. Nothing in the body
changes for it; `MatchRecordBodiesUtils` still writes the recording's bytes.

## 8. Open

* **Field offsets inside `sv_net.cc`, `sv_custom_point.cc` and `sv_respawn.cc`.**
  §7 names the modules and the values they hold; it does not read their frames.
  The port has no symbols, so a field's offset there has to be reached from a
  call site — the routine names alone are what make the readings in §7 **[I]**
  rather than **[V]**.
* **The node class and its slot 1 / slot 2 implementations.** They are reached
  through the heap object at `0x50103340`, so they need a dump with that object
  live, not another disassembly sweep of the image. Slot 1 parses the payload
  from `+7`; slot 2 takes a matched node's `+8` and the `u16` at `+7`–`+8`.
* **The close pair's eight-byte value and the stage pair's `fd 00` tail.** Both
  are inside the payload handed to slot 1, so their meaning is what slot 1 makes
  of them. They are constant across captures that share a stage and differ
  across stages (`briefing-room-state.md` §1), so they are stage facts.
* **The object's layout above `+0x7C`.** The flag word at `+0x88` and the list
  head at `+0x80` sit inside the region the unchecked kind index scales across.
