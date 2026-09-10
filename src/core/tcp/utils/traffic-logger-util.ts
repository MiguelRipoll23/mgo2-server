import type { TcpSession } from "../types/session-type.ts";

const BYTES_PER_LINE = 16;

function formatAscii(byte: number): string {
  return byte >= 0x20 && byte <= 0x7e ? String.fromCharCode(byte) : ".";
}

export function formatHexAscii(buffer: Uint8Array): string {
  const lines: string[] = [];

  for (let offset = 0; offset < buffer.length; offset += BYTES_PER_LINE) {
    const chunk = buffer.subarray(offset, offset + BYTES_PER_LINE);
    const hex = Array.from(chunk, (byte) => byte.toString(16).padStart(2, "0")).join(" ");
    const ascii = Array.from(chunk, formatAscii).join("");
    lines.push(`${offset.toString(16).padStart(4, "0")}  ${hex.padEnd(47, " ")}  ${ascii}`);
  }

  return lines.join("\n");
}

export function logUdpTraffic(
  serverType: string,
  remoteAddress: string,
  direction: "IN" | "OUT",
  buffer: Uint8Array,
): void {
  console.debug(
    `[${serverType}] ${direction} ${remoteAddress} ${buffer.length} bytes\n${formatHexAscii(buffer)}`,
  );
}

const hexOf = (buffer: Uint8Array): string =>
  Array.from(buffer, (byte) => byte.toString(16).padStart(2, "0")).join(" ");

/** UDP p2p equivalent of logTcpInPacket — decrypted datagram, hex-only line. */
export function logUdpInPacket(
  logPrefix: string,
  tag: string,
  buffer: Uint8Array,
): void {
  const hex = hexOf(buffer);
  console.debug(`[${logPrefix}] IN ${tag} (${buffer.length} bytes): ${hex}`);
}

/** UDP p2p equivalent of encodePacket's OUT log — plaintext frame, hex-only line. */
export function logUdpOutPacket(
  logPrefix: string,
  tag: string,
  buffer: Uint8Array,
): void {
  const hex = hexOf(buffer);
  console.debug(`[${logPrefix}] OUT ${tag} (${buffer.length} bytes): ${hex}`);
}

export function logUdpConnection(logPrefix: string, remoteAddress: string): void {
  console.info(`[${logPrefix}] Peer connected from ${remoteAddress}`);
}

export function logUdpDisconnection(logPrefix: string, remoteAddress: string): void {
  console.info(`[${logPrefix}] Peer disconnected from ${remoteAddress}`);
}

export function logTcpConnection(logPrefix: string, remoteAddress: string): void {
  console.info(`[${logPrefix}] Client connected from ${remoteAddress}`);
}

export function logTcpDisconnection(logPrefix: string, remoteAddress: string): void {
  console.info(`[${logPrefix}] Client disconnected from ${remoteAddress}`);
}

export function logTcpInPacket(
  session: TcpSession,
  buffer: Uint8Array,
): void {
  const hex = Array.from(buffer).map((b) => b.toString(16).padStart(2, "0")).join(" ");
  const command = (buffer[0] << 8) | buffer[1];
  console.debug(`[${session.logPrefix}] IN 0x${command.toString(16).padStart(4, "0")} (${buffer.length} bytes): ${hex}`);
}

export async function writeLoggedBytes(
  session: TcpSession,
  bytes: Uint8Array,
): Promise<void> {
  await session.connection.write(bytes);
}
