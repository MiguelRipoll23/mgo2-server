import struct, sys

data = open(r"C:/Users/migue/Downloads/MGO2.ELF", "rb").read()
SHIFT = 0x10454

def s16(v):
    return v - 0x10000 if v & 0x8000 else v

def dec(off, w):
    op = w >> 26
    rD, rA, rB = (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31
    imm = s16(w & 0xFFFF)
    u = w & 0xFFFF
    if op == 14:
        return f"li r{rD}, {imm:+d} (0x{u:04x})" if rA == 0 else f"addi r{rD}, r{rA}, {imm:+d} (0x{u:04x})"
    if op == 15:
        return f"lis r{rD}, 0x{u:04x}"
    if op == 12:
        return f"addic. r{rD}, r{rA}, {imm:+d}"
    if op == 8:
        return f"subfic r{rD}, r{rA}, {imm:+d}"
    if op == 7:
        return f"mulli r{rD}, r{rA}, {imm:+d}"
    if op == 13:
        return f"addis r{rD}, r{rA}, 0x{u:04x}" if rA else f"lis r{rD}, 0x{u:04x}"
    if op == 10:  # cmpli
        return f"cmplwi cr{rD>>2}, r{rA}, 0x{u:04x}"
    if op == 11:  # cmpi
        return f"cmpwi cr{rD>>2}, r{rA}, {imm:+d}"
    if op == 24:
        if rA == 0: return f"nop" if w == 0x60000000 else f"ori r{rD}, r0, 0x{u:04x}"
        return f"ori r{rD}, r{rA}, 0x{u:04x}"
    if op == 25:
        return f"oris r{rD}, r{rA}, 0x{u:04x}"
    if op == 28:
        return f"andi. r{rD}, r{rA}, 0x{u:04x}"
    if op == 29:
        return f"andis. r{rD}, r{rA}, 0x{u:04x}"
    if op == 32:
        return f"lwz r{rD}, {imm:+d}(r{rA})"
    if op == 34:
        return f"lbz r{rD}, {imm:+d}(r{rA})"
    if op == 35:
        return f"lbzx r{rD}, r{rA}, r{rB}"
    if op == 36:
        return f"stw r{rD}, {imm:+d}(r{rA})"
    if op == 38:
        return f"stb r{rD}, {imm:+d}(r{rA})"
    if op == 40:
        return f"lhz r{rD}, {imm:+d}(r{rA})"
    if op == 44:
        return f"sth r{rD}, {imm:+d}(r{rA})"
    if op == 31:
        xo = (w >> 1) & 0x3FF
        names = {266: "add", 40: "subf", 87: "lbzx", 279: "lhzx", 23: "lwzx", 215: "stbx", 407: "sthx", 151: "stwx", 28: "and", 444: "or", 316: "xor", 26: "cntlzw", 922: "extsh", 954: "extsb", 24: "slw", 536: "srw", 792: "sraw", 824: "srawi", 235: "mullw", 491: "divw", 459: "divwu", 104: "neg", 284: "eqv", 476: "nand", 124: "nor", 412: "orc", 28: "and"}
        nm = names.get(xo, f"xop{xo}")
        if xo == 444: return f"or r{rD}, r{rA}, r{rB}"
        if xo == 28: return f"and r{rD}, r{rA}, r{rB}"
        if xo == 316: return f"xor r{rD}, r{rA}, r{rB}"
        if xo == 266: return f"add r{rD}, r{rA}, r{rB}"
        if xo == 40: return f"subf r{rD}, r{rA}, r{rB}"
        if xo == 24: return f"slw r{rD}, r{rA}, r{rB}"
        if xo == 536: return f"srw r{rD}, r{rA}, r{rB}"
        if xo == 792: return f"sraw r{rD}, r{rA}, r{rB}"
        if xo == 824: return f"srawi r{rD}, r{rA}, {rB}"
        if xo == 26: return f"cntlzw r{rD}, r{rA}"
        if xo == 922: return f"extsh r{rD}, r{rA}"
        if xo == 954: return f"extsb r{rD}, r{rA}"
        if xo == 279: return f"lhzx r{rD}, r{rA}, r{rB}"
        if xo == 87: return f"lbzx r{rD}, r{rA}, r{rB}"
        if xo == 23: return f"lwzx r{rD}, r{rA}, r{rB}"
        if xo == 407: return f"sthx r{rD}, r{rA}, r{rB}"
        if xo == 215: return f"stbx r{rD}, r{rA}, r{rB}"
        if xo == 151: return f"stwx r{rD}, r{rA}, r{rB}"
        if xo == 339: return f"mfspr r{rD}, SPR{(w>>11)&0x3FF}"  # mflr etc
        if xo == 467: return f"mtspr SPR{(w>>11)&0x3FF}, r{rD}"
        if xo == 19: return f"mfcr r{rD}"
        if xo == 0: return f"cmp cr{rD>>2}, r{rA}, r{rB}"
        if xo == 32: return f"cmplw cr{rD>>2}, r{rA}, r{rB}"
        return f"XO({xo}) r{rD}, r{rA}, r{rB}"
    if op == 21:  # rlwinm
        sh = (w >> 11) & 31
        mb = (w >> 6) & 31
        me = (w >> 1) & 31
        return f"rlwinm r{rD}, r{rA}, {sh}, {mb}, {me}"
    if op == 20:
        sh = (w >> 11) & 31
        mb = (w >> 6) & 31
        me = (w >> 1) & 31
        return f"rlwimi r{rD}, r{rA}, {sh}, {mb}, {me}"
    if op == 23:
        sh = (w >> 11) & 31
        mb = (w >> 6) & 31
        me = (w >> 1) & 31
        return f"rlwnm r{rD}, r{rA}, r{rB}, {mb}, {me}"
    if op == 18:  # b
        li = w & 0x03FFFFFC
        if li & 0x02000000: li -= 0x04000000
        aa = (w >> 1) & 1
        lk = w & 1
        tgt = li if aa else off + li
        cond = (w >> 5) & 0x3FF if False else None
        bo = (w >> 21) & 31
        bi = (w >> 16) & 31
        if op == 18:
            if bo == 16:
                kind = ["blt", "bgt", "beq", "bso", "bge", "ble", "bne", "bns"][bi & 7]
                if bi & 16: kind = kind[1:]  # 'lt'->'t' style for bclr forms; not used
                return f"b{kind[1:]}lr" if False else f"{kind} cr{bi>>2}, 0x{tgt:x}{',l' if lk else ''}".replace(",l","l") if lk else f"{kind} cr{bi>>2}, 0x{tgt:x}"
            return f"{'b' if not lk else 'bl'} 0x{tgt:x}"
    if op == 16:  # bc
        bo = (w >> 21) & 31
        bi = (w >> 16) & 31
        bd = s16(w & 0xFFFC)
        aa = (w >> 1) & 1
        lk = w & 1
        tgt = bd if aa else off + bd
        kind = ["lt", "gt", "eq", "so", "ge", "le", "ne", "ns"][bi & 3] if (bo & 4) else "d"
        return f"b{kind}{'l' if lk else ''} cr{bi>>2}, 0x{tgt:x}" if bo & 4 else f"bc 0x{bo:02x}, {bi}, 0x{tgt:x}{'l' if lk else ''}"
    if op == 17:
        return f"sc"
    if op == 19:
        xo = (w >> 1) & 0x3FF
        if xo == 16: return f"bclr {['bltlr','bclr','beqlr','bsolr','bgelr','blelr','bnelr','bnslr'][(w>>11)&7] if ((w>>21)&31)==16 else 'bclr'}"
        if xo == 528: return f"bcctr {((w>>21)&31)}"
        return f"CR-XO({xo})"
    return f"op{op} 0x{w:08x}"

start = int(sys.argv[1], 16)
end = int(sys.argv[2], 16)
off = start - SHIFT
a = start
while a < end:
    w = struct.unpack_from(">I", data, a - SHIFT)[0]
    print(f"0x{a:06x}  {w:08x}  {dec(a, w)}")
    a += 4
