# The briefing screen of 3 November: this host against the working one

**Measured 3 November 2026**, from the four files of the two sessions, with
`tools/udp_briefing_flow.py`, `tools/udp_flow_summary.py`,
`tools/udp_session_records.py` and a per-record walk of the log capture. Every
claim below is a counted fact from one of the three captures, or is marked
**[U]**.

| capture | session | what it holds |
| --- | --- | --- |
| `myserver.pcapng` | `192.168.1.20:5730` (joiner) ↔ `89.129.16.203:5731` (this project's dedicated host), 26.0 s of records in a 60 s file | one join, the report at t+10.181, the run at t+13.24, then the client's silence from t+22.3 |
| `working server/briefing-then-solo-game.pcapng` | `10.2.0.2:5730` ↔ `64.181.216.250:5720`, 199.5 s | a solo join into a briefing, the round opening at t+146.0 |
| `working server/working-server-log.pcapng` + `working-server-log.log` | `10.2.0.2:5730` ↔ `143.47.227.126:5725`, 594.5 s, 37 137 datagrams | four sessions in one file; the briefing join measured here is at t+512.0 |

The two reference hosts are the working server's, and the third file is the only
pair in this repository that holds a patcher log **and** its own capture, so
every statement about "what the client was doing" below has the wire behind it.

## 1. The client publishes the briefing itself, in a record this host never sees

A client whose briefing screen is up writes its own operations rows 0.3–0.9 s
after its join report:

```
0x9a54 / 0x1254  1d <slot> 00 01 03 00 00     class 00
0x1a54           1d <slot> 01 02 01 00 00     class 01
0x1a54           1d <slot> 02 00 00 00 00     class 02
0x1a54           1d <slot> 03 00 00 00 00     class 03
0x1a54           1d <slot> 04 00 00 00 00     class 04
0x1a54           22 <slot> 00 0b 16 00 0e 0e 1e 45 2f 57 39 67 68 0e 0e 00 0e 0e 00 00   22 bytes
```

| capture | client's report | its five `1d` rows | gap |
| --- | --- | --- | --- |
| `briefing-then-solo-game` | t+8.696 `e1 0a` | t+8.993, slot `0a` | **0.30 s** |
| `working-server-log` | t+512.01 `e1 0f` (+ `9b06 02 0f`) | t+512.883, slot `0f` | **0.87 s** |
| `myserver.pcapng` | t+10.181 `e1 01` | **none in the whole capture** | — |

**They are the client's own publication and not an answer.** In
`briefing-then-solo-game` they precede that host's opening run (t+9.780); in
`working-server-log` they follow it (t+512.10). One order cannot be an answer to
a record if the other is not, so the rows are written on the client's clock,
once the screen exists.

**The report repeats too, and ours does not.** The reference client re-sends
`e1 <slot>` about once a second for as long as it sits in the briefing — 156
report records over `briefing-then-solo-game`'s 199 s, the count running to
t+144.8 with the round's open at t+146.0 behind it — and the log capture's
client writes a *second*
report, `e1 0f` on `0x1ade` beside `9b06 02 0f`, in the briefing of t+512.
`myserver.pcapng` holds exactly **one** report from the client, at t+10.181, and
nothing of the kind after it. The client is alive throughout — it acknowledges
the countdown with `0x5254` at t+13.38, 15.78, 18.21, 20.72 and 23.13 — so the
silence is its own state and not a dead socket.

These two behaviours are the only differences on the wire that are of a kind
rather than of a count, and both belong to the client. Nothing this host writes
changed either of them.

## 2. What the reference host writes that this host does not

The ready record is written now (§3); the rest was not written when this file was
measured, and none of the three can be the screen (§1, §5).

**The per-player ready record, `0x1b06`, nine bytes.** The log capture holds
eleven copies, at t+508.7 to t+513.5, one per player of the room as that player
reports ready, and every one of them is the same body with its own slot:

```
00 <slot> 02 60 09 06 00 c4 09
```

`60 09` is `0x0960` = 2 400 — that room's round clock in the stream's 125 ms
units. The three bytes behind it, `06 00 c4 09`, are **[U]**; `c4 09` (= 2 500)
moves with nothing this measurement could see. The client's own ready is
`0x9b06 02 <slot>` two bytes, which the host answers `0xdb06 06` one byte, and
the pair travels with the payload report and its `e0 <slot>` publication in the
same datagram. This repository has no record of the family anywhere — not in
`src/`, not in `docs/protocol/` — so a peer joining a room of the reference's
kind is the one party that never states a player's ready in it.

**The item-box pair through the briefing.** Both reference hosts keep the
`0x1250`/`0x125a` (and `0x1a50`/`0x1a5a`) group going past the run: the log
capture writes 124 item-box records between t+505 and t+540 in groups of four,
0.27 s to 1.8 s apart, and 566 in its 594.5 s, against this host's **four**
records in the whole capture — one group, the run's own tail.
The pinned build already implements the measured thirty continuation groups
(`ItemBoxFramesUtils.BriefingContinuationGroups`, commit `92b0d8a`), which the
capture confirms and `myserver.pcapng` does not show — see §6.

**The room's count is sparse rather than absent.** `working-server-log` writes
the family 14 times in 594.5 s, the first `1a58 00 13` at t+509.02 (a room of
nineteen); `briefing-then-solo-game` writes it **not once** in 199 s; this host
writes `1a58 00 01` beside its roster answer and never again.

## 3. What was written

**The ready record, beside every publication.** `0x1b06` is built by
`BriefingReadyStateBodiesUtils.Body(slot, roundUnits)` and written for each
reporting player by `BriefingStateService.BuildPublication`, immediately ahead of
that player's `e0` publication and at the same fourth byte — the order and the
ordinal the capture holds. Because both callers of the publication are the report
handler and the briefing's repeat, the record repeats with it and not once.

| field | source |
| --- | --- |
| version `00` | the recorded byte, every copy |
| slot | the byte the player's own join report carried |
| state `02` | recorded, unchanged for a reporter and a readied player — **[U]** what it states |
| round clock | `MatchStartRunService.BriefingRoundUnits` → `MatchRoundStateService.BriefingClockUnits`, the clock the briefing's run carries |
| `06 00`, `c4 09` | the recording's, in all eleven copies — **[U]** |

The identifier is derived rather than tabulated:
`MatchRecordGenerationUtils.ReadyState` is the payload record `0x28` along
(`0x1ade` → `0x1b06`), and the sibling set follows from the generation bit
(`0x12de` → `0x1306`), which is **[I]** until a capture of that set's briefing
holds it.

**What would confirm it.** A join against this host that draws the two records
per player in one datagram, `1b06` first, at one ordinal — and a client that
answers `0xdb06`/`0x5b06` beside its payload acknowledgements, which is the
answer a client of that room gives. The comparison after that is the two trailing
words: they are the one part of the record this measurement cannot derive.

## 4. What this host gets right, checked and not the defect

Recorded so a later reader does not re-derive it:

* **The roster answer is the reference's, record for record.** `myserver.pcapng`
  t+1.947 writes the five records `§11` asks for — the host's own entry (68 B),
  the joiner's (69 B), the roster close (7 B, `07 00 00 00 00 00 03`), the
  room's count (10 B, `0b 00 00 04 00 00 00 00 00 02`) and the peer's settings
  (50 B) — in the reference's order and at the same bodies. The count and the
  close are byte-identical to `mgo2-game-started-on-briefing`'s.
* **The opening run is the measured fourteen records.** t+13.24 carries the two
  empty `0x5001` heads, the 90-byte state block, `0f 00`, both 26-byte arrays
  (`85 7e …`, `86 7e …`), the presence answer, the close pair, both 22-byte room
  records, the seven-byte roster close and the item-box pair. The client
  acknowledges it (`0x5ade`, `0x5254`) and re-sends its entry echo behind it.
* **The run's instant is the dwell it is meant to be.** The client's entry echo
  is at t+10.734 and the run at t+13.24, 3.06 s later — the 2.5 s dwell plus a
  beat, which is `join-stall-after-mode2.md` §73's change working. The
  reference's run follows its echo 0.84 s later because its host has no dwell.
* **The countdown is held, not stuck.** `0b 3c 00 00 00 00 00` at t+13.30,
  15.73, 18.18, 20.62, 23.06 and 25.50 — one a tick, held at the room's whole
  minute because the room holds one player, which is `BRIEFING_FLOW.md` §1.

So this join is not a host that wrote nothing, a wrong run, a wrong roster or a
dead clock.

## 5. Why the screen is not something this wire can supply

The three files agree with `briefing-state-machine.md` §8–§10 and extend it:

* The reference client is on the briefing **before** its host's run either way
  round (§1), and it reveals itself by writing its own rows, not by asking for
  them.
* A client that reaches the screen and a client that does not are the same
  build, the same patcher and the same 8.7 s to 10.2 s between the game socket
  and the map-loaded report (`briefing-then-solo-game` 8.7 s,
  `myserver.pcapng` 10.2 s).
* **The patcher's briefing probes cannot see the screen.** The log of the
  reference run reports the briefing loader `0 time(s)`, the room's asker `0`,
  the one-block asker `0`, the data set `0`, the flow's last screen `"none"`,
  and the battle-before flow stepped `0` and started `0` — in the same session
  whose client wrote `1d 0f 00 01 03 00 00` at t+512.883 (§1). Every counter is
  zero and the briefing happened, so a log that reads `0 briefing` is not
  evidence that none was shown. The labels the same log does carry
  (`COMcsl`, `dmg_count`, `battle_loading`, `COCtimescore`, `battle-start`,
  `btst-EVENT01`/`02`, then `result_game`, `option-game`, `wp_select`,
  `LmenuWeapon`, `LmenuItem`, `bat_eff`, `battle_end_main`, `hit_eff`) are the
  script engine's name registrations through `0x10022370`, as
  `briefing-state-machine.md` §6 measures, and they name the map's HUD and not
  the briefing flow.

What is left is the channel that decides the flow, and neither folder holds it:
the lobby's `0x4313` room details and its join answers, read by the client
before it opens the game socket. `join-stall-after-mode2.md` §72 has since
written the room's briefing time and its per-rule words into the host's settings
row, and the reply reads them; whether the client that opened
`myserver.pcapng` was told a briefing of one minute is exactly what that capture
cannot show.

**What would settle it.** One TCP capture of the lobby channel for one join to
each of the two servers — the same client, the same ten seconds — compared at
the room-details reply's briefing time (`0x0ec`), its per-rule words (`0x10c`,
17 of them) and the join answer. If the two replies agree and only one client
briefs, the decision is in the client or in a field neither document names yet;
if they differ, the reply is the defect and the gameplay wire is exonerated for
good.

