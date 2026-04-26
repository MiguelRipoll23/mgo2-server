import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

@injectable()
@GameCommandHandler(0x4680)
export class GetMatchHistoryHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendStartEndPacket(session, 0x4681);
    await sendStartEndPacket(session, 0x4682);
    await sendStartEndPacket(session, 0x4683);
  }
}
