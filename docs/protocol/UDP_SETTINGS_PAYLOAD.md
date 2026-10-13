# The `0b` settings payload — host rotation and per-player loadout

## Finding

The `0b <slot>` family a dedicated host sends over the gameplay channel is the
replay console's `TYPE_SETTINGS` record (`TYPE_SETTINGS = 0x0b`): the host's map
rotation, and each player's appearance and equipped skills. It was previously
filed as undecoded — *"Loadout — bodies 25–161 bytes [U]"* — and, when one body
was run through the roster reader, it came back as a roster entry with a name of
control bytes (`\x0d\x05`). Both readings are the same record; this is what it
decodes to.

The layout is **ported, not re-derived**, from `rpdt.js` (`?v=ins236`), the
decoder the SaveMGO replay console loads. A verbatim copy is committed at
`docs/analysis/replay-console/rpdt.js` — that copy is what these offsets were
read from, and its hash is in that directory's README. Its `appearanceRecord`
and `skillRecord` functions are the source of every offset, length and alignment
rule below. The captures are the evidence that the port is
right: the sample values in each section are what it produces for
`mgo2-game1.pcapng` and `mgo2-game2.pcapng`.

The decoder is `tools/udp_settings_semantics.py`; `tools/pcap_roster_acks.py`
prints it under *"settings payloads (0x0b)"*.

## Envelope

| offset | size | field |
| --- | --- | --- |
| `0x00` | 1 | `0x0b`, the shape discriminator |
| `0x01` | 1 | slot / kind byte |

Body lengths run 3 to 161. The leading `0x0b` is a property of the **body**, not
the record identifier — a `0x1001` or `0x9001` record carries a `07 48` roster
entry on one datagram and a `0b` payload on the next (`UDP_GAME_CAPTURE.md` §4).
**[V]**

The slot byte picks the shape:

* `0x00` with `L >= 71` — the host's own settings/rotation record.
* `>= 1` with `L` in `{50, 51, 53, 54, 55, 56, 57, 58}` — a player loadout,
  whose slot is the byte minus one, so slot `0` is the same first joining player
  the roster counts from zero. The host itself is the rotation record, never a
  loadout (`rpdt.js` requires `b[a + 1] >= 1`). **[V]**

## When they arrive — and whose body the slot-0 loadout is

A `0b` payload is **not** part of the roster run. The run carries roster entries
only; the host's own record is a later frame, in the post-join burst. Measured,
both captures **[V]**:

| capture | roster run | host's `0b 00` rotation | loadouts in the same frames |
| --- | --- | --- | --- |
| `mgo2-game1.pcapng` | t+4.170, `0x9001`/`0x1001` roster entries only | t+6.687, 161 B — 2.5 s later | slots 0 and 20 at t+6.687; each later joiner with its own join |
| `mgo2-game2.pcapng` | t+6.112, roster entries only | t+10.236, 161 B — 4.1 s later, then re-sent | slot 1 at t+6.112, and again beside the rotation |

**The slot is the room's own player-table key, and one of the bodies is the
host's.** Every loadout-shaped record in both captures has `slot byte >= 1`, so
`appearanceRecord` and `skillRecord` — which refuse `b[a + 1] < 1` outright —
read all of them; the keys they name are the same ones §8.4 of
`UDP_GAME_CAPTURE.md` reads out of the same capture's roster records. In `game1`
the five joining players hold keys `14`, `0f`, `04`, `0b` and `06`, and the six
loadout bodies name those five **and slot `0`**, which no joining player holds —
slot `0` is the host's own entry in its player table
(`RoomRosterService.HostSessionSlot`). The burst's pairing says the same thing:
the two bodies it writes while the room holds the host and its first joiner are
slot `0` and slot `14`, the two players who are there. So a slot-0 loadout is
sent, and it is the host's own; an earlier reading of this file called it a
sixth joining player. **[V]** for the keys and the pairing; that the slot-0 body's
appearance is the host character's own **[I]** — in the capture it is identical to
the joiner's, both being default appearances.
The host also sends a rotation record, and its 90 bytes after the 15 triples
(`0x47..0xa0`) are **[U]**, not an appearance block: read as one, they give gear
ids `02 02 02 04 00 02 04` where the observed characters' gear is items like
`1e 45 2f 57 39 67 68`. The host's own appearance is therefore the slot-0 loadout
body above and not a section of the rotation — which is what the live host writes,
from its own character's row.

## The rotation record — `0b 00`, `L >= 71`

Fifteen three-byte entries beginning at offset `0x1a`:

| bytes | field |
| --- | --- |
| `0x1a + 3i + 0` | map id |
| `0x1a + 3i + 1` | mode / game rule |
| `0x1a + 3i + 2` | unresolved |

