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
| 3 | `0x000003d8` | `0xaa1560` | readiness bit clear → state 4; set → countdown/failure |
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
   state 3 ──────  helper returns 0 ───────────────────────────▶ state 4
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

`0x281db0` reads bit `23` of the word at `global+0x88` and returns it as
`0x100` or `0`. **[V]** The branch is easy to misread: **zero advances to
state 4**, while nonzero decrements the 3000-tick countdown; expiry tears the
session down and enters the failure path. The bit's name/meaning is not
established by this branch alone.

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

There are two related state fields. The connect FSM uses `ctx+0x70`; the P2P
transport session uses `session+4`. The successful progression is:

```
state 0  send TCP 0x4320
state 1  read TCP 0x4321, build the endpoint descriptor, 0x261fe0 → state 2
state 2  the module session tick returns 8
state 3  readiness helper returns 0
state 4  net options 0xa1/0x52/0x58 applied → state 6 (finished)
```

The critical boundary is state 2. It waits for the transport session to report
its data phase, **state 8**. If state 2 times out, the connect FSM reports
failure; outer state 6 is reached only after state 8 and the later state-3/4
work. State 6 is the connect FSM's terminal success value, not the P2P
session's state 6.

The server-dependent requirements are:

1. **The `0x4321` reply must advertise a usable host and identity.** The
   descriptor's first word is `ctx+0x7c`, which the client gets from the
   host-first player list. The UDP reply's `peer_id` must equal that host
   character ID (not the joiner's). A mismatch is silently ignored, so the
   transport never promotes.
2. **The UDP reply must pass all handshake gates.** Its tail digest, peer ID,
   module magic and negotiated capability must be valid. Passing these gates
   accepts the reply and derives the session key, but **does not by itself
   prove that the transport reached state 8**.
3. **The client's reliable-record window must drain.** The module's state-8
   path requires both role bits and the promotion test at `0x268f18` to pass.
   The test checks the send/answer window described in §7.1b. The captured
   server answers each numbered client record with the same identifier plus
   `0x4000`, matching fourth byte, and a body length appropriate to its
   `0x8000` class bit (§7.1c). The latest failed live join received empty
   bodies for one-byte-required answers; the current server now sends a
   one-byte answer for that class. A successful live join after this correction
   is still needed to confirm that the window drains in the deployed path.

### What the gameplay server sends (implemented)

`src/GameplayServer/Commands/PeerCommandHandlers.cs` (`AcceptHandshakeHandler`)
sends the two frames the capture holds, in order:

* the keep-alive **pre-keyed** at counter 0;
* the handshake reply **pre-keyed** at counter 1;

then sets `Session.Established = true` so that the roster response and later
traffic use the session key and keyed digest on the shared outbound counter.
It does not send a separate keyed keep-alive after the handshake; the first
keyed host response is sent when the client submits a valid profile and
receives the roster.

The earlier fake-host trial showed that a session-keyed keep-alive stopped
handshake re-dials, but that observation is not evidence of state-8 promotion.
The later live failure reached the reply-accepted/keyed phase yet remained in
outer state 2, then timed out. The disassembly identifies the additional
reliable-record-window promotion gate (§7.1b); the subsequent live attempt
showed the server's empty answers did not advance that window (§7.1c). The
current server sends one-byte answers for the affected record class, matching
the capture's shape. This correction still needs a successful live join to
verify.

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

An earlier reading treated this rejoin as an unconditional state-8 store. That
is wrong: it omits the branch through `0x26ab58` before `0x26907c`. The
acceptance path installs the peer identity/key and sets flags `0x4` and `0x2`;
the separate promotion pass sets bit `0x1` and stores state 8 only after the
reliable-record window test succeeds. The reference capture proves keyed
traffic follows the accepted reply, but keyed traffic alone does not prove
state 8. The later live run remained in outer state 2 and failed with `0B09`,
which is consistent with the promotion gate remaining closed. See §7.1b–d.

---

### 7.1b Acceptance is not the promotion — the gate §7.1 called "the same call"

§7.1 walks the accept path as if it stored state 8 in the call that accepts the
reply. Re-read instruction by instruction, it does not: the accept block sets
`flags |= 4`, `|= 2`, folds the capability in and sets `r23`, and the **state-8
store is a separate pass**, reached only when the state dispatch is re-entered and
one more test says go.

```
00268c00: beq      cr7, 0x268e20      ; flags bit 0x2 clear -> read the peer's record (the reply)
;; accept block 0x268e20 .. 0x269068: 0x268f6c ori 4 (accepted), 0x268fb0 ori 2 (role),
;; 0x269044-64 capability fold, 0x269048 li r23,1; 0x269068 b 0x268ea0 (back to the queue loop)
00268c04: lbz      r9, 4(r28)         ; state, on the next pass
00268c0c: cmpwi    r0, 3 ; beq 0x268cac
00268c14: cmpwi    r0, 4 ; beq 0x268c94   ; dial/launch session state
00268c1c: cmpwi    r0, 2 ; beq 0x268c94
00268c94: clrlwi   r0, r11, 0x1f       ; flags & 1   (the other role bit)
00268c9c: beq      cr7, 0x268f04       ; clear -> the promotion test
00268f04: addi     r3, r24, 0x48
00268f0c: bl       0x26ad58            ; lookup(the handle at session+0x48)
00268f18: bl       0x26ab58            ; 0 = go, -1 = no
00268f20: cmpwi    cr7, r3, 0 ; beq 0x26907c
0026907c: lhz r11,0x14(r28) ; ori r0,r11,1 ; sth   ; flags |= 1   <-- only on "go"
00268c44: clrlwi   r0, r11, 0x1e       ; flags & 3
00268c4c: cmpwi    r0, 3
00268c58: stb      8, 4(r28)           ; state := 8
```

`0x26ab58` is three instructions of substance: it returns **0** when the object
looked up is null or when its byte `[0xb]` equals its byte `[0xc]`, and **-1**
when they differ.

```
0026ab58: cmpdi r3, 0 ; bne 0x26ab70
0026ab70: lbz   r0, 0xc(r3)
0026ab74: lbz   r9, 0xb(r3)
0026ab78: cmpw  cr7, r9, r0 ; bne -> -1
```

The object is created by `0x261ab0` (handle out-parameter), its fields are written
through `0x26b2b0(&handle, selector, value)` — selectors 0, 1, 2 and 3 write bytes
`[0xa]`, `[0xd]`, `[9]` and the halfword at `[4]`, and **none of them touches
`[0xb]` or `[0xc]`** — and the session's `+0x48` slot is filled from those handles
in the send builders (`0x268478`, `0x2686d0`, `0x268734`, `0x26879c`) and cleared
to `-1` at `0x2682cc`.

The two bytes are a **sliding window over the records this session builds**.
`0x26b190` initialises `[0xb]`, `[0xc]`, `[0xd]` and `[0xe]` from the same
register, so they start equal; the record builder at `0x26b0a4` then reads them as
a window and stamps the outgoing record's fourth byte with `[0xb]`:

```
0026b0dc: lbz   r9, 0xb(r31)      ; the sequence being handed out
0026b0e0: lbz   r0, 0xc(r31)      ; the window's far end
0026b0e4: addi  r11, r9, 1
0026b0e8: subf  r0, r0, r9        ; r9 - r0 = how far the window is open
0026b0f0: cmplwi cr7, r0, 0x17    ; more than 23 out -> refuse this record
0026b104: stb   r9, 0x7a(r1)      ; the record's fourth byte := [0xb]
0026b108: stb   r11, 0xb(r31)     ; [0xb] := [0xb] + 1
```

Four builders repeat that tail — `0x26afd8`, `0x26b848`, `0x26b988` and the
`0x26b108` above — one for each record family they build. So `[0xb]` is the
sequence this session hands to the next record it builds and `[0xc]` is the far
end of a window 24 records deep, and the promotion test at `0x268f18` asks
whether that window has drained: whether every record this session built has been
answered. **[V]** for the instructions. It is the single gate between the live
join and state 8 — `0x268f24`, the branch on `0x26ab58`, is the only path to the
state-8 store — and §7.1's "stores state 8 in one call" should be read as
"reaches the state-8 store once this test passes".

What advances `[0xc]` is the **answer path**, and it is located. The store is
`0x2676e0`, on the receive side, on the object `0x261438` returns at `0x266f14`,
and its arithmetic is the builder's in reverse — `[0xc]` and `[0xd]` move by the
same amount in opposite directions, so what one gains the other loses:

```
002676b8: lbz  r0, 0xa(r28)      ; the sequence the matched entry names
002676bc: subf r0, r21, r0       ; r21 = [0xc], so r0 = named - [0xc]
002676c4: cmpw cr7, r26, r0
002676cc: mr   r26, r0           ; r26 := min([0xd], named - [0xc])
002676d8: add  r9, r26, r21      ; [0xc] + r26
002676e0: stb  r9, 0xc(r23)      ; [0xc] := [0xc] + r26
002676e4: subf r0, r9, r0        ; ([0xc] + [0xd]) - [0xc]_new
002676e8: stb  r0, 0xd(r23)      ; [0xd] := [0xd] - r26
```

So `[0xc]` is the sequence the window has been answered up to and `[0xd]` is what
is still outstanding; their sum is invariant, which is what a sliding window
means, and it is the peer's answers that move `[0xc]`. That settles the earlier
hesitation about the two objects being different: a builder that refuses the
24th unacknowledged record would be handing out sequences nothing ever released
if the receive path did not release them. The entry the receive path reads its
sequence from (`[0xa]`, matched by the low twelve bits of its first word and the
record's fourth byte) is the bookkeeping the sender kept for the record it built.
**[V]** for the instructions, **[I]** for the identification of the two objects
as one — no type table exists in the image to confirm it, and the arithmetic is
what stands in for one.

#### The measurement that forced this reading

A live join against the deployed k3s gameplay server (2026-10-08, image
`87a3d9a`, logs kept out of the repository) took the "no" branch — and it is the
first run that shows *which* gate holds it:

* **The reply was accepted.** The client's own key came from the reply's
  `counter_base`: the session key our server derives for the client's frames
  (`0x9bdda542 ^ 0x12345678`) verified on every keyed frame the client sent, and
  that base is stored (`0x268c30`–`0x268c40`) only on the accept path, behind all
  three gates. So gates 1–3 passed and `r23` was set.
* **The join never reached the data phase.** The client sent its profile once,
  never re-sent it, and its next 25 s are 17-byte one-byte-body records
  (`0x9001` flags 1 / `0x1001` flags 2…5) at about one a second. Nothing from the
  data phase. The FSM's state 2 timed out and `0B09` was raised ~39 s after the
  dial, which is the 6000 + 4500 countdown with no re-dial in between.
* That is exactly the path above with the promotion test answering no: the key
  installed (`r23`), `flags & 3 == 2`, no state-8 store.

So `P2P_CONNECT_FSM.md` §7's "acceptance carries the module into state 8 by
itself" is too strong, and so is the trial reading that only the keyed frame
matters: with the frame present the join still stops here. Section 7.1's
measurement of the reference stands, but it measures a client that *did* pass this
test.

#### Divergences from the reference left open

These are the differences between our live join and the reference join. The
endpoint and role rows are open; the joiner's own handshake row is what the
2026-10-09 join did instead of the reference's, and the rows marked **fixed**
were measured in a later join and corrected.

| what | reference | ours |
| --- | --- | --- |
| reply's endpoint pair | host public, then host private (`99.66.131.177:5731`, `10.104.10.28:5731`) | the same public address twice — re-measured 2026-10-10, byte for byte: `0x4321` carries `89.129.16.203:5731` in both endpoint fields |
| reply fields | `peer_id 0xa001`, magic, capability `02`, `[19..21) 1`, count 2 | `peer_id 1`, magic, capability `02`, `[19..21) 1`, count 2 |
| reply's trailing bytes (the 43-byte payload ends `canRate`, `currentGame`, one more) | `00 01 01` | `01 00 00` — canRate set for the joiner, `currentGame` written as 0 |
| the joiner's own handshake | capability byte `02`, `[19..21) 2` | capability byte `03`, `[19..21) 1` |
| the joiner's non-profile records | `0x1001` one-byte answers, then one `0x9001` per sequence, `1`, `2`, `3` … | 18 one-byte `0x9001`, fourth bytes `1`…`0x12`, one every ~5 s: its sequence climbing while its window's base stands still |
| the host's answers to `0x9001` | `0xd001` with **one** body byte | `0xd001` with **none** (2026-10-09 run) — **fixed**: `PeerAcknowledgementUtils` builds answers with the body length the captures give, §7.1c |
| the burst's fourth bytes | `3 4 5 6` over a one-player roster | `0 0 0 0` (2026-10-09 run) — **fixed**: `RoomRosterService.NextRunOrdinal` continues the run's numbering |

The joiner's own handshake is built from the session descriptor our TCP `0x4321`
reply filled in, so its two differing bytes point at that reply rather than at the
UDP channel — §7's requirement about the `0x4321` payload is worth re-checking
field by field.

**`[19..21)` is worth the most attention.** It is the one field where our joiner
and the reference joiner disagree *about themselves* (`1` against `2`), and it is a
small integer rather than an address. A first reading gave its reader as
`0x268f40`, which tests `session[5] == 1` and then stores state **3** instead of
following the rest of the promotion path to 8, with `session[5]` taken for the
client's own mode. Re-read with `tools/mgo2_disasm.py` it is not: `session[5]` is
the session constructor's **class argument**, and the join dial passes a literal
`4` (§7.1d). The mode byte is a different field, and that test is not what holds a
joining client back. **Measured 2026-10-10 across the three readable rounds
(`UDP_GAME_CAPTURE.md` §8.3): the field is `2` on every joining client and `1` on
every host, and the deployed gameplay server's own frame dump agrees — the live
joiner sent `1` with capability `03`.** The source is traced below, and it is not
in our `0x4321` reply, our `0x4313` player list or the room record — no field of
ours can name it. **[V]** for the value, **[U]** for the input that makes a client
choose it.

#### Where both bytes come from, disassembled (2026-10-10)

Neither byte is a field the server sends. Both are read out of the client's own
state by the handshake builder at `0x268d40`, which is entered from the
queue-drain at `0x268ba8` and writes the handshake record field by field:

```
00268d40: lhz   r11, 0x14(r28)          ; r11 = the session's flags halfword
00268d44: rlwinm r9, r11, 0, 0x17, 0x17 ; test bit 8
00268d48: li    r0, 2
00268d50: stb   r0, 0x70(r1)            ; capability := 2
00268d54: beq   cr7, 0x268d60           ; bit clear -> keep 2
00268d58: li    r0, 3
00268d5c: stb   r0, 0x70(r1)            ; bit set   -> capability := 3
00268d60: rlwinm r0, r11, 0, 0x14, 0x14 ; test bit 11
00268d68: beq   cr7, 0x268d78
00268d70: ori   r0, r0, 4               ; capability |= 4
00268d78: addi  r4, r1, 0x70
00268d84: bl    0x26cc10                ; write 1 byte -> capability [18]
00268d8c: addi  r4, r27, 6
00268d9c: bl    0x26cc10                ; write 2 bytes -> mode [19..21)
```

* **The capability is derived from the session's own flags** — `2`, or `3` when
  flag bit `0x100` is set, with bit `0x4` OR-ed in when `0x800` is set. It is the
  same field the accept path folds into flags at `0x269064` (§7.1d). So `02` against `03`
  distinguishes two *session states*, not two servers.
* **The mode comes from `r27 + 6`**, and `r27` is the object `0x261730` returns,
  which is three instructions: `*(module_globals) + 0x74`. The mode is therefore
  the u16 at **`module_instance + 0x7a`**, which resolves the earlier note that
  called it `module_base + 0x7a` with no trace. Its only writer in the image is a
  four-instruction setter at **`0x261718`**, published once through the OPD slot
  at `0x11f0d58`, so the value is client state written by the client's own
  bring-up. **No server packet carries it**, which is why a capture comparison of
  the TCP channel alone cannot name the trigger; the caller of `0x261718` is
  reached indirectly (there is no `bl` to it and no `lis`/`ori` building its OPD
  address), and locating it is the open item.

**`AcceptHandshakeHandler` now logs `capability` and `mode`** for every handshake
it parses, so the next live join records the role the client presented in the
server's own log rather than in another capture. `FrameBuilderUtility.ParseHandshakeBody`
already read both; only the log line was missing them. **[V]**

#### What the client's own game object decides it from (2026-10-10)

`0x261718` has exactly two callers in the image, and both apply the same rule.
`docs/analysis/module-sweep.txt` carries them decompiled:

```c
/* FUN_00ea1bb0, the room-entry step */
iVar1 = (*(code *)**(undefined4 **)(**(int **)(param_1 + 0x41c0) + 8))
                  (*(int **)(param_1 + 0x41c0), &uStack_6c);
if (iVar1 == 2) return;              /* not settled yet, poll again  */
*(int *)(param_1 + 0x80) = iVar1;    /* the state the mode is set from */
...
_opd_FUN_0027e578(uVar2, 0x100, 2, param_1 + 0x41ae);
if (iVar1 == 1) {
  _opd_FUN_00261718(2);              /* state 1 -> mode 2, a joiner  */
}
else {
  _opd_FUN_00261718(1);              /* anything else -> mode 1, host */
}
```

```c
/* FUN_00ea2570, the poll step */
piVar5 = (int *)_opd_FUN_00d7d9c0();
param_1[0x1070] = (int)piVar5;                 /* ctx+0x41c0 */
if (*(short *)((int)param_1 + 0x41ae) == 0) {  /* my own port is unknown */
  param_1[0x20] = 5;                           /* -> state 5 -> mode 1  */
  return;
}
iVar6 = (*(code *)**(undefined4 **)(*piVar5 + 0xc))(piVar5, *(short *)((int)param_1 + 0x41ae));
param_1[0x20] = iVar6;
```

Three things follow, and none of them is a byte on the wire:

1. **The rule is `state == 1` → mode `2`, else mode `1`.** `param_1[0x20]` and
   `param_1 + 0x80` are the same field (a `u32`), and the states the code writes
   into it are `1`, `2` (retry), `3`, `4` and `5`. So mode `2` means "the client
   ended up a guest in someone else's game"; every other outcome, including the
   `port == 0` fallback that stores `5`, means mode `1` — with no server packet
   involved at all.
2. **The state is the return of a virtual call on a lazily-built singleton.**
   `ctx+0x41c0` holds the object `0x00d7d9c0` returns: a **0x30160**-byte singleton
   — `lis r4, 3` and `ori r4, r4, 0x160` at `0xd7d9d0`, so the size the earlier
   note read as `0x3160` was missing the `lis` — allocated on first use with its
   vtable stored from a module global. Its method
   at vtable `+0xc` is entered with the port at `ctx+0x41ae` (a `0x100` net option)
   and its method at `+0x8` is polled for `(state, address-string)`.
3. **The same call supplies the address this client dials.** The 16-byte string it
   returns is copied to `ctx+0x418a` / `ctx+0x419a` and parsed by `0x00fbb79c` into
   the P2P endpoint — the same string-to-address parser the connect FSM uses on the
   `0x4321` reply at `0xaa1468`. So the role and the dial address are produced by
   one object, which is why the mode tracks the room the client believes it is
   entering and not the handshake it sends.

The question that paragraph names is answered below, and the answer is a negative
one: the mode is the client's own role code, written by its own UPnP/NAT module
into its connection record and read back over an *internal* message. No packet we
send, and no pass of our own, takes part.

#### The role value, traced from the handshake byte to the table (2026-10-10)

| step | where | what |
| --- | --- | --- |
| the mode the handshake carries | `0x261718` | `module_instance+0x7a`, read into `[19..21)` by the handshake builder `0x268d40` |
| its only caller on the join path | `0xea1df0` | `mode := (ctx[0x80] == 1) ? 2 : 1` — `li r3, 2` at `0xea1ef0`, `li r3, 1` at `0xea1dfc` |
| `ctx[0x80]` | the room object's poll | vtable `+0x8` → OPD `0x01211820` → code `0x00d7e7a8`, called at `0xea1bf4` |
| the poll's states | field `this+0x30010`, table `0x00d7e87c` | `0` → return `0`, `> 5` → return `2`, `1..5` → one table entry each |
| the only `1` it can return | states 3 and 4 | `this+0x3001c`, filled by the `+0xc` method in state 1 |
| that method | vtable `+0xc` → OPD `0x01211790` → code `0x00d7d530` | called at `0xd7e958` with `this+0x30154`; its result stored at `0xd7e968` |

`0x00d7d530` is eleven instructions and one return, and it is the whole decision:

```
r4 = arg & 0xfcff                ; 0xd7d538-3c: bits 0x100/0x200 are flags, not value
if (r4 == 0x10) return 0         ; 0xd7d544
if (r4 <  0x10) return (r4 > 2) ? 1 : 3     ; 0xd7d550 -> 0xd7d568
if (r4 == 0x90) return 0         ; 0xd7d554
return 1                         ; 0xd7d558
```

So the poll answers `1` — "this client is a guest" — for **any value outside
`{0, 1, 2, 0x10, 0x90}`**, and every other outcome the poll can produce (`0`, `2`,
`0x1532`, `0x2000`, and those five values through a state that returns them)
leaves the mode at `1`. A 1200-instruction sweep of the poll holds no `li r7, 1`
at all, so `this+0x3001c` is the only source of the one value that matters.

`this+0x30154` is the third halfword of the peer block the object keeps at
`this+0x30130` — `addr1` (16 bytes), `addr2` (16), `port1` (2), `port2` (2),
`port3` (2) — and that block is filled from the client's **own connection record**
by internal messages, not by a packet. The connection object publishes them at
`0xf4c68c`–`0xf4c898`: `0x2103`/`0x2104` carry the two addresses from `+0xa0c` and
`+0xa10`, `0x2105`/`0x2106`/`0x2102` the three halfwords from `+0xa14`, `+0xa16`
and `+0xa18`, and each of the six is skipped when its source is absent — the two
addresses when the value is `-1` (`0xf4c694`, `0xf4c6dc`), the three ports when it
is `0` (`0xf4c724`, `0xf4c76c`, `0xf4c844`). The receiver writes them at
`0xd7e6a4`, `0xd7e6dc`, `0xd7e798` and `0xd7e6e8`.

**`+0xa18` is therefore the value, and the mrd module writes it three ways:**

| writer | value | when |
| --- | --- | --- |
| the connection initialiser `0xf4bc30` | `0` | a fresh record, with `+0xa00 = 0x1102`, both addresses `-1` and all three ports `0` |
| `_opd_FUN_00f4ebc8`, the UPnP port bind (`mrd/upnp_port_bind.c`, `mrdUPnP_test2`, `less_than_1024_port_%d`) | **`2`** | the bind produced a port `>= 0x400`, `+0xa84 == -1`, the state is `0x2202`, `+0xac8 != 0`, and the `0x2301` task answers something other than `0x1201` (the code this module returns while a task is unfinished) — `0xf4eb78`, and again at `0xf4ecfc` |
| `0x00f5b0f8`, the NAT task driver (`0x2301`, `0x2303`, `0x2304`; fields read by name as `RESULT`, `MAPPED-ADDRESS`, `true`, `false`) | **`0x10`, `0x30`, `0x90`, `0xd0`** | `0xf5c8b8`–`0xf5c8e8`: a four-entry table indexed by `r25 + 2·bit`, `r25` the parsed boolean (`1` when the value equals `true`, `0` when `false`) and the bit one bit of `abs((frame+0xb8) ^ parsed)` |

The two sets line up with the classifier exactly:

| `+0xa18` | classifier | the handshake carries |
| --- | --- | --- |
| `0` (never set) | `3` | mode `1`, host |
| `2` (the port bind succeeded) | `3` | mode `1`, host |
| `0x10`, `0x90` (the `true` half of the table) | `0` | mode `1`, host |
| `0x30`, `0xd0` (the `false` half) | `1` | **mode `2`, guest** |

A value that is none of those — a real port such as `0x1662` — is `1` as well, so
the guest test is "anything but the four role codes and the two small numbers".
**[V]** for every address, value and branch above, and for the parse that
distinguishes `true` from `false` (`0xf4f2b8` is a case-insensitive compare that
returns `0` on equality). **[I]** for the step from that boolean to *which* NAT
state it describes: the field is one of a UPnP/NAT response and the two halves of
the table are one enum, but the field's name is not carried into the code that
reads it. **[U]** for the bit that picks between the two host codes (`0x10`
against `0x90`) and between the two guest codes (`0x30` against `0xd0`).

**What this means for the join.** The byte our joiner sent is not a value the
server can name: it is the client's own P2P role, computed by the client's own
UPnP/NAT module into its connection record and read back over an internal
message. Our `0x4313` and `0x4321` payloads, and the room record behind them, are
never consulted. So the divergence between our live join and the reference is a
client-side one — the reference's joining client resolved to a guest code and
ours to a host code — and the check that closes it is on the client: a client
whose port-mapping state resolves the other way presents mode `2` against the
same server. **[V]** for the mechanism; **[U]** for which of the six values that
client's record held.

### 7.1c The records that pair up — a reading, and what a live join did to it

§7.1b once left the promotion gate's two cursors unidentified, and this section
was written to fill them in: the host's records whose fourth byte matches the
joiner's, read as answers that drain the joiner's send window. **Both halves hold.**
The cursors are identified (§7.1b) and the records do pair up as answers; what
went wrong in between is the shape of the answer, which a live join measured and
which the section keeps, because the answer that was served and the answer the
captures show differ by one byte and by nothing else.

Every session record a peer sends carries its sequence in its fourth byte, and a
record of **the same identifier with class bit `0x4000` set** and the same fourth
byte follows it:

| a record of | is answered by | with |
| --- | --- | --- |
| `0x1001` | `0x5001` | an empty body |
| `0x9001` | `0xd001` | **one** body byte |

and a record that already carries `0x4000` is not answered again, which is why
`0x5001` and `0xd001` are senders and never the thing answered. The earlier
version of this table listed `0x5001` → `0x9001` as a third pair; that is the
same mistake as reading the pairs as a cadence — `0x5001` carries `0x4000`, so
by the rule it is answered by nothing, and the `0x9001` records opposite it are
the joiner's own next sequence.

Measured over the reference round (`mgo2-game.pcapng`, joiner `10.2.0.2:5730`):
191 joiner `0x9001` records and 176 host `0xd001`, 49 `0x1001` and 60 `0x5001`,
481 `0x5001` and 71 `0x9001`. Two further captured rounds (`mgo2-game2.pcapng`
and `mgo2-game3.pcapng`, host `143.47.227.126`, ports `5721` and `5720`) show the
same counts and the same reply bit: 87 joiner `0x9001` records and 86 host
`0xd001` in game2, 14 and 14 in game3, plus `0x1001`→`0x5001` in both. Records that
already carry `0x4000` were not counted again, and tick records (below `0x1000`)
are not numbered at all. **[V]** for the counts and the identifiers; what the check
at the bottom of this section makes of the pairing is there, and it holds.

> **Reading those two captures.** Their host keys differently from the reference:
> its own direction verifies with the bare pre-key constant and not with a
> session key, while the joiner's verifies with `joiner_base ^ host_base` as
> usual, and its handshake reply is a 31-byte frame rather than 44. A decoder
> that assumes a 44-byte reply and a session-keyed host, as `udp_flow_summary.py`
> does, finds only the joiner's half of these captures and derives no key at all.

**The reading was right and the answers were wrong.** Serving them was tried on
2026-10-09: the gameplay server (image built from `f2e33e9`) answered every
numbered record with `T | 0x4000` and the same fourth byte, and that join is the
one whose client sat in state 2 — so this section first rejected the reading. The
error is in the shape of the answer, not in the pairing. **Every answer to a
record carrying `0x8000` carries one body byte, and ours carried an empty body** —
no `0xd001` in any of the three rounds has zero bytes, and none of the six
directions of answer in the reference's opening exchange does either:

```
 t+4.170 host -> 5001 b4=0  9001 b4=0  1001 b4=1  1001 b4=2
 t+4.226 join -> d001 b4=0 body=03 , 5001 b4=1 , 5001 b4=2 , 9001 b4=1 body=0a
 t+6.687 host -> d001 b4=1 body=24 , 9001 b4=3 , 1001 b4=4 , 1001 b4=5 , 1001 b4=6
 t+6.751 join -> d001 b4=3 body=23 , 5001 b4=4 , 5001 b4=5 , 5001 b4=6 , 9001 b4=2 body=0c010000 00
```

The fourth bytes pair one for one — `9001 b4=0` is answered by `d001 b4=0`,
`1001 b4=4` by `5001 b4=4`, and so on through every record of the exchange — and
the body lengths follow the identifier's `0x8000` bit in all six. Checked over
both readable rounds as a rule rather than read off one exchange:
`tools/probe_ack_rule.py` pairs 2803 of the reference's 2952 answers and all 1025
of game2's with a record the other side sent, and **every one of those 3828 carries
exactly the body length the rule gives**. Of the reference's 250 answers, 239 pair
(11 do not, all of them to a `0x1001` whose fourth byte the joiner never sent); of
the joiner's 2702, the 138 that do not pair are steady-phase answers to the slot
records, where the fourth byte of the answer stops tracking the record answered.
**[V]** for the join window, which is where the host's answers decide the join.

The count argument this section used to make — 191 joiner `0x9001` against 176
host `0xd001`, 87 against 86, 14 against 14 — does not survive the pairing: an
answer summarises records already answered, so the two counts need only agree
roughly and drift behind by whatever is in flight at the end of a capture. The
body-value argument was sound and is answered by the same table: the byte is the
acker's own control value, `0x02`/`0x03`/`0x24`/`0x23` in the reference round, and
nothing needs to be derived from the record answered. Only the length has to be
right, and the length is the part that was wrong.

**What the live run then shows is the window, not the absence of an answer.** In
that run our server answered each `0x9001` the client sent, within a millisecond,
with `d001 b4=<same>` and an *empty* body. The client did not treat them as
answers: it went on building records — `9001` of one byte, fourth bytes `1`, `2`,
`3` … `0x12` in order, ~5 s apart — and reached 18 before the join was given up,
which is its `[0xb]` climbing a step per record while `[0xc]` stood still. 18 is
still inside the 24 records the window allows (§7.1b), so what it ran into first
was the timeout, not the window's limit; nothing it sent after the first record
was from the data phase, and it never sent the `0x5001` answers of the roster the
reference's joiner sends. **[V]**

So a host that answers the client's records in the shape the captures show is
still the one candidate that accounts for all of it, and the served-and-failed
run is evidence *for* the mechanism once the answers are read beside the
capture's: `[0xc]` is moved by an answer, the answers served could not move it,
and the window ran out. Whether the one-byte body is what the client matches on —
as opposed to its own bookkeeping rejecting a plain-text-less record — is not
separated here. **[U]** for that last step; the fix itself is in
`PeerAcknowledgementUtils`, and what settles it is another live join.

### 7.1d What the receiving side does with the byte — and what `session[5]` really is

§7.1b once read the field through `0x268f40`, which tests `session[5] == 1` and
stores state **3** instead of the rest of the promotion path, and took `session[5]`
for the client's own mode. Re-read with `tools/mgo2_disasm.py` (Capstone, no Ghidra
project), both halves of that are measured, and the second is wrong.

**The byte arrives, is stored, and is never tested.** The accept block reads the
peer's record field by field, and each read refuses the record unless it returns
the whole count:

```
00268ed0: mr    r3, r31            ; the received record
00268ed4: addi  r4, r1, 0x74
00268ed8: li    r5, 4
00268edc: bl    0x26cc88           ; 4 bytes -> r1+0x74    gate 1: == session[0x18]
;; 0x268f8c 4 bytes -> r1+0x7c     counter base -> session[8], key -> session[0xc] (0x268c30)
;; 0x268fac 4 bytes -> r1+0x78     module magic, compared with the module's own
;; 0x268ff0 1 byte  -> r1+0x70     capability, folded into flags at 0x269064
00269024: addi  r4, r24, 0x1e      ; r24 == r28 == the session (0x268be4/0x268bf4)
00269030: li    r5, 2
00269034: bl    0x26cc88           ; 2 bytes -> session+0x1e   ◀── the mode
0026903c: cmpwi cr7, r3, 2 ; bne-  0x2690d0   ; the *count* is what is tested
```

`0x26cc88` is the record reader, not a writer: it returns
`min(remaining, requested)` and copies a byte loop to `r4`. So the byte our reply
carries lands at `session+0x1e` and the tests around it are on the read's length.
In `0x260000`–`0x27a000`, the module that owns this object, the only reads of
`+0x1e` are struct copies (`0x26033c`, `0x26c360`), and no code compares a
session's peer mode with the client's own at `module_instance+0x7a`. **The client
decides no role from either byte.**

**`session[5]` is the session's class, and the join dial's is `4`.** `0x2681ac`
writes the constructor's third argument into it (`stb r5, 5(r27)` with `r27 = r3`,
the slot being initialised), and `0x261fe0` forwards its **second** argument there
(`mr r11, r4` at `0x261ffc`, `extsw r5, r11` at `0x26206c`). At every direct call
site in the image:

