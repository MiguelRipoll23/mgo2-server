import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { AccountCommandHandler } from "../../../../../core/tcp/decorators/account-command-handler-decorator.ts";
import { PacketReader } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";
import { UserService } from "../../../../../modules/user/user-service.ts";
import { sendStartEndPacket, sendError } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { ERROR_INVALID_SESSION } from "../../../../../core/constants/error-codes-constants.ts";

@injectable()
@AccountCommandHandler(0x3103)
export class SelectCharacterHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private userService = inject(UserService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (session.userId === null) {
      await sendError(session, 0x3104, ERROR_INVALID_SESSION);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const slotIndex = reader.readUint8();

    const user = await this.userService.findById(session.userId);
    if (user === null) {
      await sendError(session, 0x3104, ERROR_INVALID_SESSION);
      return;
    }

    const allCharacters = await this.characterService.findByUserId(
      session.userId,
    );
    const mainCharacterId = user.main_character_id;
    const sortedCharacters = [
      ...allCharacters.filter((character) => character.id === mainCharacterId),
      ...allCharacters.filter((character) => character.id !== mainCharacterId),
    ];

    if (slotIndex >= sortedCharacters.length) {
      await sendError(session, 0x3104, ERROR_INVALID_SESSION);
      return;
    }

    const selectedCharacter = sortedCharacters[slotIndex];
    session.characterId = selectedCharacter.id;

    await sendStartEndPacket(session, 0x3104);
  }
}
