# Running Ghidra against `MGO2.ELF`

The image at the repository root (`MGO2.ELF`, 19 615 992 bytes, PPC64 big-endian
ELFv1) is the game binary every protocol document in `docs/protocol/` was read
from. This is how to get Ghidra to open it, and the two ways the command goes
wrong on this machine.

**Most questions do not need it.** The Capstone readers in `tools/` disassemble
any range or any function of the image directly — no project, no import, no
analysis pass — and every instruction cited in `docs/protocol/` is reproducible
that way (see the last section). Ghidra is worth its cost when a
*decompilation* is what the question needs; the pass below takes hours, and on
this machine that is the whole cost of it.

On this machine the pieces are:

| | |
| --- | --- |
| Ghidra | `C:\Users\migue\Documents\ghidra_12.1.4_PUBLIC` (12.1.4, PUBLIC) |
| project | `C:\Users\migue\Documents\ghidra-projects\MGO2` — `MGO2.gpr` and `MGO2.rep` |
| program | `/MGO2.ELF`, imported at language `PowerPC:BE:64:A2ALT`, image base 0 |
| scripts | `.ghidra_scripts/` in this repository |

## The command

```
C:\Users\migue\Documents\ghidra_12.1.4_PUBLIC\support\analyzeHeadless.bat ^
  C:\Users\migue\Documents\ghidra-projects\MGO2 MGO2 ^
  -process MGO2.ELF ^
  -scriptPath C:\Users\migue\Documents\GitHub\mgo2-server\.ghidra_scripts ^
  -postScript Mgo2Verify.java
```

The first argument is the **project directory**, the second the **project name**,
and the project lives in the *first* — `…\ghidra-projects\MGO2\MGO2.gpr`, with its
`MGO2.rep` beside it. Passing `…\ghidra-projects` first and `MGO2` second asks for
`…\ghidra-projects\MGO2.gpr`, which does not exist, and headless stops with:

```
ERROR Could not find project: C:\Users\migue\Documents\ghidra-projects\MGO2
      -- should already exist in -process mode.
```

The message names the path it built, which is the tell: it is one level short of
the project. `-noanalysis` skips the analyzers and runs the scripts against what
the program already has, which is what every script here wants. Leave it off to
analyze (see below).

`-scriptPath` is needed because this repository's scripts are not in Ghidra's
script directories. `-process MGO2.ELF` without `-postScript` opens, saves and
closes the program, which is a way to check the project is reachable at all.

## Complete the analysis before reading anything

The project's analysis had never finished, and that is what makes it look broken:
the bytes are all there, and the decompiler has nothing to say about most of them.

Measured on the project before the pass below, with `Mgo2Coverage.java`:

```
functions        = 2228
instructions     = 225212
window 00260000-00270000: 867 instructions, 0 functions
window 00a90000-00ab0000: 0 instructions, 1 functions
```

The record manager this repository reads lives at `0x264000`–`0x270000`: 867
instructions scattered across a 64 KB code region, no function at `0x26ab58` (the
gate) or at `0x2676e0` (the window update), and no decompilation of either. The
connect FSM fares no better: `0xaa1140` has a function entry — named `fsm_tick` in
this project — and not one instruction disassembled under it.

An import interrupted part way leaves exactly this, so finish it once:

```
…\analyzeHeadless.bat <projectDir> MGO2 -process MGO2.ELF ^
  -scriptPath <repo>\.ghidra_scripts -postScript Mgo2Coverage.java ^
  -analysisTimeoutPerFile 21600
```

`-analysisTimeoutPerFile` is in seconds, and the distribution's own description
says what it is for: *"if analysis on a file exceeds the specified time, analysis
is interrupted and processing continues as scheduled"*. That is the state this
project was found in, so the value here is six hours — the pass is the slow part
and a second interruption costs another one. How long it actually takes is the
wall clock of the run; the script prints the counts when it ends, and until then
the log is the only progress there is, since headless reports nothing between
"ANALYZING all memory and code" and the result. On a run that takes an hour, the
Java process's CPU time is the way to tell a slow pass from a stuck one.

In the GUI the same thing is **Analysis → Auto Analyze 'MGO2.ELF'**; the headless
form is preferred here because it prints the coverage numbers before and after.
The pass preserves whatever the project already holds, including renamed
functions, so it is safe to run on a project somebody has been working in.

## The scripts

| script | what it answers |
| --- | --- |
| `Mgo2Probe.java` | `decomp <addr>…` decompiles functions, `refs <addr>…` lists references to an address, `offset 0xb 0xc` finds loads and stores at an object offset |
| `Mgo2Verify.java` | one screen: language, image base, function and instruction counts, and what the project has at the addresses the protocol documents cite |
| `Mgo2Coverage.java` | the memory map, per-address block and bytes, and instruction and function counts over windows — the before/after check for an analysis pass |

All three take their addresses from the command line or from the `cited` list, so
a new address is a one-line edit rather than a new script.

