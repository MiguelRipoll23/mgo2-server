# The 161-byte rotation payload over the whole corpus

**What this file is.** Every room-state payload of the captures in `.pcapngs/`
(fifteen of them write one) read field by field, so the bytes
`RoomRotationPayloadUtils.BuildCore` still writes as literals can be told apart
from the bytes that are a room's own. It is the measurement behind §1 and §2;
where a byte's meaning is still open it is left open rather than guessed.

**How it was measured.** Each capture's gameplay session was decoded with the
session key from its two handshake bases, the host's first `0x1001` record of 161
bytes was taken (`tools/udp_state_vector.py`'s `session_records`), and every
offset of the fifteen bodies was tabulated against the room facts the body itself
carries: its sixteen rotation triples, its population (`0x56`), its current
rotation slot (`0x55`) and its round phase (`0x09`).

| capture | slots | pop | rot | round | rules in the rotation | max map |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| `joined-final-round-solo-then-final-round` | 15 | 2 | 0 | 3 | 1,2,3,4,5,6,7,16 | 17 |
| `joined-on-briefing-then-game` | 15 | 2 | 0 | 2 | 4,15 | 19 |
| `joined-on-game-solo` | 6 | 2 | 3 | 3 | 1 | 18 |
| `mgo2-game-failed-memoryviolation` | 1 | 13 | 0 | 2 | 16 | 7 |
| `mgo2-game-joined-when-red-team-then-briefing` | 15 | 6 | 0 | 5 | 1 | 21 |
| `mgo2-game-started-on-briefing` | 6 | 2 | 0 | 2 | 1 | 10 |
| `mgo2-game-started-team-selection-selected` | 15 | 2 | 7 | 3 | 4,15 | 17 |
| `mgo2-game1` | 15 | 2 | 12 | 3 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-game2` | 15 | 2 | 0 | 2 | 7,15 | 17 |
| `mgo2-game3` | 15 | 2 | 13 | 3 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-joined-game-in-progress-last-round` | 15 | 4 | 14 | 4 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-started-briefing-then-solo-game` | 15 | 2 | 13 | 2 | 1,2,3,4,5,6,7,16 | 17 |
| `mgo2-started-on-game-round1` | 11 | 8 | 9 | 4 | 1,5 | 17 |
| `mgo2-started-on-solo-game` | 6 | 2 | 0 | 3 | 1 | 18 |
| `myserver` | 9 | 2 | 4 | 4 | 1,2,3,5,6,16 | 14 |

Two offsets are a room fact in all fifteen and nothing else:

* **`0x55` — the current rotation slot** (**[V]**), the value the native reader at
  `0x8EB0C0`/`0x8EB148` uses to select a triple;
* **`0x56` — the room's population, host included** (**[V]** in this corpus: 2 for
  a solo room, 4, 6, 8 and 13 for the larger ones).

## 1. Field 29's timer vector: what the tail entries are

The 17 little-endian `u16`s at `0x65`–`0x86` are rule times in seconds. Elements
0–7 are established: DM, TDM, RES, CAP, SNE, BASE, BOMB, TSNE from the room's own
timer bank. The corpus settles the tail. Seven vectors cover the fifteen captures:

| captures | elements 0–7 | elements 8–11 | elements 12–16 |
| --- | --- | --- | --- |
| `mgo2-game1`, `game3`, `joined-final-round-solo…`, `joined-game-in-progress…`, `started-briefing-then-solo-game` | 300 420 180 300 540 300 300 240 | 53251 44192 9216 34 | 180 5940 0 300 300 |
| `joined-on-briefing-then-game`, `game-started-team-selection…` | 300 300 420 240 480 300 420 240 | 53251 44192 10240 34 | 180 1200 0 5940 300 |
| `joined-on-game-solo`, `started-on-solo-game` | 300 420 420 240 420 300 420 600 | 53251 44192 10240 34 | 180 1200 0 300 300 |
| `mgo2-game2` (TSNE only) | 300 300 300 300 300 300 300 240 | 0 0 0 0 | 300 300 0 1800 300 |
| `mgo2-game-failed-memoryviolation` | 300 × 8 | 0 0 0 0 | 300 300 300 300 1200 |
| `mgo2-game-joined-when-red-team…`, `started-on-game-round1`, `myserver` | 300 × 8 | 0 0 0 0 | 300 300 300 300 300 |
| `mgo2-game-started-on-briefing` | 300 480 300 × 6 | 0 0 0 0 | 300 300 300 300 300 |

So elements 12–16 are **per-room rule times for rule ids 12–16** and not a
constant: element 12 is 180 in nine rooms and 300 in six, element 13 is 5940,
1200 or 300, element 15 is 5940, 1800 or 300, element 16 is 1200 only in the room
that failed. Their units are seconds, like elements 0–7. One correlation holds
across all fifteen: element 14 is `0` in exactly the ten rooms that state an
INTERVAL or SCAP time other than 300, and 300 in the other five — so element 14
(ANEST) is not a constant but moves with the extended rules a room configures.

Elements 8–11 (`0x75`–`0x7c`) are **not** timers, and not the room's either. They
are the four slots for **rule ids 8–11**, the ids no rotation in the corpus uses,
while the ids on both sides are the ones that do: element `k` is rule id `k`
throughout the vector's two inhabited stretches, `0`–`7` and `12`–`16`. Four
readings of the values fail on the numbers themselves — as seconds they are
14 h 47 m, 12 h 16 m, 2 h 33 m and 34 s; as operations-clock units (480 a minute,
125 ms) 110.9, 92.1, 19.2 and 0.07 minutes. No rule owns any of them.

They are also not room data. The same eight bytes appear in **every** body that
sets them — eleven rooms on nine hosts, with different slots, populations, rules
and maps — while each neighbouring element of the very same vector moves per room,
and the seven all-default rooms write zero. What holds still across rooms, hosts
and sessions while moving with no room option belongs to the client build, so the
four slots are what the client's bank carries for the rule ids it does not ship,
written through without scaling — which is also why they are the only four of the
seventeen that are not whole minutes.

**The four ids have names.** The client's own rule table,
`game/stage/<stage>/scenerio/resources.gcl` (the same block in `n001a`, `n010a`,
`n013a` and `lobby`), lists the seventeen in id order — DM, TDM, RESCUE, CAP,
SNEAK, BASE, BOMB, TSNE, **COOP, TRA, OFFICER_TRA, CLANROOM**, SDM, INTERVAL,
ANEST, SCAP, RACE — and every id this corpus already knows lands where it should
(`0` DM, `1` TDM, `2` RESCUE, `3` CAP, `4` SNEAK, `5` BASE, `6` BOMB, `7` TSNE,
`12` SDM, `13` INTERVAL, `14` ANEST, `15` SCAP, `16` RACE). So ids 8–11 are
**Co-op, Training, Officer Training and Clan Room**: the four modes the client
ships a name for and no rotation can hold, which is why their slots carry no room
time — a mode without a round has no timer to configure.

The single part that does move is one bit:
`0x2400` in the five bodies of the `mgo2-game1` family, `0x2800` in the four of
the other two families and in `.pcapngs/pcapng3` (the one capture in this
checkout that sets them, whose vector is `900 300 420 240 480 300 420 300 53251
44192 10240 34 900 1200 0 300 300`). A bit that follows a capture family follows
a code path, not a setting.

No copy of the eight bytes exists in the game files or in `MGO2.ELF`: searching
the install and the ELF for `03 d0 a0 ac`, `00 24 22 00`, `00 28 22 00` and the
big-endian pair `d0 03 ac a0` finds them in speech and sprite archives only,
never in the ELF or a table resource, so the words are assembled rather than
read from the disc. The reading is **[I]**: the slots carry no meaning a room can
state. Its discriminator is a capture from a second client build — if the four
words change with the client while the rest of the vector keeps changing with the
room, the slots are build data and the question closes.

## 2. The bytes `BuildCore` writes as literals, and what the corpus says about each

| body offset | builder value | corpus | verdict |
| --- | --- | --- | --- |
| `0x4a` (field 3) | `0x11` | `0x0d` (1), `0x0f` (8), `0x11` (13) | per-room, and which room gets which is **[U]**: `0x0d` appears only in `joined-on-game-solo`, while `0x0f` and `0x11` both appear in fifteen-slot rooms, so it is neither the slot count nor the population. It is the room's and not its host's: `129.153.74.111:5720` writes `0x11` for its thirteen-player room and `0x0f` for the six-player one (§7) |
| `0x4d` (field 5) | `0x02` | `02` in 21 of 22, `14` in one | **a state byte, not a setting**: `02` in every body of a room's game phase and `14` (`20`) in the body written while that room was held at its briefing, so it moves with the room rather than being pinned by it |
| `0x4e` (field 6) | `0x03` | `3` (19), `2` (2), `6` (1) | per-room: `2` only in the two six-slot rooms of `202.215.217.164`, `6` only in `mgo2-game2` (a TSNE/SCAP rotation), `3` in the other nineteen. **This is the byte an earlier row here called field 5** |
| `0x4f`–`0x52` (field 7) | `0x00274022` | `0x0007622a`, `0x00126073`, `0x0007603a`, `0x00274022`, `0x00076022`, `0x00074022`, `0x00034033` | per-room 32-bit word; the native readers test bits `0x20` and `0x40`. Bit `0x20` is set in **all twenty-two** bodies; bit `0x40` only in `0x00126073`, and only for the one host whose join showed the client a team-selection screen (§7). An earlier reading claimed both bits were set in every value but `0x00034033`, which is wrong: six of the seven carry `0x20` alone. The rule reading — "set in the rooms that rotate SNE and SCAP" — is refuted by `mgo2-game2`, which rotates rule 15 and writes `0x00034033` |
| `0x53`–`0x54` (field 8) | `0x0000` | `0x0000`, `0x00b4`, `0x00f0`, `0x012c` | per-room, and constant across that room's own captures; every value is a whole number of minutes held as seconds (`0`, `180`, `240`, `300`), which no other offset here but field 4 is. The `0x002c` an earlier row recorded is the low byte of `0x012c`. What the time limits is **[U]** |
| `0x57`–`0x64` (fields 13, 17–27) | `00 00 00 05 00 00 00 05 00 00 01 00 00 00` | identical in the builder's own rooms, `00` in the all-default rooms, other values in the rest | partly a room's own: `0x60` is `2` in most rooms and `0` in the all-default ones, `0x63` is `2`, `3`, `6` or `0`, `0x64` is `10` or `0`; **`0x57` is the room's unique-character setting, repeated at `0xa0` in all fifteen** (below); the two words at `0x5a` and `0x5e` are a per-round pair (§7) |
| `0x87`–`0x9b` (fields 30–49) | `02`×8 `01 02 02 02 32 1e 05 03 00 00 00 7f 7f` | `0x89`, `0x8d`, `0x90` take `2`, `4` or `6`; `0x91` takes `2` or `20`; `0x93`/`0x94` take `20/30`, `40/99`, `50/30`, `51/30`, `55/30`; `0x9a`/`0x9b` are `7f 7f` except `03 06` and `04 09` | the per-rule round and ticket counts, the SNE/extra-time settings, the DM/SDM/SCAP/RACE round counts and the unnamed host-flag bits, and the unique-character pair; the run's own field boundaries are the client's, 15 + 4 + 2, and they are §5a |
| `0x9c` (field 50) | `0xff` | `0xff` in all fifteen | **constant**; the unassigned slot reference, confirmed beyond the one capture it came from |
| `0x9d` (field 51) | `0x8c` | changes between bodies of one room (`140`, `232`, `233` for `64.181.216.250:5720`) | the capture's byte and not the room's; it is not a checksum of the body — byte sum, negated sum, `u16` sum, byte XOR and CRC16 over every prefix ending at `0x9d` or later match it in none of the twenty-two bodies |
| `0x9e`–`0x9f` (field 52) | `0x0128` | constant per room across its own bodies (`277`, `296`, `3813`) | the room's own word, so the pair an earlier row called opaque is two fields; which counter it names is **[U]** |
| `0xa0` (field 53) | `0` | `0` or `1` | the room's unique-character setting; offset `0x57` (field 13) carries the **same byte** in all fifteen captures. The pair at `0x9a`/`0x9b` is its assignment: the client's `P2P_AssignRoleSlots` (`0x77b628`) picks two distinct player slots when the room states them |

**Eight bodies from six captures and five hosts, measured 2026-11-08.** Decoding
`.pcapngs/briefing-then-solo-game`, `working-server-4-sessions`,
`working-server-log`, `game-with-drebin-points-disabled`, `pcapng1` and `pcapng2`
yields eight distinct 161-byte bodies, from `129.153.74.111`, `143.47.227.126`,
`64.181.216.250` (two rooms) and `69.213.233.238`. Three of the builder's fields
barely move across them, which is what makes them the ones a writer can pin with
confidence:

| field | body | value in all eight | reading |
| --- | ---: | --- | --- |
| 4 | `0x4b` | `180` | the client's own literal — it writes `li r0,0xb4` into its object at `0x9e9500`, so no room chose it |
| 6 | `0x4e` | `3` | the team-kill kick, from record `+0x3a6` (block `0xB6`) |
| 5 | `0x4d` | `2` in seven, `20` in the body written at a briefing | the room's phase, not a setting |

Field 3 (`0x4a`) is `17` in all eight of these, and only the wider corpus in §2
carries the other two values (`13`, `15`), so it is the one of the four that a room
changes. What genuinely varies body to body is the rest: field 8 is `0` or `300`;
the word at `0x4f` takes five values (`00274022`, `00264022`, `00126073`,
`00076022`, `00004033` — the last from the capture named for Drebin points being
disabled, whose bit `0x02` is nonetheless still set, so that flag is not what that
capture turned off); `0x57`–`0x64` and `0x87`–`0x9b` move with the room; and the
tail byte and word differ per body. Those are the fields with no getter in the
shipped client (§4) and no source in this server, which is why they keep the working
capture's bytes.

### The writer's block: 168 bytes, its fields, and the client's own literals

The function around the writer cluster is the client's builder of this state, and it
states the object's size and its fields outright. It clears **`0xa8` (168) bytes at
`r1+0xd8`** (`addi r29,r1,0xd8; li r5,0xa8` before the zero-fill call) and then fills
that block through the setter one field at a time. So the object is **seven bytes
longer than the 161-byte body**, which is the measured reason its offsets and the
body's are not one shift apart.

| object offset | width | source |
| ---: | ---: | --- |
| `0x01` | 16 | stack |
| `0x11` | 4 | stack |
| `0x15` | 48 | the rotation, copied to `r1+0xa6` first |
| `0x45` | 1 | stack |
| `0x46` | 2 | **literal `0x00b4`** (`li r0,0xb4` at `0x9e9500`) |
| `0x48` | 1 | **literal `2`** (`li r0,2` at `0x9e9518`) |
| `0x4c` | 4 | the settings word, built from the room record's `+0x3a0` bitfield |
| `0x50` | 2 | stack |
| `0x52` | 1 | stack — the slot |
| `0x53`, `0x54`, `0x56`, `0x58`, `0x59` | 1, 2, 2, 1, 1 | stack |
| `0x5a` | 8 | stack — two independent bytes (§4) |
| `0x62`, `0x64`, `0x66`, `0x67` | 1, 2, 1, 1 | stack |
| `0x68` | 34 | the seventeen rule times, in seconds |
| `0x8a`–`0x9d` | 1 each | the room record's own bytes |
| `0x9e` | 2 | **literal `0x7f 0x7f`** |
| `0xa0` | 1 | **literal `0xff`** |
| `0xa4` | 1 | **literal `0`** |

Two fields the corpus was treating as room data are the client's own constants: `0x46`
is `180` because the client writes it, and `0x9e` is `0x7f 0x7f` for the same reason —
the unassigned Sneaking role slots. The word at `0x4c` is not one value either: the
code loads the room record's `+0x3a0` dword, and a run of tests expands it with
`ori`s of `0x200`, `0x1000`, `0x2000`, `0x4000`, `0x8000`, `0x10000` and `0x20000`
(`0x9e8e00`–`0x9e8ea0`). Bit `0x02` — the member `RoomCommonSettingsFlag` calls
Drebin Points — is the one branch that is not a plain expansion: a `+0x3a0` bit makes
the client call a routine, and the bit is set only when that call answers zero, so the
Drebin bit is the record's flag **and** the client's own preference together. The byte
at `+0x3b7` supplies `0x04` and `0x20` the same way.

That is the `S+0x3A0` bitfield `ADDRESSES.md` records as feeding the `0x4310`
settings word, reached from the other end, and it is why the pinned word's low bits
are per-room while the high bits track a record the operations body never carries.

The seventeen rule times are where the client shows its units: twelve fields of the
record are multiplied by `mulli r0,r0,0x3c` — minutes into seconds — and stored as
`sth`s into the block's `0x68` bank, so `300` on the wire is five minutes by the
client's own arithmetic rather than by inference from captures.

### The settings word, named by the screen that writes it

The client hosts matches, so the word at object `0x4c` — body `0x4f`–`0x52` — is not
only read but **written**, and the code that writes it says which switches it carries.
The builder expands the room record's `+0x3a0` dword into the word, and the
single-variable captures in `.pcapngs/settings` name the bits it lands on. Each
capture flips one Create Game option against the same baseline word `0x00204022`:

| bit | value | Create Game row |
| ---: | ---: | --- |
| 16 | `0x10000` | Auto Assign Teams |
| 17 | `0x20000` | Teams Switch Positions |
| 18 | `0x40000` | Lock On (AUTO AIM) |
| 22 | `0x400000` | Level Limit (enable) |
| 0 and 4 | `0x11` | Friendly Fire — moved together, not separated |

These are **not** the `0x4310` common-settings positions (`0x142`/`0x143`), where
the same rows sit at other bits; the two words are not interchangeable. Drebin
Points is not in this word — the Drebin capture sets the rotation triple's third
byte and leaves the word on the baseline — and team switching changes no byte at
all. Bits `0x20`, `0x2000`, `0x4000` and the remaining low bits still have no
reading.

### The record the builder reads is the game-settings block

The builder's sources are one struct, and its offsets are the client's own game-settings
block with a base of **752** — the same 204-byte block `0x4313` delivers and `0x4310`
carries, whose field map is in `docs/proto/outbound/mgo2_cmd_4313_s2c.ksy`. Four
alignments pin it, and they were not looked for — they fell out of reading the builder:

| record | block | the block's field | width the builder reads |
| ---: | ---: | --- | --- |
| `+0x332` | `+66` | **Max Number of Characters** | one byte |
| `+0x334` | `+68` | **Briefing Time** | one word, its low half into a `sth` |
| `+0x34e` | `+94` | **stance** | one byte |
| `+0x354`–`+0x394` | `+100`–`+164` | the seventeen per-rule words | seventeen `lwz`/`lbz` |
| `+0x3a0` | `+176` | the 32-bit **common-settings word** | one `lwz`, expanded by `ori` |
| `+0x3a4` | `+180` | **Idle Kick** | one `lhz` |
| `+0x3ac`–`+0x3b7` | `+188`–`+199` | Capture **Extra Time**, the Sneaking **SNAKE** count, and the block's unread tail | one byte each |

The widths agree field by field, which is what makes this an identification rather than
a coincidence of numbering: `Idle Kick` is the one `lhz` in the run because it is the
one `u2` in that stretch of the block, and `Max Number of Characters` and `Briefing
Time` are adjacent at `+66`/`+68` exactly as they are in the block.

**Field 3 closes on it.** Body `0x4a` is `Max Number of Characters`: the builder takes
the block's `+66` byte — `max_players` — and writes it to the slot three bytes before
the one it fills with the literal `180`, which is the same three-byte run the body
carries at `0x4a`–`0x4d`. So 13, 15 and 17 are rooms stating their capacity, and no
level-limit reading ever fit because `Level Limit` is a *different* field of the same
block (`+95`/`+96`) that this path never reads — which is also why no level or rank
appeared anywhere in the gameplay wire.

**The briefing time is `0x61`–`0x62`, and the fill code settles it.** The builder loads
the block's `+0x44` (`briefing_time`, wire `0x0ec`) and stores its low half at object
`0x64`; the presence map puts object `0x64` at body `0x61`–`0x62`. The working capture's
`01 00` is a one-minute briefing. **Body `0x53` is a different minute limit:** it holds
`0`, `180`, `240` or `300` and no traced write gives it a source, so its name stays
open. The round's own times are the seventeen-word bank at `0x65`, filled from the
block's `+100`–`+164`.

One caveat, stated rather than smoothed over: `+0x3ac`–`+0x3b7` corresponds to block
`+188`–`+199`, and the block map's last named field ends at `+189` with the mobile own
note that `+190`–`+203` has no reader in the image. The builder *does* read those
bytes, so either that negative needs narrowing to the settings screen's own copy, or
the builder reads a game-side copy of the block at the same offsets — `0xD49440`
memcpys 204 bytes between exactly these two shapes. Which of the two it is does not
change the names above, which rest on offsets the block map calls settled.

### The complete inventory

Every offset of the 161-byte body, with what it is and how well that is known. **[V]**
measured, **[I]** inferred from a shape or a label, **[U]** open.

| body | field | what it is | |
| --- | --- | --- | --- |
| `0x00` | — | the record's state index, `0x0b` | [V] |
| `0x01`–`0x08` | — | the state's presence mask, constant in every body | [V] shape, [U] per bit |
| `0x09` | — | the round phase; the client compares it with `0`, `3`, `7` | [V] |
| `0x0a`–`0x19` | — | reserved, zero in every body | [V] |
| `0x1a`–`0x49` | — | sixteen rotation triples of map, rule, options | [V] |
| `0x4a` | 3 | **`Max Number of Characters`** — the settings block's `+66` byte | [V] |
| `0x4b`–`0x4c` | 4 | `180`, the client's own literal | [V] |
| `0x4d` | 5 | the room's phase, `2` and `20` at a briefing | [V] |
| `0x4e` | 6 | `team_kill_kick` | [ELF] builder source `+0x3a6` |
| `0x4f`–`0x52` | 7 | the common-settings switch word (above) | [V] bits 8-23 |
| `0x53`–`0x54` | 8 | a whole number of minutes as seconds; a minute limit, not the briefing time (which is `0x61`–`0x62`) | [I] |
| `0x55` | 9 | the current rotation slot | [V] |
| `0x56` | 10 | the room's population, host included | [V] |
| `0x57` | 13 | the room's unique-character setting, repeated at `0xa0` | [V] |
| `0x58`–`0x64` | 17-27 | per-room settings; `0x5a`/`0x5e` the teams' remaining tickets, `0x60` the host stance, `0x61`–`0x62` the briefing time, `0x63`/`0x64` the level band | [I] / [ELF] |
| `0x65`–`0x86` | 29 | seventeen rule times in seconds: 0-7 the eight standard rules, 8-11 the room family's own, 12-16 rules 12-16 | [V] unit, [I] binding |
| `0x87`–`0x9b` | 30-49 | the SNE/CAP/RES/TDM/BASE/BOMB/TSNE round counts, the TDM/DM ticket counts, the Sneaking kill count, Capture extra time, the DM/SDM/SCAP/RACE round counts and the unnamed host-flag bits, and the unique-character pair | [ELF] builder sources |
| `0x9c` | 50 | `0xff`, the unassigned slot reference | [V] |
| `0x9d` | — | a byte that moves between bodies of one room; not a checksum of any prefix | [U] |
| `0x9e`–`0x9f` | — | a word that holds across a room's bodies — a counter, unnamed | [U] |
| `0xa0` | 53 | the selector again | [V] |

Nothing in this table is written from a guess: every row is either a value the corpus
separates, a label the client's own screen carries, or a literal the writer loads. The
five `[U]` rows are the ones where this build contains no reader *and* no labelled
producer, and they are the honest end of the line for static analysis — a capture of
the original server carrying different values is the only remaining instrument.

**Two bodies of one host, measured 2026-11-08.** `pcapng1` and `pcapng2` are the same
room (`129.153.74.111:5720`) about fifteen minutes apart, and their 161-byte bodies
differ only where the room moved: field 3 is **`0x11` (17) in both**, the population
byte goes `16` → `17`, the rotation slot `12` → `13`, and the tail is
`ff 26 38 01 01` → `ff 28 38 01 00` (the byte before the word, the word itself, then
field 53 flipping). So field 3 is a setting the room holds rather than a count of who
is in it, and it is not the population either. The range argument separates the two
settings the builder's comment names: `17` is inside Level-Limit's `0..22` and the
room's players top out at level 15, so the limit is not violated; a character cap of
17 would be one past the sixteen the game allows.

**What the builder now writes.** Every field this server does not derive is the
working capture's: `.pcapngs/mgo2-started-briefing-then-solo-game.pcapng`, the
capture the port's notes call `briefing-then-solo-game`. Its body therefore
reproduces that capture field for field except the three the room owns — the
rotation triples at `0x1a`, the slot at `0x55` and the population at `0x56`. The
regions taken from it are `0x4a`–`0x54`, `0x57`–`0x64`, `0x65`–`0x86`, `0x87`–`0x9b`
and `0x9c`–`0xa0`, and the port's own builder is checked against the capture byte for
byte in exactly those regions.

**What the duplication settles.** Field 13 at `0x57` and field 53 at `0xa0` hold
the same byte in every capture, so `BuildCore` writes both from the room's
unique-character setting instead of a literal at `0x57` and a second at the tail.

**What this changes in the builder.** Elements 12–16 are now documented as the
times of rule ids 12–16 — SDM, INTERVAL, ANEST, SCAP and RACE, indexed by rule id
rather than by the lobby reply's order elements 0–7 follow — and the reference 300
stands only because the lobby's own times for them are settings this server never
receives: the bank it does receive is in the reply's order, which has no word for
these rule ids. Values of 0 are **not** written there, because element 14 reads 0
only in a room that configures an extended rule and 300 in the ones that do not,
so the constant belongs to the room and not to the format. The four words before
them stay zero, which is what seven captures measure. Nothing else in this file's
table is renamed, because nothing else is measured to a meaning.

## 3. What the client binaries and the port's notes added

The mapping of elements 12–16 to rule ids is already read out in
`docs/protocol/UDP_SETTINGS_PAYLOAD.md`: the PC binary's code at VA `0x1020fd59`
selects `Rule_Eng_SDM`, `Rule_Eng_INTERVAL` and `Rule_Eng_SCAP` for ids 12, 13
and 15, and the five adjacent name pointers at file offset `0x441b24` order SDM,
INTERVAL, ANEST, SCAP, RACE. That is what fixes the five elements to rule ids
rather than to five unnamed slots, and this corpus is the independent check on it:
the values move per room exactly where the per-rule table says they should, and
index 14 is the one with no captured rule to exercise it. The same holds for the
`0x4313` tail bytes `0x166`–`0x16a`, whose bytes 0, 2 and 4 read 60× into
indices 12, 13 and 15.

Nothing in the binaries names fields 51–52 or the byte at `0xa0`; the payload is a
state transfer rather than a record with a parser, so those stay as §2 states them.

## 4. What the native readers say

Three findings from the PS3 `system.dll`'s PPC code, read with `tools/mgo2_disasm.py`
over `.game/ps3/MGO2.ELF`:

* **The client's rotation object is a 48-byte block plus a one-byte index, and both
  are fetched through one accessor.** `FUN_008eb0c0`, `FUN_008eb148` and
  `FUN_002816f4` each call `FUN_0027e2b0` (resolve the settings object), then
  `FUN_0027e480(object, 0x15, 48, dst)` and `FUN_0027e480(object, 0x52, 1, &slot)`,
  and then return `dst[3*slot + n]`. That is the same sixteen-entry, three-byte
  layout this file's tables list at body `0x1a`–`0x49`, selected by the byte at
  `0x55`; the client's object keeps the block at offset `0x15` and the index at
  `0x52`, so the object offsets are not the wire offsets.
* **The triple's first two bytes have getters, and the earlier reading had them one
  index high.** `0x8eb0c0` indexes `slot * 3` and so returns `triple[slot][0]`, the
  map; `0x8eb148` indexes `slot * 3 + 1` and returns `triple[slot][1]`, the rule
  (measured 2026-11-25 from both bodies — this file had called them `[1]` and `[2]`).
  The third byte has no getter in this family, so what it *means* stays **[U]**.
* **The accessor call sites are the client's field map.** Scanning the whole text
  segment for `bl FUN_0027e480` finds 283 sites, each carrying its key and length in
  `r4`/`r5`; the pair the three getters use is `0x15`/48 and `0x52`/1, and the
  others name the remaining settings fields. `FUN_0027bb10` uses the same pair and
  then computes `3*(slot+1)`, clears the slot to zero and raises an engine notify
  when `slot > 14` — the rotation walk, and the reason a room plays fifteen slots
  and leaves the sixteenth zeroed.

**The shipped getters, key by key.** Of those 283 sites, exactly **eight** lie in
  the settings getter family (`0x8ea428`–`0x8ebfe8`), and between them they read
  four fields — this is the whole of what the shipped build exposes as a property:

| getter | key | width | returns |
| --- | ---: | ---: | --- |
| `0x8eaf88` | `0x01` | 16 | `block[i]`, the sixteen-byte field one byte at a time |
| `0x8eafe0` | `0x4c` | 4 | `word & 0x40` — the boolean property, unreferenced by any table |
| `0x8eb078` | `0x5a` | 8 | `block[1]` |
| `0x8eb030` | `0x5a` | 8 | `block[5]` |
| `0x8eb0c0` | `0x15`+`0x52` | 48+1 | `triple[slot][0]`, the map (corrected 2026-11-25) |
| `0x8eb148` | `0x15`+`0x52` | 48+1 | `triple[slot][1]`, the rule (corrected 2026-11-25) |

The two bytes of key `0x5a` being fetched by two different getters is the measured
  reason the eight-byte field is read as at least two independent values rather than
  one word, and it is why the pair at body `0x5a`/`0x5e` is described as two team
  figures. Every other field this corpus pins — fields 3 to 8, 13, 17 to 27, the
  tail entries of field 29 and fields 30 to 49 — has **no getter in the shipped
  build at all**, which is the concrete reason those offsets have no name to carry:
  the value matters for byte fidelity with the room that wrote it, not because this
  client consumes it.

## 5. The settings descriptor table

`FUN_0027e480` resolves a field by walking an array of ten-byte records, and the
records are in the image at VA `0x1229b80`–`0x1229da5` (file `0x1219b80`), 55 of
them, decoded by `tools/mgo2_settings_descriptors.py`:

| u16 | meaning |
| --- | --- |
| `key` | byte offset of the field inside the settings object |
| `length` | width of the field in bytes |
| `stride` | `1` in every record but two: `2` at the field that has three widths, and `0x11` (17) for the per-rule view of the timer bank |
| `flags` | a per-field word (`0x0025`, `0x0125`, `0x0225`, `0x0235`, `0x0064`, `0x0000`) |
| — | zero |

The records tile the object densely from `0x00` to `0x8a`, which is the arithmetic
the accessor performs (`key + length * stride`), and three records share key
`0x5a` with lengths 8, 4 and 1 — one field with three widths.

**The table confirms the wire layout field for field.** The object is not the wire
body: the body packs the fields and the object leaves two gaps (`0x11`–`0x14` and
`0x4a`–`0x4b`), which is why the reader's offsets and the body's differ by 9 bytes
before the first gap and by 5 and 3 after them. Matched by width, and by the two
readers' own keys, the correspondences are:

| object key | width | body offset | what this file calls it |
| ---: | ---: | ---: | --- |
| `0x00` | 1 | `0x09` | round phase (field 0) |
| `0x01` | 16 | `0x0a` | field 1, sixteen reserved bytes |
| `0x15` | 48 | `0x1a` | the sixteen rotation triples |
| `0x45` | 1 | `0x4a` | field 3 |
| `0x46` | 2 | `0x4b` | field 4, always `0x00b4` |
| `0x48` | 1 | `0x4d` | field 5 |
| `0x49` | 1 | `0x4e` | field 6 |
| `0x4c` | 4 | `0x4f` | field 7, the word with bits `0x20`/`0x40` |
| `0x50` | 2 | `0x53` | field 8 |
| `0x52` | 1 | `0x55` | current rotation slot (the getters' key) |
| `0x53` | 1 | `0x56` | population |
| `0x68` | 34 | `0x65` | field 29, the seventeen rule times |

So the two bytes this file has been calling "field 9" and "field 10" are, in the
client, a one-byte slot index and a one-byte population at object offsets `0x52`
and `0x53`; the seventeen-`u16` timer vector is one 34-byte field, not seventeen;
and the third byte of a triple is inside the 48-byte block the client treats as one
field, which is why only the engine's own getter reads it.

### 5a. The fields at `0x8a`–`0xa5`, and why the table read stopped at 29

The table above was read to `0x1229ca2`, and that is where the *script* stopped, not
where the table does. `tools/mgo2_settings_descriptors.py` walks out from the
`0x15`/48 anchor while each ten-byte record passes `is_record`, whose stride test
accepted only `1`, `2` and `4` — and record 28 is `0x68`, width 2, **stride `0x11`
(17)**. That record is the per-rule view of the seventeen-entry timer bank: a caller
passes `0x68 + 2 * rule` and gets that one rule's two bytes back, because the record's
bound `length * stride` (2 × 17 = 34) keeps every such key inside it. The test refused
it, the walk stopped there, and the 26 records behind it — every field the body writes from
`0x87` onwards — were never in the table this file was reading.

The table runs to `0x1229da5`, **55 records**, and the last third of it is the shape
of the body's tail:

| object key | width | stride | flags | body offset |
| ---: | ---: | ---: | --- | ---: |
| `0x66` | 1 | 1 | `0x0025` | — |
| `0x67` | 1 | 1 | `0x0025` | — |
| `0x68` | 34 | 1 | `0x0225` | `0x65`–`0x86` |
| `0x68` | 2 | `0x11` | `0x0035` | `0x65 + 2k` |
| `0x8a`–`0x98` | 1 each | 1 | `0x0025` | `0x87`–`0x95` |
| `0x9a`–`0x9d` | 1 each | 1 | `0x0025` | `0x96`–`0x99` |
| `0x9e` | 2 | 1 | `0x0025` | `0x9a`–`0x9b` |
| `0xa0` | 1 | 1 | `0x0025` | `0x9c` |
| `0xa1` | 1 | 1 | `0x0025` | `0x9d` |
| `0xa2` | 2 | 1 | **`0x0024`** | `0x9e`–`0x9f` |
| `0xa4` | 1 | 1 | `0x0025` | `0xa0` |
| `0xa5` | 1 | 1 | `0x0000` | — |

**Object `0x99` has no record**, which is why the run of one-byte fields is 15 + 4 and
not 19: the wire carries declared fields only, so the body has no hole for it. The
count is exact — 15 + 4 + 2 = the 21 bytes at `0x87`–`0x9b` — and the writers follow
that split: `WriteUnidentifiedStateFields` writes the fifteen through `0x95`,
`WriteUnidentifiedRoundFields` the four at `0x96`–`0x99`, `WriteRoleSlots` the pair at
`0x9a`–`0x9b`, and `WriteTailFields` starts on `0x9c`, which is the `0x9e`/`0xa0` field
boundary and not a byte inside one.

**The last two bytes are one client field, not two.** `0x9a`/`0x9b` is the single
field two bytes wide at `0x9e`. The server reads its halves as two slot references
because `0x7f` is a byte-sized value meaning no slot, and that reading is what the
corpus measures — but the client declares one field, and a reader for either half
would have to fetch both.

**The flags make `0xa2` the odd one.** Every field in the run is `0x0025`, whose bit
`0x0001` is what `0x27e578` tests to publish a write that changes the field — except
`0xa2`, the room's word at body `0x9e`/`0x9f`, which is `0x0024` and so is never
published, and `0xa5`, the only `0x0000` field in the table and the one field the body
does not carry at all. A field the client receives, holds and never sends is what a
per-room constant looks like from the client's side.

**The builder addresses every key, and two fields now have a meaning.** The getter
family reads only `0x01`, `0x4c`, `0x5a`, `0x15` and `0x52` (§4), which is where the
earlier reading stopped. The client's room-state builder does not: the `0x9e9xxx` band
carries `li r4,key; li r5,width` before each `P2P_ReadOperationStateField` /
`P2P_WriteOperationStateField` for `0x8a`, `0x8b`, `0x8d`, `0x90`, `0x91`, `0x92`,
`0x93`, `0x94`, `0x95`, `0x96`, `0x97`, `0x9a`, `0x9b`, `0x9c`, `0x9d`, `0x9e`, `0xa0`
and `0xa4`. **The lobby band `0xd30xxx` adds none.** Its `li r4,0x90` and
`li r4,0x91` immediates are arguments to `0xd226b0`, which packs a
`{key, width, index, value}` record and calls through a function pointer at
`0xc1208` — a generic dispatch, not `P2P_ReadOperationStateField` or
`P2P_WriteOperationStateField`, so the call cannot be attributed to the room-state
descriptor. Its `li r4,0x96; li r5,1` sites call `0xd52ab0`, a float routine (it
saves `f8`–`f31`), with a constant `0x100`, which is the same false positive as
`0x877848` (§5b). `0xa0` has no site in the band at all. So `0x90`, `0x91`, `0x96`
and `0xa0` join `0x98` and `0xa2` as keys the shipped build does not read or write
outside the builder; the fields at `0x87`–`0x99` stay unnamed.

Two of the fields read off the bodies that write them:

* **`0x9e` (body `0x9a`/`0x9b`) is the round's role pair, and the client assigns it.**
  `0x77b628` draws two values with an LCG (`state = state * 0x5d588b65 + 1`, index
  `(draw + state + byte[+0xa1]) % count`), retries while the second repeats the first,
  and writes the pair with `P2P_WriteOperationStateField(block, 0x9e, 2, …)`. So the
  pair is two distinct slots, which is why it moves between bodies of one SNE room.
* **`0xa1` (body `0x9d`) is a counter.** `P2P_ApplyFieldA1Record` (`0x26dd60`) reads it
  into its own `+0xbcb`; on the other branch it increments that copy, writes it back,
  then moves every player entry whose state is above `0x211` to `0x212`. That is the
  byte no checksum matched: `232 → 233` is one increment, and `140 → 232` is
  ninety-two of them between the sampled bodies.

`0xa2` is also the run's only `0x0024` field — the one the write gate never publishes —
and `0xa5` is the only `0x0000` field and the one the body does not carry. A field the
client receives, holds and never addresses is what a per-room constant looks like from
the client's side.

### 5b. What each remaining key does, and the sites that say so

The key-immediate search (`li r4,key; li r5,width`) gives the addresses that name a
field, and every address had to be checked against its callee before it counted:
`0x877848` matches key `0x49` but its call is `0x3530f8(…, 0x49, 1)`, an event code and
not a field read. Read that way, four fields have a behaviour and the rest have only the
builder:

| object key | what the sites do |
| ---: | --- |
| `0x45` | **max players, and the client enforces it.** `0x271ef0` reads it and compares it with the room's player count at `instance + 0xd0`; when the cap is at or below that count it tears the peer's entry down. The corpus's character-cap reading is confirmed by its consumer |
| `0x58` | **its presence gates the join state machine.** `0x288d18` tests `0xa1`, `0x52` and `0x58` through `0x27e198`, a presence test rather than a read, before advancing to its next state — which is why the body's `0x57` repeats `0xa0` |
| `0x59` | **an index, not a value.** `0x2806f0` reads it and hands it as the id argument to `0xf0ea88`, beside a count and a list it builds from fields `0x11f`, `0x120` and `0x114` of the twenty-four state blocks |
| `0x63` | **script-visible.** `0x77e668` reads it into `+0xd0` and fires stage event `0x16002c` with the byte, guarded by `+0xb8 < 10` and a `0xff` sentinel |
| `0x5a` | the ticket block. `P2P_ReadOperationStateField(block, 0x5a, 8, …)` copies body `0x5a`–`0x61`; `0x8eb078` returns byte 1 (`0x5b`) and `0x8eb030`/`0x8eb050` byte 5 (`0x5f`). Those are the **high bytes** of the two `u16` counters at `0x5a`/`0x5e` — zero for every captured count — so the getters are not the path the ticket panel reads a number from |

Every other key in the run — `0x54`, `0x56`, `0x62`, `0x64`, `0x66`, `0x67` and the
fifteen at `0x8a`–`0x98` — has no site outside the `0x9e9xxx` builder, where the client
writes it and reads it back. They are round-tripped and unnamed, and the builder's own
body is the only place left to look.

**The builder's body is readable, and its entry is `0x9e8b9c`.** The function spans
`0x9e8b9c`–`0x9e985f` and opens with `stdu r1,-0x26f(r1)` — a 623-byte frame. **That is
why every earlier scan failed:** this is a 64-bit image and it allocates stack with
`stdu` (`f8 21`), not the 32-bit `stwu` (`94 21`), so searching for the 32-bit prologue
could never match however far back it went. The frame is what explains the two buffers
the copy sites write into: the partial one at `r1+0x7d` and the body block at `r1+0x13f`
both sit inside 623 bytes, and the return address is saved at `0x280(r1)`, which is the
caller's linkage slot once the frame is subtracted.

The record the copies read is the function's **first argument**: the prologue does
`mr r27,r3`, and `0x9e9040` does `clrldi r3,r27,0x20` before `lbz r0,0x332(r3)`. So
`+0x332` is an offset in the room record passed in, not a global. Its head is a
five-case jump table (`bctr` at `0x9e8bf0`, case offsets `0x14`, `0x90`, `0xa68`,
`0xab4`, `0xc30`).

The copy sites are: object `0x45` takes a byte from record `+0x332`, `0x66` from
`+0x34f`, `0x67` from the word at `+0x350`, and `0x49` from the halfword at `+0x3a6`.
Through the block's `+752` relation those are `max_players`, `level_limit_tolerance`,
`level_limit_base` and `team_kill_kick`.

**The decompiler still stops at the prologue.** The body is undefined in the project and
the auto-analysis does not sweep it, so `decompile` returns the state test and the
jumptable dispatch and nothing else; recovering the rest needs the body disassembled
first. The function is named `P2P_BuildRoomStateBody` in the project so the entry is not
lost again.

**The builder's fill code names the remaining fields.** The 42 copies read the stack
object at `r1+0xd8`, and the code before them fills that object from the game-settings
record `r3` — the same record whose `+752` is the 204-byte block. The loads are direct,
so the block offsets are exact:

| body | object key | record offset | block offset | setting |
| ---: | ---: | ---: | ---: | --- |
| `0x60` | `0x62` | `+0x34e` | `+0x5E` | `host_stance` |
| `0x61`–`0x62` | `0x64` | `+0x334` | `+0x44` | `briefing_time` |
| `0x87` | `0x8a` | `+0x358` | `+0x68` | rule timer index 1 — Sneaking rounds |
| `0x88` | `0x8b` | `+0x360` | `+0x70` | index 3 — Capture rounds |
| `0x89` | `0x8c` | `+0x368` | `+0x78` | index 5 — Rescue rounds |
| `0x8a` | `0x8d` | `+0x370` | `+0x80` | index 7 — Team Deathmatch rounds |
| `0x8b` | `0x8e` | `+0x384` | `+0x94` | index 12 — Base rounds |
| `0x8c` | `0x8f` | `+0x38c` | `+0x9C` | index 14 — Bomb rounds |
| `0x8d` | `0x90` | `+0x394` | `+0xA4` | index 16 — Team Sneaking rounds |
| `0x8f` | `0x92` | `+0x3b1` | `+0xC1` | Deathmatch rounds (wire `0x169`) |
| `0x90` | `0x93` | `+0x3af` | `+0xBF` | Stealth Deathmatch rounds (wire `0x167`) |
| `0x91` | `0x94` | `+0x3b3` | `+0xC3` | Sneaking Capture rounds (wire `0x16B`) |
| `0x92` | `0x95` | `+0x3b5` | `+0xC5` | Race rounds (wire `0x16D`) |
| `0x93` | `0x96` | `+0x374` | `+0x84` | index 8 — Team Deathmatch tickets |
| `0x94` | `0x97` | `+0x37c` | `+0x8C` | index 10 — Deathmatch tickets |
| `0x96` | `0x9a` | `+0x3ad` | `+0xBD` | `sneaking_snake_kills` |
| `0x97` | `0x9b` | `+0x3ac` | `+0xBC` | `capture_extra_time` |
| `0x98` | `0x9c` | `+0x3b7` bit 0 | `+0xC7` bit 0 | unnamed host flag, bit 0 (wire `0x16F`) |
| `0x99` | `0x9d` | `+0x3b7` bit 2 | `+0xC7` bit 2 | unnamed host flag, bit 2 (wire `0x16F`) |
| `0x9a`–`0x9b` | `0x9e` | `+0x398`/`+0x399` | `+0xA8`/`+0xA9` | `unique_red`/`unique_blue`; `0x7f` when zero |

The rule-timer indices are the block's own order — SNE t/r, CAP t/r, RES t/r, TDM
t/r/tickets, DM t/tickets, BASE t/r, BOMB t/r, TSNE t/r — so the body carries each
rule's **round count** at `0x87`–`0x8d`, the two ticket counts at `0x93`/`0x94`, and
the SNE/extra-time settings at `0x96`/`0x97`. The capture's `02` run is two rounds
per rule, TSNE's `01` is one, and `0x93`/`0x94` read `50`/`30` — TDM and DM tickets.

Three corrections follow. **The briefing time is `0x61`–`0x62`, not `0x53`.** The
builder stores block `+0x44` (`briefing_time`) into object `0x64` as a halfword; the
`0x53` minute limit is a different field. **The tail is not unread, and it is not a tail.** The four bytes at `0x8f`–`0x92`
are the second half of the settings' own byte-sized timer row at wire `0x166`–`0x16D`
(SDM time/rounds, INT time, DM rounds, SCAP time/rounds, RACE time/rounds), so the body
carries the **DM, SDM, SCAP and RACE round counts** at block `+0xC1`, `+0xBF`, `+0xC3`
and `+0xC5`. The two flags at `0x98`/`0x99` are bits 0 and 2 of the byte at `+0xC7` (wire
`0x16F`), and they have **no name to give**: the fourteen bytes at wire `0x166` are one raw
block (`0x4310` `src+942`, emitted unchanged at `0xD44C3C`; `0x4313` copies it raw at
`0xD43BBC`) that the client never reads or writes, zero in all 214 archived `0x4310` pushes,
so bits 0 and 2 reach the client from the server and pass straight through. No client screen
names them and no reference server gives them a meaning. The old "bit 1 is the non-stat
game" label is our own `0x4305` reply echoing the request's `0x155` back, and the ring of
that echo is the only thing it ever measured. **The role pair is the unique-character pair.** The builder
writes `unique_red`/`unique_blue` into object `0x9e`, or `0x7f 0x7f` when both are
zero, which is why an unassigned round reads `7f 7f`.

The settings fill does not write two fields: `0x8e` (object `0x91`), the SNE dog-tag
count — the stage's "Snake wins if he collects a set number of dog tags", `2` in the
working capture and `20` in the one SNE room — and `0x95` (object `0x98`), the
checkpoint count — the stage's "Set the number of checkpoints that must be passed to
win", `5` in the working capture. The block's `unknown_72`/`unknown_76` are not their
sources: the `0x4313` spec records no live reader for either, and the builder's fill
code never loads them. The room's rule state is the writer, so the names are [V] values
plus [I] stage labels.

**The rule-state copy names both fields.** The operation-state block holds two
copies of the same 0x7c-byte rule state: the source at object `0x1a` and the
published copy at `0x96`. The copy at `0x7a8588` reads `0x1a` and writes `0x96`, so
object `0x98` is the source block's `0x1c`; the reverse copy at `0x7a7da0` restores
`0x96` to `0x1a`. Object `0x91` is in the source block itself, at offset `0x77`.
The client's presence mask confirms the field IDs: `0x308538`/`0x308af0` test field
`0x91`, and `0x308dd8`/`0x308820` set and clear field `0x98`, all through the same
per-player bitmask. So the two fields are the room's rule-state fields, not the
settings block's, and the stage labels name them: the SNE dog-tag count at `0x91`
and the checkpoint count at `0x98`.

**The object-to-body offset is settled, and the presence map is what settles it.** The
body's first nine bytes are the payload type, the state index and a seven-byte map over
the table's 55 fields, LSB first. Read against the widths, the set bits are the fields
`0x00`, `0x01`, `0x15`, `0x45`, `0x46`, `0x48`, `0x49`, `0x4c`, `0x50`, `0x52`, `0x53`,
`0x58`, `0x5a`, `0x5b`, `0x5c`, `0x5e`, `0x5f`, `0x60`, `0x62`, `0x64`, `0x66`, `0x67`,
`0x68` (the stride-17 view, not the 34-byte one), `0x8a`–`0x98`, `0x9a`–`0x9e`, `0xa0`–
`0xa2` and `0xa4`; the clear bits are `0x54`, `0x56`, `0x59`, the 8-byte `0x5a`, the
stride-2 `0x5a`, `0x63` and `0xa5` — eighteen bytes, and the body carries exactly the
other 152. Packed in table order the offsets land on every measured anchor: the slot at
`0x55`, the population at `0x56`, the selector at `0x57`, the timer vector at
`0x65`–`0x86`, the fifteen one-byte fields at `0x87`–`0x95`, the role pair at `0x9a`/
`0x9b`, the counter at `0x9d`, the tail word at `0x9e`/`0x9f` and the repeated selector
at `0xa0`. So the shift is not a constant — it is 5 before `0x58`, 3 through the timer
vector, and 3 again after the run, because the bytes the map omits are what move it.

Two corrections follow from the packing, and both are in the code now.

**Field 6 (`0x4e`, object `0x49`) is the team-kill kick, not the round count.** The
builder fills it from a halfword at the game-details record's `+0x3a6`, and that record
is the `0x4313` block at `+752` — `0x3a6 - 752 = 0xB6`, the block's `team_kill_kick`,
which the client keeps as a low byte. The values fit the kick and not a rotation length:
`3` in nineteen of the twenty-two bodies, which is the default every archived `0x4310`
capture carries. `Number of Rounds` was an inference from the lobby's labels and is
superseded.

**Field 3 (`0x4a`, object `0x45`) is `max_players`.** Its source is record `+0x332`,
which is block `0x42` — `max_players` — and its consumer `0x271ef0` enforces it against
the room's player count.

The builder's other two sources are the same record's `+0x34f` and `+0x350`, which are
block `0x5f` and `0x60` — `level_limit_tolerance` and `level_limit_base` — landing at
object `0x66` and `0x67`, body `0x62` and `0x63`–`0x64`. So all four bytes the builder
takes from the game-details record are host settings its peers need: the character cap,
the team-kill kick, and the level limit's tolerance and base.

**The role pair's derivation is not available.** `0x77b628` assigns it, but only under
conditions the corpus does not isolate: sixteen of the twenty bodies whose rotation
holds SNE or TSNE still read `7f 7f`, and the four that carry a pair are all one room's.
Writing a pair from the room's occupied slots would be a guess that disagrees with the
client in most of its own bodies, which is why this server keeps `7f 7f`.

### What the shipped assets do and do not contain

Naming a field from data alone is not possible here, and it is worth saying which
sources were checked. `pc-client/game/params` holds three NPC parameter files
(`npc_parameter_mission.mgaparam`, `npc_parameter_tdm.mgaparam`,
`npc_system_parameter.mgaparam`); `game/mgo2_slots.txt` is a stage-sector list
(`slot_oct_list_online <slot> <sector> <sectors>`); `game/slotdat/` holds the
cosmetic slot files (`slot_online_head.slot` and its neighbours); and
`.gametools/replay-console` is a 3D replay viewer, whose every `rotation` match is
quaternion code. None of the four describes a settings field. `.gametools/Haven`
is stage, GEO and GCL tooling. So a name has to come from the code that reads a
field, which is what the call-site table below sets up.

The `flags` word is what still has no reading: `0x0064` marks the slot index and
one other byte (`0x58`), `0x0125` the sixteen-byte field and the rotation block,
`0x0225`/`0x0235` the widest variants at `0x5a` and the timer vector, `0x0025`
nearly everything else, and `0x0000` four bytes. It is not a width or an order.

### What the call sites show, key by key

53 of the accessor's 283 call sites pass a constant key the descriptor declares.
Grouped by key, with what the caller does with the value it gets back:

| key | width | sites | what the caller does |
| ---: | ---: | ---: | --- |
| `0x00` | 1 | 25 | compares it against `0` and `7` and branches to the same place — a phase with two "not playing" values |
| `0x01` | 16 | 3 | returns one byte of the block (`lbz r3, dst[n]`) and compares one byte against another — a sixteen-byte text field read character by character |
| `0x15` | 48 | 5 | the rotation block, always paired with a `0x52` fetch |
| `0x45` | 1 | 1 | loaded into a per-round loop that also walks a list at `-0x8000(r30)` |
| `0x4c` | 4 | 8 | tests **bit `0x20`** (`rlwinm r0, r0, 0, 0x1a, 0x1a`) and stores the answer at `+0x6244` of its own object — this is the bit the port already knows is read |
| `0x52` | 1 | 5 | the slot, multiplied by three and added to the block base; `0x27bb10` wraps it past 14 |
| `0x59` | 1 | 2 | passed as the id argument (`r4`) to `0xf0ea88` with a destination buffer — an index into a table, not a value |
| `0x5a` | 8/4/1 | 2 | returns one byte from inside the variant (`lbz r3, dst[1]` and `dst[5]`) |
| `0x63` | 1 | 2 | fetched, then the caller branches on its own `+0xb8` counter and a `0xff` sentinel |

### The two hot keys, traced

**Key `0x00` (body `0x09`, width 1, flags `0x0025`) — 25 sites in 17 functions.**
Two of them, in `FUN_00288160` and `FUN_00288540`, branch away when the value is
`0` or `7`; a third (`0xaa1bb4`) compares it with `3`; the other twenty-two hand
it to flow helpers (`0xf9d840`, `0xf9dca0`, `0x97c124`, `0xe6fc08`, `0xcdc90`,
`0xd6f5b8`, `0x2891e0`). So the field is an enumerated phase in `0..7` where `0`
and `7` are outside a running round and `3` is a phase a caller names — and the
corpus only ever shows `2`, `3`, `4` and `5`, which fits that range and never
reaches the two boundary values.

**Key `0x4c` (body `0x4f`, width 4, flags `0x0025`) — 8 sites in 6 functions.**
It is a bit field, and the client reads three bits out of it:

| bit | instruction | site | effect |
| --- | --- | --- | --- |
| `0x20` | `rlwinm r0, r0, 0, 0x1a, 0x1a` | `0xa3a90`, `0xa3c6c`, `0xa5690`, `0xa5720`, `0xa5904` | branches; one site stores the answer at `+0x6244` of its own object |
| `0x40` | `rlwinm r3, r3, 0, 0x19, 0x19` | `0x8eb008` (in the getter family) | returned as a boolean by a getter |

Bit `0x40` has a getter of its own: `FUN_008eafe0` fetches key `0x4c` (4 bytes) and
returns `rlwinm r3, r3, 0, 0x19, 0x19` zero-extended — a boolean property in the same
family as the map, rule and third-component getters. Its `.opd` entry is
`0x1203e60`, and it appears in **no method table and at no call site**: unlike the
rule getter, which sits in nine tables (bases `0x119d250`, `0x119d270`, `0x119d2d8`,
surrounded by the `0x8eb`/`0x8ed` getter family), this one is unreferenced in the
shipped build. Its name cannot come from a slot, because it has none.

Bit `0x20` is branched on by the lobby code at `0xa3978`, `0xa3af8` and `0xa5030`,
which also store the answer at `+0x6244` of its own object. Their shape is dispatch,
not property logic: `0xa3978` has five callers (`0xb3ca0c`, `0xb7b8e8`, `0xb97d58`,
`0xbb8568`, `0xbb8b50`) and branches on the values `0xa` and `6` from two state
getters; `0xa5030` has one caller (`0xa76fc`) and switches on `[r31+0x70]` compared
against `4`. Neither compares the flag with a room concept, so the bit still has a
reader but no name.

Against the corpus: bit `0x20` is set in **all fifteen** rooms, and bit `0x40` in
exactly the two rooms whose rotation is rule 4/15 (`joined-on-briefing-then-game`,
`game-started-team-selection`). That is a correlation, not a name; what it rules out
is the reading that field 7 is a plain number — two bits of it are decisions the
client makes.

**The rule reading of bit `0x40` is refuted, and a better candidate exists.**
`mgo2-game2` rotates rule 15 and writes `0x00034033`, so the bit does not follow the
rules. What the two rooms have in common is their host: both are
`69.213.233.238:5731`, and `briefing/working server/working-server-4-sessions.txt`
records that this room is the one that *asked which team to join* before the round —
blue, auto or red. The bit is therefore the room's "choose your side" condition,
**[I]** on one host and one note, and it is the best-named unresolved bit in the body.

One earlier reading is corrected here: the `rlwinm r0, r9, 0, 0x1d, 0x1d` at
`0xa3c74` tests `r31+0x2c0`, not the word the accessor fetched, so it is not a third
bit of field 7.

That is as far as the reader side goes. No asset in `pc-client` and none in
`.gametools` names these fields, and no string in the binary annotates the
descriptor, so a name would have to be consistent with the width, the flags word
and the usage shape above rather than read off a table. Two keys can be named
outright — `0x15` (rotation) and `0x52` (slot) — and one described but not named:
the `0x4c` word's bit `0x20`.

### The writer: found — `FUN_0027e578`, and what it copies

`FUN_0027e578` is the setter counterpart of the getter: the same 26-entry table
walk over the same ten-byte records, but it stores `r5` bytes of `r6` into the
object at `r4`. It has **210 call sites**, and one cluster — `0x9e9184` to
`0x9e9590`, 42 calls in ascending key order — is the settings object being filled
field by field:

| order | key | width | source | | order | key | width | source |
| ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | --- |
| 1 | `0x01` | 16 | `r1+0xd9` | | 22 | `0x8a` | 1 | `r1+0x162` |
| 2 | `0x11` | 4 | `r1+0xe9` | | 23 | `0x8b` | 1 | `r1+0x163` |
| 3 | `0x15` | 48 | `r1+0xa6` | | 24 | `0x8c` | 1 | `r1+0x164` |
| 4 | `0x45` | 1 | `r1+0x11d` | | 25 | `0x8d` | 1 | `r1+0x165` |
| 5 | `0x49` | 1 | `r1+0x121` | | 26 | `0x8e` | 1 | `r1+0x166` |
| 6 | `0x4c` | 4 | `r1+0x78` | | 27 | `0x8f` | 1 | `r1+0x167` |
| 7 | `0x50` | 2 | `r1+0x72` | | 28 | `0x90` | 1 | `r1+0x168` |
| 8 | `0x52` | 1 | `r1+0x12a` | | 29 | `0x92` | 1 | `r1+0x16a` |
| 9 | `0x53` | 1 | `r1+0x12b` | | 30 | `0x93` | 1 | `r1+0x16b` |
| 10 | `0x54` | 2 | `r1+0x12c` | | 31 | `0x94` | 1 | `r1+0x16c` |
| 11 | `0x56` | 2 | `r1+0x12e` | | 32 | `0x95` | 1 | `r1+0x16d` |
| 12 | `0x58` | 1 | `r1+0x130` | | 33 | `0x96` | 1 | `r1+0x16e` |
| 13 | `0x59` | 1 | `r1+0x131` | | 34 | `0x97` | 1 | `r1+0x16f` |
| 14 | `0x5a` | 8 | `r1+0x7c` | | 35 | `0x9a` | 1 | `r1+0x172` |
| 15 | `0x62` | 1 | `r1+0x13a` | | 36 | `0x9b` | 1 | `r1+0x173` |
| 16 | `0x64` | 2 | `r1+0x13c` | | 37 | `0x9c` | 1 | `r1+0x174` |
| 17 | `0x66` | 1 | `r1+0x13e` | | 38 | `0x9d` | 1 | `r1+0x175` |
| 18 | `0x67` | 1 | `r1+0x13f` | | 39 | `0x9e` | 2 | `r1+0x176` |
| 19 | `0x68` | 34 | `r1+0x84` | | 40 | `0x46` | 2 | register |
| 20 | `0xa0` | 1 | `r1+0x70` | | 41 | `0x48` | 1 | register |
| 21 | `0xa4` | 1 | register | | 42 | `0x00` | 1 | register |

The decisive detail is the arithmetic: **27 of the 42 read their source at
`r1+0xd8+key`** (`0x45`→`0x11d`, `0x49`→`0x121`, `0x52`→`0x12a`, `0x53`→`0x12b`,
`0x54`→`0x12c`, `0x56`→`0x12e`, …, `0x9e`→`0x176`). That is a stack copy laid out
exactly like the object, so the record's key really is the object's own offset —
confirmed independently of the accessor's `key + length * stride` arithmetic. The
other fifteen take a computed temporary (`0x01` at `r1+0xd9`, `0x11` at `r1+0xe9`,
`0x15` at `r1+0xa6`, `0x4c` at `r1+0x78`, `0x50` at `r1+0x72`, `0x5a` at `r1+0x7c`,
`0x68` at `r1+0x84`) or a register (`0x00`, `0x46`, `0x48`, `0xa0`, `0xa4`).

**What this settles, and what it does not.** It settles the key's meaning and the
order the client fills the object in. It does not give the wire offsets, and the
search for the encoder needs one correction first: an earlier pass here reported
that no code stores at wire offsets `0x55`, `0x56`, `0x65` or `0x9c`, but that scan
was invalid — Capstone's generator stops at the first undecodable byte, so it had
only walked a fragment of the text segment. Re-run with `skipdata` over the whole
segment (**4 585 680 instructions**), the answer changes:

* **Two functions do store at those offsets** — `FUN_00d6c4a0` and its twin
  `FUN_00d6cda8`, at `0x55`, `0x56` and the `0x9c`–`0x9f` tail. But the pattern is
  not per-field: the stores come in runs of consecutive registers into consecutive
  bytes (`stb r0, 0x10`, `stb r8, 0x11`, `stb r7, 0x12`, …, at the `0x10`, `0x50`
  and `0x90` columns). That is an unrolled sixteen-byte block copy of a
  body-sized buffer — a transport of the body, not a serializer that computes it.
* **Nothing loads the length and the map together.** No function holds both `0xa1`
  and the presence-map bytes `0x27`/`0xfe`/`0xee`/`0x3f`; the one candidate that
  looked like it, `FUN_00b14ae0`, is a six-instruction dispatcher on a small enum.
* **No literal pointer to the descriptor table.** The raw search still stands: no
  aligned 32-bit reference to `0x1229b80` or to a record inside it exists in the
  image (the only hit is unaligned bytes at file `0x12198ac`).

The callers were then traced. `FUN_00d6c4a0` and `FUN_00d6cda8` have **no direct
call site**, and the method tables settle what they are. The tables are arrays of
32-bit `.opd` pointers, and `FUN_00d6c4a0`'s entry appears exactly once, in the
table at `0x11db5d8`:

```
  0x11db5dc -> 0x00d6c4a0   0x11db5e0 -> 0x00d6c460   0x11db5e4 -> 0x00d6cca8
```

Three adjacent copiers, nothing else — a transport family, not a serializer. The
encoder is therefore not behind a table that holds these functions, and the static
anchors are exhausted (see the port check below).

### The port's own DLLs do not hold the encoder

`system.dll` and `savemgo.dll` are both PE32 for x86 (image base `0x10000000`),
16.6 MB and 4.7 MB. `savemgo.dll` carries the record type (`0x1001`, 18 code
immediates) and body-sized lengths (`0xa1`, 117 code immediates), but **no function
holds both**: zero pairs fall within 0x100 bytes of each other. Its hits are a
network and graphics layer — the `rotation` string is `SDL.surface.rotation`, the
`settings` one is a DXVK config comment. Neither DLL contains the presence-map
bytes (`ff 27 fe ee`), which is what a serializer would emit. The encoder stays in
the PPC client.

## 6. Open

* field 3 (`0x4a`) and field 6 (`0x4e`), which are per-room and correlate with
  nothing in the corpus;
* field 7's two flag bits and field 8, which the native readers test but do not
  name — field 8 is now measured to be whole minutes of some unidentified time;
* field 5 (`0x4d`), which is state rather than a setting and whose two measured
  values (`02`, `14`) name nothing;
* the capture's tail byte at `0x9d` and the room's tail word at `0x9e`, which are
  two fields and not one, and match no timer, slot, count or checksum measured
  here; the two per-team words at `0x5a` and `0x5e`, which are per-round state and
  are not Drebin points;
* fields 30–49, of which nine bytes are per-room and two are slot references;
* elements 8–11 of field 29, which are a per-room-family constant but carry no
  reading as time;
* the descriptor table's `flags` word, and the object offsets of the fields whose
  wires the body places after `0x64` — the widths match one field each, but the
  packing order there is not yet traced through the writer;
* the object-to-wire step itself: the setter fills the object, and no code was
  found that lays that object out as the 161-byte body.

## 7. The wider corpus of 3 November, and what it corrects

Three more captures — the `briefing/` set — add five rooms to the fifteen above:
`briefing-then-solo-game` (`64.181.216.250:5720`), `working-server-log` and
`working-server-4-sessions`, which between them hold that host's second room on
`:5722`, a second room on `69.213.233.238:5731`, and the eleven-slot room on
`143.47.227.126:5725`. Recounted over the whole set, **twenty-two distinct bodies
from eleven rooms and nine hosts**:

| room | slots | pop | rot | phase | rules | max map |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| `briefing-then-solo-game` (`64.181.216.250:5720`) | 9 | 2 | 4 | 2 | 1,2,3,5,6,16 | 14 |
| `working-server-log` (`64.181.216.250:5722`) | 15 | 3 | 1 | 3 | 1–7,16 | 17 |
| `working-server-log` (`69.213.233.238:5731`, second room) | 15 | 2 | 8 | 3 | 4,15 | 19 |
| `working-server-log` (`143.47.227.126:5725`) | 11 | 14 | 6 | 4 | 1,5,7,12 | 17 |

**`myserver`'s body is not evidence.** It is the reference's, field for field
(`7e e5 0e` at the tail, as in `mgo2-started-briefing-then-solo-game`), because
`BuildCore` pins that capture; counting it as a second measurement of a pinned
offset confirms the pin and not the wire.

What the recount settles, and where the tables above are corrected:

* **Field 5 (`0x4d`) is state.** `0x02` in twenty-one bodies and `0x14` (`20`) in
  the body `joined-on-briefing-then-game` wrote while its room was held at the
  briefing — the same host and rotation as bodies reading `0x02` in another phase.
* **Field 6 (`0x4e`) is the per-room `3`/`2`/`6` byte**, not field 5. Its `2` is the
  two six-slot rooms of one host.
* **Field 3 (`0x4a`) is the room's, not the host's**: two rooms of one host differ.
  It only ever holds `13`, `15` or `17`.
* **Field 8 (`0x53`) is whole minutes**: `0`, `180`, `240`, `300`, constant across a
  room's own bodies. The `0x002c` recorded above is the low byte of `0x012c`.
* **Field 7's bit `0x40`** is set in the four bodies of `69.213.233.238:5731` and in
  no other body; bit `0x20` is set in all twenty-two. The bit does not follow the
  rules — `mgo2-game2` rotates rule 15 without it — and the room it does follow is
  the one whose joins asked the client which team to join, per
  `briefing/working server/working-server-4-sessions.txt`: so it reads as the room's
  team-selection condition, **[I]**.
* **The tail is two fields**: `0x9e` holds across a room's bodies (`277` for all
  three of `143.47.227.126:5725`, `296` for the three of `64.181.216.250:5720`,
  `3813` for the five of `99.66.131.177:5731`) while `0x9d` does not.
* **The words at `0x5a` and `0x5e` are a per-round pair**: one room's bodies hold
  them equal (`5/5`, `40/40`, `50/50`, `55/55`, `0/0`), another's hold them apart
  (`15/33`, `27/18`, `30/32`, `38/44`, `47/35`, `48/47`). **Within one capture the pair
  does not move** — every room re-reads it identical one to two seconds later
  (`40/40`, `55/55`, `0/0`, `5/5`, `47/35`, `50/50`) — so it is a room's standing total
  and not a clock. Two per-team values, equal at some moments and apart at others, is
  what a *remaining ticket count* looks like, and the client's own TDM panel uses that
  word for it: "reducing the other team's tickets to zero"
  (`docs/analysis/rule-instruction-texts.md`). That reading is **[I]**, not **[V]**: it
  fits the shape and the vocabulary and nothing here observes a ticket spent. They are
  not Drebin points — the capture's Drebin values sit in a 54-byte `0x1001` record of
  their own (`docs/analysis/drebin-point-records.md`) and no 161-byte body carries
  `1000` or `5000`.

The files still cannot name these fields. Every one of them is a room-creation
setting, and the gameplay wire only ever shows the value its host already decided:
their source is the lobby's room-settings reply, which none of these captures hold.
The client log in the `briefing/` set does not hold them either — its only room fact
is stage order, and it confirms nothing beyond `0x55` selecting `triple[slot][0]`.

## 8. What the client's own Create Game screen calls them

The PC client downloaded on 3 November carries the Create Game menus in
`game/stage/lobby/scenerio.gcx`, so the list of settings these fields can be is now
read rather than guessed. The common settings block, with its offsets in that file:

| setting | offset |
| --- | --- |
| `Round Time`, `TIME(min.)` | `0x0cce91`, `0x0ccdcf` |
| `Number of Rounds` | `0x0ccf8f` |
| `Extra Time` | `0x0ccfe8` |
| `Starting Number of Tickets` | `0x0cceed` |
| `Number of Snake Eliminations to Win` | `0x0cd02c` |
| `Checkpoints` | `0x0cd0f8` |
| `Max Number of Characters` | `0x0cfd9c` |
| `Briefing Time` | `0x0cfe3b` |
| `Headshots`, `Lock On (AUTO AIM)`, `Enemy Nametag Display` | `0x0cfeb5`, `0x0cfefb`, `0x0cff6c` |
| `Silent Mode`, `Auto Assign Teams`, `Teams Switch Positions` | `0x0cffff`, `0x0d0066`, `0x0d00fc` |
| `Ghost Pranks`, `Allow Quick-Join` | `0x0d0193`, `0x0d0200` |
| `Level-Limit` | `0x0d0289`, `0x0d02a5` |
| `Team Kill Kick`, `Idle Kick`, `Auto Balance Teams` | `0x0d02dd`, `0x0d0366`, `0x0d03ef` |
| `Weapon Restriction`, `Clan Members Only`, `Allow Solo Players` | `0x0d04b1`, `0x0d0564`, `0x0d0632` |
| `Log Personal Stats`, `Dedicated Host Settings` | `0x0d06d3`, `0x0cfb21` |

That list is the whole candidate set: the body's unresolved bytes are these settings,
not conditions the game invents. It also explains a shape that did not have one —
**field 7 is four bytes because the common settings are a dozen and a half switches**,
and the client's reader at `0x8eb008` returns one bit of it as a boolean.

### What the values now single out

| body field | measured values | the candidates, and how far each is supported |
| --- | --- | --- |
| `0x4a` field 3 | `13`, `15`, `17`, per room | **`Max Number of Characters`** — settled by the builder (§ "The record the builder reads"): it writes the settings block's `+66` byte into this slot. The `Level-Limit` candidate is excluded, because the level limit is a different field of that block (`+95`/`+96`) which this path never reads |
| `0x4d` field 5 | `02`, and `14` in one body | a **state** byte: it moves between bodies of one host while the settings around it do not. `14` = 20 is the shape of a countdown or a count remaining **[U]** |
| `0x4e` field 6 | `2`, `3`, `6`, per room | The team-kill kick. The builder fills it from record `+0x3a6` = block `0xB6`, and `3` is the default every archived `0x4310` capture carries. The `Number of Rounds` reading came from the screen label and is superseded. **[ELF]** |
| `0x53` field 8 | `0`, `180`, `240`, `300` | a whole-minute limit, but **not the briefing time**: the builder fills block `+0x44` into object `0x64`, which the presence map puts at body `0x61`–`0x62`. What the `0x53` limit is remains **[I]**. |
| `0x4f` field 7 | 4 bytes, bit `0x02` in all 34 bodies | the **room's switch word**. The single-variable captures in `.pcapngs/settings` place Auto Assign Teams at `0x10000`, Teams Switch Positions at `0x20000`, Lock On (AUTO AIM) at `0x40000` and the Level Limit enable at `0x400000`; Friendly Fire moves `0x01` and `0x10` together. Bit `0x02` is set in every room including the Drebin-disabled one, so it is not the Drebin flag — that is the rotation triple's third byte. The earlier `0x40` = Auto Assign reading is superseded (`docs/protocol/UDP_SETTINGS_PAYLOAD.md`) |
| `0x5a`, `0x5e` | `5/5`, `40/40`, `47/35`, `15/33`, … | each team's **remaining tickets**. `0x93` is the room's *starting* count and this pair never exceeds it, so direction is now measured rather than assumed: the pair equals `0x93` in the rooms whose round has not been spent (`40/40` with `0x28`, `50/50` with `0x32`, `55/55` with `0x37`) and sits below it once players have respawned (`30/32` and `5/5` against `50`; `5/5` against `20`). The TDM panel states the mechanic — "Each team starts with a set number of tickets… steal tickets" — and the tooltip at `0x0cee5e` says a ticket is spent on each respawn |
| `0x93` | `20`, `40`, `50`, `51`, `55` | the **Team Deathmatch ticket count** — block rule timer index 8, the TDM tickets setting — in the rooms that play a ticketed rule: never below the pair at `0x5a`/`0x5e` in any of the 34 bodies, and equal to it in the thirteen whose round is untouched. The SNE/SCAP room carries `51` because the TDM setting is still 51 even when TDM is not played, which is why the count appears where no tickets are spent. **[ELF]** |
| `0x9a`, `0x9b` | `7f 7f` in most, `03/02`, `03/06`, `04/09` in one room | the **unique-character pair** — the players holding Snake and Metal Gear Mk.II, from block `+0xA8`/`+0xA9` (`unique_red`/`unique_blue`), with `0x7f` when both are zero. All fourteen bodies whose rotation holds neither SNE nor TSNE read `0x7f 0x7f`; a real pair appears only in the SNE/SCAP room, while sixteen of the twenty bodies that *do* hold SNE or TSNE still read `0x7f 0x7f`. So the pair is the assignment for the round being played, not a room setting, and it moves between bodies of that one room (`03/02` → `03/06` → `04/09`) |
| `0x87`–`0x9b` | `… 32 1e 05 03`, `… 06 00 01 06 14 02` | the builder's fill code names the run: `0x87`–`0x8d` are the SNE/CAP/RES/TDM/BASE/BOMB/TSNE round counts (rule-timer indices 1, 3, 5, 7, 12, 14, 16), `0x8e` the SNE dog-tag count, `0x93`/`0x94` the TDM/DM ticket counts (indices 8, 10), `0x95` the checkpoint count, `0x96`/`0x97` the Sneaking kill count and Capture extra time, `0x8f`–`0x92` the DM/SDM/SCAP/RACE round counts, and `0x98`/`0x99` bits 0 and 2 of an unnamed host-flag byte. `0x9a`/`0x9b` are the unique-character pair. **[ELF]** |
| `0x91` | `2` everywhere, `20` in the one SNE room | the **SNE dog-tag count** — the stage script's SNE text: "Snake wins if he collects a set number of dog tags." The block's `unknown_72` is not the source: the `0x4313` spec records no live reader for it, and the builder never loads it. The room's SNE rule state is the writer. **[V] values, [I] name** |

### What the stage script adds beyond the labels

Three things, and one of them reopens a question rather than closing it.

**The unit words confirm the ticket pair.** The screen prints a value's unit as its own
string, immediately before the labels that use it: `ROUND` and `ROUNDS` at `0x0cce0e`
and `0x0cce14`, `TICKET` and `TICKETS` at `0x0cce28` and `0x0cce2f`, then `tickets`,
`Tickets` and `ticket` at `0x0cd162`–`0x0cd172`, sitting beside `Round Time`
(`0x0cce91`), `Number of Rounds` (`0x0ccf8f`) and `Starting Number of Tickets`
(`0x0cceed`). So a team's number on this screen is denominated in tickets and not in
points or score, which is why the `0x5a`/`0x5e` pair is read as tickets and the two
`0x1001` Drebin words are not.

**The level limit is a pair, not a number.** The only numeric format string the settings
screens use is `%d ± %d` (`0x0d09fd`), and it follows the `Allow`/`Disallow` labels that
begin the level block, so the client presents `Level-Limit` as a base with a spread.
That pair is now measured: `0x63` is the tolerance and `0x64` the base, in that order.
The `.pcapngs/settings` capture that enables the limit and selects level 8 ± 2 writes
`0x02`/`0x08`, a room without the option writes `0x00`/`0x00`, and the working
server's body keeps `0x02`/`0x0a` with the enable bit clear
(`docs/protocol/UDP_SETTINGS_PAYLOAD.md`). Field 3 is not the base — it is the
character cap.

**The room list has no status words at all.** The list screen's column headers are in
the file — `STATUS` at `0x0b0cdf`, `HOST SCORE` at `0x0b04fb`, `HOST NAME` at
`0x0b04b9`, `LEVEL` at `0x0b0ae2`, `AVG.LEVEL` at `0x0b026e`, `PLAY TIME` at
`0x0b094e`, `GAME TYPE` at `0x0b042d`, `RULE` at `0x0b0c57`, `MAP` at `0x0b06b2` — but
no value it can print in that status cell. The whole file's short upper-case strings are
`LOCKED ON`, `OPEN`, `NOW LOADING`, `NEXT`, `STATUS`, `ENDZIEL`, `LOCKED`, `WAITING ROOM`,
`NEXT PAGE` and `FULL SCREEN`; none of them is a room state. So the status cell is drawn
from something other than these strings — a number, or an image — and the state bytes
(`0x4d`, and the phase at `0x09` that the client compares with `0`, `3` and `7`) cannot
be named from this file at all. A lobby packet capture remains the only route to them.

### What the rule panels add

Grouping the corpus by which rules a room's rotation holds separates two fields out of
the `0x87`–`0x9b` run that nothing else did, and gives a third a direction.

**The Sneaking role slots.** All fourteen bodies whose rotation holds neither SNE nor
TSNE read `0x7f 0x7f` at `0x9a`, and the four bodies that carry a real slot pair all
belong to one room — `69.213.233.238:5731`, which plays SNE and SCAP — where the pair
moves between bodies (`03/02` → `03/06` → `04/09`). The other sixteen bodies that do
hold SNE or TSNE read `0x7f 0x7f` as well, so the reading is not "this room has Snake" but
"which slots hold the roles for the round being played": an unassigned pair is a round
that is not a Sneaking round. The panels name the roles: "One player controls Snake…
If many players join, one player becomes Metal Gear Mk.II and can support Snake."

**The ticket pair's direction.** `0x93` holds `20`, `40`, `50`, `51` or `55`, and the
pair at `0x5a`/`0x5e` is at or below it in all 34 bodies: equal where the round is
untouched (`40/40`, `50/50`, `55/55` against `0x28`, `0x32`, `0x37`) and below where it
has been spent (`30/32` against `50`, `5/5` against `50`, `5/5` against `20`, `15/33`
and `48/47` against `50`). So in a ticketed room `0x93` is the starting count and the
pair is what is left of it — the two labels the screen gives are `Starting Number of
Tickets` (`0x0cceed`) and `TICKETS` (`0x0cce2f`), and the tooltip at `0x0cee5e` states
the rule: "One ticket is used each time a player respawns on the field." The SNE/SCAP
room holds `51` with no ticketed rule in its rotation, which is why the reading is
scoped to rooms that play one.

**Snake's dog-tag count.** `0x91` is `2` in every body but the SNE room, which carries
`0x14` = `20`. The SNE panel's win condition is "Snake wins if he collects a set number
of dog tags", so 20 is a candidate for that number — one room, one value, **[U]**.

**What would close it.** The room-details reply (`0x4313`) for one room carries the
same settings as values, so one capture of a join alongside its gameplay wire turns
every **[I]** in that table into a **[V]** — and the two candidates for field 3 into
one.
