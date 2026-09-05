import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

@injectable()
@GameCommandHandler(0x4380)
export class QuitGameHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const gameId = session.gameId;
    if (gameId !== null) {
      const game = await this.gameService.findById(gameId);
      if (game && game.host_id === session.characterId) {
        // Roster and round-snapshot rows cascade with the game.
        await this.gameService.delete(gameId);
      } else if (game) {
        await this.gameService.removePlayer(gameId, session.characterId ?? 0);
      }
      session.gameId = null;
    }
    await sendResult(session, 0x4381, RESULT_NONE);
  }
}