## The record reader and its neighbours, by name

The functions the ordinal work reasons about were renamed in the project, so a
decompilation of the record path reads as protocol instead of as `FUN_`. Each name
below was given only after the decompiled body showed what it does; a name that
would have been a guess is left as `FUN_`.

| address | name | what the body does |
| --- | --- | --- |
| `0x26b5c8` | `read_queued_p2p_message` | the reader: takes a queued record only when its `+0xa` equals the stream state's `+0xe`, then advances `+0xe` by one; a mismatch jumps to the drop path |
| `0x26b178` | `P2P_ChannelStateInit` | builds a stream state: `+0xe = 0` (the first byte it expects), `+9 = 0x80`, `+0xb = 0` |
| `0x26b1c8` | `P2P_ChannelStateRelease` | drops one reference to a stream state and clears `+0xa` with the last one |
| `0x2666c8` | `decode_p2p_datagram` | the datagram decoder; its `type & 0x4000` branch is the answer reader, which walks the send window and marks the entry whose `+0xa` matches |
| `0x26b778` | `P2P_SendRecord` | the sender: builds the queued entry on its stack from the descriptor — handle word at `+0`, the `u16` at `+4`, the size from the cursor at `+6`, state `1` at `+9`, and **the fourth wire byte at `+10` from the descriptor's `+0xb`** — then refuses the write that would leave `writes − acked == 0xff`, advances `+0xb`, and hands the entry to the gate. The sequence byte is stamped in this call (`lbz r9,0xb(r31)` / `stb r9,0x7a(r1)` before `stb r11,0xb(r31)`) |
| `0x26b2b0` | `P2P_SetRecordHeaderField` | writes one field of a record header; called with field `0` and value `3` where a channel is first registered |
| `0x2692c0` | `P2P_MessageIdentifier` | the record's 16-bit identifier, class bits included |
| `0x2692e0` | `P2P_MessageBodyLength` | the record's body length |
| `0x261600` | `P2P_SessionTickNumber` | the session's tick number, `*(singleton + 8)`; `decode_p2p_datagram` times a record only while it is under `200` |
| `0x26c9e0` | `P2P_ReadTimeBaseInto` | writes the current `TBLr` time base into the word it is handed |
| `0x26c9f8` | `P2P_ElapsedMillisecondsSince` | `(TBLr − stored) * 1000 / timebase frequency`: milliseconds since a stored base |
| `0x26caf0` | `P2P_CopyRecordBody` | copies the delivered body out of a queued record |
| `0x269b00` | `P2P_QueueInboundRecord` | queues a received record against its channel |
| `0x269280` | `P2P_EntryWalkNext` | advances the send-window walk |
| `0x269390` / `0x2693a8` / `0x2693e0` | `P2P_InboundQueueBegin` / `Current` / `Next` | walk the queue a datagram's records are read out of |
| `0x269448` / `0x269480` / `0x269668` | `P2P_RecordQueueBegin` / `Advance` / `End` | the same walk over a frame's records as a whole |

### What the two structures look like, from those bodies

The **queued record** has a twelve-byte header: `+0x6` is the body length as a
`ushort`, `+0x9` the flags byte the reader gates on (`0x80` set once delivered, and
`0x01|0x04` tested before the ordinal check), `+0xa` the fourth wire byte, `+0xb` a
retry counter the send walk zeroes, and `+0xc` the body — `P2P_CopyRecordBody` copies
`*(ushort *)(record + 6) - 0xc` bytes from there.

The **stream state** carries the expectation at `+0xe`, the two bytes an answer
advances at `+0xc` and `+0xd`, and a reference count at `+8`.

The flags byte is **not** in the wire header, which is only identifier, length and the
fourth byte. That is why `docs/analysis/stream-sequencing.md` reasons from what the
peer *answers* rather than from a reconstructed flags byte: which records the reader
ordinal-checks cannot be read off the wire.

### What the `0x8000` class bit is for

The bit was the last unexplained class bit. `decode_p2p_datagram` branches on it in
two places, and both are timing:

- **A sequenced record carrying it opens a timed exchange.** The branch stamps
  `P2P_ReadTimeBaseInto(session + 0x18)` (a record this host sends) or `session + 0x1a`
  (one it receives), copies the record's fourth byte to `session + 0x55` and its handle
  to `session + 0x20`, guarded by `P2P_SessionTickNumber() < 200`.
- **Its answer closes it.** The `0x4000` branch, when the answer also carries `0x8000`,
  has a one-byte body that is not `0xff`, and repeats the fourth byte:
  `P2P_ElapsedMillisecondsSince(session + 0x1a)` gives the elapsed milliseconds, the
  answer's byte is subtracted, and the remainder lands at `session + 0xc2`. A remainder
  over `600` sets `session[5] |= 0x20` (when it is under `0x3e9`) or shrinks the interval
  at `session[0x24]`, clamped to at least `16 000`; the pending handle at `session[0x1f]`
  is cleared either way.
