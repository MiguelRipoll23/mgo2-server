# The weapon master table (ELF-extracted 2026-07-23)

The game's static weapon table lives in `MGO2.elf`. The image is not vendored here,
but the table is reproduced below and mirrored by
[`WeaponIdentifier`](../src/Shared/Types/WeaponIdentifier.cs), whose member values
are the ids in the first column.

## Where it lives

| property | value |
|---|---|
| virtual address | `0x1035834` |
| file offset | `0x1025834` |
| stride | `0x2C` — 11 big-endian `u32` columns |
| records | **141**, ids `0`–`140`, zero-based: the array position **is** the weapon id |

A separate ammo-name table (`WP_Bul*`, 67 entries, stride 8) lives at vaddr
`0x1037d80`.

### Row layout

| word | meaning |
|---|---|
| `w0` | pointer to the row's NUL-terminated display name |
| `w1` | category bitmask in the low `u16` |
| `w2` | flags and id; **bit `0x8000` = non-lethal** |
| `w3`–`w8` | numeric parameters, unlabelled |
| `w9` | model/hash |
| `w10` | approximately zero |

The name is a pointer into the image's string data, not an inline string. The
category bits are:

| bit | class | bit | class |
|---|---|---|---|
| `0x0001` | knife | `0x0100` | launcher |
| `0x0002` | pistol | `0x0200` | grenade |
| `0x0004` | SMG | `0x0400` | placeable |
| `0x0008` | assault rifle | `0x0800` | attachment |
| `0x0010` | LMG | `0x1000` | shield |
| `0x0020` | shotgun | | |
| `0x0040` | sniper rifle | | |
| `0x0080` | grenade launcher | | |

## What anchors the ids

The `0x43a2` round-end tally list is indexed by weapon id. Its live anchors are
`1 = ST KNIFE` for a knife kill and `43 = MOSIN N` for a tranquilliser faint; the
non-lethal flag on row 43 agrees. Ids `0`–`126` are the tally-eligible damage
sources, while `127`–`140` are boss and environment sources.

**Name-column caveat (2026-07-24).** The ids and behaviours are the confirmed
layer; the names sit one tier below. UI correspondence has been verified for
AK102 (25) and RUGER (2) — the latter was initially suspected of diverging
because players call it "the Mk.2", which is MGS4 habit for the same gun. The
duplicate SBS (36/74), TANE (29) and the repeated `SURPRESSOR` rows suggest some
rows are internal-only, so an unverified name should be read as [INFERRED] until
seen on the live UI.

The tally triple is per weapon `{kills, headshots, faints}`. The headshot field is
a terminal-event qualifier, not a hit counter: headshots register only on the
kill or faint they cause. A weapon with no terminal event gets no row, and CQC
knockouts never enter this list at all — melee stuns live only in the `0x4390`
stun pair. Confirmed anchors: `1 = ST KNIFE`, `2 = RUGER` (the Mk.2
tranquilliser, counting faints caused), `25 = AK102`, `43 = MOSIN N`.

## The full table

