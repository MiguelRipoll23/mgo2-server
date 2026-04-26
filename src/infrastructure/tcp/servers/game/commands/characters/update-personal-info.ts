import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const SKILL_EXP = 0x600000;

interface PersonalInfoUpdate {
  upper: number;
  lower: number;
  facePaint: number;
  upperColor: number;
  lowerColor: number;
  head: number;
  chest: number;
  hands: number;
  waist: number;
  feet: number;
  accessory1: number;
  accessory2: number;
  headColor: number;
  chestColor: number;
  handsColor: number;
  waistColor: number;
  feetColor: number;
  accessory1Color: number;
  accessory2Color: number;
  skill1: number;
  skill2: number;
  skill3: number;
  skill4: number;
  level1: number;
  level2: number;
  level3: number;
  level4: number;
  comment: string;
}

function readPersonalInfoUpdate(payload: Uint8Array): PersonalInfoUpdate {
  const reader = new PacketReader(payload);

  const update: PersonalInfoUpdate = {
    upper: reader.readUint8(),
    lower: reader.readUint8(),
    facePaint: reader.readUint8(),
    upperColor: reader.readUint8(),
    lowerColor: reader.readUint8(),
    head: reader.readUint8(),
    chest: reader.readUint8(),
    hands: reader.readUint8(),
    waist: reader.readUint8(),
    feet: reader.readUint8(),
    accessory1: reader.readUint8(),
    accessory2: reader.readUint8(),
    headColor: reader.readUint8(),
    chestColor: reader.readUint8(),
    handsColor: reader.readUint8(),
    waistColor: reader.readUint8(),
    feetColor: reader.readUint8(),
    accessory1Color: reader.readUint8(),
    accessory2Color: reader.readUint8(),
    skill1: reader.readInt8(),
    skill2: reader.readInt8(),
    skill3: reader.readInt8(),
    skill4: reader.readInt8(),
    level1: 0,
    level2: 0,
    level3: 0,
    level4: 0,
    comment: "",
  };

  reader.skip(1); // Skip 1 byte
  update.level1 = reader.readInt8();
  update.level2 = reader.readInt8();
  update.level3 = reader.readInt8();
  update.level4 = reader.readInt8();

  reader.skip(2); // Skip 2 bytes
  update.comment = reader.readFixedString(128);

  return update;
}

function writePersonalInfoUpdateResponse(
  update: PersonalInfoUpdate
): Uint8Array {
  const writer = new PacketWriter();

  writer.writePadding(4);
  writer.writeUint8(update.upper);
  writer.writeUint8(update.lower);
  writer.writeUint8(update.facePaint);
  writer.writeUint8(update.upperColor);
  writer.writeUint8(update.lowerColor);
  writer.writeUint8(update.head);
  writer.writeUint8(update.chest);
  writer.writeUint8(update.hands);
  writer.writeUint8(update.waist);
  writer.writeUint8(update.feet);
  writer.writeUint8(update.accessory1);
  writer.writeUint8(update.accessory2);
  writer.writeUint8(update.headColor);
  writer.writeUint8(update.chestColor);
  writer.writeUint8(update.handsColor);
  writer.writeUint8(update.waistColor);
  writer.writeUint8(update.feetColor);
  writer.writeUint8(update.accessory1Color);
  writer.writeUint8(update.accessory2Color);
  writer.writeInt8(update.skill1);
  writer.writeInt8(update.skill2);
  writer.writeInt8(update.skill3);
  writer.writeInt8(update.skill4);
  writer.writePadding(1);
  writer.writeInt8(update.level1);
  writer.writeInt8(update.level2);
  writer.writeInt8(update.level3);
  writer.writeInt8(update.level4);
  writer.writePadding(1);

  for (let i = 0; i < 4; i++) {
    writer.writeUint32(SKILL_EXP);
  }

  writer.writePadding(5);
  writer.writeFixedString(update.comment, 128);

  // 4-byte face paint color unlock bitmask (0xffffffff = all 32 colors unlocked)
  writer.writeUint32(0xffffffff);

  return writer.build();
}

@injectable()
@GameCommandHandler(0x4130)
export class UpdatePersonalInfoHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) { }

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;

    const update = readPersonalInfoUpdate(packet.payload);

    if (characterId !== null) {
      await this.characterService.updateAppearance(characterId, {
        face_paint: update.facePaint,
        upper: update.upper,
        lower: update.lower,
        upper_color: update.upperColor,
        lower_color: update.lowerColor,
        head: update.head,
        head_color: update.headColor,
        chest: update.chest,
        chest_color: update.chestColor,
        waist: update.waist,
        waist_color: update.waistColor,
        hands: update.hands,
        hands_color: update.handsColor,
        feet: update.feet,
        feet_color: update.feetColor,
        accessory1: update.accessory1,
        accessory1_color: update.accessory1Color,
        accessory2: update.accessory2,
        accessory2_color: update.accessory2Color,
      });
    }

    const response = writePersonalInfoUpdateResponse(update);
    await sendPacket(session, 0x4131, response);
  }
}