- **Without `0x1000` it does something else.** The unsequenced branch sets bit `4` of
  the session word at `+0x26`, advances the counter at `+0xb2` by one, and the record
  still goes to the ordinary queue path.

So `0x8000` is a **round-trip measurement**: the record asks the peer for one byte back,
and that byte is a duration the receiver subtracts from its own clock and uses to pace
its resends. The wire agrees — every `0x8000` record in the working captures is answered
with exactly one body byte and nothing else (`0x9001` → `0xd001`, `0x92de` → `0xd2de`).
What the record's own body means is still open, which is why the bit is named for the
exchange rather than for the report it carries.

The class bits are now all accounted for: `0x10` sequenced, `0x20` sixteen-bit length,
`0x40` answer, `0x80` timed.

## The sender, which is also in the image

The game lets a player host, so the client carries the host's half as well. These are
the functions on that side.

| address | name | what the body does |
| --- | --- | --- |
| `0x26b778` | `P2P_SendRecord` | sends **one** record: `+0xa & 1` marks it sequenced; the fourth wire byte is stamped from `+0xb`; `retries(+0xb) − acknowledged(+0xc)` equal to `0xff` is refused (the peer already took it), otherwise `+0xb++`; then hands it to the gate |
| `0x263530` | `gate_p2p_record_send` | zeroes the retry byte and appends the record to the datagram buffer at `peer+0xd·4`; refuses while the channel byte at `peer+4` is `<= 1` (not ready) |
| `0x2696d8` | `append_udp_datagram_to_send_buffer` | the packer: write position `+= (3 + len) & 0xfffc`, returns `-1` when the datagram has no room (that is how a run is split), and zeroes `+0xb` on every successful append |
| `0x2729d8` | `P2P_PhaseTick_SendOperations` | the per-frame tick: `receive_p2p_datagrams()` **first**, then the phase's own records, walking a 24-slot (`0x18`) record table |
| `0x270e00` | `P2P_RunOpener_BuildAndSend` | the run opener's 0x174-byte body; stamps the handle with `P2P_SetRecordHeaderField(0, 3)` and `(3, 0x64)`, then sends it |
| `0x2753c8` | `P2P_PublishPlayerRecord` | applies a player-record body and publishes it: `[0]`→state `0x12c`, `[2]`→`0x12e`, `[4]`→`0x126`, `[5]`→`0x130`, `[0xc]`→`0x128`, `[0x10..0x26]`→`0x131` (0x17 bytes), `[0x27]`→five flag/value pairs, `[0x32..0x131]`→`0x152` and `0x252` (0x100 bytes each), and the entry's own 0x18-byte settings header→state `2` |
| `0x276e88` | `P2P_ReadPlayerSettingsHeader` | the settings-body **parser**, and it refuses everything unless the entry's state word is exactly `0x211` (in the room). Layout, in order: `u16`, `u16`, `byte`, `byte`, `byte`, `u32`, `u32`, `0x17` bytes, five present-flag/value byte pairs, a count byte and that many `u16`s, then `0x30` bytes. It applies the same state writes as the publisher and answers the record |

### Where each of those lives in the server

Every named function above has one counterpart and no second implementation, so a rule read
here can be found in the code by its name rather than reconstructed from a log.

| image | server |
| --- | --- |
| `P2P_SetRecordHeaderField` `0x26b2b0` | `MatchRecords/RecordHeaderFieldService` (`Set`, the four selectors and their width refusals), over `MatchRecords/RecordHeaderEntry`; every record is built through `RecordHeaderEntry.Build` |
| `P2P_SendRecord` `0x26b778` | `GameplayServerService.SendMessages`: it assigns nothing — here the fourth byte was stamped by the builder, where the image stamps it in the send call — and the `0xff` guard is `StreamRetransmitService.AddRetries` |
| `gate_p2p_record_send` `0x263530` | `Shared/Udp/RecordSendGateService`: refuses a channel that is not ready, then appends |
| `append_udp_datagram_to_send_buffer` `0x2696d8` | `Shared/Udp/OutboundDatagramBufferService.AppendToBuffer` (`-1` on no room) and `Split` built on it |
| `P2P_QueueInboundRecord` `0x269b00` | `Shared/Udp/InboundRecordQueueService`: twelve-byte entry, ordinal zeroed for an unsequenced record, `false` when the queue is full |
| `read_queued_p2p_message` `0x26b5c8` | `Shared/Udp/InboundRecordGateService`: `(state & 5) == 5`, compare the container's expectation, advance on equality, drop without advancing on a mismatch |
| `P2P_FlushChannelQueues` `0x285b58` | `GameplayServerService/StreamRetransmitService`: one window per stream, measured in entry bytes against the record queue's capacity word (`queue + 0`), drained at `capacity >> 1` back under `capacity >> 2`, re-sent only for the streams a peer frame names, the same `0xff` abandon |
| `decode_p2p_datagram` answer branch `0x2666c8` | `PeerAcknowledgementUtils` / `GameplayServerService.FrameDispatch`: an answer echoes the record's fourth byte and frees the window to it, and is never queued |

