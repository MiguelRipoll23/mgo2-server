# Findings — giving the replayed records meaning, so they can be built

Working notes for the goal in the commit message: *find meaning to all replay
data to build it instead of replaying it.* The gameplay host writes its match
records by replaying a capture (`src/GameplayServer/MatchRecords/`); this file
records what each replayed byte is, what it is still only a literal, and what
was settled this pass.

`[V]` measured over captures · `[D]` derived here from a measured rule ·
`[I]` inferred · `[U]` unresolved.

---

## 1. The loadout record's length is now derived, not listed

The replay console's `LOADOUT_LEN` (working-tree `rpdt.js:104`) grew from
`[50, 51, 53…58]` to `[50…62]`. That list looked like a whitelist of shapes;
it is not. Working the layout out over the three recorded bodies gives the
rule behind every entry, which the list never stated.

### 1.1 The layout is fixed-length, and the length is a function of the pairs

Take `rpdt.js`'s own reader (`skillRecord`, `rpdt.js:437`) — `nn = b[a+22]`,
`end = L − 23 − nn − 1`, the list ends on `0xff` at `end`, the pairs are read
back from there — and run it over the three bodies the server holds as
templates:

| body | `L` | `b[0x16]` (`nn`) | `end` = `L−26` | region `b[0x17..end]` | pairs |
|---|---:|---:|---:|---|---:|
| `PlayerLoadout` | 50 | 2 | 24 | `fe ff` | 0 |
| `SecondLoadout` | 54 | 2 | 28 | `ff 01 03 02 01 ff` | 2 |
| `ThirdLoadout` | 56 | 2 | 30 | `ff 05 02 0d 03 0f 01 ff` | 3 |

Two things fall out, and they are the whole of the answer.

**`nn` is not a size — it is the constant `2`.** The two-byte run between the
list end and the appearance block fixes `end`:

```
L = 0x17 (header) + (region) + 2 (the run before the appearance) + 23 (appearance)
  => end = L − 26        regardless of nn
  => nn  = L − 23 − (L − 26) − 1 = 2    for every body
```

So `nn` is a constant of the record, not a count, and `end` is a fixed distance
from the body's end. `[D]`

**The length is the pair count.** The region is a leading marker, two bytes per
pair, and the list end:

```
region = 1 + 2·pairs + 1
L      = 23 + (2 + 2·pairs) + 2 + 23 = 50 + 2·pairs
```

| pairs | 0 | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---:|---:|---:|---:|---:|---:|---:|
| `L` | 50 | **52** | 54 | 56 | **58** | 60 | 62 |

and the `0x10` region suffix `rpdt.js` drops when present adds one byte, giving
the odd lengths `51, 53, 55, 57, 59, 61` for the same pair counts. **`59` — the
other length the working tree adds — is four pairs with the suffix**, which is
why it looked unrelated to `52` when they were added together.

`LOADOUT_LEN` is therefore exactly `{50 + 2p + s : p ≤ 6, s ∈ {0,1}}`, and
`52` was missing from it only because a one-skill player had never been in a
sampled file. **[V]** — the comment on `rpdt.js:104` says exactly that, and the
derivation says why the number was needed at all.

### 1.2 What this changes on the server

`LoadoutBodiesUtils` held three shapes and stated the consequence:

> the lengths the replay decoder accepts contain no 52 — the length a single
> pair would need — so a player with one or four equipped skills has no recorded
> body of that shape

That was true of the **list**, and it is why the gap existed. It is no longer
true of the format:

- `MaximumSkills` was `3`. `CharacterEquippedSkill` carries **four** slots
  (`LoadoutFramesUtils.TrySkillsOf` reads `Skill1..Skill4`), so a four-skill
  player was reachable and was written at the wrong length.
- A body written at the wrong length is the one failure the burst cannot
  absorb: the player is announced with a *different player's* skill count, and
  the pair the room holds for him is simply not on the wire.

Now resolved:

| shape | pairs | `L` | source |
|---|---:|---:|---|
| `PlayerLoadout` | 0 | 50 | recorded (`mgo2-game1.pcapng`, t+6.687) |
| `SingleLoadout` | 1 | 52 | **built** — the 50-byte body with the region sized for one pair |
| `SecondLoadout` | 2 | 54 | recorded (the capture writes it twice) |
| `ThirdLoadout` | 3 | 56 | recorded |
| `FourthLoadout` | 4 | 58 | **built** — the 56-byte body with the region sized for four |

The two new shapes are **built, not captured**: they are a recorded body with
two (or four) zero bytes spliced into the region behind its leading marker,
after `0x17`. Every other byte is the recorded body's, and the spliced bytes are
always overwritten by the player's own pair in `LoadoutFramesUtils.TryWrite`, so
no value is invented — the same claim the three recorded templates already
make, extended to the two shapes they could not express.

`L = 50 + 2·pairs` is now a rule (`LoadoutFramesUtils.BodyLengthFor`), so the
next shape needs no new constant.

### 1.3 The region marker — a lead, not a claim

The no-skills body leads its region with `fe`; every body with pairs leads with
`ff`. `0xff` is the replay parser's `SKILL_LIST_END` and `0xfe` its rules-roster
`LIST_END`, and the parser never checks the leading marker at all. So the byte
is unread, not read-and-equal. Whether it says "this body carries no skills" or
is simply an uninitialised tail byte is **[U]**; the two new shapes each take
the marker of the body they grow from, which is the recorded behaviour in both
cases and asserts nothing beyond it.

