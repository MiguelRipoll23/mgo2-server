import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { CharacterStatsService } from "../../../../../../modules/character/character-stats-service.ts";
import { RoundReportService } from "../../../../../../modules/game/round-report-service.ts";
import type { RoundStats } from "../../../../../../modules/character/character-stats-service.ts";

// ── End-of-round stat submission ────────────────────────────────────────────
//
// The 167-byte host frame (0x4390) is capture-confirmed in the reference
// server's PROTOCOL.md: the TARGET character id is ON THE WIRE at 0x00 — the
// host reports every player as they leave, one packet per player — and the
// absolute total experience sits at 0x27. Wire 0x23 is {u16 team-win flag,
// u16 seconds}, not a u32. Struct A is 15 s16 counters at 0x05 + 2*i with
// fixed labels (A0 kills, A1 deaths, A3 score, A4 stuns, A6 headshot kills,
// A7 headshot deaths); struct B (58 s16 at 0x2F) and the trailing u32 at 0xA3
// exist only in the long form.
//
// The report is applied ONLY when the target verifiably played: a current
// roster member, or a member of the round snapshot taken at 0x43c8/0x43ca —
// without that check a host could attribute stats to any character id it
// liked. The frame is also stored whole as a round_report row, which is what
// match history (0x4680) and the post-game screen derive from.
//
// 0x4350 is the same frame sent by a non-host client for itself; the target
// is the connection's own character.

const TARGET_OFFSET = 0x00;
const KILLS_OFFSET = 0x05; // struct A index 0
const TEAM_WIN_OFFSET = 0x23;
const SECONDS_OFFSET = 0x25;
const EXPERIENCE_OFFSET = 0x27;
const ABORTED_OFFSET = 0xb7; // Nomad's longer build layout; read only if present

@injectable()
@GameCommandHandler(0x4350)
export class UpdateStatsHandler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private characterService = inject(CharacterService),
    private statsService = inject(CharacterStatsService),
    private reportService = inject(RoundReportService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    await processStatsPacket(
      session,
      packet,
      this.gameService,
      this.characterService,
      this.statsService,
      this.reportService,
    );
    await sendResult(session, 0x4351, RESULT_NONE);
  }
}

@injectable()
@GameCommandHandler(0x4390)
export class HostUpdateStatsHandler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private characterService = inject(CharacterService),
    private statsService = inject(CharacterStatsService),
    private reportService = inject(RoundReportService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    await processStatsPacket(
      session,
      packet,
      this.gameService,
      this.characterService,
      this.statsService,
      this.reportService,
    );
    await sendResult(session, 0x4391, RESULT_NONE);
  }
}

async function processStatsPacket(
  session: TcpSession,
  packet: Packet,
  gameService: GameService,
  characterService: CharacterService,
  statsService: CharacterStatsService,
  reportService: RoundReportService,
): Promise<void> {
  const reporterId = session.characterId;
  if (reporterId === null) return;

  const payload = packet.payload;
  if (payload.length < EXPERIENCE_OFFSET + 4) return; // shorter than the layout

  const view = new DataView(payload.buffer, payload.byteOffset);
  const s16 = (offset: number): number =>
    offset + 2 <= payload.length ? view.getInt16(offset, false) : 0;

  const gameId = session.gameId;
  const game = gameId !== null ? await gameService.findById(gameId) : null;

  // Target attribution: on the wire for the host frame; the reporter
  // themselves for 0x4350.
  const targetId = payload.length >= TARGET_OFFSET + 4
    ? view.getUint32(TARGET_OFFSET, false)
    : reporterId;
  const isHostFrame = targetId !== reporterId;

  // Reporting-model tripwire (reference PROTOCOL.md): only hosts have ever
  // sent reports. A non-host 0x4390 means the model needs revisiting.
  if (isHostFrame && (game === null || game.host_id !== reporterId)) {
    console.warn(
      `[tcp][game] 0x4390 from a NON-HOST connection (character ${reporterId}) — reporting-model deviation.`,
    );
  }

  const experience = view.getUint32(EXPERIENCE_OFFSET, false);
  const aborted = payload.length > ABORTED_OFFSET && payload[ABORTED_OFFSET] === 1;

  // Participation: current roster, or the round snapshot (mid-round quitters).
  const played = game !== null &&
    (await gameService.isInGame(game.id, game.host_id, targetId));

  if (game !== null && !played) {
    console.warn(
      `[tcp][game] game ${game.id}: stat report for character ${targetId} who neither is in the game nor played the round; dropped.`,
    );
    return;
  }

  const gameMode = await resolveGameMode(game);
  const round = parseStatsPayload(payload, gameMode, experience, aborted, s16);

  // The whole frame is stored, one round_report row per report — match
  // history derives from these rows. Stored even when the target's live
  // stats application is skipped, because the row IS the history.
  if (game !== null) {
    await reportService.insert({
      game_id: game.id,
      host_character_id: game.host_id,
      target_character_id: targetId,
      team_win: view.getUint16(TEAM_WIN_OFFSET, false),
      seconds: view.getUint16(SECONDS_OFFSET, false),
      experience,
      aborted,
      lobby_subtype: session.lobbyId !== null ? await lobbySubtype(gameService, session) : 0,
    });
  }

  await statsService.applyRoundStats(targetId, round);
  await characterService.addExperience(targetId, round.experience, round.aborted);

  const updatedStats = await statsService.findByCharacterId(targetId);
  if (updatedStats) {
    await characterService.updateRank(targetId, updatedStats);
  }
}

