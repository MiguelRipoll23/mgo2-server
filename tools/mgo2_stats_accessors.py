#!/usr/bin/env python3
"""Disassemble the stream accessors to pin their read sizes.

Usage: python tools/mgo2_stats_accessors.py [elf-path]
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


ACCESSORS = {
    0x00F2E0FC: "f2e0fc (unknown)",
    0x00F2E134: "f2e134 (read 1 byte -> r3?)",
    0x00F2E1BC: "f2e1bc (read 2 bytes -> buf?)",
    0x00F2E280: "f2e280 (read 4 bytes -> buf?)",
    0x00F2E5C0: "f2e5c0 (copy r5 bytes -> buf?)",
}

md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)

for address, label in ACCESSORS.items():
    offset = v2o(address)
    if offset is None:
        print(f"== {label}: 0x{address:08x} not mapped ==")
        continue
    print(f"== {label}: 0x{address:08x} ==")
    count = 0
    for instruction in md.disasm(data[offset:offset + 40 * 4], address):
        print(f"  {instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")
        count += 1
        if instruction.mnemonic == "blr" or count >= 40:
            break
    print()
