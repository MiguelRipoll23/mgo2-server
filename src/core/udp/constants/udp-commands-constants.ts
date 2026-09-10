// Message types carried in a p2p frame's content region (u16 LE, §6.2 of
// docs/udp-p2p-protocol.md). Dispatched via @PeerCommandHandler.
export const UDP_CMD_HANDSHAKE = 0x1000;
export const UDP_CMD_KEEP_ALIVE = 0x5000;
export const UDP_CMD_PLAYER_PROFILE = 0x1001;

// Frame layout: [2-byte scrambled hdr][content region][10-byte tail digest].
export const UDP_HEADER_SIZE = 2;
export const UDP_TAIL_SIZE = 10;
export const UDP_MESSAGE_HEADER_SIZE = 4;
export const UDP_FRAME_OVERHEAD = UDP_HEADER_SIZE + UDP_TAIL_SIZE;

// hdr bit 15 marks the content region as a raw LZSS stream; it is masked out
// of the chain seed (hdr & 0x7fff).
export const UDP_COMPRESSION_MARKER = 0x8000;
export const UDP_COUNTER_MASK = 0x7fff;

// LZSS decompressor bounds (FUN_00efd840).
export const UDP_LZSS_RING_SIZE = 0x200;
export const UDP_LZSS_MAX_OUTPUT = 0x800;
