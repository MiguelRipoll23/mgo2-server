# The joiner ↔ host UDP flow, step by step, against `MGO2.ELF`

`mgo2.pcapng` (byte-identical to `docs/mgo2-game.pcapng`) is one complete game on a real
dedicated server. `UDP_GAME_CAPTURE.md` reads that channel off the wire; this file walks
the opening datagrams **in order and ties each one to the instruction that produces or
consumes it**. Where the two disagree the binary wins, and the disagreement is recorded
here rather than quietly overwritten.

Confidence tags match the rest of `docs/protocol/`: **[V]** verified against the bytes and
the disassembly, **[I]** inferred, **[U]** unresolved.

Addresses are BLUS30109 `MGO2.ELF`, PPC64 ELFv1 big-endian. They were read with
`tools/mgo2_disasm.py` (Capstone). There is no decompiler in this project's toolchain, so
every claim below is "this instruction does this", never "this function is called X".

---

## 0. How the rotate masks read in this image

Several instructions that decide *which* variant of something gets sent are `rlwinm` /
`clrlwi` rotate-and-mask forms, and the flag bits they test are the whole point of those
branches. The rule that makes them readable is:

> **the mask covers bits `31-MB` .. `31-ME`**, not `MB` .. `ME`.

Read literally the other way round, every one of them is dead code — a mask of bit 20 or
29 against a zero-extended `lhz` can never be set, so the branches would never fall
through, and the join would never leave the handshake state. Nine sites fix the rule, and
five of them land on a value this project had already asserted from entirely separate
evidence:

| site | raw `MB`/`ME` | mask | what it has to be, and why |
| --- | --- | --- | --- |
| `0x268c44` state-8 gate | 30 / 31 | `0x3` | both role bits — §6 already said so |
| `0x268f5c` guards `flags \|= 4` | 29 / 29 | `0x4` | guards the very bit it then sets, so it runs once |
| `0x267000`, `0x26693c` digest key | 28 / 28 | `0x8` | "session key established" — §6 already said so |
| `0x269018` accept gate 3, flags | 20 / 20 | `0x800` | §4's original reading of gate 3 |
| `0x269008` accept gate 3, capability | 29 / 29 | `0x4` | symmetric with the builder below |

The last two are the same gate reading two different words, and they agree with each other:
the builder sets the capability's bit 2 exactly when the session's `0x800` is set, and
gate 3 rejects exactly when the peer's bit 2 is clear *and* ours is set. Neither reading
was fitted to the other.

**Why this is worth stating as a rule rather than a workaround.** The complements line up
at five independent sites across two unrelated code paths, so this is a property of the
image and not a coincidence I tuned until it fitted. The *reason* is not established — the
compiler plainly treats these 16-bit flag words as living in a narrower domain than the
32-bit registers they are held in, and a mask expressed in that domain comes out
complemented. `tools/mgo2_disasm.py` prints Capstone's operands verbatim, which is correct;
the conversion has to happen afterwards.

Readings that do not depend on any of this are stated as such: the comparisons (`cmpw`),
explicit loads and stores, `ori`/`xoris`/`xori` constant construction, and `li`/`ori`
literals.

---

## 1. Which flow is the joiner↔host flow

| | |
| --- | --- |
| joiner | `10.2.0.2:5730`, `peer_id` **74543** (0x1232f), counter_base **0x736773a6** |
| host | `99.66.131.177:5731`, `peer_id` **40961** (0xa001), counter_base **0x3c8a65d6** |
| session key | **K = 0x4fed1670** = `0x736773a6 ^ 0x3c8a65d6` |
| volume | 19 021 frames, 5 111 LZSS, ~13 minutes |

The capture holds four peer flows. Only this one establishes. The other three
(`98.26.168.27`, `89.152.226.41`, `92.191.178.103` — 19, 771 and 7 datagrams) carry
handshakes that are never answered, and `tools/udp_flow_summary.py` reports
`no session key` for every frame in them. They are not part of this flow. **[V]**

