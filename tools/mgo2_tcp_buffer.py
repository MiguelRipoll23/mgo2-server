#!/usr/bin/env python3
"""Disassemble f01638 to find the TCP receive buffer layout.

Usage: python tools/mgo2_tcp_buffer.py [elf-path]
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

address = 0x00F01638
offset = v2o(address)
print(f"== f01638: 0x{address:08x} ==")
count = 0
for instruction in md.disasm(data[offset:offset + 80 * 4], address):
    print(f"  {instruction.address:08x}: {instruction.mnemonic:10s} {instruction.op_str}")
    count += 1
    if instruction.mnemonic == "blr" or count >= 80:
        break
