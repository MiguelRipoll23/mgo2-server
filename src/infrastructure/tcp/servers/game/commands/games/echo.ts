import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

// 0x0001 echoes its payload back — part of the pre-lobby handshake, outside
// the lobby packet library's command space. Unanswered it stalls the client.
@injectable()
@GameCommandHandler(0x0001)
export class EchoHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    await sendPacket(session, 0x0001, packet.payload);
  }
}
