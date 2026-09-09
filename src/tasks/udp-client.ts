// MGO2 UDP P2P client — sends a join handshake and dumps the response to a
// PCAP file.
//
// Usage:
//   deno task udp-client
//
// Env vars:
//   UDP_CLIENT_PORT       — local bind port for receiving (default 11181)
//   UDP_CLIENT_START_PORT — first remote port to try (default 1)
//   UDP_CLIENT_END_PORT   — last remote port to try  (default 65535)
//   UDP_CLIENT_TIMEOUT    — total seconds to scan all ports (default 15)
//   UDP_CLIENT_HOST       — target hostname           (default game.mgo2pc.com)
//   UDP_CLIENT_PEER_ID    — our character id           (default 2)
//   UDP_CLIENT_BASE       — our counter base hex       (default 0xa9d0a9e4)
//   UDP_CLIENT_IP         — our advertised IP          (default auto-detect)
//   UDP_CLIENT_OUT        — output PCAP path           (default udp-capture.pcap)
//
// The persistent port (0x2bad = 11181) is the default bind of the game client's
// own P2P socket. The join dial is sent FROM the client's advertised port TO the
// host's advertised port. This client listens on 11181 for the host's reply and
// sends handshakes from an ephemeral port.

const LCG_MULT = 0x5d588b65;
const PRE_HANDSHAKE_XOR = 0x87103c2f;
const MODULE_MAGIC = 0x4d258ab7;
const HANDSHAKE_PREFIX = 0x001c1000;
const TAIL_DIGEST_KEY = 0x2b58de69;

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

function concatBytes(a: Uint8Array, b: Uint8Array): Uint8Array {
  const out = new Uint8Array(a.length + b.length);
  out.set(a);
  out.set(b, a.length);
  return out;
}

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

// ---------------------------------------------------------------------------
// Wire crypto (from udp-dump.ts, verified against live captures)
// ---------------------------------------------------------------------------

const lcg = (x: number): number => (Math.imul(x, LCG_MULT) + 1) >>> 0;

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

function unscrambleHeaderOnly(raw: Uint8Array): number {
  const n = raw.length;
  const [p0, p1, q0, q1] = scramblePositions(n);
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

function encodeFrame(
  plain: Uint8Array,
  hdr: number,
  keyed: number,
  digestKey: number = TAIL_DIGEST_KEY,
): Uint8Array {
  const out = plain.slice();
  xorChain(out, hdr, keyed, true);
  computeTailDigest(out, digestKey);
  const n = out.length;
  const [p0, p1, q0, q1] = scramblePositions(n);
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

function decodeFrame(
  raw: Uint8Array,
  keyed: number,
): { hdr: number; decoded: Uint8Array } | null {
  const work = raw.slice();
  const hdr = unscrambleHeaderOnly(work);
  if (!verifyTailDigest(work, TAIL_DIGEST_KEY)) {
    if (!verifyTailDigest(work, (keyed ^ TAIL_DIGEST_KEY) >>> 0)) {
      return null;
    }
  }
  const decoded = work.slice();
  xorChain(decoded, hdr, keyed, false);
  return { hdr, decoded };
}

// ---------------------------------------------------------------------------
// Handshake parse / build
// ---------------------------------------------------------------------------

function u32LE(b: Uint8Array, off: number): number {
  return (b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24)) >>> 0;
}

function u16LE(b: Uint8Array, off: number): number {
  return (b[off] | (b[off + 1] << 8)) >>> 0;
}

interface Handshake {
  peerId: number;
  counterBase: number;
  magic: number;
  flags: number;
  unk: number;
  count: number;
  pairs: Array<{ ip: string; port: number }>;
}

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
    magic: u32LE(decoded, 14),
    flags: decoded[18],
    unk: u16LE(decoded, 19),
    count,
    pairs,
  };
}