## 6. Two facts a reader should carry forward

* **The pod that answered `myserver.pcapng` may not be the pinned image.** The
  pin is `92e57fe`, and `92b0d8a` — an ancestor of it — already writes the thirty
  item-box continuation groups and one state blob per player. The capture shows
  one item-box group in the run and none behind it, and the client
  acknowledging just that one. Either the room's state suppressed them or the
  pod is older than the pin; the image tag on the running pod settles it and the
  capture cannot.
* **`0x1b06` was missing from this repository entirely**, and is written since
  (§3). What is still open is what its two trailing words state: all eleven
  measured copies carry `06 00` and `c4 09`, and nothing in this session moves
  them.

## 7. What the client's binaries and its stage script add

The loader, the game DLL and the lobby stage script were all read for joining or
briefing log text. None of them has any; what they have is structure, and it is worth
having because it says which exchange the screen is waiting on.

**The loader logs transport, not games.** `mgo2.exe` carries `connection refused`,
`not connected`, `already connected`, `Check your internet connection and retry.`
(`0x00452B24`, `0x00452B14`, `0x00452B00`, `0x00451BBE`) and `WinHttpConnect`
(`0x00455E68`) — it is the HTTP front end. The only net log the PS3 client states is
`mgo_connect_server_by_index() index=%d, type=%d` (`0x00FF9008`) and
`mgonet_connect_timeo` (`0x00FF8FC8`), both about reaching the server.

