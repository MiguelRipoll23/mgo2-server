import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

import { injectable } from "@needle-di/core";

// Host migration — answered {result=0}.
@injectable()
@GameCommandHandler(0x43a0)
export class PassRoundHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
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
