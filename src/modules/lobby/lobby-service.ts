import { inject, injectable } from "@needle-di/core";
import { asc, eq, gt, sql, sum } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import { lobbiesTable, lobbyInstanceCountsTable, LobbyType } from "../../db/schema.ts";
import type { Lobby, NewLobby } from "../../db/schema.ts";
import { ServerError } from "../../core/errors/server-error.ts";

export interface LobbyResponse {
  id: number;
  typeId: LobbyType;
  subtypeId: number;
  name: string;
  ipAddress: string;
  port: number;
  playersCount: number;
  beginnerOnly: boolean;
  expansionOnly: boolean;
  noHeadshot: boolean;
  replaysOnly: boolean;
}

export interface LobbyCreateInput {
  typeId: LobbyType;
  subtypeId?: number;
  name: string;
  ipAddress: string;
  port: number;
  playersCount?: number;
}

const LISTENING_IP = Deno.env.get("LISTENING_IP") ?? "0.0.0.0";
const OVERRIDE_IP =
  LISTENING_IP !== "0.0.0.0" &&
  LISTENING_IP !== "127.0.0.1" &&
  LISTENING_IP !== "localhost"
    ? LISTENING_IP
    : null;

// Stale threshold: instances that haven't reported in 2× the cron interval are excluded.
// Default cron is every 15 minutes → 30 minutes stale threshold.
const STALE_SECONDS = (() => {
  const expr = Deno.env.get("LOBBIES_REFRESH_CRON") ?? "*/15 * * * *";
  const m = expr.match(/\*\/(\d+)/);
  const intervalMinutes = m ? parseInt(m[1], 10) : 15;
  return intervalMinutes * 2 * 60;
})();

@injectable()
export class LobbyService {
  private cache: Lobby[] | null = null;

  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  setCache(rows: Lobby[]): void {
    this.cache = rows;
  }

  getCached(): Lobby[] {
    if (this.cache === null) {
      throw new Error("Lobby cache has not been initialized");
    }
    return this.cache;
  }

  async loadCache(instanceId?: string): Promise<void> {
    // Ordered by id, not name: the client expects list index and lobby type to coincide
    // (index 0 = gate, 1 = account, 2 = game). Ordering by name breaks the client.
    const rows = await this.db
      .select()
      .from(lobbiesTable)
      .orderBy(asc(lobbiesTable.id));

    if (instanceId) {
      // Sum player counts from all non-stale instances per lobby.
      const cutoff = new Date(Date.now() - STALE_SECONDS * 1000);
      const instanceSums = await this.db
        .select({
          lobbyId: lobbyInstanceCountsTable.lobbyId,
          total: sum(lobbyInstanceCountsTable.count).mapWith(Number),
        })
        .from(lobbyInstanceCountsTable)
        .where(gt(lobbyInstanceCountsTable.updatedAt, cutoff))
        .groupBy(lobbyInstanceCountsTable.lobbyId);

      const sumByLobbyId = new Map<number, number>(
        instanceSums.map((r) => [r.lobbyId, r.total ?? 0]),
      );

      this.cache = rows.map((lobby) => ({
        ...lobby,
        ipAddress: OVERRIDE_IP ?? lobby.ipAddress,
        playersCount: sumByLobbyId.get(lobby.id) ?? 0,
      }));
    } else {
      this.cache = OVERRIDE_IP
        ? rows.map((lobby) => ({ ...lobby, ipAddress: OVERRIDE_IP }))
        : rows;
    }
  }

  async findAll(): Promise<LobbyResponse[]> {
    const rows = await this.db
      .select()
      .from(lobbiesTable)
      .orderBy(asc(lobbiesTable.id));
    return rows.map((r) => this.toLobbyResponse(r));
  }

  async findById(lobbyId: number): Promise<LobbyResponse> {
    const rows = await this.db
      .select()
      .from(lobbiesTable)
      .where(eq(lobbiesTable.id, lobbyId))
      .limit(1);
    if (!rows[0]) {
      throw new ServerError("NOT_FOUND", "Lobby not found", 404);
    }
    return this.toLobbyResponse(rows[0]);
  }

  async create(data: LobbyCreateInput): Promise<LobbyResponse> {
    const [created] = await this.db.insert(lobbiesTable).values(
      this.fromLobbyInputCreate(data),
    ).returning();
    return this.toLobbyResponse(created);
  }

  async update(
    lobbyId: number,
    data: Partial<LobbyCreateInput>,
  ): Promise<LobbyResponse> {
    const [updated] = await this.db
      .update(lobbiesTable)
      .set(this.fromLobbyInputUpdate(data))
      .where(eq(lobbiesTable.id, lobbyId))
      .returning();
    if (!updated) {
      throw new ServerError("NOT_FOUND", "Lobby not found", 404);
    }
    return this.toLobbyResponse(updated);
  }

  async remove(lobbyId: number): Promise<void> {
    const deleted = await this.db
      .delete(lobbiesTable)
      .where(eq(lobbiesTable.id, lobbyId))
      .returning({ id: lobbiesTable.id });
    if (deleted.length === 0) {
      throw new ServerError("NOT_FOUND", "Lobby not found", 404);
    }
  }

  async updatePlayerCount(lobbyId: number, count: number): Promise<void> {
    await this.db
      .update(lobbiesTable)
      .set({ playersCount: count })
      .where(eq(lobbiesTable.id, lobbyId));
  }

  async upsertInstanceCount(
    instanceId: string,
    lobbyId: number,
    count: number,
  ): Promise<void> {
    await this.db
      .insert(lobbyInstanceCountsTable)
      .values({ instanceId, lobbyId, count, updatedAt: new Date() })
      .onConflictDoUpdate({
        target: [lobbyInstanceCountsTable.instanceId, lobbyInstanceCountsTable.lobbyId],
        set: { count, updatedAt: sql`now()` },
      });
  }

  private toLobbyResponse(lobby: Lobby): LobbyResponse {
    return {
      id: lobby.id,
      typeId: lobby.typeId,
      subtypeId: lobby.subtypeId,
      name: lobby.name,
      ipAddress: OVERRIDE_IP ?? lobby.ipAddress,
      port: lobby.port,
      playersCount: lobby.playersCount,
      beginnerOnly: lobby.beginnerOnly,
      expansionOnly: lobby.expansionOnly,
      noHeadshot: lobby.noHeadshot,
      replaysOnly: lobby.replaysOnly,
    };
  }

  private fromLobbyInputCreate(
    data: LobbyCreateInput,
  ): Omit<NewLobby, "id"> {
    return {
      typeId: data.typeId,
      name: data.name,
      ipAddress: data.ipAddress,
      port: data.port,
      playersCount: data.playersCount ?? 0,
      subtypeId: data.subtypeId ?? 1,
      beginnerOnly: false,
      expansionOnly: false,
      noHeadshot: false,
      replaysOnly: false,
    };
  }

  private fromLobbyInputUpdate(
    data: Partial<LobbyCreateInput>,
  ): Partial<Omit<NewLobby, "id">> {
    const result: Partial<Omit<NewLobby, "id">> = {};
    if (data.typeId !== undefined) result.typeId = data.typeId;
    if (data.name !== undefined) result.name = data.name;
    if (data.ipAddress !== undefined) result.ipAddress = data.ipAddress;
    if (data.port !== undefined) result.port = data.port;
    if (data.playersCount !== undefined) {
      result.playersCount = data.playersCount;
    }
    if (data.subtypeId !== undefined) result.subtypeId = data.subtypeId;
    return result;
  }
}