| site | caller | class passed |
| --- | --- | --- |
| `0xaa1528` | the connect FSM's dial | `4` — `li r4, 4` at `0xaa1518` |
| `0x8ee48c` | the game module's room entry | `4` |
| `0x270f74` | the room module, the room owner's own session | `2` |
| `0x268024` | this module's own answer path | `5` |
| `0x276cb0` | the room module, a per-peer session | `0`, or `1` when reached from `0x276e28` |

A class-`1` session therefore exists — the room module creates one — but the
joining client's dial uses `4`, so `0x268f40`'s `== 1` never holds for it. The
class byte and the mode byte share the value `2` at one site each, which is what
made the two look like one field. **[V]** for the instructions, the call sites and
the argument each passes; **[U]** for what a class means.

**The promotion itself, unchanged.** State 8 — the data phase the connect FSM's
state 2 waits for — is stored when both role bits are set:

```
00268c44: clrlwi r0, r11, 0x1e    ; flags & 3
00268c4c: cmpwi  cr7, r0, 3 ; bne
00268c58: stb    r0=8, 4(r28)     ; state := 8
```

Bit `2` comes from the accept (`0x268fb0`), bit `1` from the window pass once
`0x26ab58` reports the 24-record window drained (`0x26907c`), and neither depends
on the mode or the class byte.

