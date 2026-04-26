import { injectable, inject } from "@needle-di/core";
import { eq } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import { characterStatsTable } from "../../db/schema.ts";
import type { CharacterStats, NewCharacterStats } from "../../db/schema.ts";
import { MODE_COLUMN } from "../../core/constants/game-mode-constants.ts";

const DEFAULT_MODE_STATS = JSON.stringify({
  wins: 0, rounds: 0, score: 0, time: 0,
  kills: 0, deaths: 0, stuns: 0, stunsRec: 0,
  hsKills: 0, hsDeaths: 0, hsStuns: 0, hsStunsRec: 0,
  lockKills: 0, lockDeaths: 0, lockStuns: 0, lockStunsRec: 0,
});

export interface RoundStats {
  kills: number;
  deaths: number;
  wins: number;
  stuns: number;
  stunsReceived: number;
  stunsFriendly: number;
  headshotKills: number;
  headshotDeaths: number;
  headshotStuns: number;
  headshotStunsReceived: number;
  lockKills: number;
  lockDeaths: number;
  lockStuns: number;
  lockStunsReceived: number;
  consecutiveKills: number;
  consecutiveDeaths: number;
  consecutiveHeadshots: number;
  suicides: number;
  score: number;
  experience: number;
  rolls: number;
  cqcGiven: number;
  cqcTaken: number;
  knifeKills: number;
  knifeStuns: number;
  teamKills: number;
  melee: number;
  meleeRec: number;
  radio: number;
  chat: number;
  salutes: number;
  spotted: number;
  selfSpotted: number;
  basesCaptured: number;
  basesDestroyed: number;
  bombDisarms: number;
  gakoSaved: number;
  gakoDefended: number;
  gakoFirst: number;
  raceCheckpoints: number;
  boxUses: number;
  boxTime: number;
  time: number;
  pointsAssist: number;
  pointsBase: number;
  wakeups: number;
  boosts: number;
  scans: number;
  evgTime: number;
  aborted: boolean;
  gameMode: number;
}

@injectable()
export class CharacterStatsService {
  constructor(private readonly databaseService = inject(DatabaseService)) {}