---

### 1.4 The round's opening run is thirteen records, and this host wrote eleven

`MatchStartRunFramesUtils` carried the one open point it named itself: the run
holds a compressed frame, so its records are only legible after decompression,
and its two documents disagreed on the size — "the 13-record match-start run" in
one and "the twelve records behind them" in the other, which would make
fourteen. Decompressed, both captures write the same thirteen, in the same order:

```
0x5001 len 0             the roster head, twice
0x5001 len 0
<ops>  len 90            the state block
<ops>  len 2             0f 00
0x9001 len 26            the team array      (index)
0x1001 len 26            the occupancy array (index)
0x1001 len 20            the close pair
0x1001 len 20
0x1001 len 7             07 00 00 00 00 00 03 — the roster close
<ops>  len 3             the round clock
<payload> len 6          the answer to the report
0x0a61 len 1             the empty rules list, under each class
0x0a61 len 1
```

`mgo2-game1.pcapng` writes the marker as a datagram of its own (t+46.415,
counter 24) and the thirteen behind it (counter 32793);
`mgo2-game-started-on-briefing.pcapng` writes the marker inside the same frame
(t+198.371, counter 655), with a live item-box group in front of the thirteen.

Three things the host was missing or had out of order, now built:

| | before | the capture |
|---|---|---|
| the run's head | started at the payload | two empty `0x5001` |
| the run's `0x1001` tail | a close **pair** | a close **triple** — the pair plus the seven-byte roster close (`PlayerProfileRecordUtility.BuildRosterClose`) |
| the order | arrays → presence → block → short → clock → rules → closes | heads → block → short → arrays → closes → clock → payload → rules |

The close triple's third record was the one thing the run needed that the change
did not have to invent at all: it is the same record a join's roster run closes
on, already built by `PlayerProfileRecordUtility` and already written by
`RoomRosterService`. **[V]**

**The two captures disagree on the order, and that is worth knowing.** The
*set* above is the same in all three runs, but `mgo2-game1` puts the payload near
the end with the rules pair behind it, while `mgo2-game-started-on-briefing` and
`mgo2-game-joined-when-red-team-then-briefing` put the payload straight behind
the two roster heads and write **no rules pair in the run at all** (their
`0x0a61` pair rides the beat a frame later). The run's records and the clock are
identical; only the order differs. So the order is not a gate a client waits on —
which is worth remembering before reading this change as the fix for a client
that will not leave its briefing. The three records the run was missing *are* the
substantive part of it. `tools/udp_start_run.py` prints either capture's run
beside the shape the host writes, by role, and shows both.

**What the run still cannot settle from the capture alone.** Nothing in these
two files separates "the client needs a record it was not sent" from "the client
needs a record it was sent at the wrong moment": both captures were taken
against hosts whose runs already had the heads and the close, so neither is a
negative control. That is a live test, and it is not one this host can run from
the repository.

### 1.5 The join state answer's first byte is the reporters in order

The row in §2 assumed the set was "a bit set over its slots". It is not, and the
captures' own values say so:

| capture | values | as bits |
|---|---|---|
| `started-on-briefing` | `02`, `06`, `0e`, `1e`, then `16` once a player left | bit 1; bits 1–2; 1–3; 1–4; then 1, 2 and **4** |
| `joined-when-red-team` | `2a` | bits 1, 3 and 5 |

That is one bit per reporter in the order they reported, and the bit of the
reporter who left is the one that clears — `1e → 16` drops bit 3, which is the
third reporter. Read as the reporting players' *lobby slots* the bits do not fit:
`joined-when-red-team`'s own arrays name slots `0x0b`, `0x0f` and `0x14`, and
none of the three is a bit of a byte. So `02` was never "a room of one" — it is
the bit of the **first** reporter, and a room of one writes it because its only
reporter is first.

`BriefingStateService.JoinedMask` now writes the room's own set — one bit per
`ReadyState`, at `PublicationIndex + 1` — and `BuildRun` carries it into
`MatchStartBodiesUtils.JoinStateAnswer`. What stays **[U]** is the trailing
three bytes, which have never moved in a capture and stay the recorded zeros.

## 2. What is still a literal, and what it would take

The rest of the replayed stream, worst first. Each is a value the host currently
copies; each needs a rule to become a build.

| replayed value | where | what would settle it |
|---|---|---|
| the sparse phase's 17 steps — 11 delays totalling 42 240 ms | `SparsePhaseFramesUtils` | the delays are the host's own while it waits; they need to become a rule keyed on what the host is waiting for |
| the same steps' fourth byte, `0x02…0x09` in order | `SparsePhaseFramesUtils` | one counter, or nine meanings |
| the `0xd001` bodies `02` / `03` / `04` / `25` | `MatchRecordBodiesUtils` | what the four control bytes say |
| the 22-byte `0x0002` blob's `0x40` marker and its big-endian `u16` | `MatchStateBodiesUtils` | what the marker is, and what the value counts |
| the state vector's 49 constant bytes of 89 | `MatchStateBodiesUtils.ConstantRuns` | the nine offsets the room writes are known; the other 49 are the layout's constants and need naming to be built |
| the close triple's two 18-byte remainders | `MatchRecordBodiesUtils` | the roster header layout past offset 7; the triple's third record is the roster close and is no longer a literal (§1.4) |
| `0x0f 0x00` | `MatchRecordBodiesUtils` | the one operation opcode the replay parser does not name |
| the item-box opening bodies (8) | `ItemBoxRecordBodiesUtils` | the head/item codes and the positions |
| `JoinStateAnswer`'s trailing three bytes | `MatchStartBodiesUtils` | **the first byte is settled (§1.5)** and written from the room; the three bytes are not |

