import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";
import { parseGameplayOptionsPayload } from "./gameplay-options-codec.ts";

@injectable()
@GameCommandHandler(0x4110)
export class UpdateGameplayOptionsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    const options = parseGameplayOptionsPayload(packet.payload);

    if (characterId !== null && options !== null) {
      // Persist as JSON in the character's existing gameplay_options column;
      // 0x4120 reads the same column back. The layout is NOT guessed: it is
      // the 0x4120 payload truncated at 0x130 (48-byte settings header + four
      // 64-byte codec names), per the client's builder at 0xD3BFC0.
      await this.characterService.updateGameplayOptions(
        characterId,
        JSON.stringify(options),
      );
    }
    // The reply must carry u32 result == 0 or the enter-game flow stalls.
    await sendResult(session, 0x4111, RESULT_NONE);
  }
}
