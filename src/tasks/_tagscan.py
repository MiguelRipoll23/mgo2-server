import struct, sys

data = open(r"C:/Users/migue/Downloads/MGO2.ELF", "rb").read()
SHIFT = 0x10454
BASE, END = 0x261000, 0x28f000  # p2p module range (session/queue/decoder/builder)

def s16(v):
    return v - 0x10000 if v & 0x8000 else v

targets = {0x1001: "tag0x1001", 0xc480: "tag0xc480", 0x5000: "tag0x5000", 0x1000: "tag0x1000"}
hits = []
for a in range(BASE, END, 4):
    w = struct.unpack_from(">I", data, a - SHIFT)[0]
    op = w >> 26
    u = w & 0xFFFF
    # immediates: cmpi/cmpli/addi/andi/ori/sth etc. (D-form ops)
    if u in targets and op not in (18, 16, 4, 19, 31, 63, 59, 62, 58):
        hits.append((a, w, op, u))

for a, w, op, u in hits:
    name = targets[u]
    kind = {10: "cmplwi", 11: "cmpwi", 14: "addi/li", 12: "addic.", 24: "ori", 25: "oris", 28: "andi.", 29: "andis.", 7: "mulli", 8: "subfic", 13: "addis", 32: "lwz", 34: "lbz", 36: "stw", 40: "lhz", 44: "sth", 38: "stb", 33: "lwzu", 35: "lbzu"}.get(op, f"op{op}")
    print(f"0x{a:06x}  {w:08x}  {kind} imm=0x{u:04x} ({name})")
print(f"total: {len(hits)}")