Only the trailing three bytes of the eighth row are left: its first byte had a
measured rule in hand and is now written from the room (§1.5), because the rule
turned out not to be the one the row assumed.

## 3. The binary, and what was established about it

`MGO2.ELF` (19,615,992 B, sha256 `1A55A41E…`) is **MGO2 1.36 decrypted** — not
the disc build. Findings, all re-derived with python capstone (`CS_ARCH_PPC`,
`CS_MODE_BIG_ENDIAN | CS_MODE_64`):

- `file offset = VA − 0x10000`. Code `0x10200`–`0xFBD91C`, rodata
  `0xFBD91C`–`0x118E348`, file-backed data `0x1190000`–`0x12ACA00`, `.bss`
  `0x121E0A8`–`0x128C3E0`.
- **TOC = `0x01222A00`**, read out of the entry stub at `0x10230`, not guessed.
  Every `lwz rX, disp(r2)` with a negative displacement is a TOC slot.
- The **OPD section is at `0x011E7358`** and every entry is
  `{u32 entry, u32 toc}` with `toc = 0x01222A00`. Any search for a bare function
  pointer misses.
- The **P2P module is `0x25F000`–`0x276000`**, found on the SDK socket stubs
  (`recvfrom` `0xFBB59C`, `socket` `0xFBB75C`), whose virtual addresses are
  identical in both builds.
- **The P2P module has not moved between the disc build and 1.36.** All 23
  addresses the repository's docs cite decode identically here: decoder
  `0x2666C8`, handshake receiver `0x267D58`, serializer `0x269860`, receive
  loop `0x2620D8`. `docs/ADDRESSES.md`'s "no disc address transfers" does not
  hold for this module. **[V]**
- The **record's first word carries the vid**: at `0x2698A0` the serializer does
  `clrlwi r10, r0, 0x14` — `word0 & 0xFFF` — and writes that as the wire type.
  So the id lives in the record object, **not in an immediate**, which is why a
  `cmpwi` sweep for `0x1a54` finds nothing.
- The **per-record receive handler is a vtable slot, not a table index**:
  `0x27A180` loads the object's vtable, then the vtable's first OPD entry, then
  `bctrl`. The frame-drain loop is at `0x26E790`.
- The handler array is 0x10-byte descriptors at `module_instance + 0x34`
  (instance at `0x011A6308` — **corrected below**). Re-read with capstone, the lookup at `0x2614F8`
  masks an entry's word 0 with **`0x7F000FFF`** (`lis 0x7f00` / `ori r7,r7,0xfff`
  at `0x261568`/`0x261578`) and compares it against
  `((0x18 + bank) << 24) | (argument & 0xFFFF)` — `slwi r0, r4, 0x18` /
  `or r6, r0, r9` — where the bank is 0 when the caller's object is the one at
  `instance + 0x2C` and 1 when it is `instance + 0x30`, and anything else returns
  `-1`. So `UDP_P2P_PROTOCOL.md` §6.2's mask reading is the right one, and the
  `0x7F007FFF` that stood here was a misread. It also explains §6.2's own puzzle:
  the argument's bits 12–15 **stay** in the comparison key while the entry's are
  dropped by the mask, so a class nibble in the argument cannot match at all
  rather than being ignored — which is why `0x5001` could not be dispatched
  literally and why the argument is evidently not the raw type word there. An
  entry is considered only when bit 7 of its byte `+0xA` is set (a signed test,
  `extsb`/`bge`), and the walk is bounded at 0x1000 entries. **The array is built
  at runtime** — the slot holds `0xE353F7CF` in the file — so the id→handler map
  cannot be read out of the image.
  **Correction: `0x011A6308` is a static holding the instance pointer, not the
  instance.** The instance is `0x01856C10` at run time, and the array's per-`vid`
  entry is a key with a use count and not a handler — the handler is the record
  class's vtable slot 0 (`0x27A180`). See
  [the dispatch read-out](udp-record-dispatch.md) §2–§3.
- The P2P object's own block from `TOC − 0x7ABC` is `0x011AE308`; `docs` cites
  `0x1856C10`, which **is not an address at all** — it is a constant stored in
  the module object's first 48 bytes, outside every mapped segment. **[V]**

### 3.1 The route that is left for the handler map

Three, in order of cost:

1. **Runtime.** Break at the send primitive `0x261AB0` (`r5` = session, `r3` =
   record buffer); the descriptor walk begins at `0x261B2C`
   (`lwz r6, 0x34(r10)`). Walk the 0x10-byte entries the way `0x2614F8`
   matches them, which is now read statically and not guessed: an entry counts
   only when bit 7 of its byte `+0xA` is set, and its word 0 carries the id in
   bits 0–11 **and** the bank selector in bits 24–30 — `word0 & 0x7F000FFF`
   against `((0x18 + bank) << 24) | id`, so `word0 & 0xFFF` alone is only half
   the key. One pass gives the complete map.
