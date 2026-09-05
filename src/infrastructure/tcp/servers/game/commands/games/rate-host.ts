import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService, MAX_HOST_RATING, MIN_HOST_RATING } from "../../../../../../modules/game/game-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// A host-rating vote (0x43c4) — the end-of-game star picker, which the 0x4313
// rating gate armed. The vote comes from a PLAYER, not the host, so the game
// is resolved by membership rather than by who hosts. The rating applies to
// whoever hosts that game. Out-of-range values are dropped rather than
// clamped — the client will not send them, so one that arrives means the
// reading is wrong and should be visible.
//
// Payload: {u32 rating, 1..5}. Reply: {u32 result}.
@injectable()
@GameCommandHandler(0x43c4)
export class RateHostHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const voterId = session.characterId;

    if (voterId !== null && packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      const rating = reader.readUint32();

      if (rating < MIN_HOST_RATING || rating > MAX_HOST_RATING) {
        console.log(
          `[tcp][game] 0x43c4 host rating ${rating} is outside ${MIN_HOST_RATING}..${MAX_HOST_RATING} — dropped. The client rejects these too, so this means our reading of the command is wrong.`,
        );
      } else {
        const game = await this.gameService.gameContaining(voterId);
        if (game === null) {
          console.log(
            `[tcp][game] 0x43c4 host rating ${rating} from character ${voterId}, who is in no game; dropped.`,
          );
        } else {
          const recorded = await this.gameService.recordHostVote(
            game.id,
            game.host_id,
            voterId,
            rating,
          );
          if (recorded) {
            console.log(
              `[tcp][game] game ${game.id}: character ${voterId} rated host ${game.host_id} at ${rating} stars.`,
            );
          } else {
            // If this fires after the join gate went in, the gate and the
            // constraint disagree — worth seeing.
            console.log(
              `[tcp][game] game ${game.id}: character ${voterId} rated host at ${rating} stars, but a vote for this game is already recorded — discarded. The 0x4321 rating gate should have stopped the client offering this.`,
            );
          }
        }
      }
    }

    await sendResult(session, 0x43c5, RESULT_NONE);
  }
}
