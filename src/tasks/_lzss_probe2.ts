// TEMP: brute-probe the LZSS decompressor (doc §7) against the real captured
// 89-byte data-phase frame to find (a) where the bitstream starts and
// (b) the compressed message layout.

// Captured from the live log (SESSION-KEYED 89-byte frame, first occurrence):
const plain = Uint8Array.from([
  0x02, 0x80, // hdr 0x8002 (compression marker bit 7 of byte 1)
  0x80, 0xc4, 0x2b, 0x50, 0x08, 0x14, 0x00, 0x06, 0x88, 0x6c, 0x03, 0x4f,
  0x00, 0x8f, 0x51, 0x65, 0xf5, 0x79, 0xcd, 0x9e, 0xd1, 0x0e, 0x87, 0x40,
  0x01, 0xc1, 0x80, 0x42, 0xe2, 0xc0, 0xf0, 0xc1, 0xe1, 0xe0, 0x3c, 0x50,
  0xe1, 0x70, 0x08, 0xff, 0xff, 0xff, 0x0c, 0x0c, 0x42, 0x07, 0xff, 0x04,
  0x40, 0x3c, 0x10, 0x90, 0x03, 0x71, 0x8e, 0x06, 0x05, 0x1d, 0x0d, 0x82,
  0x01, 0x70, 0xb4, 0x5a, 0x6d, 0x96, 0x4b, 0xad, 0xb8, 0x52, 0x0b, 0xcc,
  0xca, 0x67, 0x00, 0x00, 0x00,
  0x60, 0x48, 0xbe, 0x93, // tail digest (4)
]);
// frame len = 89 → content = plain[2 .. 79)
const content = plain.subarray(2, plain.length - 4 - 6); // [2 .. len-0xa)
console.log("content bytes:", content.length, [...content].map((b) => b.toString(16).padStart(2, "0")).join(" "));

// Doc §7: MSB-first bitstream, 1 flag bit per token:
//   0 = literal byte
//   1 = back-reference: 9-bit offset, then 4-bit (length - 2)
// window 512 (& 0x1ff), mirrored writes at i and i+0x11
function tryDecode(start: number, flagZeroIsLiteral: boolean, out: Uint8Array): number {
  let bitPos = start * 8;
  const totalBits = content.length * 8;
  let outLen = 0;
  const win = new Uint8Array(0x200 + 0x20);
  let wi = 0;
  const readBit = (): number => {
    if (bitPos >= totalBits) return -1;
    const byte = content[bitPos >> 3];
    const bit = (byte >> (7 - (bitPos & 7))) & 1;
    bitPos++;
    return bit;
  };
  const readBits = (n: number): number => {
    let v = 0;
    for (let i = 0; i < n; i++) {
      const b = readBit();
      if (b < 0) return -1;
      v = (v << 1) | b;
    }
    return v;
  };
  const readByte = (): number => {
    // doc says literal byte — try 8 bits MSB-first
    return readBits(8);
  };
  while (bitPos < totalBits && outLen < out.length) {
    const flag = readBit();
    if (flag < 0) break;
    const literal = flagZeroIsLiteral ? flag === 0 : flag === 1;
    if (literal) {
      const b = readByte();
      if (b < 0) break;
      out[outLen++] = b;
      win[wi & 0x1ff] = b;
      win[(wi & 0x1ff) + 0x11] = b;
      wi++;
    } else {
      const off = readBits(9);
      if (off < 0) break;
      const lenBits = readBits(4);
      if (lenBits < 0) break;
      const mlen = lenBits + 2;
      for (let i = 0; i < mlen && outLen < out.length; i++) {
        const src = (wi - off) & 0x1ff;
        const b = win[src];
        out[outLen++] = b;
        win[wi & 0x1ff] = b;
        win[(wi & 0x1ff) + 0x11] = b;
        wi++;
      }
    }
  }
  return outLen;
}

for (const start of [0, 1, 2, 4, 6]) {
  for (const flagZeroIsLiteral of [true, false]) {
    const out = new Uint8Array(0x2000);
    const n = tryDecode(start, flagZeroIsLiteral, out);
    // printability score
    let printable = 0;
    for (let i = 0; i < Math.min(n, 64); i++) {
      const c = out[i];
      if ((c >= 0x20 && c < 0x7f) || c === 0) printable++;
    }
    console.log(
      `start=${start} zero=${flagZeroIsLiteral ? "lit" : "ref"} outLen=${n} printable=${printable}`,
      [...out.subarray(0, Math.min(n, 48))].map((b) => b.toString(16).padStart(2, "0")).join(" "),
      n > 0 ? JSON.stringify(String.fromCharCode(...out.subarray(0, Math.min(n, 32)))) : "",
    );
  }
}
