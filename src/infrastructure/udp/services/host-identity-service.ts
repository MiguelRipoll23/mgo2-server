import { injectable } from "@needle-di/core";
import { UDP_HOST_COUNTER_BASE, UDP_HOST_PEER_ID } from "../../../core/udp/constants/udp-host-identity-constants.ts";

/**
 * Identity the dedicated host presents on the p2p channel: the HOST's
 * character id (peers gate on it — §4 gate 1) and its counter base (feeds the
 * session key K = peer_base ^ host_base).
 */
@injectable()
export class HostIdentityService {
  readonly peerId = UDP_HOST_PEER_ID;
  readonly counterBase = UDP_HOST_COUNTER_BASE;
}
