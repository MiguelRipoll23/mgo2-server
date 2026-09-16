#!/usr/bin/env python3
"""Disassemble the dispatcher stubs and the 4-byte stream reader to settle bounds."""
import struct

ELF = "docs/MGO2.ELF"
data = open(ELF, "rb").read()
phoff = struct.unpack(">Q", data[0x20:0x28])[0]
phentsize = struct.unpack(">H", data[0x36:0x38])[0]
phnum = struct.unpack(">H", data[0x38:0x3A])[0]
segs = []
for i in range(phnum):
    off = phoff + i * phentsize
    p_offset, p_vaddr = struct.unpack(">QQ", data[off + 8:off + 24])
    p_filesz = struct.unpack(">Q", data[off + 0x20:off + 0x28])[0]
    if p_filesz:
        segs.append((p_vaddr, p_offset, p_filesz))


def read(vaddr, n):
    for va, off, sz in segs:
        if va <= vaddr < va + sz:
            fo = off + (vaddr - va)
            return data[fo:fo + n]
    return None


def disasm(addr, count):
    from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64
    md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    code = read(addr, count * 4)
    for insn in md.disasm(code, addr):
        txt = f"{insn.address:08x}: {insn.mnemonic:<10} {insn.op_str}"
        if insn.mnemonic == "bl":
            txt += "   ; call"
        print(txt)


print("== dispatcher stub f05248 (0x4101) and f052b8 (0x4103) ==")
disasm(0xF05248, 8)
print()
disasm(0xF052B8, 8)
print()
print("== f2e280: the 4-byte stream reader (bounds check?) ==")
disasm(0xF2E280, 40)
print()
print("== f2ddec: stream reset ==")
disasm(0xF2DDEC, 5)
print()
print("== f01638: receive-record fetch ==")
disasm(0xF01638, 40)
