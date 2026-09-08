// Fake MGO2 p2p host + packet dump.
//
// Listens on the port the NPC host's character_connections row advertises
// (the joiner dials exactly that) and acts as a fake host:
//   1. Decrypts the joiner's first datagram (the p2p handshake — scrambled
//      header + XOR chain on the wire, NOT plaintext as the old docs claimed)
//   2. Logs the handshake (in)
//   3. Replies with our own handshake in the same wire format (out)
//
// The crypto was reversed from MGO2.ELF's p2p module (module.bin):
//   - decoder  FUN_002666c8 — header unscramble (LCG positions from datagram
//     length, 0x5d588b65) + LE32 XOR chain with plaintext feedback
//   - handshake sender FUN_00268ba8, receiver FUN_00267d58
//   - pre-handshake key: ((hdr & 0x7fff) * 0x5d588b65 + 1) ^ 0x87103c2f
//   - post-handshake key: ((hdr & 0x7fff) * 0x5d588b65 + 1) ^ (peerBase ^ ownBase)
// Every transformation below was verified byte-for-byte against 10 live
// captures from the game client (decode -> re-encode == wire bytes).
//
// Decoded handshake layout (44-byte datagram):
//   [0..2)   hdr counter (LE u16)          — seeds the XOR chain
//   [2..6)   constant prefix 00 10 1c 00
//   [6..10)  peerId (LE u32)
//   [10..14) counterBase (LE u32)
//   [14..18) module magic (LE u32) 0x4d258ab7 — validated by the receiver
//   [18]     flags (bit 2 must be clear)
//   [19..21) unk (LE u16)
//   [21]     count (u8, <= 2)
//   [22..28) pair 0 { ip[4], port LE u16 }
//   [28..34) pair 1 { ip[4], port LE u16 }
//   [34..44) tail — NOT free: a self-verifying MD5 digest. The decoder
//   (FUN_002666c8, pre-handshake path 0x266ff4) computes
//     d1 = MD5(0x2b58de69 LE || datagram[0..len-10))
//     d2 = MD5(0x2b58de69 LE || d1)
//     tail[i] = d2[i] ^ (i < 6 ? d2[10+i] : 0)
//   and drops the datagram on mismatch, BEFORE any field validation. Our
//   replies were zeroing it, so the joiner silently dropped every reply and
//   kept re-dialing. All 10 captured dials verify against this formula.
//
// 11181 must NEVER be in UDP_PORTS: it is the port the joining game client
// binds for its own p2p socket (0x2bad), and while this dump holds it the
// game's session init fails its bind and never sends a single datagram.
// The joiner's own p2p socket is also on 5730 (the dials' source port), so
// the default listener is 5731 — never 5730, or both sockets fight for it.

const LCG_MULT = 0x5d588b65;
const PRE_HANDSHAKE_XOR = 0x87103c2f;
const MODULE_MAGIC = 0x4d258ab7;
const HANDSHAKE_PREFIX = 0x001c1000; // constant 4-byte field at decoded [2..6)
const TAIL_DIGEST_KEY = 0x2b58de69; // tail MD5 key (decoder 0x266ff4)

// ---------------------------------------------------------------------------
// MD5 (RFC 1321) — pure TS, Deno's WebCrypto doesn't support MD5.
// ---------------------------------------------------------------------------

const MD5_S = [
  7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
  5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
  4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
  6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
];
const MD5_K = new Uint32Array(64);
for (let i = 0; i < 64; i++) {
  MD5_K[i] = Math.floor(Math.abs(Math.sin(i + 1)) * 0x100000000) >>> 0;
}

