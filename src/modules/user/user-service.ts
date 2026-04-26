import { injectable, inject } from "@needle-di/core";
import { eq } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import { usersTable } from "../../db/schema.ts";
import type { User } from "../../db/schema.ts";

@injectable()
export class UserService {
  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  async findByDisplayName(displayName: string): Promise<User | null> {
    const rows = await this.db
      .select()
      .from(usersTable)
      .where(eq(usersTable.display_name, displayName))
      .limit(1);
    return rows[0] ?? null;
  }

  async createUser(data: {
    display_name: string;
    password: string;
  }): Promise<{ id: number; display_name: string }> {
    const [created] = await this.db
      .insert(usersTable)
      .values(data)
      .returning({
        id: usersTable.id,
        display_name: usersTable.display_name,
      });
    return created;
  }

  async findById(userId: number): Promise<User | null> {
    const rows = await this.db
      .select()
      .from(usersTable)
      .where(eq(usersTable.id, userId))
      .limit(1);
    return rows[0] ?? null;
  }

  async setCurrentCharacter(
    userId: number,
    characterId: number | null,
  ): Promise<void> {
    await this.db
      .update(usersTable)
      .set({ current_character_id: characterId })
      .where(eq(usersTable.id, userId));
  }

  async setMainCharacter(
    userId: number,
    characterId: number | null,
  ): Promise<void> {
    await this.db
      .update(usersTable)
      .set({ main_character_id: characterId })
      .where(eq(usersTable.id, userId));
  }
}


