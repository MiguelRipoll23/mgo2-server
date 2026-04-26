import { injectable, inject } from "@needle-di/core";
import { asc, eq } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import { gamesTable } from "../../db/schema.ts";
import type { Game, NewGame } from "../../db/schema.ts";

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
    await this.db.delete(gamesTable).where(eq(gamesTable.id, gameId));
  }

  async clearLobbyGames(lobbyId: number): Promise<void> {
    await this.db.delete(gamesTable).where(eq(gamesTable.lobby_id, lobbyId));
  }
}

