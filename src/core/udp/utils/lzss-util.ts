// LZSS decompressor — instruction-exact port of MGO2.ELF FUN_00efd840,
// verified byte-for-byte on the capture's profile record (account name
// decodes intact). MSB-first bitstream; flag 1 = literal (8 bits), flag 0 =
// back-reference (9-bit absolute ring offset + 4-bit length field,
// len = field + 2); ring write index starts at 1; offset 0 = EOF marker.
// docs/udp-p2p-protocol.md §7.

import { UDP_LZSS_MAX_OUTPUT, UDP_LZSS_RING_SIZE } from "../constants/udp-commands-constants.ts";

export function lzssDecompress(src: Uint8Array): Uint8Array | null {
  const maxout = UDP_LZSS_MAX_OUTPUT;
  const out = new Uint8Array(maxout);
  const ring = new Uint8Array(UDP_LZSS_RING_SIZE);
  let w = 1;
  let n = 0;
  let bitpos = 0;
  const total = src.length * 8;

  const bit = (): number => {
    if (bitpos >= total) return -1;
    const v = (src[bitpos >> 3] >> (7 - (bitpos & 7))) & 1;
    bitpos++;
    return v;
  };
  const bits = (k: number): number => {
    let v = 0;
    for (let i = 0; i < k; i++) {
      const b = bit();
      if (b < 0) return -1;
      v = (v << 1) | b;
    }
    return v;
  };

  while (bitpos < total && n < maxout) {
    const flag = bit();
    if (flag < 0) break;
    if (flag === 1) {
      const b = bits(8);
      if (b < 0) return null;
      out[n++] = b;
      ring[w++ & 0x1ff] = b;
    } else {
      const off = bits(9);
      const lf = bits(4);
      if (off < 0 || lf < 0) return null;
      if (off === 0) break; // EOF marker
      for (let j = 0; j < lf + 2 && n < maxout; j++) {
        const b = ring[(off + j) & 0x1ff];
        out[n++] = b;
        ring[w++ & 0x1ff] = b;
      }
    }
  }
  return out.slice(0, n);
}
