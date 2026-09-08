// TEMP: brute-force LZSS variants against the captured compressed payload
// (89-byte session-keyed frame, plain[2..len-10) = 77 bytes).
// Frame plain: 02 80 | 80 c4 2b 50 08 14 00 06 88 6c ... (hdr=0x8002 -> compression bit set)

const payload = Uint8Array.from([
  0x80, 0xc4, 0x2b, 0x50, 0x08, 0x14, 0x00, 0x06, 0x88, 0x6c, 0x03, 0x4f,
  0x00, 0x8f, 0x51, 0x65, 0xf5, 0x79, 0xcd, 0x9e, 0xd1, 0x0e, 0x87, 0x40,
  0x01, 0xc1, 0x80, 0x42, 0xe2, 0xc0, 0xf0, 0xc1, 0xe1, 0xe0, 0x3c, 0x50,
  0xe1, 0x70, 0x08, 0xff, 0xff, 0xff, 0x0c, 0x0c, 0x42, 0x07, 0xff, 0x04,
  0x40, 0x3c, 0x10, 0x90, 0x03, 0x71, 0x8e, 0x06, 0x05, 0x1d, 0x0d, 0x82,
  0x01, 0x70, 0xb4, 0x5a, 0x6d, 0x96, 0x4b, 0xad, 0xb8, 0x52, 0x0b, 0xcc,
  0xca, 0x67, 0x00, 0x00, 0x00,
]);

const hex = (b: Uint8Array) =>
  Array.from(b).map((x) => x.toString(16).padStart(2, "0")).join(" ");

/** Okumura-style LZSS: flag byte, bit=i -> literal, bit=0 -> ref. */
function okumura(
  src: Uint8Array,
  opts: { msb: boolean; window: number; minLen: number; lenBits: number; offBits: number; skip: number; prefill: number | null },
): Uint8Array | null {
  const { msb, window, minLen, lenBits, offBits, skip, prefill } = opts;
  const out: number[] = [];
  const ring = new Uint8Array(window);
  let ringPos = 0;
  if (prefill !== null) ring.fill(prefill);
  let pos = skip;
  let flag = 0, flagBits = 0;
  const getBit = (): number => {
    if (flagBits === 0) {
      if (pos >= src.length) return -1;
      flag = src[pos++];
      flagBits = 8;
    }
    let bit: number;
    if (msb) {
      bit = (flag >> 7) & 1;
      flag = (flag << 1) & 0xff;
    } else {
      bit = flag & 1;
      flag >>= 1;
    }
    flagBits--;
    return bit;
  };
  const getByte = (): number => (pos < src.length ? src[pos++] : -1);
  while (pos < src.length || flagBits > 0) {
    const bit = getBit();
    if (bit < 0) break;
    if (bit === 1) {
      const b = getByte();
      if (b < 0) break;
      out.push(b);
      ring[ringPos] = b;
      ringPos = (ringPos + 1) % window;
    } else {
      // reference: read (offBits+lenBits)/8 bytes (assume 8|16 split as 2 bytes)
      const b1 = getByte();
      const b2 = getByte();
      if (b1 < 0 || b2 < 0) break;
      // offset high bits in b1 low bits, length in b2? try common layouts:
      // layout A: off = ((b1 >> (8-offBitsHi)) ...) — use generic: combined 16-bit LE
      const combined = b1 | (b2 << 8);
      const len = (combined & ((1 << lenBits) - 1)) + minLen;
      const off = (combined >>> lenBits) & ((1 << offBits) - 1);
      if (len > 0x100 + minLen) return null;
      for (let i = 0; i < len; i++) {
        const idx = (ringPos - off - 1 + window * 2) % window;
        const b = ring[idx];
        out.push(b);
        ring[ringPos] = b;
        ringPos = (ringPos + 1) % window;
      }
    }
    if (out.length > 0x2000) return null;
  }
  return Uint8Array.from(out);
}

