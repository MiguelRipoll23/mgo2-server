import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult, sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// Match-history drill-down (0x4684 → 0x4685/0x4686/0x4687). The request is a
// single u32 entry id — the id field of a 0x4682 record. The 93-byte detail
// record is ELF-traced but unlabelled, so an empty list is the honest answer:
// "no details" is true, where invented rows would say something false in a
// way the player can read (the reference's fingerprint records once minted
// 17 unearned medals this way).
const GET_MATCH_DETAILS = 0x4684;

@injectable()
@GameCommandHandler(GET_MATCH_DETAILS)
export class GetMatchDetailsHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, 0x4685, RESULT_NONE);
    await sendStartEndPacket(session, 0x4686);
    await sendStartEndPacket(session, 0x4687);
  }
}
