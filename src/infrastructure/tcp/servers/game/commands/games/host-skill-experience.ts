import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// The host's per-skill experience report — the only way skill progression
// would persist, since skills level by use, which the server cannot observe.
// Every character now serves the fixed max-level catalogue (0x4125), so there
// is nothing to store: the report is parsed for the log and acknowledged —
// the reply opens a wait slot, and an unanswered command is a latent FFFFFF60
// hang.
//
// Layout: {u32 charaId, u32 count, count × {u8 skillId, u16 experience}}.
// Count is capped at 127 by the client's own serializer (0xD419BC:
// `cmplwi cr7,r0,127; bgt`); each record is 3 bytes. Values are ABSOLUTE
// totals, not deltas. The host reports for every player slot (arming sites
// sweep slots 0..23) — attribution is the character id at wire +0x00.
const MAX_SKILL_RECORDS = 127;
const SKILL_RECORD_WIRE_SIZE = 3;

@injectable()
@GameCommandHandler(0x43a4)
export class HostSkillExperienceHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const payload = packet.payload;

    let targetId = 0;
    let reported = 0;
    if (payload.length >= 8) {
      const reader = new PacketReader(payload);
      targetId = reader.readUint32();
      const count = Math.min(reader.readUint32(), MAX_SKILL_RECORDS);

      if (reader.remaining() >= count * SKILL_RECORD_WIRE_SIZE) {
        reported = count;
        // Records are parsed no further — nothing is applied anywhere.
      }
    }

    console.log(
      `[tcp][game] 0x43a4: character ${targetId} reported ${reported} skill record(s); acknowledged, not persisted (all skills served at max level)`,
    );

    await sendResult(session, 0x43a5, RESULT_NONE);
  }
}
