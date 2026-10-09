# Running Ghidra against `MGO2.ELF`

The image at the repository root (`MGO2.ELF`, 19 615 992 bytes, PPC64 big-endian
ELFv1) is the game binary every protocol document in `docs/protocol/` was read
from. This is how to get Ghidra to open it, and the two ways the command goes
wrong on this machine.

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

`-analysisTimeoutPerFile` is in seconds and its default is an hour, which a
20-megabyte game image can exceed. In the GUI the same thing is **Analysis →
Auto Analyze 'MGO2.ELF'**; the headless form is preferred here because it prints
the coverage numbers before and after. The pass preserves whatever the project
already holds, including renamed functions, so it is safe to run on a project
somebody has been working in.

## The scripts

| script | what it answers |
| --- | --- |
| `Mgo2Probe.java` | `decomp <addr>…` decompiles functions, `refs <addr>…` lists references to an address, `offset 0xb 0xc` finds loads and stores at an object offset |
| `Mgo2Verify.java` | one screen: language, image base, function and instruction counts, and what the project has at the addresses the protocol documents cite |
| `Mgo2Coverage.java` | the memory map, per-address block and bytes, and instruction and function counts over windows — the before/after check for an analysis pass |

All three take their addresses from the command line or from the `cited` list, so
a new address is a one-line edit rather than a new script.

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
