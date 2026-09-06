import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket, sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// Host settings are stored as a single current blob per character (keyed by a
// fixed type). The reference keys per (character, lobby subtype); one row is
// enough here because reads only ever touch HOST_SETTINGS_TYPE, so stale rows
// written by older builds are never picked up.
export const HOST_SETTINGS_TYPE = 0;

/**
 * 0x4305 carries NO result word — it is the host's saved settings block, read
 * by the Create Game screen. Valid shapes only:
 *   - empty:     128 zero bytes  (character never pushed settings)
 *   - populated: 0x15C = 348 bytes, the request blob re-mapped into the reply
 *     layout (see buildHostSettingsReply)
 * Anything else is parsed as a settings block out of stale receive buffer and
 * breaks the screen, so a variable echo of the stored blob is never sent.
 */
const HOST_SETTINGS_EMPTY_SIZE = 128;
const HOST_SETTINGS_REPLY_SIZE = 0x15c;
/** Longest request-blob offset the reply mapping reads, plus one. */
const HOST_SETTINGS_MIN_BLOB = 0x156;

/** Decodes the stored JSON byte array back into raw bytes; null on garbage. */
export function decodeHostSettingsBytes(settingsJson: string): Uint8Array | null {
  try {
    const raw = JSON.parse(settingsJson) as unknown;
    return Array.isArray(raw) ? Uint8Array.from(raw as number[]) : null;
  } catch {
    return null;
  }
}

@injectable()
@GameCommandHandler(0x4304)
export class GetHostSettingsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    let blob: Uint8Array | null = null;
    if (characterId !== null) {
      const settingsRows = await this.characterService.getHostSettings(
        characterId,
      );
      const saved = settingsRows.find((row) => row.type === HOST_SETTINGS_TYPE);
      if (saved !== undefined) {
        blob = decodeHostSettingsBytes(saved.settings);
      }
    }

    let payload: Uint8Array;
    if (blob !== null && blob.length >= HOST_SETTINGS_MIN_BLOB) {
      payload = buildHostSettingsReply(blob);
    } else {
      payload = new Uint8Array(HOST_SETTINGS_EMPTY_SIZE);
    }

    await sendPacket(session, 0x4305, payload);
  }
}

@injectable()
@GameCommandHandler(0x4310)
export class CheckHostSettingsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length > 0) {
      // The blob IS the whole payload — the 16-byte game name starts at
      // offset 0. (An earlier build read the name's first four bytes as a
      // "settings type" and dropped them, shifting every field.)
      const settingsJson = JSON.stringify(Array.from(packet.payload));
      await this.characterService.updateHostSettings(
        characterId,
        HOST_SETTINGS_TYPE,
        settingsJson,
      );
    }
    await sendResult(session, 0x4311, RESULT_NONE);
  }
}

/**
 * Re-maps the stored request-shaped 0x4310 blob into the 0x4305 reply layout.
 * Transcribed from the reference server's HostSettingsReply (live-verified
 * against a real client): the reply drops the request's subtype byte, re-bases
 * every offset and keeps its first u32 zeroed. Each copy is clamped so a
 * short blob leaves the destination zeroed instead of reading past the end.
 */
function buildHostSettingsReply(blob: Uint8Array): Uint8Array {
  const out = new Uint8Array(HOST_SETTINGS_REPLY_SIZE);
  copyField(out, 0x004, blob, 0x00, 0x10); // name
  copyField(out, 0x014, blob, 0x10, 0x80); // comment
  copyField(out, 0x094, blob, 0x90, 0x11); // password: enabled flag + password[15]
  copyField(out, 0x0a5, blob, 0xa1, 1); // dedicated
  copyField(out, 0x0a6, blob, 0xa3, 0x30); // rotation: 16 {rule, map, flags}
  copyField(out, 0x0d8, blob, 0xd5, 0x10); // weapon restrictions
  copyField(out, 0x0e8, blob, 0xe5, 1); // max players
  copyField(out, 0x0e9, blob, 0xe6, 4); // briefing time
  copyField(out, 0x0ed, blob, 0xea, 4); // echoed, not a constant
  copyField(out, 0x0f9, blob, 0xf6, 1); // stance
  copyField(out, 0x0fa, blob, 0xf7, 1); // level-limit tolerance
  copyField(out, 0x0fb, blob, 0xf8, 4); // level-limit base
  copyField(out, 0x0ff, blob, 0xfc, 17 * 4); // per-rule timers/rounds/tickets
  copyField(out, 0x143, blob, 0x140, 2); // unique characters red/blue
  copyField(out, 0x145, blob, 0x142, 1); // commonA
  copyField(out, 0x146, blob, 0x143, 1); // commonB
  copyField(out, 0x147, blob, 0x144, 1); // low byte of the flags word
  copyField(out, 0x148, blob, 0x145, 2); // idle kick (u16)
  copyField(out, 0x14a, blob, 0x147, 2); // team-kill kick (u16)
  copyField(out, 0x14c, blob, 0x149, 1); // capture extra time
  copyField(out, 0x14d, blob, 0x14a, 1); // SNAKE: times Snake must be defeated
  copyField(out, 0x14e, blob, 0x14b, 8); // sdm t/r, int t, dm r, scap t/r, race t/r
  copyField(out, 0x157, blob, 0x154, 1); // extra-time flags
  copyField(out, 0x158, blob, 0x155, 1); // host options
  return out;
}

/** Copies what the blob actually holds; a short blob leaves the destination zeroed. */
function copyField(
  out: Uint8Array,
  dst: number,
  blob: Uint8Array,
  src: number,
  length: number,
): void {
  const available = Math.max(0, Math.min(length, blob.length - src));
  if (available > 0) {
    out.set(blob.subarray(src, src + available), dst);
  }
}
