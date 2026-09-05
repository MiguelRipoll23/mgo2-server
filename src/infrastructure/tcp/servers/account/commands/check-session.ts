import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { AccountCommandHandler } from "../../../../../core/tcp/decorators/account-command-handler-decorator.ts";
import { PacketReader } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { SessionsService } from "../../../../../modules/auth/sessions-service.ts";
import { sendResult } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { storedSessionFieldFromWire } from "../../../../../core/tcp/utils/crypto-util.ts";
import {
  RESULT_NONE,
  RESULT_INVALID_SESSION,
} from "../../../../../core/constants/error-codes-constants.ts";

// Field length of the check-session value the client derives from its login
// token. The payload is {u32 claimedAccountId, byte[16] sessionField}.
const SESSION_FIELD_LENGTH = 16;

@injectable()
@AccountCommandHandler(0x3003)
export class CheckSessionHandler implements ICommandHandler {
  constructor(private sessionsService = inject(SessionsService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length < 4 + SESSION_FIELD_LENGTH) {
      console.log(
        `[tcp][account] check session: payload too short (${packet.payload.length} bytes)`,
      );
      await sendResult(session, 0x3004, RESULT_INVALID_SESSION);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const claimedUserId = reader.readUint32();
    const sessionField = storedSessionFieldFromWire(reader.readBytes(SESSION_FIELD_LENGTH));

    // The client derived this from its login token, so the session row
    // already holds the same value and it is matched directly. Nothing is
    // decoded back into a token.
    const user = await this.sessionsService.findSessionByToken(sessionField);

    if (user === null) {
      console.log(
        `[tcp][account] check session: no account holds the presented session`,
      );
      await sendResult(session, 0x3004, RESULT_INVALID_SESSION);
      return;
    }

    // The session is real, but it has to belong to whoever the client says it
    // is; otherwise a leaked token would let any id be claimed.
    if (user.user_id !== claimedUserId) {
      console.log(
        `[tcp][account] check session: session belongs to user ${user.user_id}, client claimed ${claimedUserId}`,
      );
      await sendResult(session, 0x3004, RESULT_INVALID_SESSION);
      return;
    }

    // Entering an account lobby means choosing a character, so a stale
    // selection is meaningless here; the character choice arrives with the
    // game-lobby check-session.
    session.userId = user.user_id;
    session.characterId = null;

    await sendResult(session, 0x3004, RESULT_NONE);
  }
}
