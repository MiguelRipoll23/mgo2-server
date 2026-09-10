import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import {
  CharacterService,
  skillLevelAtMax,
} from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const SKILL_SET_NAME_LENGTH = 63;
const NUM_SKILL_SETS = 3;

function buildSkillSetsPayload(
  sets: Array<{
    idx: number;
    name: string;
    modes: number;
    skill_1: number;
    skill_2: number;
    skill_3: number;
    skill_4: number;
    level_1: number;
    level_2: number;
    level_3: number;
    level_4: number;
  }>,
): Uint8Array {
  const writer = new PacketWriter();

  for (let i = 0; i < NUM_SKILL_SETS; i++) {
    const set = sets.find((s) => s.idx === i);
    writer.writeUint32(set?.modes ?? 0);
    writer.writeUint8(set?.skill_1 ?? 0);
    writer.writeUint8(set?.skill_2 ?? 0);
    writer.writeUint8(set?.skill_3 ?? 0);
    writer.writeUint8(set?.skill_4 ?? 0);
    writer.writeUint8(0);
    writer.writeUint8(set?.level_1 ?? 0);
    writer.writeUint8(set?.level_2 ?? 0);
    writer.writeUint8(set?.level_3 ?? 0);
    writer.writeUint8(set?.level_4 ?? 0);
    writer.writeUint8(0);
    writer.writeFixedString(set?.name ?? "", SKILL_SET_NAME_LENGTH);
  }

  return writer.build();
}

/**
 * Builds the stored skill-sets payload (0x4140) for a character. Shared with
 * the connect burst: an empty 0x4140 there leaves the client's saved-set
 * table unpopulated, which reads as "my saved sets are gone".
 */
export async function loadSkillSetsPayload(
  characterService: CharacterService,
  characterId: number | null,
): Promise<Uint8Array> {
  if (characterId === null) {
    return buildSkillSetsPayload([]);
  }
  const sets = await characterService.getSkillSets(characterId);
  // Levels are not read back from storage: every skill is max level, so
  // each assigned slot serves its skill's max regardless of what a stale
  // row (or an older client save) recorded.
  return buildSkillSetsPayload(
    sets.map((set) => ({
      ...set,
      level_1: skillLevelAtMax(set.skill_1),
      level_2: skillLevelAtMax(set.skill_2),
      level_3: skillLevelAtMax(set.skill_3),
      level_4: skillLevelAtMax(set.skill_4),
    })),
  );
}

@injectable()
@GameCommandHandler(0x4140)
export class GetSkillSetsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const payload = await loadSkillSetsPayload(
      this.characterService,
      session.characterId,
    );
    await sendPacket(session, 0x4140, payload);
  }
}