**`peer_id = 0xa001` on the host side is the dedicated host's character id**, and it is
independently visible in the TCP lobby channel of the same session (`docs/protocol/`
records it at `0x4313` beside the name `Low Level Host`). The UDP handshake therefore
agrees with the TCP record without either being used to derive the other. **[V]**

---

## 2. The nine opening datagrams

Offsets are relative to the datagram start, after the §5 header unscramble and XOR chain.

```
[ 0.000] J->S   44 B  hdr 0x0000   handshake
[ 1.668] S->J   16 B  hdr 0x0000   0x5000 len 0        (keep-alive, sent FIRST)
[ 1.668] S->J   44 B  hdr 0x0001   handshake
[ 1.694] J->S   92 B  hdr 0x8001   0x5000 len 0, 0x1001 len 140
[ 3.628] J->S   88 B  hdr 0x8002   0x1001 len 140      (identical body — reliable re-send)
[ 4.170] S->J  129 B  hdr 0x8002   0x5001 len 0, 0x9001 len 83, 0x1001 len 79, 0x1001 len 7
[ 4.170] S->J  124 B  hdr 0x8003   the same three records, 117 µs later
[ 4.170] S->J   16 B  hdr 0x0004   0x5001 len 0, 158 µs later
[ 4.226] J->S   32 B  hdr 0x8003   0xd001, 0x5001 ×2, 0xd001, 0x5001 ×2, 0x9001
```

The host's keep-alive, handshake, roster, roster repeat and trailer all leave inside
**202 µs** at the end — too fast for a timer to have scheduled them. They are
back-to-back sends. **[V]**

---

## 3. Step 1 — the joiner dials (44 B, hdr 0x0000)

Decoded with the pre-handshake chain key:

```
[ 0.. 2) hdr counter    0x0000
[ 2.. 4) type 0x1000    0010
[ 4]     len 0x1c = 28  1c
[ 5]     flags2 0       00
[ 6..10) peer_id        2f230100  = 74543
[10..14) counter_base   a6736773  = 0x736773a6
[14..18) module_magic   b78a254d  = 0x4d258ab7
[18]     computed byte  02
[19..21) u16            0200      = 2
[21]     count          02
[22..28) entry 0        d98ad505 6216 -> 217.138.213.5:5730
[28..34) entry 1        0a020002 6216 -> 10.2.0.2:5730
[34..44) tail           1f1f167b085047e1892b
```

**Binary.** `FUN_00268ba8`, build path `0x268cc4`–`0x268e1c`, writing in exactly this order:

| offset | address | instruction | source |
| --- | --- | --- | --- |
| `[6..10)` | `0x268cfc` | `bl 0x26cc10`, len 4 | struct returned by `0x261730`, `+0` |
| `[10..14)` | `0x268d24` | `bl 0x26cc10`, len 4 | `session+0x10` — our own base |
| `[14..18)` | `0x268d38` | `bl 0x26cc10`, len 4 | `lwz r4,-0x7fd8(r30)` — magic global |
| `[18]` | `0x268d84` | `bl 0x26cc10`, len 1 | stack `0x70(r1)`, computed — §4 below |
| `[19..21)` | `0x268d9c` | `bl 0x26cc10`, len 2 | struct `+6` |
| `[21]` | `0x268db8` | `bl 0x26cc10`, len 1 | `lhz 4(r26)` |
| `[22..28)` | `0x268dec` | `bl 0x26cb98`, len 4 | entry `+8`, **plain copy** → BE |
| | `0x268e08` | `bl 0x26cc10`, len 2 | entry `+0xc`, **reversing copy** → LE, `r31 += 6` |

`16 + 2 × 6 = 28`, which is the `len` byte the wire carries. **[V]**

The mixed byte order is not an assumption: `FUN_0026cc10` walks a cursor **backwards**
from `src + len - 1` to `src` (`0x26cc40`–`0x26cc58`, `addi r4,r11,-1` / `addi r10,r10,1`
per iteration) and `FUN_0026cb98` is a forward `memcpy`. So the same loop emits a
big-endian address and a little-endian port. **[V]**

