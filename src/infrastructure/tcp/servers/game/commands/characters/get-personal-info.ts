import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

// 25-byte fixed block that follows the clan info header (matches ref BYTES_1)
const PERSONAL_INFO_FIXED_BYTES = new Uint8Array([
  0x01, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
]);

const PERSONAL_INFO_TRAILER = new Uint8Array([0x00, 0xa7, 0x00, 0x0d]);

const DEFAULT_SKILL_EXP = 0x600000;

export async function buildPersonalInfoPayload(
  characterService: CharacterService,
  characterId: number,
): Promise<Uint8Array> {
  const [character, appearance, skills, clanInfo] = await Promise.all([
    characterService.findById(characterId),
    characterService.getAppearance(characterId),
    characterService.getEquippedSkills(characterId),
    characterService.getClanInfo(characterId),
  ]);

  const writer = new PacketWriter();

  // Clan header: clanId(4) + clanName(16) — zeros/empty when not in a clan
  writer.writeUint32(clanInfo?.clanId ?? 0);
  writer.writeFixedString(clanInfo?.clanName ?? "", 16);

  writer.writeBytes(PERSONAL_INFO_FIXED_BYTES);

  const time = Math.floor(Date.now() / 1000);
  writer.writeUint32(time);

  if (appearance) {
    writer.writeUint8(appearance.gender);
    writer.writeUint8(appearance.face);
    writer.writeUint8(appearance.upper);
    writer.writeUint8(appearance.lower);
    writer.writeUint8(appearance.face_paint);
    writer.writeUint8(appearance.upper_color);
    writer.writeUint8(appearance.lower_color);
    writer.writeUint8(appearance.voice);
    writer.writeUint8(appearance.pitch);
    writer.writePadding(4);
    writer.writeUint8(appearance.head);
    writer.writeUint8(appearance.chest);
    writer.writeUint8(appearance.hands);
    writer.writeUint8(appearance.waist);
    writer.writeUint8(appearance.feet);
    writer.writeUint8(appearance.accessory1);
    writer.writeUint8(appearance.accessory2);
    writer.writeUint8(appearance.head_color);
    writer.writeUint8(appearance.chest_color);
    writer.writeUint8(appearance.hands_color);
    writer.writeUint8(appearance.waist_color);
    writer.writeUint8(appearance.feet_color);
    writer.writeUint8(appearance.accessory1_color);
    writer.writeUint8(appearance.accessory2_color);
  } else {
    writer.writePadding(24);
  }

  if (skills) {
    writer.writeUint8(skills.skill_1);
    writer.writeUint8(skills.skill_2);
    writer.writeUint8(skills.skill_3);
    writer.writeUint8(skills.skill_4);
    writer.writeUint8(0);
    writer.writeUint8(skills.level_1);
    writer.writeUint8(skills.level_2);
    writer.writeUint8(skills.level_3);
    writer.writeUint8(skills.level_4);
    writer.writeUint8(0);
  } else {
    writer.writePadding(10);
  }

  for (let i = 0; i < 4; i++) {
    writer.writeUint32(DEFAULT_SKILL_EXP);
  }
  writer.writePadding(5);

  writer.writeUint32(characterId);

  writer.writeFixedString(character?.comment ?? "", 128);

  writer.writeUint8(character?.rank ?? 0);
  // Clan emblem flag: 3 = has published emblem, 0 = none
  writer.writeUint8(clanInfo?.hasEmblem ? 3 : 0);

  writer.writeBytes(PERSONAL_INFO_TRAILER);

  return writer.build();
}

@injectable()
@GameCommandHandler(0x4122)
export class GetPersonalInfoHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendPacket(session, 0x4122);
      return;
    }

    const payload = await buildPersonalInfoPayload(
      this.characterService,
      characterId,
    );
    await sendPacket(session, 0x4122, payload);
  }
}