2. **The registrar.** Break on the store to `0x011A633C` and walk the callers;
   they are module-init functions whose arguments carry the vids.
3. **vtables in the data section.** Scan for runs of `u32` values inside the OPD
   range `0x011E7358…0x01222A00` — each run is a vtable of function pointers, and
   its first slot is the handler (a function pointer is an OPD address, not a
   `{entry, toc}` pair; the correction is below). This is static and is the only
   one that needs no debugger.

The handler is where a body layout becomes readable: a handler that does
`lbz r3, 5(r4)` says body byte 5 is read, which is the difference between
copying a body and building one. That is what the remaining rows in §2 need.

**Route 3 as written does not hold, and the correction is cheap.** A scan of the
whole image for contiguous `{u32 entry, u32 0x01222A00}` pairs finds exactly one
run — the OPD section at `0x011E7358` itself, 26 325 entries — and nothing else.
That is the PowerPC64 ABI: a function pointer is the address of an OPD entry, so
a vtable holds `u32` values inside `0x011E7358…0x01222A00`, not the `{entry, toc}`
pairs those values point at. Scanning for runs of *those* finds 876 runs in the
file-backed data section from `0x01190004` on, which is the shape route 3 needs.
**[V]**

**Route 3, done: eleven P2P vtables, and the module object's own.**
`tools/p2p_vtable_scan.py` walks the data section for runs of OPD pointers,
dereferences each slot through its OPD entry and keeps the runs with a slot
inside the module's code range. Of 3 418 runs, **11** reach the P2P module
(`0x25F000`–`0x276000`). Two of them confirm the docs and one is new:

| vtable | slots | what |
|---|---:|---|
| `0x01191B30` | 3 | slot 0 is `0x26E790`, the frame-drain loop §3 names **[V]** |
| `0x011A63C8` | 3 | the **module instance `0x011A6308` + `0xC0`** — the module object's own vtable. Slot 0 `0x27E8B0`, slot 1 `0x26DE98`, slot 2 `0x26DB78` |
| `0x01191AB8`, `0x01191C68`, `0x01191CA8`, `0x01191CE8` | 14 | the dispatch class family: slots 0–3, 5–7 identical across all four (`0x263328`, `0x268930`, `0x263330`, `0x263348`, `0x263358`, `0x2632B0`, `0x2635F0`), differing only at slots 4, 8, 9 — a base with four deriveds. Slot 1 `0x268930` is the serializer's neighbourhood (§3 reads the type word at `0x2698A0`) |
| `0x011A63E0` | 7 | a second object beside the module instance: slots 0, 1, 2, 4, 5, 6 in the module (`0x2749B8`, `0x274D30`, `0x2749C0`, `0x274ED8`, `0x274E50`, `0x274CF8`) |

The receive path §3 cites was re-read and holds: `0x27A180` tests bit 1 of the
record object's byte at `+0xD`, then takes the object at `+4`, loads *its* vtable
(`lwz r9, 0(r11)`), the slot's OPD entry (`lwz r10, 0(r9)`) and the entry's code
(`lwz r0, 0(r10)`), and branches — so the handler is the record's **vtable slot
0**, and a record class per handler is why the run's own record types answer
differently. **[V]**

**And the operation dispatch is a table, not a jump.** The module object's own
slot 0 (`0x27E8B0`) opens by reading **one byte** of its argument through the
byte reader at `0x26CB20`, then does `rlwinm r0, r0, 2, 0x16, 0x1D` — the byte
scaled by four and masked to `0x3FC` — and `lwzx` through it, with the table base
taken from the module object (`lwz r9, -0x8000(r30)`, `r30` being `TOC − 0x7A88`,
so `r30 − 0x8000 = 0x011A6308`). A one-byte key, a table of four-byte entries and
a null entry that branches away: that is the operations stream's opcode dispatch
— the same shape `MatchRecordFramesUtils` reconstructs — and it is the thing to
read next, because handler N is where opcode N becomes readable. The table itself
is built at run time, so its entries are the subject of §3's routes 1 and 2 and
not of a static read. **[V]** for the dispatch; **[U]** for the table's entries.

Two of §3's addresses were re-derived against the file while checking this, and
both hold in 1.36: the send path's descriptor walk is really
`lwz r6, 0x34(r10)` at `0x261B2C` with the object taken from `TOC − 0x7ABC`
(`lwz r30, -0x7abc(r2)` at `0x261ACC`), and the serializer's type word is really
`clrlwi r10, r0, 0x14` at `0x2698A0` with the class bits OR'd on behind it
(`ori r9, r10, 0x1000` at `0x2698BC` is the reliable class, `ori r11, r11, 0x2000`
at `0x2698E8` the next one up) — which is the wire type the codec's
`FrameBuilderUtility.MessageOf` builds. **[V]**

## 4. Correction to a stale comment in the tree

