import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { injectable } from "@needle-di/core";
import { PacketReader, PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket, sendResult } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../core/constants/error-codes-constants.ts";

const _MSG_TYPE_MAIL = 0x0f;
const MSG_TYPE_CLAN = 0x10;

// 0x4801: {u32 status, u8 flags}. Bit 0 of the flags byte MUST be set — the
// parser tests only bit 0; clear, it re-sends the entire letter as a 969-byte
// 0x4860 and re-waits, so a zero here starts a second request we would also
// have to answer. (Nomad's flags=0 works only because it answers 0x4860.)
const SEND_SUCCESS_PAYLOAD = new PacketWriter()
  .writeUint32(RESULT_NONE)
  .writeUint8(0x01)
  .build();

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
    const type = reader.remaining() > 0 ? reader.readUint8() : 0x0f;

    // Both start and end carry a result code, not a count — the client counts
    // the entry records itself.
    await sendResult(session, 0x4821, RESULT_NONE);

    if (type === MSG_TYPE_CLAN) {
      await sendResult(session, 0x4822, RESULT_NONE);
    }

    await sendResult(session, 0x4823, RESULT_NONE);
  }
}

// 0x4841 is {u32 result} + the 708-byte body block = 712 bytes. The body
// reader bound-checks the 1023-byte receive buffer rather than the payload,
// so a SHORT reply copies stale buffer into the mail object and reports
// success with garbage — the dangerous failure, not a benign one. (We have no
// mail store yet; the zeroed body renders as an empty letter, which is honest.)
const READ_BODY_LENGTH = 708;

@injectable()
@GameCommandHandler(0x4840)
export class GetMessageContentsHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const writer = new PacketWriter()
      .writeUint32(RESULT_NONE)
      .writePadding(READ_BODY_LENGTH);
    await sendPacket(session, 0x4841, writer.build());
  }
}

@injectable()
@GameCommandHandler(0x4860)
export class AddSentMessageHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, 0x4861, RESULT_NONE);
  }
}

@injectable()
@GameCommandHandler(0x4880)
export class DeleteMessageHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, 0x4881, RESULT_NONE);
  }
}
