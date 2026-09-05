import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import {
  CharacterService,
} from "../../../../../../modules/character/character-service.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// The host's per-skill experience report — the ONLY way skill progression
// persists: skills level by use, which the server cannot observe, so the
// client reports it here. PERSISTED to the character_skills table; only rows
// the character already owns are updated (a record for a missing skill would
// be the client claiming a grant, which is not this command's job — 0x4125
// decides what a character owns).
//
// Layout: {u32 charaId, u32 count, count × {u8 skillId, u16 experience}}.
// Count is capped at 127 by the client's own serializer (0xD419BC:
// `cmplwi cr7,r0,127; bgt`); each record is 3 bytes. Values are ABSOLUTE
// totals, not deltas — the builder computes a delta only to decide whether a
// skill changed, then overwrites it with the live value (0x27D140).
//
// THE HOST REPORTS FOR EVERYONE (arming sites sweep slots 0..23), so
// attribution is the character id at wire +0x00, not the connection — and it
// is validated against this game's roster rather than trusted.
const MAX_SKILL_RECORDS = 127;
const SKILL_RECORD_WIRE_SIZE = 3;

@injectable()
@GameCommandHandler(0x43a4)
export class HostSkillExperienceHandler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private characterService = inject(CharacterService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const payload = packet.payload;

    if (payload.length < 8) {
      // Nothing readable — acknowledge anyway: the report opens a wait slot,
      // so an unanswered reply is a latent FFFFFF60 hang.
      await sendResult(session, 0x43a5, RESULT_NONE);
      return;
    }

    const reader = new PacketReader(payload);
    const targetId = reader.readUint32();
    const count = reader.readUint32();

    if (
      count > MAX_SKILL_RECORDS ||
      reader.remaining() < count * SKILL_RECORD_WIRE_SIZE
    ) {
      // Declared count does not match the wire — drop the records but still
      // complete the request slot.
      await sendResult(session, 0x43a5, RESULT_NONE);
      return;
    }

    const reported = new Map<number, number>();
    for (let i = 0; i < count; i++) {
      const skillId = reader.readUint8();
      const experience = reader.readUint16();
      reported.set(skillId, experience);
    }

    // Attribution is the payload's character id, validated against the game
    // roster — the host sweeps every player slot and reports for all of them.
    const gameId = session.gameId;
    const game = gameId !== null ? await this.gameService.findById(gameId) : null;

    if (game === null) {
      console.log(
        `[tcp][game] 0x43a4 from a non-host/no-game connection (character ${targetId}); dropped`,
      );
      await sendResult(session, 0x43a5, RESULT_NONE);
      return;
    }

    // Attribution must be a roster member: the host sweeps every player slot,
    // so a valid report is always for a CURRENT player. (The reference also
    // accepts characters that played last round via a played-last-round
    // table; we drop those instead — the host re-reports nothing for them.)
    const inGame = await this.gameService.isInGame(
      game.id,
      game.host_id,
      targetId,
    );
    if (!inGame) {
      console.log(
        `[tcp][game] 0x43a4 from game ${game.id} reports character ${targetId}, who is not in it; dropped`,
      );
      await sendResult(session, 0x43a5, RESULT_NONE);
      return;
    }

    const applied = await this.characterService.applySkillExperience(
      targetId,
      reported,
    );

    console.log(
      `[tcp][game] game ${game.id}: character ${targetId} reported ${reported.size} changed skill(s); ${applied} applied`,
    );

    await sendResult(session, 0x43a5, RESULT_NONE);
  }
}