`MatchRecordFramesUtils.cs:57-71` said the stream "opens no round, so it stays on
the first set for the whole stream". It does not: `BeatCounters` carries
`FrameIndex`, `MatchRecordService.BeatAsync` restarts it on a round change and
passes `roundState.Generation` per frame, and `MatchRecordGenerationUtils` has
the sibling set ready. **Both places said it and both are now repaired** — the
`MatchRecordFramesUtils` paragraph no longer lists the sibling set among what the
replay leaves out, and `BeatCounters`' remark says the beat reads the set off
`MatchRoundStateService.Generation` rather than holding `First` for the stream.
**[V]** the code, and the repair is in this change rather than still owed.

---

## 5. The six captures read against each other: rule, or one room's data?

Every row in §2 is a value this host copies from `mgo2-game1.pcapng`. This
section asks the next question of each — *does any other host write it the same
way?* — because a byte four hosts write and two do not is the third case, a rule
for some hosts and state for others, and a byte no two hosts share is not a rule
at all. Measured with `tools/udp_session_records.py`, whose `--flags2` mode
prints one stream's fourth byte in wire order.

### 5.1 The fourth byte of a session record is the stream's own sequence number

**Derived, and already implemented in one place.** For a record whose identifier
is `0x1000` or above, the fourth byte is not a class and not a constant: it is a
rolling sequence number for that record's stream, repeated when the capture
repeats the record and climbed by one on the next record of the stream — and
§5.7 sharpens *stream* to the record's low twelve bits, which is the form that
makes the shared runs visible. Over the
six captures:

| capture | stream | records | `+1` steps | first values |
|---|---|---:|---:|---|
| `joined-when-red-team` | `0x1a5a` | 632 | 437 | `00 01 02 03 04 05` |
| `joined-when-red-team` | `0x1250` | 1 068 | 898 | `00 01 02 03 04 05` |
| `started-on-briefing` | `0x1a50` | 352 | 310 | `00 01 02 03 04 05` |
| `started-on-briefing` | `0x125a` | 244 | 157 | `00 01 02 03 04 05` |
| `game1` | `0x1a5a` | 62 | 40 | `00 01 02 03 04 05` |
| `game1` | `0xd001` | 57 | 33 | `01 01 01 03 03 04` |

The steps that do not advance are the repeats: the same record sent again before
its answer arrived, carrying the value it was first given. That is why the counts
fall short of a perfect run — `game1`'s `0x1a5a` stream writes 62 records for 24
distinct values, and the live client answers each with the value it received
(`0x5a5a` answering `0x1a5a`), which is the mechanism
`ItemBoxFramesUtils.ItemBoxGroup` already implements and documents.

**The sparse phase's literals are faithful to `game1` and fixed to it.**
`SparsePhaseFramesUtils.SparseSteps` stamps its controls `0x02`, `0x02`, `0x03`,
`0x03`, `0x04` … `0x09`, and that is `game1`'s own `0xd001` stream minus its
first value — read as a sequence it is the capture's numbering, including the two
values it repeats, one of which is the same body twice (a retransmission) and one
of which is two different bodies sharing `0x03`. So nothing here is wrong about
`game1`; what it is fixed to is *one host's starting point*. A host whose stream
has already reached `0x09` when the sparse phase begins — which is what the other
five captures show, their phases starting anywhere from `0x01` on — writes those
same values over records the client is already tracking, and the rule that would
avoid it is the one the item-box group already applies. **[D]** the rule;
**[V]** it across five streams in three captures; **[I]** the consequence for a
host that starts elsewhere.

### 5.2 The sparse phase's bodies are one host's state, and its length is that
host's wait

The phase this host replays is `game1`'s, and the six captures do not share it.
The control's body byte — `0x02` and `0x03` in `game1`, which is why
`MatchRecordBodiesUtils` names them *early* and *alternate* — is a different
value in every other capture:

| capture | n | the `0xd001` bodies before its marker, in order |
|---|---:|---|
| `game1` | 12 | `24 02 02 02 03 02 02 03 02 03 02 03` |
| `game2` | 28 | `0b 05 11 07 0f 11 0d 0e 10 05 0a 07 09 07 01 0f 01 06 0e 04 0c 02 0d 11 0a 07 0a 10` |
| `game3` | 13 | `24 24 03 03 03 03 03 03 03 03 03 03 03` |
| `started-on-briefing` | 71 | `0c 0a 0a 03 0a 10 0b 10 02 08 0b 01 04 05 0c 03 0d …` — its marker lands minutes later, so this is its whole briefing stream |
| `started-team-selection` | 68 | `34 02 02 02 12 02 12 13 03 13 14 02 13 02 16 …` |
| `joined-when-red-team` | 22 | `08 14 04 04 10 10 10 00 08 0c 00 04 08 08 0c 2c 10 10 00 04 08 04` |

The values run to `0x9a` and no two hosts draw the same set — `game1` uses
`02`/`03` and a `24`, `game3` writes one `03` for its whole phase, and the
red-team host's are **every one a multiple of four** (`00 04 08 0c 10 14 2c`),
which reads as a bitfield rather than an enumeration. So these are the host's own
item, map or flags state, not a protocol byte: `EarlyControl` and
`AlternateControl` name `game1`'s two values and not the wire. The *length* of the
phase is the host's wait for its players and moves the same way: `game1`'s run
opens at t+46.4 s and `game3`'s at t+59.6 s, on the same 42.2 s step list.
`SparsePhaseFramesUtils`'s own remark already says the step list is read off
`game1` and that nothing in it is derived, so this measures the size of that
claim rather than contradicting it. **[V]**

