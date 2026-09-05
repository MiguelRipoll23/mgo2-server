import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult, sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

@injectable()
@GameCommandHandler(0x4680)
export class GetMatchHistoryHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    // START carries a RESULT code — sending the entry count there made the
    // client read the count as a failure code ("Unable to acquire Friend
    // List.(1002:<count>)" was exactly this mistake on 0x4581).
    await sendResult(session, 0x4681, RESULT_NONE);
    await sendStartEndPacket(session, 0x4682);
    await sendStartEndPacket(session, 0x4683);
  }
}
