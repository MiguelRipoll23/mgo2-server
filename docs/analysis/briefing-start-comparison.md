# Why a peer does not leave the briefing: the live capture against a working one

**What this file is.** The record-by-record comparison of `myserver.pcapng` — a
peer joined to this project's dedicated host that never left its briefing — with
the two captures of a working server that hold the same moment. It lists every
difference the tools in `tools/` can read, says which of them is a defect this
host can fix, and marks the rest open rather than guessed at.

The three captures:

| capture | session | what it holds |
| --- | --- | --- |
| `myserver.pcapng` | `192.168.1.20:5730` (joiner) ↔ `89.129.16.203:5731` (host), 162 s | one join into a one-player room, the ready reports, the round opening on the briefing's deadline, then the beat — the client stops sending at t+102 and is reaped |
| `mgo2-started-briefing-then-solo-game.pcapng` | `10.2.0.2:5730` ↔ `99.66.131.177:5731`, 294 s | the same shape: one player, a briefing, a round opened for it, and the client entering the game at t+181 |
| `mgo2-game-started-on-briefing.pcapng` | `10.2.0.2:5730` ↔ `143.47.227.126:5720`, 8 MB | a room of six, the ready set growing, the round opening on the last report |

The solo capture is the one to compare against: it is the same room size as the
live one, and unlike the six-player capture its records are not drowned by five
other peers' streams.

**The relevant live server.** The deployed gameplay pod's log is the other half of
the evidence and is quoted below where it is decisive. Its own settings are in the
first line it writes: *"The room runs TDM on Blood Bath for 7-minute round(s) with
a 1-minute briefing, ending a round on its clock alone."*

## 1. What the live host gets right

These were checked and are not the defect. They are recorded so a later reader
does not re-derive them:

* **The round opens, and on the right gate.** The client's first report is at
  t+51.568; the room holds one player, so `PlayersAReportOpensFor` holds the round
  shut and the room's own one-minute briefing ends the wait — the log says so
  (`"The room's 1-minute briefing ran out with 0 player(s) still to report; the
  round opens for the players that reported, on 0x1ade"`) and the run goes out at
  t+60.816, 60 s after the handshake.
* **The opening run is the measured thirteen.** `tools/udp_start_run.py` reads the
  live run as *"the run is the measured 13 role(s), in order"*: the two empty
  `0x5001` heads, the 90-byte state block and its two-byte companion, the
  lobby-slot pair, the close triple, the round clock, the payload and the rules
  pair. The record set and the order are what
  `docs/analysis/replay-findings.md` §1.4 measures, and that section says in its
  own words that the order *"is not a gate a client waits on"*.
* **The client receives and acknowledges the run.** The log's `acknowledged
  record 0x1001 ordinal 9…13` and `0x1a54 ordinal 0/1` are the run's own records,
  so the frame was decrypted and read.
* **The lobby-slot handshake completes.** The client answers `82 f3`/`84 f3` at
  t+52.555 and the host confirms `81 f3` at t+52.606, which is the only thing that
  advances the pair.
* **The countdown, the ready set and the room's pair are live.** The briefing
  clock runs `128 → 96 → 64 → 32 → 0` over sixteen seconds and then holds, the
  ready set is published on every report and repeated every ~2.5 s, and the rules
  pair rides the same tick.

So the defect is not "no round", "no run", "wrong run" or "nothing on the wire".
It is something the client needs that the host is not sending, or is sending in
the wrong shape.

## 2. The settings burst does not name the host's own player

**This is the first defect, and the live log states it outright:**

```
[22:57:03 WRN] Character 26, in the room's slot 0, has no appearance this record
               can carry; the burst leaves it out rather than announcing another
               character's
[22:57:03 DBG] Character 2 is announced in slot 1, on side 0, with 0 skill(s)
[22:57:03 DBG] Match 266: 1 of 2 player(s) of the room have their own loadout read
               for the settings burst
[22:57:03 DBG] UDP 5731: sending the settings burst to 89.129.16.203:5730 after the
               roster pause: 1 player loadout(s) from the room's own rows
