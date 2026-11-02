# The `0xd001` control byte: what it does, and every value it takes

**Measured 2 November 2026.** The one byte that is the whole body of a
record-level answer — the `0xd001` family, `0x9001 | 0x4000` and
`0x5001 | 0x8000` — read from the sixteen captures in `.pcapngs` (plus
`tools/_bts.pcapng`) and from the client both ways: the byte's handler and its
two readers in `.game/MGO2.ELF` (PPC64 BE, disassembled with capstone), and the
native port's `savemgo.dll`, `system.dll` and `mgo2.exe` for the same sites.

Tools: `tools/udp_control_value_inventory.py` (counts, and `--ladder` for the
screen step), `tools/udp_start_run.py` for the run's record list. The host side
of this server is `src/GameplayServer/PeerAcknowledgementUtils.cs` (the record)
and `src/GameplayServer/Rooms/PeerControlByteService.cs` (the value).

## 0. What the record is

An answer is the answering side's copy of the record with bits `0x4000` set, the
same fourth byte, and a body whose length follows the record's `0x8000` bit: one
byte when the record carries it, none when it does not. Both peers do this, so
the same `0xd001` identity appears in both directions and carries each side's own
byte.

## 1. Where the byte goes

The record's handler in the client is `0x26DB78`. It reads one byte
(`0x26DBA4`), takes state `0x00`'s block (`0x26DBB8 → 0x27E2B0`), writes the byte
at **offset 99** of it (`0x26DBD0 → 0x27E578`, `(block, 99, 1, &byte)`), and sets
`+0xBCC |= 8`. Offset 99 is field 24 of that state's 55: one byte, flags
`0x0000`, so the client stores it and publishes nothing. A descriptor-table copy
at `0x51B300` moves the whole field set, which is how the value survives a state
transfer.

## 2. What the byte does

Two functions read that field and treat it identically — `0x77E6A0` and
`0x77F878`. The first is reproduced here in full; the second is the same with
`r28`/`r29` swapped and the event built in the same instruction pair:

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
0x77E6F0: bl     0x7770F0           ; raise 0x16002C with the byte
```

So the byte is a **one-shot "advance this screen" token**:

* the only value it is compared with is `0xff`, the declared no-value;
* a screen at step **9 or below** jumps to step **12**, sets `+0xc4 = 1`, clears
  `+0xbc`, and the byte is handed to the runtime listener list through
  `0x7770F0` as the argument of script event `0x16002C`;
* a screen at step **10 or 11** is not moved at all — the `bgt` on 9 returns
  before the advance, so those two steps are unreachable from here;
* a screen already at **12** returns after the flag test, so the event is not
  raised a second time.

Nothing else in the image compares the byte to anything, so which of its values
arrives is a host's choice. That is why a host may pick per phase and why no
client can be broken by a value it does not expect — only by `0xff`, which says
"nothing" and would leave a screen where it is.

## 3. Every value the captures hold

Host side, one-byte bodies, all sixteen captures plus `_bts.pcapng`:

| | records | distinct | codes (`v & 0x1f`) | flag bits (`v & 0xe0`) |
| --- | ---: | ---: | --- | --- |
| host | 1 334 | 51 | 26 of 32 | `20`×41 `40`×7 `60`×4 `80`×3 `a0`×3 `c0`×1 |
| client | 46 | 23 | 18 of 32 | `20`×17 `40`×2 `e0`×1 |
| both | 1 380 | 60 | 30 of 32 | — |

Codes never seen in either direction: `0x17`, `0x1e`; from a host alone also
`0x19`, `0x1b`, `0x1d`, `0x1f`. The whole alphabet of the two directions runs
`00 01 02 03 04 05 06 07 08 09 0a 0b 0c 0d 0e 0f 10 11 12 13 14 15 16 18 1c 1d
20 22 23 24 25 26 27 28 2a 2c 2d 2e 34 39 3a 3b 45 46 47 48 49 58 60 69 78 7c
84 8c 9a a4 b4 bc c8 ff`.

## 4. The ladder the two full sessions show

`--ladder` prints each answer against the receiving screen's step, which is the
only reading that says anything: the byte is written by the answering side and
consumed by the peer that receives it.

`mgo2-game1` (183 answers):

| t+ | direction | byte | screen |
| --- | --- | --- | --- |
| 4.226 | client → host | `03`×2 | 0 → 12, advanced |
| 6.687 | host → client | `24`×1 | 0 → 12, advanced |
| 6.688 … 63.546 | host → client | `02`, `03`, `04` … | 12 → 12, no change |
| 25.351 → 761.809 | both | `04`, `05`, `26`, `69`, `ff` … | 12 → 12, no change |

`mgo2-game3` (59 answers): client `22` at t+4.383, host `24`×2 at t+8.595, host
`03`×11 from t+10.967 (its briefing), host `24` at t+59.596, `03`×13 from
t+64.609, `07` at t+118.218 and `04`×29 behind it (its round). Every host value
after the first lands on a screen already at 12.

The per-phase values, as the hosts pick them:

| phase | bytes | captures |
| --- | --- | --- |
| joining pass | `24` (`game1`, `game3`, `started-briefing-then-solo`), `45`, `06` (`briefing-then-solo-game`) | per-host choice |
| briefing | `03` flat (`game3`, 11 then 13 answers), `02`/`03` alternating (`game1`) | the room waiting on players |
| round | `04` on the first pass, then `04|0x20` (`25`–`28`) as the round runs | `game1` from t+278.1, `game3` from t+415.4 |
| round turning over | `07`, `10` where a round closes and the next opens | `game1`, `game3` |

`0x24` is `0x04 | 0x20`: the join value is the round's code flagged, which is why
the ladder can be written as three rungs and one flag
(`PeerControlByteService`).

## 5. `0xff`

**Never from a host** — 0 of 1 334 host answers. It appears exactly once in the
corpus, and from the client side: `mgo2-game1`, t+761.809, answer 171 of 183,
with the session continuing normally afterwards (host `04` at t+763.417, `69` at
t+765.794, `05` at t+770.418). So `0xff` is a legal value in both directions and
the client uses it as the no-value the reader treats it as.

What a screen at step 9 does with `0xff`: **nothing**. The `beq` on `0xff`
returns before the three field writes and before the event, and there is no timer
or retry in that function — the screen stays at 9 until a later answer carries a
non-`0xff` byte, which then advances it to 12 (9 is still at or below 9). It is
the one way a client can be left waiting on a record it has already received.

## 6. What is not settled

| question | state |
| --- | --- |
| what the code bits 1–31 name | not in the image; nothing compares them |
| what the flag bits `0x20`, `0x40`, `0x60`, `0x80`, `0xa0`, `0xc0` mean | unused by the reader, which tests only `0xff` |
| the low nibble climbing `0x25`→`0x28` in `game1` | reads as a counter inside the code; not reproduced by this host |
| which listener consumes `0x16002C` | resolved at runtime through `0x7770F0`; no static binding |
| screens at step 10 or 11 | never advanced by this path; nothing in the image sets them |
