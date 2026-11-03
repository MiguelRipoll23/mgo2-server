# The records that carry Drebin points

**What this file is.** Two things a reader asking "where are the Drebin points on this
wire" needs and has to look in two places for: the **round option** that turns them on,
which this repository already names, and the **records that carry the figures**, which
it does not. The second half is the measurement; the first is a pointer so that nobody
searches for the option again.

## 1. The option is already measured — `docs/GATES.md` §2

The third byte of a rotation triple — body `0x1a + 3i + 2` of the room-state payload,
and payload offset `0xA3 + 3 × round` of the host's `0x4310` — is a **three-way radio,
not a bitfield**: `0` Normal, `2` Drebin Points enabled, `4` Headshots Only. It is read
at `0x6A9948`, the labels come from the menu table at `0xFE7084/88/8C`
(`obj_4_select_others`, `icon_drebin_point`, `icon_HSonly`), and the round list draws
Drebin rounds pink and Headshots-Only rounds light blue. `docs/OBSERVED.md` carries the
same finding with the tooltips.

**What the corpus shows at that byte** (`docs/analysis/rotation-payload-corpus.md` §2):
`0`, `2`, `32` and `34` — so the wire carries the radio's `2` for a Drebin round, and a
`0x20` bit beside it that is not part of the radio (`32` = `0x20`, `34` = `0x20 | 0x02`).
The `0x20` bit is **[U]**; it is not one of the three radio values, and it is present in
the two rooms whose triples were captured with non-zero flags.

**Where the server writes it:** `RoomStateRegionUtils.WriteRotationTriples` writes each
triple's third byte from the room's own `MatchRotationEntry.Flags`, so a room that turns
Drebin points on states `2` there. Nothing in this change alters that path.

## 2. The figures travel in their own `0x1001` records

**How it was measured.** Every gameplay flow of `.pcapngs/**` was decoded with its two
handshake bases, and every `0x1001` message with a body of 40 to 70 bytes was tabulated
with its ordinal, its length and its body. The family is a **52-byte** `07 2d` pair and
a run of **54-byte** `07 2f` records, written once per player index.

| capture | flow | t | ordinals | records |
| --- | --- | ---: | --- | --- |
| `working-server-4-sessions` | `143.47.227.126:5725` | t+8.44 | 36–42 | two `07 2d`, five `07 2f` for indexes 0–4 |
| `working-server-4-sessions` | `64.181.216.250:5722` | t+8.55 | 10–14 | two `07 2d`, three `07 2f` for indexes 0–2 |
| `working-server-log` | `64.181.216.250:5720` | t+9.67 | 12–14 | the same three `07 2f` bodies, byte for byte |
| `mgo2-game1` | `99.66.131.177:5731` | t+466, t+565 | 89/90, 127/128 | the `07 2d` pair only |

The same room's index-2 body appearing byte for byte in two captures is what makes its
values a player's fields rather than a per-capture counter.

### The `07 2f` body as measured

Body of `143.47.227.126:5725` ordinal 40 (index `02`), 54 bytes:

```
07 2f 00 01 02 00 00 02 26 00 d8 e8 03 00 12 fd a3 0f 00 00 00 04
00 00 00 12 fd 03 00 00 00 00 00 12 fd 43 1f 00 d0 07 00 12 fd 03
00 00 d0 07 00 12 fd 43 1f 00
```

| offset | width | measured | reading |
| ---: | --- | --- | --- |
| `0x00` | 1 | `07` | the roster record's version byte |
| `0x01` | 1 | `2f` | the record's own length less seven: `0x2f` = 47, and 47 + 7 = 54 |
| `0x02` | 1 | `00` | constant in every body of the family |
| `0x03` | 1 | `01` | constant in every `07 2f` body |
| `0x04` | 1 | `00`…`04` | **the player index** — one record per index, written in order |
| `0x05`, `0x07` | 1 each | `00`, `01` | two one-byte flags that move together; **[U]** |
| `0x08` | 1 | `26` | constant in all five bodies |
| `0x09` | 1 | `00`…`03` | **[U]**, moves per player |
| `0x0a`–`0x35` | 4 | runs of `12 fd`, `00 00 00`, `43 1f` | four repeated groups, **[U]** |

### Where the figures sit

| record | index | values found |
| --- | ---: | --- |
| ord 39 | `01` | `5000` at `0x16` and `0x1e`; `7000` at `0x25` and `0x2d` |
| ord 40 | `02` | **`1000` at `0x0b`**; `2000` at `0x26` and `0x2e`; `4000` at `0x24` |
| ord 41 | `03` | `22500` at `0x0b`; further values per slot |
| ord 42 | `04` | `0x0ad4`, `0x1f0c`, values per slot |

That is the change the 3 November capture shows: `1000` in one player's record and
`5000` in another's, at `0x0b` and `0x16`/`0x1e`. A four-digit value, one per player,
moving during a session, is the shape of the Drebin figures; which of the repeated
groups is the player's own balance and which are the round's or the team's is **[U]**.

## 3. What is not written, and why

The burst writers do not build this family. The measured offsets stop at the record
header and the index; the repeated four-byte groups have not been separated into
fields, and a builder that guessed them would put numbers on a wire that no capture
states. What a builder needs first is a capture where one player's Drebin total is
known at two moments — the figure the client draws on its own screen — so that one
offset can be tied to one number.

## 4. The room-state body's pair is not this

The two words at `0x5a` and `0x5e` of the 161-byte body are not Drebin points: no
captured body carries `1000` or `5000`, and the figures live in the family above
(`docs/analysis/rotation-payload-corpus.md` §7).