Two differences are deliberate and stated in those files: a full datagram starts the next
frame here instead of aborting the channel, and an unsequenced record's ordinal is never
compared because it is never written.

The settings path is where the score and ticket families live, and the parser is strict in
one more way than the body layout suggests: **it only runs for an entry whose state word
is `0x211`**, so a record that arrives while the client still holds the player in any other
state is discarded however well formed it is.

**The sequence flag and the sequence byte are the same byte.**
`P2P_SetRecordHeaderField`'s selector `0` writes the *low nibble* of `+0xa`, and
`P2P_SendRecord` tests bit `0x01` of that same byte to decide whether the record is
sequenced at all. The wire header carries one byte and no separate flag, so which
records the reader compares cannot be derived from a capture — the sender stamps it
from the record's class when it builds it. Every wire-side filter tried against the
working captures failed for that reason (`body == 63` takes 0 records in `pcapng1`), and
that is why the analysis reasons from answers rather than from a guessed gate.

**The wire ordinal is stamped by the sender, inside the send call.** The disassembly settles what the decompiler's variable naming hid. `P2P_SendRecord` builds the twelve-byte queued entry on its own stack at `r1+0x70`: the handle word at `+0` (`lwz r0,0x0(r31)` / `stw r0,0x70(r1)`), the descriptor's `u16` at `+4`, the size at `+6` (cursor bytes + `0xc`), `1` at `+9`, and at `+10` — the entry's fourth wire byte — the descriptor's `+0xb` (`lbz r9,0xb(r31)` / `stb r9,0x7a(r1)`), before `addi r11,r9,1` / `stb r11,0xb(r31)` advances that byte. `P2P_SendRecordByEntry` (`0x26b048`) and `0x26b8b8` do the same, so all three senders stamp it. The header setter is not involved: selector `1`, the only selector that writes a whole byte, still has no caller, and the byte it writes at `+0xd` is not the wire byte. **The send path cannot separate numbering from appending**: the byte is assigned by the same call that hands the entry to `gate_p2p_record_send` and `append_udp_datagram_to_send_buffer`, and there is no lock, yield or callback between the stamp and the append — the gate and the packer are called directly.

The four callers that pass a **runtime** value to selector `0` settle what that value is: it is
bits `0` and `1` of the object's flag byte at `+0x11` — `uVar = byte[0x11] & 1`, then `uVar |= 2`
when bit `1` is set (`_opd_FUN_00277848` `0x277848` and its siblings `0x278eb8`, `0x278aa8`,
`0x2792f8`) — so it is a two-bit class the entry is created with, not a counter.

**The receive-side copy is settled, and so is the send's stamp.** `P2P_QueueInboundRecord` (`0x269b00`) is the one store that puts the wire's fourth byte into a queue entry: `entry[+10] = record[+3]`, or `record[+4]` when the record's `0x20` bit widens the length field, and `0` for a record without the `0x10` bit (which also zeroes `entry[+9]`). Outbound, `P2P_SendRecord` stamps `entry[+10]` from the descriptor it is sending (above), so the record carries its stream byte before it enters the window and the answer branch can name it (`decode_p2p_datagram`'s `0x4000` branch matches `+0xa`). The difference that matters for this host is *where* that stamp happens: here the number is taken by the builder and the record is sent later, so two builders can number in one order and write in another. The image has no such gap — the take and the append are one call. This host has no equivalent: the number is taken by the builder (`RoomRosterService.TakeRunOrdinal`, `MatchRecordSequenceService.Take`) and the record is written later.

## When Ghidra is not the tool for the question

`tools/` holds Capstone-based readers — `mgo2_disasm.py` over an address range,
`ppc_function_disasm.py` walking basic blocks from an entry point,
`p2p_fsm_disasm.py` for the connect FSM's inline jump table — and they need no
project at all:

```
python tools/mgo2_disasm.py 0x266f00 0x266fb0 MGO2.ELF
```

Every instruction cited in `docs/protocol/P2P_CONNECT_FSM.md` was read this way.
The trade is that they stop where Ghidra's analysis does not exist and do not
decompile: for a decompiled function, the project has to be analyzed first.

## The record queues and the field layer (renamed 2026-11-16)

The receive path and the send path each fill one queue, and both hold the same entry: a
twelve-byte header (handle, entry size, container id, state code, fourth wire byte,
retransmit flag) with the body behind it, the write offset advanced by
`(3 + size) & 0xfffc`. The record's fourth byte sits at wire offset 3, or 4 when the header's
bit `0x20` declares a sixteen-bit length; bit `0x10` is what makes the record sequenced at
all, and an unsequenced record is queued with its ordinal zeroed.

