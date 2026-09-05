export interface PacketHeader {
  command: number;       // u16
  payloadLength: number; // u16
  sequence: number;      // u32
  checksum: Uint8Array;  // 16 bytes
}

export interface Packet {
  header: PacketHeader;
  payload: Uint8Array;
}

export const HEADER_SIZE = 24;
// 0x400 (1024) is accepted on decode: payloads are zero-padded to block
// alignment for Blowfish, and padded inbound packets carry the padded length.
export const MAX_PAYLOAD_LENGTH = 0x400;
export const MAX_PACKET_LENGTH = MAX_PAYLOAD_LENGTH + HEADER_SIZE + 1;
