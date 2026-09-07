import { injectable, inject } from "@needle-di/core";
import { DatabaseService } from "../../core/services/database-service.ts";
import { roundReportsTable, weaponTalliesTable } from "../../db/schema.ts";
import type { NewRoundReport, NewWeaponTally } from "../../db/schema.ts";
import { and, desc, eq, inArray, sql } from "drizzle-orm";

/** One "met players" row for 0x4680, derived from round reports at query time. */
export interface MetPlayer {
  charaId: number;
  name: string;
  lastMetEpochSeconds: number;
  lobbySubtype: number;
}

@injectable()
export class RoundReportService {
  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  async insert(report: NewRoundReport): Promise<void> {
    await this.db.insert(roundReportsTable).values(report);
  }

  async insertTallies(tallies: NewWeaponTally[]): Promise<void> {
    if (tallies.length === 0) return;
    await this.db.insert(weaponTalliesTable).values(tallies);
  }

  /**
   * Players who shared a game with the viewer, newest encounter first —
   * distinct target characters of round reports in games the viewer also
   * appears in (either side of a report counts as "met").
   */
  async metPlayers(viewerCharacterId: number, limit: number): Promise<MetPlayer[]> {
    const rows = await this.db
      .select({
        charaId: sql<number>`other.character_id`,
        name: sql<string>`c.name`,
        lastMet: sql<number>`max(r.created_at)`,
        lobbySubtype: sql<number>`max(r.lobby_subtype)::int`,
      })
      .from(sql`(
        select target_character_id as character_id, game_id, created_at, lobby_subtype
        from round_reports
        where host_character_id = ${viewerCharacterId}
        union all
        select host_character_id as character_id, game_id, created_at, lobby_subtype
        from round_reports
        where target_character_id = ${viewerCharacterId}
      ) as other`)
      .innerJoin(sql`round_reports r`, sql`r.game_id = other.game_id`)
      .innerJoin(sql`characters c`, sql`c.id = other.character_id`)
      .where(
        sql`(r.target_character_id = ${viewerCharacterId} or r.host_character_id = ${viewerCharacterId}) and other.character_id <> ${viewerCharacterId}`,
      )
      .groupBy(sql`other.character_id, c.name`)
      .orderBy(sql`max(r.created_at) desc`)
      .limit(limit);

    return rows.map((row) => ({
      charaId: row.charaId,
      name: row.name,
      lastMetEpochSeconds: Math.floor(new Date(row.lastMet).getTime() / 1000),
      lobbySubtype: row.lobbySubtype,
    }));
  }

  /** Whether any round report names this character in this game. */
  async playedIn(gameId: number, characterId: number): Promise<boolean> {
    const rows = await this.db
      .select({ id: roundReportsTable.id })
      .from(roundReportsTable)
      .where(
        and(
          eq(roundReportsTable.game_id, gameId),
          eq(roundReportsTable.target_character_id, characterId),
        ),
      )
      .limit(1);
    return rows.length > 0;
  }

  /** The game ids this character appears in — used to scope history queries. */
  async gameIdsFor(characterId: number, limit: number): Promise<number[]> {
    const rows = await this.db
      .select({ gameId: roundReportsTable.game_id })
      .from(roundReportsTable)
      .where(
        sql`${roundReportsTable.target_character_id} = ${characterId} or ${roundReportsTable.host_character_id} = ${characterId}`,
      )
      .orderBy(desc(roundReportsTable.created_at))
      .limit(limit);
    return [...new Set(rows.map((row) => row.gameId))];
  }

  /** Games the character appears in, filtered to a set (cascade-safe). */
  async gameIdsIn(characterId: number, gameIds: number[]): Promise<number[]> {
    if (gameIds.length === 0) return [];
    const rows = await this.db
      .select({ gameId: roundReportsTable.game_id })
      .from(roundReportsTable)
      .where(
        and(
          inArray(roundReportsTable.game_id, gameIds),
          sql`${roundReportsTable.target_character_id} = ${characterId} or ${roundReportsTable.host_character_id} = ${characterId}`,
        ),
      );
    return [...new Set(rows.map((row) => row.gameId))];
  }
}
