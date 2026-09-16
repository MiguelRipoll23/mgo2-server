#!/usr/bin/env python3
"""Resolve each burst command's parser via the dispatcher stubs, then count its stream reads."""
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


def disasm(addr, count):
    from capstone import Cs, CS_ARCH_PPC, CS_MODE_BIG_ENDIAN, CS_MODE_64
    md = Cs(CS_ARCH_PPC, CS_MODE_BIG_ENDIAN | CS_MODE_64)
    off = va_to_off(addr)
    code = data[off:off + count * 4]
    return [(i.address, i.mnemonic, i.op_str) for i in md.disasm(code, addr)]


# Dispatcher stubs inside FUN_00f0494c for the connect-burst commands.
stubs = {
    "0x4120 gameplay": 0xF05258,
    "0x4121 macros": 0xF05268,
    "0x4122 personal": 0xF05278,
    "0x4124 gear": 0xF05288,
    "0x4125 skills": 0xF05298,
    "0x4140 skillsets": 0xF05338,
    "0x4142 gearsets": 0xF05358,
}

parsers = {}
for name, stub in stubs.items():
    insns = disasm(stub, 3)
    target = None
    for _, mn, op in insns:
        if mn == "bl":
            target = int(op.split()[0], 16)
            break
    parsers[name] = target
    print(f"{name}: stub {stub:08x} -> parser {target:08x}")

print()
ACCESSORS = {0xF2E134: "u8", 0xF2E0FC: "u8", 0xF2E1BC: "u16", 0xF2E280: "u32", 0xF2E5C0: "memcpy"}

for name, parser in parsers.items():
    if parser is None:
        continue
    counts = {"u8": 0, "u16": 0, "u32": 0, "memcpy": 0}
    loops = []
    body = disasm(parser, 900)
    for addr, mn, op in body:
        if mn == "bl":
            try:
                tgt = int(op.split()[0], 16)
            except ValueError:
                continue
            if tgt in ACCESSORS:
                counts[ACCESSORS[tgt]] += 1
        if mn == "mtctr" and op == "r0":
            # look back a few instructions for the load of r0
            pass
    # crude loop detection: bdnz with a backward target
    addrs = [a for a, _, _ in body]
    for idx, (addr, mn, op) in enumerate(body):
        if mn == "bdnz":
            loops.append(hex(addr))
    total = counts["u8"] * 1 + counts["u16"] * 2 + counts["u32"] * 4
    print(f"== {name} parser {parser:08x}: u8={counts['u8']} u16={counts['u16']} "
          f"u32={counts['u32']} memcpy={counts['memcpy']} loops(bdnz)={len(loops)} "
          f"=> straight-line >= {total} bytes ==")
