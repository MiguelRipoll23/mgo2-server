import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// HOST MIGRATION (0x43a0 → 0x43a1) — the host quitting, not a player handing
// the game on. There is exactly one builder in the client, called from the
// host's leave-game routine; its three callers are UI leave handlers that ask
// "am I the host" and branch: the host sends this, everyone else sends 0x4380.
// They are mutually exclusive, so a migrating host does not also send a quit.
//
// The successor is elected by the client, silently: it walks the roster
// reading each player's connection-quality property and keeps the maximum,
// with a laxer pass that drops the liveness check. The player is never asked.
//
// Payload is {sender's own chara id, elected successor's chara id}. The first
// word is ignored — the session already identifies the sender.
//
// The game is re-keyed to the target and the sender leaves the roster; joins
// pick up the new host's endpoint automatically because 0x4320 reads the
// connection by the game's host id at join time.
@injectable()
@GameCommandHandler(0x43a0)
export class PassRoundHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const gameId = session.gameId;
    const characterId = session.characterId;
    const game = gameId !== null ? await this.gameService.findById(gameId) : null;

    if (
      game !== null && characterId !== null && game.host_id === characterId &&
      packet.payload.length >= 8
    ) {
      const targetId = new DataView(
        packet.payload.buffer,
        packet.payload.byteOffset,
      ).getUint32(4, false);

      const roster = await this.gameService.getPlayers(game.id, game.host_id);
      const isPlayer = roster.some((id) => id === targetId);

      if (isPlayer && targetId !== game.host_id) {
        // Re-key: the successor becomes host, the quitter leaves the roster.
        await this.gameService.update(game.id, { host_id: targetId });
        await this.gameService.removePlayer(game.id, characterId);
        if (session.gameId !== null) session.gameId = game.id;
      } else {
        // THE SUCCESSOR IS ALREADY GONE: the client's fallback pass can
        // nominate a roster entry that has since left. Dropping the command
        // would leave the game keyed to a host who just quit — a ghost row.
        // The sender has left either way, so tear down as an ordinary quit.
        await this.gameService.delete(game.id);
        session.gameId = null;
      }
    }

    await sendResult(session, 0x43a1, RESULT_NONE);
  }
}

@injectable()
@GameCommandHandler(0x4348)
export class HostPassHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, 0x4349, RESULT_NONE);
  }
}