function buildHandshake(
  hdr: number,
  peerId: number,
  counterBase: number,
  ourIp: string,
  port: number,
): Uint8Array {
  const out = new Uint8Array(44);
  out[0] = hdr & 0xff;
  out[1] = (hdr >> 8) & 0xff;
  // constant prefix at [2..6)
  out[2] = (HANDSHAKE_PREFIX >> 0) & 0xff;
  out[3] = (HANDSHAKE_PREFIX >> 8) & 0xff;
  out[4] = (HANDSHAKE_PREFIX >> 16) & 0xff;
  out[5] = (HANDSHAKE_PREFIX >> 24) & 0xff;
  // peerId [6..10)
  out[6] = peerId & 0xff;
  out[7] = (peerId >> 8) & 0xff;
  out[8] = (peerId >> 16) & 0xff;
  out[9] = (peerId >> 24) & 0xff;
  // counterBase [10..14)
  out[10] = counterBase & 0xff;
  out[11] = (counterBase >> 8) & 0xff;
  out[12] = (counterBase >> 16) & 0xff;
  out[13] = (counterBase >> 24) & 0xff;
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
  const ip = ipToBytes(ourIp) ?? new Uint8Array([127, 0, 0, 1]);
  out.set(ip, 22);
  out[26] = port & 0xff;
  out[27] = (port >> 8) & 0xff;
  out.set(ip, 28);
  out[32] = port & 0xff;
  out[33] = (port >> 8) & 0xff;
  return out;
}

function ipToBytes(ip: string): Uint8Array | null {
  const parts = ip.split(".").map((p) => Number(p));
  if (parts.length !== 4 || parts.some((p) => !Number.isInteger(p) || p < 0 || p > 255)) {
    return null;
  }
  return new Uint8Array(parts);
}

// ---------------------------------------------------------------------------
// PCAP writer (libpcap format — link type 101 = raw IP)
// ---------------------------------------------------------------------------

function writePcapHeader(): Uint8Array {
  const buf = new ArrayBuffer(24);
  const dv = new DataView(buf);
  dv.setUint32(0, 0xa1b2c3d4, false); // magic (big-endian)
  dv.setUint16(4, 2, false);           // version major
  dv.setUint16(6, 4, false);           // version minor
  dv.setUint32(8, 0, false);           // thiszone
  dv.setUint32(12, 0, false);          // sigfigs
  dv.setUint32(16, 65535, false);      // snaplen
  dv.setUint32(20, 101, false);        // linktype = raw IP
  return new Uint8Array(buf);
}

function writePcapPacket(
  tsSec: number,
  tsUsec: number,
  data: Uint8Array,
): Uint8Array {
  const buf = new ArrayBuffer(16 + data.length);
  const dv = new DataView(buf);
  dv.setUint32(0, tsSec >>> 0, false);
  dv.setUint32(4, tsUsec >>> 0, false);
  dv.setUint32(8, data.length, false);
  dv.setUint32(12, data.length, false);
  new Uint8Array(buf).set(data, 16);
  return new Uint8Array(buf);
}