### 5.3 The close triple's two remainders are room data, not constants

The run's two 20-byte records open `07 0d` — the roster version and the record's
own length, which the host writes — and the eighteen bytes behind them differ per
capture:

| capture | first record, bytes 2..19 | second record, bytes 2..19 |
|---|---|---|
| `game1` | `00 02 00 00 00 01 02 42 f5 9f da ff c3 ec 7d 00 00 00` | `00 02 01 00 00 00 02 f4 01 c0 ea ff 83 af 02 00 00 00` |
| `joined-when-red-team` | the same two bodies, under classes `0x50`/`0x51` and `0x89`/`0x8a` | |
| `game2` | `00 02 00 00 00 01 02 78 ec 1f 12 fd a3 0f 00 00 00 00` | the same body twice |
| `game3` | `00 02 00 00 00 00 02 e6 92 5f 19 01 58 26 7f 00 00 00` | `00 02 01 00 00 01 02 54 6f 80 38 01 b0 36 00 00 00 00` |
| `started-on-briefing` | `00 02 00 00 00 00 02 28 a6 1f 80 02 e8 1f 02 00 00 00` | `00 02 01 00 00 01 02 e2 e6 1f f1 ff 03 0c 7e 00 00 00` |
| `started-team-selection` | `00 02 00 00 00 00 02 14 5c 1e 00 00 70 2f 7c 00 00 00` | `00 02 01 00 00 01 02 a4 c1 1e 00 00 b0 a1 03 00 00 00` |

The shape is shared — `00 02`, a byte that is `00` on the first record and `01`
on the second, two bytes, then `02`, then **two 4-byte values**, then a tail — and
the values are not. `game1`'s and `joined-when-red-team`'s are identical, which is
the one pair of captures taken by the same client, and every other host writes
its own. So these bytes are read as two coordinates or two slots of the room, and
a host that replays `game1`'s writes another room's. What they name needs the
handler that reads them, which is §3's open route. **[V]** the shape and the
variation; **[U]** the meaning.

### 5.4 The state-blob identifier carries a family, and `0x40` is one member

`MatchStateBodiesUtils` models `0x0002` as one 22-byte body with a `0x40` marker at
offset 18 and a big-endian `u16` at 20. Over the six captures the identifier
appears under **nineteen** fourth-byte values — **every value from `0x01` to
`0x12`, and `0x15`** — and the trailing `u16` is not one
number per phase: `game1` carries `0x0032`, `0x0045`, `0x005d`, `0x0062` and
`0x0063` beside the sparse 100, and `joined-when-red-team` walks `0x000a` through
`0x0064` in fourteen shapes. The sparse 100 under class `0x01` and the steady zero
under `0x15` are two of them and this host writes those two. **[V]**

### 5.5 The join state answer's trailing bytes move, and only the briefings write
the set

The row in §2 said the first byte is settled and the trailing three are not.
Reading all six captures, **only the two briefings write the record as
`[set, 0, 0, 0]`**: `game2` answers `02 00 00 00`, and `started-on-briefing`
`02`, `06`, `0e`, `1e` then `16`, the growth §1.5 derives the set from. The rest
do not: `game1`'s host answers `00 80 10 00`, `00 88 10 00`, `10 80 00 00` and
`40 08 10 00`, and `started-team-selection` answers `01 00 02 00`. So an earlier
claim that the trailing three bytes had never moved in any capture is withdrawn —
the second and third bytes move in three of the six — and the derivation in §1.5
holds of the two captures that carry a briefing, which is what it was measured
from. **[V]**

### 5.6 What the replay console already names, and where it runs out

`docs/analysis/replay-console/rpdt.js` is the other reading of this channel, and
it is worth saying which of §5's questions it closes and which it does not.

**It names the tick records' classes**, which the fourth byte of a record below
`0x1000` carries (`rpdt.js:98`): `0x00` damage, `0x01` state, `0x02` position,
`0x03` health, `0x04` weapon, `0x05` aim, `0x06` incapacitated, `0x07` roster,
`0x08` throw, `0x0b` settings — the last being the body `UDP_SETTINGS_PAYLOAD.md`
reads. So `0x090c`'s class `0x03` is confirmed health, and `0x0a61`'s `0x00`/`0x01`
are damage and state, which is what `MatchRecordFramesUtils` says they are.

**It gives the roster payload's field offsets** (`rpdt.js:555`,
`rosterPlayer`): join counter at `+4`, slot at `+5`, character id as a
little-endian `u32` at `+8`, clan id at `+31` (or `+19` in the 12-bytes-shorter
layout a late-2026-10 build writes), the two names behind `RosterNameOffset`.
Applied to the close **triple**'s two 20-byte records — which are below the
console's `ROSTER_MIN_LEN` of 58, so it would refuse them — those offsets read as:
byte `+4` a join counter that is `0` on the first record and `1` on the second in
five of the six captures (`game2` writes one body twice and never distinguishes
them), byte `+5` a slot that is `0` in all six, and bytes `+8..+12` a
little-endian `u32` that differs per capture. That is a **lead and not a reading**:
the record is too short to be the roster entry those offsets belong to, so what
carries them here is the same `0x07` type byte and the same length byte, nothing
more. **[I]**

