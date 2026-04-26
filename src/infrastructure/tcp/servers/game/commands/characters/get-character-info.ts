import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { UserService } from "../../../../../../modules/user/user-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { buildPersonalInfoPayload } from "./get-personal-info.ts";
import { GEAR_ITEMS } from "./get-gear.ts";

const CHAR_INFO_FIXED_BYTES = new Uint8Array([
  0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
]);
const FRIENDS_REGION_SIZE = 0x100;
const BLOCKED_REGION_SIZE = 0x100;
const MAX_FRIENDS_IN_REGION = 10;

// 26 bytes = 208 bits, covering gear_slot values 0–207.
// Bit N set means gear_slot N is owned by the character.
// The full 26 bytes are written directly at the end of the payload (no
// leading zero); the last byte (slots 200–207) is always zero because no
// GEAR_ITEMS fall in that range.
const GEAR_BITMASK_SIZE = 26;

function buildGearBitmask(ownedGearSlots: number[]): Uint8Array {
  const bitmask = new Uint8Array(GEAR_BITMASK_SIZE);
  for (const slot of ownedGearSlots) {
    if (slot < GEAR_BITMASK_SIZE * 8) {
      bitmask[slot >> 3] |= 1 << (slot & 7);
    }
  }
  return bitmask;
}

function buildCharacterInfoPayload(
  characterId: number,
  characterName: string,
  experience: number,
  lastLogin: number,
  secondLastLogin: number,
  friends: number[],
  blocked: number[],
  ownedGearSlots: number[],
): Uint8Array {
  const writer = new PacketWriter();

  writer.writeUint32(characterId);
  writer.writeFixedString(characterName, 16);
  writer.writeBytes(CHAR_INFO_FIXED_BYTES);
  writer.writeUint32(experience);
  writer.writeUint32(secondLastLogin);
  writer.writeUint32(lastLogin);
  writer.writeUint8(0);

  const friendsRegion = new Uint8Array(FRIENDS_REGION_SIZE);
  const friendsView = new DataView(friendsRegion.buffer);
  const friendCount = Math.min(friends.length, MAX_FRIENDS_IN_REGION);
  for (let index = 0; index < friendCount; index++) {
    friendsView.setUint32(index * 4, friends[index], false);
  }
  writer.writeBytes(friendsRegion);

  const blockedRegion = new Uint8Array(BLOCKED_REGION_SIZE);
  const blockedView = new DataView(blockedRegion.buffer);
  const blockedCount = Math.min(blocked.length, MAX_FRIENDS_IN_REGION);
  for (let index = 0; index < blockedCount; index++) {
    blockedView.setUint32(index * 4, blocked[index], false);
  }
  writer.writeBytes(blockedRegion);

  // Trailer: 26-byte gear ownership bitmask (bit N = gear_slot N owned, slots 0–207)
  writer.writeBytes(buildGearBitmask(ownedGearSlots));

  return writer.build();
}

@injectable()
@GameCommandHandler(0x4100)
export class GetCharacterInfoHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private userService = inject(UserService),
  ) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId ?? 0;
    const [character, user, friendsAndBlocked] = await Promise.all([
      characterId > 0
        ? this.characterService.findById(characterId)
        : Promise.resolve(null),
      session.userId
        ? this.userService.findById(session.userId)
        : Promise.resolve(null),
      characterId > 0
        ? this.characterService.getFriendsAndBlocked(characterId)
        : Promise.resolve([]),
    ]);

    const friends = friendsAndBlocked
      .filter((entry) => entry.type === 0)
      .map((entry) => entry.target_id);
    const blocked = friendsAndBlocked
      .filter((entry) => entry.type === 1)
      .map((entry) => entry.target_id);
    const ownedGearSlots = Array.from(GEAR_ITEMS);

    const experience = user?.main_exp ?? 0;
    const lastLogin = character?.creation_time ?? 0;

    const characterInfoPayload = buildCharacterInfoPayload(
      characterId,
      character?.name ?? "",
      experience,
      lastLogin,
      0,
      friends,
      blocked,
      ownedGearSlots,
    );

    await sendPacket(session, 0x4101, characterInfoPayload);
    await sendPacket(session, 0x4120);
    await sendPacket(session, 0x4121);
    if (characterId > 0) {
      const personalInfoPayload = await buildPersonalInfoPayload(
        this.characterService,
        characterId,
      );
      await sendPacket(session, 0x4122, personalInfoPayload);
    } else {
      await sendPacket(session, 0x4122);
    }
    await sendPacket(session, 0x4125);
    await sendPacket(session, 0x4140);
    await sendPacket(session, 0x4142);
  }
}