**Five** sites build this body — `0x267e6c`, `0x2684e0`, `0x268804`, `0x268aa0`,
`0x268d2c`, each loading the magic global for writing. A sixth, `0x268fcc`, loads it to
*compare*. The host's reply comes from one of the other four; the sequence decoded above
is the one at `0x268cc4`. **[V]**

`peer_id` is the sender's own character id: session init stores it at `session[0x18]`
(`0x268210`, from the session-creation argument), and it traces game-side through
`FUN_00f08a18` (TCP `0x4101`) → `FUN_00f02e04` → `FUN_00aa0f48` → setter `FUN_002616d8`. **[V]**

The advertised entries are the sender's **public then private** endpoints. The joiner
lists `217.138.213.5:5730` and `10.2.0.2:5730` — the same public/private shape as the
host's, not "the same address twice". **[V]**

---

## 4. The byte at `[18]` — a capability byte, not a version

This is the one substantive correction to `UDP_P2P_PROTOCOL.md` §4, and it rests on
loads, stores and `ori` rather than on any mask.

**On send** (`0x268d40`–`0x268d74`) the byte is *computed from the session's own flags
word* and can only take the values 2, 3, 6 or 7:

```
00268d40: lhz    r11, 0x14(r28)      ; our own flags
00268d48: li     r0, 2
00268d50: stb    r0, 0x70(r1)        ; default 2
00268d58: li     r0, 3
00268d5c: stb    r0, 0x70(r1)        ; otherwise 3
00268d70: ori    r0, r0, 4           ; optionally |= 4
00268d74: stb    r0, 0x70(r1)
```

**On receipt** (`0x269044`–`0x269064`) the peer's byte is read back out and OR'd *into*
`session+0x14`:

```
00269044: lbz    r0, 0x70(r1)       ; the peer's byte
0026904c: lhz    r11, 0x14(r28)     ; our flags
00269050: rlwinm r9, r0, 8, 0x17, 0x17
00269054: rldicl r0, r0, 0x3f, 0x3f
0026905c: slwi   r0, r0, 9
00269060: or     r9, r9, r0
00269064: sth    r9, 0x14(r28)      ; flags = flags | (peer's byte, relocated)
```

A byte that is derived from local state on the way out and folded back into local state
on the way in is a **negotiated capability**, not a protocol version. The send path can
only ever emit 2, 3, 6 or 7 — there is no version number in it. **[V]**

Both parties in this capture sent `0x02`. **[V]**

**Which flag each value comes from is now read directly** (§0 gives the mask rule):

| capability | when |
| --- | --- |
| `2` | default |
| `3` | when the sender's own `session+0x14` has bit `0x100` |
| `\| 4` | when the sender's own `session+0x14` has bit `0x800` |

So the four values the builder can emit are `2`, `3`, `6` and `7`, and bit 2 is a faithful
copy of the sender's `0x800`. Both parties in the live capture sent `0x02`, so neither of
their `0x100`/`0x800` was set at that moment. **[V]**

> **[U] the receipt-side mapping.** Which peer bits land in which `session+0x14` bits is a
> different question: the merge at `0x269050`–`0x269064` uses `rlwinm … , 8, …` and
> `rldicl`, and §0's rule is established only for `SH = 0`. What can be said without
> decoding those two is that the peer's byte is OR'd *into* the flags word through a
> rotate by 8 and a `slwi … 9`, so its contributions land in flags bits 0 and 9 rather
> than in the high half. `UDP_P2P_PROTOCOL.md` §7 calls `0x200` (bit 9) the LZSS gate.
>
> Note that the live capture does **not** support "capability bit 7 gates compression":
> bit 7 is clear in the `0x02` both sides sent, yet the roster frames are compressed
> (`hdr 0x8002`, `0x8003`). Whatever enables LZSS here is not that bit.

---

