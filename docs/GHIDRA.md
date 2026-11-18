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
| `0x26b778` | `P2P_SendRecord` | the sender: copies the entry's header word out (`lwz r0,0x0(r31)` / `stw r0,0x70(r1)`), sets the datagram's byte `9`, writes the retry byte at `+0xb` into byte `0xa`, advances `+0xb`, and refuses the write that would leave `writes − acked == 0xff` |
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
| `0x26b778` | `P2P_SendRecord` | sends **one** record: `+0xa & 1` marks it sequenced; `retries(+0xb) − acknowledged(+0xc)` equal to `0xff` is refused (the peer already took it), otherwise `+0xb++`; then hands it to the gate |
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
| `P2P_SendRecord` `0x26b778` | `GameplayServerService.SendMessages`: it assigns nothing — the fourth byte is on the record before the write — spends no value, and the `0xff` guard is `StreamRetransmitService.AddRetries` |
| `gate_p2p_record_send` `0x263530` | `Shared/Udp/RecordSendGateService`: refuses a channel that is not ready, then appends |
| `append_udp_datagram_to_send_buffer` `0x2696d8` | `Shared/Udp/OutboundDatagramBufferService.AppendToBuffer` (`-1` on no room) and `Split` built on it |
| `P2P_QueueInboundRecord` `0x269b00` | `Shared/Udp/InboundRecordQueueService`: twelve-byte entry, ordinal zeroed for an unsequenced record, `false` when the queue is full |
| `read_queued_p2p_message` `0x26b5c8` | `Shared/Udp/InboundRecordGateService`: `(state & 5) == 5`, compare the container's expectation, advance on equality, drop without advancing on a mismatch |
| `P2P_FlushChannelQueues` `0x285b58` | `GameplayServerService/StreamRetransmitService`: a window of 24 per stream, Go-Back-N re-send, the same `0xff` abandon |
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

**Where the wire ordinal itself is written is still open.** Selector `0` only marks the
entry; `register_p2p_channel_record` builds the entry's header word as
`[index][id bits 11..4][id bits 3..0][0]`, with the fourth byte zero, and neither
`P2P_SendRecord` nor `gate_p2p_record_send` writes `+0xa` — the sender copies the header
out unchanged and spends only the retry byte. So the value the peer compares is stamped
before the send, by whoever builds the record, and the build site for the host's own
session records has not been read yet. This host stamps it in the write path
(`GameplayServerService.NumberAtWrite`), *before* the record enters the send window — the
same invariant the image holds, and the reason the order matters: a held entry must
already carry the byte an answer will name (`decode_p2p_datagram`'s `0x4000` branch, marked
by matching `+0xa`), or no answer can ever free it.

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

**A publisher is not a packer of the eleven-byte settings record.** Every `P2P_PublishOpcode*`
function appends the literal opcode it is named for, and the settings family's `0x0b` is not
among them; a byte scan of the publish cluster for `addi …, r0, 11` finds nothing either. The
function that packs the `0x0b` body is still unidentified.

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
