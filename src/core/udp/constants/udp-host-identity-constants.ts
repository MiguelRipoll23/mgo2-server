// Identity the dedicated host presents on the p2p channel.
//
// peer_id on the wire is the SENDER's own id: the reply must carry the HOST's
// character id (the seeded NPC, 1) — the joiner's drain gate compares it
// against the peer descriptor stored in its dial session, NOT an echo of its
// own id (§4 gate 1).
export const UDP_HOST_PEER_ID = 1;
export const UDP_HOST_COUNTER_BASE = 0x12345678;
