import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// The host cycling the rotation while staging (0x4392 → 0x4393). Request is a
// single byte: the index into the rotation pushed via 0x4310. The rotation is
// stored on the game row as the `games` JSON array of [rule, map, flags]
// tuples; the index resolves into it so the browser reflects the round being
// staged. Sixteen entries, not fifteen — the 0x4310 writer emits sixteen
// triples and the parsers read sixteen.
const ROTATION_ROUNDS = 16;

@injectable()
@GameCommandHandler(0x4392)
export class HostSetGameHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const gameId = session.gameId;
    const game = gameId !== null ? await this.gameService.findById(gameId) : null;

    if (game !== null && packet.payload.length >= 1) {
      const index = packet.payload[0];
      let rotation: number[][] = [];
      try {
        const parsed: unknown = JSON.parse(game.games);
        if (Array.isArray(parsed)) rotation = parsed as number[][];
      } catch {
        rotation = [];
      }

      if (index < ROTATION_ROUNDS && index < rotation.length) {
        await this.gameService.update(game.id, { current_game: index });
      }
      // An index with no stored rotation entry is ignored, not an error —
      // the reply still goes out and the client proceeds.
    }

    await sendResult(session, 0x4393, RESULT_NONE);
  }
}