## 5. Step 2 — the host answers, keep-alive first

```
[ 1.668] S->J  16 B  hdr 0x0000   0x5000 len 0 flags2 0
[ 1.668] S->J  44 B  hdr 0x0001
   peer_id        01a00000 = 40961 = 0xa001
   counter_base   d6658a3c = 0x3c8a65d6
   module_magic   b78a254d
   [18]           02
   [19..21)       0100 = 1
   entries        99.66.131.177:5731, 10.104.10.28:5731
```

The keep-alive takes outbound counter **0** and the handshake counter **1**, both in the
same millisecond. **[V]** This contradicts the reading in `UDP_P2P_PROTOCOL.md` §2/§6.1
that the keep-alive is sent *after* the session is keyed and is therefore
"key-establishing"; see §7 below for the key evidence.

---

## 6. Steps 3–4 — acceptance, and the switch from the pre-key to the session key

The receive loop `FUN_002620d8` routes by source address: a session match goes to the
decoder `0x2666c8` then the handshake sender's queue-drain `0x268ba8` (`0x262550`); no
match falls through to the handshake receiver `0x267d58` (`0x26244c`). **[V]**

### The gates, in the order they run

| gate | address | instruction | on failure |
| --- | --- | --- | --- |
| tail digest | `0x266918` | decoder, before the queue | dropped, never queued |
| 1 `peer_id` | `0x268ef4` | `lwz r0,0x18(r28)` / `lwz r9,0x74(r1)` / `cmpw` / `beq 0x268f58` | `0x268efc: li r27,0` — **silent skip**, state unchanged |
| 2 `module_magic` | `0x268fd8` | `lwz r11,0(r9)` vs the global, `cmpw`, `bne- 0x2690d0` | `0x2690d0` → **state 6** + `bl 0x26b1c8` |
| 3 one-byte test | `0x269008` | read 1 byte, then `rlwinm`/`cmpwi` — see §0 | `bne- 0x2690d0` |

Gate 1 and gate 2 fail *differently*, and that asymmetry is the useful part: a `peer_id`
mismatch leaves the joiner re-dialing every ~1.9 s forever, while a bad magic drops it to
state 6. Continued re-dialing against byte-perfect replies therefore points at
`peer_id`, not at the magic. **[V]**

On success the peer sockaddr is captured at `session+0x2c..0x30` (`0x268f70`), then:

```
00268f6c: ori  r11, r11, 4      ; flags |= 0x4   (reply accepted)
00268fb0: ori  r0,  r0,  2      ; flags |= 0x2
```

and the counter base is stored — `0x268c30`–`0x268c40`:

```
00268c30: lwz   r9, 0x7c(r1)    ; base parsed from the reply
00268c38: stw   r9, 8(r28)      ; session[8]   = peer_base
00268c34: xoris r0, r9, 0x2b58
00268c3c: xori  r0, r0, 0xde69
00268c40: stw   r0, 0xc(r28)    ; session[0xc] = peer_base ^ 0x2b58de69
```

`session[0xc]` is stored **pre-XORed**, which is the whole reason it exists: it makes the
tail-digest key a single XOR away. **[V]**

State stores in the module: `2` at `0x2690c8`, `3` at `0x268f50`, `6` at `0x2690d8`, `8`
at `0x268c58`. **[V]**

### Two keys, two purposes

| purpose | address | instruction | value |
| --- | --- | --- | --- |
| chain key | `0x266b7c` | `lwz r9,8(r25)` / `lwz r0,0x10(r25)` / `xor r16,r9,r0` | `peer_base ^ own_base` = **0x4fed1670** |
| digest key | `0x26695c` | `lwz r0,0x10(r25)` / `lwz r9,0xc(r25)` / `xor r9,r9,r0` | `session[0xc] ^ session[0x10]` = **K ^ 0x2b58de69** |
| pre-key chain | `0x26627c`, `0x2670e0` | `lis` + `ori` | **0x87103c2f** |
| pre-key digest | `0x267010` | `lis r0,0x2b58` / `ori r0,r0,0xde69` | **0x2b58de69** bare |

