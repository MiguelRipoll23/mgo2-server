import { injectable, inject } from "@needle-di/core";
import { and, asc, eq, inArray, sql } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import {
  gamesTable,
  gamePlayersTable,
  gameRoundsTable,
  hostReviewsTable,
} from "../../db/schema.ts";
import type { Game, NewGame } from "../../db/schema.ts";

export interface ConnectionInfo {
  publicIp: string;
  publicPort: number;
  privateIp: string;
  privatePort: number;
}

/**
 * Peer-to-peer endpoints registered by hosts via 0x4700, keyed by character.
 * In-memory: an endpoint is only meaningful while its host is connected, and a
 * stale row after a crash would hand joiners an unreachable address.
 */
const connectionInfos = new Map<number, ConnectionInfo>();

/** The 1..5 star range the client's own picker sends; anything else is a mis-parse. */
export const MIN_HOST_RATING = 1;
export const MAX_HOST_RATING = 5;

@injectable()
export class GameService {
  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  async findAll(): Promise<Game[]> {
    return await this.db.select().from(gamesTable).orderBy(asc(gamesTable.id));
  }

  async findByLobby(lobbyId: number): Promise<Game[]> {
    return await this.db.select().from(gamesTable).where(eq(gamesTable.lobby_id, lobbyId));
  }

  async findById(gameId: number): Promise<Game | null> {
    const rows = await this.db
      .select()
      .from(gamesTable)
      .where(eq(gamesTable.id, gameId))
      .limit(1);
    return rows[0] ?? null;
  }

  async create(data: NewGame): Promise<Game> {
    const rows = await this.db.insert(gamesTable).values(data).returning();
    return rows[0];
  }

  async update(gameId: number, data: Partial<Game>): Promise<Game | null> {
    const rows = await this.db.update(gamesTable).set(data).where(eq(gamesTable.id, gameId)).returning();
    return rows[0] ?? null;
  }

  async delete(gameId: number): Promise<void> {
    // Roster and round-snapshot rows cascade with the game; votes outlive it.
    await this.db.delete(gamesTable).where(eq(gamesTable.id, gameId));
  }

  async clearLobbyGames(lobbyId: number): Promise<void> {
    await this.db.delete(gamesTable).where(eq(gamesTable.lobby_id, lobbyId));
  }

  // ── Peer-to-peer endpoints (0x4700 → 0x4321 handoff) ──────────────────────

  saveConnectionInfo(characterId: number, info: ConnectionInfo): void {
    connectionInfos.set(characterId, info);
  }

  getConnectionInfo(characterId: number): ConnectionInfo | null {
    return connectionInfos.get(characterId) ?? null;
  }

  // ── In-game roster (DB-backed: pings, teams, round attribution) ───────────

  /** Inserts the roster row; the host is always a member of its own game. */
  async addPlayer(gameId: number, characterId: number): Promise<void> {
    if (characterId <= 0) return;
    await this.db
      .insert(gamePlayersTable)
      .values({ game_id: gameId, character_id: characterId })
      .onConflictDoNothing();
  }

  async removePlayer(gameId: number, characterId: number): Promise<void> {
    await this.db
      .delete(gamePlayersTable)
      .where(
        and(
          eq(gamePlayersTable.game_id, gameId),
          eq(gamePlayersTable.character_id, characterId),
        ),
      );
  }

  /** The full roster, host first — the ordering the player list expects. */
  async getPlayers(gameId: number, hostCharacterId: number): Promise<number[]> {
    const rows = await this.db
      .select({ characterId: gamePlayersTable.character_id })
      .from(gamePlayersTable)
      .where(eq(gamePlayersTable.game_id, gameId));

    const players = rows
      .map((row) => row.characterId)
      .filter((id) => id !== hostCharacterId);
    return hostCharacterId > 0 ? [hostCharacterId, ...players] : players;
  }

  async countPlayers(gameId: number): Promise<number> {
    const rows = await this.db
      .select({ count: sql<number>`count(*)::int` })
      .from(gamePlayersTable)
      .where(eq(gamePlayersTable.game_id, gameId));
    return rows[0]?.count ?? 0;
  }

  /** Per-player pings for the game-details player entries. */
  async getPlayerPings(gameId: number): Promise<Map<number, number>> {
    const rows = await this.db
      .select({
        characterId: gamePlayersTable.character_id,
        ping: gamePlayersTable.ping,
      })
      .from(gamePlayersTable)
      .where(eq(gamePlayersTable.game_id, gameId));
    return new Map(rows.map((row) => [row.characterId, row.ping]));
  }

  /**
   * The host's latency report (0x4398): its own ping onto the game row (shown
   * in the browser), each player's onto their roster row (shown in the player
   * list). Zero-id entries are skipped.
   */
  async updatePings(
    gameId: number,
    hostPing: number,
    playerPings: Map<number, number>,
  ): Promise<void> {
    await this.db
      .update(gamesTable)
      .set({ ping: hostPing })
      .where(eq(gamesTable.id, gameId));

    for (const [characterId, ping] of playerPings) {
      if (characterId <= 0) continue;
      await this.db
        .update(gamePlayersTable)
        .set({ ping: Math.max(0, Math.min(0xffff, ping)) })
        .where(
          and(
            eq(gamePlayersTable.game_id, gameId),
            eq(gamePlayersTable.character_id, characterId),
          ),
        );
    }
  }

