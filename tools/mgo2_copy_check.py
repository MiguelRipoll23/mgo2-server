#!/usr/bin/env python3
"""Check the reference-cited parsers (0xd3e9ac, 0xd3e53c) and who calls them.

Usage: python tools/mgo2_copy_check.py [elf-path]
"""
import struct
import sys

from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

ELF = sys.argv[1] if len(sys.argv) > 1 else "docs/MGO2.ELF"

data = open(ELF, "rb").read()
phoff = struct.unpack(">Q", data[0x20:0x28])[0]
phentsize = struct.unpack(">H", data[0x36:0x38])[0]
phnum = struct.unpack(">H", data[0x38:0x3A])[0]

segs = []
for i in range(phnum):
    off = phoff + i * phentsize
    seg_type, _flags = struct.unpack(">II", data[off:off + 8])
    file_offset, vaddr, _paddr, filesz, _memsz, _align = struct.unpack(">QQQQQQ", data[off + 8:off + 56])
    if seg_type == 1:
        segs.append((vaddr, file_offset, filesz))


def v2o(vaddr):
    for v, o, sz in segs:
        if v <= vaddr < v + sz:
            return o + (vaddr - v)
    return None


md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)

# 1. Do the reference-cited addresses call the same stream accessors?
for address, label in [(0x00D3E9AC, "0xd3e9ac (ref: 0x4103 status/body)"),
                       (0x00D3E53C, "0xd3e53c (ref: matrix parser)"),
                       (0x00D3E4B0, "0xd3e4b0 (ref: slot complete)")]:
    offset = v2o(address)
    if offset is None:
        print(f"== {label}: not mapped ==")
        continue
    print(f"== {label} ==")
    count = 0
    calls = []
    for instruction in md.disasm(data[offset:offset + 60 * 4], address):
        if instruction.mnemonic in ("bl", "b") and instruction.op_str.startswith("0x"):
            calls.append((instruction.address, instruction.op_str))
        count += 1
        if count >= 60:
            break
    for addr, target in calls[:24]:
        print(f"  {addr:08x}: bl {target}")
    print()

# 2. Scan .text for branch-and-link instructions targeting these addresses.
ACCESSOR_CALLS = {0x00F2E280: "f2e280(word)", 0x00F2E5C0: "f2e5c0(bytes)", 0x00F2E20C: "f2e20c(word)"}


def find_bl_targets(target):
    """Scan .text for 'bl target' by encoding: 0x48000001 | ((target-pc)&0x3FFFFFC)."""
    hits = []
    text_start, text_end = 0x00D00000, 0x00F80000
    o = v2o(text_start)
    if o is None:
        return hits
    code = data[o:o + (text_end - text_start)]
    for instruction in md.disasm(code, text_start):
        if instruction.mnemonic == "bl" and instruction.op_str == hex(target):
            hits.append(instruction.address)
    return hits


for target, label in [(0x00D3E9AC, "0xd3e9ac"), (0x00D3E53C, "0xd3e53c"), (0x00D3E4B0, "0xd3e4b0")]:
    callers = find_bl_targets(target)
    print(f"callers of {label}: {[hex(c) for c in callers] or 'NONE'}")
