#!/usr/bin/env python3
"""Scan the ELF for pointers to the two candidate 0x4101 parsers, then compare them."""
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


def va_to_off(vaddr):
    for va, off, sz in segs:
        if va <= vaddr < va + sz:
            return off + (vaddr - va)
    return None


def scan_pointer(target):
    """Find every 8-byte big-endian word equal to target (code pointer / OPD entry)."""
    needle = struct.pack(">Q", target)
    hits = []
    start = 0
    while True:
        idx = data.find(needle, start)
        if idx < 0:
            break
        # Map file offset back to a virtual address.
        va = None
        for base, off, sz in segs:
            if off <= idx < off + sz:
                va = base + (idx - off)
                break
        hits.append((va, idx))
        start = idx + 1
    return hits


def disasm(addr, count):
    from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64
    md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    off = va_to_off(addr)
    if off is None:
        print(f"  {addr:08x}: NOT IN A LOADED SEGMENT")
        return
    code = data[off:off + count * 4]
    for insn in md.disasm(code, addr):
        print(f"{insn.address:08x}: {insn.mnemonic:<10} {insn.op_str}")


for name, addr in [("f08a18 (dispatcher f05248 target)", 0xF08A18), ("d3c120 (reference-cited)", 0xD3C120)]:
    hits = scan_pointer(addr)
    print(f"== pointers to {name}: {len(hits)} ==")
    for va, fo in hits[:20]:
        print(f"  at va {va:08x} (file {fo:08x})")

print()
print("== d3c120: first 120 instructions ==")
disasm(0xD3C120, 120)