/** Bit-stream readers for non byte-aligned variants. */
function bitstreamVariants(): Array<{ name: string; out: Uint8Array | null }> {
  const results: Array<{ name: string; out: Uint8Array | null }> = [];

  // Generic: MSB-first bit reader over the whole payload; flag bit 1=literal(8 bits), 0=ref(off 11-12 bits + len 4 bits)
  function generic(opts: {
    msb: boolean; window: number; minLen: number; lenBits: number; offBits: number;
    lenFirst: boolean; skip: number; prefill: number | null; flagPerGroup: boolean;
  }): Uint8Array | null {
    const { msb, window, minLen, lenBits, offBits, lenFirst, skip, prefill, flagPerGroup } = opts;
    const out: number[] = [];
    const ring = new Uint8Array(window);
    let ringPos = 0;
    if (prefill !== null) ring.fill(prefill);
    let bitPos = skip * 8;
    const totalBits = payload.length * 8;
    const bit = (): number => {
      if (bitPos >= totalBits) return -1;
      const byte = payload[bitPos >> 3];
      const b = msb ? (byte >> (7 - (bitPos & 7))) & 1 : (byte >> (bitPos & 7)) & 1;
      bitPos++;
      return b;
    };
    const bits = (n: number): number => {
      let v = 0;
      for (let i = 0; i < n; i++) {
        const b = bit();
        if (b < 0) return -1;
        v = msb ? (v << 1) | b : v | (b << i);
      }
      return v;
    };
    while (bitPos < totalBits && out.length < 0x2000) {
      if (flagPerGroup) {
        // 8-use flag byte
        const f = bits(8);
        if (f < 0) break;
        for (let i = 0; i < 8; i++) {
          const fb = msb ? (f >> (7 - i)) & 1 : (f >> i) & 1;
          if (fb === 1) {
            const b = bits(8);
            if (b < 0) return null;
            out.push(b);
            ring[ringPos] = b;
            ringPos = (ringPos + 1) % window;
          } else {
            let off: number, len: number;
            if (lenFirst) {
              len = bits(lenBits); off = bits(offBits);
            } else {
              off = bits(offBits); len = bits(lenBits);
            }
            if (off < 0 || len < 0) return null;
            len += minLen;
            if (off === 0 && out.length === 0) return null;
            for (let j = 0; j < len; j++) {
              const idx = (ringPos - off - 1 + window * 2) % window;
              const b = ring[idx];
              out.push(b);
              ring[ringPos] = b;
              ringPos = (ringPos + 1) % window;
            }
          }
        }
      } else {
        const fb = bit();
        if (fb < 0) break;
        if (fb === 1) {
          const b = bits(8);
          if (b < 0) return null;
          out.push(b);
          ring[ringPos] = b;
          ringPos = (ringPos + 1) % window;
        } else {
          let off: number, len: number;
          if (lenFirst) { len = bits(lenBits); off = bits(offBits); }
          else { off = bits(offBits); len = bits(lenBits); }
          if (off < 0 || len < 0) return null;
          len += minLen;
          if (off === 0 && out.length === 0) return null;
          for (let j = 0; j < len; j++) {
            const idx = (ringPos - off - 1 + window * 2) % window;
            const b = ring[idx];
            out.push(b);
            ring[ringPos] = b;
            ringPos = (ringPos + 1) % window;
          }
        }
      }
    }
    return Uint8Array.from(out);
  }

  for (const msb of [true, false]) {
    for (const window of [0x800, 0x1000, 0x2000]) {
      for (const minLen of [2, 3]) {
        for (const lenBits of [4, 5, 6, 8]) {
          for (const offBits of [11, 12, 13, 16]) {
            for (const lenFirst of [true, false]) {
              for (const skip of [0, 1, 2]) {
                for (const prefill of [null, 0, 0x20]) {
                  for (const flagPerGroup of [false, true]) {
                    const out = generic({ msb, window, minLen, lenBits, offBits, lenFirst, skip, prefill, flagPerGroup });
                    if (out && out.length >= 8) {
                      results.push({
                        name: `gen msb=${msb} win=0x${window.toString(16)} min=${minLen} lenB=${lenBits} offB=${offBits} lenFirst=${lenFirst} skip=${skip} pre=${prefill} fpg=${flagPerGroup}`,
                        out,
                      });
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
  }
  return results;
}

const results = bitstreamVariants();
console.log(`variants producing >=8 bytes: ${results.length}`);
// Score: printable ASCII ratio + presence of 00 10 prefix (tag/msgLen layout)
for (const r of results.slice(0, 40)) {
  const printable = r.out.filter((b) => (b >= 0x20 && b < 0x7f) || b === 0).length / r.out.length;
  const has0010 = r.out[0] === 0x00 && r.out[1] === 0x10;
  console.log(`${printable.toFixed(2)} ${has0010 ? "TAG " : "    "} len=${r.out.length} ${r.name}`);
  if (has0010 || printable > 0.85) console.log(`   -> ${hex(r.out.slice(0, 48))}`);
}
console.log(`\npayload (${payload.length}B): ${hex(payload)}`);