| address | name | what it does |
| --- | --- | --- |
| `0x269b00` | `P2P_QueueInboundRecord` | copies one inbound record into the channel's queue; returns its size, or `0xffffffff` when it does not fit |
| `0x2696d8` | `append_udp_datagram_to_send_buffer` | the send-side mirror of the same entry; clears the retransmit byte, returns `0xffffffff` when the datagram is full |
| `0x269390` / `3a8` / `3e0` | `P2P_InboundQueueBegin` / `Current` / `Next` | walks the queued entries one per turn |
| `0x277e78` | `P2P_DrainRecordQueues` | walks the channel's twenty-four record slots, reads with `read_queued_p2p_message` and writes the pending ones again |
| `0x2784b0` | `P2P_SendBuiltRecord` | sends a packed record and tracks the byte the peer answers |
| `0x27e2b0` / `480` / `578` | `P2P_GetOperationStateBlock` / `ReadOperationStateField` / `WriteOperationStateField` | the operation state block and its per-field transfer codes; a write sets the field's two-bit code to `3` and raises the block's dirty word |
| `0x26db78` / `26dd60` / `26f158` | `P2P_ApplyField99Record` / `P2P_ApplyFieldA1Record` / `…Second` | one-byte record handlers that write a single state field |
| `0x274f60` | `P2P_CountBlockField90` | reads field `0x90` of a player's block, counts it up and writes it back under a latch |
| `0x754b50` … `0x757798`, `0x71caf8`, `0x737cd0` | `P2P_PublishOpcode*`, `P2P_PublishBlockByte` | the per-opcode publishers: open a cursor, append the opcode and its fields, hand it to `P2P_SendBuiltRecord` |

**A publisher is not a packer of the eleven-byte settings record, but the `0x0b` builder is
`P2P_PublishOpcode0B` (`0x755228`).** Its body is the opcode, a `u16` and a `u32` — seven bytes,
exactly the countdown record's wire shape (`0b 39 00 e0 a8 1d 00`). It was missed because it sits
outside the `0x754b50`–`0x757798` run the earlier scan walked, not because the opcode is absent.

## The receive buffer, the arena and the record index (renamed 2026-11-16, second pass)

| address | name | what it is |
| --- | --- | --- |
| `0x2622a0` | `receive_p2p_datagrams` | allocates `0x578` and reads with `recvfrom(socket, buffer, 0x578, …)`; walks the twenties-four channel records, stride `0x6d8` |
| `0x26d350` | `P2P_AllocateNetworkMemory` | the network arena: `0x4000` bytes, requests rounded to sixteen, returns pointer + `0x10` |
| `0x26d250` | `P2P_ReleaseNetworkMemory` | frees an arena block |
| `0x26c6d8` | `P2P_InitializeNetwork` | `sys_net_initialize_network_ex`, `cellNetCtlInit`, `sceNpInit(0x20000, …)` |
| `0x269808` | `P2P_ResetRecordHandleTable` | clears the `0x600`-byte handle table (256 six-byte entries) and sets the queue's used count to `0xc` |
| `0x269ea8` | `P2P_CompactRecordQueue` | walks the queue's entries, compacts the ones still marked, and rebuilds the handle table |
| `0x26a0e8` / `0x26a360` | `P2P_IndexDatagramRecords` / `…Second` | parses a datagram's records into an offset table of two bytes per record, allocated `count << 1` from the arena |
| `0x269ca8` | `P2P_QueueRecordHandles` | hands a run of record handles to a channel's queue |
| `0x263ee0` | `P2P_SendQueuedHandles` | collects a channel's queued handles into a `0x400` scratch buffer, sorts them and queues them |
| `0x285b58` | `P2P_FlushChannelQueues` | the channel's flush: frees from the front once used reaches `capacity >> 1`, down to `capacity >> 2`, marking what it frees for re-send |

### Two facts these settled

**The record header's fourth byte exists only on a sequenced record.** `0x26a0e8` takes the
body's start as `(4 or 3, by the length flag) + (1 if the sequenced bit is set)`: two bytes
of id, one or two of length, then the ordinal if and only if the record is sequenced, then
the body. An unsequenced record's header is three bytes, and `P2P_QueueInboundRecord` zeroes
the ordinal it stores for exactly that case.

**The capacity is a constant, not a setting.** `0x578` (1 400 bytes) is what the receive path
allocates and what `recvfrom` is given, so it is the most the receive path can be holding.
There is no `MTU`, `kbps` or `512` string anywhere in the image, and no bandwidth figure is
read on this path: the kbps choices in the game's menus are resource labels that never reach
this code.