**What the live run says.** The deployed gameplay server logs every inbound frame
with its bytes, and its own record of the join agrees with the capture
comparison: the joiner's handshake at `2026-10-09 23:30:49` decodes to
`peer=0x00000002 base=0xb712cbf1 cap=0x03 mode=1 pairs=2` — a host's mode from a
joining client, against the `2` every joining client sends in
`mgo2-game*.pcapng` (the joins at `99.66.131.177:5731`, `98.26.168.27:5730`,
`143.47.227.126:5721` and `:5720`). Both sides of our wire present mode `1`, and
there is no tie to resolve: the field is recorded and not read. **[V]**

---

### 7.1d The answer's *timing* — the other thing the captures refuse

The body length was one half of the answer. The other is *when* it is written,
and it is measured the same way. Pairing every answer in the reference round
with the record it answers — same identifier with `0x4000` set, same fourth byte,
same direction — gives 2 801 pairs (`mgo2-game1.pcapng`, joiner
`10.2.0.2:5730` ↔ host `99.66.131.177:5731`, session key `0x4fed1670`):

| latency from the record to its answer | s |
| --- | --- |
| shortest | **0.0130** |
| 10th percentile | 0.3336 |
| median | 1.1093 |
| 90th percentile | 3.7879 |

**Nothing answers in the millisecond its record arrived.** A console host
dequeues on one pass, acts, and writes on the next, so the floor is a tick and
the observed floor is 13 ms; 102 of the 2 801 land inside 50 ms and the body
lengths follow §7.1c again from the other side (2 619 answers with no body, the
182 answers to `0x8000`-class records with one byte).