```

Two players are in the room — the dedicated host (character 26, slot 0) and the
joiner (character 2, slot 1) — and the burst announces only the joiner.

**The working capture announces both.** Its burst, at `t+6.006`, is:

```
0xd001 len 1                          the opening control
0x9001 len 161 class 0x03             the room's rotation
0x1001 len  50 class 0x04 body 0b 01  the host's own loadout, slot 0x01, no pairs
0x1001 len  54 class 0x05 body 0b 04  the joiner's, slot 0x04, two pairs
0x1001 len  54 class 0x06 body 0b 04  the same joiner's body again
0x0002 len  23 class 0x04             the closing state blob
```

Three loadout records for two players: the host's own (the fifty-byte no-pair
body, which is exactly `LoadoutBodiesUtils.PlayerLoadout`) and the joiner's
written twice — the repetition `docs/GAMEPLAY_SERVER.md` §7 records as unresolved.

The live burst has **one** loadout (`0x1001 len 50`), the joiner's. The record the
working host writes for slot 0 is missing.

**Why it is missing, and it is not the burst builder.** `LobbyLoadoutService`
reads each player's appearance from the characters domain and *drops the player
from the burst* when the read cannot answer (`"the burst leaves it out rather than
announcing another character's"`). The dedicated host character is created with no
appearance row at all: `AccountService.GetOrCreateCharacterIdentifierAsync`
inserts `characters (account_id, name, comment, created_at)` and nothing else, and
`CharacterService.EnsureDedicatedHostSettingsAsync` writes only the dedicated
flag. So the host is the one player the room can never read.

`docs/GAMEPLAY_SERVER.md` §3.1 lists the loadout each player is announced under as
*live* — "the room's players' now, read from the character and game rows". A
player the read cannot answer for was the one case the live path handled by
omission, and the host is always that player.

**Fixed** by falling back to the recorded host's own appearance block
(`LoadoutBodiesUtils.ReferenceAppearanceBlock`) so the slot is still announced,
and by announcing a player whose skills cannot be expressed with no pairs rather
than dropping it. That is the reference value the working server's host carries,
which is what the dedicated host should present.

## 3. The presence answer is never sent on its own, and its byte is derived the wrong way

### 3.1 It is only ever written inside the run

The client puts a one-byte presence record in the same datagram as its readiness
report — `0x0a5c` under the first generation, `0x025c` under the sibling, class
`0x04`. The live capture:

```
[51.568] IN 0x0a5c len 2 class 0x04 body 01   the client's presence, slot 01
         IN 0x1ade len 2 class 0x00 body e1 01  the report itself
```

and the host's answers:

```
[60.816] OUT 0x0a5c len 5 class 0x06 body 02 00 00 00   ×5, one per run repeat
```

Five answers — the run and its four repeats — and **not one answer in the 40 s
between the report and the round**. The working captures write it beside the
report as well as (in one of them) inside the run:

| capture | presence writes |
| --- | --- |
| `started-on-briefing` | `02` within half a second of the first report, then `06`, `0e`, `1e` as the rest report, then `16` when the third leaves — and again inside its run |
| `started-briefing-then-solo-game` | `08 00 00 00` beside the report's own stream, and `88 00 00 00` once a second player is in the room |
| `myserver` | inside the run only |

So a live client that reported got its ready state echoed (`e0 01 …`) but never an
answer to the half of its datagram that says *which player has arrived*, until the
round opened.

### 3.2 The first byte is the reported slot, not the publication order

`PlayerReadyService.PresenceMask` built the set as *one bit per reporter in
publication order* (bit 1 for the first reporter). The solo capture refutes that
reading and settles the other one:

```
[54.918] IN 0x025c len 2 class 0x04 body 03   the room's only reporter, byte 03
...
[88.118] OUT 0x025c len 5 class 0x06 body 08 00 00 00   the answer: bit 3
```

A room of one has one reporter, so publication order gives `02`; the capture
writes `08`. Under `1 << reportedByte` every briefing fits:

| capture | report bytes | answers | `1 << byte` |
| --- | --- | --- | --- |
| `started-on-briefing` | 01, 02, 03, 04 (then 03 gone) | `02`, `06`, `0e`, `1e`, `16` | bit 1; bits 1–2; 1–3; 1–4; then 1, 2, 4 |
| `joined-when-red-team` | 01, 03, 05 | `2a` | bits 1, 3, 5 |
| `started-briefing-then-solo-game` | 03 | `08` | bit 3 |