**`system.dll` says what the briefing is fed.** Its key names, not its logs:

| key | offset |
| --- | --- |
| `root/matching/briefing/player_data/team_data%d/player_data%d/user_name` | `0x10FC4798` |
| `…/user_type`, `…/user_class`, `…/user_act` | `0x10FC4708`, `0x10FC4750`, `0x10FC47E0` |
| `…/shop`, `…/areacode`, `…/medal_data%d`, `…/shikan_pp` | `0x10FC4678`, `0x10FC46C0`, `0x10FC45E0`, `0x10FC4630` |
| `…/team_pp`, `…/team_pp_rate` | `0x10FC4864`, `0x10FC4828` |
| `root/matching/briefing/player_data/shikan_pp_max`, `…/ivent_btl`, `…/shikan_btl` | `0x10FC489C`, `0x10FC48D0`, `0x10FC4900` |

So the briefing is populated from a **matching-phase roster keyed per team and per
player** — two teams, each player's name, class, shop, area and medals, plus per-team
PP — and the flow units that own it are named as well:
`..\game_flow\MGA_BattleBefor\MGA_BattleBefor_Briefing.cpp` (`0x10FC505C`),
`.\online\hud_mgo2\h_mgo2_briefing.cc` (`0x10FEFFB0`), with the state keys `briefing`
and `briefing-matching` at `0x10FC5638` and `0x10FC5618`. Joining has its own units and
asserts: `.\online\lobby\ac_lb_host_join.cc` (`0x1108E128`),
`mgo_lobby_get_joined_editteamdetail()` (`0x11083250`) and
`SVSlotMan::ssJoin( %d ) no member` (`0x10FD9BC0`) — a slot read before the member is
written. The gameplay body never carries any of this, which is the point: the
161-byte room record is not what a briefing reads.

