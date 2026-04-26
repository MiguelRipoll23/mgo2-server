import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const GEAR_SET_NAME_LENGTH = 63;
const NUM_GEAR_SETS = 3;

function buildGearSetsPayload(
  sets: Array<{
    idx: number;
    name: string;
    stages: number;
    face: number;
    head: number;
    upper: number;
    lower: number;
    chest: number;
    waist: number;
    hands: number;
    feet: number;
    accessory1: number;
    accessory2: number;
    head_color: number;
    upper_color: number;
    lower_color: number;
    chest_color: number;
    waist_color: number;
    hands_color: number;
    feet_color: number;
    accessory1_color: number;
    accessory2_color: number;
    face_paint: number;
  }>,
): Uint8Array {
  const writer = new PacketWriter();

  for (let i = 0; i < NUM_GEAR_SETS; i++) {
    const set = sets.find((s) => s.idx === i);
    writer.writeUint32(set?.stages ?? 0);
    writer.writeUint8(set?.face ?? 0);
    writer.writeUint8(set?.head ?? 0);
    writer.writeUint8(set?.upper ?? 0);
    writer.writeUint8(set?.lower ?? 0);
    writer.writeUint8(set?.chest ?? 0);
    writer.writeUint8(set?.waist ?? 0);
    writer.writeUint8(set?.hands ?? 0);
    writer.writeUint8(set?.feet ?? 0);
    writer.writeUint8(set?.accessory1 ?? 0);
    writer.writeUint8(set?.accessory2 ?? 0);
    writer.writeUint8(set?.head_color ?? 0);
    writer.writeUint8(set?.upper_color ?? 0);
    writer.writeUint8(set?.lower_color ?? 0);
    writer.writeUint8(set?.chest_color ?? 0);
    writer.writeUint8(set?.waist_color ?? 0);
    writer.writeUint8(set?.hands_color ?? 0);
    writer.writeUint8(set?.feet_color ?? 0);
    writer.writeUint8(set?.accessory1_color ?? 0);
    writer.writeUint8(set?.accessory2_color ?? 0);
    writer.writeUint8(set?.face_paint ?? 0);
    writer.writeFixedString(set?.name ?? "", GEAR_SET_NAME_LENGTH);
  }

  return writer.build();
}

@injectable()
@GameCommandHandler(0x4142)
export class GetGearSetsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendPacket(session, 0x4142);
      return;
    }

    const sets = await this.characterService.getGearSets(characterId);
    const payload = buildGearSetsPayload(sets);
    await sendPacket(session, 0x4142, payload);
  }
}
