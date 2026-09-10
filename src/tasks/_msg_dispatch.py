"""Part 10: compressor match-emit region (how off/len fields are produced)."""
import struct
from capstone import Cs, CS_ARCH_PPC, CS_MODE_32, CS_MODE_BIG_ENDIAN

data = open("MGO2.ELF", "rb").read()
def v2o(v):
    return v - 0x10000

md = Cs(CS_ARCH_PPC, CS_MODE_32 | CS_MODE_BIG_ENDIAN)
def dis(v, cnt, label=""):
    print(f"\n--- {label} 0x{v:08x} ({cnt} insns) ---")
    for ins in md.disasm(data[v2o(v):v2o(v) + 4 * cnt], v):
        print(f"0x{ins.address:08x}  {ins.mnemonic:<10} {ins.op_str}")

# compressor: after hash-match lookup, emit match token (off/len) or literal
dis(0x00efe400, 96, "compressor emit region")
# the second decompress call site in the decoder (~0x266960 per decoder-disasm.txt)
dis(0x266940, 40, "decoder: first decompress call site")