function buildIpUdpPacket(
  srcIp: string,
  dstIp: string,
  srcPort: number,
  dstPort: number,
  payload: Uint8Array,
): Uint8Array {
  const src = ipToBytes(srcIp)!;
  const dst = ipToBytes(dstIp)!;
  // UDP header (8 bytes) + payload
  const udpLen = 8 + payload.length;
  // IPv4 header (20 bytes) + UDP
  const ipTotal = 20 + udpLen;
  const buf = new Uint8Array(ipTotal);
  const dv = new DataView(buf.buffer);

  // IPv4 header
  buf[0] = 0x45;           // version=4, IHL=5
  buf[1] = 0x00;           // DSCP
  dv.setUint16(2, ipTotal, false); // total length
  dv.setUint16(4, 0x0000, false);  // identification
  dv.setUint16(6, 0x4000, false);  // flags=DF, fragment offset=0
  buf[8] = 64;             // TTL
  buf[9] = 17;             // protocol = UDP
  // header checksum = 0 (compute)
  buf.set(src, 12);
  buf.set(dst, 16);
  let sum = 0;
  for (let i = 0; i < 20; i += 2) sum += dv.getUint16(i, false);
  while (sum > 0xffff) sum = (sum & 0xffff) + (sum >> 16);
  dv.setUint16(10, ~sum & 0xffff, false);

  // UDP header
  dv.setUint16(20, srcPort, false);
  dv.setUint16(22, dstPort, false);
  dv.setUint16(24, udpLen, false);
  // UDP checksum = 0 (optional for IPv4)
  buf.set(payload, 28);

  return buf;
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

const localPort = Number(Deno.env.get("UDP_CLIENT_PORT") ?? "11181");
const startPort = Number(Deno.env.get("UDP_CLIENT_START_PORT") ?? "1");
const endPort = Number(Deno.env.get("UDP_CLIENT_END_PORT") ?? "65535");
const timeoutSec = Number(Deno.env.get("UDP_CLIENT_TIMEOUT") ?? "15");
const remoteHost = Deno.env.get("UDP_CLIENT_HOST") ?? "game.mgo2pc.com";
const ourPeerId = Number(Deno.env.get("UDP_CLIENT_PEER_ID") ?? "2") >>> 0;
const ourBase = Number(Deno.env.get("UDP_CLIENT_BASE") ?? "0xa9d0a9e4") >>> 0;
const ourIp = Deno.env.get("UDP_CLIENT_IP") ?? "192.168.1.50";
const outPath = Deno.env.get("UDP_CLIENT_OUT") ?? "udp-capture.pcap";

console.log(`[udp-client] persistent port: ${localPort}`);
console.log(`[udp-client] target: ${remoteHost}`);
console.log(`[udp-client] port range: ${startPort}..${endPort}`);
console.log(`[udp-client] timeout: ${timeoutSec}s per port`);
console.log(`[udp-client] peer_id: 0x${ourPeerId.toString(16).padStart(8, "0")}`);
console.log(`[udp-client] base: 0x${ourBase.toString(16).padStart(8, "0")}`);
console.log(`[udp-client] output: ${outPath}`);

// Resolve the target hostname once. If it's already an IP literal, use it directly.
let targetIp: string;
if (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(remoteHost)) {
  targetIp = remoteHost;
  console.log(`[udp-client] using IP literal ${targetIp}`);
} else {
  const targetAddrs = await Deno.resolveDns(remoteHost, "A");
  if (targetAddrs.length === 0) {
    console.error(`[udp-client] failed to resolve ${remoteHost}`);
    Deno.exit(1);
  }
  targetIp = targetAddrs[0];
  console.log(`[udp-client] resolved ${remoteHost} -> ${targetIp}`);
}

// Open the output file and write the PCAP global header.
const pcapFile = await Deno.open(outPath, { write: true, create: true, truncate: true });
await pcapFile.write(writePcapHeader());

// Build our handshake plaintext (34 bytes, will be encoded to 44-byte wire format).
let handshakeCounter = 0;
function nextHandshake(): Uint8Array {
  const plain = buildHandshake(handshakeCounter, ourPeerId, ourBase, ourIp, localPort);
  const wire = encodeFrame(plain, handshakeCounter, PRE_HANDSHAKE_XOR);
  handshakeCounter++;
  return wire;
}

// Bind the local UDP socket on the persistent port.
const socket = Deno.listenDatagram({ port: localPort, hostname: "0.0.0.0", transport: "udp" });
console.log(`[udp-client] listening on 0.0.0.0:${localPort}`);

// Scan port range: blast handshakes to ALL ports as fast as possible, listen
// for replies concurrently. Total timeout = timeoutSec (default 15s).
let answered = false;
const startTime = Date.now();
const deadline = startTime + timeoutSec * 1000;

// Listen in background — resolve when we get a valid reply.
let replyResolve: ((port: number) => void) | null = null;
const replyPromise = new Promise<number>((resolve) => { replyResolve = resolve; });

async function listenForReplies(): Promise<void> {
  while (Date.now() < deadline) {
    try {
      const timeoutLeft = deadline - Date.now();
      if (timeoutLeft <= 0) break;
      const result = await Promise.race([
        socket.receive() as Promise<[Uint8Array, Deno.NetAddr]>,
        new Promise<never>((_, rej) => setTimeout(() => rej(new Error("timeout")), timeoutLeft)),
      ]);
      const [raw, remote] = result;
      const now = Date.now();
      const elapsed = now - startTime;
      console.log(`[udp-client] <- ${remote.hostname}:${remote.port} (${raw.length} bytes) after ${elapsed}ms`);

      // Write to PCAP.
      const tsSec = Math.floor(now / 1000);
      const tsUsec = (now % 1000) * 1000;
      const ipPkt = buildIpUdpPacket(remote.hostname, ourIp, remote.port, localPort, raw);
      await pcapFile.write(writePcapPacket(tsSec, tsUsec, ipPkt));

      // Decode.
      const decoded = decodeFrame(raw, PRE_HANDSHAKE_XOR);
      if (decoded) {
        const hs = parseHandshake(decoded.decoded);
        if (hs) {
          console.log(
            `[udp-client] HANDSHAKE REPLY peerId=0x${hs.peerId.toString(16).padStart(8, "0")} ` +
            `base=0x${hs.counterBase.toString(16).padStart(8, "0")} ` +
            `magic=0x${hs.magic.toString(16).padStart(8, "0")}`,
          );
          for (let i = 0; i < hs.pairs.length; i++) {
            const p = hs.pairs[i];
            console.log(`[udp-client]   pair${i}: ${p.ip}:${p.port}`);
          }
          replyResolve!(remote.port);
          return;
        } else {
          const frameTag = decoded.decoded.length >= 6 ? u16LE(decoded.decoded, 2) : -1;
          console.log(`[udp-client] frame tag=0x${(frameTag & 0xffff).toString(16).padStart(4, "0")} — not a handshake`);
        }
      } else {
        console.log(`[udp-client] undecodable frame, hex: ${hexOf(raw)}`);
      }
    } catch (e) {
      if (e instanceof Error && e.message === "timeout") break;
      if (e instanceof Deno.errors.BadResource) break;
      throw e;
    }
  }
}

// Start listener before blasting.
const listenerDone = listenForReplies();

// Blast handshakes to all ports.
let sent = 0;
const totalPorts = endPort - startPort + 1;
for (let port = startPort; port <= endPort; port++) {
  if (Date.now() >= deadline) break;
  const hs = nextHandshake();
  await socket.send(hs, { transport: "udp", port, hostname: targetIp });

  // Log outgoing to PCAP (sampled — every 100th to avoid file bloat).
  if (sent % 100 === 0) {
    const nowSend = Date.now();
    const tsSecSend = Math.floor(nowSend / 1000);
    const tsUsecSend = (nowSend % 1000) * 1000;
    const ipPktSend = buildIpUdpPacket(ourIp, targetIp, localPort, port, hs);
    await pcapFile.write(writePcapPacket(tsSecSend, tsUsecSend, ipPktSend));
  }
  sent++;
  if (sent % 10000 === 0) {
    console.log(`[udp-client] sent ${sent}/${totalPorts} handshakes (${Date.now() - startTime}ms elapsed)`);
  }
}
console.log(`[udp-client] blast complete: ${sent} handshakes sent in ${Date.now() - startTime}ms`);

// Wait for listener to finish (hits deadline or gets a reply).
await listenerDone;

const elapsed = Date.now() - startTime;
if (elapsed >= timeoutSec * 1000) {
  console.log(`[udp-client] no reply after ${elapsed}ms — scanning ports ${startPort}..${endPort}`);
  await pcapFile.close();
  Deno.exit(1);
}

console.log(`[udp-client] received reply — listening for data frames...`);

// Keep listening for follow-up frames until interrupted.
async function listenLoop(): Promise<void> {
  while (true) {
    try {
      const [raw, remote] = await socket.receive() as [Uint8Array, Deno.NetAddr];
      const now = Date.now();
      const tsSec = Math.floor(now / 1000);
      const tsUsec = (now % 1000) * 1000;

      console.log(`[udp-client] <- ${remote.hostname}:${remote.port} (${raw.length} bytes)`);

      const ipPkt = buildIpUdpPacket(remote.hostname, ourIp, remote.port, localPort, raw);
      await pcapFile.write(writePcapPacket(tsSec, tsUsec, ipPkt));

      const decoded = decodeFrame(raw, PRE_HANDSHAKE_XOR);
      if (decoded) {
        const hs = parseHandshake(decoded.decoded);
        if (hs) {
          console.log(
            `[udp-client] HANDSHAKE peerId=0x${hs.peerId.toString(16).padStart(8, "0")} ` +
            `base=0x${hs.counterBase.toString(16).padStart(8, "0")}`,
          );
        } else {
          const frameTag = decoded.decoded.length >= 6 ? u16LE(decoded.decoded, 2) : -1;
          console.log(`[udp-client] frame tag=0x${(frameTag & 0xffff).toString(16).padStart(4, "0")}`);
        }
      }
    } catch (e) {
      if (e instanceof Deno.errors.BadResource) break;
      throw e;
    }
  }
}

await listenLoop();

await pcapFile.close();
console.log(`[udp-client] done — wrote ${outPath}`);

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const hexOf = (bytes: Uint8Array): string =>
  Array.from(bytes).map((b) => b.toString(16).padStart(2, "0")).join(" ");
