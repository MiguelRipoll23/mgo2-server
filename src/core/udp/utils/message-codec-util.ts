// Codec for the p2p frame content region (§6.2 of docs/udp-p2p-protocol.md):
//
//   [0..2)  hdr u16 LE — shared per-peer counter; bit 0x8000 = LZSS-compressed
//   [2..)   content: concatenated messages { type u16 LE | len u8 | flags u8 |
//           body }, OR a raw LZSS stream when the marker is set
//   [..)    10-byte tail digest (verified/stripped by frame-crypto-util)
//
// The 2-byte tag word IS the first message's type — "tag" and "message type"
// are the same field (the handshake's `00 10 1c 00` is type 0x1000, len 0x1c).

import {
  UDP_COMPRESSION_MARKER,
  UDP_COUNTER_MASK,
  UDP_MESSAGE_HEADER_SIZE,
  UDP_TAIL_SIZE,
} from "../constants/udp-commands-constants.ts";
import type { UdpFrame, UdpMessage } from "../types/udp-type.ts";
import { lzssDecompress } from "./lzss-util.ts";
import { concatBytes, putU16LE, u16LE } from "./binary-util.ts";

/**
 * Parse a fully decoded datagram (chain + scramble removed, digest verified)
 * into a frame: messages from the content region, decompressed when the
 * compression marker is set.
 */
export function decodeFrame(decoded: Uint8Array): UdpFrame {
  const rawCounter = decoded.length >= 2 ? u16LE(decoded, 0) : 0;
  const compressed = (rawCounter & UDP_COMPRESSION_MARKER) !== 0;
  let content = decoded.subarray(
    2,
    Math.max(2, decoded.length - UDP_TAIL_SIZE),
  );
  if (compressed && content.length > 0) {
    content = lzssDecompress(content) ?? new Uint8Array(0);
  }
  return {
    counter: rawCounter & UDP_COUNTER_MASK,
    compressed,
    content,
    messages: parseMessages(content),
    decoded,
  };
}

export function parseMessages(buf: Uint8Array): UdpMessage[] {
  const messages: UdpMessage[] = [];
  let off = 0;
  while (off + UDP_MESSAGE_HEADER_SIZE <= buf.length) {
    const type = u16LE(buf, off);
    const length = buf[off + 2];
    const flags = buf[off + 3];
    const body = buf.slice(
      off + UDP_MESSAGE_HEADER_SIZE,
      Math.min(off + UDP_MESSAGE_HEADER_SIZE + length, buf.length),
    );
    messages.push({ type, length, flags, body });
    off += UDP_MESSAGE_HEADER_SIZE + length;
  }
  return messages;
}

/** Serialize messages into a content region payload (after the hdr word). */
export function serializeMessages(messages: UdpMessage[]): Uint8Array {
  let total = 0;
  for (const m of messages) total += UDP_MESSAGE_HEADER_SIZE + m.body.length;
  const out = new Uint8Array(total);
  let off = 0;
  for (const m of messages) {
    out[off] = m.type & 0xff;
    out[off + 1] = (m.type >>> 8) & 0xff;
    out[off + 2] = m.body.length & 0xff;
    out[off + 3] = m.flags & 0xff;
    out.set(m.body, off + UDP_MESSAGE_HEADER_SIZE);
    off += UDP_MESSAGE_HEADER_SIZE + m.body.length;
  }
  return out;
}

/**
 * Build the packet-log bytes for a frame: hdr + content region, with the
 * content DECOMPRESSED for compressed frames (the log shows the real message
 * bytes, not the LZSS stream). The tail digest is omitted — it is derived
 * checksum data, not content.
 */
export function frameToLogBytes(frame: UdpFrame): Uint8Array {
  const hdr = new Uint8Array(2);
  putU16LE(hdr, 0, frame.counter | (frame.compressed ? UDP_COMPRESSION_MARKER : 0));
  return concatBytes(hdr, frame.content);
}