**It confirms the sibling bit and the ops family.** `OPS_VID_LOW = 0x254`
(`rpdt.js:243`) with `RULES_VIDS = [0x261, 0xa61]`: the low twelve bits of an
operations record are `0x254`, the `0xa5c` and `0xa61` forms are that with the
`0x800` sibling bit set, and the join state record measured in §5.5 sits at
`0x25c`/`0x0a5c` — `0x254 + 8`. So the run's identifier set is the same family
`MatchRecordGenerationUtils` models, and the sibling bit is the console's own
reading rather than this host's convention. **[V]**

**It does not name `0x0f`.** The console's operations opcodes are five —
`OP_STATE 0x00`, `OP_CLOCK 0x01`, `OP_WRITE 0x02`, `OP_KILL 0x09`,
`OP_RATING 0x13` (`rpdt.js:245`) — and the two-byte record the match-start run
carries is not among them, so the byte stays the recorded one exactly as
`MatchRecordBodiesUtils.OperationsShort` says. **[V]** that it is unnamed on both
sides.

### 5.7 The sequence counter is one per twelve-bit id, not one per record type

§5.1 read the fourth byte as the record's *stream* sequence. Read against each
other, the six captures say something sharper: **the counter belongs to the
record's low twelve bits, and the class bits do not get counters of their own.**
The evidence is the *shape of the gaps* in the types that share an id. In
`started-on-briefing` the `0x1001` stream writes 340 records, and the `0x5001`
stream — the same id `1` with the `0x4000` bit — writes 12, whose values are
`00 00 02 0c 0d 1a …`: sparse, and landing inside the range the `0x1001` stream
is walking at the same time. A counter of its own would number them `01 02 03 …`.
`mgo2-game1` is where the rule is visible frame by frame. At t+44.747 `0x9001`
takes `0x07` and the four `0x1001` records take `0x08 09 0a 0b`; at t+5.019
`0xd001` takes `0x01`, `0x9001` `0x03` and the three `0x1001` `0x04 05 06`; at
t+188.002 `0x9001` takes `0x1e` on one `0x1001` `0x1f`; at t+192.374 `0x9001`
takes `0x22` and the five `0x1001` take `0x23 24 25 26 27`. The flow's whole id-
`1` family — `0x1001`, `0x5001`, `0x9001`, `0xd001`, one id with the class bits
on — is 576 records whose fourth bytes take 193 distinct values spanning `00`–`c9`
with nine holes in `01`–`c9` (`20 aa ab ae af b0 b1 b2 b3`) and whose 575 steps
ascend 475 times. Four private counters would number each stream `00 01 02 …`.
Counted per unique frame (the capture re-sends; raw figures are `0x1001` 463,
`0x5001` 66, `0x9001` 71, `0xd001` 178): 352, 60, 48, 116. **[V]**

That rule is what `RoomRosterService`'s ordinals already implement for id `1` —
`TakeRunOrdinals(5)` hands five consecutive values to one `0x9001` and four
`0x1001` records — and it is **not** what the rest of the writer does. The beat
numbers a `0x1a54` clock and a `0xd001` control from one counter
(`BeatCounters.SessionFlags2`, opened on `0x07` — a value borrowed from the run's
*team array*, which is id `1`), and the sparse phase numbers its `0xd001` controls
`0x02`–`0x09` from literals, so the `0xd001` stream restarts when the beat begins
and the `0x1a54` stream inherits a value from another id. Two counters per id is
what a correct writer needs: one for `id 0xa54` and one for `id 0xd001`. **[D]**
the rule from five streams; **[U]** nothing about it.

### 5.8 The sparse phase's cadence is a ~2.25 s tick, and its blob placement is
not derivable

Pooled over the six captures — every gap between two consecutive `0xd001` or
`0x0002` records before the marker, 246 of them — the delays are not free-running
and they are not a period either:

```
1.75s:11  2.00s:25  2.25s:61  2.50s:52  2.75s:4      <- 153 of 246
4.00s: 9  4.25s: 9  4.50s:16  4.75s:15  5.00s:12     <- 61 of 246, ~2x
6.25s: 2  6.50s: 1  6.75s: 3  7.00s: 3  7.25s: 2  7.50s: 1  9.25s: 2
```

So the host writes on **one tick of about 2.25 s** — 18 of the stream's own
125 ms units — and a gap is one tick or a small multiple of it, with per-host
jitter. `2.25 s × 18 = 40.5 s`, and the phase's own 42 240 ms is close to it
without being it.

**What cannot be derived is what each tick carries.** In `game1` a blob rides
7 of 11 ticks, in `game3` 9 of 13, and the ticks with no blob have no measured
property in common — the placement is the host's own decision, not a rule the
captures hold. A phase generated from the tick alone would therefore write *a*
sparse phase and not any capture's, so `SparsePhaseFramesUtils`'s step list stays
the recorded one and this measurement's use is to say what its delays are: ticks
of a ~2.25 s clock, not arbitrary waits. **[V]** the tick and the blob counts;
**[U]** the placement.

### 5.9 The close pair's two 32-bit values are not character ids and not a room's

§5.3 asked what the two 4-byte values in the close records are. Three candidates
can now be refused or kept, against the captures' own roster entries:

