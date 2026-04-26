import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";

// Total packet size (matches echo's PostGameInfoPacket.BUFFER_SIZE)
const BUFFER_SIZE = 0x8b;

// Skills with reduced EXP in the hardcoded skill table
const LOW_EXP_SKILLS = new Set([17, 20, 22]);
const NUM_SKILLS = 25;

@injectable()
@GameCommandHandler(0x4128)
export class GetPostGameInfoHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendPacket(session, 0x4129, null);
      return;
    }

    const [character, clan] = await Promise.all([
      this.characterService.findById(characterId),
      this.characterService.getClanInfo(characterId),
    ]);

    const experience = character?.experience ?? 0;
    const rank = character?.rank ?? 0;
    const gradePoints = experience;
    const rwd = characterId;
    const clanId = clan?.clanId ?? 0;
    const hasEmblem = clan?.hasEmblem ?? false;

    const writer = new PacketWriter();

    // Header
    writer.writeUint32(0);           // reserved
    writer.writeUint8(rank);         // animal rank
    writer.writeUint32(experience);  // experience
    writer.writeUint8(0);            // padding

    // Skills table: writeInt(count) then for each skill: byte(id) + uint16(exp) + byte(0)
    writer.writeUint32(NUM_SKILLS);
    for (let i = 1; i <= NUM_SKILLS; i++) {
      const skillExp = LOW_EXP_SKILLS.has(i) ? 0x2000 : 0x6000;
      writer.writeUint8(i);
      writer.writeUint16(skillExp);
      writer.writeUint8(0);
    }

    // Grade / clan section
    writer.writeUint32(0);            // reserved
    writer.writeUint32(gradePoints);  // grade points (same as experience)
    writer.writeUint32(0xffffff);     // unknown constant
    writer.writeUint32(clanId);       // clan id (0 if no clan)
    writer.writeUint16(0);            // reserved
    writer.writeUint8(1);             // unknown constant
    writer.writeUint8(hasEmblem ? 3 : 0); // emblem flag
    writer.writeUint32(rwd);          // reward value (character id)
    writer.writeUint8(0);             // trailing padding

    // Sanity pad to exact size in case of off-by-one
    if (writer.size < BUFFER_SIZE) {
      writer.writePadding(BUFFER_SIZE - writer.size);
    }

    await sendPacket(session, 0x4129, writer.build());
  }
}