**The stage script has no log lines, and that is now a measurement rather than an
assumption.** `game/stage/lobby/scenerio.gcx` decompiles with the bundled tool —
`node .gametools/gcl-vscode/bin/gcx.js decompile <file.gcx> <outdir>` — in half a
second, giving 26 procs, 2 821 lines of GCL and 33 000 resources (`script.gcl`,
`resources.gcl`). Its whole command vocabulary is `ComGetCurrentRule` (four calls),
`ComGetCurrentStage`, `NewBGColorSet`, `NewSystemLightSet`, `NewFogSet`,
`COM_SetCamera`, `COM_SoundTuneRegist`, `COM_SoundSpeechRegist` and
`NewGclVariableMove`. Of the three debug commands the dictionary knows —
`NewGclAssert` (`0x6B237D`), `CR_AddDebugNet` (`0xFEBFBD`) and
`NewDebugPutStageSet` (`0xCCF220`) — it calls **none**, so the lobby's script cannot
log anything, and the strings it does hold are the resource table the HUD draws from.

**What this settles.** The briefing screen is a matching-phase roster render whose text
is already in hand (`docs/analysis/rule-instruction-texts.md`) and whose inputs are team
and player records. Nothing in the client writes a log line that would say why a screen
did not appear, so the diagnostic in §5 stands unchanged: compare the two servers'
room-details replies and join answers, because the roster the briefing reads arrives
with the join, not with the gameplay body.

## 8. The second pair, 3 November 23:47: the ready record is confirmed, the client still writes nothing

Two files arrived after §1–§7 were written: `.pcapngs/briefing/myserver/myserver.pcapng`
(6 956 088 B) and its `mgo2-patcher.log` beside it. Both were read against
`working server/briefing-then-solo-game.pcapng` with `tools/udp_session_records.py`
and `tools/udp_briefing_flow.py`, which agree family for family.

