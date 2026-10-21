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
* `>= 1` with `L` `24` or `25` — the player's settings header, a slot-state
  transfer of one or two fields (below).
* `>= 1` with `L` in `{50..62}` — a player loadout, whose slot is the byte minus
  one, so slot `0` is the same first joining player the roster counts from zero.
  The host itself is the rotation record, never a loadout (`rpdt.js` requires
  `b[a + 1] >= 1`). The set is the pair count's own arithmetic, not a list of
  shapes: `L = 50 + 2·pairs`, plus one when the region carries the `0x10` suffix,
  so `52` is one pair and `59` is four pairs with the suffix. **[D]** for the
  rule; **[V]** for the slot byte.

A `0b` body of exactly **10** bytes is not this family: it is a state-`0x00`
transfer of one field — `[key 0x0b][state 0x00][7-byte map][value]` — read by
`0x27E8B0`, and all 188 in the corpus fit that parse
(`docs/analysis/join-stall-after-mode2.md` §34).

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

## The settings header — `0b <slot + 1>`, `L` 24 or 25

A third shape shares the discriminator, and it is neither the rotation nor a
loadout: a **state transfer** of the player's own slot state, one field or two.
It is the record a host writes for a player at the join and again when that
player asks to be placed in the room (`SettingsHeaderBodiesUtils`). **[V]**

| offset | size | field |
| --- | --- | --- |
| `0x00` | 1 | `0x0b` |
| `0x01` | 1 | slot + 1 — the same slot byte the loadouts carry |
| `0x02` | 21 | presence map, one bit per slot-state field, read low bit first |
| `0x17` | 1–2 | the value of each field whose bit is set, in field order |

So the body is the map and then one byte per field it names; the length follows
the field count, which is what tells the two forms apart:

| form | `L` | map bits set | values | when |
| --- | ---: | --- | --- | --- |
| join | 25 | field 1 (`0x02` at `0x02`), field 156 (`0x10` at `0x15`) | `0xFE`, `0xFF` | the host's own player, in the post-join burst |
| answer | 24 | field 1 only | `0x00` | the player asking to be placed in the room |

