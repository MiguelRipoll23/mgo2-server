"""Byte-order evidence, part 3: the write side (frame builder stores).

Finds the reverse-walking store helper the frame builder uses (the mirror of
reader A at 0x26ca98 / reader B at 0x26cc88) and the tag-0x5000 store site.
"""
from capstone import Cs, CS_ARCH_PPC, CS_MODE_32, CS_MODE_BIG_ENDIAN

data = open("MGO2.ELF", "rb").read()


def v2o(v):
    return v - 0x10000


md = Cs(CS_ARCH_PPC, CS_MODE_32 | CS_MODE_BIG_ENDIAN)


def dis(v, cnt, label=""):
    print(f"\n--- {label} 0x{v:08x} ({cnt} insns) ---")
    for ins in md.disasm(data[v2o(v):v2o(v) + 4 * cnt], v):
        print(f"0x{ins.address:08x}  {ins.mnemonic:<10} {ins.op_str}")


# scan the helper cluster for the reverse-walking WRITER (stb with forward
# dest, backward src — mirror of reader A) and dump candidates
for base, cnt, label in [
    (0x26cb20, 40, "helper cluster after reader A"),
    (0x2698a0, 48, "frame builder: tag 0x1000 store + call"),
    (0x2699f0, 32, "frame builder: tag 0x5000 store + call"),
    (0x26ca90, 8, "reader A prologue (for reference)"),
]:
    dis(base, cnt, label)