| marker | this host | the working host |
| --- | --- | --- |
| session | `192.168.1.20:5730` ↔ `89.129.16.203:5731` | `10.2.0.2:5730` ↔ `64.181.216.250:5720` |
| span, records | 38.4 s, 243 (41 in, 120 out) | 199.5 s, 5 547 (1 076 in, 528 out) |
| client report `e1 <slot>` | **1** | **157** |
| client briefing rows `1d` | **0** | **9** |
| client ready `1f <slot> <state>` | **0** | 1 |
| host publication `e0 <slot>` | 4 | 2 |
| `0x1b06`-family ready record, 9 B | **4** | 0 |
| its acknowledgement `0x5306` | 4, from the client | 0 |
| round opens | 0 | 1, at t+146.007 |
| last client datagram | t+32.904 | t+199.472 |

**The ready record is confirmed on the wire for the first time.** Every copy is the
nine bytes `00 01 02 60 09 06 00 c4 09` — byte-identical to the eleven §2 counts in
the log capture — under `0x1306`, which is `0x1b06` with the generation bit clear,
as §3 derived. It is written at t+9.959, 12.080, 14.545 and 17.005 (2.12, 2.47 and
2.46 s apart), immediately ahead of that peer's `e0 01 00 00 00` publication at the
same ordinal, and the client answers each with an empty `0x5306`. So the record goes
out where and how the reference writes it, and its two unread words `06 00` and
`c4 09` are accepted by the client rather than refused — which is as far as §6's
second bullet can be closed without a capture where the words move. `60 09` is
2 400, this room's one-minute briefing in 125 ms units.

**Every remaining difference is on the client's side.** In 38 s the client reports
once and never again, never readies, and writes no briefing row: the opposite of
§1's shape, where a client on the screen writes its five `1d` rows 0.3–0.9 s after
the report and repeats `e1 <slot>` about once a second. The four publications are
this host's own repeat, and the join's ending is not a fault in itself — a
one-minute briefing that nobody readies opens at its deadline, and this capture
stops before it.

**The stage script names the branch it took.** The patcher log holds six stages,
the last `n007a`; the script runs and sets the score table (13 writes, the 14th past
the table, one check at line 94), and the labels it registers are
`battle_loading`, `battle-start` (518 calls), `btst-EVENT01`/`02`, `COCtimescore`
(211), `COMcsl` and `dmg_count`. The working log registers those seven **plus**
`wp_select`, `LmenuWeapon`, `LmenuItem`, `bat_eff`, `battle_end_main`, `hit_eff`,
`result_game` and `option-game` — the round it played — so the label sets do not
separate the two clients; what they do say is that this map went into its battle
labels on our join, with `the flow's screen request` still at 0 and the briefing
loader at 0 (never), as §7 measured it must.

**What the pair settles and what it leaves.** It confirms the `0x1b06` write end to
end and adds a second live session in which nothing the host sends is refused. It
changes nothing about the screen: the client is not on it, its own rows are absent,
and the map's script reached battle. The open item stands exactly where §7 left it —
the briefing proc of `stage/<map>/scenerio.gcx` — with `n007a` now the stage whose
script is known to have run without it.

**Where that script can be read.** It is not in this tree: the extracted client
carries five stage scripts only (`init_n`, `lobby`, `mga_boot`, `nt_mgsetup`,
`r_sna01_n`), the 129 MB `pc-client.zip` holds the same five, and no file matching
`n007a` exists anywhere under the project. The session to be read from ran on an
install at `C:\Users\migue\Desktop\pc-client` (its patcher's own `config:` line),
so `game/stage/n007a/scenerio.gcx` is one copy away; the download manifest pins it
at 1 480 825 bytes, HMAC `938a71f3 6f872af2 b8c6d604 bd2bd8f7`. Dropped into
`game/stage/n007a/`, it decompiles with `node .gametools/gcl-vscode/bin/gcx.js
decompile <file> <outdir>` (`--key stage/n007a` if the copy is still in its encrypted
container), exactly as the lobby script was. Any playable map stage answers the same
question: `PROTOCOL.md` records `proc23`'s score table as byte-identical across
`n002a`, `n003a` and `n004a`, and the briefing procs are shared the same way.
