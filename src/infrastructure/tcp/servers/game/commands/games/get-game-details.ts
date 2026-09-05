import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";

// The game details reply (0x4313): everything the details, join and
// player-list screens show about one hosted game. The layout is read straight
// out of the client's parser — the reply dispatcher routes 0x4313 to a parser
// whose read sequence fixes every field's size and order (reference
// GameDetails.java).
//
// The parser reads the fixed part UNCONDITIONALLY: a shorter payload is a
// parse error and the client keeps waiting, so every unknown field must be
// present even if zero. Player entries are read while payload remains, at
// most 18; a truncated final entry is also a parse error, never a shorter
// list.
const FIXED_SIZE = 372;
const PLAYER_ENTRY_SIZE = 28; // u32 charaId, char[16] name, u32 ping, u32 exp
const MAX_PLAYERS = 18;
const NAME_LENGTH = 16;
const ROTATION_ROUNDS = 16;
const RULE_TIMERS = 17;

// commonA/commonB bits — same values 0x4302 sends for consistency.
const A_ALWAYS = 0b100;
const B_AUTO_ASSIGN = 0b10;
const B_VOICE_CHAT = 0b1000000;

@injectable()
@GameCommandHandler(0x4312)
export class GetGameDetailsHandler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private characterService = inject(CharacterService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const gameId = reader.readUint32();

    const game = gameId > 0 ? await this.gameService.findById(gameId) : null;

    if (game === null) {
      // A details request for a game that no longer exists: the client's
      // parser still reads the full fixed part, so the refusal carries the
      // result word and nothing else readable.
      await sendPacket(session, 0x4313, new PacketWriter().writeUint32(RESULT_NONE).writePadding(FIXED_SIZE - 4).build());
      return;
    }

    const roster = (await this.gameService.getPlayers(game.id, game.host_id))
      .slice(0, MAX_PLAYERS);
    const pings = await this.gameService.getPlayerPings(game.id);
    const rating = (
      await this.gameService.getHostRatingSums([game.host_id])
    ).get(game.host_id) ?? { ratingSum: 0, votes: 0 };

    // Mean experience across the players currently in the game.
    let totalExperience = 0;
    const experiences = new Map<number, number>();
    for (const charaId of roster) {
      const character = await this.characterService.findById(charaId);
      const experience = character?.experience ?? 0;
      experiences.set(charaId, experience);
      totalExperience += experience;
    }
    const averageExperience = roster.length > 0
      ? Math.round(totalExperience / roster.length)
      : 0;

    const viewerIsHost = session.characterId === game.host_id;
    const rotation = parseStoredRotation(game.games);

    const writer = new PacketWriter();

    // ── Fixed part, 372 bytes ──────────────────────────────────────────────
    writer.writeUint32(RESULT_NONE); // result
    writer.writeUint32(game.id);
    writer.writeFixedString(game.name ?? "", NAME_LENGTH);
    writer.writeFixedString(game.comment ?? "", 128);

    // Not padding: the parser writes these into the same object fields the
    // create-game side fills — password_enabled and dedicated. The
    // create-game validator demands a 3..16-character password when
    // password_enabled is 1, and the dedicated toggle makes the max-players
    // row render one lower because the host keeps a connection without
    // taking a playing slot.
    writer.writeUint8(game.password !== "" ? 1 : 0);
    writer.writeUint8(0); // dedicated: not supported
    // The serving lobby's subtype, echoed one byte after two zero bytes.
    // session.lobbyId is the closest identity we carry on the session; the
    // subtype it derives from lives in the lobby table.
    writer.writeUint8(lobbySubtypeByte(session));
    writer.writeUint32(averageExperience);
    writer.writeUint32(rating.ratingSum); // hostScore — the host's lifetime rating sum
    writer.writeUint32(rating.votes); // hostVotes — lifetime vote count

    // THE HOST-RATING GATE, not a constant: a nonzero here is what lets the
    // end-of-game screen open the star picker and send 0x43c4. It must be 0
    // for your own game — that is how the client stops you rating yourself.
    writer.writeUint8(viewerIsHost ? 0 : 1);

    // The rotation: 16 interleaved (rule, map, flags) triples. Only a single
    // rule and map are stored until the 0x4310 settings blob is fully
    // interpreted, so round 0 carries them and the rest stay empty.
    for (let i = 0; i < ROTATION_ROUNDS; i++) {
      const round = rotation[i];
      writer.writeUint8(round?.[0] ?? 0);
      writer.writeUint8(round?.[1] ?? 0);
      writer.writeUint8(round?.[2] ?? 0);
    }
    writer.writeUint8(0); // two u8s after the rotation; meaning unknown
    writer.writeUint8(0);

    // Weapon restrictions: one bit per item, 1 = locked, bit 0 of byte 0 the
    // master enable. The per-weapon map is established but the remaining bits
    // are unverifiable on this build — kept zero rather than hardened guesses.
    writer.writePadding(16);

    writer.writeUint8(game.max_players);
    writer.writeUint8(roster.length);
    writer.writeUint32(0); // briefingTime
    writer.writePadding(22); // u32,u32,u16,u16,u32,u32,u16 in the parser; all zero
    writer.writeUint8(game.stance);
    writer.writeUint8(0); // levelLimitTolerance
    writer.writeUint32(0); // levelLimitBase — was once a hardcoded 22, a
    // plausible-looking level, which is why it survived unquestioned
    for (let i = 0; i < RULE_TIMERS; i++) writer.writeUint32(0); // per-rule timers
    writer.writeUint8(0); // uniqueRed
    writer.writeUint8(0); // uniqueBlue
    writer.writePadding(7);
    writer.writeUint8(A_ALWAYS); // commonA
    writer.writeUint8(B_AUTO_ASSIGN | B_VOICE_CHAT); // commonB
    writer.writeUint8(0);
    writer.writeUint16(0); // idleKick
    writer.writeUint16(0); // teamKillKick
    writer.writeUint32(game.ping); // the host's live ping — same column 0x4302 sends
    writer.writeUint8(0); // captureExtraTime
    writer.writeUint8(0); // sneakingSnakeKills
    writer.writePadding(8); // "per-rule byte-sized timers" for post-launch modes; replayed, not interpreted
    writer.writeUint8(0);
    writer.writeUint8(0); // extra flags: bit 1 = non-stat
    writer.writePadding(4);

    if (writer.size !== FIXED_SIZE) {
      // The parser reads exactly this much unconditionally; a wrong fixed
      // size desynchronises every later field. Checked rather than asserted.
      console.error(
        `[tcp][game] 0x4313 fixed part is ${writer.size} bytes, expected ${FIXED_SIZE}`,
      );
    }

    // ── Player entries: host first ─────────────────────────────────────────
    for (const charaId of roster) {
      const character = await this.characterService.findById(charaId);
      writer.writeUint32(charaId);
      writer.writeFixedString(character?.name ?? "", NAME_LENGTH);
      // Host-reported via 0x4398; 0 until a report arrives.
      writer.writeUint32(pings.get(charaId) ?? 0);
      writer.writeUint32(experiences.get(charaId) ?? 0);
    }

    await sendPacket(session, 0x4313, writer.build());
  }
}

function lobbySubtypeByte(session: TcpSession): number {
  return session.lobbyId === null ? 0 : Math.min(session.lobbyId, 0xff);
}

/** Decodes the stored JSON rotation triples ([[rule, map, flags], ...]). */
function parseStoredRotation(gamesJson: string): number[][] {
  try {
    const parsed = JSON.parse(gamesJson) as unknown;
    return Array.isArray(parsed) ? (parsed as number[][]) : [];
  } catch {
    return [];
  }
}
