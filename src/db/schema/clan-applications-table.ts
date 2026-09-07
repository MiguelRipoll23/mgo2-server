import { integer, pgTable, serial, timestamp, unique } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";
import { clansTable } from "./clans-table.ts";
import { sql } from "drizzle-orm";

/**
 * Pending applications to join a clan (0x4b42). The applicant list is served
 * on 0x4b73 and in the leader's clan-applications mailbox (0x4820 selector
 * 0x10); the leader judges each application with 0x4b30 (accept) or 0x4b32
 * (decline), which consume the row.
 */
export const clanApplicationsTable = pgTable(
  "clan_applications",
  {
    id: serial("id").primaryKey(),
    clan_id: integer("clan_id")
      .notNull()
      .references(() => clansTable.id, { onDelete: "cascade" }),
    character_id: integer("character_id")
      .notNull()
      .references(() => charactersTable.id, { onDelete: "cascade" }),
    applied_at: timestamp("applied_at").notNull().default(sql`now()`),
  },
  (table) => [unique("clan_applications_unique").on(table.clan_id, table.character_id)],
);

export type ClanApplication = typeof clanApplicationsTable.$inferSelect;
export type NewClanApplication = typeof clanApplicationsTable.$inferInsert;
