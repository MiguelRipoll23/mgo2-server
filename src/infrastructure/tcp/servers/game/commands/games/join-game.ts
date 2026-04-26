import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const JOIN_RESPONSE_SIZE = 32;

@injectable()
@GameCommandHandler(0x4320)
export class JoinGameHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const gameId = reader.readUint32();

    const game = gameId > 0 ? await this.gameService.findById(gameId) : null;

    if (!game) {
      await sendPacket(session, 0x4321, null);
      return;
    }

    session.gameId = game.id;

    const writer = new PacketWriter();
    writer.writeUint32(game.id);
    writer.writeUint32(game.host_id);
    writer.writePadding(JOIN_RESPONSE_SIZE - 8);

    await sendPacket(session, 0x4321, writer.build());
  }
}