`rpdt.js` picks the entry whose map equals the file header's map and reads that
entry's `[1]` as the round's mode, which names the first two. **[V]** for the
offsets and the count; the third byte is `0x00` in `game1` and `0x00` for all
but one entry in `game2`, and is **[U]**. The live server writes it from the
room's own flags byte — the only non-zero value this position was ever seen to
hold — and what the peer does with it is still **[U]**.

* `mgo2-game1.pcapng` (`0b 00`, 161 B, t+6.687) — map/mode
  `5/1, 7/5, 10/3, 6/7, 14/1, 15/4, 3/1, 17/2, 2/16, 9/1, 12/5, 4/4, 1/1, 8/6, 13/5`.
* `mgo2-game2.pcapng` (`0b 00`, 161 B) — `7/15, 5/7, 15/7, 2/7, 10/7, 6/7, 7/7,
  9/7, 12/7, 3/7, 17/7, 4/7, 8/7, 1/7, 13/7` — one mode repeated, a single-rule
  rotation (`rpdt.js`'s `TSNE_RULE` is `7`). **[V]**

## The loadout record — `0b <slot + 1>`, `L` in 50–58

| offset | size | field |
| --- | --- | --- |
| `0x00` | 1 | `0x0b` |
| `0x01` | 1 | slot + 1 |
| `0x02` | 1 | team byte |
| `0x16` | 1 | skill-count byte that sizes the skill region |
| `L - 23` | 23 | appearance block |
| `0x17 .. L - 24` | var | skill pairs |

### Appearance — the trailing 23 bytes

Field order, `rpdt.js`'s `APPEARANCE_FIELDS`, byte `L-23+i`:

| # | field | # | field | # | field |
| --- | --- | --- | --- | --- | --- |
| 0 | head | 8 | chestColor | 16 | facePaint |
| 1 | chest | 9 | handColor | 17 | upperColor |
| 2 | hand | 10 | waistColor | 18 | lowerColor |
| 3 | waist | 11 | footColor | 19 | voice |
| 4 | foot | 12 | gender | 20 | pitch |
| 5 | acc1 | 13 | face | 21 | acc1Color |
| 6 | acc2 | 14 | upper | 22 | acc2Color |
| 7 | headColor | 15 | lower | | |

`gender` is `0` male, `1` female, and is **measured** (`rpdt.js` carries it as
"item keys, gender 0 = male"); the seven colour bytes' order is **inferred** by
the replay decoder and is marked so there. **[V]** for the offsets; **[I]** for
the colour order.

**The side byte is the lobby array's team group, doubled.** `rpdt.js` aligns the
lobby-array team groups it reads from the indexed `0x85`/`0x86` pair against this
byte over 30 recordings and reports group `0` as byte `0` and group `1` as byte
`2` for **323 of 323** players carrying both, which is the mapping the live server
writes from (`LoadoutFramesUtils.SideByteOf`). The same decoder warns that a body
whose byte is uniform across every slot is the *unassigned default* rather than a
real assignment — every loadout of `game1` carries `02` — so a player the room's
rows put on no side keeps the recorded `02`, which is also what side `1` codes to.
**[V]** for the mapping over the 30 files; **[I]** for reading this byte as the
player's side in the room the record is sent to.

**Three shapes, and no fourth.** A body's length is 49 bytes plus its skill
region, and the region is `0xff` and one pair per two bytes (an optional `0x10`
suffix aside), so the lengths the decoder accepts leave exactly three shapes: no
pair (`0b 01`, 50 B), two pairs (54 B) and three pairs (56 B). The accepted set
contains no 52, which is the length a single pair would need, and a body with four
pairs would decode as garbage because the decoder reads three pairs from the
region's end; a character whose equipped skills are neither none, two nor three has
no body to be written in. **[V]** for the shapes and the arithmetic.

**This is not this repository's own appearance order.** `PROTOCOL.md`'s 27-byte
appearance struct runs `gender, face, upper, lower, face paint, upper colour,
lower colour, voice, pitch, [4 zero], head … accessory-2 colour`; this network
block is the same fields in a different order, without the four zero bytes, and
byte 0 is `head`, not `gender`. The two are not interchangeable. **[V]**

**The cross-sample constant is a character's gear, not a template.** §6.6 of
`UDP_P2P_PROTOCOL.md` and §Loadout of `UDP_COMMANDS.md` read `45 2f 57 39 67 68`
as a fixed template. At body offset `0x1a` is `head` and `0x1b` is `chest`, so
those six bytes are `chest, hand, waist, foot, acc1, acc2` — the character's
gear ids, the same field the join request carries at body offset `0x11`. **[V]**

### Skills

`skillRecord`: the count byte at `0x16` sizes the region, the region ends on
`0xff`, an optional `0x10` suffix is dropped, and the pairs are then read back
from the region's end as **three, two or one** `(skill id, level)` pairs — the
first count whose ids all lie in 1..25 and levels in 1..3 wins. A region that
cannot be aligned is *not* reported as "no skills". Ids are base 1, measured.
**[V]** for the alignment; **[I]** for the name an id maps to.

Sample `mgo2-game1.pcapng` loadouts — **[V]** for the bytes:

| slot | L | team | gender | gear (head chest hand waist foot acc1 acc2) | skills |
| --- | --- | --- | --- | --- | --- |
| 0 | 50 | 2 | male | `1e 45 2f 57 39 67 68` | none |
| 4 | 54 | 2 | female | `1c c0 32 60 40 71 74` | HAND_MAS/2, FASTMOV/2 |
| 6 | 54 | 2 | male | `88 47 a2 5a 3b 6e 6a` | CQC/3, ASSLT_MAS/1 |
| 11 | 56 | 2 | male | `1f 44 2e 64 3e 6b 66` | SNIPPER_MAS/2, ENEMY/3, ALERT/1 |
| 15 | 54 | 2 | male | `88 47 a2 5a 3b 6e 6a` | ASSLT_MAS/2, FASTMOV/2 |
| 20 | 54 | 2 | male | `1e 45 2f 57 39 67 68` | HAND_MAS/3, SBMC_MAS/1 |

`mgo2-game2.pcapng`: slot 1 (54 B) male, `HAND_MAS/3, SBMC_MAS/1`.

## What this changes

The host's payload `0b 00` and each player's loadout now decode; the rectangle
`UDP_COMMANDS.md` filed as "[U]" is named. **The server writes the rotation and
writes the loadouts.** The room's rotation is this instance's own setting
(`MatchSettings.Rotation`, `GAME_SETTINGS_WIRE.md` — the same rotation the host
plays and the same one it writes into the room row), so the burst's fifteen
triples are written from it — entry *i* is the rotation's entry *i* modulo its
count, so a room naming fewer rounds than the field holds repeats its own rounds
rather than naming map `0` — and the third byte is zero, which is what fifteen of
the sixteen recorded triples carry and what a room that states no flags byte is
published with. The writer is `PlayerSettingsBurstService.RoomRotationBody`.

The loadouts are written from the room's own players. A player's own fields are
rows this server holds — the appearance block from `characters_appearance`
(`CharacterService.GetAppearanceAsync`), the skill pairs from the character's
equipped skills (`GetEquippedSkillsAsync`), the slot byte from the key the
roster handed out, and the side byte from the team rows the lobby stored — so
the burst names the room's own faces, gear and skills instead of the recorded
host's. The readers and writers are `LobbyLoadoutService` and
`LoadoutFramesUtils.TryWrite`, and the body of a shape the player's skills do not
have falls back to the recorded one, logged.

**What is left as recorded, and why.** Three regions of a loadout body are named
by nothing: the nineteen bytes at `0x03`, the four bytes at `0x12`, and the two
bytes between the skill list's end and the appearance block. The last are the
player's and not the appearance's — `game1`'s slot 20 and `game2`'s slot 1 carry
the same appearance, the same pairs and the same four leading bytes and differ in
that tail alone, `05 78` against `05 7b`, and within one capture the tail differs
between players — so they are written from the template, and so is the byte a
no-pair region opens with (`fe` in `game1`'s body, `01` in the briefing capture's).
Checked against all twenty distinct loadout bodies the five captures hold, a
written body differs from the captured one **only** in those unnamed bytes: seven
reproduce byte for byte, twelve differ in the tail and (for the no-pair bodies)
the region byte, and one — the 55-byte record of the capture where the player had
chosen red and whose skill region does not align — is refused by the shape rule
and keeps the recorded body. What this settles for the sender is
which bytes of the burst are the room's and which are one character's; the
tag-plus-slot-plus-zeros body the loadouts replaced was a shape no peer had been
shown to read.

## See also

- `tools/udp_settings_semantics.py` — the decoder
- `tools/pcap_roster_acks.py` — prints it under "settings payloads (0x0b)"
- `docs/analysis/replay-console/` — the vendored `rpdt.js` these offsets come from
- `RPDT_REPLAY_RECORDS.md` — settings records, `TYPE_SETTINGS`
- `UDP_COMMANDS.md` §Loadout, `UDP_P2P_PROTOCOL.md` §6.6 — superseded readings