/** The lobby subtype of the session's lobby, stamped onto the report row. */
async function lobbySubtype(
  gameService: GameService,
  session: TcpSession,
): Promise<number> {
  if (session.lobbyId === null) return 0;
  const game = session.gameId !== null ? await gameService.findById(session.gameId) : null;
  if (game === null) return 0;
  // The lobby subtype comes from the lobbies table via the game's lobby_id.
  try {
    const lobby = await gameService.findLobby(game.lobby_id);
    return lobby?.subtypeId ?? 0;
  } catch {
    return 0;
  }
}

/**
 * Read the active game mode from the game's rotation config.
 * `games` is stored as a JSON array of [rule, map, flags] tuples;
 * `current_game` is the index into that array.
 */
async function resolveGameMode(game: Awaited<ReturnType<GameService["findById"]>>): Promise<number> {
  if (!game) return 0;
  try {
    const rounds: number[][] = JSON.parse(game.games);
    const entry = rounds[game.current_game];
    return Array.isArray(entry) ? (entry[0] ?? 0) : 0;
  } catch {
    return 0;
  }
}

/**
 * Parse the 0x4350 / 0x4390 stats payload in the reference layout: struct A
 * is 15 s16 counters at 0x05 + 2*i with capture-confirmed labels; the
 * absolute experience total arrives at 0x27.
 */
function parseStatsPayload(
  payload: Uint8Array,
  gameMode: number,
  experience: number,
  aborted: boolean,
  s16: (offset: number) => number,
): RoundStats {
  return {
    aborted,
    gameMode,
    // Struct A — capture-confirmed labels:
    wins:               s16(0x05 + 2 * 2),  // A2
    kills:              s16(0x05 + 2 * 0),  // A0
    deaths:             s16(0x05 + 2 * 1),  // A1
    stunsReceived:      s16(0x05 + 2 * 5),  // A5
    score:              s16(0x05 + 2 * 3),  // A3
    stuns:              s16(0x05 + 2 * 4),  // A4
    headshotKills:      s16(0x05 + 2 * 6),  // A6
    headshotDeaths:     s16(0x05 + 2 * 7),  // A7
    headshotStuns:      s16(0x05 + 2 * 8),  // A8
    headshotStunsReceived: s16(0x05 + 2 * 9), // A9
    lockKills:          s16(0x05 + 2 * 10), // A10
    lockDeaths:         s16(0x05 + 2 * 11), // A11
    lockStuns:          s16(0x05 + 2 * 12), // A12
    lockStunsReceived:  s16(0x05 + 2 * 13), // A13
    consecutiveKills:   s16(0x05 + 2 * 14), // A14
    rolls:              0,
    time:               s16(0x25),
    // Fields the 0x4390 frame does not carry — zeroed:
    experience,
    stunsFriendly:      0,
    consecutiveDeaths:  0,
    consecutiveHeadshots: 0,
    suicides:           0,
    cqcGiven:           0,
    cqcTaken:           0,
    knifeKills:         0,
    knifeStuns:         0,
    teamKills:          0,
    melee:              0,
    meleeRec:           0,
    radio:              0,
    chat:               0,
    salutes:            0,
    spotted:            0,
    selfSpotted:        0,
    basesCaptured:      0,
    basesDestroyed:     0,
    bombDisarms:        0,
    gakoSaved:          0,
    gakoDefended:       0,
    gakoFirst:          0,
    raceCheckpoints:    0,
    boxUses:            0,
    boxTime:            0,
    boosts:             0,
    scans:              0,
    evgTime:            0,
    wakeups:            0,
    pointsAssist:       0,
    pointsBase:         0,
  };
}