**And the queue's own capacity word has no writer.** A record queue begins with a pair of
halfwords — capacity at `+0`, used at `+2` — and entries from `+0xc`, each advanced by
`(3 + size) & 0xfffc`. Every function that touches a queue maintains the *used* halfword
only: `P2P_ResetRecordHandleTable` (`0x269808`) writes `0xc` there, `P2P_QueueRecordHandles`
(`0x269ca8`) refuses when `capacity < used + size`, `P2P_CompactRecordQueue` (`0x269ea8`)
shrinks it as it drops delivered entries, and `P2P_FlushChannelQueues` (`0x285b58`) drains
against it. `initialize_p2p_session` (`0x268148`) resets the three queues at `slot + 0x34`,
`+0x38` and `+0x3c` through that reset and never stores a capacity, and `create_p2p_session`
(`0x261fe0`) only scans the 24 static slots (stride `0x6d8`) for one whose byte `+4` is zero.
So the capacity is laid out with the queue buffer before any of that code runs; the image's
value for it was not read, and this host uses `0x578` as that size.

### Where a queue actually lives

A slot's queues are **pointers**, not inline storage, and the whole network state is runtime
arena memory: the singleton is `0x01856c10`, outside every section of the file, so no address
the queue could occupy is in the ELF. What the code fixes is the layout:

| offset | what it holds |
| --- | --- |
| `singleton + 0x28` | first session slot, 24 of them, stride `0x6d8` — `receive_p2p_datagrams` walks them as `+0x1b6` ints, and `register_p2p_channel_record` (`0x261ab0`) divides a slot pointer by `0x6d8` to get its index |
| `singleton + 0x2c`, `+0x30` | the two class-2 sessions, outside the 24 (`create_p2p_session` uses slot base `+ 0x9d68` for that class) |
| `singleton + 0x34` | the channel descriptor table: 24 × `0xaa0` in a `0x10000` window, class-2 channels at `0xf460`, entries tagged by the byte at `+0xa` (`0x80` normal, `0x90` a reply) |
| `slot + 0x34`, `+0x38`, `+0x3c` | the slot's three record-queue pointers — the ones `initialize_p2p_session` resets |
| `queue + 0` / `+2` | capacity / used, halfwords |

`receive_p2p_datagrams` (`0x2622a0`) is the clearest witness: it loads `slot[0x3c]` and reads
it as a queue header — `(ushort[0] * 3) >> 2 < ushort[1]` ticks the session before the
datagram is decoded. So there are two fractions on the same word: **three quarters** before a
receive, **one half** before the flush starts freeing. The buffers are allocated in the
`0x20000` block `P2P_InitializeNetwork` hands to `sceNpInit`, which is why the capacity has no
store instruction anywhere in the module — it belongs to the allocation's layout.

## The retry bytes (settled 2026-11-16)

Two bytes on a record's handle carry the send budget, and they are not the entry's own flag:

| handle offset | meaning | who changes it |
| --- | --- | --- |
| `+0xb` | writes since the acknowledged base | `P2P_SendRecord` `0x26b778`, `+1` per write |
| `+0xc` | the acknowledged base | the answer branch of `decode_p2p_datagram` `0x2666c8`, `base = distance + answer` |
| entry `+0xb` | "needs writing again" | cleared by `gate_p2p_record_send` `0x263530` and by `append_udp_datagram_to_send_buffer` `0x2696d8` |

`P2P_SendRecord` refuses when `writes − acked == 0xff` and calls the channel's abort vector
(vtable `+8`); otherwise it increments the writes. `gate_p2p_record_send` then clears the
entry's flag and refuses the record outright while the channel's connect byte is `≤ 1`, and
an append that does not fit aborts the channel too.

So the retry budget is **255 unanswered writes of one record**, tested on the oldest held
record before each write, and the response to spending it is to end that channel — not to keep
copying the stream. `src/GameplayServer/StreamRetransmitService.cs` keeps the counter as
`Pending.Attempts`, tests it at `0xff`, and drops the stream's window when it fires; it does not
tear the session down, which is the one deliberate difference.

## The stream counter is never taken backwards (2026-11-18)

This is the image's half of the round-start question, and it is why the counters in
`MatchRecordSequenceService` are never cleared.

**The only store that zeroes a sequence is the one that builds a stream state.**
`P2P_ChannelStateInit` (`0x26b178`) writes `+0xe = 0` — the byte the stream expects next — along
with the refcount and the two answer bytes, and it runs when a channel state is built
(`register_p2p_channel_record`, `0x261ab0`). After that:

| function | what it does to `+0xe` |
| --- | --- |
| `read_queued_p2p_message` `0x26b5c8` | compares the inbound record's `+0xa` with it, advances it by one on equality, and drops the record **without** advancing on a mismatch |
| `P2P_ChannelStateRelease` `0x26b1c8` | drops a reference; it never writes the expectation |
| `P2P_SendRecord` `0x26b778`, `P2P_SendRecordByEntry` `0x26b048`, `0x26b8b8` | stamp the wire byte from `+0xb` and advance `+0xb`; none of them touches `+0xe`, so the peer's expectation is never rewound |

So a live stream cannot be renumbered by the image. The one way a stream starts again at `00` is
a new channel state, which is exactly what happens at a round boundary on the client's side — it
opens its own stream at `00` and the host answers the values, while the host's own series carries
on (`docs/analysis/stream-sequencing.md`, D5, and the `pcapng1` measurement there). Clearing the
map instead renumbers a live reader, which is the defect that file records.