All four constants are built by `lis`/`ori` or `xoris`/`xori`, so none of this depends on
§0. **[V]**

**The wire confirms the split.** Every opening frame was decoded with all four candidate
keys:

| frame | `0x87103c2f` | `K` | `0x2b58de69` | `K ^ 0x2b58de69` |
| --- | --- | --- | --- | --- |
| joiner handshake, hdr 0 | **verifies** | — | — | — |
| host keep-alive, hdr 0 | **verifies** | — | — | — |
| host handshake, hdr 1 | **verifies** | — | — | — |
| joiner `0x1001`, hdr 0x8001 | — | **verifies** | — | — |
| joiner `0x1001`, hdr 0x8002 | — | **verifies** | — | — |
| host roster, hdr 0x8002 | — | **verifies** | — | — |

The switch is clean and it happens at acceptance, between hdr `0x0001` and hdr `0x8001`.
No frame verifies under two keys. **[V]**

In particular the host's 16-byte keep-alive is **pre-keyed** — chain `0x87103c2f`, digest
the bare `0x2b58de69`. It is not "key-establishing" in the sense
`UDP_P2P_PROTOCOL.md` §2/§6.1 gives it. The joiner reaches its keyed state on
*accepting the handshake reply*, which it can only do after reading it. **[V]**

---

## 7. Step 5 — the reliable re-send, then the roster

```
[ 3.628] J->S  88 B  hdr 0x8002   0x1001 len 140, identical body to hdr 0x8001
[ 4.170] S->J 129 B  hdr 0x8002   0x5001 len 0 / 0x9001 len 83 / 0x1001 len 79 / 0x1001 len 7
[ 4.170] S->J 124 B  hdr 0x8003   the same three records, 117 µs later
[ 4.170] S->J  16 B  hdr 0x0004   0x5001 len 0
```

The joiner re-sends the identical `0x1001` record 1.93 s later — the retry interval, the
same one the re-dial uses. **[V]**

`0x1001` is produced by the serializer at `0x2698bc` (`ori r9, r10, 0x1000` — the
`flags & 1` reliable bit), with the long form at `0x2698e8` (`ori r11, r11, 0x2000`).
Both are unambiguous `ori`s. **[V]**

The host's own roster entry travels under **`0x9001`**, the tag the joiner itself opened
with, while the joining players and the close travel under `0x1001` — on both the
original frame and the repeat. **[V]**

### What this binary cannot show

The roster's *content* — which entry is emitted as `0x9001`, the character names, the
`0x0000a001` / `0x0001xxxx` ids at roster offsets `0x08` — is written by the **dedicated
server**, which is not `MGO2.ELF`. `MGO2.ELF` proves only the client half: what it
advertises, what it accepts, how it keys and dispatches what comes back. The per-id
dispatch site is `0x2614f8`, but see `UDP_P2P_PROTOCOL.md` §6.2 — its mask is **not**
established here. **[U]**

---

## 8. Reproducing this

```
python3 tools/pcap_conversations.py mgo2.pcapng                    # the four flows
python3 tools/udp_flow_summary.py  mgo2.pcapng --peer 99.66.131.177:5731
python3 tools/udp_join_replay.py   mgo2.pcapng --frames 8          # the nine datagrams
python3 tools/udp_frame.py         mgo2.pcapng --peer 99.66.131.177:5731 --limit 40
```

For the binary side, `tools/mgo2_disasm.py <start-hex> <end-hex> MGO2.ELF` covers every
address above. `tools/mgo2_xrefs.py <target-hex> <window-hex> MGO2.ELF` finds callers;
note both default to `docs/MGO2.ELF`, so pass the path explicitly — the binary is at the
repository root.

The handshake layout can be re-proved against the wire without trusting this file: decode
the two 44-byte datagrams and read the fields at the offsets in §3. Every one of them
lands on the value the builder writes at the address in the same row.
