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
import { GEAR_PAYLOAD } from "./get-gear.ts";

const CHAR_INFO_FIXED_BYTES = new Uint8Array([
  0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
]);

// The client's 0x4101 parser consumes a fixed 0x142-byte grid: the 0x29-byte
// header, 32 friend ids, 32 blocked ids, then a 25-byte tail (u8, 16 bytes,
// two u32s). Anything past 0x142 is never read. The reference servers send
// 0x243 with 256-byte friend/blocked regions — under them the client's
// "blocked list" was actually friend ids 33-64.
const INFO_PAYLOAD_SIZE = 0x142;
const MAX_LIST_IDS = 32;

function buildCharacterInfoPayload(
  characterId: number,
  characterName: string,
  experience: number,
  lastLogin: number,
  secondLastLogin: number,
  friends: number[],
  blocked: number[],
): Uint8Array {
  const writer = new PacketWriter();

  writer.writeUint32(characterId);
  writer.writeFixedString(characterName, 16);
  writer.writeBytes(CHAR_INFO_FIXED_BYTES);
  writer.writeUint32(experience);
  // The client shows the previous login alongside the current one.
  writer.writeUint32(secondLastLogin);
  writer.writeUint32(lastLogin);
  // Wire 0x028: the privilege nibble. 0 = none (the checked-correct default).
  writer.writeUint8(0);

  // Fixed 32-slot friend and blocked id arrays, zero-padded. These arrays are
  // how the client learns its authoritative list state at login.
  for (let i = 0; i < MAX_LIST_IDS; i++) {
    writer.writeUint32(i < friends.length ? friends[i] : 0);
  }
  for (let i = 0; i < MAX_LIST_IDS; i++) {
    writer.writeUint32(i < blocked.length ? blocked[i] : 0);
  }
  // 25-byte tail: u8 + 16 bytes + two u32s, all zeros.
  writer.writePadding(INFO_PAYLOAD_SIZE - writer.size);

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

    const experience = user?.main_exp ?? 0;
    const now = Math.floor(Date.now() / 1000);
    const lastLogin = character?.creation_time ?? now;

    const characterInfoPayload = buildCharacterInfoPayload(
      characterId,
      character?.name ?? "",
      experience,
      now,
      lastLogin,
      friends,
      blocked,
    );

    // Connect burst order matches the original's:
    // 0x4101, 0x4120, 0x4121×2, 0x4122, 0x4124, 0x4125, 0x4140, 0x4142.
    // 0x4124 (gear) was missing entirely — the client's gear table was
    // filled by whatever 0x4133 happened to send later.
    await sendPacket(session, 0x4101, characterInfoPayload);
    await sendPacket(session, 0x4120);
    if (characterId > 0) {
      const macros = await this.characterService.getChatMacros(characterId);
      await sendPacket(session, 0x4121, buildChatMacroPayload(macros, 0));
      await sendPacket(session, 0x4121, buildChatMacroPayload(macros, 1));
    } else {
      await sendPacket(session, 0x4121, buildChatMacroPayload([], 0));
      await sendPacket(session, 0x4121, buildChatMacroPayload([], 1));
    }
    if (characterId > 0) {
      const personalInfoPayload = await buildPersonalInfoPayload(
        this.characterService,
        characterId,
      );
      await sendPacket(session, 0x4122, personalInfoPayload);
    } else {
      await sendPacket(session, 0x4122);
    }
    await sendPacket(session, 0x4124, GEAR_PAYLOAD);
    await sendPacket(session, 0x4125);
    await sendPacket(session, 0x4140);
    await sendPacket(session, 0x4142);
  }
}

// ── 0x4121 chat macros: one packet per type ─────────────────────────────────
// Layout: u8 type, then twelve 64-byte texts. The previous single 0x301-byte
// reply padded each type's text block out and sent only one packet.

const MACRO_TEXT_LENGTH = 64;
const MACROS_PER_TYPE = 12;

function buildChatMacroPayload(
  macros: Array<{ type: number; idx: number; text: string }>,
  type: number,
): Uint8Array {
  const writer = new PacketWriter();
  writer.writeUint8(type);

  const typeMacros = macros
    .filter((m) => m.type === type)
    .sort((a, b) => a.idx - b.idx);

  for (let i = 0; i < MACROS_PER_TYPE; i++) {
    const macro = typeMacros.find((m) => m.idx === i);
    writer.writeFixedString(macro?.text ?? "", MACRO_TEXT_LENGTH);
  }

  return writer.build();
}
