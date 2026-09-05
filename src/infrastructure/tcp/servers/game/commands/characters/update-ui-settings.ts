import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

@injectable()
@GameCommandHandler(0x4112)
export class UpdateUiSettingsHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    // 0x4112 is a 32-byte opaque blob the client BLOCKS on (wait slot 0x18);
    // 0x4113 is a bare u32 result.
    await sendResult(session, 0x4113, RESULT_NONE);
  }
}
