import type { UdpMessage } from "../types/udp-type.ts";
import type { PeerSession } from "../types/udp-type.ts";

/**
 * Context handed to a peer command handler for one inbound message.
 * `send` writes a message back to the peer through the session's shared
 * outbound counter (the only counter the joiner's seq window accepts);
 * frames are keyed pre-session or session-keyed per `session.established`.
 */
export interface PeerContext {
  readonly session: PeerSession;
  readonly message: UdpMessage;
  readonly remote: { hostname: string; port: number };
  /** The UDP port this host is listening on (for endpoint advertisement). */
  readonly localPort: number;
  send(messageType: number, body: Uint8Array): Promise<void>;
}

/** A UDP peer message handler, the p2p analogue of ICommandHandler. */
export interface IPeerCommandHandler {
  handle(context: PeerContext): Promise<void>;
}
