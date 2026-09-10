"""Verified LZSS decoder for the MGO2 UDP data phase (docs/udp-p2p-protocol.md §7).

Semantics recovered instruction-exact from MGO2.ELF FUN_00efd840 (Capstone) and
confirmed on the live capture: the compressed frame (hdr 0x8002) decompresses
plain[2 .. len-0xa) to a 94-byte (4 + 0x5a) type-0x1001 record containing the
joiner's account name byte-for-byte. Feed it a decrypted frame's [2..len-0xa)
bytes.

- bitstream: MSB-first, 1 flag bit per token
- flag 1 = literal (8 bits), flag 0 = back-reference
- back-ref: 9-bit ABSOLUTE ring offset, then 4-bit length field,
  length = field + 2 (2..17; the copy loop's exit check is off by one, and the
  compressor symmetrically stores len-2)
- match byte j is read from ring[(off + j) & 0x1FF]; every emitted byte (literal
  or copied) is also written to ring[w++ & 0x1FF] where the write index starts
  at 1 (li r31, 1 @ 0x00efd894) — ring[0] stays zero, so ring position k holds
  output byte k-1 (off is effectively 1-based: match byte j = output[off+j-1])
- offset 0 = end-of-stream marker
"""

def decode(src, maxout=0x2000):
    out = bytearray()
    ring = bytearray(0x200)
    w = 1
    bitpos = 0
    total = len(src) * 8
    toks = []

    def bit():
        nonlocal bitpos
        if bitpos >= total:
            return -1
        v = (src[bitpos >> 3] >> (7 - (bitpos & 7))) & 1
        bitpos += 1
        return v

    def bits(k):
        v = 0
        for _ in range(k):
            b = bit()
            if b < 0:
                return -1
            v = (v << 1) | b
        return v

    while bitpos < total and len(out) < maxout:
        flag = bit()
        if flag < 0:
            break
        if flag == 1:
            b = bits(8)
            if b < 0:
                break
            out.append(b)
            ring[w & 0x1FF] = b
            w += 1
            toks.append(f"L{b:02x}")
        else:
            off = bits(9)
            lf = bits(4)
            if off < 0 or lf < 0:
                break
            if off == 0:
                toks.append(f"EOF(lf={lf}) @{bitpos - 13}b")
                break
            mlen = lf + 2
            toks.append(f"M(off={off},len={mlen})")
            for j in range(mlen):
                if len(out) >= maxout:
                    break
                b = ring[(off + j) & 0x1FF]
                out.append(b)
                ring[w & 0x1FF] = b
                w += 1
    return bytes(out), len(out), toks, bitpos


PLAIN = bytes.fromhex(
    "02 80 80 c4 2b 50 08 14 00 06 88 6c 03 4f 00 8f 51 65 f5 79 cd 9e d1 0e 87 40 01"
    " c1 80 42 e2 c0 f0 c1 e1 e0 3c 50 e1 70 08 ff ff ff 0c 0c 42 07 ff 04 40 3c 10 90"
    " 03 71 8e 06 05 1d 0d 82 01 70 b4 5a 6d 96 4b ad b8 52 0b cc ca 67 00 00 00 d8 c6"
    " 77 7d 60 3f 4d 01 91 7c".replace(" ", "")
)

src = PLAIN[2:79]  # chain/stream region before the 10-byte tail
out, n, toks, consumed = decode(src)
name_at = out.find(b"phildunphy23")
print(f"output={n} bytes (expect 94 = 4 + 0x5a), consumed {consumed}/{len(src) * 8} bits")
print(f"account name at offset {name_at:#x}: {out[name_at:name_at + 12].decode('ascii')!r}")
assert n == 94 and name_at == 0x51, "reference decode regressed"
print("\n--- full output hexdump ---")
for i in range(0, n, 16):
    row = out[i:i + 16]
    hexs = " ".join(f"{b:02x}" for b in row)
    asc = "".join(chr(b) if 32 <= b < 127 else "." for b in row)
    print(f"{i:04x}  {hexs:<48}  {asc}")