Our host answered **inside the dispatch that received the record**, which is why
the served-and-failed join of §7.1c looked the way it did: the client's own
bookkeeping for the record it had just built — the entry `[0xc]` is advanced
from — is written on its next pass, and an answer already in flight before that
entry exists has nothing to match. Two live joins, one with the wrong body
length and one with the right one, both climbed their sequence without the base
moving, and both got their answers in under a millisecond.

The correction is `PeerAnswerSchedulerService`: the answers a frame's numbered
records are owed are queued and written **300 ms** later — the capture's tenth
percentile rounded down, inside the measured envelope rather than at its edge —
not awaited, so the dispatch loop keeps reading every other peer of the room.
The counter is still the session's, so an answer goes out on the sequence after
whatever the host wrote meanwhile, which is what the recorded host's own frames
show.

Both halves of the answer are now the captures': the length (§7.1c) and the
timing. What confirms it is still a live join — the promotion gate at `0x268f18`
is the thing to watch, and a client that reaches the data phase is the only
measurement that closes it.

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

# the role chain: the code each vtable slot really names (through its OPD), with
# every off(r2) operand resolved to the data it reads
python3 tools/ppc_vtable_dump.py 0x011db824 --slots 6
python3 tools/ppc_vtable_dump.py 0x011db824 --disasm 0xc --count 14   # the classifier
python3 tools/ppc_function_disasm.py 0xd7e7a8 MGO2.ELF 0x1000  # the poll's dispatcher
python3 tools/ppc_function_disasm.py 0xea1bb0 MGO2.ELF 0x300   # the mode set
# the poll's five state handlers, from the table at 0xd7e87c
python3 tools/ppc_function_disasm.py 0xd7e894 MGO2.ELF 0x120   # state 1
python3 tools/ppc_function_disasm.py 0xd7e9fc MGO2.ELF 0x60    # state 4, which returns +0x3001c

