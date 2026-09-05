import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { AccountCommandHandler } from "../../../../../core/tcp/decorators/account-command-handler-decorator.ts";
import { PacketReader } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";
import { sendResult } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import {
  RESULT_INVALID_SESSION,
  RESULT_GENERAL,
  RESULT_NONE,
  RESULT_NAME_INVALID,
} from "../../../../../core/constants/error-codes-constants.ts";

const NAME_LENGTH = 16;

// A pre-check that answers more leniently than the create it precedes would
// only move the failure one screen later, so the same taken-name rule that
// create-character applies is enforced here.
@injectable()
@AccountCommandHandler(0x3107)
export class CheckCharacterNameHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (session.userId === null) {
      await sendResult(session, 0x3108, RESULT_INVALID_SESSION);
      return;
    }

    if (packet.payload.length < NAME_LENGTH) {
      await sendResult(session, 0x3108, RESULT_GENERAL);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const name = reader.readFixedString(NAME_LENGTH).trim();

    if (name.length === 0) {
      await sendResult(session, 0x3108, RESULT_NAME_INVALID);
      return;
    }

    const taken = await this.characterService.findByName(name);
    await sendResult(session, 0x3108, taken !== null ? RESULT_NAME_TAKEN : RESULT_NONE);
  }
}

// Official code: CHARACTER_NAME_TAKEN(-260), unmasked.
const RESULT_NAME_TAKEN = 0xfffffefc;
