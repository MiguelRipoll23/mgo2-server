import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const GEAR_SET_NAME_LENGTH = 63;
/** u32 stages + 22 appearance bytes + 63-byte name. */
const GEAR_SET_RECORD_SIZE = 4 + 22 + GEAR_SET_NAME_LENGTH;

/**
 * 0x4143 — the client's gear-set save. In this build the request is three
 * records of { u32 stages, 22×u8 appearance bytes, 63-byte name } (builder
 * 0xf0ca34: u32 via 0xf2df64, then 22 single-byte appends at +0x44..+0x59,
 * then the 63-byte name), and the ack is parsed on the SAME id 0x4143
 * (waiter 0xf06b38 reads one u32 result). Replying 0x4144 was never parsed.
 *
 * Byte map for the 22 appearance bytes (0x44-relative): 0 face, 1 head,
 * 2 upper, 3 lower, 4 chest, 5 waist, 6 hands, 7 feet, 8 accessory1,
 * 9 accessory2, 10 head colour, 11 upper colour, 12 lower colour,
 * 13 chest colour, 14 waist colour, 15 hands colour, 16 feet colour,
 * 17 accessory1 colour, 18 accessory2 colour, 19 face paint; 20-21 unused.
 */
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
      while (reader.remaining() >= GEAR_SET_RECORD_SIZE) {
        const stages = reader.readUint32();
        const b = reader.readBytes(22);
        const name = reader.readFixedString(GEAR_SET_NAME_LENGTH);

        sets.push({
          idx: index,
          name,
          stages,
          face: b[0],
          head: b[1],
          upper: b[2],
          lower: b[3],
          chest: b[4],
          waist: b[5],
          hands: b[6],
          feet: b[7],
          accessory1: b[8],
          accessory2: b[9],
          head_color: b[10],
          upper_color: b[11],
          lower_color: b[12],
          chest_color: b[13],
          waist_color: b[14],
          hands_color: b[15],
          feet_color: b[16],
          accessory1_color: b[17],
          accessory2_color: b[18],
          face_paint: b[19],
        });
        index++;
      }

      await this.characterService.updateGearSets(characterId, sets);
    }
    // Ack on the request's own id; the waiter reads the first u32 as result.
    await sendPacket(session, 0x4143, new Uint8Array(4));
  }
}
