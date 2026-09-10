import { injectable } from "@needle-di/core";
import type { IPeerCommandHandler, PeerContext } from "../../../core/udp/interfaces/peer-command-handler-interface.ts";
import { PeerCommandHandler } from "../../../core/udp/services/peer-command-registry-service.ts";
import { UDP_CMD_KEEP_ALIVE } from "../../../core/udp/constants/udp-commands-constants.ts";

/**
 * Mirrors a peer's keep-alive (type 0x5000) straight back — the same shape a
 * real host answers with (verified live: the joiner's state-6 pings expect a
 * matching pre-keyed echo). Keying follows session.established automatically.
 */
@injectable()
@PeerCommandHandler(UDP_CMD_KEEP_ALIVE)
export class AckKeepAliveHandler implements IPeerCommandHandler {
  async handle(context: PeerContext): Promise<void> {
    await context.send(UDP_CMD_KEEP_ALIVE, context.message.body);
  }
}