| id | name | id | name | id | name |
|---|---|---|---|---|---|
| 0 | NONE | 47 | XM25 | 94 | REX LASER |
| 1 | ST KNIFE | 48 | STINGER | 95 | Nighthawk |
| 2 | RUGER | 49 | JAVELIN | 96 | AK KNIFE |
| 3 | OPERATOR | 50 | RPG7 | 97 | Throw Knife |
| 4 | SOCOM | 51 | M72A3 | 98 | Throw Machete |
| 5 | PMM | 52 | GRENADE | 99 | WP_ParaCord |
| 6 | FIVESEVEN | 53 | WP G | 100 | XM8 Short |
| 7 | SIG GSR | 54 | STUN G | 101 | XM8 Long |
| 8 | D EAGLE | 55 | CHAFF G | 102 | XM8 Jona |
| 9 | D EAGLE L | 56 | SMOKE G | 103 | MORTARBOMB |
| 10 | THOR | 57 | SMOKE G.(R) | 104 | M2 |
| 11 | M1911A1 | 58 | SMOKE G.(G) | 105 | CORE |
| 12 | RACE GUN | 59 | SMOKE G.(Y) | 106 | CLAYMORE |
| 13 | GUN DEL SOL | 60 | SMOKE G.(P) | 107 | KICK |
| 14 | PSS | 61 | PETRO BOMB | 108 | PUNCH |
| 15 | GLOCK | 62 | MAGAZINE | 109 | PUNCH |
| 16 | T17 | 63 | EMP GRENADE | 110 | STOMP |
| 17 | MP7 | 64 | CLAYMORE | 111 | ROLLING |
| 18 | MP5 | 65 | SLEEP CLAYMORE | 112 | CQC |
| 19 | INGRAM | 66 | C4 | 113 | ROLLING DRUM |
| 20 | P90 | 67 | SLEEP C4 | 114 | MK2 SPARK |
| 21 | BIZON | 68 | BASE BUSTER | 115 | DOOR ATTACK |
| 22 | PATRIOT | 69 | BOOK | 116 | DRUMCAN |
| 23 | SKORPION | 70 | AFFECT BOOK | 117 | GROUND FIRE |
| 24 | M4 | 71 | DOLL | 118 | GROUND FIRE |
| 25 | AK102 | 72 | DOLL | 119 | ANIMAL STRONG |
| 26 | G3A3 | 73 | SHIELD | 120 | C4 TRIGGER |
| 27 | AN94 | 74 | SBS | 121 | SLEEP C4 TRIGGER |
| 28 | FAL | 75 | XM320 | 122 | INJECTION |
| 29 | TANE | 76 | GP30 | 123 | CRITICAL KNIFE |
| 30 | MK17 | 77 | SURPRESSOR | 124 | EXPLOSION SMALL |
| 31 | XM8 | 78 | SURPRESSOR | 125 | EXPLOSION MIDDLE |
| 32 | LMG | 79 | SURPRESSOR | 126 | EXPLOSION LARGE |
| 33 | HK21E | 80 | SURPRESSOR | 127 | TORNADO |
| 34 | PK | 81 | SURPRESSOR | 128 | GEKKOVULCAN |
| 35 | M60E4 | 82 | SURPRESSOR | 129 | GEKKOMISSILE |
| 36 | SBS | 83 | SURPRESSOR | 130 | GK KICK |
| 37 | M870 | 84 | SCOPE | 131 | GK STOMP |
| 38 | SAIGA | 85 | DOTSIGHT | 132 | REX KICK |
| 39 | VSS | 86 | MP7 DOTSIGHT | 133 | REX BODY |
| 40 | M82A2 | 87 | LIGHT | 134 | RAY VULCAN |
| 41 | DSR1 | 88 | LASERSIGHT | 135 | RAY MISSILE |
| 42 | M14EBR | 89 | LIGHT | 136 | RAY CUTTER |
| 43 | MOSIN N | 90 | GRIP A | 137 | RAY KICK |
| 44 | DRAGNOV | 91 | GRIP B | 138 | RAY BODY |
| 45 | RAILGUN | 92 | REX VULCAN | 139 | BINOCULAR |
| 46 | MGL-140 | 93 | REX MISSILE | 140 | ENVG |

## Using the table in code

`WeaponIdentifier` mirrors the rows one for one. A row's position is the enum's
value, so the enum is `byte`-backed and stops at `Envg = 140`. `Unreported = 0xFF`
is the one member that is not a row: it is the value a weapon-select family
carries for a player who reported nothing. Repeated table strings keep every
member distinct by suffixing the later rows; the table's own spelling remains in
a comment.

**Use `WeaponIdentifier` for every byte that names a carried weapon** — the
actor's pair and the weapon byte a damage record carries. The client resolves
both against this table. It does not resolve either against the replay console's
item table, and at the low ids the two tables are not interchangeable:

| carried weapon | ELF weapon id (pair and damage record) | console item table's row of that name |
|---|---:|---:|
| ST KNIFE | 1 | 2 |
| RUGER | 2 | 3 |
| AK KNIFE | 96 | 89 |
| M4 | 24 | 24 |
| AK102 | 25 | 25 |

The console's item table has an extra `NONE` row at 1, which shifts the first
sixteen rows; from id 17 (`MP7`) upward the two agree. `ItemIdentifier` remains
the table for **world-object** items — what a box or a dropped object holds — and
for nothing a bot writes.

This distinction is not cosmetic, and it has cost two crashes. Announcing a bot
with item id `89` made the client look for the weapon model named `LIGHT`, an
attachment row, instead of `AK KNIFE` at `96`; the model header was null and
`dg_model.cpp` asserted. Converting the bot's *damage*-record byte the same way
repeated the lookup — `BotDamageWeaponUtils.DamageIdOf` turned the carried `96`
into `89` — so the crash returned the moment an infiltrator bot landed a hit.
The captures read one table for both fields: a knife blow's record carries
`0x01` in the same capture where the client's own actor pair reads
`00 01 00 00 00`, and `0x01` is `ST KNIFE` here and `NONE` in the item table. A
carried weapon is named by this table wherever it is written, and the two-table
bridge is gone.

See [`protocol/ITEM_AND_DAMAGE_TABLE.md`](protocol/ITEM_AND_DAMAGE_TABLE.md) for
the measured damage-record ids, and [`BOTS.md`](BOTS.md) for how the gameplay
server chooses a bot's carried weapon.

## Re-reading the image

Read 141 records of 11 big-endian `u32` words at the address above, then follow
each row's `w0` pointer to its display string. The read is anchored by
`0 NONE`, `24 M4`, `96 AK KNIFE` and `140 ENVG`, and by the tally anchors
`1 ST KNIFE` and `43 MOSIN N`. The table is static; a different image should be
checked against those rows before its ids are trusted. Captures validate the
rows they exercise, but they do not define the table — `WeaponIdentifier` comes
from the image, not from a replay parser.
