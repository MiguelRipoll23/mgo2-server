import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

@injectable()
@GameCommandHandler(0x4316)
export class CreateGameHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    const lobbyId = session.lobbyId;

    if (characterId === null || lobbyId === null) {
      await sendPacket(session, 0x4317, null);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const name = reader.readFixedString(16);
    const password = reader.readFixedString(15);
    const comment = reader.readFixedString(128);
    const maxPlayers = reader.remaining() >= 4 ? reader.readUint32() : 8;

    const game = await this.gameService.create({
      host_id: characterId,
      lobby_id: lobbyId,
      name: name || `Game_${characterId}`,
      password,
      comment,
      max_players: maxPlayers,
    });

    session.gameId = game.id;

    const writer = new PacketWriter().writeUint32(game.id);
    await sendPacket(session, 0x4317, writer.build());
  }
}
