import { HEADER_SIZE, MAX_PAYLOAD_LENGTH } from "../types/packet-type.ts";
import type { Packet } from "../types/packet-type.ts";
import {
  xorBuffer,
  computeHmacMd5,
  blowfishDecrypt,
  blowfishEncrypt,
} from "../utils/crypto-util.ts";
import { BLOWFISH_KEY_PACKET } from "../../constants/crypto-keys-constants.ts";
import {
  BLOWFISH_ENCRYPTED_INBOUND,
  BLOWFISH_ENCRYPTED_OUTBOUND,
} from "../types/tcp-constants-type.ts";

const PACKET_KEY_VIEW = new DataView(
  BLOWFISH_KEY_PACKET.buffer,
  BLOWFISH_KEY_PACKET.byteOffset,
);

/** Applied to non-zero error codes in error response packets. */
const ERROR_MASK = (0xc0ffee << 8) >>> 0;

/** Blowfish block size in bytes. */
const BLOWFISH_BLOCK_SIZE = 8;

function readU16Be(buffer: Uint8Array, offset: number): number {
  return (buffer[offset] << 8) | buffer[offset + 1];
}

function readU32Be(buffer: Uint8Array, offset: number): number {
  return (
    ((buffer[offset] << 24) |
      (buffer[offset + 1] << 16) |
      (buffer[offset + 2] << 8) |
      buffer[offset + 3]) >>>
    0
  );
}

function writeU16Be(buffer: Uint8Array, offset: number, value: number): void {
  buffer[offset] = (value >>> 8) & 0xff;
  buffer[offset + 1] = value & 0xff;
}

function writeU32Be(buffer: Uint8Array, offset: number, value: number): void {
  buffer[offset] = (value >>> 24) & 0xff;
  buffer[offset + 1] = (value >>> 16) & 0xff;
  buffer[offset + 2] = (value >>> 8) & 0xff;
  buffer[offset + 3] = value & 0xff;
}

function padToBlockSize(data: Uint8Array, blockSize: number): Uint8Array {
  const remainder = data.length % blockSize;
  if (remainder === 0) return data;
  const padded = new Uint8Array(data.length + (blockSize - remainder));
  padded.set(data);
  return padded;
}

function checksumsEqual(first: Uint8Array, second: Uint8Array): boolean {
  if (first.length !== second.length) return false;
  return first.every((byte, index) => byte === second[index]);
}

/**
 * Decodes a raw MGO2 TCP packet buffer into a structured Packet.
 * Returns null if the buffer is malformed or the checksum is invalid.
 *
 * Wire format (XOR'd with 0x5A7085AF):
 *   [0x00] command       : u16 BE
 *   [0x02] payloadLength : u16 BE
 *   [0x04] sequence      : u32 BE
 *   [0x08] checksum      : 16 bytes (HMAC-MD5)
 *   [0x18] payload       : variable
 */
export function decodePacket(buffer: Uint8Array): Packet | null {
  if (buffer.length < HEADER_SIZE) return null;

  // Work on a copy so we don't mutate the caller's buffer
  const decrypted = buffer.slice();
  xorBuffer(decrypted);

  const command = readU16Be(decrypted, 0);
  const payloadLength = readU16Be(decrypted, 2);
  const sequence = readU32Be(decrypted, 4);
  const checksum = decrypted.slice(8, 24);

  if (payloadLength > MAX_PAYLOAD_LENGTH) return null;
  if (decrypted.length < HEADER_SIZE + payloadLength) return null;

  let payload = decrypted.slice(HEADER_SIZE, HEADER_SIZE + payloadLength);

  const expectedChecksum = computeHmacMd5(decrypted.slice(0, 8), payload);
  if (!checksumsEqual(checksum, expectedChecksum)) {
    return null;
  }

  if ((BLOWFISH_ENCRYPTED_INBOUND as number[]).includes(command)) {
    const paddedPayload = padToBlockSize(payload, BLOWFISH_BLOCK_SIZE);
    blowfishDecrypt(paddedPayload, PACKET_KEY_VIEW);
    payload = paddedPayload.slice(0, payloadLength);
  }

  return {
    header: { command, payloadLength, sequence, checksum },
    payload,
  };
}

/**
 * Encodes a structured packet into an XOR'd MGO2 wire-format buffer.
 * Applies Blowfish encryption to the payload when required by the command ID.
 */
export function encodePacket(
  commandId: number,
  payload: Uint8Array,
  sequenceOut: number,
  logPrefix?: string,
): Uint8Array {
  let encodedPayload = payload;

  // Some commands expect the payload to be Blowfish-encrypted on the wire
  // (these are listed in BLOWFISH_ENCRYPTED_OUTBOUND for server->client and
  // BLOWFISH_ENCRYPTED_INBOUND for client->server). When encoding a packet
  // for sending (either direction in tests), encrypt the payload if the
  // command is present in either list so the resulting wire bytes match the
  // protocol expected by the peer.
  if (
    (BLOWFISH_ENCRYPTED_OUTBOUND as number[]).includes(commandId) ||
    (BLOWFISH_ENCRYPTED_INBOUND as number[]).includes(commandId)
  ) {
    encodedPayload = padToBlockSize(payload, BLOWFISH_BLOCK_SIZE);
    blowfishEncrypt(encodedPayload, PACKET_KEY_VIEW);
  }

  const packetBuffer = new Uint8Array(HEADER_SIZE + encodedPayload.length);
  writeU16Be(packetBuffer, 0, commandId);
  writeU16Be(packetBuffer, 2, encodedPayload.length);
  writeU32Be(packetBuffer, 4, sequenceOut);

  const checksum = computeHmacMd5(packetBuffer.slice(0, 8), encodedPayload);
  packetBuffer.set(checksum, 8);
  packetBuffer.set(encodedPayload, HEADER_SIZE);

  const prefix = logPrefix ?? "packet-codec";
  const hex = Array.from(packetBuffer)
    .map((b) => b.toString(16).padStart(2, "0"))
    .join(" ");
  console.log(
    `[${prefix}] OUT 0x${commandId.toString(16).padStart(4, "0")} (${packetBuffer.length} bytes): ${hex}`,
  );

  xorBuffer(packetBuffer);
  return packetBuffer;
}

export function encodeErrorPacket(
  commandId: number,
  errorCode: number,
  sequenceOut: number,
  logPrefix?: string,
): Uint8Array {
  const payload = new Uint8Array(4);
  const maskedErrorCode = errorCode !== 0 ? (errorCode | ERROR_MASK) >>> 0 : 0;
  writeU32Be(payload, 0, maskedErrorCode);
  return encodePacket(commandId, payload, sequenceOut, logPrefix);
}

export function encodeAckPacket(
  commandId: number,
  sequenceOut: number,
  logPrefix?: string,
): Uint8Array {
  return encodePacket(commandId, new Uint8Array(0), sequenceOut, logPrefix);
}
