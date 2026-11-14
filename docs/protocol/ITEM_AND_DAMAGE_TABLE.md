# The damage and item table, mapped

The replay console (`.gametools/replay-console/rpdt.js`) carries three tables that name
what a damage record is: `ITEM_NAMES` (every id the protocol's item byte can hold),
`HIT_ZONE` (what a damage record's first byte means) and `STUN_SUBTYPE` (the two zones of a
stun). This page maps them onto this repository's constants, and records which ids the two
captures actually carry, so a reader can tell a table lookup from a measurement.

Everything below the table headings is either **read from the console's array** or
**counted in a capture**; nothing here is inferred.

## Hit zones

| byte | console `HIT_ZONE` | console `STUN_SUBTYPE` | this repository |
|---|---|---|---|
| `0x11` | body | — | `BotActorRecordUtils.DamageSubtype` (body) |
| `0x12` | head | — | `BotActorRecordUtils.HeadshotDamageSubtype` |
| `0x13` | — | — | `BotHitUtils.BlastSubtype` (28-byte body) |
| `0x41` | body | body | `BotHitUtils.BodyZoneSubtype` |
| `0x42` | head | head | `BotHitUtils.StunSubtype` |
| `0x91` | body | — | `BotHitUtils.MeleeSubtype` |

`0x41` is the byte this repository refused until now: `KindOf` recognised five bytes and
`Decode` returned `null` for everything else, so a client writing it had its report dropped
whole — no health taken, no record echoed. The console names it a body hit, in both tables.
Neither capture holds one (measured below), so the gap was latent: it is closed as a
correct reading of the table, not as a fix for an observed capture.

## Weapons, and the byte a record carries

`ITEM_NAMES[i]` is the name of item id `i`. The weapon byte in a damage record
(`body[4]`) is one of those ids. Measured, whole capture, both directions:

| capture | source | subtype | weapon | id | length | count |
|---|---|---|---|---|---|---|
| `actions.pcapng` | client | `0x11` | `0x18` | 24 | 15 | 22 |
| | client | `0x12` | `0x18` | 24 | 15 | 6 |
| | client | `0x13` | `0x34` | 52 | 28 | 2 |
| | client | `0x42` | `0x02` | 2 | 15 | 4 |
| | host | `0x11` | `0x18` | 24 | 15 | 360 |
| | host | `0x11` | `0x19` | 25 | 15 | 55 |
| | host | `0x12` | `0x18` | 24 | 15 | 7 |
| | host | `0x13` | `0x34` | 52 | 28 | 2 |
| | host | `0x42` | `0x02` | 2 | 15 | 4 |
| `myserver.pcapng` | client | `0x11` | `0x17` | 23 | 15 | 10 |
| | client | `0x11` | `0x07` | 7 | 15 | 3 |
| | client | `0x12` | `0x17` | 23 | 15 | 1 |
| | host | `0x11` | `0x18` | 24 | 15 | 57 |
| | host | `0x11` | `0x17` | 23 | 15 | 8 |
| | host | `0x11` | `0x07` | 7 | 15 | 2 |
| | host | `0x12` | `0x18` | 24 | 15 | 2 |
| | host | `0x12` | `0x17` | 23 | 15 | 1 |

Read against `ITEM_NAMES`: 24 = `M4`, 25 = `AK102`, 52 = `GRENADE`, 2 = `ST KNIFE`,
23 = `SKORPION`, 7 = `FIVESEVEN`. Two corrections follow, both to comments that named the
wrong row:

- A stun record's weapon `02` is **`ST KNIFE`**, the knife a hold-up is made with — not the
  Mk.2 tranquilliser an earlier comment claimed (the Mk.2 is `MG MkII`, id 184).
- A knife blow's weapon is **whatever the attacker held** where the client reports it:
  `0x91` arrives with `0x01` (`NONE`, the empty row) *and* with `0x18` (`M4`) in the same
  capture. The id is not the blade.

`docs/WEAPONS.md` carries the ELF's own 141-row table at `0x1035834`. Our
`WeaponIdentifier` and the console's array agree from id 17 (`MP7`) upward — which is why
`0x18` reading as M4 in both is a check on both — and differ in the first sixteen rows:
the console has a `NONE` row at 1 that our enum does not, and our enum has `Pmm` at 5 that
the console does not, so ids 1–10 name different items in each while 11–16 realign. Only
the console's ordering has been measured on the wire (ids above 16); the low ids are not
exercised by either capture, so they are left as the image read them.

## Items, and the ids the captures use

The table runs to `WP_Bul*` at the end; these are the rows that matter for a match and that
this repository names anywhere. A capture shows **none** of them in a damage record — the
only item-shaped records are world objects under `0x1a50`, whose body is
`<index> <…> <item>` (`03 00 00 00 21 00` in both captures).

| id | console name | meaning |
|---|---|---|
| 52 | `GRENADE` | the blast record's weapon, 28-byte body |
| 53–60 | `WP G`, `STUN G`, `CHAFF G`, `SMOKE G`, `SMOKE G.(R/G/Y/P)` | thrown |
| 62 | `MAGAZINE` | the dropped-magazine object |
| 64 / 65 | `CLAYMORE` / `SLEEP CLAYMORE` | placed |
| 66 / 67 | `C4` / `SLEEP C4` | placed; triggers are 120 / 121 |
| 69 / 70 | `BOOK` / `PSEUDO BOOK` | the stealth book and its decoy |
| 73 | `SHIELD` | carried |
| 168–174 | `RATION`, `E. BAR`, `E. DRINK`, `PENTAZEMIN`, `STUPE`, `S Eye`, `MG MkII` | pickups |
| 175–180 | `CAMERA`, `C BOX A`, `iPod`, `RADIO`, `CIGAR`, `MUNA` | `C BOX A` (176) is the cardboard box |
| 181–187 | `BANDANA`, `STEALTH`, `SYRINGE`, `S. PLUG`, `BINO.`, `S.PLUG_SPR`, `FACECAM` | pickups |
| 188 / 189 | `KEROTAN` / `GAKO` | the frogs |
| 190 | `OCTOCAM` | the camera |

These ids are the generated enumeration's own values (`src/Shared/Types/ItemIdentifier.cs`),
not a count by hand: the first version of this table placed them ten higher, by walking the
console array by eye and losing `CBOX TACKLE` and its neighbours on the way.

`ItemIdentifier` now carries all 297 rows of the console's table, so the whole listing above
is a name in code. It is an enum of `ushort` and not `byte` for a measured reason: the array
is 297 rows and a record's item field is one byte, so `0`–`255` are addressable and the
`WP_Bul*` bullet rows at `256`–`296` are not. `0xff` is `WP_BulVss` in that table — the one id
where the console's listing and our `WeaponIdentifier.Unreported` describe the same byte. `WeaponIdentifier` remains the ELF's own 141-row read and stops at `Envg`
— which it numbers 140 and the console's table numbers **130**: the two tables agree on the
firearm and grenade rows a capture has been read on (`0x18` M4, `0x19` AK102, `0x34` GRENADE)
and diverge on the low knife byte `0x02`, which is `ST KNIFE` in the console's table and
`RUGER` in the ELF's. Where they disagree, the console's numbering is the one
its parser has validated against recorded replays, so a new **damage-record** constant should
come from `ItemIdentifier`.

The actor's **weapon pair is not a damage-record weapon byte**. Its second byte names the
carried model and comes from the ELF's weapon table; the two tables agree for firearms and
diverge on the knives, which is why `docs/WEAPONS.md` records both sides. A bot carries a
`WeaponIdentifier` for the actor pair and converts it to an `ItemIdentifier` with
`BotDamageWeaponUtils.DamageIdOf` when it writes a damage record. Putting the item id `89`
(`AK KNIFE` in this table) into the actor pair instead names `LIGHT` in the ELF table and
produces the `dg_model.cpp` null-header assertion.
