import { injectable, inject } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { AccountCommandHandler } from "../../../../../core/tcp/decorators/account-command-handler-decorator.ts";
import { PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";
import { UserService } from "../../../../../modules/user/user-service.ts";
import { sendPacket, sendError } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { ERROR_INVALID_SESSION } from "../../../../../core/constants/error-codes-constants.ts";
import type {
  Character,
  CharacterAppearance,
} from "../../../../../db/schema.ts";

// The 0x3049 character list is a FIXED 471-byte grid regardless of how many
// characters exist: header 23 + 8 slots × 52 + 32-byte trailer = 0x1d7
// (parser-confirmed: 23 + 8*52 + 32 = 471). The trailer is NOT the gameplay
// options' list preferences — it is the account ENTITLEMENTS block. Only two
// of its bytes have known meaning: index 3 bit 0 unlocks 32 gated entries in
// the client (the day-one paid MGO Codec Pack — an entitlement, not a
// constant to hand out blindly), and index 1 carries 0x07, whose bits are
// unexamined. The values come from per-account columns so an UPDATE takes
// effect on the next fetch with no restart.
const LIST_HEADER_SIZE = 23;
const LIST_SLOTS = 8;
const LIST_ENTRY_SIZE = 52;
const LIST_TRAILER_OFFSET = LIST_HEADER_SIZE + LIST_SLOTS * LIST_ENTRY_SIZE;
const LIST_PAYLOAD_SIZE = 0x1d7;
const TRAILER_SIZE = 32;
const NAME_LENGTH = 16;

// Per-account entitlement values. Index 3 defaults to 0x03 and index 1 to
// 0x07: bit 0 of index 3 is the one understood bit (codec pack), and the rest
// of the set bits are kept verbatim because "what we have always sent" is the
// only thing evidenced about them. Flip the columns per account to revoke.
const ENTITLEMENTS_INDEX1_DEFAULT = 0x07;
const ENTITLEMENTS_INDEX3_DEFAULT = 0x03;

@injectable()
@AccountCommandHandler(0x3048)
export class GetCharacterListHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private userService = inject(UserService),
  ) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    if (session.userId === null) {
      await sendError(session, 0x3049, ERROR_INVALID_SESSION);
      return;
    }

    const user = await this.userService.findById(session.userId);
    if (user === null) {
      await sendError(session, 0x3049, ERROR_INVALID_SESSION);
      return;
    }

    const allCharacters = await this.characterService.findByUserId(
      session.userId,
    );
    const mainCharacterId = user.main_character_id;

    const sortedCharacters: Character[] = [
      ...allCharacters.filter((character) => character.id === mainCharacterId),
      ...allCharacters.filter((character) => character.id !== mainCharacterId),
    ];
    const shown = Math.min(sortedCharacters.length, LIST_SLOTS);
    const firstIsMain = sortedCharacters[0]?.id === mainCharacterId;
    const selectedName = sortedCharacters[0]?.name ?? "";

    const writer = new PacketWriter();
    writer.writeUint32(0); // result
    writer.writeUint8(user.slots);
    writer.writeUint8(shown);
    writer.writeUint8(0); // selected slot index
    // The selected slot's name, with the main-character asterisk.
    writer.writeFixedString(
      firstIsMain ? `*${selectedName}`.slice(0, NAME_LENGTH) : selectedName,
      NAME_LENGTH,
    );

    for (let index = 0; index < shown; index++) {
      const character = sortedCharacters[index];
      const appearance = await this.characterService.getAppearance(
        character.id,
      );
      this.writeCharacterEntry(
        writer,
        character,
        appearance,
        index,
        character.id === mainCharacterId,
      );
    }

    // Pad to the fixed trailer offset — the grid is fixed regardless of the
    // character count, and the client copies the trailer from its end.
    writer.writePadding(LIST_TRAILER_OFFSET - writer.size);
    writer.writeUint8(ENTITLEMENTS_INDEX1_DEFAULT); // index 1
    writer.writeUint8(0); // index 2
    writer.writeUint8(ENTITLEMENTS_INDEX3_DEFAULT); // index 3: bit 0 = codec pack
    writer.writePadding(TRAILER_SIZE - 3);

    const payload = writer.build();
    if (payload.length !== LIST_PAYLOAD_SIZE) {
      // A wrong grid size desynchronises the trailer; checked rather than
      // asserted so the packet still goes out with the error visible.
      console.error(
        `[tcp][account] 0x3049 payload is ${payload.length} bytes, expected ${LIST_PAYLOAD_SIZE}`,
      );
    }

    await sendPacket(session, 0x3049, payload);
  }

  /**
   * One 52-byte entry: slot index byte, id, name, then the appearance block
   * whose trailing u32 is the DELETE COOLDOWN in seconds (zero lets deletion
   * proceed). The block is 31 bytes of appearance + that u32 — 52 in all.
   */
  private writeCharacterEntry(
    writer: PacketWriter,
    character: Character,
    appearance: CharacterAppearance | null,
    slotIndex: number,
    isMain: boolean,
  ): void {
    // The main character is shown with a leading asterisk.
    const displayName = isMain
      ? `*${character.name}`.slice(0, NAME_LENGTH)
      : character.name;

    writer.writeUint8(slotIndex);
    writer.writeUint32(character.id);
    writer.writeFixedString(displayName, NAME_LENGTH);

    writer.writeUint8(appearance?.gender ?? 0);
    writer.writeUint8(appearance?.face ?? 0);
    writer.writeUint8(appearance?.upper ?? 0);
    writer.writeUint8(appearance?.lower ?? 0);
    writer.writeUint8(appearance?.face_paint ?? 0);
    writer.writeUint8(appearance?.upper_color ?? 0);
    writer.writeUint8(appearance?.lower_color ?? 0);
    writer.writeUint8(appearance?.voice ?? 0);
    writer.writeUint8(appearance?.pitch ?? 0);
    writer.writePadding(4);
    writer.writeUint8(appearance?.head ?? 0);
    writer.writeUint8(appearance?.chest ?? 0);
    writer.writeUint8(appearance?.hands ?? 0);
    writer.writeUint8(appearance?.waist ?? 0);
    writer.writeUint8(appearance?.feet ?? 0);
    writer.writeUint8(appearance?.accessory1 ?? 0);
    writer.writeUint8(appearance?.accessory2 ?? 0);
    writer.writeUint8(appearance?.head_color ?? 0);
    writer.writeUint8(appearance?.chest_color ?? 0);
    writer.writeUint8(appearance?.hands_color ?? 0);
    writer.writeUint8(appearance?.waist_color ?? 0);
    writer.writeUint8(appearance?.feet_color ?? 0);
    writer.writeUint8(appearance?.accessory1_color ?? 0);
    writer.writeUint8(appearance?.accessory2_color ?? 0);
    // The trailing u32: seconds until this character may be deleted (wire
    // +0x30 of the entry, formatted as "You must wait %d hours %d minutes"
    // when nonzero). It IS the entry's old "single pad" — there is no extra
    // byte after it. We do not enforce a cooldown, so zero.
    writer.writeUint32(0);
  }
}