function md5(input: Uint8Array): Uint8Array {
  const bitLen = input.length * 8;
  const paddedLen = (((input.length + 8) >> 6) + 1) << 6;
  const buf = new Uint8Array(paddedLen);
  buf.set(input);
  buf[input.length] = 0x80;
  const dv = new DataView(buf.buffer);
  dv.setUint32(paddedLen - 8, bitLen >>> 0, true);
  dv.setUint32(paddedLen - 4, Math.floor(bitLen / 0x100000000), true);

  let a0 = 0x67452301;
  let b0 = 0xefcdab89;
  let c0 = 0x98badcfe;
  let d0 = 0x10325476;

  for (let off = 0; off < paddedLen; off += 64) {
    const m = new Uint32Array(16);
    for (let i = 0; i < 16; i++) m[i] = dv.getUint32(off + i * 4, true);
    let a = a0, b = b0, c = c0, d = d0;
    for (let i = 0; i < 64; i++) {
      let f: number;
      let g: number;
      if (i < 16) {
        f = (b & c) | (~b & d);
        g = i;
      } else if (i < 32) {
        f = (d & b) | (~d & c);
        g = (5 * i + 1) % 16;
      } else if (i < 48) {
        f = b ^ c ^ d;
        g = (3 * i + 5) % 16;
      } else {
        f = c ^ (b | ~d);
        g = (7 * i) % 16;
      }
      f = (f + a + MD5_K[i] + m[g]) >>> 0;
      a = d;
      d = c;
      c = b;
      b = (b + ((f << MD5_S[i]) | (f >>> (32 - MD5_S[i])))) >>> 0;
    }
    a0 = (a0 + a) >>> 0;
    b0 = (b0 + b) >>> 0;
    c0 = (c0 + c) >>> 0;
    d0 = (d0 + d) >>> 0;
  }

  const out = new Uint8Array(16);
  const odv = new DataView(out.buffer);
  odv.setUint32(0, a0, true);
  odv.setUint32(4, b0, true);
  odv.setUint32(8, c0, true);
  odv.setUint32(12, d0, true);
  return out;
}

/**
 * Compute the self-verifying tail digest in place (decoder FUN_002666c8,
 * pre-handshake path 0x266ff4 / keyed path 0x266948). Must run AFTER the XOR
 * chain and BEFORE the header scramble — the receiver computes it on the
 * header-unscrambled, chain-ENCRYPTED datagram (digest gate is pre-chain).
 *
 * key: state-DEPENDENT (UDP_P2P.md §5.3/§6). Frames sent while the session is
 * below state 8 (handshakes, the joiner's state-6 keep-alives) use the
 * constant TAIL_DIGEST_KEY. True session-keyed (state ≥ 8) frames use
 * K ^ TAIL_DIGEST_KEY where K = peer_base ^ own_base — the accept store at
 * 0x26908c/0x269094 writes session+8 = base, +0xc = base ^ const, and the
 * keyed digest is session[0xc] ^ session[0x10].
 */
function computeTailDigest(decoded: Uint8Array, key: number): void {
  const len = decoded.length;
  const key4 = new Uint8Array(4);
  new DataView(key4.buffer).setUint32(0, key, true);
  const d1 = md5(concatBytes(key4, decoded.subarray(0, len - 10)));
  const d2 = md5(concatBytes(key4, d1));
  for (let i = 0; i < 10; i++) {
    decoded[len - 10 + i] = d2[i] ^ (i < 6 ? d2[10 + i] : 0);
  }
}

function concatBytes(a: Uint8Array, b: Uint8Array): Uint8Array {
  const out = new Uint8Array(a.length + b.length);
  out.set(a);
  out.set(b, a.length);
  return out;
}

/**
 * Verify the tail digest on a header-unscrambled, PRE-chain datagram (the
 * receiver's gate runs before the XOR chain). Key = the constant for pre-keyed
 * (state < 8) frames; K ^ const for true session-keyed frames.
 */
function verifyTailDigest(unscrambled: Uint8Array, key: number): boolean {
  const len = unscrambled.length;
  if (len < 11) return false;
  const tail = unscrambled.subarray(len - 10);
  const k4 = new Uint8Array(4);
  new DataView(k4.buffer).setUint32(0, key, true);
  const d1 = md5(concatBytes(k4, unscrambled.subarray(0, len - 10)));
  const d2 = md5(concatBytes(k4, d1));
  for (let i = 0; i < 10; i++) {
    if (tail[i] !== (d2[i] ^ (i < 6 ? d2[10 + i] : 0))) return false;
  }
  return true;
}

const ports = (Deno.env.get("UDP_PORTS") ?? "5731")
  .split(",")
  .map((part) => Number(part.trim()))
  .filter((port) => Number.isInteger(port) && port > 0);
