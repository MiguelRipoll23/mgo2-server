import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

@injectable()
@GameCommandHandler(0x4312)
export class GetGameDetailsHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const gameId = reader.readUint32();

    const game = gameId > 0 ? await this.gameService.findById(gameId) : null;

    const writer = new PacketWriter();
    writer.writeUint32(game?.id ?? 0);
    writer.writeFixedString(game?.name ?? "", 16);
    writer.writeFixedString(game?.comment ?? "", 128);
    writer.writeUint32(game?.host_id ?? 0);
    writer.writeUint32(game?.max_players ?? 0);
    writer.writeUint32(game?.status ?? 0);
    writer.writePadding(32);

    await sendPacket(session, 0x4313, writer.build());
  }
}
