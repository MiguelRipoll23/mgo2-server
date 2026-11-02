# The `0xd001` control byte: what it does, and every value it takes

**Measured 2 November 2026.** The one byte that is the whole body of a record-level
answer — the `0xd001` family, `0x9001 | 0x4000` and `0x5001 | 0x8000` — read from
every capture the repository holds: the fifteen `.pcapngs/*.pcapng` sessions plus
`tools/_bts.pcapng`, a kept copy of the `briefing-then-solo-game` recording. Sixteen
sessions, 1 334 host answers and 46 client answers, 1 380 in all.

The other half of the measurement is the client image, `.game/MGO2.ELF` (PPC64 BE,
disassembled with capstone), with `savemgo.dll`, `system.dll` and `mgo2.exe` of the
native port checked for the same sites. Tools: `tools/udp_control_value_inventory.py`
(counts, and `--ladder` for the screen step), `tools/udp_start_run.py` for the run's
record list. The host side of this server is
`src/GameplayServer/PeerAcknowledgementUtils.cs` (the record) and
`src/GameplayServer/Rooms/PeerControlByteService.cs` (the value).

The corpus is very unevenly sized, and the two extremes are the most informative:

| | capture | why it matters |
| --- | --- | --- |
| longest session | `mgo2-started-on-game-round1` (18.3 MB, 1 378 s) | 168 answers with **a different value on nearly every one** |
| most answers | `mgo2-game-joined-when-red-team-then-briefing` (8.9 MB, 927 s) | 196 answers, **every one a multiple of four** |

An earlier reading of this file, taken from `game1` and `game3` alone, described a
three-rung ladder. Those two sessions are real, but they are one habit among three,
and the two largest captures do not follow it. That is the correction §5 records.

## 0. What the record is

An answer is the answering side's copy of the record with bits `0x4000` set, the same
fourth byte, and a body whose length follows the record's `0x8000` bit: one byte when
the record carries it, none when it does not. Both peers do this, so the same
`0xd001` identity appears in both directions and carries each side's own byte.

## 1. Where the byte goes

The record's handler in the client is `0x26DB78`. It reads one byte (`0x26DBA4`),
takes state `0x00`'s block (`0x26DBB8 → 0x27E2B0`), writes the byte at **offset 99**
of it (`0x26DBD0 → 0x27E578`, `(block, 99, 1, &byte)`), and sets `+0xBCC |= 8`.
Offset 99 is field 24 of that state's 55: one byte, flags `0x0000`, so the client
stores it and publishes nothing. The descriptor-table copy at `0x51B300` moves the
whole field set, which is how the value survives a state transfer.

## 2. What the byte does

Two functions read that field and treat it identically — `0x77E6A0` and `0x77F878`.
The first is reproduced in full; the second is the same with `r28`/`r29` swapped and
the event built in the same instruction pair:

```
0x77E6A8: lwz    r0, 0xb8(r29)      ; the screen's step
0x77E6AC: cmpwi  cr7, r0, 9
0x77E6B4: bgt    cr7, 0x77E6F8      ; step > 9: return, do nothing
0x77E6B8: lbz    r5, 0xd0(r29)      ; the byte, copied in from field 24
0x77E6BC: cmpwi  cr6, r0, 0xc
0x77E6C0: cmpwi  cr7, r5, 0xff
0x77E6C4: beq    cr7, 0x77E6F8      ; 0xff: return, do nothing
0x77E6C8: lis    r4, 0x16
0x77E6D0: ori    r4, r4, 0x2c       ; script event 0x16002C
0x77E6D4: beq    cr6, 0x77E6F8      ; already at step 12: return
0x77E6E0: stw    r0(0), 0xbc(r29)   ; the pending word cleared
0x77E6E8: stb    r9(1), 0xc4(r29)   ; the "advanced" flag set
0x77E6EC: stw    r0(0xc), 0xb8(r29) ; step = 12
0x77E6F0: bl     0x7770f0           ; raise 0x16002C with the byte
```

So the byte is a **one-shot "advance this screen" token**:

* the only value it is compared with is `0xff`, the declared no-value;
* a screen at step **9 or below** jumps to step **12**, sets `+0xc4 = 1`, clears
  `+0xbc`, and the byte is handed to the runtime listener list through `0x7770F0` as
  the argument of script event `0x16002C`;
* a screen at step **10 or 11** is not moved at all — the `bgt` on 9 returns before
  the advance, so those two steps are unreachable from here;
* a screen already at **12** returns after the flag test, so the event is not raised
  a second time.