const hostname = Deno.env.get("UDP_HOSTNAME") ?? "0.0.0.0";

// Our fake-host identity: peerId + counterBase go into the reply handshake,
// and the post-handshake key is peerBase ^ ourBase.
//
// peer_id on the wire is the SENDER's own id (the joiner's handshake carries
// its own character id, e.g. 2). The joiner's drain gate compares the reply's
// peer_id against session[0x18] — the 5-word peer descriptor copied into the
// dial session at creation (decoder 0x26864c) — which for a dial session is
// the HOST's character id (1 = the seeded NPC). A mismatch is silently
// ignored (state stays 5, the joiner keeps re-dialing every ~1.9 s), while a
// wrong magic would move the session on — so the reply must carry the HOST's
// id, NOT an echo of the joiner's. P2P_ID overrides (set it to the host
// character's id if it isn't 1); when unset we default to 1.
const p2pIdEnv = Deno.env.get("P2P_ID");
const ourPeerId = Number(p2pIdEnv ?? "1") >>> 0;
const ourBase = Number(Deno.env.get("P2P_BASE") ?? "0x12345678") >>> 0;

// NAT workaround (default ON): the joiner's handshake advertises its public
// endpoint first (pair0) — the router's WAN IP, unreachable in a LAN test —
// and its private endpoint second (pair1). Anything that dials the joiner
// back would target pair0 and fail, so we overwrite pair0 with pair1.
// Disable with P2P_OVERRIDE_PUBLIC=0.
const overridePublic = !["0", "false"].includes(
  (Deno.env.get("P2P_OVERRIDE_PUBLIC") ?? "1").toLowerCase(),
);

// Post-handshake continuation. KEY MECHANISM (decoder FUN_002666c8 decompile):
// the joiner's dial session sits in state 6 after accepting our reply, and
// EVERYTHING it sends (its 16-byte keep-alives, tag 0x5000) is pre-keyed —
// the session key is NOT established yet (flags bit 0x8 clear). The ONLY code
// path that establishes it: a frame arriving in the handshake phase whose tail
// digest verifies with K ^ const (K = peer_base ^ own_base) flips flags |= 9,
// and the accept pump then sets state 8 when (flags & 3) == 3. So the fake
// host must send ONE session-keyed frame (we use an empty tag-0x5000
// keep-alive — the same shape the joiner sends) to move the joiner into the
// data phase. Pre-keyed frames alone can never do it.
// ALL outbound frames share ONE per-peer hdr counter: the joiner's seq window
// (session[0x42] + reorder bitmask) drops anything outside last+1..last+0x20,
// so separate reply/data counters interleave and get discarded.
//   P2P_SEND_KEYED_FRAME = keepalive (default) | data | keyed | never
//     keepalive — after each reply send the SESSION-keyed empty keep-alive
//               (key establishment) and mirror the joiner's pre-keyed pings
//     data     — send a PRE-keyed data frame (tag 0x1000) after each reply
//                (diagnostic only — cannot establish the key)
//     keyed    — send a SESSION-keyed data frame after each reply (establishes
//               the key too, but the message type is a guess)
//     never    — handshake replies only
//   P2P_FRAME_TYPE / P2P_FRAME_BODY = the data frame's message type (u16, no
//     class/flags bits) and hex body — content is a diagnostic guess; the
//     decoder ignores message types it doesn't handle.
const sendKeyedMode = (Deno.env.get("P2P_SEND_KEYED_FRAME") ?? "keepalive").toLowerCase();
// Disable the session-keyed key-establishment keep-alive with P2P_ESTABLISH=0.
const establish = !["0", "false"].includes(
  (Deno.env.get("P2P_ESTABLISH") ?? "1").toLowerCase(),
);
const frameType = Number(Deno.env.get("P2P_FRAME_TYPE") ?? "0x0001") >>> 0;
const frameBodyHex = (Deno.env.get("P2P_FRAME_BODY") ?? "").replace(/[^0-9a-fA-F]/g, "");
const frameBody = new Uint8Array(frameBodyHex.length / 2)
  .map((_, i) => parseInt(frameBodyHex.slice(i * 2, i * 2 + 2), 16));

