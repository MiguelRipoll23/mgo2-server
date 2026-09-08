import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import {
  buildGameplayOptionsPayload,
} from "./gameplay-options-codec.ts";

function parseStoredOptions(json: string): Record<string, unknown> {
  try {
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return {};
  }
}

/**
 * Builds the stored (or default) 0x4120 payload for a character. Shared with
 * the connect burst: sending an empty or flag-less 0x4120 there makes the
 * client's validator reset every gameplay setting to its hardcoded default,
 * which is exactly the "settings are not saved" symptom.
 */
export async function loadGameplayOptionsPayload(
  characterService: CharacterService,
  characterId: number | null,
): Promise<Uint8Array> {
  if (characterId === null) {
    return buildGameplayOptionsPayload({});
  }
  const character = await characterService.findById(characterId);
  const stored = character?.gameplay_options
    ? parseStoredOptions(character.gameplay_options)
    : {};
  return buildGameplayOptionsPayload(stored);
}

@injectable()
@GameCommandHandler(0x411b)
export class GetGameplayOptionsUiSettingsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendPacket(session, 0x4120, buildGameplayOptionsPayload({}));
      return;
    }

    const character = await this.characterService.findById(characterId);
    const stored = character?.gameplay_options
      ? parseStoredOptions(character.gameplay_options)
      : {};
    // 0x150 bytes exactly: 48-byte header, four 64-byte codec names, the
    // 32-byte list-preferences trailer (all zeros — see the codec).
    const payload = buildGameplayOptionsPayload(stored);
    await sendPacket(session, 0x4120, payload);
  }
}
