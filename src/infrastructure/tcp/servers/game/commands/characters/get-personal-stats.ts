import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { CharacterStatsService } from "../../../../../../modules/character/character-stats-service.ts";
import { RESULT_CHARACTER_GONE, RESULT_GENERAL } from "../../../../../../core/constants/error-codes-constants.ts";

// Fixed grids the three parsers consume (from the reference's ELF trace).
const INFO_SIZE = 0x288;
const MATRIX_SIZE = 0x248;
const TAIL_SIZE = 0x24c;

// 0x4103 wire layout, byte-exact from the reference's parser trace
// (0xd3e9ac) — the writes total exactly 0x288 = 648 bytes.
const NAME_LENGTH = 16;
const COMMENT_LENGTH = 128;
const COMMENT_OFFSET = 413; // confirmed live via fingerprint v3
const RELATION_LIST_IDS = 32;

// Matrix: 8 mode rows × 18 u32 columns, mode-major, after {status, page}.
const MODE_ROWS = 8;
const STAT_COLUMNS = 18;
const SCORE_COLUMN = 3;
// Column 17 of the summary (last) row = play time; column 13 = level.
const SUMMARY_PLAY_SECONDS_COLUMN = 17;
const SUMMARY_LEVEL_COLUMN = 13;

// Tail: two 73-u32 score records after the status word.
const TAIL_RECORD_INTS = 73;

@injectable()
@GameCommandHandler(0x4102)
export class GetPersonalStatsHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private statsService = inject(CharacterStatsService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    if (reader.remaining() < 4) {
      // Malformed request: the generic collapse. (Every nonzero value other
      // than -266 shows the same "Unable to acquire character information".)
      await sendPacket(
        session,
        0x4103,
        new PacketWriter().writeUint32(RESULT_GENERAL).build(),
      );
      return;
    }
    const targetCharacterId = reader.readUint32();

    const character = targetCharacterId > 0
      ? await this.characterService.findById(targetCharacterId)
      : null;
    if (!character) {
      // The client's own code for a deleted character, sent unmasked so it
      // matches its error table ("Designated character has been deleted…").
      await sendPacket(
        session,
        0x4103,
        new PacketWriter().writeUint32(RESULT_CHARACTER_GONE).build(),
      );
      return;
    }

    const stats = await this.statsService.findByCharacterId(targetCharacterId);
    const clanInfo = await this.characterService.getClanInfo(targetCharacterId);
    const friendsAndBlocked = await this.characterService.getFriendsAndBlocked(
      targetCharacterId,
    );
    const friends = friendsAndBlocked
      .filter((entry) => entry.type === 0)
      .map((entry) => entry.target_id);
    const blocked = friendsAndBlocked
      .filter((entry) => entry.type === 1)
      .map((entry) => entry.target_id);

    // ── Packet 0x4103: header, fixed 0x288 bytes ────────────────────────────
    // Opens with a REAL status u32: on nonzero the client completes with that
    // error and skips the rest, so this word cannot be a fingerprint.
    const header = new PacketWriter();
    header.writeUint32(0); // status
    header.writeUint32(targetCharacterId);
    header.writeFixedString(character.name ?? "", NAME_LENGTH);
    header.writeBytes(CHAR_INFO_PREFIX); // the four dead u16 constants (→ wire 0x20)
    header.writeUint32(character.experience ?? 0);
    // Login times: we do not record them. Zero — an invented epoch renders as
    // a real date, and "never" is the honest answer.
    header.writeUint32(0);
    header.writeUint32(0);
    header.writeUint8(0); // the lone u8 [UNKNOWN]
    // Friend and blocked lists: REAL ids, zero-padded to the fixed 32-slot
    // arrays. They used to be fingerprints, which fed the client 64 character
    // ids that do not exist.
    for (let i = 0; i < RELATION_LIST_IDS; i++) {
      header.writeUint32(i < friends.length ? friends[i] : 0);
    }
    for (let i = 0; i < RELATION_LIST_IDS; i++) {
      header.writeUint32(i < blocked.length ? blocked[i] : 0);
    }
    // The clan record: {u32 id, name[16], u8 status} + 12×u16 privilege word.
    // This is why Personal Data showed no clan.
    header.writeUint8(0); // u8 @301 [UNKNOWN]
    header.writeUint32(clanInfo?.clanId ?? 0);
    header.writeFixedString(clanInfo?.clanName ?? "", NAME_LENGTH);
    header.writeUint8(clanInfo ? 1 : 0); // clan state
    for (let i = 0; i < 12; i++) {
      header.writeUint16(0); // the privilege word must stay 0
    }
    // Remaining tail fields [UNKNOWN] — ZERO. The client mints medals and
    // titles from this packet (a medal bitmask is parsed out of it), so
    // invented values award unearned medals.
    header.writeUint32(0);
    for (let i = 0; i < 9; i++) header.writeUint8(0);
    header.writeUint32(0);
    for (let i = 0; i < 14; i++) header.writeUint8(0);
    for (let i = 0; i < 10; i++) header.writeUint8(0);
    for (let i = 0; i < 5; i++) header.writeUint32(0);
    header.writeUint8(0);
    header.writeUint32(0);
    header.writePadding(COMMENT_OFFSET - header.size); // → wire 413
    header.writeFixedString(character.comment ?? "", COMMENT_LENGTH);
    header.writeUint8(0); // worn title (no award service yet)
    for (let i = 0; i < 9; i++) header.writeUint8(0);
    // Rating block, nine u32s: e3 = title unlock mask (never set — bit 22+
    // overruns the client's popcount loop), e5/e6 = host rating num/den.
    for (let i = 0; i < 9; i++) header.writeUint32(0);
    header.writeUint32(0); // → obj+0x30
    header.writeFixedString("", NAME_LENGTH); // instructor name (none)
    header.writeUint32(0); // generation
    header.writeUint32(0);
    header.writePadding(16); // the 16-byte medal bitfield — all zero
    header.writeUint8(0); // clan emblem flag (0 = never fetch)
    header.writeUint32(0); // instructor score numerator
    header.writeUint32(0); // instructor score denominator
    header.writeUint32(0); // wire 640 [UNKNOWN]
    header.writeUint32(0); // trailing u32
    header.writePadding(INFO_SIZE - header.size); // → exactly 0x288

    await sendPacket(session, 0x4103, header.build());

    // ── Packets 0x4105 ×2: per-mode stats grids ─────────────────────────────
    // ORDER IS LOAD-BEARING: page 0 (cumulative) must precede page 1 (weekly)
    // — receipt of page 0 zeroes the whole grid region including page 1. And
    // 0x4107 must be LAST: its parser unconditionally completes the wait slot.
    await sendPacket(
      session,
      0x4105,
      buildMatrix(stats, this.statsService, 0, character),
    );
    await sendPacket(
      session,
      0x4105,
      buildMatrix(stats, this.statsService, 1, character),
    );

    // ── Packet 0x4107: tail, sent last, releases the client ─────────────────
    const tail = new PacketWriter();
    tail.writeUint32(0); // status
    writeScoreRecord(tail, stats, true);
    writeScoreRecord(tail, stats, false);
    tail.writePadding(TAIL_SIZE - tail.size);
    await sendPacket(session, 0x4107, tail.build());
  }
}

