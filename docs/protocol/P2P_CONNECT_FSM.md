# The P2P connect state machine — `FUN_00aa1140`, six states

This is the machine behind *"Unable to connect to host."* It is the **joiner's**
peer-to-peer connect driver: the client that has already been handed a host by
the TCP game server (`0x4320` → `0x4321`) and is now trying to reach that host
over UDP. `FUN_00aa1140` is its per-frame tick.

It is worth stating the family tree first, because two different "state
machines" live near each other and only one of them owns `0B09`:

| | |
| --- | --- |
| this FSM (`0xaa1140`) | the **joiner's dial / join driver** — ticks on the game object at `ctx+0x70`, raises `0B00`…`0B09`, `0B12`, `0D08` |
| the module session state (`session+4`) | the transport handshake state — `2` socket-open, `3` handshake sent, `5` awaiting reply, `6` reply accepted, `8` data phase (`UDP_P2P_PROTOCOL.md` §6) |
| the other flow FSM (`0x288ff0`/`0x288d18`) | lobby/session bring-up, shares the state-3/state-4 helpers but not the error set |

The joiner reaches the **full joined state** only when both line up: this FSM
reaches its terminal success state **6**, which happens only after the module
session reports **8** (the data phase). That is the whole reason a server that
looks correct on the wire can still leave the client at the join screen.

Method: the machine is decoded with **Capstone** from `MGO2.ELF` (PPC64
ELFv1, big-endian, `BLUS30109`). Ghidra's auto-analysis stops at the `bctr`
dispatch, so the handlers below were crossed by hand with
`tools/p2p_fsm_disasm.py` and `tools/ppc_function_disasm.py` (both added with
this document). Tags are **[V]** verified in the binary / **[I]** inferred /
**[U]** unresolved. The image is the one at the repository root (`MGO2.ELF`);
the older `docs/MGO2.ELF` path in some tool defaults is stale.

---

## 1. The object the tick is called on

`r3` is the FSM context. Every field below is read or written inside
`0xaa1140`'s handlers.

| offset | width | meaning | written at |
| --- | --- | --- | --- |
| `ctx+0x60` | u32 | the **network object** — the channel owner passed to every `0xf0xxxx` accessor, and to `0x261fe0`'s sibling send path | `0x97c124`/`0xf1203c` inputs; set elsewhere |
| `ctx+0x64` | u32 | the **P2P session handle** returned by `0x261fe0`; `0` = none | `0xaa1534`, `0xaa144c`, `0xaa166c`, `0xaa1558` |
| `ctx+0x6c`, `ctx+0x78` | u32 | arguments to the `0x4320` send (`0xf1203c`) | set before the tick runs |
| `ctx+0x70` | u16 | **the state** the dispatcher switches on | `sth` at `0xaa12b8`, `0xaa13b4`, `0xaa1540`, `0xaa158c` |
| `ctx+0x74` | u32 | per-state countdown, decremented by `5` on each tick | `0xaa1454`, `0xaa13bc`, `0xaa1584`, `0xaa141c`, `0xaa1630` |
| `ctx+0x7c` | u32 | peer identity copied into the P2P session descriptor; it ends up gating the handshake reply (§4 gate 1) | `0xaa10cc`, the constructor `0xaa1028`'s second argument — see §7.1 |
| `ctx+0x80` | u32 | flags; bit `0` = "may re-dial the same peer" (`0xaa13e0` tests it, `0xaa1420` clears it) | outside this FSM |
| `ctx+0x84` | u32 | set to `1` by the raise path (`0xaa16d0`) — the "this attempt is over" marker the caller polls | `0xaa16d0`, `0xaa162c` |
| `ctx+0x88` | u32 | the `0xB01`/`0xB08` timeout counter, stepped by `5` | `0xaa1534`, `0xaa12b4`, `0xaa168c` |

The tick returns `void` (it restores registers and `blr`s at `0xaa16f8`); the
caller learns the outcome from `ctx+0x70` / `ctx+0x84`.

---

## 2. Dispatch — the tick and its inline jump table

```
00aa1140: stdu r1,-0x200(r1)          ; prologue …
00aa115c: lhz  r0, 0x70(r3)           ; state
00aa1160: mr   r31, r3                ; r31 = ctx
00aa1164: lwz  r30, -0x6de8(r2)       ; module globals
00aa1168: cmplwi cr7, r0, 5
00aa116c: bgt  cr7, 0xaa16dc          ; state > 5 → epilogue, return
00aa1170: lwz  r9, -0x7fe4(r30)       ; r9 = base of the jump table
00aa1174: rlwinm r0, r0, 2, 0xe, 0x1d ; r0 = state * 4
00aa1178: lwax r0, r9, r0             ; entry (a signed 32-bit relative offset)
00aa117c: add  r0, r0, r9
00aa1180: mtctr r0
00aa1184: bctr
```

**The table is inline at `0xaa1188`, immediately behind the `bctr`**, as six
32-bit signed offsets relative to `0xaa1188`. That is why Ghidra stops here:
Capstone too halts at the first non-instruction word unless the walker crosses
the data by hand. The table base is pinned to `0xaa1188` by the entries
themselves: every offset lands on a real handler, in range, that ends in that
state's transition. The `r2`-based slot the earlier notes read is not the
table:

| state | table offset @ `0xaa1188+4·n` | handler | tail |
| --- | --- | --- | --- |
| 0 | `0x00000018` | `0xaa11a0` | send `0x4320` → state 1 |
| 1 | `0x0000006c` | `0xaa11f4` | await `0x4321`, dial peer → state 2 or 5 |
| 2 | `0x00000194` | `0xaa131c` | await module state 8 → state 3 |
| 3 | `0x000003d8` | `0xaa1560` | await net-ready bit → state 4 |
| 4 | `0x00000440` | `0xaa15c8` | apply net options → state 6 |
| 5 | `0x000004f8` | `0xaa1680` | post-failure wait → **raise `0B09`** / `0B08` |
| >5 | — | `0xaa16dc` | epilogue |

`0xaa1140` is published through the function descriptor at **`0x1208bd0`**
(code `0x00aa1140`, TOC `0x01222a00`), inside the module's OPD table; it is
called by the game's per-frame driver, not from this module directly. **[V]**

---

## 3. State summary

```
                 ┌──────────────────────────────────────────────┐
   state 0 ──────┤ send TCP 0x4320 (joinGame)                    │  0xB00 on send failure
        │        └──────────────────────────────────────────────┘
        ▼
   state 1 ──────  poll TCP channel 0x2a (0x4321 reply)
        │            timeout 12000 → 0xB01
        │            channel error → 0xB02/0xB03/0xB04/0xB12/0xD08
        │            got reply → build endpoint desc → 0x261fe0
        │                 ├─ session != 0 ─────────────────────▶ state 2
        │                 └─ session == 0 → 0xf10dec sends 0x4322
        │                        ├─ error → 0xB07
        │                        └─ ok ────────────────────────▶ state 5
        ▼
   state 2 ──────  tick module session, await return 8
        │            == 8 → 0x270e00 handoff ─────────────────▶ state 3
        │            != 8 → countdown 6000
        │                 expiry, flags&1 clear → destroy → 0x4322 → state 5
        │                 expiry, flags&1 set, session==6 → re-arm, keep waiting
        │                 expiry, flags&1 set, session!=6 → destroy + re-dial peer
        ▼
   state 3 ──────  await net-ready bit (0x281db0) ─────────────▶ state 4
        │            timeout 3000 → destroy → 0x4322 → state 5
        ▼
   state 4 ──────  net options 0xa1/0x52/0x58 (0x27e198) ──────▶ state 6  (success)
        │            timeout 3000 → destroy → 0x4322 → state 5
        ▼
   state 5 ──────  poll channel 0x2b completion
                     completed → raise 0xB09 (:00000000 if the error slot is 0)
                     timeout   → raise 0xB08:FFFFFF60
```

`state 6` is terminal: the dispatcher's `bgt r0,5` branch sends it straight to
the epilogue, so once a tick has stored state 6 the machine is finished.

---

## 4. The states, one by one

### State 0 — `0xaa11a0`, request the host

```
00aa11a0: bl   0x97c124                 ; → game/flow object
00aa11a8: li   r6, 3                    ; default mode = 3
00aa11ac: lbz  r0, 0x2b4(r3)            ; mode byte
00aa11b0: cmpwi cr7, r0, 0xa
00aa11b4: beq  cr7, 0xaa11bc
00aa11b8: mr   r6, r0                   ; otherwise mode as-is
00aa11bc: clrldi r6, r6, 0x38
00aa11c0: lwz  r3, 0x60(r31)            ; network object
00aa11c4: lwz  r4, 0x78(r31)
00aa11c8: lwz  r5, 0x6c(r31)
00aa11cc: bl   0xf1203c                 ; build + send TCP 0x4320 (joinGame)
00aa11d4: cmpwi cr7, r3, 0
00aa11d8: bne  cr7, 0xaa11e8            ; failure → raise
00aa11dc: li   r0, 1
00aa11e0: stw  r3, 0x88(r31)            ; counter = 0
00aa11e4: b    0xaa12b8                 ; state = 1
00aa11e8: extsw r4, r3
00aa11ec: li   r3, 0xb00
00aa11f0: b    0xaa16c8                 ; raise(0xB00, rc)
```

`0xf1203c` builds the join packet and calls the TCP writer with opcode
**`0x4320`** (`0xf2e4e8(…, 0x4320)` at `0xf121a0`). The matching result parser
is the separate `0x4321` handler (`FUN_00f12278`). `[V]` A failure to even queue
the request is the only `0xB00`.

### State 1 — `0xaa11f4`, await the host and dial it