if (ports.includes(11181)) {
  console.warn(
    "[udp] WARNING: 11181 is in UDP_PORTS — the game client binds that port for its own p2p socket, so the join will never dial. Remove it.",
  );
}

const sockets = ports.map((port) => ({
  port,
  socket: Deno.listenDatagram({ port, hostname, transport: "udp" }),
}));

for (const { port } of sockets) {
  console.log(`[udp] listening on ${hostname}:${port}`);
}
console.log(`[udp] continuation=${sendKeyedMode} our base=0x${ourBase.toString(16)}`);

// ---------------------------------------------------------------------------
// Wire crypto (verified against 10 live captures)
// ---------------------------------------------------------------------------

const lcg = (x: number): number => (Math.imul(x, LCG_MULT) + 1) >>> 0;

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

const chainSeed = (hdr: number, keyed: number): number =>
  ((((hdr & 0x7fff) * LCG_MULT + 1) >>> 0) ^ (keyed >>> 0)) >>> 0;

/**
 * XOR chain over LE32 words starting at byte 2, covering len - 0xc bytes,
 * with plaintext feedback: seed_{i+1} = seed_i + word_i.
 * decrypt: word is the wire word, feedback is the decrypted word.
 * encrypt: word is the plaintext word, feedback is the plaintext word.
 */
