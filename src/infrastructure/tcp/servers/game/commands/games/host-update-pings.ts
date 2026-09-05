import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// The host reports round-trip times for everyone in its game:
// {u32 hostPing, then {u32 charaId, u32 ping} pairs}. Its own ping goes onto
// the game row (the browser shows it), each player's onto their roster row
// (the player list shows it). There is NO count field — the loop runs to
// end-of-stream; 0x43a4, which does carry one, must not be confused with it.
// A zero character id is skipped.
@injectable()
@GameCommandHandler(0x4398)
export class HostUpdatePingsHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const gameId = session.gameId;
    const game = gameId !== null ? await this.gameService.findById(gameId) : null;

    if (game !== null && packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      const hostPing = reader.readUint32();
      const playerPings = new Map<number, number>();
      while (reader.remaining() >= 8) {
        const characterId = reader.readUint32();
        const ping = reader.readUint32();
        if (characterId !== 0) {
          playerPings.set(characterId, ping);
        }
      }
      await this.gameService.updatePings(game.id, hostPing, playerPings);
    }

    // Explicit 4-byte zero, not an empty payload: the parser reads a u32
    // unconditionally and hands it to the waiting request slot; an empty
    // payload only "worked" because the read primitives bound-check the
    // 1023-byte receive buffer rather than the payload length, so the client
    // consumed four bytes of stale buffer as its result code.
    await sendResult(session, 0x4399, RESULT_NONE);
  }
}
