import { injectable, inject } from "@needle-di/core";
import { DatabaseService } from "../../core/services/database-service.ts";
import { CryptoService } from "../../core/services/crypto-service.ts";
import { usersTable } from "../../db/schema.ts";
import { eq } from "drizzle-orm";
import { ServerError } from "../../core/errors/server-error.ts";

export interface RegisterResponse {
  id: number;
  displayName: string;
}

@injectable()
export class RegistrationService {
  constructor(
    private readonly dbs = inject(DatabaseService),
    private readonly cryptoService = inject(CryptoService),
  ) {}

  private get db() {
    return this.dbs.get();
  }

  async register(displayName: string, password: string): Promise<RegisterResponse> {
    const passwordHash = this.cryptoService.md5Hex(password);
    const existing = await this.db
      .select()
      .from(usersTable)
      .where(eq(usersTable.display_name, displayName))
      .limit(1);

    if (existing[0]) {
      throw new ServerError("CONFLICT", "Display name is already taken", 409);
    }

    const [created] = await this.db
      .insert(usersTable)
      .values({ display_name: displayName, password: passwordHash })
      .returning({
        id: usersTable.id,
        display_name: usersTable.display_name,
      });
    return this.toRegisterResponse(created);
  }

  private toRegisterResponse(row: { id: number; display_name: string }): RegisterResponse {
    return { id: row.id, displayName: row.display_name };
  }
}
