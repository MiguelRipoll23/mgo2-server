#!/usr/bin/env python3
"""Disassemble the payload accessor f04920 to find the stream's backing buffer."""
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


def disasm(addr, count=120):
    from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64
    md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    md.detail = False
    code = read(addr, count * 4)
    out = []
    for insn in md.disasm(code, addr):
        txt = f"{insn.address:08x}: {insn.mnemonic:<10} {insn.op_str}"
        if insn.mnemonic == "bl":
            try:
                target = int(insn.op_str, 16)
                txt += f"   ; call"
            except ValueError:
                pass
        out.append(txt)
    return out


print("== FUN_00f04920 (payload accessor called by all parsers) ==")
for line in disasm(0xF04920, 100):
    print(line)
