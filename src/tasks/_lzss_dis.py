import struct

data = open(r"C:/Users/migue/Downloads/MGO2.ELF", "rb").read()
SHIFT = 0x00000

def s16(v):
    return v - 0x10000 if v & 0x8000 else v

def dec(w, addr):
    op = w >> 26
    if op == 14:  # addi
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        imm = s16(w & 0xFFFF)
        return f"{'li' if rA == 0 else 'addi'} r{rD}," + (f" {imm}" if rA == 0 else f" r{rA}, {imm}") + f" (0x{imm & 0xffff:04x})"
    if op == 15:  # addis/oris
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        imm = w & 0xFFFF
        return f"{'lis' if rA == 0 else 'addis'} r{rD}," + (f" 0x{imm:04x}" if rA == 0 else f" r{rA}, 0x{imm:04x}")
    if op == 12:  # addic
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"addic r{rD}, r{rA}, {s16(w & 0xFFFF)}"
    if op == 10:  # cmpli
        crf = (w >> 23) & 7
        rA, imm = (w >> 16) & 31, w & 0xFFFF
        return f"cmplwi cr{crf}, r{rA}, 0x{imm:04x}" if (w >> 26) == 10 else ""
    if op == 11:  # cmpi
        crf = (w >> 23) & 7
        rA, imm = (w >> 16) & 31, s16(w & 0xFFFF)
        return f"cmpwi cr{crf}, r{rA}, {imm} (0x{imm & 0xffff:04x})"
    if op == 24:  # ori
        rS, rA, imm = (w >> 21) & 31, (w >> 16) & 31, w & 0xFFFF
        return f"{'nop' if w == 0x60000000 else f'ori r{rA}, r{rS}, 0x{imm:04x}'}"
    if op == 25:  # oris
        rS, rA, imm = (w >> 21) & 31, (w >> 16) & 31, w & 0xFFFF
        return f"oris r{rA}, r{rS}, 0x{imm:04x}"
    if op == 20:  # rlwimi
        rS, rA, sh, mb, me = (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31, (w >> 6) & 31, (w >> 1) & 31
        return f"rlwimi r{rA}, r{rS}, {sh}, {mb}, {me}"
    if op == 21:  # rlwinm
        rS, rA, sh, mb, me = (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31, (w >> 6) & 31, (w >> 1) & 31
        rc = w & 1
        if sh == 0 and mb == 0 and me == 31:
            return f"{'mr.' if rc else 'mr'} r{rA}, r{rS}"
        if sh == 0 and mb == 0:
            return f"{'clrrwi' if not rc else 'clrrwi.'} r{rA}, r{rS}, {31 - me}"
        if me == 31:
            return f"{'slwi' if not rc else 'slwi.'} r{rA}, r{rS}, {sh}" if mb == 0 else f"rlwinm r{rA}, r{rS}, {sh}, {mb}, {me}"
        return f"rlwinm{'.' if rc else ''} r{rA}, r{rS}, {sh}, {mb}, {me}"
    if op == 30:  # rld* (64-bit)
        rS, rA = (w >> 21) & 31, (w >> 16) & 31
        sh = ((w >> 11) & 31) | ((w & 2) << 4)
        m = (w >> 5) & 0x7f
        xo = (w >> 2) & 7
        rc = w & 1
        # rldicl xo=0, rldicr xo=1, rldic xo=2, rldimi xo=3
        if xo == 0:
            if m == 0:
                return f"{'extsw' if sh == 32 else f'srdi r{rA}, r{rS}, {64 - sh}'}" if not rc else f"rldicl. r{rA}, r{rS}, {sh}, {m}"
            return f"rldicl{'.' if rc else ''} r{rA}, r{rS}, {sh}, {m}"
        if xo == 1:
            if m == 63 - sh:
                return f"sldi r{rA}, r{rS}, {sh}"
            return f"rldicr{'.' if rc else ''} r{rA}, r{rS}, {sh}, {m}"
        if xo == 2:
            return f"rldic{'.' if rc else ''} r{rA}, r{rS}, {sh}, {m}"
        if xo == 3:
            return f"rldimi{'.' if rc else ''} r{rA}, r{rS}, {sh}, {m}"
        if xo == 8:  # clrldi via rldicl with m=0 handled above
            return f"rld-clr8 r{rA}, r{rS}, {sh}, {m}"
        return f"rld?{xo} r{rA}, r{rS}, sh={sh}, m={m}"
    if op == 31:  # x-form
        rS, rA, rB = (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31
        xo = (w >> 1) & 0x3ff
        rc = w & 1
        names = {
            26: "cntlzw", 28: "and", 444: "or", 316: "xor", 476: "nor", 124: "nand",
            284: "eqv", 412: "orc", 23: "slw", 536: "srw", 792: "sraw", 824: "srawi",
            8: "subfc", 40: "subf", 104: "neg", 10: "addc", 266: "add", 234: "mulhw?",
            11: "mulhwu", 75: "mulhw", 45: "icbt" , 150: "stwbrx", 20: "lwbrx",
            87: "lbzx", 119: "lbzux", 23: "slw", 279: "lhzx", 539: "lbzx?",
            55: "lwzx", 278: "lhzu?", 21: "lwa?", 533: "lswx",
            534: "lwbrx", 661: "stwx", 407: "sthx", 215: "stbx",
            27: "sld?", 24: "slw?", 566: "??", 28: "and",
            954: "extsb", 922: "extsh", 235: "mul?", 165: "mul?",
            451: "sthx?", 203: "stswx?", 210: "??",
            1014: "??", 814: "sradi?", 413: "sradi",
            9: "mulhdu?", 73: "mulhd", 233: "mulld", 161: "??",
            401: "??", 138: "ae?", 10: "addc", 522: "??",
        }
        # decode common ones precisely
        if xo == 444: return f"{'mr' if rB == rS and rA == rS else 'or'}{'' if not rc else '.'} r{rA}, r{rS}, r{rB}"
        if xo == 28: return f"and{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 316: return f"xor{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 476: return f"nor{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 8: return f"subfc{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 40: return f"subf{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 104: return f"neg{'.' if rc else ''} r{rA}, r{rS}"
        if xo == 266: return f"add{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 10: return f"addc{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 11: return f"mulhwu r{rA}, r{rS}, r{rB}"
        if xo == 75: return f"mulhw r{rA}, r{rS}, r{rB}"
        if xo == 9: return f"mulhdu r{rA}, r{rS}, r{rB}"
        if xo == 73: return f"mulhd r{rA}, r{rS}, r{rB}"
        if xo == 233: return f"mulld{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 23: return f"slw{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 536: return f"srw{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 792: return f"sraw{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 824: return f"srawi{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 27: return f"sld{'.' if rc else ''} r{rA}, r{rS}, r{rB}"
        if xo == 24: return f"sld{'' if not rc else '.'} r{rA}, r{rS}, r{rB}"  # actually slw variant
        if xo == 26: return f"cntlzw{'.' if rc else ''} r{rA}, r{rS}"
        if xo == 954: return f"extsb{'.' if rc else ''} r{rA}, r{rS}"
        if xo == 922: return f"extsh{'.' if rc else ''} r{rA}, r{rS}"
        if xo == 87: return f"lbzx r{rA}, r{rS}, r{rB}" if rS else f"lbzx r{rA}, 0, r{rB}"
        if xo == 279: return f"lhzx r{rA}, r{rS}, r{rB}"
        if xo == 55: return f"lwzx r{rA}, r{rS}, r{rB}"
        if xo == 661: return f"stwx r{rS}, r{rA}, r{rB}"
        if xo == 407: return f"sthx r{rS}, r{rA}, r{rB}"
        if xo == 215: return f"stbx r{rS}, r{rA}, r{rB}"
        if xo == 86: return f"ecowx?"
        if xo == 339 or xo == 467: return f"op31 xo={xo} (mfspr/mtspr)"
        if xo == 19: return f"op31 xo=19 (mfcr?)"
        if xo == 150: return f"stwbrx r{rS}, r{rA}, r{rB}"
        if xo == 20: return f"lwbrx r{rA}, r{rS}, r{rB}"
        if xo == 534: return f"lwbrx r{rA}, r{rS}, r{rB}"
        if xo == 814: return f"sradi{'.' if rc else ''} r{rA}, r{rS}, {(w >> 11) & 31}"
        if xo == 413: return f"sradi{'.' if rc else ''} r{rA}, r{rS}, {(w >> 11) & 31}"
        return f"op31 xo={xo} rc={rc} r{rA}, r{rS}, r{rB}"
    if op == 32:  # lwz
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"lwz r{rD}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 33:  # lwzu
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"lwzu r{rD}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 34:  # lbz
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"lbz r{rD}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 35:  # lbzu
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"lbzu r{rD}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 36:  # stw
        rS, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"stw r{rS}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 38:  # stb
        rS, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"stb r{rS}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 40:  # lhz
        rD, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"lhz r{rD}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 44:  # sth
        rS, rA = (w >> 21) & 31, (w >> 16) & 31
        return f"sth r{rS}, {s16(w & 0xFFFF)}(r{rA})"
    if op == 16:  # bc
        bo, bi = (w >> 21) & 31, (w >> 16) & 31
        bd = s16(w & 0xFFFC)
        lk = w & 1
        cond = {0: "lt", 1: "gt", 2: "eq", 3: "so"}.get(bi & 3, "?")
        if bo == 12: name = f"b{'lr' if lk else ''}{cond}"
        elif bo == 4: name = f"bne{'lr' if lk else ''}" if (bi & 3) == 2 else f"b{'lr' if lk else ''}{cond}"
        elif bo == 16: name = f"bdnz"
        else: name = f"bc bo={bo} bi={bi}"
        tgt = addr + bd
        return f"{name} 0x{tgt:x}"
    if op == 18:  # b
        li = s16(w & 0x03FFFFFC)
        lk = w & 1
        aa = (w >> 1) & 1
        tgt = (li if aa else addr + li)
        return f"{'bl' if lk else 'b'} 0x{tgt & 0xffffffff:x}"
    if op == 17: return "sc"
    if op == 18: return "b"
    return f"op{op} 0x{w:08x}"

start = 0x00efd840
end = 0x00efda24
off = start - SHIFT
for i in range((end - start) // 4):
    w = struct.unpack_from(">I", data, off + i * 4)[0]
    a = start + i * 4
    print(f"0x{a:06x}  {w:08x}  {dec(w, a)}")