function xorChain(
  b: Uint8Array,
  hdr: number,
  keyed: number,
  encrypt: boolean,
): void {
  let seed = chainSeed(hdr, keyed);
  let prev = 0;
  let pos = 2;
  let remaining = b.length - 0xc;
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

/** Undo the header scramble only (returns the hdr counter) — no chain pass.
 * The digest gate runs on exactly this pre-chain state (§5.3). */
function unscrambleHeaderOnly(raw: Uint8Array): number {
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
  return (raw[0] | (raw[1] << 8)) >>> 0;
}

/**
 * Encode a plaintext datagram -> wire bytes (inverse of decodeFrame).
 * digestKey defaults to the constant TAIL_DIGEST_KEY — correct for both the
 * handshake and keyed frames (verified against live joiner keyed pings).
 */
function encodeFrame(
  plain: Uint8Array,
  hdr: number,
  keyed: number,
  digestKey: number = TAIL_DIGEST_KEY,
): Uint8Array {
  const out = plain.slice();
  xorChain(out, hdr, keyed, true);
  // tail digest: computed after the chain, before the scramble (decoder order)
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

// ---------------------------------------------------------------------------
// Handshake parse / build
// ---------------------------------------------------------------------------

interface Handshake {
  peerId: number;
  counterBase: number;
  magic: number;
  flags: number;
  unk: number;
  count: number;
  pairs: Array<{ ip: string; port: number }>;
  tail: Uint8Array;
}

function u32LE(b: Uint8Array, off: number): number {
  return (b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24)) >>> 0;
}

function u16LE(b: Uint8Array, off: number): number {
  return (b[off] | (b[off + 1] << 8)) >>> 0;
}

function ipToBytes(ip: string): Uint8Array | null {
  const parts = ip.split(".").map((p) => Number(p));
  if (parts.length !== 4 || parts.some((p) => !Number.isInteger(p) || p < 0 || p > 255)) {
    return null;
  }
  return new Uint8Array(parts);
}

/** Parse a decoded 44-byte handshake. Returns null if it isn't one. */
function parseHandshake(decoded: Uint8Array): Handshake | null {
  if (decoded.length < 34) return null;
  if (u32LE(decoded, 14) !== MODULE_MAGIC) return null;
  const count = decoded[21];
  if (count > 2 || decoded[18] & 0x04) return null;
  const pairs: Array<{ ip: string; port: number }> = [];
  for (let i = 0; i < count; i++) {
    const off = 22 + i * 6;
    pairs.push({
      ip: `${decoded[off]}.${decoded[off + 1]}.${decoded[off + 2]}.${decoded[off + 3]}`,
      port: u16LE(decoded, off + 4),
    });
  }
  return {
    peerId: u32LE(decoded, 6),
    counterBase: u32LE(decoded, 10),
    magic: MODULE_MAGIC,
    flags: decoded[18],
    unk: u16LE(decoded, 19),
    count,
    pairs,
    tail: decoded.slice(34),
  };
}

/**
 * Build a plaintext post-handshake data frame (UDP_P2P.md §3/§5): hdr u16 LE,
 * prefix 00 10 + msgLen u16 LE, then one message {type u16 BE, len u8, body};
 * the 10-byte tail digest is computed by encodeFrame. The message header is
 * the frame builder's (FUN_00269860) — type bits 0x1000/0x2000/0x4000/0x8000
 * are class/flags and are NOT set here, so a 1-byte length follows the type.
 */
function buildDataFrame(hdr: number, msgType: number, body: Uint8Array): Uint8Array {
  const msgLen = 3 + body.length;
  const out = new Uint8Array(16 + msgLen);
  out[0] = hdr & 0xff;
  out[1] = (hdr >> 8) & 0xff;
  out[2] = 0x00; // 0x1000 tag (constant, not validated)
  out[3] = 0x10;
  out[4] = msgLen & 0xff; // LE u16 message length
  out[5] = (msgLen >> 8) & 0xff;
  out[6] = (msgType >> 8) & 0xff; // message type BE
  out[7] = msgType & 0xff;
  out[8] = body.length & 0xff; // 1-byte length
  out.set(body, 9);
  return out;
}

/**
 * Build a plaintext empty keep-alive frame — the exact frame the joiner's
 * state-6 dial session emits after accepting our reply (verified live):
 * hdr u16 LE, tag 0x5000, msgLen 0; the 10-byte tail digest is added by
 * encodeFrame. Pre-keyed (state < 8 crypto), like everything else in the
 * handshake phase.
 */
function buildKeepaliveFrame(hdr: number): Uint8Array {
  const out = new Uint8Array(16);
  out[0] = hdr & 0xff;
  out[1] = (hdr >> 8) & 0xff;
  out[2] = 0x00; // tag 0x5000
  out[3] = 0x50;
  out[4] = 0x00; // msgLen 0
  out[5] = 0x00;
  return out;
}

/**
 * Build a plaintext 44-byte host handshake. Advertised endpoint = the LAN IP
 * the joiner reached us from (overridable via P2P_HOST) on our listening port.
 * peerId = the value to stamp: the HOST's character id (default 1, P2P_ID
 * override) — the joiner's drain gate checks the reply's sender id against
 * the peer descriptor stored in its dial session (see the const block above).
 */
function buildHandshake(hdr: number, peerIp: string, port: number, peerId: number): Uint8Array {
  const out = new Uint8Array(44);
  out[0] = hdr & 0xff;
  out[1] = (hdr >> 8) & 0xff;
  // constant prefix at [2..6)
  out[2] = (HANDSHAKE_PREFIX >> 0) & 0xff;
  out[3] = (HANDSHAKE_PREFIX >> 8) & 0xff;
  out[4] = (HANDSHAKE_PREFIX >> 16) & 0xff;
  out[5] = (HANDSHAKE_PREFIX >> 24) & 0xff;
  // peerId [6..10) — the HOST's character id (joiner gates on it, NOT an echo)
  out[6] = peerId & 0xff;
  out[7] = (peerId >> 8) & 0xff;
  out[8] = (peerId >> 16) & 0xff;
  out[9] = (peerId >> 24) & 0xff;
  // counterBase [10..14)
  out[10] = ourBase & 0xff;
  out[11] = (ourBase >> 8) & 0xff;
  out[12] = (ourBase >> 16) & 0xff;
  out[13] = (ourBase >> 24) & 0xff;
  // magic [14..18)
  out[14] = MODULE_MAGIC & 0xff;
  out[15] = (MODULE_MAGIC >> 8) & 0xff;
  out[16] = (MODULE_MAGIC >> 16) & 0xff;
  out[17] = (MODULE_MAGIC >> 24) & 0xff;
  // flags / unk / count
  out[18] = 0x02;
  out[19] = 0x01;
  out[20] = 0x00;
  out[21] = 0x02;
  // pairs: advertise our endpoint (public + private, same here)
  const ip = ipToBytes(peerIp) ?? new Uint8Array([127, 0, 0, 1]);
  out.set(ip, 22);
  out[26] = port & 0xff;
  out[27] = (port >> 8) & 0xff;
  out.set(ip, 28);
  out[32] = port & 0xff;
  out[33] = (port >> 8) & 0xff;
  // [34..44) tail: computed by encodeFrame (self-verifying MD5 digest)
  return out;
}

// ---------------------------------------------------------------------------
// Main loop
// ---------------------------------------------------------------------------

interface Peer {
  counterBase: number;
  outHdr: number; // SHARED outbound counter for this peer (replies, acks, data)
  lastReplyAt: number; // ms timestamp of our last reply
  handshakesSeen: number;
  accepted: boolean; // joiner's session signaled us (keep-alive / keyed frame seen)
  keyedPings: number; // post-handshake frames received from the joiner
  dialBack: { hostname: string; port: number }; // where to reach the joiner (post-override)
}

const peers = new Map<string, Peer>();

const hexOf = (bytes: Uint8Array): string =>
  Array.from(bytes).map((b) => b.toString(16).padStart(2, "0")).join(" ");

const decOf = (bytes: Uint8Array): string => Array.from(bytes).join(", ");

function logHandshake(tag: string, hs: Handshake): void {
  console.log(`${tag} peerId=0x${hs.peerId.toString(16).padStart(8, "0")} base=0x${
    hs.counterBase.toString(16).padStart(8, "0")
  } magic=0x${hs.magic.toString(16).padStart(8, "0")} flags=0x${
    hs.flags.toString(16).padStart(2, "0")
  } unk=0x${hs.unk.toString(16).padStart(4, "0")} count=${hs.count}`);
  for (let i = 0; i < hs.pairs.length; i++) {
    const p = hs.pairs[i];
    console.log(`${tag}   pair${i}: ${p.ip}:${p.port}`);
  }
}

while (sockets.length > 0) {
  const result = await Promise.race(
    sockets.map(async ({ port, socket }) => ({
      port,
      socket,
      data: (await socket.receive()) as [Uint8Array, Deno.NetAddr],
    })),
  );

  const [raw, remote] = result.data;
  const sender = remote.transport === "udp"
    ? `${remote.hostname}:${remote.port}`
    : "unix";
  const tag = `[udp:${result.port}]`;

  console.log(`${tag} IN ${sender} (${raw.length} bytes)`);
  console.log(`${tag}   hex: ${hexOf(raw)}`);
  console.log(`${tag}   dec: [${decOf(raw)}]`);

  // Classify the datagram by its digest key (the gate runs pre-chain, §5.3):
  // pre-keyed (state < 8) frames verify with the bare constant; true
  // session-keyed frames verify with K ^ const. The joiner's dial session
  // sends ONLY pre-keyed frames until it reaches state 8 — verified live via
  // its 16-byte keep-alives (tag 0x5000), whose wire bytes are identical
  // across runs with different session keys.
  const peerKey = `${remote.hostname}:${remote.port}`;
  const knownPeer = peers.get(peerKey);
  const work = raw.slice();
  const hdr = unscrambleHeaderOnly(work);
  const preVerified = verifyTailDigest(work, TAIL_DIGEST_KEY);
  let sessionKey = 0;
  let keyedVerified = false;
  if (!preVerified && knownPeer) {
    sessionKey = (knownPeer.counterBase ^ ourBase) >>> 0;
    keyedVerified = verifyTailDigest(work, (sessionKey ^ TAIL_DIGEST_KEY) >>> 0);
  }

  const decoded = work.slice();
  xorChain(decoded, hdr, PRE_HANDSHAKE_XOR, false);
  const hs = preVerified ? parseHandshake(decoded) : null;

  if (!hs) {
    if (preVerified) {
      // Pre-keyed non-handshake frame — e.g. the joiner's state-6 keep-alive.
      if (knownPeer) {
        knownPeer.accepted = true;
        knownPeer.keyedPings++;
      }
      const frameTag = decoded.length >= 6 ? u16LE(decoded, 2) : -1;
      const frameMsgLen = decoded.length >= 6 ? u16LE(decoded, 4) : -1;
      console.log(
        `${tag} PRE-KEYED frame hdr=0x${hdr.toString(16).padStart(4, "0")} tag=0x${
          (frameTag & 0xffff).toString(16).padStart(4, "0")
        } msgLen=${frameMsgLen} — joiner session in the handshake phase (state < 8)`,
      );
      console.log(`${tag}   plain: ${hexOf(decoded)}`);
      // Mirror the keep-alive back, pre-keyed (what a state-6 host answers).
      if (frameTag === 0x5000 && frameMsgLen === 0 && sendKeyedMode !== "never") {
        const pingHdr = knownPeer ? knownPeer.outHdr : 0;
        if (knownPeer) knownPeer.outHdr = pingHdr + 1;
        const ackPlain = buildKeepaliveFrame(pingHdr);
        const ack = encodeFrame(ackPlain, pingHdr, PRE_HANDSHAKE_XOR);
        console.log(
          `${tag} PING-ACK OUT ${sender} (${ack.length} bytes, PRE-keyed keep-alive, hdr=0x${
            pingHdr.toString(16).padStart(4, "0")
          })`,
        );
        console.log(`${tag}   hex: ${hexOf(ack)}`);
        await result.socket.send(ack, {
          transport: "udp",
          port: remote.port,
          hostname: remote.hostname,
        });
      }
    } else if (keyedVerified && knownPeer) {
      // True session-keyed frame — the joiner's dial session reached the
      // data phase (state >= 8). Everything from here is K-keyed.
      knownPeer.accepted = true;
      knownPeer.keyedPings++;
      const decodedK = work.slice();
      xorChain(decodedK, hdr, sessionKey, false);
      const frameTag = decodedK.length >= 6 ? u16LE(decodedK, 2) : -1;
      const frameMsgLen = decodedK.length >= 6 ? u16LE(decodedK, 4) : -1;
      console.log(
        `${tag} SESSION-KEYED frame hdr=0x${hdr.toString(16).padStart(4, "0")} tag=0x${
          (frameTag & 0xffff).toString(16).padStart(4, "0")
        } msgLen=${frameMsgLen} K=0x${sessionKey.toString(16)} — JOINER IS IN THE DATA PHASE (state >= 8)`,
      );
      console.log(`${tag}   plain: ${hexOf(decodedK)}`);
    } else {
      console.log(
        `${tag} undecodable frame: digest fails with the pre-key${
          knownPeer ? ` and with K^const (K=0x${sessionKey.toString(16)})` : " (no known peer yet)"
        }, raw hdr=0x${hdr.toString(16).padStart(4, "0")}`,
      );
    }
    continue;
  }

  // NAT override: replace the joiner's public endpoint with its private one
  // before logging / storing, so a dial-back uses the reachable LAN address.
  let overrideNote = "";
  if (overridePublic && hs.pairs.length >= 2) {
    overrideNote = `${hs.pairs[0].ip}:${hs.pairs[0].port} -> ${hs.pairs[1].ip}:${hs.pairs[1].port}`;
    hs.pairs[0] = { ...hs.pairs[1] };
  }

  logHandshake(`${tag} HANDSHAKE IN `, hs);
  if (overrideNote) {
    console.log(`${tag}   override: public pair0 ${overrideNote} (P2P_OVERRIDE_PUBLIC on)`);
  }

  // Reply with our own handshake to the joiner's socket.
  const now = Date.now();
  const existing = peers.get(peerKey);
  const replyHdr = existing ? existing.outHdr + 1 : 0;

  if (existing) {
    const since = now - existing.lastReplyAt;
    console.log(
      `${tag}   handshake ${since}ms after our reply (joiner session: ${
        existing.accepted
          ? "signaled us — keep-alive/keyed frames seen"
          : "handshake phase, no keep-alives yet"
      })`,
    );
  }

  const peer: Peer = {
    counterBase: hs.counterBase,
    outHdr: existing ? existing.outHdr + 1 : 0,
    lastReplyAt: now,
    handshakesSeen: (existing?.handshakesSeen ?? 0) + 1,
    accepted: existing?.accepted ?? false,
    keyedPings: existing?.keyedPings ?? 0,
    dialBack: {
      hostname: hs.pairs[0].ip,
      port: hs.pairs[0].port,
    },
  };
  peers.set(peerKey, peer);

  // Advertise the LAN IP the joiner reached us from (same machine in a
  // loopback test), overridable with P2P_HOST for a remote fake host.
  const advertiseIp = Deno.env.get("P2P_HOST") ?? remote.hostname;
  // Stamp the HOST's character id (P2P_ID override, default 1) — see the
  // const block above for why this is NOT an echo of the joiner's id.
  const replyPeerId = p2pIdEnv !== undefined ? ourPeerId : 1;
  const plain = buildHandshake(replyHdr, advertiseIp, result.port, replyPeerId);
  const reply = encodeFrame(plain, replyHdr, PRE_HANDSHAKE_XOR);

  console.log(`${tag} OUT ${sender} (${reply.length} bytes)`);
  console.log(`${tag}   hex: ${hexOf(reply)}`);
  console.log(`${tag}   dec: [${decOf(reply)}]`);
  logHandshake(`${tag} HANDSHAKE OUT`, parseHandshake(plain)!);
  console.log(
    `${tag}   reply peer_id=${replyPeerId} (host id — joiner gates on it; joiner's own id is ${hs.peerId})`,
  );

  await result.socket.send(reply, {
    transport: "udp",
    port: remote.port,
    hostname: remote.hostname,
  });

  // Post-handshake continuation (see the P2P_SEND_KEYED_FRAME block above).
  // The joiner decodes everything we send with the PRE-handshake key until
  // its session reaches state 8 — session-keyed frames are garbage to a
  // state-6 joiner (verified live). Digest keys follow §5.3: the constant
  // for pre-keyed frames, K ^ const for session-keyed ones.
  // KEY ESTABLISHMENT (decoder FUN_002666c8, handshake-phase second chance):
  // a frame whose tail digest verifies with K ^ const flips the joiner's
  // session flags |= 9, and the accept pump then sets state 8 when
  // (flags & 3) == 3. We send an empty tag-0x5000 keep-alive keyed with the
  // session key — the exact frame shape the joiner itself sends, so the
  // message content carries no new assumptions.
  if (sendKeyedMode !== "never" && establish) {
    const keyed = (hs.counterBase ^ ourBase) >>> 0;
    const estHdr = peer.outHdr;
    peer.outHdr = estHdr + 1;
    const plain = buildKeepaliveFrame(estHdr);
    const frame = encodeFrame(
      plain,
      estHdr,
      keyed,
      (keyed ^ TAIL_DIGEST_KEY) >>> 0,
    );
    console.log(
      `${tag} ESTABLISH OUT ${peer.dialBack.hostname}:${peer.dialBack.port} (${frame.length} bytes, SESSION-keyed K=0x${
        keyed.toString(16)
      }, digest=K^const, tag=0x5000, hdr=0x${estHdr.toString(16).padStart(4, "0")})`,
    );
    console.log(`${tag}   hex: ${hexOf(frame)}`);
    await result.socket.send(frame, {
      transport: "udp",
      port: peer.dialBack.port,
      hostname: peer.dialBack.hostname,
    });
  }

  if (sendKeyedMode === "data" || sendKeyedMode === "keyed") {
    const keyed = (hs.counterBase ^ ourBase) >>> 0;
    const preKeyed = sendKeyedMode === "data";
    const dataHdr = peer.outHdr;
    peer.outHdr = dataHdr + 1;
    const plain = buildDataFrame(dataHdr, frameType, frameBody);
    const frame = encodeFrame(
      plain,
      dataHdr,
      preKeyed ? PRE_HANDSHAKE_XOR : keyed,
      preKeyed ? TAIL_DIGEST_KEY : (keyed ^ TAIL_DIGEST_KEY) >>> 0,
    );
    console.log(
      `${tag} DATA OUT ${peer.dialBack.hostname}:${peer.dialBack.port} (${frame.length} bytes, ${
        preKeyed
          ? "PRE-keyed (state<8 crypto)"
          : `SESSION-keyed K=0x${keyed.toString(16)}, digest=K^const`
      }, type=0x${frameType.toString(16).padStart(4, "0")}, body=${frameBody.length}B, hdr=0x${
        dataHdr.toString(16).padStart(4, "0")
      })`,
    );
    console.log(`${tag}   hex: ${hexOf(frame)}`);
    await result.socket.send(frame, {
      transport: "udp",
      port: peer.dialBack.port,
      hostname: peer.dialBack.hostname,
    });
  }
}