Nothing else in the image compares the byte to anything, so which of its values
arrives is a host's choice. That is why a host may pick per phase, per pass or per
answer and still move the same screen — only `0xff` says "nothing" and leaves a
screen where it is.

## 3. Every value the captures hold

Host side, one-byte bodies, all sixteen sessions:

| | records | distinct | codes (`v & 0x1f`) | flag bits (`v & 0xe0`) |
| --- | ---: | ---: | --- | --- |
| host | 1 334 | 51 | 26 of 32 | `20`×41 `40`×7 `60`×4 `80`×3 `a0`×3 `c0`×1 |
| client | 46 | 23 | 18 of 32 | `20`×17 `40`×2 `e0`×1 |
| both | 1 380 | 60 | 30 of 32 | — |

Codes never seen in either direction: `0x17`, `0x1e`; from a host alone also `0x19`,
`0x1b`, `0x1d`, `0x1f`. The whole alphabet of the two directions:

```
00 01 02 03 04 05 06 07 08 09 0a 0b 0c 0d 0e 0f 10 11 12 13 14 15 16 18 1c 1d
20 22 23 24 25 26 27 28 2a 2c 2d 2e 34 39 3a 3b 45 46 47 48 49 58 60 69 78 7c
84 8c 9a a4 b4 bc c8 ff
```

## 4. The corpus, session by session

Host answers, the values it used and how long the session ran. The last column is
how many of its values are multiples of four, which is the split §5 needs.

| capture | size | span | host | client | values | `v & 3 == 0` |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| `mgo2-started-on-game-round1` | 18.3 MB | 1 378 s | 168 | 0 | `01`–`11`, `60` | 26% |
| `mgo2-game-joined-when-red-team-then-briefing` | 8.9 MB | 927 s | 196 | 0 | `00 04 08 0c 10 14 1c 20 2c 34 58 78 7c 84 8c a4 b4 bc c8` | **100%** |
| `mgo2-game1` | 3.7 MB | 798 s | 178 | 5 | `02 03 04 05 06 08 09 10 24 25 26 27 28 49 69` | 36% |
| `joined-final-round-solo…-other-player-joined` | 3.0 MB | 674 s | 196 | 4 | `02 03 04 05 06 08 24 25` | 75% |
| `mgo2-joined-game-in-progress-last-round` | 1.6 MB | 390 s | 86 | 15 | `03 04 05 06 07 08 09 27 28 2a 46 48` | 26% |
| `briefing-then-game` | 1.0 MB | 151 s | 32 | 0 | `01 02 05 06 07 08 09 0a 0b 0e 0f 10 11` | 16% |
| `mgo2-started-on-solo-game` | 0.9 MB | 189 s | 43 | 2 | `01 02 03 12 13 14 24 34` | 21% |
| `mgo2-started-briefing-then-solo-game` | 0.7 MB | 271 s | 81 | 2 | `02 03 04 06 45` | 32% |
| `mgo2-game-started-team-selection-selected-blue…` | 0.7 MB | 240 s | 68 | 1 | `02 03 04 05 12 13 14 15 16 34` | 37% |
| `mgo2-game-started-on-briefing` | 0.6 MB | 282 s | 71 | 0 | `01`–`10`, `12`, `9a` | 24% |
| `mgo2-game3` | 0.6 MB | 205 s | 57 | 2 | `03 04 07 24` | 56% |
| `mgo2-game2` | 0.5 MB | 317 s | 86 | 0 | `01`–`11`, `13` | 19% |
| `joined-on-briefing-then-game` | 0.5 MB | 103 s | 24 | 11 | `01 03 04 05 11 12 13 14 15 18 23` | 33% |
| `mgo2-game-failed-memoryviolation` | 0.5 MB | 89 s | 20 | 0 | `00 04 08 0c 10` | **100%** |
| `joined-on-game-solo` | 0.2 MB | 46 s | 11 | 4 | `02 03 11 12 13` | 0% |
| `tools/_bts.pcapng` | 0.2 MB | 198 s | 17 | 0 | `02 03 05 06 07 08 0a 0c 0d 0e 0f 10` | 29% |

Pooled, 635 of the 1 334 host answers (47.6%) are multiples of four and 699 are not,
so no single family covers the corpus and no reading of one session generalises.

## 5. The three habits

**One: a phase ladder, which is what `game1`, `game3` and
`started-briefing-then-solo-game` write.** `game3` is the cleanest — 59 answers:

| t+ | direction | byte | screen |
| --- | --- | --- | --- |
| 4.383 | client → host | `22` | 0 → 12, advanced |
| 8.595 | host → client | `24`×2 | 0 → 12, advanced |
| 10.967 | host → client | `03`×11 | 12, no change |
| 59.596 | host → client | `24` | 12, no change |
| 64.609 | host → client | `03`×13 | 12, no change |
| 118.218 | host → client | `07`, then `04`×29 | 12, no change |

`game1` alternates `02` and `03` through its first briefing, moves to `04` when its
round opens at t+278.1, and writes `25`–`28` later in that round — `0x24` is
`0x04 | 0x20`, so the joining pass and the round's later answers carry the round's
code under the same bit. This is the habit `PeerControlByteService` follows: brief in
`03`, open the round in `04`, flag it from the second pass, join in `24`.

**Two: a ring of eight indices under three flag bits, which is what the
196-answer session writes.** `mgo2-game-joined-when-red-team-then-briefing` never
writes a value that is not a multiple of four, and its codes are exactly
`00 04 08 0c 10 14 18 1c` — a three-bit index in bits 2–4, with `0x20`, `0x40` and
`0x60` used as flags (`58` = `18|40`, `78` = `18|60`, `8c` = `0c|80`, `a4` = `04|a0`,
`bc` = `1c|a0`, `c8` = `08|c0`). The index is walked irregularly, about one answer
every 2–7 s:

```
t+11.67  08     t+21.32  10     t+36.91  08     t+46.44  00
t+14.18  14     t+27.92  10     t+44.39  0c     t+46.44  04
t+18.81  04     t+30.04  10                       t+53.17  08
t+18.81  04     t+32.41  00                       t+57.86  08
```

`mgo2-game-failed-memoryviolation` is the same habit at five indices (`00 04 08 0c
10`, all twenty answers) — the session named for the client's memory fault, whose
byte was not varying when it happened.

**Three: a value that changes with nearly every answer, which is what the longest
session writes.** `mgo2-started-on-game-round1` (1 378 s, 168 answers) cycles the
small codes at one answer every five seconds:

```
t+ 3.29  05    t+31.49  0a    t+56.56  02    t+81.66  07    t+106.78 08
t+ 6.37  04    t+36.51  02    t+61.63  0c    t+86.67  0d    t+111.80 0a
t+10.27  11    t+41.57  08    t+66.65  03    t+96.71  07    t+116.83 0d
t+11.42  03    t+46.57  11    t+71.66  0f
t+16.42  10    t+51.63  60    t+76.62  0e
```

There is no phase in that sequence: the value changes while the room sits in one
phase for minutes. Whatever the host is expressing there, it is not the room's state
— a per-pass or per-answer token, `60` being the one flagged value in the session.

## 6. `0xff`

**Never from a host** — 0 of 1 334 host answers, in any session. It appears exactly
once in the whole corpus, and from the client side: `mgo2-game1`, t+761.809, answer
171 of 183, with the session continuing normally afterwards (host `04` at t+763.417,
`69` at t+765.794, `05` at t+770.418). So `0xff` is a legal value in both directions
and the client uses it as the no-value the reader treats it as.

What a screen at step 9 does with `0xff`: **nothing**. The `beq` on `0xff` returns
before the three field writes and before the event, and there is no timer or retry in
that function — the screen stays at 9 until a later answer carries a non-`0xff` byte,
which then advances it to 12 (9 is still at or below 9). It is the one way a client
can be left waiting on a record it has already received.

## 7. What the ladder cannot say, and what is left open

`--ladder` tracks one screen per side, from a step of 0, because no frame carries the
client's screen state. Its value is in the shape, not the count: **no host in the
corpus ever writes `0xff`, so the first host answer of every session moves the
receiving screen to 12 and every answer after it lands at 12**, which makes the token
spent once per screen and the value of the later answers unobservable. A client that
opens a new screen mid-session would start below 9 again and nothing on the wire marks
that moment, so the model over-counts "no change" after it.

| question | state |
| --- | --- |
| what the code bits 1–31 name | not in the image; nothing compares them |
| why some hosts use a ring of four-strided indices and others small codes | not in the image; two sessions are wholly four-strided, the other fourteen mix |
| what the flag bits `0x20`, `0x40`, `0x60`, `0x80`, `0xa0`, `0xc0` mean | unused by the reader, which tests only `0xff` |
| why `started-on-game-round1` changes the byte every five seconds | measured, unexplained |
| which listener consumes `0x16002C` | resolved at runtime through `0x7770F0`; no static binding |
| screens at step 10 or 11 | never advanced by this path; nothing in the image sets them |
