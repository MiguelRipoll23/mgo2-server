import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { AccountCommandHandler } from "../../../../../core/tcp/decorators/account-command-handler-decorator.ts";
import { PacketReader, PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { isValidName } from "../../../../../core/tcp/utils/string-util.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";
import { UserService } from "../../../../../modules/user/user-service.ts";
import { sendPacket, sendError } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import {
  ERROR_GENERAL,
  ERROR_INVALID_SESSION,
  ERROR_CHAR_NAMETAKEN,
  ERROR_CHAR_NAMEINVALID,
  ERROR_CHAR_NAMEPREFIX,
  ERROR_CHAR_NAMERESERVED,
} from "../../../../../core/constants/error-codes-constants.ts";

const CHARACTER_NAME_LENGTH = 16;
const RESERVED_PREFIXES = [":#", "GM_", "GM-", "GM.", "GM,"];
const RESERVED_NAMES = ["SaveMGO"];

function hasReservedPrefix(name: string): boolean {
  return RESERVED_PREFIXES.some((prefix) => name.startsWith(prefix));
}

function isReservedName(name: string): boolean {
  return RESERVED_NAMES.includes(name);
}

@injectable()
@AccountCommandHandler(0x3101)
export class CreateCharacterHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private userService = inject(UserService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (session.userId === null) {
      await sendError(session, 0x3102, ERROR_INVALID_SESSION);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const name = reader.readFixedString(CHARACTER_NAME_LENGTH);
    const gender = reader.readUint8();
    const face = reader.readUint8();
    const upper = reader.readUint8();
    const lower = reader.readUint8();
    const facePaint = reader.readUint8();
    const upperColor = reader.readUint8();
    const lowerColor = reader.readUint8();
    const voice = reader.readUint8();
    const pitch = reader.readUint8();
    reader.skip(4);
    const head = reader.readUint8();
    const chest = reader.readUint8();
    const hands = reader.readUint8();
    const waist = reader.readUint8();
    const feet = reader.readUint8();
    const accessory1 = reader.readUint8();
    const accessory2 = reader.readUint8();
    const headColor = reader.readUint8();
    const chestColor = reader.readUint8();
    const handsColor = reader.readUint8();
    const waistColor = reader.readUint8();
    const feetColor = reader.readUint8();
    const accessory1Color = reader.readUint8();
    const accessory2Color = reader.readUint8();

    if (!isValidName(name)) {
      await sendError(session, 0x3102, ERROR_CHAR_NAMEINVALID);
      return;
    }

    if (hasReservedPrefix(name)) {
      await sendError(session, 0x3102, ERROR_CHAR_NAMEPREFIX);
      return;
    }

    if (isReservedName(name)) {
      await sendError(session, 0x3102, ERROR_CHAR_NAMERESERVED);
      return;
    }

    const existingCharacter = await this.characterService.findByName(name);
    if (existingCharacter !== null) {
      await sendError(session, 0x3102, ERROR_CHAR_NAMETAKEN);
      return;
    }

    const user = await this.userService.findById(session.userId);
    if (user === null) {
      await sendError(session, 0x3102, ERROR_INVALID_SESSION);
      return;
    }

    const existingCharacters = await this.characterService.findByUserId(
      session.userId,
    );
    if (existingCharacters.length >= user.slots) {
      await sendError(session, 0x3102, ERROR_GENERAL);
      return;
    }

    const creationTime = Math.floor(Date.now() / 1000);
    const newCharacter = await this.characterService.create(
      { user_id: session.userId, name, creation_time: creationTime },
      {
        gender,
        face,
        voice,
        pitch,
        head,
        head_color: headColor,
        upper,
        upper_color: upperColor,
        lower,
        lower_color: lowerColor,
        chest,
        chest_color: chestColor,
        waist,
        waist_color: waistColor,
        hands,
        hands_color: handsColor,
        feet,
        feet_color: feetColor,
        accessory1,
        accessory1_color: accessory1Color,
        accessory2,
        accessory2_color: accessory2Color,
        face_paint: facePaint,
      },
    );

    const responseWriter = new PacketWriter();
    responseWriter.writePadding(4);
    responseWriter.writeUint32(newCharacter.id);

    await sendPacket(session, 0x3102, responseWriter.build());
  }
}
