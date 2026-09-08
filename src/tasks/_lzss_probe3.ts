// TEMP: round-trip the doc-§7 LZSS against the real captured compressed frame
// to discover the message layout preceding the bitstream.

const full = Uint8Array.from([
  0x80, 0xc4, 0x2b, 0x50, 0x08, 0x14, 0x00, 0x06, 0x88, 0x6c, 0x03, 0x4f,
  0x00, 0x8f, 0x51, 0x65, 0xf5, 0x79, 0xcd, 0x9e, 0xd1, 0x0e, 0x87, 0x40,
  0x01, 0xc1, 0x80, 0x42, 0xe2, 0xc0, 0xf0, 0xc1, 0xe1, 0xe0, 0x3c, 0x50,
  0xe1, 0x70, 0x08, 0xff, 0xff, 0xff, 0x0c, 0x0c, 0x42, 0x07, 0xff, 0x04,
  0x40, 0x3c, 0x10, 0x90, 0x03, 0x71, 0x8e, 0x06, 0x05, 0x1d, 0x0d, 0x82,
  0x01, 0x70, 0xb4, 0x5a, 0x6d, 0x96, 0x4b, 0xad, 0xb8, 0x52, 0x0b, 0xcc,
  0xca, 0x67, 0x00, 0x00, 0x00,
]);

// Doc-§7 LZSS: MSB-first bitstream; flag 0 = literal, flag 1 = back-ref
// (9-bit offset then 4-bit length-2); 512-byte ring window, mirrored at +0x11.
function lzssDecode(content: Uint8Array, start: number, out: Uint8Array): number {
  let bitPos = start * 8;
  const totalBits = content.length * 8;
  let outLen = 0;
  const win = new Uint8Array(0x200 + 0x20);
  let wi = 0;
  const readBits = (n: number): number => {
    let v = 0;
    for (let i = 0; i < n; i++) {
      if (bitPos >= totalBits) return -1;
      const byte = content[bitPos >> 3];
      v = (v << 1) | ((byte >> (7 - (bitPos & 7))) & 1);
      bitPos++;
    }
    return v;
  };
  while (bitPos < totalBits && outLen < out.length) {
    const flag = readBits(1);
    if (flag < 0) break;
    if (flag === 0) {
      const b = readBits(8);
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
        const b = win[(wi - off) & 0x1ff];
        out[outLen++] = b;
        win[wi & 0x1ff] = b;
        win[(wi & 0x1ff) + 0x11] = b;
        wi++;
      }
    }
  }
  return outLen;
}

// Compress per doc §7 (greedy longest match, len 2..17, off 1..511)
function lzssEncode(src: Uint8Array): Uint8Array {
  const bits: number[] = [];
  const win = new Uint8Array(0x200 + 0x20);
  let wi = 0;
  const pushBit = (b: number) => bits.push(b);
  for (let i = 0; i < src.length; ) {
    let bestLen = 0;
    let bestOff = 0;
    for (let off = 1; off <= Math.min(0x1ff, wi); off++) {
      let l = 0;
      while (l < 17 && i + l < src.length && win[(wi - off + l) & 0x1ff] === src[i + l]) l++;
      if (l > bestLen) {
        bestLen = l;
        bestOff = off;
        if (l === 17) break;
      }
    }
    if (bestLen >= 2) {
      pushBit(1);
      for (let k = 8; k >= 0; k--) pushBit((bestOff >> k) & 1);
      for (let k = 3; k >= 0; k--) pushBit(((bestLen - 2) >> k) & 1);
      for (let j = 0; j < bestLen; j++) {
        win[wi & 0x1ff] = src[i + j];
        win[(wi & 0x1ff) + 0x11] = src[i + j];
        wi++;
      }
      i += bestLen;
    } else {
      pushBit(0);
      for (let k = 7; k >= 0; k--) pushBit((src[i] >> k) & 1);
      win[wi & 0x1ff] = src[i];
      win[(wi & 0x1ff) + 0x11] = src[i];
      wi++;
      i++;
    }
  }
  const out = new Uint8Array(Math.ceil(bits.length / 8));
  bits.forEach((b, i) => {
    if (b) out[i >> 3] |= 0x80 >> (i & 7);
  });
  return out;
}

// score a decoded message: does it end with a plausible 4-byte tag like 00 00 0N 00?
function score(out: Uint8Array, n: number): string {
  const tail4 = [...out.subarray(Math.max(0, n - 4), n)].map((b) => b.toString(16).padStart(2, "0")).join(" ");
  const zeros = out.subarray(0, n).reduce((a, b) => a + (b === 0 ? 1 : 0), 0);
  return `outLen=${n} zeros=${zeros}/${n} tail4=[${tail4}] head=${[...out.subarray(0, 8)].map((b) => b.toString(16).padStart(2, "0")).join(" ")}`;
}

console.log("=== sweep: bitstream start, digest position ===");
for (const start of [0, 1, 2, 3, 4, 5, 6, 7, 8]) {
  for (const [digestPos, dn] of [["tail", 4], ["none", 0]] as const) {
    const end = digestPos === "tail" ? full.length - 4 : full.length;
    const content = full.subarray(start, end);
    const out = new Uint8Array(0x2000);
    const n = lzssDecode(content, 0, out);
    console.log(`start=${start} digest=${digestPos} ${score(out, n)}`);
  }
}

console.log("\n=== round-trip sanity: encode(decode(x)) structure ===");
// verify encode∘decode round-trips arbitrary data (validates my impl)
const sample = new Uint8Array(0x400);
const rnd = 123456789;
let s = rnd;
for (let i = 0; i < sample.length; i++) {
  s = (s * 1103515245 + 12345) & 0x7fffffff;
  sample[i] = (s >>> 16) & (i % 3 === 0 ? 0xff : 0x07); // semi-compressible
}
const enc = lzssEncode(sample);
const dec = new Uint8Array(0x2000);
const dn = lzssDecode(enc, 0, dec);
console.log(`round-trip: ${dn === sample.length && dec.subarray(0, dn).every((b, i) => b === sample[i]) ? "OK" : "FAIL"} (${sample.length} -> ${enc.length} -> ${dn})`);
