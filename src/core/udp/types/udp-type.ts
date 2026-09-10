/** A decoded message carried in a p2p frame's content region (§6.2). */
export interface UdpMessage {
  /** Message type, u16 LE (class bits 0x4000/0x8000 live in this word). */
  type: number;
  /** Body length, u8. */
  length: number;
  /** Per-message flags byte following the length. */
  flags: number;
  /** Body bytes (length long). */
  body: Uint8Array;
}

/** A fully decoded p2p frame: what the wire datagram means. */
export interface UdpFrame {
  /** hdr counter (u16 LE), compression marker masked off. */
  counter: number;
  /** Whether the content region is a raw LZSS stream (hdr bit 0x8000). */
  compressed: boolean;
  /** The content region (decompressed when the marker was set). */
  content: Uint8Array;
  /** Messages parsed from the content region. */
  messages: UdpMessage[];
  /** The raw decoded (chain-removed, scramble-removed) datagram. */
  decoded: Uint8Array;
}

/** Negotiated p2p session with one peer, keyed by its remote endpoint. */
export interface PeerSession {
  /** Remote endpoint "ip:port" this session is keyed by (the joiner's socket). */
  remoteAddress: string;
  /** Where to send frames back to the peer (may differ from remoteAddress). */
  dialBack: { hostname: string; port: number };
  /** counter_base the peer announced in its handshake. */
  counterBase: number;
  /** Shared outbound hdr counter (§6.2) — ALL outbound frames draw from it. */
  outCounter: number;
  /** Session key: peer_base ^ own_base (§5.2). */
  sessionKey: number;
  /** Character id the peer announced (its handshake peer_id). */
  peerId: number;
  /** Peer id the host announced to this peer (its own character id). */
  hostPeerId: number;
  /** True once the session key has been established (state ≥ 8). */
  established: boolean;
  /** Last time this peer sent us anything (ms timestamp). */
  lastSeenAt: number;
  /**
   * Highest inbound frame seq processed (0 = none yet). Every newly observed
   * seq is acknowledged (type 0x1000 | seq); duplicates (seq ≤ this) are not
   * re-acked. Gaps self-heal: a lost frame returns as a re-send with a fresh
   * seq and is acked then.
   */
  lastInSeq: number;
  /**
   * Inbound ACK type ids (0x1000|seq) already seen — the joiner keeps
   * re-sending its ACK until it sees ours, so duplicates are expected and
   * must not re-trigger the same acknowledgement.
   */
  seenAcks: Set<number>;
}
