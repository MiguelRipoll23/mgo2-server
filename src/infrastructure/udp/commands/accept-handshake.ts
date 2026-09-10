import { inject, injectable } from "@needle-di/core";
import type { IPeerCommandHandler, PeerContext } from "../../../core/udp/interfaces/peer-command-handler-interface.ts";
import { PeerCommandHandler } from "../../../core/udp/services/peer-command-registry-service.ts";
import { UDP_CMD_HANDSHAKE, UDP_CMD_KEEP_ALIVE } from "../../../core/udp/constants/udp-commands-constants.ts";
import { buildHandshakeBody, parseHandshakeBody } from "../../../core/udp/utils/frame-builder-util.ts";
import { HostIdentityService } from "../services/host-identity-service.ts";

/**
 * Accepts a joiner's p2p handshake (type 0x1000, 44-byte datagram).
 * Replies with the host's own handshake — stamped with the HOST's peer id and
 * counter base (§4 gate 1: the joiner gates on the host's id, NOT an echo) —
 * then sends one SESSION-keyed keep-alive: a frame whose tail digest verifies
 * with K ^ const flips the joiner's flags |= 9, and its accept pump then sets
 * state 8 (session key established, §6.1). Both frames draw from the session's
 * shared outbound counter (§6.2). `send` picks the keying per
 * `session.established`, so the reply goes out pre-keyed and the keep-alive
 * session-keyed.
 */
@injectable()
@PeerCommandHandler(UDP_CMD_HANDSHAKE)
export class AcceptHandshakeHandler implements IPeerCommandHandler {
  constructor(private identity = inject(HostIdentityService)) {}

  async handle(context: PeerContext): Promise<void> {
    const hs = parseHandshakeBody(context.message.body);
    if (!hs) {
      console.warn(
        `[udp:${context.localPort}] handshake from ${context.remote.hostname}:${context.remote.port} failed validation`,
      );
      return;
    }

    // Record the joiner's identity in its session (the provisioning pass ran
    // before the handshake body was parsed, so recompute the session key).
    context.session.peerId = hs.peerId;
    context.session.counterBase = hs.counterBase;
    context.session.sessionKey = (hs.counterBase ^ this.identity.counterBase) >>> 0;
    context.session.dialBack = {
      hostname: context.remote.hostname,
      port: context.remote.port,
    };

    console.info(
      `[udp:${context.localPort}] handshake from ${context.remote.hostname}:${context.remote.port} ` +
        `peer=0x${hs.peerId.toString(16).padStart(8, "0")} base=0x${hs.counterBase.toString(16).padStart(8, "0")} ` +
        `pairs=[${hs.pairs.map((p: { ip: string; port: number }) => `${p.ip}:${p.port}`).join(", ")}]`,
    );

    // 1. Our handshake reply (pre-keyed — the joiner is still in state < 8).
    const reply = buildHandshakeBody(
      this.identity.peerId,
      this.identity.counterBase,
      context.remote.hostname,
      context.localPort,
    );
    await context.send(UDP_CMD_HANDSHAKE, reply);

    // 2. The key-establishing session-keyed keep-alive (§6.1).
    context.session.established = true;
    await context.send(UDP_CMD_KEEP_ALIVE, new Uint8Array(0));
    console.info(
      `[udp:${context.localPort}] session with peer=0x${hs.peerId.toString(16).padStart(8, "0")} established`,
    );
  }
}
