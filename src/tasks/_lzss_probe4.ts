// TEMP: LZSS probe on the REAL big-frame body (type 0xc480, len 0x2b, flags2 0x50,
// body 43 bytes per the msg layout type-LE | len | flags2 | body).
// Sweep: stream start, bit order, flag polarity, offset/length field widths.

const plain = Uint8Array.from([
  0x02, 0x80, 0x80, 0xc4, 0x2b, 0x50, 0x08, 0x14, 0x00, 0x06, 0x88, 0x6c,
  0x03, 0x4f, 0x00, 0x8f, 0x51, 0x65, 0xf5, 0x79, 0xcd, 0x9e, 0xd1, 0x0e,
  0x87, 0x40, 0x01, 0xc1, 0x80, 0x42, 0xe2, 0xc0, 0xf0, 0xc1, 0xe1, 0xe0,
  0x3c, 0x50, 0xe1, 0x70, 0x08, 0xff, 0xff, 0xff, 0x0c, 0x0c, 0x42, 0x07,
  0xff, 0x04, 0x40, 0x3c, 0x10, 0x90, 0x03, 0x71, 0x8e, 0x06, 0x05, 0x1d,
  0x0d, 0x82, 0x01, 0x70, 0xb4, 0x5a, 0x6d, 0x96, 0x4b, 0xad, 0xb8, 0x52,
  0x0b, 0xcc, 0xca, 0x67, 0x00, 0x00, 0x00, 0x60, 0x48, 0xbe, 0x93, 0x95,
  0x52, 0x43, 0xab, 0x61, 0xbe,
]);

// candidates for the compressed stream within `plain`
const cands: Array<[string, Uint8Array]> = [
  ["body(43)@7", plain.subarray(7, 7 + 43)],
  ["body@7+tail", plain.subarray(7, plain.length - 4)],
  ["content@2", plain.subarray(2, plain.length - 4)],
  ["content@2-6tail", plain.subarray(2, plain.length - 10)],
  ["after_type_len@5", plain.subarray(5, plain.length - 4)],
  ["after_4b_hdr@6", plain.subarray(6, plain.length - 4)],
];

function decode(
  src: Uint8Array,
  msbFirst: boolean,
  zeroIsLiteral: boolean,
  offBits: number,
  lenBits: number,
  minLen: number,
  out: Uint8Array,
): { n: number; consumedBits: number } {
  let bitPos = 0;
  const totalBits = src.length * 8;
  let outLen = 0;
  const win = new Uint8Array(0x200 + 0x20);
  let wi = 0;
  const bit = (): number => {
    if (bitPos >= totalBits) return -1;
    const byte = src[bitPos >> 3];
    const b = msbFirst ? (byte >> (7 - (bitPos & 7))) & 1 : (byte >> (bitPos & 7)) & 1;
    bitPos++;
    return b;
  };
  const bits = (n: number): number => {
    let v = 0;
    for (let i = 0; i < n; i++) {
      const b = bit();
      if (b < 0) return -1;
      v = msbFirst ? (v << 1) | b : v | (b << i);
    }
    return v;
  };
  while (bitPos < totalBits - 4 && outLen < out.length) {
    const flag = bit();
    if (flag < 0) break;
    if ((flag === 0) === zeroIsLiteral) {
      const b = bits(8);
      if (b < 0) break;
      out[outLen++] = b;
      win[wi & 0x1ff] = b;
      win[(wi & 0x1ff) + 0x11] = b;
      wi++;
    } else {
      const off = bits(offBits);
      if (off < 0) break;
      const lb = bits(lenBits);
      if (lb < 0) break;
      const mlen = lb + minLen;
      if (off === 0 || off > wi) break;
      for (let i = 0; i < mlen && outLen < out.length; i++) {
        const b = win[(wi - off) & 0x1ff];
        out[outLen++] = b;
        win[wi & 0x1ff] = b;
        win[(wi & 0x1ff) + 0x11] = b;
        wi++;
      }
    }
  }
  return { n: outLen, consumedBits: bitPos };
}

for (const [name, src] of cands) {
  for (const msbFirst of [true, false]) {
    for (const zeroIsLiteral of [true, false]) {
      const out = new Uint8Array(0x2000);
      const { n, consumedBits } = decode(src, msbFirst, zeroIsLiteral, 9, 4, 2, out);
      // score: fraction of zeros + run of zeros at start (MGO messages are struct-heavy)
      let zeros = 0;
      for (let i = 0; i < n; i++) if (out[i] === 0) zeros++;
      const head = [...out.subarray(0, 12)].map((b) => b.toString(16).padStart(2, "0")).join(" ");
      const tail4 = [...out.subarray(Math.max(0, n - 4), n)].map((b) => b.toString(16).padStart(2, "0")).join(" ");
      const zr = n ? (zeros / n).toFixed(2) : "0";
      if (n > 32) {
        console.log(
          `${name} msb=${msbFirst ? 1 : 0} zlit=${zeroIsLiteral ? 1 : 0}: out=${n} zeros=${zr} bits=${consumedBits}/${src.length * 8} head=[${head}] tail=[${tail4}]`,
        );
      }
    }
  }
}
