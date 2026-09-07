import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// A client settings write pushed despite the 0x43xx host range: the sender
// (ELF 0x27E0E4, per the Java reference) puts client setting id 332's raw u32
// value straight on the wire — observed live carrying 3 after a training
// graduation. What setting 332 means is unknown, so nothing is persisted; the
// reply (0x43a7) is a bare u32 result and nothing else — its parser (0xD3FFE0)
// reads one word and signals request-status 48 with it, taking no other branch.
const PUT_CLIENT_SETTING = 0x43a6;
const PUT_CLIENT_SETTING_RESULT = 0x43a7;

@injectable()
@GameCommandHandler(PUT_CLIENT_SETTING)
export class HostPutClientSettingHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, PUT_CLIENT_SETTING_RESULT, RESULT_NONE);
  }
}