// The four dead u16 constants after the name in 0x4101/0x4103
// (0x16AE, 0x0338, 0x013E, 0x0150) — kept as captured.
const CHAR_INFO_PREFIX = new Uint8Array([
  0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
]);

/** One 0x4105 matrix: status, page selector, then 8×18 u32 columns. */
type CharacterRow = NonNullable<Awaited<ReturnType<CharacterService["findById"]>>>;
type CharacterStatsRow = NonNullable<Awaited<ReturnType<CharacterStatsService["findByCharacterId"]>>>;

function buildMatrix(
  stats: CharacterStatsRow | null,
  statsService: CharacterStatsService,
  page: number,
  character: CharacterRow,
): Uint8Array {
  const matrix = new PacketWriter();
  matrix.writeUint32(0); // status
  matrix.writeUint32(page); // page selector (0 or 1; anything larger is discarded)

  for (let mode = 0; mode < MODE_ROWS; mode++) {
    for (let column = 0; column < STAT_COLUMNS; column++) {
      const value = readStat(stats, statsService, mode, column);
      matrix.writeUint32(value);
    }
  }

  // Summary row (the last row) of the cumulative matrix feeds the
  // player-details card: column 17 = play time, column 13 = level.
  if (page === 0) {
    // Patch the two summary cells into the already-written grid.
    const payload = matrix.build();
    const summaryBase = 8 + (MODE_ROWS - 1) * STAT_COLUMNS * 4;
    const level = CharacterService.calculateLevel(character.experience ?? 0);
    // Play time for the details card: the character's recorded total seconds.
    const playSeconds = character.creation_time > 0 && stats ? (stats.time ?? 0) : 0;
    new DataView(payload.buffer).setUint32(summaryBase + SUMMARY_PLAY_SECONDS_COLUMN * 4, playSeconds >>> 0, false);
    new DataView(payload.buffer).setUint32(summaryBase + SUMMARY_LEVEL_COLUMN * 4, level >>> 0, false);
    return payload;
  }

  return matrix.build();
}

function readStat(
  stats: CharacterStatsRow | null,
  statsService: CharacterStatsService,
  mode: number,
  column: number,
): number {
  if (!stats) return 0;
  try {
    const m = statsService.getModeStats(stats, mode);
    const values = [
      m.kills ?? 0,
      m.deaths ?? 0,
      m.lockKills ?? 0,
      m.score ?? 0,
      m.stuns ?? 0,
      m.stunsRec ?? 0,
      m.hsKills ?? 0,
      m.hsDeaths ?? 0,
      m.hsStuns ?? 0,
      m.hsStunsRec ?? 0,
      m.lockStuns ?? 0,
      m.lockDeaths ?? 0,
      m.lockStunsRec ?? 0,
      m.score ?? 0,
      m.rounds ?? 0,
      0,
      m.wins ?? 0,
      m.time ?? 0,
    ];
    const value = values[column] ?? 0;
    // Column 3 (score) is the one signed column; everything else saturates.
    return column === SCORE_COLUMN
      ? value | 0
      : Math.min(value >>> 0, 0xffffffff) >>> 0;
  } catch {
    return 0;
  }
}

/** One 73-slot 0x4107 record; the slot array is 1-based, so slot 0 is skipped. */
function writeScoreRecord(
  writer: PacketWriter,
  stats: CharacterStatsRow | null,
  cumulative: boolean,
): void {
  for (let slot = 1; slot <= TAIL_RECORD_INTS; slot++) {
    writer.writeUint32(readTailSlot(stats, slot, cumulative));
  }
}

function readTailSlot(
  stats: CharacterStatsRow | null,
  slot: number,
  _cumulative: boolean,
): number {
  if (!stats) return 0;
  // Slot mapping [UNKNOWN beyond the reference's zeros]; honest zeros.
  void slot;
  return 0;
}
