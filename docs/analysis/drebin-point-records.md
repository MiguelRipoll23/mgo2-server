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

## 2. The figures travel in their own `0x1001` records — **superseded 2026-11-08**

**This section's reading is wrong, and the measurement below is what corrects it.**
The `07 2f` family is the client's **stage** kind `0x01` record, not a player's
figures: `docs/analysis/roster-record-kinds.md` §4 has the dispatch (kind `0x01`
→ node `1`, slot `1`), `docs/protocol/UDP_COMMANDS.md` §"Kinds `0x01`–`0x04`"
has the frame, and the family's bodies repeat byte for byte across unrelated
sessions on one stage — which a per-player figure cannot do. The four-digit runs
read as balances here are the geometry of that record: `12 fd a3 0f` is `4003`
read as a signed sixteen, and the identical run appears inside kind `0x02`'s
corners in the same capture.

**The three measurements that settle it.** Decoded with `tools/udp_frame.py`:

* `pcapng1` — the capture whose weapon-select screen reads **`DP: 1000/8500`** —
  carries **no `07 2f` record at all**; the same is true of
  `game-with-drebin-points-disabled`.
* `pcapng2` — whose screen reads **`DP: 1000/20750`** — carries no `20750` in any
  `0x1001` record of the session (a scan of every record body for the value, and
  for its `u32` form).
* `working-server-4-sessions`, the one capture that does write `07 2f`, writes it
  for a room whose stage is not either of those rooms'.

So a player's Drebin balance is **not** carried in the `0x1001` family, and the
paragraphs below are kept only for provenance. §5 is the part of this file that is
measured: the **toggle**, which is on the wire and is now confirmed.

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
captured body carries `1000` or `5000`, and nothing in the family above does either
(§2).

## 5. The toggle, measured from an enabled and a disabled capture

**The round's option is the third byte of every rotation triple** — body
`0x1a + 3 × slot + 2` of the 161-byte room-state `0b 00` body, and the same byte in
the room's `0x4310` block and `0x4313` reply. Read out of the two captures of the
same field:

| capture | screen | configured triples | third byte |
| --- | --- | ---: | --- |
| `pcapng1` | `DP: 1000/8500` | 14 | `0x02` × 12, `0x22` × 2 |
| `game-with-drebin-points-disabled` | Drebin off | 7 | `0x00` × 7 |

The same capture pair also separates the value from the room's other switches:
the enabled room's word at body `0x4f`–`0x52` sets bit `0x02`, which
`RoomCommonSettingsFlag.DrebinPoints` already names, and the disabled room's word
is the capture's own. Two things follow that the server needs:

* **`0x02` is the Drebin mode, not a per-client choice.** Nothing a joiner sends
  moves it; it is the host's statement about the round, which is why a client
  drawing `DP:` is drawing a mode it was told.
* **`0x20` is a second bit and not part of the mode.** Two of `pcapng1`'s triples
  read `0x22` — Drebin with `0x20` beside it — so the byte is a mode plus at
  least one switch nothing has named yet. It stays as the room states it.

An ordinary round is `0x00`, a Headshots-Only round `0x04`, and a value carrying
`0x02` and `0x04` together has never been measured (`docs/GATES.md` §2).

**Where a player's own balance is, is still open.** It is not in the room state,
not in either `0x1001` family (`§2`), and the weapon-select screen's own figures
move without a record in this channel carrying them; what would settle it is a
capture of one player whose balance is known at two moments beside the records
written between them.

## 6. The one value that does match the screen, and why it is not claimed

A scan of the **decrypted** plaintext of `pcapng1` — every record, every type, not
the `0x1001` family alone — finds the weapon-select screen's own figures as a
little-endian `u32` at offset `0x0c` of **sixteen-byte records**: `8500` in the
frame at t+332.764 and `20750` four times from t+346.880 on, which is the number
the two weapon-select screenshots read (`DP: 1000/8500` and `DP: 1000/20750`).
The three bodies, byte for byte:

```
e1 81 03 00 00 00 8f 0c 88 01 f0 d0 | 34 21 00 00   the host, 8500
41 01 04 00 0c 00 63 05 74 02 57 ca | 34 21 00 00   the client, 8500
21 00 01 00 08 00 c7 f4 88 01 39 d1 | 0e 51 00 00   the host, 20750
```

**It is a lead and not a reading, for three reasons.** The same `8500` appears in
both directions inside one session, which a player's balance should not do unless
the peer echoes the host's figure — and nothing here says which of the two writes
it first. The six bytes in front of it (`88 01 f0 d0`, `57 ca 34 21`) move with
neither the value nor the player, so the record's own header has not been
separated from its payload. And the value was found by searching for the screen's
number, which cannot tell a field that *is* the balance from a field that happens
to equal one of the screen's two readings once in a whole session.

**What would settle it** is the same thing §5's toggle needed and got: the same
record read at two moments where the balance is known to have moved, with the
record's neighbours named. The screen's `8500` → `20750` is that movement, so what
is missing is not a capture but the record's meaning — the twelve bytes ahead of
the value in the client's own reader.

### The candidate is refuted — measured 2026-11-08

The search that produced it was circular — it looked for the screen's numbers — and
the field it found is not a balance at all. The records are the **position** family
(`0x0081`, `0x0881`, `0x00b3`, `0x08b3`, `0x006d`, `0x00db`, attribute class
`0x02`), and offset `0x0c` is the word `PlayerPositionRecordUtility` already places
as the second facing. Read over every sixteen-byte position body of `pcapng1`
(9 056 of them) that word takes **7 330 distinct values between 11 and 65 534**, and
the commonest are `32770`, `32766`, `57052` and `49153` — the sixteen-bit turn's
half and quarter points, not four-digit balances. A field that spans the whole turn
is an angle; a Drebin balance in this room's range `0`…`25 250` would take a few
hundred values and never `57 052`.

The four values a search for the screens' figures had matched — `4500`, `6000`,
`7000`, `8500`, `10250`, `20750`, `22500` — are therefore angles that happen to
coincide with them, which 9 000 records of a 65 536-value field make certain rather
than surprising. **Nothing is renamed from this**, and the player's balance stays
where §5 left it: not in the room state, not in either `0x1001` family, and not in
the position records.

**The screens it was validated against**, for the next pass: `pcapng1` reads
`DP: 0` at 15:55:02, `DP: 4500` at 15:58:44 and `DP: 1000/8500` at 16:01:22;
`pcapng2` reads `DP: 1000/7000` at 16:06:08, `DP: 6000` at 16:06:17, `DP: 1000/10250`
at 16:08:54, `DP: 1000/14000` at 16:10:50, `DP: 1000/20750` at 16:13:03,
`DP: 3500/22500` at 16:14:21 and `DP: 20000/25250` at 16:15:25. A real balance field
must walk that progression for the local player, and the rounds' result screens carry
no `DP:` figure at all.