```
00aa11f4: lwz  r9, 0x88(r3)             ; timeout counter
00aa11f8: li   r3, 0xb01
00aa11fc: addi r9, r9, 5
00aa1200: cmplwi cr7, r9, 0x2ee0        ; 12000
00aa1204: stw  r9, 0x88(r31)
00aa1208: bgt  cr7, 0xaa1698            ; → r4 = -0xa0 → raise(0xB01,-0xa0)
00aa120c: lwz  r3, 0x60(r31)
00aa1210: bl   0xf0d3b8                 ; poll channel 0x2a
00aa1218: cmpwi cr7, r3, 0
00aa121c: beq  cr7, 0xaa16dc            ; 0 → keep waiting
00aa1220: lwz  r3, 0x60(r31)
00aa1224: bl   0xf0d01c                 ; error of channel 0x2a
00aa122c: cmpwi cr7, r3, 0
00aa1230: bne  cr7, 0xaa12c0            ; error table below
00aa1234: lwz  r3, 0x60(r31)
00aa1238: bl   0xf0cd6c                 ; pointer to the stored 0x4321 reply
00aa1240: cmpwi cr7, r3, 0
00aa1244: beq  cr7, 0xaa1284            ; no reply yet → still build + dial
00aa1248: lbz  r29, 0xa(r3)
00aa1254: lbz  r3, 9(r3)
00aa1260: bl   0xc2c1f8                 ; format helper [U]
00aa1278: bl   0xc2cb60                 ; format helper [U]
00aa1284: lwz  r9, 0x7c(r31)            ; peer identity
00aa1288: li   r28, 2
00aa128c: lwz  r3, 0x60(r31)
00aa1290: b    0xaa1450                 ; build the descriptor and dial
```

The error table (`0xaa12c0`…`0xaa1318`) maps the channel's code onto the
client's dialogs:

| channel error | dialog raised |
| --- | --- |
| `-0x1f7` | `0xB03` |
| `-0x21c` | `0xB04` |
| `-0x21d` | `0xD08` |
| `-0x21e` | `0xB12` |
| anything else | `0xB02` |

Then the dial itself, `0xaa1450`:

```
00aa1450: li   r0, 0x1770               ; 6000 → 1200 ticks
00aa1454: stw  r0, 0x74(r31)
00aa1458: stw  r9, 0x74(r1)             ; desc[0x00] = ctx+0x7c  (u32)
00aa145c: sth  r28, 0x78(r1)            ; desc[0x04] = 2         (u16)
00aa1460: bl   0xf0ccf4                 ; public IP string
00aa1468: addi r0, r1, 0x70            ; … parsed by 0xfbb79c into 4 bytes
00aa1488: addi r9, r1, 0x7c            ; desc[0x08..0x0b] = public IP
00aa14b0: bl   0xf0cd10                ; desc[0x0c..0x0d] = public port
00aa14c0: bl   0xf0cd30                ; desc[0x0e..0x11] = private IP
00aa150c: bl   0xf0cd4c                ; desc[0x12..0x13] = private port
00aa1524: addi r3, r1, 0x74
00aa1528: bl   0x261fe0                 ; create the P2P session
00aa1534: stw  r3, 0x64(r31)
00aa1538: beq  cr7, 0xaa129c            ; 0 → failure path
00aa153c: sth  r28, 0x70(r31)           ; state = 2
```

So the descriptor is 20 bytes: `[0]` peer identity from `ctx+0x7c`, `[4]` the
constant `2`, then public IP/port and private IP/port taken from the network
object the `0x4321` reply was parsed into. **[V]**

`0x261fe0(desc, 4, 0)` is the module's peer-session constructor. Two gates
inside it return 0 instead of a handle:

```
00262004: lwz  r9, -0x8000(r30)        ; module instance
00262008: lwz  r0, 0(r9)
0026200c: cmpwi cr7, r0, 2
00262010: beq  cr7, 0x262034           ; else fall through to return 0
       … a 24-slot scan (stride 0x6d8); all busy → return 0
       … free slot → 0x268148(slot, desc, 4, 0); return the slot
```

**`0x261fe0` returns 0 unless the module instance's first word is exactly 2 and
a free session slot exists among 24.** `[V]` This is a hard precondition for
the dial: a server cannot make `0x261fe0` succeed; only the client's own
network module can. When it does return 0 the FSM takes the failure path.

Failure path (`0xaa129c`):

```
00aa129c: lwz  r3, 0x60(r31)
00aa12a0: bl   0xf10dec                 ; send TCP 0x4322 (join failed)
00aa12ac: bne  cr7, 0xaa1674            ; nonzero → raise(0xB07, rc)
00aa12b0: li   r0, 5
00aa12b4: stw  r3, 0x88(r31)            ; reset the counter
00aa12b8: sth  r0, 0x70(r31)            ; state = 5
```

`0xf10dec` resets the TCP send queue, writes opcode **`0x4322`**
(`0xf2e4e8(…,0x4322)` at `0xf10e54`), re-arms channel `0x2b`
(`0xefeb18(…,0x2b,1)` at `0xf10eb8`) and returns `0` on success, `-0x24`
(no object) or `-0x3d` (send failure). `[V]`

### State 2 — `0xaa131c`, await the module's data phase