The doc's earlier rejection of "slots" (`replay-findings.md` §1.5) read the *lobby
arrays'* keys — `0x0b`, `0x0f`, `0x14` — and those are a different field from the
byte the report itself carries, which the run's payload echoes back.

**Fixed.** `PresenceMask` now sets `1 << ReportByte` per reporter, and
`PlayerReadyHandler` writes the answer to the reporting peer under the report's
own generation, as both captures do.

## 4. What is different but is not this host's to fix (or is unresolved)

### 4.1 The host's own entity stream — resolved: it is the addressed player's vitals slot

**This was the largest open item in the first version of this file and the
replay console settles it.** The working solo host writes `0x0862` — a
three-byte body `fa fa` under class `0x03` (`TYPE_HEALTH`) — 2 770 times over
its round, while the client writes `0x0861`, `0x0863`, `0x0865` and `0x0867`.
The live host writes its beat vitals as `0x090c` and **never** an `0x08xx`
record. They are not two streams at all: **a player's tick records are one
block of six identifiers**, and the replay parser's role table is that block —
`role.set(vid, TYPE_POS)`, `vid + VID_STATE` (`-2`), `vid + VID_HEALTH` (`-1`),
`vid + VID_WEAPON` (`+2`), `vid + VID_AIM` (`+4`), `vid + VID_DAMAGE` (`-5`),
with a health payload filed under track `vid - VID_HEALTH`, that is `vid + 1`
(`rpdt.js`; `docs/protocol/RPDT_REPLAY_RECORDS.md`). A tick record's class byte
*is* its payload type, so the capture's own records confirm the table slot by
slot: with the position at `0x0863`, the client's `0x0861`/`0x0865`/`0x0867`
carry classes `1`/`6`/`5` (`TYPE_STATE`, `TYPE_INCAP`, `TYPE_AIM`) and the
host's `0x0862` carries class `3` (`TYPE_HEALTH`).

The whole corpus was then asked to falsify it: for every player block that carries
a state record (class `1`) and a position record (class `2`), is there a record
one above the state at class `3`? **Eighteen blocks have both records and every
one of them carries its health, host-written; a nineteenth — the solo capture's
`0x8063`, a position with no state record — carries the health alone. No block in
any capture contradicts it.** The pairs, which is where the identifier question
started:

| capture | the player's own block (client → host) | the host's vitals |
| --- | --- | --- |
| `mgo2-started-briefing-then-solo-game` | `0x0861`, `0x0863`, `0x0865`, `0x0867` | `0x0862 fa fa` |
| `mgo2-started-on-solo-game` | `0x8063` position (the `0x8000` form of its own) | `0x0062 fa fa` |
| `mgo2-game3` | `0x08e3`, `0x08e5`, `0x08e7`, `0x08e9` | `0x08e4 fa fa` |
| `mgo2-game2` | `0x004d`…`0x0053` and `0x084d`…`0x0853` (two sessions), and `0x0057`…`0x005d` for the other player | `0x004e`, `0x084e`, `0x0058 fa fa` |
| `joined-final-round-solo-then-final-round-started-when-other-player-joined` | `0x0075`/`0x0875`… and `0x00ed` | `0x0076`/`0x0876` and `0x00ee fa fa` |
| `mgo2-game1` | `0x090b`, `0x090d`, `0x090f`, `0x0911`; then `0x010b`, `0x010d`, `0x010f`, `0x0111` | `0x090c` (t+73.4 s → 827 s) and `0x010c` (t+201 s → 756 s) |
| `mgo2-joined-game-in-progress-last-round` | (the joiner's own, off-capture) | `0x008a`, `0x0102` beside the blocks at `0x008b` and `0x0103` |

**The client marks its own records `0x8000` and the host does not**, which the
solo capture shows with both halves in one session: the client writes its
position as `0x8063` and the host writes that player's health as `0x0062`, the
same id with the bit cleared and one below the block's position slot. game1's
`0x810d` against the host's `0x010c` is the same arithmetic, so a host that reads
a peer's own block can name that peer's vitals slot without guessing.

So the record the live host writes is the *addressed player's* vitals slot, and
`0x090c` is the right slot for the session this host replays: it is game1's
joiner block (`0x090b`…`0x0911`) and its round-1 generation, which is also the
generation this host takes from the client's own readiness report
(`MatchRecordGenerationUtils`). The `0x800` bit the generation flips is part of
the block: game1's host writes `0x090c` while its client writes `0x090b`, and
`0x010c` while its client writes `0x010b`.

**What is not settled is the block's low byte**, and it cannot be settled from a
handshake: it is a per-session allotment (`0x0b`, `0x4d`, `0x61`, `0x75`, `0xe3`
across the five sessions above) and the client's peer id is `0x0001232f` in
**every** capture, so nothing a joining peer sends before its first tick names
it. A host that replays a recorded session can only carry that session's, which
is what this one does; the field stays **[U]**.

The mirror family the client produces is still worth watching for: a client
that never loads the map never writes `0x0861`-shaped records of its own, which
is the front-half measurement this file asked for in §6.

### 4.2 The run's three unclassified records

The solo capture's run carries three `0x1001` records the live run does not —
`len 22` and two `len 54`, each opening `07` (the roster/profile opcode, not the
`0b` settings opcode). `mgo2-game1`'s run does **not** carry them, and
`replay-findings.md` §1.4 reads `game1`'s run as exactly the thirteen. So they are
not part of the run's measured set, and this host does not write them. They stay
**[U]**: they may be the room's later joiners' profile records riding the frame,
or something else the solo host had to hand. `tools/udp_start_run.py` classifies
them as extra on the capture itself.

### 4.3 The state vector's room-owned bytes

Folded with `tools/udp_state_vector.py`, the live round's vector and the solo
capture's differ at the offsets the reading names as the room's own:
`state[11..13]` move in the working capture (`0/1`, `0/1`, `0/4`) and never in the
live one, and the working capture's `state[40]` (objective holder 0) is written
`0` where the live one holds `255`. Both are the values a real round's own play
produces and neither is written by a replay, so they are consistent with a host
that is not simulating. Not a fix; recorded so the next reader does not read them
as a framing defect.

### 4.4 The bundle-vs-separate question

Checked for every pair the captures hold. Only one record's *place* differs
between the live host and a working one, and it is §3's presence answer:

| record | working host | live host | verdict |
| --- | --- | --- | --- |
| readiness publication (`e0 …`) | one frame per report | one frame per report | same |
| presence answer (`0x0a5c` class 6) | beside the report **and** in the run | in the run only | **defect, fixed** |
| round marker (`0x5ade`/`0x52de`) | its own datagram in one capture, inside the run in the other | inside the run | not a gate (§1.4) |
| opening run + repeats | one frame, repeated | one frame, repeated | same |
| rules pair | one frame per tick | one frame per tick | same |
| lobby-slot pair | one frame with the run, then republished | same | same |
| settings burst | one frame per record, roster pause | same | same |

No capture shows a record the live host splits across datagrams that a working
host bundles, or the reverse, except the presence answer.

## 5. What was changed

| file | change |
| --- | --- |
| `Rooms/LoadoutBodiesUtils.cs` | added `ReferenceAppearanceBlock()` — the recorded host body's trailing 23-byte appearance block |
| `Rooms/LobbyLoadoutService.cs` | a player whose appearance cannot be read is announced with the reference block instead of dropped; a player whose skills cannot be expressed is announced with no pairs |
| `Rooms/PlayerReadyService.cs` | `PresenceMask` sets `1 << reportedByte` (the player's own slot) instead of one bit per publication index |
| `Commands/Peer/Room/PlayerReadyHandler.cs` | writes the presence answer to the reporting peer under the report's generation, beside the report and not only inside the run |

## 6. What would settle the rest

* **A live run of the fix**, with the client given the same 3 minutes the working
  capture took: the measurable outcome is the client's first `0x0861`/`0x0863`
  position record, which the working capture sends from t+181 and the live one
  never sent.
* **The player block's low byte** needs a capture in which the client's own
  block is known *and* the host's records that name that player are legible, so
  the allotment's source can be read rather than assumed. The handshake rules it
  out (its peer id is constant across captures) and the client only writes its
  own block once the round is running.
* **The run's three extra records** need a second capture of a solo room whose
  join numbering is known, so the records can be matched to a stage of the join.
