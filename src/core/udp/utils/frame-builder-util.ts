// Plaintext frame builders for the p2p channel (wire framing added by
// encodeFrame in frame-crypto-util.ts). Layouts verified against the game's
// serializer FUN_00269860 / handshake builder FUN_00268ba8.

import {
  UDP_TAIL_SIZE,
} from "../constants/udp-commands-constants.ts";
import { UDP_MODULE_MAGIC } from "../constants/udp-crypto-keys-constants.ts";
import { concatBytes, putU16LE, putU32LE, u16LE, u32LE } from "./binary-util.ts";
import { serializeMessages } from "./message-codec-util.ts";
import type { UdpMessage } from "../types/udp-type.ts";

/**
 * Build a plaintext frame wrapping messages: hdr u16 LE + the serialized
 * messages + the zeroed 10-byte tail region (encodeFrame fills it — the XOR
 * chain and digest both run over the full frame length, matching the game's
 * 16-byte keep-alive / 44-byte handshake plaintexts).
 */
export function buildMessageFrame(counter: number, messages: UdpMessage[]): Uint8Array {
  const hdr = new Uint8Array(2);
  putU16LE(hdr, 0, counter);
  return concatBytes(hdr, serializeMessages(messages), new Uint8Array(UDP_TAIL_SIZE));
}

export function messageOf(type: number, body: Uint8Array, flags = 0): UdpMessage {
  return { type, length: body.length, flags, body };
}

/** The empty keep-alive message (type 0x5000, len 0). */
export function keepAliveMessage(): UdpMessage {
  return messageOf(0x5000, new Uint8Array(0));
}

/** Host identity announced in the handshake message body. */
export interface HandshakeBody {
  peerId: number;
  counterBase: number;
  magic: number;
  flags: number;
  unk: number;
  pairs: Array<{ ip: string; port: number }>;
}

/**
 * Build the handshake message body (type 0x1000, 28 bytes). peerId is the
 * HOST's character id — the joiner's drain gate checks the reply's peer_id
 * against the peer descriptor stored in its dial session, NOT an echo of its
 * own id (§4 gate 1). The same endpoint is advertised in both pair slots.
 */
export function buildHandshakeBody(
  peerId: number,
  counterBase: number,
  advertiseIp: string,
  advertisePort: number,
): Uint8Array {
  const body = new Uint8Array(0x1c);
  putU32LE(body, 0, peerId);
  putU32LE(body, 4, counterBase);
  putU32LE(body, 8, UDP_MODULE_MAGIC);
  body[12] = 0x02; // flags (bit 2 must be clear)
  body[13] = 0x01; // unk
  body[14] = 0x00;
  body[15] = 0x02; // pair count
  const ip = ipToBytes(advertiseIp) ?? new Uint8Array([127, 0, 0, 1]);
  body.set(ip, 16);
  putU16LE(body, 20, advertisePort);
  body.set(ip, 22);
  putU16LE(body, 26, advertisePort);
  return body;
}

/** Parse a handshake message body. Returns null when it isn't a valid one. */
export function parseHandshakeBody(body: Uint8Array): HandshakeBody | null {
  if (body.length < 0x10) return null;
  const magic = u32LE(body, 8);
  if (magic !== UDP_MODULE_MAGIC) return null;
  const flags = body[12];
  if (flags & 0x04) return null;
  const count = body[15];
  if (count > 2) return null;
  const pairs: Array<{ ip: string; port: number }> = [];
  for (let i = 0; i < count; i++) {
    const off = 16 + i * 6;
    pairs.push({
      ip: `${body[off]}.${body[off + 1]}.${body[off + 2]}.${body[off + 3]}`,
      port: u16LE(body, off + 4),
    });
  }
  return {
    peerId: u32LE(body, 0),
    counterBase: u32LE(body, 4),
    magic,
    flags,
    unk: u16LE(body, 13),
    pairs,
  };
}

export function ipToBytes(ip: string): Uint8Array | null {
  const parts = ip.split(".").map((p) => Number(p));
  if (parts.length !== 4 || parts.some((p) => !Number.isInteger(p) || p < 0 || p > 255)) {
    return null;
  }
  return new Uint8Array(parts);
}
