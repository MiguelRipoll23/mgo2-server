from capstone import *

data = open(r"C:/Users/migue/Downloads/MGO2.ELF", "rb").read()
SHIFT = 0x10454

md = Cs(CS_ARCH_PPC, CS_MODE_32)
md.skipdata = True

start, end = 0x00efd340, 0x00efd850
off = start - SHIFT
count = 0
for ins in md.disasm(data[off:off + (end - start)], start):
    print(f"0x{ins.address:06x}  {ins.bytes.hex():<8}  {ins.mnemonic:<10} {ins.op_str}")
    count += 1
print(f"--- {count} insns")
