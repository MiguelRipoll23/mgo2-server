import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket, sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const GAME_ELEMENT_SIZE = 64;

@injectable()
@GameCommandHandler(0x4300)
export class GetGameListHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const lobbyId = session.lobbyId ?? 0;
    const games =
      lobbyId > 0 ? await this.gameService.findByLobby(lobbyId) : [];

    await sendStartEndPacket(session, 0x4301);

    for (const game of games) {
      const writer = new PacketWriter();
      writer.writeUint32(game.id);
      writer.writeFixedString(game.name, 16);
      writer.writeUint32(game.host_id);
      writer.writeUint32(game.max_players);
      writer.writeUint32(game.status);
      writer.writePadding(GAME_ELEMENT_SIZE - writer.size);
      await sendPacket(session, 0x4302, writer.build());
    }

    await sendStartEndPacket(session, 0x4303);
  }
}