```
00aa131c: lwz  r9, 0x64(r3)             ; session handle
00aa1324: lwz  r9, 0(r9)                ; *session
00aa1328: lwz  r9, 0(r9)                ; **session = vtable
00aa132c: lwz  r0, 0(r9)                ; method 0 of the vtable
00aa133c: bctrl                         ; session->tick()
00aa1344: cmpwi cr7, r3, 8
00aa1348: bne  cr7, 0xaa13c4            ; not yet → poll/countdown
00aa134c: addi r3, r1, 0x74
00aa1350: bl   0x97c39c                 ; clear scratch
00aa1358: bl   0x97c124
00aa1360: lbz  r0, 0x2b4(r3)            ; mode byte
00aa1364: li   r5, 0x1002
00aa1368: cmpwi cr7, r0, 7 → r5 = 0x1002 ; else 0x2002-family handoff code
00aa1394: lwz  r4, 0x64(r31)            ; session
00aa13a0: bl   0x270e00                 ; 0x270e00(0, session, code, scratch)
00aa13a8: bl   0x281dd8                 ; net notifier
00aa13b0: li   r0, 3
00aa13b4: sth  r0, 0x70(r31)            ; state = 3
00aa13b8: li   r0, 0xbb8
00aa13bc: stw  r0, 0x74(r31)            ; 3000 → 600 ticks
```

**Return `8` from the session vtable is the "transport is up" signal.** It is
the module's data phase (`UDP_P2P_PROTOCOL.md` §6), and reaching it is the
step every other part of this document is about. On `8` the FSM hands the
session to the game side with `0x270e00(0, session, 0x1002/0x2002, scratch)`
and advances to state 3. **[V]**

Anything else counts down `ctx+0x74` (6000, i.e. 1200 ticks, set when the dial
was issued). On expiry (`0xaa13c4`):

* `ctx+0x80 & 1` **clear** → destroy the session (`0x261de0`), store
  `ctx+0x64 = 0`, and take the `0x4322` failure path to state 5.
* `ctx+0x80 & 1` **set** → tick the session again:
  * return `6` → `ctx+0x74 = 0x1194` (4500), clear the re-dial bit, keep
    waiting (the reply was accepted; the key is still missing);
  * otherwise → destroy, zero `ctx+0x64`, **re-dial the same peer** through
    `0xaa1450` (build descriptor → `0x261fe0`), staying in state 2.

That re-dial loop is the client-side twin of the wire's "handshake every
~1.9 s" cadence.

### State 3 — `0xaa1560`, wait for the network-ready bit

```
00aa1560: bl   0x281db0                 ; returns 0x100 or 0
00aa1568: cmpwi cr7, r3, 0
00aa156c: bne  cr7, 0xaa1590            ; ready → countdown branch
00aa1570: li   r3, 0xa
00aa1574: li   r4, 0
00aa1578: bl   0x26e9f8                 ; net event 0x0a
00aa1580: li   r0, 0xbb8
00aa1584: stw  r0, 0x74(r31)            ; 3000
00aa1588: li   r0, 4
00aa158c: b    0xaa12b8                 ; state = 4
00aa1590: lwz  r9, 0x74(r31)
00aa1594: addi r9, r9, -5
00aa15a0: bge  cr7, 0xaa16dc            ; still counting → return
00aa15a4: bl   0x2703e8                 ; net teardown
00aa15ac: lwz  r3, 0x64(r31)            ; session …
00aa15bc: bl   0x261de0                 ; destroy
00aa15c4: b    0xaa1294                 ; → 0x4322 → state 5
```

`0x281db0` is a five-instruction global reader: it returns bit `23` of the
word at `global+0x88` (`rlwinm r3,r3,0,0x17,0x17`) as `0x100` or `0`, i.e. a
single network-readiness flag. **[V]** When it is clear the same 3000-tick
countdown runs; expiry is a join failure.

### State 4 — `0xaa15c8`, apply the network options

```
00aa15c8: li   r3, 0
00aa15cc: bl   0x27e2b0                 ; → network manager
00aa15dc: li   r5, 1
00aa15e0: mr   r3, r29
00aa15e4: bl   0x27e198                 ; option 0xa1
00aa15f4: beq  cr7, 0xaa1638            ; not applied → countdown
00aa15f8: bl   0x26f158                 ; net notify
00aa160c: bl   0x27e198                 ; option 0x52
00aa1620: bl   0x27e198                 ; option 0x58
00aa1628: li   r0, 0
00aa162c: stw  r0, 0x84(r31)            ; clear the "over" marker
00aa1630: li   r0, 6
00aa1634: b    0xaa12b8                 ; state = 6   ◀── SUCCESS
```

`0x27e2b0(0)` returns `*(globalTable + 0)` then `+8`; `0x27e198(list, index,
value)` walks the list for `index` and flips the bit for `(index,value)` in a
per-entry bitmap. Which options `0xa1`/`0x52`/`0x58` are is **[U]**; the shapes
are shared verbatim with the lobby flow FSM at `0x288d18`, which applies the
same three in its own state 3. `[V]` The branch that is *not* taken
(`0xaa1638`) decrements the same 3000-tick countdown and, on expiry, tears the
session down into the `0x4322` failure path.

### State 5 — `0xaa1680`, the post-failure wait (and `0B09`)

This state is reached **only** from a failure path: `0x261fe0` returned 0 and
the `0x4322` join-failed send succeeded (state 1), or a later state's countdown
expired and tore the session down. Its two exits raise an error — it never
succeeds.

