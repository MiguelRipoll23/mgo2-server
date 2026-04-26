import { injectable } from "@needle-di/core";

@injectable()
export class CryptoService {
  md5Hex(text: string): string {
    const msg = new TextEncoder().encode(text);
    const bytes = [...msg];
    const s = [7,12,17,22,7,12,17,22,7,12,17,22,7,12,17,22,5,9,14,20,5,9,14,20,5,9,14,20,5,9,14,20,4,11,16,23,4,11,16,23,4,11,16,23,4,11,16,23,6,10,15,21,6,10,15,21,6,10,15,21,6,10,15,21];
    const K = Array.from({ length: 64 }, (_, i) => Math.floor(Math.abs(Math.sin(i + 1)) * 2 ** 32) >>> 0);
    bytes.push(0x80);
    while (bytes.length % 64 !== 56) bytes.push(0);
    const bitLen = msg.length * 8;
    for (let i = 0; i < 8; i++) bytes.push((bitLen / 2 ** (i * 8)) & 0xff);
    let a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
    for (let i = 0; i < bytes.length; i += 64) {
      const M = Array.from({ length: 16 }, (_, j) => (bytes[i+j*4] | bytes[i+j*4+1]<<8 | bytes[i+j*4+2]<<16 | bytes[i+j*4+3]<<24) >>> 0);
      let [A, B, C, D] = [a0, b0, c0, d0];
      for (let j = 0; j < 64; j++) {
        let F: number, g: number;
        if (j < 16)      { F = (B & C) | (~B & D); g = j; }
        else if (j < 32) { F = (D & B) | (~D & C); g = (5*j+1)%16; }
        else if (j < 48) { F = B ^ C ^ D;           g = (3*j+5)%16; }
        else             { F = C ^ (B | ~D);         g = (7*j)%16; }
        F = (F + A + K[j] + M[g]) >>> 0;
        A = D; D = C; C = B;
        B = (B + ((F << s[j]) | (F >>> (32 - s[j])))) >>> 0;
      }
      a0=(a0+A)>>>0; b0=(b0+B)>>>0; c0=(c0+C)>>>0; d0=(d0+D)>>>0;
    }
    return [a0,b0,c0,d0].map((n) => n.toString(16).padStart(8,"0").match(/../g)!.reverse().join("")).join("");
  }
}
