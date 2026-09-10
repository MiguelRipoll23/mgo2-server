#!/usr/bin/env python3
"""Analyze MGO2.ELF with Capstone: skill table bounds, equip-cost table."""
import struct, sys
from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64

ELF_PATH = "MGO2.ELF"

with open(ELF_PATH, "rb") as f:
    data = f.read()

# Parse ELF64 program headers (PPC64 BE)
assert data[:4] == b"\x7fELF"
e_phoff = struct.unpack(">Q", data[0x20:0x28])[0]
e_phentsize = struct.unpack(">H", data[0x36:0x38])[0]
e_phnum = struct.unpack(">H", data[0x38:0x3A])[0]

segs = []
for i in range(e_phnum):
    off = e_phoff + i * e_phentsize
    p_type, p_flags = struct.unpack(">II", data[off:off+8])
    p_offset, p_vaddr, p_paddr, p_filesz, p_memsz, p_align = struct.unpack(">QQQQQQ", data[off+8:off+56])
    if p_type == 1:  # PT_LOAD
        segs.append((p_vaddr, p_offset, p_filesz))

def v2o(vaddr):
    for v, o, sz in segs:
        if v <= vaddr < v + sz:
            return o + (vaddr - v)
    return None

def read_u32(vaddr):
    o = v2o(vaddr)
    if o is None or o + 4 > len(data):
        return None
    return struct.unpack(">I", data[o:o+4])[0]

def read_u16(vaddr):
    o = v2o(vaddr)
    if o is None or o + 2 > len(data):
        return None
    return struct.unpack(">H", data[o:o+2])[0]

def read_bytes(vaddr, n):
    o = v2o(vaddr)
    if o is None or o + n > len(data):
        return None
    return data[o:o+n]

md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
md.detail = False

def disasm(vaddr, count=20, label=""):
    o = v2o(vaddr)
    if o is None:
        print(f"{label} {hex(vaddr)}: no mapping")
        return
    print(f"{label} {hex(vaddr)}:")
    for ins in md.disasm(data[o:o+count*8], vaddr):
        print(f"  {ins.address:08x}: {ins.mnemonic:8s} {ins.op_str}")
        count -= 1
        if count <= 0: break

def scan_cmpwi(start, end, imm, label=""):
    """Find cmpwi rX,imm instructions in [start,end)."""
    hits = []
    o = v2o(start)
    e = v2o(end) if v2o(end) else start + (end - start)
    if o is None: return hits
    for ins in md.disasm(data[o:e-o], start):
        if ins.mnemonic.startswith("cmpwi") and f", {imm}" in ins.op_str:
            hits.append(ins.address)
    return hits

if __name__ == "__main__":
    print("== segments ==")
    for v, o, sz in segs:
        print(f"  vaddr {v:08x} file {o:08x} size {sz:x}")

    for addr in (0x8DC3A8, 0xB3B530, 0xB3B5B0, 0x6FC528, 0x897320, 0x8DB8A8, 0x6FCD48):
        print()
        print(f"== {hex(addr)} ==")
        print("  u32:", hex(read_u32(addr)) if read_u32(addr) is not None else "n/a")

    print()
    print("== scan for cmpwi with skill bounds in .text 0x6C0000..0x700000 ==")
    for imm in (16, 17, 24, 25, 0x10, 0x18, 0x19):
        hits = scan_cmpwi(0x6C0000, 0x720000, imm)
        if hits:
            print(f"  cmpwi ...,{imm}: {[hex(h) for h in hits[:12]]}{'...' if len(hits)>12 else ''}")

def disasm_range(start, end, label=""):
    o = v2o(start)
    if o is None:
        print(f"{label} {hex(start)}: no mapping")
        return
    n = end - start
    print(f"{label} {hex(start)}..{hex(end)}:")
    for ins in md.disasm(data[o:o+n], start):
        print(f"  {ins.address:08x}: {ins.mnemonic:8s} {ins.op_str}")

def find_u32_pattern(pat, start=0, end=None):
    end = end or len(data)
    hits = []
    p = struct.pack(">I", pat)
    pos = start
    while True:
        idx = data.find(p, pos, end)
        if idx == -1: break
        hits.append(idx)
        pos = idx + 1
    return hits

def find_string(s):
    b = s.encode()
    hits = []
    pos = 0
    while True:
        idx = data.find(b, pos)
        if idx == -1: break
        hits.append(idx)
        pos = idx + 1
    return hits

def o2v(off):
    for v, o, sz in segs:
        if o <= off < o + sz:
            return v + (off - o)
    return None

def find_xrefs(vaddr, window=0x1400000):
    """Find 64-bit pointers to vaddr in the file (raw pointer scan, BE)."""
    p = struct.pack(">Q", vaddr)
    hits = []
    pos = 0
    while True:
        idx = data.find(p, pos)
        if idx == -1: break
        hits.append(idx)
        pos = idx + 1
    return hits

def find_xrefs32(vaddr):
    p = struct.pack(">I", vaddr)
    hits = []
    pos = 0
    while True:
        idx = data.find(p, pos)
        if idx == -1: break
        hits.append(idx)
        pos = idx + 1
    return hits