```
00aa1680: lwz  r9, 0x88(r3)
00aa1684: addi r9, r9, 5
00aa1688: cmplwi cr7, r9, 0x2ee0        ; 12000
00aa168c: stw  r9, 0x88(r3)
00aa1690: ble  cr7, 0xaa16a0
00aa1694: li   r3, 0xb08                ; ── timeout
00aa1698: li   r4, -0xa0
00aa169c: b    0xaa16c8                 ; raise(0xB08, -0xa0)
00aa16a0: lwz  r3, 0x60(r3)             ; network object
00aa16a4: bl   0xf0d38c                 ; poll channel 0x2b
00aa16a8: cmpwi cr7, r3, 0
00aa16b0: beq  cr7, 0xaa16dc            ; still working → return
00aa16b4: lwz  r3, 0x60(r31)
00aa16b8: bl   0xf0cff0                 ; error of channel 0x2b
00aa16c0: extsw r4, r3
00aa16c4: li   r3, 0xb09
00aa16c8: li   r0, 1
00aa16cc: li   r5, 0
00aa16d0: stw  r0, 0x84(r31)            ; "over"
00aa16d4: bl   0x97f1b8                 ; raise(dialogId, code, 0)
```

The two channel accessors are thin wrappers over one per-channel table, and
they are the exact instruction pair the client uses to decide "the peer leg is
finished, and this is what it says":

```
00f0d38c: li  r4, 0x2b ; bl 0xf015d0    ; poll channel 0x2b
00f0cff0: li  r4, 0x2b ; bl 0xefedd0    ; error of channel 0x2b
```

`0xf015d0(handle, ch)` reads the channel slot state at
`handle + 0x168 + ch*4` and returns `-1` exactly when it is `2`, else `0`
(and it calls `0xefeb18(handle, ch, 0)` on that edge). Siblings on the same
object, same shape, are `0xf0d3b8`/`0xf0d01c` on channel **`0x2a`** — those
are what state 1 uses for the TCP join reply. `0xefedd0(handle, ch)` returns
`*(i32*)(handle + 0x370 + ch*4)`, or `-0x18` when the handle is null or
`ch > 0x81`. For channel `0x2b` that error slot is at `handle + 0x41c`. `[V]`

### States > 5 — `0xaa16dc`, the epilogue

Restores `r28`–`r31`, `r1`, `lr` and returns. **There is no behaviour for
state 6**; it is the "finished" value, not a state that does anything.

---

## 5. The raise path

Every error in the FSM converges on `0xaa16c8`, which calls the dialog raiser
`0x97f1b8(dialogId, code, 0)`:

```
0097f1b8: … builds a small event …
0097f200: ori  r0, r0, 0x10             ; event type
0097f204: stw  r26, 0x80(r1)            ; dialogId
0097f20c: stw  r27, 0x84(r1)            ; code
0097f214: bl   0x8f0478                 ; queue it
0097f220: bl   0xbe9e0                  ; (li r3,4) post/flush
```

`0x8f0478` writes the event into the live dialog slot, and the ID is rendered
by the format string **`(%04X:%08X)` at `0xff37a0`** (immediately before the
`MGO_ERROR_RES_GMINFO` string). That is the `0B09:00000000` on screen: the
low half is the code argument, the high half the dialogId. `[V]`

The dialogId → sentence mapping is the error table in `docs/ERRORS.md`:

| dialogId | hex | `docs/ERRORS.md` row | sentence |
| --- | --- | --- | --- |
| 2816 | `0B00` | 2816 | Unable to connect to host. |
| 2817 | `0B01` | 2817 | Unable to connect to host. |
| 2818 | `0B02` | 2818 | Unable to connect to host. |
| 2819 | `0B03` | 2819 | Maximum number of characters already reached. |
| 2820 | `0B04` | 2820 | Password is incorrect. |
| 2823 | `0B07` | 2823 | Unable to connect to host. |
| 2824 | `0B08` | 2824 | Unable to connect to host. |
| **2825** | **`0B09`** | **2825** | **Unable to connect to host.** |
| 2834 | `0B12` | 2834 | Unable to connect to host. |
| 3336 | `0D08` | 3336 | Unable to connect to host. |

So `0B09` and `0B08` are the *same sentence*; they differ only in which exit of
state 5 produced them.

---

## 6. What `0B09:00000000` actually is

`0B09` is **not** a TCP failure and **not** a timeout. It is raised at exactly
one site in the image — `0xaa16c4`, inside state 5 — and the state it lives in
is only ever entered *after* the client has already concluded the join failed
and has sent `0x4322`. The sequence is:

1. The dial failed: `0x261fe0` returned 0 (module instance not at 2, or no free
   session slot), **or** a later state's countdown expired and tore the session
   down. The FSM calls `0xf10dec`, which sends TCP **`0x4322` (join failed)**
   and re-arms channel `0x2b`, returning 0.
2. State 5 now waits for channel `0x2b` to complete. `0xf0d38c` returns 0
   while it hasn't; the moment the slot reads state 2 it returns `-1`.
3. On that edge the FSM reads channel `0x2b`'s error slot with `0xf0cff0`.

`0B09:00000000` means: **the peer channel finished (state 2) and its error slot
was never written**, i.e. the leg was torn down rather than refused, so the
client has no diagnostic to show. A non-zero error slot would render as
`0B09:xxxxxxxx`.

