import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

import { injectable } from "@needle-di/core";

@injectable()
@GameCommandHandler(0x4344)
export class HostSetPlayerTeamHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4345, null);
  }
}