## The operation-stream builders, named 2026-11-18

Each function below appends the literal opcode it is named for — read off its decompiled body, not
its caller — then its fields, then hands the cursor to `P2P_SendBuiltRecord` (`0x2784b0`). Where
one opcode has several copies the extras carry `Second` or `Third`; a function whose first byte is
the caller's is named for its shape instead.

| address | name | body after the opcode |
| --- | --- | --- |
| `0x7540f8` | `P2P_PublishOpcode13` | a `u32` slot mask, then one `u16` per set bit |
| `0x7541f0` | `P2P_PublishOpcode12` | three bytes, the first carrying the caller's flag in bit 7 |
| `0x754570` | `P2P_PublishOpcode11` | two bytes |
| `0x754620` | `P2P_PublishOpcode10` | a `u32` built from `0x7448e8(slot)` over the twenty-four slots |
| `0x754710` | `P2P_PublishOpcode0F` | one byte |
| `0x7547b0` | `P2P_PublishOpcode24` | one byte |
| `0x7548f0` | `P2P_PublishOpcode26` | two bytes |
| `0x7549a0` | `P2P_PublishOpcode03Or17` | `03` or `17` by `0x26d540()`, the writer's own slot, a byte and a `u16` |
| `0x754a90` | `P2P_PublishOpcode1BOr1C` | `1b` or `1c` by the caller's argument, then the writer's own slot |
| `0x754c38` | `P2P_PublishOpcode19` | two bytes, guest side only (`0x26d540() == 0`) |
| `0x754de8` | `P2P_PublishOpcode23` | one byte, on the handle at `+0x84` |
| `0x754f18` | `P2P_PublishOpcode0E` | one byte, then script event `0x160023` |
| `0x755168` | `P2P_PublishOpcode1F` | the writer's own slot and a state byte |
| `0x755228` | `P2P_PublishOpcode0B` | a `u16` and a `u32` — the seven-byte countdown record |
| `0x7552d8` | `P2P_PublishOpcode0A` | one byte with the caller's flag in bit 7 |
| `0x755390` | `P2P_PublishOpcode1E` | two bytes |
| `0x755440` | `P2P_PublishSixFieldRecord` | the opcode is the caller's first byte, then five bytes and a `u16` |
| `0x755530` | `P2P_PublishOpcode09Second` | two bytes |
| `0x7555e0` | `P2P_PublishOpcode06` | one byte |
| `0x755680` | `P2P_PublishOpcode08Third` | two bytes clamped to `0xff` |
| `0x755868` | `P2P_PublishOpcode05Second` | one byte when the caller is not this host, otherwise script event `0x15000f` |
| `0x7559b8` | `P2P_PublishOpcode04Second` | two bytes, remapped to `12` or `13` |
| `0x755af8` | `P2P_PublishOpcode02Second` | a byte, a count, then that many bytes of a buffer |
| `0x755c90` | `P2P_PublishOpcode15` | one byte |
| `0x755d30` | `P2P_PublishOpcode14` | nothing |
| `0x755db0` | `P2P_PublishOpcode06Second` | one byte |
| `0x755f00` | `P2P_ForwardOpcode26` | reads a slot and a value off an inbound record and sends `26` about them as the host |
| `0x756070` | `P2P_PublishOpcode00StateBlock` | `0x57` bytes from the state block, then its `u16` clock |
| `0x756238` | `P2P_PublishOpcode07Second` | three bytes |
| `0x756420` | `P2P_PublishOpcode1DSecond` | the writer's own slot, three bytes and a `u16` |
| `0x756550` | `P2P_PublishOpcode00StateBlockSecond` | the same body, and clears the word at `+0x164` first |
| `0x7569c8` | `P2P_DispatchQueuedRecords` | drains a peer's queue and jumps through the table for opcodes under `0x27` |
| `0x7574d0` | `P2P_DispatchQueuedRecordsBoth` | the same dispatch for the object's two sub-channels |
| `0x757520` | `P2P_PublishOpcode02Writes` | `02` writes for the bytes that differ between two buffers |
| `0x7578c8` | `P2P_ApplyOpcode1DRecord` | reads a `1d` body into state `slot * 2 + 0x148` |
| `0x7579f8` | `P2P_PublishOpcode02WritesSecond` | the second differ-and-write publisher |
| `0x757c58` | `P2P_DispatchQueuedRecordsSecond` | dispatch for opcodes `14` to `26` |

The receive side names the same set from the other end: `P2P_DispatchQueuedRecords` reads one byte
off a drained record and indexes `*(0x121b714 - 0x7ff4)`, and `P2P_ApplyOpcode1DRecord` is one of
the handlers behind that table.

## The two dispatch tables behind `P2P_DispatchQueuedRecords` (2026-11-18)

