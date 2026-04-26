import { injectable, inject } from "@needle-di/core";
import { DatabaseService } from "../../core/services/database-service.ts";
import { sessionsTable } from "../../db/schema.ts";
import type { Session } from "../../db/schema.ts";
import { eq } from "drizzle-orm";

@injectable()
export class SessionsService {
  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  async findSessionByToken(token: string): Promise<Session | null> {
    const rows = await this.db
      .select()
      .from(sessionsTable)
      .where(eq(sessionsTable.token, token))
      .limit(1);
    return rows[0] ?? null;
  }

  async createSession(userId: number, token: string): Promise<Session> {
    const existing = await this.db
      .select()
      .from(sessionsTable)
      .where(eq(sessionsTable.user_id, userId))
      .limit(1);

    if (existing[0]) {
      const [updated] = await this.db
        .update(sessionsTable)
        .set({ token })
        .where(eq(sessionsTable.user_id, userId))
        .returning();
      return updated;
    }

    const [created] = await this.db
      .insert(sessionsTable)
      .values({ user_id: userId, token })
      .returning();

    return created;
  }
}
