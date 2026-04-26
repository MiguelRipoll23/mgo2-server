import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const GEAR_SET_NAME_LENGTH = 63;

@injectable()
@GameCommandHandler(0x4143)
export class UpdateGearSetsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length > 0) {
      const reader = new PacketReader(packet.payload);
      const sets: Array<{
        idx: number;
        name: string;
        stages: number;
        face: number;
        head: number;
        head_color: number;
        upper: number;
        upper_color: number;
        lower: number;
        lower_color: number;
        chest: number;
        chest_color: number;
        waist: number;
        waist_color: number;
        hands: number;
        hands_color: number;
        feet: number;
        feet_color: number;
        accessory1: number;
        accessory1_color: number;
        accessory2: number;
        accessory2_color: number;
        face_paint: number;
      }> = [];

      let index = 0;
      while (reader.remaining() >= GEAR_SET_NAME_LENGTH + 22 * 4) {
        const name = reader.readFixedString(GEAR_SET_NAME_LENGTH);
        const stages = reader.readUint32();
        const face = reader.readUint32();
        const head = reader.readUint32();
        const head_color = reader.readUint32();
        const upper = reader.readUint32();
        const upper_color = reader.readUint32();
        const lower = reader.readUint32();
        const lower_color = reader.readUint32();
        const chest = reader.readUint32();
        const chest_color = reader.readUint32();
        const waist = reader.readUint32();
        const waist_color = reader.readUint32();
        const hands = reader.readUint32();
        const hands_color = reader.readUint32();
        const feet = reader.readUint32();
        const feet_color = reader.readUint32();
        const accessory1 = reader.readUint32();
        const accessory1_color = reader.readUint32();
        const accessory2 = reader.readUint32();
        const accessory2_color = reader.readUint32();
        const face_paint = reader.readUint32();
        sets.push({
          idx: index,
          name,
          stages,
          face,
          head,
          head_color,
          upper,
          upper_color,
          lower,
          lower_color,
          chest,
          chest_color,
          waist,
          waist_color,
          hands,
          hands_color,
          feet,
          feet_color,
          accessory1,
          accessory1_color,
          accessory2,
          accessory2_color,
          face_paint,
        });
        index++;
      }

      await this.characterService.updateGearSets(characterId, sets);
    }
    await sendPacket(session, 0x4144, null);
  }
}
