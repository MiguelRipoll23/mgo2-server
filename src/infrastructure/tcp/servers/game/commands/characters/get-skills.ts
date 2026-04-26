import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const NUM_SKILLS = 25;
const SKILL_EXP_NORMAL = 0x6000;
const SKILL_EXP_SPECIAL = 0x2000;

function buildSkillsPayload(): Uint8Array {
  const writer = new PacketWriter();
  writer.writeUint32(NUM_SKILLS);

  for (let i = 1; i <= NUM_SKILLS; i++) {
    const exp =
      i === 17 || i === 20 || i === 22 ? SKILL_EXP_SPECIAL : SKILL_EXP_NORMAL;
    writer.writeUint8(i);
    writer.writeUint16(exp);
    writer.writeUint8(0);
  }

  return writer.build();
}

@injectable()
@GameCommandHandler(0x4125)
export class GetSkillsHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const payload = buildSkillsPayload();
    await sendPacket(session, 0x4125, payload);
  }
}