```
join   0b 01 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 fe ff
answer 0b 02 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

The map is **not** a phase. Bit 1 is field 1 — the map reads low bit first, so
`0x02` is field 1 and not field 0 — and bit 156 is `0x10` of the map's
twentieth byte, at body `0x15`. A copy whose map sets other bits is another
family of the same length: a scoreboard row or a slot-state patch is a 25-byte
`0b` body too, and only the map says which. `tools/join_header_check.py`
separates them and checks every join copy against the layout above.

**The join form is in every reference join of a room that has not played** —
`mgo2-game2.pcapng` four times (t+10.236 ×3, t+12.361),
`mgo2-game-started-on-briefing.pcapng` twice (t+8.125, t+10.379) and
`mgo2-game-failed-memoryviolation.pcapng` twice (t+11.085, t+13.206) — always
host-to-joiner, always slot `01`, always the same bytes, and 47 to 49 s before
that client echoes its own lobby slot. Two more in
`mgo2-game-joined-when-red-team-then-briefing.pcapng` (t+150.644, t+153.153) are
the same bytes, later in that session. Ten copies over four captures, byte for
byte identical. `join_header_check.py` checks every copy
against the layout above; a harness calling
`SettingsHeaderBodiesUtils.BuildJoin` for each slot reproduced all ten exactly,
so the server's body and the reference's agree byte for byte. The join captures
whose clients are already in a round
(`mgo2-game1`, `myserver28`) hold none, and neither `myserver24` nor `myserver27`
does. **[V]**

The first field's value is the team array's **"no side"** byte, `0xFE`
(`TeamArrayFramesUtils` writes the same byte for a player on none), and the
second field's is `0xFF`, which reads as the empty value; what either field
*means* is **[U]**. The answer form carries the first field at `0x00`, which is
the same slot-state field written with no value.

**Who writes it.** `PlayerSettingsBurstService` writes the join form for the
host's own player between the rotation and the loadouts, under the same
consecutive ordinals as the rest of the burst; `TeamArrayService.RoomEntry`
writes the answer form when a peer asks to be placed in the room. See
`docs/analysis/join-stall-after-mode2.md` §33 for the measurement that made the
join copy a requirement rather than an answer.

## The loadout record — `0b <slot + 1>`, `L` in 50–62

| offset | size | field |
| --- | --- | --- |
| `0x00` | 1 | `0x0b` |
| `0x01` | 1 | slot + 1 |
| `0x02` | 1 | team byte |
| `0x16` | 1 | the constant `2` — see below |
| `L - 23` | 23 | appearance block |
| `0x17 .. L - 26` | var | skill region: marker, pairs, list end |

**The length is the pair count, and `0x16` is not a count.** The region between
the header and the appearance block is a leading marker, two bytes per pair and
the list end, and the run between the list end and the appearance block is two
bytes, so the list end always sits `26` bytes from the body's end and the body is
`50 + 2·pairs` long. `rpdt.js` reads the byte at `0x16` as a size and computes
the list end from it, which is why it looks like a count; over every body the
captures hold it is `2`, and `2` is what makes the list end land `26` bytes out.
**[D]** — the rule, from the three recorded bodies, whose `0x16`, list end and
pair counts are `2 / 24 / 0`, `2 / 28 / 2` and `2 / 30 / 3` at lengths `50`, `54`
and `56`.

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

**Five shapes the captures hold, and two the captures do not.** The length rule
above gives every pair count a body: no pair (`0b 01`, 50 B), one (52 B), two
(54 B), three (56 B) and four (58 B). The captures hold the zero-, two- and
three-pair bodies, so those are byte for byte what the game wrote
(`LoadoutBodiesUtils`); the one- and four-pair shapes are built from them, by
opening room for the extra pair behind the region's leading marker — every other
byte is the recorded body's, and the opened slots are the pair bytes themselves,
which the burst overwrites with the player's own.

**The one-pair shape is the one that mattered.** `52` was missing from the
decoder's length list, so a player with exactly one equipped skill had no shape
to be written in and was announced with a different player's skill count. The
working-tree `LOADOUT_LEN` adds it, and the length rule says why it is needed at
all.

**Four pairs is open, and it is a real gap.** A character's equipped skills are
saved in four slots, so four is reachable and `58` is its length. No capture and
no replay file holds a body of that length, so the shape is arithmetic and not an
observation; and the decoder's own skill reader searches at most three pairs, so
at four it matches the trailing three, reads them, and **silently drops the first
pair** rather than refusing the record. `LoadoutFramesUtils.MaximumSkills`
therefore stays `3` until a body of that length is seen, and a four-skill player
is announced in his own slot behind the no-pairs body instead. **[D]** for the
rule and the lengths; **[V]** for the captured shapes; **[U]** for the four-pair
body.

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

`skillRecord`: the byte at `0x16` sizes the region, the region ends on `0xff`,
an optional `0x10` suffix is dropped, and the pairs are then read back from the
region's end as **three, two or one** `(skill id, level)` pairs — the first count
whose ids all lie in 1..25 and levels in 1..3 wins. A region that cannot be
aligned is *not* reported as "no skills". Ids are base 1, measured.

**The three-pair cap is the decoder's, not the record's.** It is a search order
over candidate counts, and it stops at three because no replay file held a body
with more. At four pairs the search matches the trailing three and returns them,
so the first pair is dropped rather than the record refused — which is why the
four-pair shape is built but not yet written. **[D]**
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
- `tools/join_header_check.py` — the join-time settings header, byte for byte
- `tools/pcap_roster_acks.py` — prints it under "settings payloads (0x0b)"
- `docs/analysis/replay-console/` — the vendored `rpdt.js` these offsets come from
- `RPDT_REPLAY_RECORDS.md` — settings records, `TYPE_SETTINGS`
- `UDP_COMMANDS.md` §Loadout, `UDP_P2P_PROTOCOL.md` §6.6 — superseded readings
