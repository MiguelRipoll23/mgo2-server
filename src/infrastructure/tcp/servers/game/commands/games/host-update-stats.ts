import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { sendResult } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../../core/constants/error-codes-constants.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { CharacterStatsService } from "../../../../../../modules/character/character-stats-service.ts";
import type { RoundStats } from "../../../../../../modules/character/character-stats-service.ts";

/**
 * 0x4350 — client sends their own round stats (non-host).
 * Parses and persists the stats, then updates experience and animal rank.
 */
@injectable()
@GameCommandHandler(0x4350)
export class UpdateStatsHandler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private characterService = inject(CharacterService),
    private statsService = inject(CharacterStatsService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    await processStatsPacket(session, packet, this.gameService, this.characterService, this.statsService);
    await sendResult(session, 0x4351, RESULT_NONE);
  }
}

/**
 * 0x4390 — host sends aggregated round stats for all players.
 * Same parsing logic; applies stats for the host character.
 */
@injectable()
@GameCommandHandler(0x4390)
export class HostUpdateStatsHandler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private characterService = inject(CharacterService),
    private statsService = inject(CharacterStatsService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    await processStatsPacket(session, packet, this.gameService, this.characterService, this.statsService);
    await sendResult(session, 0x4391, RESULT_NONE);
  }
}

// ── Shared parsing + persistence logic ──────────────────────────────────────

async function processStatsPacket(
  session: TcpSession,
  packet: Packet,
  gameService: GameService,
  characterService: CharacterService,
  statsService: CharacterStatsService,
): Promise<void> {
  const characterId = session.characterId;
  if (characterId === null) return;

  const gameMode = await resolveGameMode(session, gameService);
  const round = parseStatsPayload(packet.payload, gameMode);

  await statsService.applyRoundStats(characterId, round);
  await characterService.addExperience(characterId, round.experience, round.aborted);

  const updatedStats = await statsService.findByCharacterId(characterId);
  if (updatedStats) {
    await characterService.updateRank(characterId, updatedStats);
  }
}

/**
 * Read the active game mode from the game's JSON config.
 * `games` is stored as a JSON array of [rule, map, flags] tuples.
 * `current_game` is the index into that array.
 */
async function resolveGameMode(session: TcpSession, gameService: GameService): Promise<number> {
  if (session.gameId === null) return 0;
  try {
    const game = await gameService.findById(session.gameId);
    if (!game) return 0;
    const rounds: number[][] = JSON.parse(game.games);
    const entry = rounds[game.current_game];
    return Array.isArray(entry) ? (entry[0] ?? 0) : 0;
  } catch {
    return 0;
  }
}

/**
 * Parse the 0x4350 / 0x4390 stats payload.
 * Values are single bytes at fixed little-endian offsets (from echo's StatsService).
 */
function parseStatsPayload(payload: Uint8Array, gameMode: number): RoundStats {
  const b = (offset: number): number =>
    offset < payload.length ? (payload[offset] & 0xff) : 0;

  const aborted = b(0) !== 0;

  return {
    aborted,
    gameMode,
    wins:                b(4),
    kills:               b(6),
    deaths:              b(8),
    stunsReceived:       b(10),
    score:               b(12),
    stuns:               b(14),
    headshotKills:       b(18),
    headshotDeaths:      b(20),
    headshotStuns:       b(22),
    headshotStunsReceived: b(24),
    rolls:               b(32),
    time:                b(38),
    lockKills:           b(40),
    lockDeaths:          b(42),
    lockStuns:           b(44),
    lockStunsReceived:   b(46),
    consecutiveKills:    b(48),
    // Fields not present in the 0x4350/0x4390 packet — zeroed
    experience:          0,
    stunsFriendly:       0,
    consecutiveDeaths:   0,
    consecutiveHeadshots: 0,
    suicides:            0,
    cqcGiven:            0,
    cqcTaken:            0,
    knifeKills:          0,
    knifeStuns:          0,
    teamKills:           0,
    melee:               0,
    meleeRec:            0,
    radio:               0,
    chat:                0,
    salutes:             0,
    spotted:             0,
    selfSpotted:         0,
    basesCaptured:       0,
    basesDestroyed:      0,
    bombDisarms:         0,
    gakoSaved:           0,
    gakoDefended:        0,
    gakoFirst:           0,
    raceCheckpoints:     0,
    boxUses:             0,
    boxTime:             0,
    boosts:              0,
    scans:               0,
    evgTime:             0,
    wakeups:             0,
    pointsAssist:        0,
    pointsBase:          0,
  };
}