# §7.1d: what the receiving side does with the mode byte, and the session class
python3 tools/mgo2_disasm.py 0x268f80 0x269070 MGO2.ELF      # the accept block's field reads
python3 tools/mgo2_disasm.py 0x26901c 0x269048 MGO2.ELF      # the 2-byte read into session+0x1e
python3 tools/mgo2_disasm.py 0x2681a8 0x2681b4 MGO2.ELF      # the class store
python3 tools/mgo2_disasm.py 0x26206c 0x262078 MGO2.ELF      # the class argument
python3 tools/mgo2_disasm.py 0x268f30 0x268f58 MGO2.ELF      # the session[5] == 1 branch
python3 tools/mgo2_disasm.py 0x268c44 0x268c5c MGO2.ELF      # the state-8 store
```

The first two tools exist because a linear sweep stops at `0xaa1188`: Capstone
halts on the first word it cannot decode, and the six jump-table offsets are
exactly that. `p2p_fsm_disasm.py` hard-codes the six handler entries from the table
and walks basic blocks; `ppc_function_disasm.py` does the same for any entry; and
`ppc_vtable_dump.py` is the third step a vtable needs on this machine, where a
slot holds an official procedure descriptor rather than a code address — it reads
the descriptor, and resolves every `off(r2)` operand of the code it names, which
is how `+0xa18` was found.

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
| module TOC (`r2`) | `0x01222a00`, from the entry descriptor at `0x011e7368` |
| the room object's vtable | `*(0x011db824)` = `0x0119e490` |
| vtable `+0x8` / `+0xc` | OPD `0x01211820` → `0x00d7e7a8` / OPD `0x01211790` → `0x00d7d530` |
| the poll's per-state table | `0x00d7e87c` (six i32 offsets) |
| the mode store | `0xea1df0`, `li r3, 2` at `0xea1ef0` and `li r3, 1` at `0xea1dfc` |
| the role's slot on the object | `this+0x30154`, in the peer block at `this+0x30130` |
| the connection record's role field | `+0xa18` (with `+0xa0c`/`+0xa10` addresses and `+0xa14`/`+0xa16` ports) |
| the messages that fill the peer block | `0x2102`/`0x2105`/`0x2106` built at `0xf4c88c`/`0xf4c754`/`0xf4c79c`, received at `0xd7e6a4`/`0xd7e6dc`/`0xd7e798` |
| the role's writers | `0xf4eb78`, `0xf4ecfc` (the literal `2`), `0xf5c8b8`–`0xf5c8e8` (the four codes) |
| received mode lands at | `session+0x1e`, read at `0x269024` with the cursor reader `0x26cc88` |
| the session's class store | `0x2681ac` (`stb r5, 5(r27)`), argument forwarded at `0x26206c` |
| the class each creator passes | `0xaa1518` `4`, `0x8ee484` `4`, `0x270f68` `2`, `0x268018` `5`, `0x276cbc` `0` (`0x276e28` `1`) |
| the state-8 store | `0x268c58`, after `(flags & 3) == 3` at `0x268c44` |

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

3. ~~**What sets the module instance's mode to `1` or `2`**, the handshake's
   `[19..21)`.~~ **Resolved** — §7.1b: the client's own role code at
   `connection+0xa18`, written by its UPnP/NAT module, read back over internal
   message `0x2102` and classified by `0x00d7d530`. `{0x30, 0xd0}` is a guest and
   `{0, 2, 0x10, 0x90}` a host; no field the server sends takes part, and the
   receiving side stores the byte at `session+0x1e` without testing it (§7.1d).
   What is left is only which of those values a given client's NAT state produces.
   **[V]**

2b. ~~**Whether the removed ESTABLISH OUT is needed after all.**~~ **Half answered.**
   The fake-host trial measured a real effect (the re-dials stopped), so the frame
   stays — but it is not sufficient: the same deployed image that sends it took
   `0B09` anyway (§7.1b). The frame is not what decides the join.
4. **State 2's re-dial vs. state 5.** Whether a real client that never gets a
   keyed frame stays in the state-2 re-dial loop for the whole timeout or gives
   up earlier is not established here; the FSM admits both. What a live run does
   with a keyed frame *and* an accepted reply is now measured (§7.1b): no re-dial
   at all, state 2 counting both deadlines out to `0B09`. **[U]**
5. ~~**What the object at `session+0x48` is, and what its bytes `0xb` and `0xc`
   mean.**~~ **Resolved** — §7.1b: `[0xb]` is the sequence the session hands to the
   next record it builds and `[0xc]` is the far end of a 24-deep window over those
   records; the gate at `0x268f18` is the only path to state 8 and asks whether
   that window has drained.
   ~~**What drains it is not located.**~~ **Located** — §7.1b: the answer path at
   `0x266f14`/`0x2676e0`, where `[0xc]` gains and `[0xd]` loses the same amount,
   `min([0xd], named - [0xc])`, off an inbound record's fourth byte. The reading
   that the host's answers drain it (answering each of the peer's records with the
   same identifier plus `0x4000`) **stands**: the live run that seemed to refute it
   (§7.1c) served those answers with an empty body, and every answer to a
   `0x8000`-class record in every readable round carries **one** byte — which
   `PeerAcknowledgementUtils` now builds. What is still open is the narrower one:
   whether the client refuses the empty body or never treats that record as an
   answer at all. **[U]** for the last step only; a live join decides it.
