# The stage's own files: effects, spawns and the map calibration

**What this file answers.** Where the points in the `0x1001` family come from.
Read from `cipherxof/Haven` — HavenStudio, "MGS4 Stage Editor", supporting MGS4,
MGO2 and Metal Gear Arcade. Paths and field names below are Haven's; the claims
about our own wire are marked, and nothing here re-derives a byte offset.

## 1. What a stage is made of

| file | what Haven reads it as |
| --- | --- |
| GCX | the stage's script (`Editors/GcxEditing/GcxScriptEditor`, `Formats/Gcx/GcxDecompiler`) |
| GEOM | the effect/prop tree and the collision primitives (`Formats/Geo/*`) |
| MDN | the models (`Formats/Mdn/*`) |
| TXN | textures, including the minimap (`online_map.txn`, `Services/MinimapService`) |
| LT3 / LIT | lighting (`Formats/Lit/*`, `Rendering/LightVertexBaker`) |
| DLD / DLZ | texture containers (`Formats/Dld`, `Formats/Dlz`) |

## 2. Every prop is an effect: a name, an index, a position

`Formats/Geo/GeoEffect.cs` is the whole model:

```
Name, Index, X, Y, Z, W, RotationX, RotationY, RotationZ, ChunkOffset, Children
```

`Index` is not a number — `Formats/Geo/GeoEffectLayout.cs` unpacks **three
10-bit payload slots** out of it: position in bits 0–9, rotation in 10–19,
scale in 20–29, each measured in eight-byte units from the effect record's own
start. So an effect states *where its payload sits*, and the same record can
carry a point, an orientation and a size.

**"Is this a placed thing" is that bit field, not a separate table.**
`Services/MinimapSpawns.cs` treats an effect as a spawn when
`GetPositionSlot(effect.Index) != 0`, and drops the three calibration markers
below. The tree's parents are the categories — `SYSTEM`, `RULE`, `ITEM`,
`CBOX`, `CLAYMORE`, … — and children hang under them, which is why the editor
shows a hierarchy rather than a list.

**The names are hashes of `PRP_*` strings.** `PRP_CENTER_SPAWN`,
`PRP_CORNER_SPAWN`, `PRP_CBOX`, `PRP_CLAYMORE_01` — resolved through
`dictionary.txt` (`Utils/DictionaryFile`), with the raw hash printed when the
dictionary has no entry. A stage names its spawns; it does not number them.

## 3. The stage states its own bounds

`PRP_STAGE_SIZE_01` and `PRP_STAGE_SIZE_02` are the two corners of the map and
`PRP_STAGE_CENTER` its middle. `MinimapSpawns.ResolveBounds` takes the pair as
the extent and only falls back to a bounding box of the spawns when they are
absent, with a 5% margin.

## 4. The map's world-to-map projection is per stage

`Services/MinimapService.cs` carries the table the game uses to place a world
point on the minimap texture (scale, additive offset, canvas extent):

| stage | scale x / z | offset x / z | canvas |
| --- | --- | --- | --- |
| `n001a` | 0.080 / 0.080 | 0 / −5000 | 8192² |
| `n002a` | 0.085 / 0.088 | 1500 / 98500 | 9638² |
| `n003a` | 0.090 / 0.090 | −1420 / −12700 | 8192² |
| `n004a` | 0.060 / 0.060 | −17500 / 63000 | 8192² |
| `n005a` | 0.078 / 0.080 | 5500 / 5500 | 8192² |
| `n007a` | 0.118 / 0.118 | 200 / −1600 | 8192² |
| `n008a` | 0.078 / 0.078 | 1800 / −1000 | 8192² |
| `n012a` | 0.068 / 0.068 | 7000 / 88500 | 16384 × 8192 |
| `n014a` | 0.048 / 0.048 | 2000 / −39000 | 8192² |
| `sm_ll`, `n020a`, `sm_dd`, `n022a`, `n024a` | patched, in 0.05 units | per stage | 8192² |

The patched five are the ones whose scale is consumed in 0.05-unit steps before
the game's minimap actor is called; the other globals patch HUD presentation and
do not move a marker.

## 5. What this settles for the `0x1001` records

* **The positions are the stage's, and the host ships a choice, not geometry.**
  Every spawn and site is already an effect in GEOM, so the room does not need
  the point described to it — it needs to be told which of the stage's points it
  is using. That is what kind `0x04` (a team, a direction, a point) and kind
  `0x03` (a point written twice behind a tag) look like from the wire side.
* **The wire's units are tenths of the stage's world units, and the stage's own
  bounds check it. [I]** `MapSpawnUtils` records n001a's spawn at (−3625, 2500)
  in wire units; ×10 is (−36250, 25000), inside Haven's `PRP_STAGE_SIZE` box for
  n001a of (−37000, −117000)–(78000, −9000). The other rows of that table sit in
  the same box as their stage's markers, which is the first independent check of
  `PlayerPosition.CoordinateScale` outside the captures.
* **A stage has no "checkpoint" object.** Haven's format has no checkpoint type;
  what a stage carries is its spawn and site set, and choosing which one a
  player returns to is the supervisor's (`sv_respawn.cc`, `SV_GetInitialPosDir`
  in the port) and the mode's. A "checkpoint" is a rule, not a stage field — so
  the kind-`0x04` reading in `roster-record-kinds.md` §7 is the whole of it.
* **The kind-`0x03` triple is not a GEOM float triple. [V]** GEOM stores
  `X/Y/Z/W` as four-byte floats in world units; the record's triples are signed
  sixteens in tenths, with x and z mirrored between the two writes. That is the
  wire's compact form of a point, not a copy of an effect record.

## 6. What would connect the two

The record's six-byte header carries no `PRP_*` hash — its `+0x02`, `+0x04` and
`+0x07` bytes are small indices — so nothing yet ties a record to a named
effect. Haven's `dictionary.txt` is where those names live, and a pass that
matched each stage's effect list against the points a host writes for it would
settle whether the dismissed positions in the captures are effects at all. That
is **[U]**, and this file is the map of where to look.
