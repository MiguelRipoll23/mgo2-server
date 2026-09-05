import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// The client sends 0x43c8 and parses 0x43c9. 0x43ca has no builder in the
// client — a handler registered there is never reached and start-round stalls.
const START_ROUND = 0x43c8;
const START_ROUND_RESULT = 0x43c9;

@injectable()
@GameCommandHandler(START_ROUND)
export class HostStartRoundHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    // Snapshot the roster: everyone in the game now "played this round",
    // which is what the attribution checks for the host's end-of-round
    // reports (0x4390/0x43a2/0x43a4) consult before applying stats to a
    // character — including one who quits mid-round.
    const gameId = session.gameId;
    if (gameId !== null) {
      await this.gameService.markRoundPlayers(gameId);
    }

    // Reply is {u32 result, u32 token}. The second word must be ZERO: the
    // client stores a nonzero value into profile+0x32F8 and republishes it to
    // every peer, where it gates the instructor-recognition prompt ("an
    // instructor is already saved"). It is not a round handle.
    const payload = new PacketWriter()
      .writeUint32(RESULT_NONE)
      .writeUint32(0)
      .build();
    await sendPacket(session, START_ROUND_RESULT, payload);
  }
}
