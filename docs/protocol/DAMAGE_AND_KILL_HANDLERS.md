# Damage and kill: shapes, fields and handlers

Two things travel for a death: the **damage record**, one per blow, under the victim's
own identifier; and the **kill feed**, three bytes written once the victim is down. The
client reads the first through its player table by the slot the identifier names, and the
second through the room's slot-to-player map. Everything below is measured from
`.pcapngs/actions/actions.pcapng` (the working host) and `.pcapngs/myserver.pcapng`
(this host), and read out of `system.dll` in Ghidra.

Related: `RPDT_REPLAY_RECORDS.md` (the console's own reading of the operations stream),
`UDP_GAME_CAPTURE.md` (frame framing), `P2P_CONNECT_FSM.md` (the member list).

---

## 1. Identifiers: which slot a record belongs to

A slot owns a block of identifiers, ten apart (`0x0a`). The block's base is the slot's
**vitals** identifier, `0x0844 + slot * 0x0a` (`PlayerVitalsFramesUtils.SlotZeroIdentifier`).

| record | identifier | written by |
|---|---|---|
| state | vitals − 1 | `BotActorRecordUtils.StateIdentifierOf` |
| position | state + 2 | `BotActorRecordUtils.Build` |
| weapon pair, weapon byte | state + 4 | `BotActorRecordUtils.Build` |
| aim | state + 6 | `BotActorRecordUtils.Build` |
| **damage** | **state − 3** | `BotActorRecordUtils.DamageIdentifierOf` |
| vitals | state + 1 | `PlayerVitalsFramesUtils.IdentifierFor` (constant `0x090c` is only the recorded room's slot-20 value) |

Two forms of every identifier are on the wire: the base and the base XOR `0x800`
(`MatchRecordGenerationUtils.SiblingBit`). A decoder must accept both.

**`state − 3` is the damage record, and `state + 7` is the next slot's — the same number.**
Blocks are ten apart, so `state(n) − 3 == state(n + 1) + 7`, and the arithmetic alone
cannot say which slot a record belongs to. The client's own state body does: on
`myserver.pcapng` (2026-11-13 13:34) it aims at slot 4 — its state's target byte reads
`4` — while writing eight hits under `0x0868`, which is `state(4) 0x086b − 3`. Reading
that record as `state + 7` filed the player's fire on slot 3, one slot below the bot he
was shooting: the bot took nothing, the slot on his own side went down, and the feed
read as a teammate kill.

---

## 2. The damage record

Attribute class `0x00`, message length **16** (the length field counts the type byte, so
the body is 15). Measured on every class-`0x00` record the working host's client wrote.

| offset | size | field |
|---|---|---|
| `+0` | 1 | subtype — what the blow is (§3) |
| `+1` | 1 | marker: `01` on every blow but the stun stab, `00` there |
| `+2` | 2 | damage word, `u16` LE: **points in bits 7–15, shooter slot in bits 0–4** — `points = word >> 7`, `shooter = word & 0x1F` (one point is 1/128 health). `0x4710` = 142 points, shooter 16 |
| `+4` | 1 | weapon id (item table, `rpdt.js` `ITEM_NAMES`) |
| `+5` | 2 | impact `x`, `i16` LE, wire tenths |
| `+7` | 2 | impact `y`, `i16` LE |
| `+9` | 2 | impact `z`, `i16` LE |
| `+11` | 2 | shot pitch, `i16` LE |
| `+13` | 2 | bearing the shot came from, `u16` LE |

`BotActorRecordUtils.BuildShot` writes this layout. A **blast** is the same record 13 bytes
longer: body **28**, weapon `0x34` (52, the grenade), and the explosion's own point as three
`f32` at `+14`. Its damage word names the thrower or the player who placed the device in
bits 0–4 just as a bullet's does; the reporting peer is only the fallback when those bits
are zero (`DamageRecordUtils.BlastBodyLength`, `BlastPointOffset`).

A victim at zero takes no more hits; the only record still answered on a corpse is the
blast, because it carries the victim's animation rather than a relay of damage
(`docs/BOTS.md`).

---

## 3. Subtypes

| byte | blow | health | marker | weapon seen |
|---|---|---|---|---|
| `11` | body hit | the points in the word (33–69 in the capture; five or six put a body down) | `01` | `0x18` |
| `12` | **headshot** | the whole bar (`10 7d` = 250.1) | `01` | `0x18` |
| `42` | **stun** (Mk.2 dart) | **none** — the stun bar, not health | `01` | `02` |
| `91` | knife | whole bar when the marker is `01` (**critical stab**) | `01` | `01` |
| `91` | knife | **none** when the marker is `00` (**stun stab**) | `00` | `01` |
| `13` | blast | the word the blast took (187.6 measured) | `00` | `34` |

Readings that matter to a host:

- **`12` is a headshot, not "the blow that kills."** Twelve of twelve credited headshots
  end on `12`; none of the thirty-two other kills does — they end on an ordinary `11`.
- **The killing word is a whole bar.** The working host writes `7d 02` on a victim whose
  vitals read `68` a beat earlier, so the client's bar reaches zero whatever it held. A
  host that writes the tracked remainder leaves the client alive and then contradicts it
  with the zero the vitals record carries.
- **A stun takes stamina, not health.** Its points are the part of the stun bar the blow
  spends. Read as health damage, the `42` word kills the actor the client was told was
  knocked out, and announces a kill for a stun.
- **The knife's marker byte decides.** `91 00` is a knockdown: two of them in the working
  capture, neither in any feed, neither victim's vitals at zero.

`BotHitUtils.Decode` is the decoder; `KindOf` maps the subtype, and the decode refuses any
length that is not 15 or 28, so the operations and objective streams (same class, lengths
12–30) are never read as damage.

---

## 4. The kill feed

Three bytes, the replay parser's opcode and the two slots:

```
09 <killer slot> <victim slot>
```

Written under the **operations** identifier `0x1a54` (and its `0x800` sibling), numbered on
that stream — the same identifier pair as the round clock
(`MatchKillFeedBodiesUtils.Opcode`).

- The killer is a **room slot**, not a character id. The console resolves it through the
  roster's slot-to-player map (`rpdt.js` `slotPlayer`/`slot2idx`); a byte no entry names
  resolves to index `0`, which is why a feed for an unknown slot is drawn on the host's row.
- The reference's feed names killers `1`–`16` and never slot `0`.
- The four-byte `[kind, slot, macro, 0x02]` body under `0x1a58` is a **chat macro**, not a
  feed; the client answers it with the voice line.
- **The body carries no icon and no weapon, and does not need to.** The in-game feed
  shows *killer name · icon · victim name · [ weapon ]*, and only the two names come from
  this record. The icon is the hit zone of the blow that killed, and the weapon is that
  same record's `+4` byte, read from the **lethal damage record** on the victim:
  `0x11` and `0x91` draw the body icon, `0x12` the head, and the console's own table says
  the same (`rpdt.js`: `HIT_ZONE = { 0x11: 'body', 0x12: 'head', 0x91: 'body' }`, with the
  stun family `0x41`/`0x42` under `STUN_SUBTYPE`). Its op-0x09 parser reads exactly this
  record — `if (r[2] < 3) return; ks = b[r[1] + 1]; vs = b[r[1] + 2];` — and takes the
  zone and `weaponName` from the joined damage event, not from the feed.

  So a feed with a missing or wrong icon means the **lethal damage record** was missing,
  carried the wrong subtype, or named a weapon the item table does not have — never that
  the three feed bytes were wrong. A stun's icon comes from the `42` record, which is why
  a stun read as a kill draws a death instead of the stun icon.

---

## 5. What the client does with them (`system.dll`)

Base `0x10000000`. All addresses are IDA addresses.

### The player table

| item | value |
|---|---|
| table | `DAT_314def20`, **24 entries** (`PL_MAX_PLAYERS = 0x18`) of `0x1c` bytes |
| written at | `0x10064688`; read at `0x100646bb`, `0x1006470c` |
| accessor | `FUN_10064660(idx)` — asserts `-1 <= idx < 0x18` from `.\player_cpp\pl_global.cc`, line `0x47` |
| entry `+0x00`, `+0x04` | the 64-bit **handle pair** the slot holds |
| entry `+0x60` | the resolved player object |

### The resolvers

```c
/* system.dll FUN_1032fec0(slot) -> player object, or 0 */
if (slot >= 0) {
    entry = FUN_10064660(slot);
    if (entry[0] != 0 || entry[4] != 0)
        return FUN_1010a2e0(entry[0], entry[4]);
}
return 0;

/* system.dll FUN_1010a2e0(lo, hi) -> object, or 0 */
i = lo * 0x10;
if ((lo == 0 && hi == 0) || [i + 0x20] != lo || [i + 0x24] != hi)
    return 0;
return [i + 0x60];
```

A slot entry that exists but holds a handle **the client never registered** resolves to
null. That is the failure mode a dedicated host must never create: the host seat is not a
player of the gameplay session, so a slot the roster names as a character but the session
never spawns is a null wait.

### The kill/damage display path

`FUN_1032fec0` has five call sites — the functions that resolve a slot to a player:

| caller | role |
|---|---|
| `0x10224d76`, inside `FUN_10224c20` | per-player HUD/objective update |
| `0x10227bf1` | HUD update |
| `0x102292e0` | HUD/score panel |
| `0x102b79b5`, `0x102b7c3a` | the two state/score readers |
| `FUN_10227620` (1192 lines) | the large slot-driven score panel |

### The kill-feed handler

`FUN_102b7810(this, byte *body)` is the `09 <killer> <victim>` handler, and it is the one
that decides whose row a kill is drawn on. It takes the body bytes directly:

```c
killer = FUN_1010a2e0(FUN_10064660(body[0]));   /* slot -> player object */
victim = FUN_1010a2e0(FUN_10064660(body[1]));
if (killer && victim) {
    if (killer->[0x88] == 1) this->0x8a8 += 1;  /* the killer's streak */
    if (victim->[0x88] == 1) this->0x8a8 = 0;   /* a death ends it */
}
...
if (killer->[0x88] == 1 && victim->[0x88] != 1) {
    record   = FUN_1004f140(body[1]);                 /* the VICTIM's record */
    attacker = FUN_1032fec0(*(int *)(victim + 0x84)); /* slot the victim holds */
    FUN_102984e0(0, 1, this->0x8a8, record + 0x24, ..., *(char *)(attacker + 0x974) == '\0');
}
```

**The row is the victim's and the attacker is `victim->0x84`, not the feed's killer byte.**
The `09` body is used for the streak and for a secondary call; the log entry itself is
built from the victim's record (`+0x24` is its text) and from a slot the *victim's player
object already holds*. So a client whose victim carries no attacker at `+0x84` — or one
that points at the host's seat — draws the kill on the host's row whatever the feed says,
and the `+0x974` read is the same null dereference as `FUN_10224c20`'s.

That is the shape to test against a blamed kill: the feed byte alone cannot move the row.

### The log entry the feed builds

Both feed handlers end by building a **`0x138`-byte kill-log entry** through
`FUN_102b71e0` (from `0x150005`) and `FUN_102b72a0` (from `0x150011`). Between them they
are the only two places that entry is filled:

```c
if ((FUN_100a19a0() & 0x8000) == 0) {
    killer = FUN_1004f140(body[0]);   /* slot -> roster record */
    victim = FUN_1004f140(body[1]);
    if (killer && victim && (entry = FUN_102b6cb0(this, id)) != 0) {
        memset(entry, 0, 0x138);
        strncpy(entry + 0x00, killer + 0x24, 0x10);   /* the killer's name */
        strncpy(entry + 0x11, victim + 0x24, 0x10);   /* the victim's name */
        entry[0x22] = killer[1];                      /* the killer's SLOT */
        entry[0x23] = victim[1];                      /* the victim's SLOT */
        *(u32 *)(entry + 0x28) = id;
        entry[0x2e] = 0x03;                           /* 0x41 in the second handler */
        entry[0x30] = body[2];
        this->[0x88c] |= 2;
    }
}
```

### Where the two names come from: `FUN_1004f140`

The resolver both feed builders call is the whole of the killer-name logic:

```c
char *FUN_1004f140(char slot)
{
  int i = 0;
  char *record = (char *)(DAT_113000d0 + 0xd4);   /* the roster table */
  do {
    if (*record == slot) {                         /* the slot key is +0x00 */
      if (*(short *)(record + 2) != 0) return record;      /* a live record */
      if (*(char *)(DAT_113000d0 + 200) == slot && slot != -1)
        return record;                             /* the local player's own slot */
    }
    i++;
    record += 0xa8;                                /* 24 records of 168 bytes */
    if (i > 0x17) return 0;
  } while (1);
}
```

| field | meaning |
|---|---|
| `DAT_113000d0 + 0xd4` | the roster table, **24 records of `0xa8` bytes** |
| record `+0x00` | the slot key the feed's byte is matched against |
| record `+0x02` | the word that makes a record live; **zero means "keep looking"** |
| `DAT_113000d0 + 0xC8` | the local player's own slot, which resolves even with a zero word |
| record `+0x24` | the character name the feed copies (16 bytes) |
| record `+0x01` | the slot the entry stores back at `+0x22` / `+0x23` |

So the killer's name is **the roster record whose `+0` byte equals the feed's killer
byte**, and a record whose `+0x02` word is zero is invisible unless it is the local
player's own slot. A missing name, or a name on the wrong player, is therefore a roster
question — the wrong slot written at `+0`, or a zero at `+2` — and never a matter of the
three feed bytes.

So the **row's two names and two slots come from the feed's own bytes**, resolved by
`FUN_1004f140` — and a feed naming a slot that function cannot resolve writes **no entry
at all** (`killer && victim` is required). The bytes at `entry + 0x22` / `+0x23` are the
slots the log keeps, so a client that resolves a killer for display does it from the feed,
not from `victim->0x84`; the `+0x84` read stays with the streak counter and the
"attacker is the local player" flag (`FUN_102b7480` additionally requires
`killer[1] == FUN_102b3730()`, the local player's slot, which is the "you killed" form).

### The dispatcher that owns both handlers

`FUN_102b7810` has exactly one caller, `FUN_10151e60(this, code, body, length)` — the game
event dispatcher, keyed on a 32-bit code:

| code | handler | what it is |
|---|---|---|
| `0x150005` | `FUN_102b7810` | the kill feed `09 <killer> <victim>` |
| `0x150011` | `FUN_102b7a90` | the same body shape and nearly the same code path — the round's second feed writer |
| `0x150006`, `0x150007` | `FUN_102b7e10` | forwards two words to the state object |
| `0x150010` | inline | sets a per-slot timer of `3000` at `this + 0x118 + slot * 4`, clears the slot's bit in `DAT_31864bf8 + 0x204` / `+ 0x20c`, and copies the slot's 16-byte name |
| `0x150017` | inline | queues `0x25 <slot>` on a local event object |
| `0x150019` | inline | writes the round phase at `this + 0x200` (`1`/`2` from the body) |
| `0x150023`, `0x150024` | inline | sets and clears the slot's bit in `DAT_31864bf8 + 0x20c` |
| `0x150028` | `FUN_102baca0` | a sound/event id (`0xec`/`0xed`/`0xee`) |

**`+0x84` is read here and not written here.** Both feed handlers take it as a slot
(`FUN_1032fec0(victim->0x84)`), and the tail of each uses it as an index into a counter
table through `FUN_10034b10` / `FUN_10034b40` (a read-modify-write of one entry). Nothing
in this dispatcher stores it; the writer is elsewhere and is not yet located — the two
functions that looked like candidates, `FUN_102baca0` and `FUN_102b93f0`, are a sound
event and a bitmask clear.

`FUN_10224c20` is where the fault of 2026-11-12 landed:

```c
iVar5 = FUN_1032fec0(uVar7);                  /* slot at *(param_1 + 0x270) */
FUN_103323a0(uVar7, ..., *(char *)(iVar5 + 0x974) == '\0');
```

The message code it tests lives at `entry + 0x90` and the value it acts on is `0xcb`. With
`iVar5 == 0` the read is `0x00000974` — **`system.dll+0x224D7B`, `read=0x974`**: a slot with
an unresolvable handle, plus the field offset. It is not a table overrun; the accessor's
assert fires first on an index outside `0…23`.

### Fields that hold a session slot

| field | meaning |
|---|---|
| `DAT_314def20 + idx * 0x1c` | the slot entry itself, 24 of them |
| entry `+0x00` / `+0x04` | handle pair, the slot's identity in the object table |
| entry `+0x60` | resolved object |
| player object `+0x84` | **attacker slot the victim holds** — what the kill log resolves to draw the row |
| player object `+0x88` | kill-eligibility/state word, tested `== 1` before a streak moves |
| `this + 0x8a8` | the local player's kill streak, incremented on a kill and zeroed on a death |
| victim record `+0x24` | the text the kill-log row is built from |
| `param_1 + 0x270` | the slot `FUN_10224c20` resolves for the current update |
| player object `+0x974` | byte read after resolution — the crash field |
| `FUN_103323a0(slot, …)` | the display call that takes the same slot |

---

## 6. Rules a host derives from the two

1. **Damage goes on `vitals − 1 + 7`**, in either sibling form, and never three below.
2. **The victim's last state is `00 ff 00`** — it does not name its killer. 108 of 113
   working deaths stand there; the five that carry a target were alive and shooting
   somebody else.
3. **The killer keeps naming the victim until the feed has been written.** The feed follows
   the lethal blow by about 0.2 s (`FeedDelayMilliseconds`), and a killer that re-targets
   inside that window leaves the client no record naming both actors.
4. **The feed is written before the victim's block in the beat**, which is the reference's
   order: killer's records, `09 <killer> <victim>`, then the victim's `00 ff 00`.
5. **Never name a slot the room does not seat.** Slot `0` is the host's seat and is not a
   player: the reference's host entry states no character at all (57 bytes, no name, where
   this host wrote 68 with `server-9999`), and the member list names slots `1`–`16`.
6. **A stun is not a death and a knife `91 00` is not a kill.** Both carry a whole bar in the
   damage word and neither moves health.
7. **The feed is written on the beat the victim's vitals reach zero** — see §7.
8. **A damage record never names a body that is already down** — see §7.

---

## 7. The two byte-level diffs of 2026-11-12

Both were read off `.pcapngs/actions/actions.pcapng` (the working host) against
`.pcapngs/myserver.pcapng` (this host), with `tools/kill_feed_players.py`,
`tools/bot_damage_trace.py`, `tools/kill_beat_diff.py` and
`tools/profile_entry_diff.py`.

### 7.1 The killer is resolved from the feed's own bytes, but only on the death's beat

The resolver `FUN_1004f140` (§5) turns the feed's killer byte into a name through the
roster table, and both handlers build the `0x138`-byte log entry from it. What the captures
differ in is **when** the feed arrives relative to the victim's vitals zero:

| capture | feed | victim's vitals reach `0` | gap |
|---|---|---|---|
| working | `09 01 12` at t+43.78 | t+43.78 | 0 frames |
| working | `09 16 02` at t+48.55 | t+48.53 | 1 frame |
| working | `09 08 07` at t+49.71 | t+49.68 | 1 frame |
| working | `09 09 15` at t+55.05 | t+55.05 | 0 frames |
| this host | `09 10 05` at t+32.56 | t+32.32 | 8 frames (0.24 s) |
| this host | `09 13 10` at t+35.76 | t+35.53 | 8 frames (0.23 s) |
| this host | `09 15 12` at t+42.82 | t+42.59 | 8 frames (0.23 s) |

**And the reference writes the death before the feed, inside the frame.** Read as record
indices of one datagram (`tools/kill_beat_diff.py`):

| capture | victim's vitals zero | feed | victim's state |
|---|---|---|---|
| working t+43.29 | 8 | `09 16 04` at 10 | `00 ff 00` at 11 |
| working t+48.55 | 2 | `09 16 02` at 4 | at 5 |
| working t+49.71 | 13 | `09 08 07` at 15 | at 16 |
| ours (before this fix) | earlier frame | feed | then the state |

The kill-log entry is only built while the victim is still standing
(`killer->[0x88] == 1 && victim->[0x88] != 1`), so a feed that arrives before the death it
names is a feed with nothing to log. `BotActorPublishService` now writes the victim's zero,
the feed, then the block, in that order.

### 7.1a The per-slot settings header is not the name's source either

Measured, and rejected. The 25-byte settings header
(`0b SS 02 … 10 00 fe ff`, `SettingsHeaderBodiesUtils.BuildJoin`) covers seventeen slots in
the reference and four in this host's capture, which made it a candidate for the row a feed's
killer resolves through. It is not one: **24 of the reference's 129 feeds name a killer that
has no header before them** (slot 16 at t+43.29 is the first) and **9 of this host's 11** do,
so the two captures do not differ in kind, and the header is written per join rather than per
row.

**The feed's bytes were never the fault.** Every bot kill this host wrote named a seated
slot in the right byte — `09 07 04`, `09 10 05`, `09 13 10` — and the profile entries those
slots resolve through are byte-identical to the reference's: `tools/profile_entry_diff.py`
reports slots 1–15 identical, with only the host's own entry differing at `+0x08`/`+0x09`
(its character id) and `+0x10`. The attribution was late, not misnamed: the log entry is
written when the feed is handled, and the beat it was written on was the beat **after** the
death had already been drawn. `FeedDelayMilliseconds` is now `0`, and the feed rides the
victim's death beat as the reference's does.

### 7.1b The stage scripts do not build the feed

The HUD was checked in the game's own script language before the code was trusted, over
**every stage and not one**. GCL (`.gametools/gcl-vscode`, `node bin/gcx.js`) decompiles
`game/stage/<stage>/scenerio.gcx` to byte-exact text; all 26 decompile, giving **147,186
lines** of script.

| what was searched | across 26 stages | result |
|---|---|---|
| `kill`, `feed` (excluding `skill`) | 147,186 lines | `-bombkill 10` only, a rule option |
| `log` as a word | 147,186 lines | nothing |
| options ending in `kill` / `log` / `feed` / `name` | 147,186 lines | `-skill N` only |
| labelled command hashes | 77 distinct labels | `d4ea76 NewHeadUpDisplay`, 21 uses |
| feed-shaped format strings | 147,186 lines of resources | none in a match stage; the only `%s` strings are the lobby's `FULL %s` / `SOLD %s` |

- The HUD is one engine handler, reached by `-init_hud p:proc32` and written as
  `chara [d4ea76/NewHeadUpDisplay] [a9f2e2]` with a list of BGM ids behind it — the same
  body in all 21 stages that have a HUD.
- Its drawing lives in the packed asset `game/afp/afp_hud_common.afp` (1.2 MB of `TXP2`),
  and no tool in `.gametools` reads that container: Haven's formats are
  `Dar Dds Dld Dlz Gcx Geo Lit Mdn Qar Txn`.

So no script change can move a name in the feed; the resolution is the code path in §8, and
the ordering above is the only thing a host controls.

### 7.1c Two record sets this host did not write, and what they cost

`tools/compare_captures.py` over the whole of both captures, at byte level. The actor block
is the same set of records on both sides — state, vitals, position, weapon pair, weapon byte,
aim, damage — and its per-frame rates agree once the 10× capture length is taken out. The two
differences that are not rate differences are these.

**The round start was written 43 times as often.**

| id | role | working (592 s) | this host (51 s) |
|---|---|---|---|
| `0x1ade` / `0x12de` | round-start payload | **2** | **82** |
| `0x5ade` / `0x52de` | round-start marker | **9** | **4** |

The 82 arrived in bursts — twelve identical `0x12de` in the same instant at t+10.45 — because
`BriefingStateService.BuildPublication` wrote one payload beside **every** ready-state record,
one per reporter and one per bot, and a room of sixteen therefore re-opened the round sixteen
times a publication. A client that re-reads a round start rebuilds the round and its actor
table, and a body rebuilt every second is one that takes no damage and never dies on screen.
It now writes **one payload per publication**, carrying the byte of the report it answers. The
next capture of 56.9 s writes **7** (`0x12de` 4, `0x1ade` 3) against the 82 this paragraph
counted before the change, and none of them repeats in one instant.

**The per-slot settings header covered four slots out of seventeen.**

| record | working | this host |
|---|---|---|
| `0x1001`/25B `0b SS 02 … 10 00 fe ff` | 194 over **17 slots** | 22 over **4 slots** |
| `0x1001`/50B loadout | 8 | 18 |
| `0x1001`/34B gear | 5 | 15 |

Only the host's own seat and a joining player's ever got the 25-byte header; a bot got a
loadout and a gear body and nothing that gives a client a **player at that slot**. That row is
what a hit is applied to and what a feed's killer byte resolves through, so it is the other
half of both reports.

**Two placements were tried, and both are wrong for their own reason.**

`BotRosterPublishService` was the first: it announces bots *appended* to a live room, and a
room whose bots are seated by its **initial run** never reaches it, so the capture after that
change still wrote 22 headers over the same four slots.

`RoomRosterFramesUtils.BuildRun` was the second, one header per seated member after the
room's count — and it **broke the join**. The whole run goes out as a single datagram
(`RosterRunSendUtils`), so sixteen seats' worth of headers took the answer from **1,148 bytes
to 1,641**, over the 1,500-byte MTU: the peer received a fragmented roster it never finished,
and the capture taken after the change holds **30 frames instead of 4,000** — no join, no
round. It is reverted, and the lesson is the one this section keeps re-learning: the reference's 194
headers are **spread through its settings bursts over the round**, one burst at a time, never
stacked into the answer to a join. Whatever carries them has to spread them the same way, and
it has to be measured at the datagram, not at the record.

The third placement is where they now go: `PlayerSettingsBurstService.JoinPreamble`, one
`BuildJoin` per seated member after the host's own. That burst is a **second, smaller
datagram** — this host's largest is **512 bytes** where the reference's largest is **1,472** —
so fifteen more headers leave it near 900 bytes and inside the limit the roster answer broke.
It is also the record's natural carrier: the burst already wrote the host's own header there
and already writes a loadout and a gear body per player of the room.

**Measured after the join regression, one capture each.** The regression capture holds 3,388
frames and a client that wrote **three** damage records (all weapon `0x14`, the P90), against
the reference's 22 over a longer round. The feeds in it name killers `1, 9, 9, 12, 13, 16` —
not the host's slot `0`, so the byte-level attribution is right and what is wrong is the
client's resolution of those slots, which is exactly the seat record above.

**Confirmed and unknown in the records this rests on.**

| record | id, class | body | status |
|---|---|---|---|
| state | `0x084d + 10k`, `0x01` | 3B `00 ff 00`; 9B `42 ff 00 x y z` | **[V]** shape and order |
| vitals | `+1`, `0x03` | 2B health `fa`=250, stamina | **[V]** both bytes |
| position | `+2`, `0x02` | 16B living, 32B corpse `22 1a …`, 38B `61 02 …` | **[V]** 16/32; **[U]** the 38B form |
| weapon pair | `+4`, `0x04` | 5B `00 18 00 00 0f`; zeroes on a corpse | **[V]** |
| weapon byte | `+4`, `0x06` | 1B `00` | **[U]** what it names |
| aim | `+6`, `0x05` | 6B, three words at a tenth of a unit | **[V]** |
| damage | `+7`, `0x00` | 15B: subtype, points×128, weapon, impact, pitch, bearing | **[V]** except pitch **[U]** |
| feed | `0x254` pair, class `0x12` | 3B `09 <killer> <victim>` | **[V]** |
| settings header | `0x1001`, class `0xa*` | 25B `0b SS 02 … 10 00 fe ff` | **[V]** shape; **[U]** the carried field |

### 7.2 This host wrote hits on bodies that were already down

`tools/bot_damage_trace.py` answers every damage record with the victim's next vitals
record inside two seconds:

| capture | damage records | answered | unanswered | client hits naming a slot at `0` |
|---|---|---|---|---|
| working | 793 | 739 | 54 | 1 of 49 |
| this host | 85 | 31 | 54 | 14 of 18 |

The host's own records were the unanswered ones: a bot whose target had just gone down still
fired its burst, `ApplyDamage` refused the blow on the corpse, and the **damage record was
written anyway**. The reference writes no hit on a dead actor — its 350 host-written records
all name a living slot — and the client applies whatever word it is handed, so a record on a
slot the host holds at `0` stands the body back up on the client's screen. The player then
spends a magazine on an actor whose every bullet the host drops, which is the report of
"the bots do not take damage".

`BotShot.Landed` now carries whether the blow was one a body could take, and a shot at a
downed victim writes no record (`BotSimulationService`, `BotActorPublishService.Combat`).

---

## 8. The whole path a kill feed takes, sender to log row

Read out of `system.dll` (base `0x10000000`) with Ghidra, 2026-11-12. Every step below is a
decompiled call or a data reference, not an inference.

| # | function / data | what it does |
|---|---|---|
| 1 | our host, `MatchKillFeedBodiesUtils.Build` | writes `09 <killer> <victim>` on the operations stream |
| 2 | `FUN_101514a0(code, body)` | the record module's sender: `FUN_1001ecf0(*(DAT_31864bf8+0x60), *(DAT_31864bf8+100), code, body, len)`. 25 call sites, all record parsers (`0x101523bd`, `0x101535b9`, `0x101540a4`–`0x1015427c`, `0x101b5ad8`–`0x101b6b76`, `0x102bd9fc`–`0x102be186`, `0x1032b08f`–`0x1032b86e`) |
| 3 | `FUN_1001ecf0(ctx, code, body, len)` | looks the context up (`ctx * 0x10`, matching id and sub-id) and calls `FUN_1001ec60` |
| 4 | `FUN_1001ec60(ctx, code, body, len)` | walks the **listener list at `ctx + 0x34`**: three ints per entry `{code, fn, ctx}`, `code == -1` receives every code, and bit `2` of a handler's return stops the walk |
| 5 | listener entry `0x110e15cc` = `{ -1, 0x10151e60 }` | the game-event dispatcher is a `-1` listener |
| 6 | `FUN_10151e60(this, code, body, len)` | `case 0x150005: FUN_102b7810(this->0x300, body)`, `case 0x150011: FUN_102b7a90(this->0x300, body)`, plus `0x150001/2/6/7/8/b/c/f/10/17/19/23/24/28` |
| 7 | `FUN_102b7810` / `FUN_102b7a90` | `body[0]` killer, `body[1]` victim, `body[2]` kind; streak at `this+0x8a8`; names through `FUN_1004f140`; `FUN_1035fd20(this+0x8a8)`, `FUN_1035fe20(this+0x8ac)`; row through `FUN_102b6b50` / `FUN_102984e0` |

**The handler's body is `killer, victim, kind` — the wire's `09` is not in it.** Both parsers
read `body[0]`, `body[1]` and `body[2]`, and the only two places the dispatcher's code is
built as an immediate build exactly that shape:

- `FUN_10224c20`, the local player's own death (`*(iVar9+0x90) == 0xcb`): two bytes,
  `CONCAT11(slot, slot)` — killer and victim both `player+0x84`, a death nobody is credited
  for.
- `FUN_104fa000`, the local player's own kill (state `0x2c`/`0x2d`): three bytes
  `[*(iVar6+0x6c), *(iVar1+0x1c), *(iVar6+0x78)]`.

There is **no third site** in the image that builds `0x150005` or `0x150011` (searched for
the constants `05 00 15 00` and `11 00 15 00`; `MGO2.ELF` holds neither). A received feed
reaches the dispatcher through step 2's `code` argument, which is why a constant scan of
the network side finds nothing — the translation from the wire opcode to the event code
lives in the record parser's argument, not in an immediate.

**What that fixes for this host:** the three wire bytes are already the handler's first
three, so the only host-side questions are when the record is written (§7.1) and whether
the slots it names resolve to live roster rows (§5, `FUN_1004f140`). Both are answered
above, and neither is a matter of the feed's byte layout.
