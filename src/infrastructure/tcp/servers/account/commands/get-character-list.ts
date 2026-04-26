import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { AccountCommandHandler } from "../../../../../core/tcp/decorators/account-command-handler-decorator.ts";
import { PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";
import { UserService } from "../../../../../modules/user/user-service.ts";
import { sendPacket, sendError } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { ERROR_INVALID_SESSION } from "../../../../../core/constants/error-codes-constants.ts";
import type {
  Character,
  CharacterAppearance,
} from "../../../../../db/schema.ts";

const CHARACTER_NAME_LENGTH = 16;
const RESPONSE_SUFFIX = new Uint8Array([
  0x00, 0x00, 0x00, 0x00, 0x07, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
]);

@injectable()
@AccountCommandHandler(0x3048)
export class GetCharacterListHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private userService = inject(UserService),
  ) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    if (session.userId === null) {
      await sendError(session, 0x3049, ERROR_INVALID_SESSION);
      return;
    }

    const user = await this.userService.findById(session.userId);
    if (user === null) {
      await sendError(session, 0x3049, ERROR_INVALID_SESSION);
      return;
    }

    const allCharacters = await this.characterService.findByUserId(
      session.userId,
    );
    const mainCharacterId = user.main_character_id;

    const sortedCharacters: Character[] = [
      ...allCharacters.filter((character) => character.id === mainCharacterId),
      ...allCharacters.filter((character) => character.id !== mainCharacterId),
    ];

    const writer = new PacketWriter();
    writer.writeUint32(0);
    writer.writeUint8(user.slots);
    writer.writeUint8(sortedCharacters.length);
    writer.writeUint8(0);

    for (let index = 0; index < sortedCharacters.length; index++) {
      const character = sortedCharacters[index];
      const appearance = await this.characterService.getAppearance(
        character.id,
      );
      const isMain = character.id === mainCharacterId;
      this.writeCharacterEntry(
        writer,
        character,
        appearance,
        index === 0,
        isMain,
      );
    }

    const bodyBytes = writer.build();
    const fullResponse = new Uint8Array(0x1b4 + RESPONSE_SUFFIX.length);
    fullResponse.set(bodyBytes.subarray(0, 0x1b4));
    fullResponse.set(RESPONSE_SUFFIX, 0x1b4);

    await sendPacket(session, 0x3049, fullResponse);
  }

  private writeCharacterEntry(
    writer: PacketWriter,
    character: Character,
    appearance: CharacterAppearance | null,
    isFirst: boolean,
    _isMain: boolean,
  ): void {
    const displayName = character.name;

    if (isFirst) {
      writer.writeFixedString(displayName, CHARACTER_NAME_LENGTH);
      writer.writeUint8(0);
    } else {
      writer.writeUint32(0);
    }

    writer.writeUint32(character.id);
    writer.writeFixedString(displayName, CHARACTER_NAME_LENGTH);

    writer.writeUint8(appearance?.gender ?? 0);
    writer.writeUint8(appearance?.face ?? 0);
    writer.writeUint8(appearance?.upper ?? 0);
    writer.writeUint8(appearance?.lower ?? 0);
    writer.writeUint8(appearance?.face_paint ?? 0);
    writer.writeUint8(appearance?.upper_color ?? 0);
    writer.writeUint8(appearance?.lower_color ?? 0);
    writer.writeUint8(appearance?.voice ?? 0);
    writer.writeUint8(appearance?.pitch ?? 0);
    writer.writePadding(4);
    writer.writeUint8(appearance?.head ?? 0);
    writer.writeUint8(appearance?.chest ?? 0);
    writer.writeUint8(appearance?.hands ?? 0);
    writer.writeUint8(appearance?.waist ?? 0);
    writer.writeUint8(appearance?.feet ?? 0);
    writer.writeUint8(appearance?.accessory1 ?? 0);
    writer.writeUint8(appearance?.accessory2 ?? 0);
    writer.writeUint8(appearance?.head_color ?? 0);
    writer.writeUint8(appearance?.chest_color ?? 0);
    writer.writeUint8(appearance?.hands_color ?? 0);
    writer.writeUint8(appearance?.waist_color ?? 0);
    writer.writeUint8(appearance?.feet_color ?? 0);
    writer.writeUint8(appearance?.accessory1_color ?? 0);
    writer.writeUint8(appearance?.accessory2_color ?? 0);
    writer.writeUint8(0);
  }
}
