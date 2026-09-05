import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import {
  CharacterService,
  MAX_SKILL_EXPERIENCE,
} from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

// The client defines exactly 17 skills: its own bound is `(id - 1) <= 16`
// (0x8DC3A8, repeated at 0xB3B530/0xB3B5B0) and every id-keyed lookup clamps
// to 17. Ids 18..127 are addressable by the parser and defined by nothing.
const NUM_DEFINED_SKILLS = 17;

// Backfill defaults, matching the reference's V20 migration: skill 17 has no
// experience path in the client binary at all (0x8DB8A8 renders it without a
// level bar), so it gets the level-1 value; everything else gets level 3.
const SKILL_EXP_DEFAULT = 0x6000;
const SKILL_EXP_NO_PATH = 0x2000;

@injectable()
@GameCommandHandler(0x4125)
export class GetSkillsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    let skills = characterId === null
      ? []
      : await this.characterService.getSkills(characterId);

    // A character with no rows yet gets the historical grant set — every
    // defined skill, at the levels previously sent unconditionally — written
    // back so later 0x43a4 reports have rows to move.
    if (characterId !== null && skills.length === 0) {
      const defaults = Array.from(
        { length: NUM_DEFINED_SKILLS },
        (_, index) => ({
          character_id: characterId,
          skill_id: index + 1,
          experience: index + 1 === NUM_DEFINED_SKILLS
            ? SKILL_EXP_NO_PATH
            : SKILL_EXP_DEFAULT,
          flag: 0,
        }),
      );
      await this.characterService.grantSkills(defaults);
      skills = await this.characterService.getSkills(characterId);
    }

    const writer = new PacketWriter();
    writer.writeUint32(skills.length);
    for (const skill of skills) {
      writer.writeUint8(skill.skill_id);
      writer.writeUint16(Math.min(skill.experience, MAX_SKILL_EXPERIENCE));
      writer.writeUint8(skill.flag);
    }

    await sendPacket(session, 0x4125, writer.build());
  }
}