  public async findByCharacterId(characterId: number): Promise<CharacterStats | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select()
      .from(characterStatsTable)
      .where(eq(characterStatsTable.character_id, characterId))
      .limit(1);
    return rows[0] ?? null;
  }

  public async getOrCreate(characterId: number): Promise<CharacterStats> {
    const existing = await this.findByCharacterId(characterId);
    if (existing) return existing;

    const db = this.databaseService.get();
    const [created] = await db
      .insert(characterStatsTable)
      .values({
        character_id: characterId,
        stats_dm: DEFAULT_MODE_STATS,
        stats_tdm: DEFAULT_MODE_STATS,
        stats_res: DEFAULT_MODE_STATS,
        stats_cap: DEFAULT_MODE_STATS,
        stats_base: DEFAULT_MODE_STATS,
        stats_bomb: DEFAULT_MODE_STATS,
        stats_sne: DEFAULT_MODE_STATS,
        stats_tsne: DEFAULT_MODE_STATS,
        stats_sdm: DEFAULT_MODE_STATS,
        stats_scap: DEFAULT_MODE_STATS,
        stats_race: DEFAULT_MODE_STATS,
      } satisfies NewCharacterStats)
      .returning();
    return created;
  }

  public async applyRoundStats(characterId: number, round: RoundStats): Promise<void> {
    const stats = await this.getOrCreate(characterId);
    const db = this.databaseService.get();

    const modeCol = MODE_COLUMN[round.gameMode] ?? "stats_dm";
    const currentModeJson = (stats[modeCol] as string | null) ?? DEFAULT_MODE_STATS;
    const modeStats = JSON.parse(currentModeJson);

    modeStats.wins = (modeStats.wins ?? 0) + round.wins;
    modeStats.rounds = (modeStats.rounds ?? 0) + 1;
    modeStats.score = (modeStats.score ?? 0) + round.score;
    modeStats.time = (modeStats.time ?? 0) + round.time;
    modeStats.kills = (modeStats.kills ?? 0) + round.kills;
    modeStats.deaths = (modeStats.deaths ?? 0) + round.deaths;
    modeStats.stuns = (modeStats.stuns ?? 0) + round.stuns;
    modeStats.stunsRec = (modeStats.stunsRec ?? 0) + round.stunsReceived;
    modeStats.hsKills = (modeStats.hsKills ?? 0) + round.headshotKills;
    modeStats.hsDeaths = (modeStats.hsDeaths ?? 0) + round.headshotDeaths;
    modeStats.hsStuns = (modeStats.hsStuns ?? 0) + round.headshotStuns;
    modeStats.hsStunsRec = (modeStats.hsStunsRec ?? 0) + round.headshotStunsReceived;
    modeStats.lockKills = (modeStats.lockKills ?? 0) + round.lockKills;
    modeStats.lockDeaths = (modeStats.lockDeaths ?? 0) + round.lockDeaths;
    modeStats.lockStuns = (modeStats.lockStuns ?? 0) + round.lockStuns;
    modeStats.lockStunsRec = (modeStats.lockStunsRec ?? 0) + round.lockStunsReceived;

    await db
      .update(characterStatsTable)
      .set({
        kills: stats.kills + round.kills,
        deaths: stats.deaths + round.deaths,
        wins: stats.wins + round.wins,
        score: stats.score + round.score,
        rounds: stats.rounds + 1,
        stuns: stats.stuns + round.stuns,
        stuns_received: stats.stuns_received + round.stunsReceived,
        stuns_friendly: stats.stuns_friendly + round.stunsFriendly,
        headshot_kills: stats.headshot_kills + round.headshotKills,
        headshot_deaths: stats.headshot_deaths + round.headshotDeaths,
        headshot_stuns: stats.headshot_stuns + round.headshotStuns,
        headshot_stuns_received: stats.headshot_stuns_received + round.headshotStunsReceived,
        lock_kills: stats.lock_kills + round.lockKills,
        lock_deaths: stats.lock_deaths + round.lockDeaths,
        lock_stuns: stats.lock_stuns + round.lockStuns,
        lock_stuns_received: stats.lock_stuns_received + round.lockStunsReceived,
        consecutive_kills: Math.max(stats.consecutive_kills, round.consecutiveKills),
        consecutive_deaths: Math.max(stats.consecutive_deaths, round.consecutiveDeaths),
        consecutive_headshots: Math.max(stats.consecutive_headshots, round.consecutiveHeadshots),
        suicides: stats.suicides + round.suicides,
        salutes: stats.salutes + round.salutes,
        radio: stats.radio + round.radio,
        chat: stats.chat + round.chat,
        cqc_given: stats.cqc_given + round.cqcGiven,
        cqc_taken: stats.cqc_taken + round.cqcTaken,
        rolls: stats.rolls + round.rolls,
        melee: stats.melee + round.melee,
        melee_rec: stats.melee_rec + round.meleeRec,
        knife_kills: stats.knife_kills + round.knifeKills,
        knife_stuns: stats.knife_stuns + round.knifeStuns,
        team_kills: stats.team_kills + round.teamKills,
        spotted: stats.spotted + round.spotted,
        self_spotted: stats.self_spotted + round.selfSpotted,
        bases_captured: stats.bases_captured + round.basesCaptured,
        bases_destroyed: stats.bases_destroyed + round.basesDestroyed,
        bomb_disarms: stats.bomb_disarms + round.bombDisarms,
        gako_saved: stats.gako_saved + round.gakoSaved,
        gako_defended: stats.gako_defended + round.gakoDefended,
        gako_first: stats.gako_first + round.gakoFirst,
        race_checkpoints: stats.race_checkpoints + round.raceCheckpoints,
        box_uses: stats.box_uses + round.boxUses,
        box_time: stats.box_time + round.boxTime,
        boosts: stats.boosts + round.boosts,
        scans: stats.scans + round.scans,
        evg_time: stats.evg_time + round.evgTime,
        wakeups: stats.wakeups + round.wakeups,
        points_assist: stats.points_assist + round.pointsAssist,
        points_base: stats.points_base + round.pointsBase,
        time: stats.time + round.time,
        [modeCol]: JSON.stringify(modeStats),
        last_updated: Math.floor(Date.now() / 1000),
      })
      .where(eq(characterStatsTable.character_id, characterId));
  }

  public getModeStats(stats: CharacterStats, mode: number): Record<string, number> {
    const col = MODE_COLUMN[mode] ?? "stats_dm";
    const json = (stats[col] as string | null) ?? DEFAULT_MODE_STATS;
    try {
      return JSON.parse(json);
    } catch {
      return JSON.parse(DEFAULT_MODE_STATS);
    }
  }
}
