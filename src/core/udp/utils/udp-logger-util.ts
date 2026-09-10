const hexOf = (buffer: Uint8Array): string =>
  Array.from(buffer, (byte) => byte.toString(16).padStart(2, "0")).join(" ");

const hex4 = (v: number): string => `0x${(v & 0xffff).toString(16).padStart(4, "0")}`;

/** UDP p2p equivalent of logTcpInPacket — decrypted datagram, hex-only line. */
export function logUdpInPacket(
  logPrefix: string,
  tag: string,
  buffer: Uint8Array,
): void {
  console.debug(`[${logPrefix}] IN ${tag} (${buffer.length} bytes): ${hexOf(buffer)}`);
}

/** UDP p2p equivalent of encodePacket's OUT log — plaintext frame, hex-only line. */
export function logUdpOutPacket(
  logPrefix: string,
  tag: string,
  buffer: Uint8Array,
): void {
  console.debug(`[${logPrefix}] OUT ${tag} (${buffer.length} bytes): ${hexOf(buffer)}`);
}

export function logUdpConnection(logPrefix: string, remoteAddress: string): void {
  console.info(`[${logPrefix}] Peer connected from ${remoteAddress}`);
}

export function logUdpDisconnection(logPrefix: string, remoteAddress: string): void {
  console.info(`[${logPrefix}] Peer disconnected from ${remoteAddress}`);
}

export function formatUdpCounter(counter: number): string {
  return hex4(counter);
}

export function formatUdpMessageType(type: number): string {
  return hex4(type);
}

/** 32-bit hex (8 digits) — session keys, counter bases, peer ids. */
export function formatUdpU32(v: number): string {
  return `0x${(v >>> 0).toString(16).padStart(8, "0")}`;
}