The other exit of state 5, `0B08:FFFFFF60`, is the 12000/5-tick timeout — the
`~40 s` hang the backlog already records, traded for the clean `0B09` once a
server answers `0x4322` (and consequently releases channel `0x2b`).

**The operative point:** `0B09` is a *verdict*, not a cause. The client was
already unable to connect before state 5 existed; state 5 only reports it.
`docs/BACKLOG.md` says the same ("`0x4322` being handled … means join *fails
politely* (`0B09`)"). Every `0B09` therefore points at the same upstream
question: **why the peer session never reached the module's data phase.**

---

## 7. How the client reaches the full joined state

The progression the server has to enable is short and precise:

```
state 0  send TCP 0x4320
state 1  read TCP 0x4321, build the endpoint descriptor, 0x261fe0 → state 2
state 2  the module session tick returns 8   ← the whole problem lives here
state 3  net-ready bit set
state 4  net options 0xa1/0x52/0x58 applied → state 6 (finished)
```

Only two of those depend on the server at all:

1. **The `0x4321` reply must be correct enough to be parsed and to carry the
   host's identity.** The descriptor's first word is `ctx+0x7c`, which the
   client only learns from the join exchange; the module constructor then
   stores it and the handshake reply is gated on it (gate 1 in
   `UDP_JOIN_FLOW.md` §6 — `peer_id` must equal `session[0x18]`, the host's
   character id, **not** an echo of the joiner's). A reply that carries the
   wrong id fails that gate *silently*: the joiner re-dials for the full
   timeout and state 2 never sees `8`. `[V]`
2. **The UDP handshake must complete and the session must be keyed.** The
   module's data phase (its state 8) is gated on `(flags & 3) == 3`
   (`0x268c44`, `0x268c58`). Bit `0x2` comes from the dialer's own handshake
   send; bit `0x1` is set by the decoder's second digest chance — a frame whose
   tail digest verifies with **`K ^ 0x2b58de69`** (`UDP_P2P_PROTOCOL.md` §6.1
   and §11.4) — and **also**, on the accept path itself, at `0x269084`
   (`ori r0, r11, 1` → `sth r0, 0x14(r28)`), on the path the accepted reply
   rejoins — so acceptance alone satisfies the gate. §7.1 measures the reference
   join and finds the joiner in its keyed phase with no host keyed frame
   anywhere before it.

So the minimum server behaviour is the set the protocol doc names and §7.1
measures: answer the handshake so the reply is accepted (correct `peer_id`,
magic, capability, digest), keep **both** opening frames pre-keyed, and send
nothing else until the joiner's profile arrives. Acceptance carries the module
into state 8 by itself, so the FSM's state 2 succeeds and states 3–4–6 follow.

### What the gameplay server sends (implemented)

`src/GameplayServer/Commands/PeerCommandHandlers.cs` (`AcceptHandshakeHandler`)
sends the two frames the capture holds, in order:

* the keep-alive **pre-keyed** at counter 0;
* the handshake reply **pre-keyed** at counter 1;

then sets `Session.Established = true` so that everything after them — the
roster run first — is signed with the session key and the keyed digest on the
shared outbound counter, and sends **one session-keyed empty keep-alive**
between the reply and that run.

That third frame is not in the reference, and a live join is why it is there.
§7.1 read the capture's two frames as sufficient — the accept path sets `flags`
bit `0x1` at `0x269084`, and the captured joiner keys up 26 ms after the reply —
while the 2026-09-09 fake-host trial found a keyed frame was what stopped the
re-dial. The game that raised `0B09` settles it the other way: answered with the
two recorded frames and nothing else, the session sat in its reply-accepted
state, and state 2 counted **both** of its deadlines out — 6000 units, a tick
that still reported that state and reset the countdown to 4500, then the failure
path. That is 35.0 s after the dial, exactly as measured, and the session never
reached 8. So one session-keyed `0x5000` goes out
(`Session.Established = true` followed by
`context.Send(UdpCommandConstants.KeepAlive, [], 0)`). It costs the capture's
counters one place — the roster run leaves at 3 rather than 2 — and the joiner's
window is `last+1 .. last+0x20` (§11.5 of `UDP_P2P_PROTOCOL.md`), so only
consecutiveness has to hold. `[V]`

Two more requirements that are easy to miss because they are not on this FSM at
all but decide whether it ever gets a reply:

* the advertised endpoint in `0x4321` must be a real, reachable address on the
  socket the client dials (`0.0.0.0` makes `sendto` fail outright; a loopback
  address is swallowed by the emulator's guest loopback — `UDP_P2P_PROTOCOL.md`
  §2, "Practical findings");
* the reply's `peer_id` must be the **host's character id** the join advertised
  — not the joiner's, and not an arbitrary constant if the client gates on it.

### 7.1 The reference capture sends no keyed frame — measured

Walking `docs/mgo2-game.pcapng`, join flow `10.2.0.2:5730` ↔
`99.66.131.177:5731` (joiner base `0x736773a6`, host base `0x3c8a65d6`,
`K = 0x4fed1670`), decoding every datagram against both the pre-key and the
session key:

```
 t (s)   len  direction  decoded
 0.0000   44  joiner     pre : counter 0     0x1000  the joiner's handshake
 1.6681   16  host       pre : counter 0     0x5000  keep-alive   ← pre-keyed
 1.6681   44  host       pre : counter 1     0x1000  the reply    ← pre-keyed
 1.6943   92  joiner     KEY : counter 1     0x5000, 0x1001(140)
 3.6282   88  joiner     KEY : counter 2     0x1001(140)
 4.1699  129  host       KEY : counter 2     0x5001, 0x9001(83), 0x1001(79)..
```

Two things follow, and the second contradicts §6.1 of
`UDP_P2P_PROTOCOL.md`:

* The host writes **exactly two frames** at join — both pre-keyed, counters 0
  and 1. Its next frame is the roster run at counter 2, **2.5 s later**. There
  is no self-keyed keep-alive in the reference, and no keyed frame of any kind
  from the host before the joiner has already keyed up.
* The joiner is sending **session-keyed** frames 26 ms after the pre-keyed reply,
  having received nothing keyed. So the reply alone carries it into the keyed
  phase. The digest gate explains it: `decode` verifies the tail with
  `BASE_DERIVE` for the pre-key path and `K ^ BASE_DERIVE` for the keyed one, so
  a frame verifying with the keyed digest was *sent* keyed — the joiner chose
  the key without ever seeing one.

The accept path as disassembled says the same, and this is the part §6.1 got
wrong: **the reply's own drain sets both role bits and stores state 8 in one
call** (`FUN_00268ba8`, the handshake sender's queue-drain).

```
;; entry of FUN_00268ba8; nothing accepted yet, flags bit 0x2 clear
00268bf0: rlwinm r0, r11, 0, 0x1e, 0x1e   ; test flags bit 0x2
00268c00: beq    cr7, 0x268e20            ; clear -> read the peer's record
00268eec: lwz    r0, 0x18(r28)            ; session[0x18] = the host character id
00268ef4: cmpw   cr7, r0, r9 ; beq 0x268f58  <-- gate 1, AND the accept entry
00268f6c: ori    r11, r11, 4 ; sth 0x14(r28)   flags |= 4   reply accepted
00268fb0: ori    r0, r0, 2   ; sth 0x14(r28)   flags |= 2   one role bit
00269064: sth    r9, 0x14(r28)             ; capability byte folded in
;; rejoin the state dispatch with the new flags
00269078: b      0x268c04
00268c04: lbz    r9, 4(r28)                ; state
00268c20: beq    cr7, 0x268c94            ; state 2 -> the dial-side block
00268c9c: beq    cr7, 0x268f04
00268f20: cmpwi  cr7, r3, 0
00268f24: beq    cr7, 0x26907c            ; dial helper says "go"
0026907c: lhz    r11, 0x14(r28)
00269084: ori    r0, r11, 1 ; sth 0x14(r28)   flags |= 1   the other role bit
00269090: b      0x268ca0
00268cac: rlwinm r0, r11, 0, 0x1e, 0x1e
00268cb4: beq    cr7, 0x268c24            ; bit 0x2 still set -> the promotion test
00268c44: clrlwi r0, r11, 0x1e             ; (flags & 3)
00268c4c: cmpwi  cr7, r0, 3
00268c58: stb    r0=8, 4(r28)             ; state := 8, no keyed frame anywhere
```

So the sequence the capture shows is the sequence the code takes: the pre-keyed
reply is accepted, the same call folds the capability in, sets bit `0x1` on the
rejoin and stores state **8**. The FSM's state 2 sees `8` on its next tick, 26 ms
later. `[V]`

The fake-host trial behind §6.1's "one session-keyed frame is what flips a
state-6 dial session into the data phase" saw a real effect, and it is the one a
live client needs: a run with the ESTABLISH OUT removed did happen, the joiner
sat in the reply-accepted state, and the join failed 35.0 s after the dial with
`0B09` (see "What the gameplay server sends" above). §7.1's reading of the
capture — that acceptance alone stores state 8 — is what a capture alone cannot
tell from a client that never got there.

---

### 7.2 Where the gating id comes from — traced, and consistent

The gating id is the FSM context's second constructor argument. `0xaa1028`
allocates the 0x90-byte context and stores its arguments straight into the
fields the tick reads:

```
00aa1044: mr   r26, r4          ; r26 = arg2
00aa104c: mr   r27, r3          ; r27 = arg1
00aa1064: mr   r28, r5          ; r28 = arg3
00aa10a8: bl   0x280418         ; r3 = net singleton (a plain pointer getter)
00aa10c0: sth  r9=0, 0x70(r29)  ; state = 0
00aa10c4: stw  r3,   0x60(r29)  ; ctx+0x60 = net singleton
00aa10c8: stw  r27,  0x78(r29)  ; ctx+0x78 = arg1
00aa10cc: stw  r26,  0x7c(r29)  ; ctx+0x7c = arg2   ◀── the gating id
00aa10d0: stw  r28,  0x6c(r29)  ; ctx+0x6c = arg3
```

Its join-path callers pass `arg2` as the **first word of the room's player-list
entry 0** — the host:

```
00a7b070: lwz   r3, 0x60(r9)   ; net singleton
00a7b074: bl    0xf13028       ; r3 = the dial/peer handle
00a7b08c: lwz   r29, 0(r11)    ; r29 = that handle's first word (arg1)
00a7b084: li    r4, 0          ; index = 0
00a7b088: lwz   r3, 0x60(r9)
00a7b090: bl    0xa3ac24       ; (net, 0) → first peer-table slot
00a7b098: lwa   r4, 0(r3)      ; arg2 = *(slot 0)
00a7b09c: li    r5, 0          ; arg3 = 0
00a7b0a0: extsw r3, r29        ; arg1
00a7b0a4: bl    0xaa1028
```

`0xa3ac24(net, index)` returns `0xf0ccd8(net) + index*32 + 0xb0`, i.e. one
32-byte slot of the net object's peer table, and every join-path caller indexes
it with a literal `0`. The client therefore files the **host's character id**
(taken from the TCP player list) as the dial session's `ctx+0x7c`, and gate 1
compares the UDP handshake reply's `peer_id` against exactly that. `[V]`

The server honours this end to end: `GameLobbyServer` builds the `0x4313`
player list host-first (`GameService.GetPlayersAsync`, and the comment at
`RoomBrowserHandlers` records why). The gameplay server gets or creates its
configured character by name (default `server`) at startup, then uses that
database id both in the host-first player list and in
`HostIdentityService.PeerIdentifier`, which feeds the handshake reply. The
roster id and the reply's `peer_id` therefore stay the same without assuming a
fixed database id. The earlier `[U]` is resolved: this is not a gap the server
has to close. `[V]`

---

## 8. Reproducing the disassembly

```
# the whole machine, following the inline table at 0xaa1188
python3 tools/p2p_fsm_disasm.py MGO2.ELF

# any single function, past capstone's stop on inline data
python3 tools/ppc_function_disasm.py 0xaa131c MGO2.ELF 0x280
python3 tools/ppc_function_disasm.py 0x261fe0 MGO2.ELF 0x260   # session create
python3 tools/ppc_function_disasm.py 0xf015d0 MGO2.ELF 0x100   # channel poll
python3 tools/ppc_function_disasm.py 0x97f1b8 MGO2.ELF 0x120   # raise()
```

The two tools exist because a linear sweep stops at `0xaa1188`: Capstone halts
on the first word it cannot decode, and the six jump-table offsets are exactly
that. `p2p_fsm_disasm.py` hard-codes the six handler entries from the table and
walks basic blocks; `ppc_function_disasm.py` does the same for any entry.

Key addresses, all re-checked against the disassembly for this document:

| what | address |
| --- | --- |
| FSM tick / dispatch | `0xaa1140` (OPD `0x1208bd0`) |
| inline jump table (6 × i32 offsets) | `0xaa1188` |
| handler 0 / 1 / 2 / 3 / 4 / 5 | `0xaa11a0` / `0xaa11f4` / `0xaa131c` / `0xaa1560` / `0xaa15c8` / `0xaa1680` |
| raise convergence | `0xaa16c8`, call `0x97f1b8` |
| `0B09` raise site | `0xaa16c4` |
| dialog format string | `0xff37a0` (`(%04X:%08X)`) |
| poll channel `0x2b` / error | `0xf0d38c` / `0xf0cff0` |
| poll channel `0x2a` / error | `0xf0d3b8` / `0xf0d01c` |
| per-channel state / error | `0xf015d0` (`handle+0x168+4·ch`) / `0xefedd0` (`handle+0x370+4·ch`) |
| session create | `0x261fe0` → `0x268148`; slot stride `0x6d8`, 24 slots |
| session destroy | `0x261de0` |
| TCP `0x4320` send | `0xf1203c` |
| TCP `0x4322` send | `0xf10dec` |

---

## 9. Open questions

1. ~~**Who writes `ctx+0x7c`** (the descriptor's first word, and therefore the
   value the handshake reply is gated on).~~ **Resolved** — §7.1: the context
   constructor `0xaa1028` stores its `arg2` there, and the join path passes the
   first peer-table slot's id, which the player list makes the host. The value
   is therefore the host character id the join advertised, and the server
   resolves that id from its configured gameplay character. **[V]**
2. **Who sets the module instance's first word to `2`**, the precondition of
   `0x261fe0`. The client's own bring-up, not the server. **[U]**

2b. ~~**Whether the removed ESTABLISH OUT is needed after all.**~~ **Resolved,
   and the answer is yes.** §7.1 measured the reference host sending no keyed
   frame and the joiner keying up regardless, while the fake-host trial reported
   the opposite. A live join that stalls in state 2 with counters matching is
   what the removal predicted — it happened, and the join failed 35.0 s after the
   dial with `0B09`. The frame is back. **[V]**
3. **The `0x270e00` handoff codes** (`0x1002` / `0x2002`-family) and the
   `0x27e198` option indices `0xa1`/`0x52`/`0x58` — the shapes are certain, the
   meaning is not. **[U]**
4. **State 2's re-dial vs. state 5.** Whether a real client that never gets a
   keyed frame stays in the state-2 re-dial loop for the whole timeout or gives
   up earlier is not established here; the FSM admits both, and which one a
   live run takes is a capture away. **[U]**
