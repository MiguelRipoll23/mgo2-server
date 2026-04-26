import { customType, integer, pgTable, serial, timestamp, varchar } from "drizzle-orm/pg-core";
import type { AnyPgColumn } from "drizzle-orm/pg-core";
import { sql } from "drizzle-orm";
import { charactersTable } from "./characters-table.ts";

const bytea = customType<{ data: Uint8Array; driverData: Uint8Array }>({
  dataType() {
    return "bytea";
  },
});

export const clansTable = pgTable("clans", {
  id: serial("id").primaryKey(),
  name: varchar("name", { length: 15 }).notNull().unique(),
  leader_id: integer("leader_id").references((): AnyPgColumn => clanMembersTable.id),
  comment: varchar("comment", { length: 128 }).notNull().default(""),
  notice: varchar("notice", { length: 512 }).notNull().default(""),
  notice_time: integer("notice_time").notNull().default(0),
  notice_writer_id: integer("notice_writer_id").references((): AnyPgColumn => clanMembersTable.id),
  emblem_editor_id: integer("emblem_editor_id").references((): AnyPgColumn => clanMembersTable.id),
  emblem: bytea("emblem"),
  emblem_wip: bytea("emblem_wip"),
  open: integer("open").notNull().default(1),
  created_at: timestamp("created_at").default(sql`now()`),
});

export type Clan = typeof clansTable.$inferSelect;
export type NewClan = typeof clansTable.$inferInsert;

export const clanMembersTable = pgTable("clans_members", {
  id: serial("id").primaryKey(),
  clan_id: integer("clan_id").notNull().references(() => clansTable.id),
  character_id: integer("character_id").notNull().references(() => charactersTable.id),
  rank: integer("rank").notNull().default(0),
}, (table) => [
  { name: "clan_members_unique", columns: [table.clan_id, table.character_id] },
]);

export type ClanMember = typeof clanMembersTable.$inferSelect;
export type NewClanMember = typeof clanMembersTable.$inferInsert;
