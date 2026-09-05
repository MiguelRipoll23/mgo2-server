import { injectable, inject } from "@needle-di/core";
import { DatabaseService } from "../../core/services/database-service.ts";
import { usersTable } from "../../db/schema.ts";
import type { User } from "../../db/schema.ts";
import { SessionsService } from "./sessions-service.ts";
import { storeSessionField } from "../../core/tcp/utils/crypto-util.ts";
import { and, eq } from "drizzle-orm";

// The third field of the login reply. Its GRAMMAR differs between builds, and
// there is no value valid for both:
//  - 1.0 (disc) requires exactly ONE integer: its parser runs three strtol
//    calls each followed by a literal comma (disc 0xBB16B0) and fails unless a
//    comma follows the third integer.
//  - 1.36 requires TEN integers separated by NINE underscores, or an empty
//    field: its parser reads an integer then requires '_' (1.36 0xD6EE70);
//    seeing ',' it jumps to the failure label and the client raises
//    090B:00000001, "Unable to connect to server".
// The values below mirror the table the 1.36 binary itself ships at
// 0x122A5D0 — NOT the 1000000 upstream sends, which is 100-1000x them and
// also the wrong shape for this client.
const LOGIN_PERKS = "1000_1000_5000_10000_1000_3000_1000_1000_2000_1000";

// Length of the token the client keeps and derives its check-session field
// from. The reply carries these sixteen characters verbatim.
const LOGIN_TOKEN_LENGTH = 16;

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

    // The client keeps these sixteen characters and derives the value it will
    // present on check-session from them, so that DERIVED value is what the
    // session row stores — see deriveSessionField in crypto-util.
    const tokenBytes = new Uint8Array(LOGIN_TOKEN_LENGTH / 2);
    crypto.getRandomValues(tokenBytes);

    const sessionToken = Array.from(tokenBytes)
      .map((byte) => byte.toString(16).padStart(2, "0"))
      .join("");

    const storedField = storeSessionField(sessionToken);

    await this.sessionsService.createSession(user.id, storedField);

    return `0,${user.id},${LOGIN_PERKS},${sessionToken}`;
  }
}
