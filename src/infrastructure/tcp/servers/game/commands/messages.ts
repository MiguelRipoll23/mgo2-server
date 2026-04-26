import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { injectable } from "@needle-di/core";
import { PacketReader } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket, sendStartEndPacket } from "../../../../../core/tcp/utils/session-helpers-util.ts";

const _MSG_TYPE_MAIL = 0x0f;
const MSG_TYPE_CLAN = 0x10;

// writeSendResponse with 0 errors: writeInt(0) + writeByte(0) + writeInt(0)
const SEND_SUCCESS_PAYLOAD = new Uint8Array([0, 0, 0, 0, 0, 0, 0, 0, 0]);

@injectable()
@GameCommandHandler(0x4800)
export class SendMessageHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4801, SEND_SUCCESS_PAYLOAD);
  }
}

@injectable()
@GameCommandHandler(0x4820)
export class GetMessagesHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const type = reader.readUint8();

    await sendStartEndPacket(session, 0x4821);

    if (type === MSG_TYPE_CLAN) {
      await sendStartEndPacket(session, 0x4822);
    }

    await sendStartEndPacket(session, 0x4823);
  }
}

@injectable()
@GameCommandHandler(0x4840)
export class GetMessageContentsHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendStartEndPacket(session, 0x4841);
  }
}

@injectable()
@GameCommandHandler(0x4860)
export class AddSentMessageHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendStartEndPacket(session, 0x4861);
  }
}
