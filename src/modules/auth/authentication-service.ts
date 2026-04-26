import { injectable, inject } from "@needle-di/core";
import { DatabaseService } from "../../core/services/database-service.ts";
import { usersTable } from "../../db/schema.ts";
import type { User } from "../../db/schema.ts";
import { SessionsService } from "./sessions-service.ts";
import { and, eq } from "drizzle-orm";

const FULL_PERKS = Array(10).fill("1000000").join("_");

@injectable()
export class AuthenticationService {
  constructor(
    private readonly databaseService = inject(DatabaseService),
    private readonly sessionsService = inject(SessionsService),
  ) {}

  public async findByCredentials(
    displayName: string,
    passwordHash: string,
  ): Promise<User | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select()
      .from(usersTable)
      .where(
        and(
          eq(usersTable.display_name, displayName),
          eq(usersTable.password, passwordHash),
        ),
      )
      .limit(1);
    return rows[0] ?? null;
  }

  public async findById(userId: number): Promise<User | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select()
      .from(usersTable)
      .where(eq(usersTable.id, userId))
      .limit(1);
    return rows[0] ?? null;
  }

  public async login(
    displayName: string,
    passwordHash: string,
  ): Promise<string> {
    const user = await this.findByCredentials(displayName, passwordHash);

    if (!user) {
      return "1,0,0,0000000000000000";
    }

    const tokenBytes = new Uint8Array(16);
    crypto.getRandomValues(tokenBytes);

    const sessionToken = Array.from(tokenBytes)
      .map((byte) => byte.toString(16).padStart(2, "0"))
      .join("");

    const databaseHexToken = sessionToken.slice(0, 8);
    const clientHexToken = sessionToken.slice(0, 16);

    await this.sessionsService.createSession(user.id, databaseHexToken);

    return `0,${user.id},${FULL_PERKS},${clientHexToken}`;
  }
}
