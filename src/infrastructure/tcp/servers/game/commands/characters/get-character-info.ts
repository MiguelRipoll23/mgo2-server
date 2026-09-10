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
import { loadGameplayOptionsPayload } from "./get-gameplay-options-ui-settings.ts";
import { buildSkillsPayload } from "./get-skills.ts";
import { loadSkillSetsPayload } from "./get-skill-sets.ts";
import { loadGearSetsPayload } from "./get-gear-sets.ts";

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

// ── 0x4101 feature byte (payload offset 0x142) — the EXPANSION gate ───────
//
// One byte past the 0x142-byte grid, consumed by the 0x4101 parser at
// 0xf08cb0 and split by 0xf06450 into four separate per-bit feature flags at
// profile+0x4184..0x4187 (bit 0 → 0x4184, bit 1 → 0x4185, bit 2 → 0x4186,
// bit 3 → 0x4187). Sending 0 is not "no features" — it actively suppresses
// whatever each bit gates. Capstone on MGO2.ELF (MGO2PC build) decoded the
// readers of every bit:
//
//   bit 0 (0x01)  — selectable-content gate: row builders at 0xc7f27c and
//                   0xc80464 store 1 (enabled) vs 2 (greyed) into room/menu
//                   rows when it is set. The reference project's GATES.md
//                   identifies its build's same bit as "Team Sneaking
//                   selectable in Create Game" (rule 7), enforced at nine
//                   client sites plus an automatch refusal. Team Sneaking is
//                   the SCENE expansion's mode — but the expansion shipped
//                   modes, maps and codecs together, and this bit is the
//                   switch that lets the client offer that content at all.
//   bit 1 (0x02)  — gates display of the profile byte at +0x2705: all three
//                   reader sites (0x97c5f0 in fn 0x97b03c, 0xa4bffc in fn
//                   0xa4a640, 0xab7848 in fn 0xab6688) also read profile
//                   +0x2705, and 0x97c5f0 forces that display byte to 0x20
//                   when the bit is set. 0xa4bffc additionally loads resource
//                   hash 0x7acf1d. Named "extra content" rather than a mode.
//   bit 2 (0x04)  — login announcement: one-time state-machine sequence at
//                   0x98e2b0 fires events 6 and 8, sets the done flag at
//                   obj+0x2c0 and advances state 0x24 → 0x25.
//   bit 3 (0x08)  — login announcement: the 0x98e208 sequence fires event
//                   0xd, sets the done flag at obj+0x2c1 and advances to
//                   state 0x27. Both waits complete on the normal login
//                   burst, so neither can stall a connected client.
//
// The lobby-list bitmask the client uses for beginner/expansion/no-headshot
// rows is NOT this byte and is not a gate: the gate list (0x2003 offset 0x2d)
// carries those bits (see get-lobby-list.ts) and the hub list (0x4902 offset
// 0x07) is bit-reversed by the parser at 0xf15b30 into a u64 the entry getter
// at 0xf17128 skips (record+8) — parsed and never read, matching the
// reference's live 0xff sweep. Only this byte gates selectable content.
//
// All four bits default ON: this server is asked to serve the expansion.
// Override with MGO2SERVER_FEATURE_BYTE (0-15) for a full sweep.
const FEATURE_EX = 0x0f;

export const FEATURE_BIT_MODE_SELECT = 0x01;
export const FEATURE_BIT_EXTRA_CONTENT = 0x02;
export const FEATURE_BIT_ANNOUNCE_A = 0x04;
export const FEATURE_BIT_ANNOUNCE_B = 0x08;

function featureByte(): number {
  const configured = Deno.env.get("MGO2SERVER_FEATURE_BYTE");
  if (configured !== undefined) {
    const parsed = Number.parseInt(configured, 10);
    if (Number.isInteger(parsed) && parsed >= 0 && parsed <= 0x0f) return parsed;
  }
  return FEATURE_EX;
}

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
  // Byte 0x142 — the feature-flag byte this build's parser reads past the
  // grid (0xf08cb0 feeds it to the splitter at 0xf06450, which stores its
  // low four bits as separate feature flags). Without this byte the parse
  // fails with -0x47 and every derived field stays zero — including anything
  // that gates content. See featureByte() for what each bit gates.
  writer.writeUint8(featureByte());

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
    // 0x4120 with the stored settings. An empty or flag-less 0x4120 makes
    // the client's validator overwrite every gameplay setting (camera
    // speed, weapon cycling, volumes, ...) with hardcoded defaults —
    // exactly the "settings are not saved" symptom.
    await sendPacket(
      session,
      0x4120,
      await loadGameplayOptionsPayload(
        this.characterService,
        characterId > 0 ? characterId : null,
      ),
    );
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
    // The remaining catalogues with real payloads too: empty versions left
    // the client's skill table and saved skill/gear-set slots unpopulated
    // after login (the reference's burst sends these fully populated).
    await sendPacket(session, 0x4125, buildSkillsPayload());
    await sendPacket(
      session,
      0x4140,
      await loadSkillSetsPayload(
        this.characterService,
        characterId > 0 ? characterId : null,
      ),
    );
    await sendPacket(
      session,
      0x4142,
      await loadGearSetsPayload(
        this.characterService,
        characterId > 0 ? characterId : null,
      ),
    );
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
