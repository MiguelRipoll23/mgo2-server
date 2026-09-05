import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

const MACRO_TEXT_LENGTH = 64;

@injectable()
@GameCommandHandler(0x4114)
export class UpdateChatMacrosHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length > 0) {
      const reader = new PacketReader(packet.payload);
      const macros: Array<{ type: number; idx: number; text: string }> = [];

      while (reader.remaining() >= MACRO_TEXT_LENGTH + 2) {
        const type = reader.readUint8();
        const idx = reader.readUint8();
        const text = reader.readFixedString(MACRO_TEXT_LENGTH);
        macros.push({ type, idx, text });
      }

      await this.characterService.updateChatMacros(characterId, macros);
    }
    await sendResult(session, 0x4115, RESULT_NONE);
  }
}
