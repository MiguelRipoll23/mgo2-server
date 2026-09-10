// Wire cipher of the p2p channel: LCG-position header scramble + LE32 XOR
// chain with plaintext feedback + self-verifying tail digest.
// Decoder FUN_002666c8 / sender FUN_00268ba8 in MGO2.ELF; verified
// byte-for-byte against 10 live captures (docs/udp-p2p-protocol.md §5).

import { UDP_HEADER_SIZE, UDP_TAIL_SIZE } from "../constants/udp-commands-constants.ts";
import {
  UDP_LCG_MULTIPLIER,
  UDP_TAIL_DIGEST_KEY,
} from "../constants/udp-crypto-keys-constants.ts";
import { concatBytes, u16LE } from "./binary-util.ts";
import { md5 } from "./md5-util.ts";

const lcg = (x: number): number => (Math.imul(x, UDP_LCG_MULTIPLIER) + 1) >>> 0;

/** Scramble positions derived from the datagram length (decoder FUN_002666c8). */
function scramblePositions(n: number): [number, number, number, number] {
  const x0 = lcg(n >>> 0);
  const x1 = lcg(x0);
  const x2 = lcg(x1);
  const x3 = lcg(x2);
  const p0 = (((x0 >>> 16) % (n - 2)) + 2) >>> 0;
  const p1 = (((x1 >>> 16) % (n - 2)) + 2) >>> 0;
  const q0 = ((x2 >>> 16) % n) >>> 0;
  const q1 = ((x3 >>> 16) % n) >>> 0;
  return [p0, p1, q0, q1];
}

const chainSeed = (counter: number, key: number): number =>
  ((((counter & 0x7fff) * UDP_LCG_MULTIPLIER + 1) >>> 0) ^ (key >>> 0)) >>> 0;

/**
 * XOR chain over LE32 words starting at byte 2, covering len − 0xc bytes,
 * with plaintext feedback: seed_{i+1} = seed_i + word_i.
 * Decrypt: `word` is the wire word, feedback is the decrypted word.
 * Encrypt: `word` is the plaintext word, feedback is the plaintext word.
 */
function xorChain(b: Uint8Array, counter: number, key: number, encrypt: boolean): void {
  let seed = chainSeed(counter, key);
  let prev = 0;
  let pos = UDP_HEADER_SIZE;
  let remaining = b.length - UDP_HEADER_SIZE - UDP_TAIL_SIZE;
  while (remaining > 0) {
    seed = (seed + prev) >>> 0;
    const take = Math.min(4, remaining);
    let w = 0;
    for (let i = 0; i < take; i++) w |= b[pos + i] << (8 * i);
    const out = (w ^ seed) >>> 0;
    for (let i = 0; i < take; i++) b[pos + i] = (out >>> (8 * i)) & 0xff;
    prev = encrypt ? w : out;
    pos += take;
    remaining -= take;
  }
}

/**
 * Compute the self-verifying tail digest in place (decoder FUN_002666c8,
 * pre-handshake path 0x266ff4 / keyed path 0x266948). Must run AFTER the XOR
 * chain and BEFORE the header scramble — the receiver checks it on the
 * header-unscrambled, chain-ENCRYPTED datagram (the digest gate is pre-chain).
 */
function computeTailDigest(decoded: Uint8Array, digestKey: number): void {
  const len = decoded.length;
  const key4 = new Uint8Array(4);
  new DataView(key4.buffer).setUint32(0, digestKey, true);
  const d1 = md5(concatBytes(key4, decoded.subarray(0, len - UDP_TAIL_SIZE)));
  const d2 = md5(concatBytes(key4, d1));
  for (let i = 0; i < UDP_TAIL_SIZE; i++) {
    decoded[len - UDP_TAIL_SIZE + i] = d2[i] ^ (i < 6 ? d2[UDP_TAIL_SIZE + i] : 0);
  }
}

/**
 * Verify the tail digest on a header-unscrambled, PRE-chain datagram (the
 * receiver's gate runs before the XOR chain). Pre-keyed frames verify with
 * the bare constant; session-keyed frames with (sessionKey ^ constant) (§5.3).
 */
export function verifyTailDigest(unscrambled: Uint8Array, digestKey: number): boolean {
  const len = unscrambled.length;
  if (len < UDP_TAIL_SIZE + 1) return false;
  const tail = unscrambled.subarray(len - UDP_TAIL_SIZE);
  const key4 = new Uint8Array(4);
  new DataView(key4.buffer).setUint32(0, digestKey, true);
  const d1 = md5(concatBytes(key4, unscrambled.subarray(0, len - UDP_TAIL_SIZE)));
  const d2 = md5(concatBytes(key4, d1));
  for (let i = 0; i < UDP_TAIL_SIZE; i++) {
    if (tail[i] !== (d2[i] ^ (i < 6 ? d2[UDP_TAIL_SIZE + i] : 0))) return false;
  }
  return true;
}

/** Undo the header scramble only (returns the hdr counter) — no chain pass. */
export function unscrambleHeaderOnly(raw: Uint8Array): number {
  const n = raw.length;
  const [p0, p1, q0, q1] = scramblePositions(n);
  // header unscramble (receiver path: swap, swap, xor, xor)
  const t1 = raw[1];
  raw[1] = raw[q1];
  raw[q1] = t1;
  const t0 = raw[0];
  raw[0] = raw[q0];
  raw[q0] = t0;
  raw[1] ^= raw[p1];
  raw[0] ^= raw[p0];
  return u16LE(raw, 0);
}

/** Remove the XOR chain in place on a header-unscrambled datagram (decrypt). */
export function removeChainInPlace(buf: Uint8Array, counter: number, key: number): void {
  xorChain(buf, counter, key, false);
}

/**
 * Decode a wire datagram in place with a single key: unscramble the header,
 * verify the tail digest, remove the chain. Returns the hdr counter, or null
 * when the digest gate fails (the game silently drops such datagrams).
 */
export function decodeFrameInPlace(
  wire: Uint8Array,
  key: number,
  digestKey: number = UDP_TAIL_DIGEST_KEY,
): number | null {
  const counter = unscrambleHeaderOnly(wire);
  if (!verifyTailDigest(wire, digestKey)) return null;
  xorChain(wire, counter, key, false);
  return counter;
}

/**
 * Encode a plaintext datagram to wire bytes (inverse of decodeFrameInPlace):
 * XOR chain, then the tail digest, then the header scramble. The plaintext
 * MUST include the 10-byte tail region (zeroed) — chain and digest both run
 * over the full frame length.
 */
export function encodeFrame(
  plain: Uint8Array,
  counter: number,
  key: number,
  digestKey: number = UDP_TAIL_DIGEST_KEY,
): Uint8Array {
  const out = plain.slice();
  xorChain(out, counter, key, true);
  computeTailDigest(out, digestKey);
  const n = out.length;
  const [p0, p1, q0, q1] = scramblePositions(n);
  // header scramble (sender is the exact inverse of the receiver)
  out[0] ^= out[p0];
  out[1] ^= out[p1];
  const t0 = out[0];
  out[0] = out[q0];
  out[q0] = t0;
  const t1 = out[1];
  out[1] = out[q1];
  out[q1] = t1;
  return out;
}
