#!/usr/bin/env python3
"""Find the vtables whose slots resolve into a code range, and name each slot.

Usage:
    python tools/p2p_vtable_scan.py                    # the P2P module's vtables
    python tools/p2p_vtable_scan.py --low 0x25F000 --high 0x276000
    python tools/p2p_vtable_scan.py --min-slots 2

A C++ object keeps its vtable pointer in its first word and each slot holds the
address of an *OPD* entry, not a code address: the entry's first word is the code
and its second the TOC the function expects in `r2` (this image's TOC is
`0x01222A00`, read out of the entry stub rather than assumed). A slot is
therefore two loads, which is why a scan for `{entry, toc}` pairs finds nothing —
the pairs live in the OPD section alone and the vtables hold pointers into it.

Scanning the data section for runs of pointers *into the OPD range*, and keeping
only the runs whose slots resolve into the code range asked for, is the one
static route to the peer-to-peer module's handlers
(`docs/analysis/replay-findings.md` §3.1, route 3). Its receive path is a vtable
slot, not a table index (`0x27A180` loads the record's vtable, then its first
entry, then branches), so the vtables are where a handler is named without a
debugger.

Two of the results are already pinned in the docs: the frame-drain loop
`0x26E790` is slot 0 of the vtable at `0x01191B30`, and the module object's own
vtable is at `0x011A63C8` — the instance `0x011A6308` plus `0xC0`.

Resolve a single vtable, with its `off(r2)` operands annotated, with
`tools/ppc_vtable_dump.py`.
"""

from __future__ import annotations

import struct
import sys

PATH = "MGO2.ELF"

# file offset = VA - 0x10000
FILE_BIAS = 0x10000
OPD_LOW = 0x011E7358
OPD_HIGH = 0x01222A00
OPD_TOC = 0x01222A00

# The data section the vtables live in; the scan does not read code or rodata.
DATA_LOW = 0x01190000

P2P_LOW = 0x25F000
P2P_HIGH = 0x276000


def read_image() -> bytes:
    with open(PATH, "rb") as handle:
        return handle.read()


def opd_code(image: bytes, pointer: int) -> int | None:
    """The code address an OPD address names, or None when it names no entry."""
    offset = pointer - FILE_BIAS
    if not OPD_LOW <= pointer < OPD_HIGH or offset + 8 > len(image):
        return None
    code, toc = struct.unpack_from(">II", image, offset)
    return code if toc == OPD_TOC else None


def runs_of_opd_pointers(image: bytes) -> list[list[tuple[int, int]]]:
    """Every contiguous run of `u32` values that are OPD addresses."""
    runs: list[list[tuple[int, int]]] = []
    current: list[tuple[int, int]] = []
    for offset in range(DATA_LOW - FILE_BIAS, len(image) - 4, 4):
        (value,) = struct.unpack_from(">I", image, offset)
        is_pointer = OPD_LOW <= value < OPD_HIGH and (value - OPD_LOW) % 8 == 0
        if is_pointer:
            if current and current[-1][0] + 4 != offset:
                runs.append(current)
                current = []
            current.append((offset, value))
            continue
        if current:
            runs.append(current)
            current = []
    if current:
        runs.append(current)
    return runs


def main() -> None:
    low = P2P_LOW
    high = P2P_HIGH
    minimum = 3
    if "--low" in sys.argv:
        low = int(sys.argv[sys.argv.index("--low") + 1], 16)
    if "--high" in sys.argv:
        high = int(sys.argv[sys.argv.index("--high") + 1], 16)
    if "--min-slots" in sys.argv:
        minimum = int(sys.argv[sys.argv.index("--min-slots") + 1])

    image = read_image()
    runs = runs_of_opd_pointers(image)

    kept = 0
    for run in runs:
        if len(run) < minimum:
            continue
        codes = [opd_code(image, value) for _, value in run]
        inside = [code for code in codes if code is not None and low <= code < high]
        if not inside:
            continue
        kept += 1
        print(f"vtable VA 0x{run[0][0] + FILE_BIAS:08x}  {len(run)} slot(s)")
        for index, code in enumerate(codes[:12]):
            where = f"0x{code:08x}" if code is not None else "—"
            mark = "in range" if code is not None and low <= code < high else ""
            print(f"    slot {index:2d}  opd 0x{run[index][1]:08x}  code {where:>10}  {mark}")
        if len(codes) > 12:
            print(f"    … {len(codes) - 12} more slot(s)")
        print()

    print(f"{kept} vtable(s) of {len(runs)} run(s) have a slot in 0x{low:08x}-0x{high:08x}")


if __name__ == "__main__":
    main()
