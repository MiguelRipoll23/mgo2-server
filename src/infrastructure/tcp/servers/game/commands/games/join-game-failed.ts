import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// Join failed (0x4322): the joiner could not establish the peer-to-peer
// connection to the host it was handed by 0x4321, and is backing out. Empty
// request; the reply parser reads only a u32 result, so an acknowledgement is
// all it needs.
//
// Unanswered, the client hangs on the loader and eventually fails
// 0B08:FFFFFF60 — the spinner is it waiting for this reply. The joiner is
// removed from the game they never managed to enter so the roster stays honest.
@injectable()
@GameCommandHandler(0x4322)
export class JoinGameFailedHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null) {
      const game = await this.gameService.gameContaining(characterId);
      if (game !== null) {
        await this.gameService.removePlayer(game.id, characterId);
        console.log(
          `[tcp][game] character ${characterId} failed to join game ${game.id} (peer connection never formed); removed from roster`,
        );
      }
    }

    await sendResult(session, 0x4323, RESULT_NONE);
  }
}