  // ── Round snapshot (attribution for host-reported stats) ──────────────────

  /**
   * Snapshots the roster at round start. Insert-only, no delete: rows
   * accumulate everyone who has been in the game, so a mid-round quitter's
   * stats still apply.
   */
  async markRoundPlayers(gameId: number): Promise<void> {
    const roster = await this.db
      .select({ characterId: gamePlayersTable.character_id })
      .from(gamePlayersTable)
      .where(eq(gamePlayersTable.game_id, gameId));

    if (roster.length === 0) return;
    await this.db
      .insert(gameRoundsTable)
      .values(
        roster.map((row) => ({ game_id: gameId, character_id: row.characterId })),
      )
      .onConflictDoNothing();
  }

  /** Whether this character was in the game when a round started. */
  async playedLastRound(gameId: number, characterId: number): Promise<boolean> {
    const rows = await this.db
      .select({ characterId: gameRoundsTable.character_id })
      .from(gameRoundsTable)
      .where(
        and(
          eq(gameRoundsTable.game_id, gameId),
          eq(gameRoundsTable.character_id, characterId),
        ),
      )
      .limit(1);
    return rows.length > 0;
  }

  /**
   * Roster-or-round-snapshot membership. This is the attribution check for
   * host-reported stats (0x43a4/0x4390/0x43a2): the host reports for everyone,
   * so the character id on the wire — not the connection — must be validated
   * against the game rather than trusted.
   */
  async isInGame(
    gameId: number,
    hostCharacterId: number,
    characterId: number,
  ): Promise<boolean> {
    if (characterId <= 0) return false;
    if (characterId === hostCharacterId) return true;
    if (await this.countRosterMembership(gameId, characterId)) return true;
    return await this.playedLastRound(gameId, characterId);
  }

  private async countRosterMembership(
    gameId: number,
    characterId: number,
  ): Promise<boolean> {
    const rows = await this.db
      .select({ characterId: gamePlayersTable.character_id })
      .from(gamePlayersTable)
      .where(
        and(
          eq(gamePlayersTable.game_id, gameId),
          eq(gamePlayersTable.character_id, characterId),
        ),
      )
      .limit(1);
    return rows.length > 0;
  }

  // ── Host ratings (0x43c4 → 0x4313/0x4302 score & votes) ───────────────────

  /**
   * Stores a host-rating vote, returning false when it was refused: a host
   * cannot vote for themselves, and the one-vote-per-game constraint absorbs
   * a retry silently.
   */
  async recordHostVote(
    gameId: number,
    hostCharacterId: number,
    voterCharacterId: number,
    rating: number,
  ): Promise<boolean> {
    if (hostCharacterId === voterCharacterId) return false;
    if (rating < MIN_HOST_RATING || rating > MAX_HOST_RATING) return false;

    const rows = await this.db
      .insert(hostReviewsTable)
      .values({
        game_id: gameId,
        host_character_id: hostCharacterId,
        voter_character_id: voterCharacterId,
        rating,
      })
      .onConflictDoNothing()
      .returning();
    return rows.length > 0;
  }

  /** Whether this player has already rated the host of this game. */
  async hasRatedHostOf(
    gameId: number,
    voterCharacterId: number,
  ): Promise<boolean> {
    const rows = await this.db
      .select({ voterId: hostReviewsTable.voter_character_id })
      .from(hostReviewsTable)
      .where(
        and(
          eq(hostReviewsTable.game_id, gameId),
          eq(hostReviewsTable.voter_character_id, voterCharacterId),
        ),
      )
      .limit(1);
    return rows.length > 0;
  }

  /**
   * Per-host rating aggregates: {u32 ratingSum, u32 votes} keyed by host
   * character id, for the browser entries and the game-details screen. Votes
   * are the host's lifetime history, not per-game.
   */
  async getHostRatingSums(
    hostCharacterIds: number[],
  ): Promise<Map<number, { ratingSum: number; votes: number }>> {
    const result = new Map<number, { ratingSum: number; votes: number }>();
    if (hostCharacterIds.length === 0) return result;

    const rows = await this.db
      .select({
        hostId: hostReviewsTable.host_character_id,
        ratingSum: sql<number>`coalesce(sum(${hostReviewsTable.rating}), 0)::int`,
        votes: sql<number>`count(*)::int`,
      })
      .from(hostReviewsTable)
      .where(inArray(hostReviewsTable.host_character_id, hostCharacterIds))
      .groupBy(hostReviewsTable.host_character_id);

    for (const row of rows) {
      result.set(row.hostId, { ratingSum: row.ratingSum, votes: row.votes });
    }
    return result;
  }

  /** The game this character is currently in (any slot), or null. */
  async gameContaining(characterId: number): Promise<Game | null> {
    if (characterId <= 0) return null;
    const rows = await this.db
      .select({ game: gamesTable })
      .from(gamePlayersTable)
      .innerJoin(gamesTable, eq(gamePlayersTable.game_id, gamesTable.id))
      .where(eq(gamePlayersTable.character_id, characterId))
      .limit(1);
    return rows[0]?.game ?? null;
  }
}
