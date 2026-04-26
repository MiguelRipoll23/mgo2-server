import { integer, pgTable, serial, text, varchar } from "drizzle-orm/pg-core";
import type { AnyPgColumn } from "drizzle-orm/pg-core";
import { lobbiesTable } from "./lobbies-table.ts";
import { usersTable } from "./users-table.ts";

export const charactersTable = pgTable("characters", {
  id: serial("id").primaryKey(),
  user_id: integer("user_id").notNull().references((): AnyPgColumn => usersTable.id),
  name: varchar("name", { length: 16 }).notNull().unique(),
  old_name: varchar("old_name", { length: 16 }),
  rank: integer("rank").notNull().default(0),
  comment: varchar("comment", { length: 128 }).notNull().default(""),
  host_score: integer("host_score").notNull().default(0),
  host_votes: integer("host_votes").notNull().default(0),
  experience: integer("experience").notNull().default(0),
  gameplay_options: text("gameplay_options"),
  creation_time: integer("creation_time").notNull().default(0),
  active: integer("active").notNull().default(1),
  lobby_id: integer("lobby_id").references(() => lobbiesTable.id),
});

export type Character = typeof charactersTable.$inferSelect;
export type NewCharacter = typeof charactersTable.$inferInsert;

export * from "./character-details-table.ts";
