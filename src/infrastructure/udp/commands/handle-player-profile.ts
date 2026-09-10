import { injectable } from "@needle-di/core";
import type { IPeerCommandHandler, PeerContext } from "../../../core/udp/interfaces/peer-command-handler-interface.ts";
import { PeerCommandHandler } from "../../../core/udp/services/peer-command-registry-service.ts";
import { UDP_CMD_PLAYER_PROFILE } from "../../../core/udp/constants/udp-commands-constants.ts";
import { u16LE } from "../../../core/udp/utils/binary-util.ts";

/** Body offset of the NUL-terminated account name in the profile record (§6.2). */
const NAME_OFFSET = 0x51 - 4; // body starts after the 4-byte record header

/**
 * Handles the joiner's player-profile record (type 0x1001, the reliable
 * compressed-frame payload, §6.2). The record body carries the joiner's
 * character id at [0..2) and its NUL-terminated account name at 0x4d.
 * After the packet log line, prints the human-readable join line.
 */
@injectable()
@PeerCommandHandler(UDP_CMD_PLAYER_PROFILE)
export class PlayerProfileHandler implements IPeerCommandHandler {
  async handle(context: PeerContext): Promise<void> {
    const body = context.message.body;
    if (body.length < 2) return;

    const characterId = u16LE(body, 0);
    const name = readNullTerminatedString(body, NAME_OFFSET);

    console.info(
      `[udp:${context.localPort}] Character ${characterId}${name ? ` (${name})` : ""} attempting to join...`,
    );
    // The frame carrying this record is ACKed by the transport
    // (DedicatedHostService.ackInbound) — no handler-side reply needed.
    await Promise.resolve();
  }
}

function readNullTerminatedString(buf: Uint8Array, offset: number): string | null {
  let end = offset;
  while (end < buf.length && buf[end] !== 0x00) end++;
  if (end === offset || end >= buf.length) return null;
  let ok = true;
  const chars: string[] = [];
  for (let i = offset; i < end; i++) {
    const b = buf[i];
    if (b < 0x20 || b > 0x7e) ok = false; // ISO-8859-1 extras are legal but not expected here
    chars.push(String.fromCharCode(b));
  }
  return ok ? chars.join("") : null;
}