Both dispatchers do the same thing: drain the peer's queue, read one byte, and tail-call
`base + *(int *)(base + opcode * 4)`. The base is not a constant in the code — it comes from the
singleton at `0x121b714`: the word at `*(0x121b714) - 0x7ff4` is the first table's base and
`*(0x121b714) - 0x7ff0` is the second's. Read from the image, those words are `0x756a84` and
`0x757d24`.

Both tables store **signed offsets from their own base**, and their targets are `case` labels
inside the dispatcher's function rather than functions of their own — which is why analysis had
never made functions of them, why the decompiler shows `unaff_r*` on the register arguments, and
why the names below were applied to freshly created functions at the table's own targets. Every
case ends by draining the queue again and jumping through the table, so a case handles one record
and returns to the dispatch loop; the names are the table index, which is a fact, not a reading of
the body.

**Table 1 — base `0x756a84`, opcodes `00`–`26`:**

| opcode | case address | name |
| --- | --- | --- |
| `00` | `0x756c2c` | `P2P_HandleQueuedOpcode00` — applies the `0x57`-byte state block and its `u16` before looping |
| `01` | `0x756c7c` | `P2P_HandleQueuedOpcode01` |
| `02` | `0x756cf4` | `P2P_HandleQueuedOpcode02` |
| `03` | `0x756d5c` | `P2P_HandleQueuedOpcode03` |
| `04` | `0x756b20` | `P2P_HandleQueuedOpcode04` |
| `05` | `0x756ddc` | `P2P_HandleQueuedOpcode05` |
| `06` | `0x756e70` | `P2P_HandleQueuedOpcode06` |
| `07` | `0x756ebc` | `P2P_HandleQueuedOpcode07` |
| `08` | `0x756f28` | `P2P_HandleQueuedOpcode08` |
| `09` | `0x756f7c` | `P2P_HandleQueuedOpcode09` |
| `0a` | `0x756fd8` | `P2P_HandleQueuedOpcode0A` |
| `0b` | `0x757020` | `P2P_HandleQueuedOpcode0B` |
| `0c` | `0x757078` | `P2P_HandleQueuedOpcode0C` |
| `0d` | `0x7570cc` | `P2P_HandleQueuedOpcode0D` |
| `0e` | `0x75715c` | `P2P_HandleQueuedOpcode0E` |
| `0f` | `0x7571a0` | `P2P_HandleQueuedOpcode0F` |
| `10` | `0x7571e0` | `P2P_HandleQueuedOpcode10` |
| `11` | `0x757248` | `P2P_HandleQueuedOpcode11` |
| `12` | `0x75729c` | `P2P_HandleQueuedOpcode12` |
| `13` | `0x757340` | `P2P_HandleQueuedOpcode13` |
| `14`–`25` | `0x756bd0` | `P2P_HandleQueuedOpcode14To25` — one case for all eighteen opcodes |
| `26` | `0x7573d0` | `P2P_HandleQueuedOpcode26` |

**Table 2 — base `0x757d24`, opcodes `14`–`26`:**

| opcode | case address | name |
| --- | --- | --- |
| `14` | `0x757e00` | `P2P_HandleQueuedOpcode14Second` — the state block again, clearing `+0x164` first |
| `15` | `0x757f58` | `P2P_HandleQueuedOpcode15Second` |
| `16` | `0x757f88` | `P2P_HandleQueuedOpcode16Second` |
| `17` | `0x757fac` | `P2P_HandleQueuedOpcode17Second` |
| `18` | `0x75802c` | `P2P_HandleQueuedOpcode18Second` |
| `19` | `0x758078` | `P2P_HandleQueuedOpcode19Second` |
| `1a` | `0x7580fc` | `P2P_HandleQueuedOpcode1ASecond` |
| `1b` | `0x758168` | `P2P_HandleQueuedOpcode1BSecond` |
| `1c` | `0x7581a4` | `P2P_HandleQueuedOpcode1CSecond` |
| `1d` | `0x7581e0` | `P2P_HandleQueuedOpcode1DSecond` — writes `slot * 2 + 0x148`/`0x149` |
| `1e` | `0x7582f4` | `P2P_HandleQueuedOpcode1ESecond` |
| `1f` | `0x758364` | `P2P_HandleQueuedOpcode1FSecond` |
| `20` | `0x7583c0` | `P2P_HandleQueuedOpcode20Second` |
| `21` | `0x758450` | `P2P_HandleQueuedOpcode21Second` |
| `22` | `0x7584d0` | `P2P_HandleQueuedOpcode22Second` |
| `23` | `0x7581e4` | **not a case of its own** — the offset lands four bytes into the `1d` case, so `23` reuses that body from its second instruction; left unnamed |
| `24` | `0x7586ac` | `P2P_HandleQueuedOpcode24Second` |
| `25` | `0x758718` | `P2P_HandleQueuedOpcode25Second` |
| `26` | `0x758754` | `P2P_HandleQueuedOpcode26Second` |

The `0x23` entry is the one thing in either table that is not a clean case start, and it is
recorded rather than named: naming it would claim a function the image does not have.
