#!/usr/bin/env python3
"""Disassemble the payload accessor and receive path helpers.

Usage: python tools/mgo2_payload_accessor.py [elf-path]
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


TARGETS = {
    0x00F04920: "f04920 payload accessor",
    0x00F2DE00: "f2de00 (post-parse notify)",
    0x00F2DDEC: "f2ddec (stream reset)",
}

md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)

for address, label in TARGETS.items():
    offset = v2o(address)
    if offset is None:
        print(f"== {label}: 0x{address:08x} not mapped ==")
        continue
    print(f"== {label}: 0x{address:08x} ==")
    count = 0
    for instruction in md.disasm(data[offset:offset + 30 * 4], address):
        print(f"  {instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")
        count += 1
        if instruction.mnemonic == "blr" or count >= 30:
            break
    print()
