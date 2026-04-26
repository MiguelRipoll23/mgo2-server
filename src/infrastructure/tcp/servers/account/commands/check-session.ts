import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { AccountCommandHandler } from "../../../../../core/tcp/decorators/account-command-handler-decorator.ts";
import { PacketReader } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { SessionsService } from "../../../../../modules/auth/sessions-service.ts";
import { sendStartEndPacket, sendError } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { ERROR_INVALID_SESSION } from "../../../../../core/constants/error-codes-constants.ts";
import { XOR_SESSION_ID_BYTES } from "../../../../../core/constants/crypto-keys-constants.ts";
import {
  xorBufferWithKey,
  encryptAuthPayload,
} from "../../../../../core/tcp/utils/crypto-util.ts";

@injectable()
@AccountCommandHandler(0x3003)
export class CheckSessionHandler implements ICommandHandler {
  constructor(private sessionsService = inject(SessionsService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const _characterId = reader.readUint32();
    const rawSessionBytes = reader.readBytes(8);
    const _unknown = reader.readBytes(8);

    xorBufferWithKey(rawSessionBytes, XOR_SESSION_ID_BYTES);
    encryptAuthPayload(rawSessionBytes);

    const sessionToken = new TextDecoder("iso-8859-1").decode(rawSessionBytes);
    const user = await this.sessionsService.findSessionByToken(sessionToken);

    if (user === null) {
      console.log(`[tcp][account] session not found: ${sessionToken}`);
      await sendError(session, 0x3004, ERROR_INVALID_SESSION);
      return;
    }

    session.userId = user.user_id;

    await sendStartEndPacket(session, 0x3004);
  }
}