* **Not a character id.** Every profile-shaped record in each capture yields its
owner id (`1 001`-family record, `body[8..12]` little-endian, the offset
`PlayerProfileRecordUtility` reads). Across the six captures that is 30 ids, from
`0x275` to `0x1232f` and the `0x7f00000x` block — and **no close record's value
is among them**, in either the `+8` or the `+12` slot. Repeated with both
endiannesses; the answer is the same. **[V]**
* **Not a constant of the room either.** One capture writes several of them:
`game1` four (under four different class pairs — one per round), the red-team
capture six, the two briefings two each. So they are written per round and not
once per room.
* **They recur with the room that wrote them.** `game1`'s `0x9FF54202`/`0xECC3FFDA`
and `0xC001F402`/`0xAF83FFEA` appear **verbatim** in the red-team capture, and
the other two of its four in `game3`. The three captures share a map and rule;
the two that do not share it write values that appear in neither of the others.
So the values are per-round state of the *room's own content* — a spawn, an
objective, an entity — and not a number about a player. **[V]** the recurrence;
**[I]** the reading.

What would name them is still §3's handler, and this narrows the search: it is
the code that reads a 20-byte `0x07` record with two 4-byte fields at `+8` and
`+12`, not the roster reader `rpdt.js:555` documents.

### 5.10 What is left, and what would settle it

The rows of §2 now split three ways:

* **A rule, buildable.** The fourth byte of a session record (§5.1).
* **One room's data, not buildable.** The close triple's remainders (§5.3), the
  join state answer's other hosts' bytes (§5.5), and the sparse phase's bodies and
  length (§5.2) — each of these is a value the room supplies and no capture can
  give a host that is not that room.
* **A family, partly named.** The state blob (§5.4).

What remains genuinely resolvable is §3's route to a handler — a record's body
layout is readable only where the code that reads it is — and everything in the
middle list above is waiting on exactly that.

---

## 6. What §5.7, §5.8 and the briefing flow changed in the server

Three of the findings above were buildable rather than only reportable, and this
section is what they changed. Everything else in §5 is still waiting on §3's
handler or on a live client, and touches nothing here.

**The fourth byte is now one counter per twelve-bit identifier (§5.7).**
`MatchRecordSequenceService` holds them: `Take` hands out an identifier's next
value, `Restart` opens a stream at zero where the recorded host opens one of its
own, and `Seed` opens a stream that nothing has taken from. What it replaced is
the defect the section names — the beat numbered an `0x1a54` clock and an
`0xd001` control from **one** counter (`BeatCounters.SessionFlags2`, opened on
`0x07`, a value borrowed from the run's *team array*), and the roster exchange
counted in a field of its own, so the two could not interleave the way the
capture's do. Now:

* the room's exchange (id `0x001`) is one sequence from the join on — the run, the
  burst, the lobby-slot pairs, the scoreboard's deltas and **both** the sparse
  phase's and the beat's `0xd001` controls (pinned literals in
  `SparsePhaseFramesUtils` were left alone: they read `01…09` in a window where
  the exchange is at 3+, so replacing them would be an unverifiable deviation);
* a round's operations stream is restarted where the round opens — `OpenRound`
  does it — so the run's three operations records take `00 01 02` and the beat's
  clocks go on from `03`, which is game1's own run and beat;
* the item-box group keeps its per-identifier counters inside the round, which is
  the numbering §5.4 measured and the client answers against.

**The briefing countdown is a fixed sixteen seconds (§5.8's own measurement).**
`BriefingCountdownBodiesUtils.CountdownUnits` no longer spreads 128 units across
the room's briefing time: it drops 32 units every four seconds and holds at zero
from sixteen seconds in, which is the capture's `128 → 96 → 64 → 32 → 0` over
16.2 s and the 45 s of zeros that follow it before its round opens. The record is
taken from the operations stream rather than numbered per peer, and the stream is
restarted where a briefing begins, so the capture's clock starts at `00`.

**The ready set is repeated while a briefing waits.** `BriefingReadyPublishService`
writes the room's reporting players to each peer every 2.5 s until the round
opens — the same records `JoinReportHandler` answers a report with, under the
same generation — which is the repeat the captures show between reports
(`56.123`/`60.383`, `112.619`/`114.978`, `144.750`/`147.306`,
`154.370`/`156.885` in `started-on-briefing`). Before it, a client whose report
had been heard and whose room still waited on somebody else was told nothing.
Both briefing writers now sleep on `MatchRoundStateService.WaitBriefingTickAsync`
rather than on a plain delay: the beat is awaited behind them, so a period slept
out after the round had opened would have held the round's first frame — and the
item-box group and vitals onset counted from it — behind a timer nothing waits on.

**Verified.** `dotnet build Mgo2Server.slnx` clean (0 warnings, 0 errors) and
`dotnet test tests/Mgo2Server.Tests` 54/54 after the last edit. The two new units
were also run directly against the captures' own numbers — the countdown's five
readings and the stream keying (`0x1001`/`0x5001`/`0x9001`/`0xd001` one stream,
`0x1a54`/`0x9a54` another, `0x1254` a third) — in a scratch harness outside the
repository, because the suite is the codecs alone and neither of these is one.
**Not verified end to end:** no client was run against the host, so what a live
client makes of a repeated ready set and of a sixteen-second countdown is **[U]**,
as §5.8 said it was.