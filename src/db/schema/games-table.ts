import { integer, pgTable, serial, text, timestamp, varchar } from "drizzle-orm/pg-core";
import { sql } from "drizzle-orm";
import { charactersTable } from "./characters-table.ts";
import { lobbiesTable } from "./lobbies-table.ts";

export const gamesTable = pgTable("games", {
  id: serial("id").primaryKey(),
  host_id: integer("host_id").notNull().references(() => charactersTable.id),
  lobby_id: integer("lobby_id").notNull().references(() => lobbiesTable.id),
  name: varchar("name", { length: 16 }).notNull(),
  password: varchar("password", { length: 15 }).notNull().default(""),
  comment: varchar("comment", { length: 128 }).notNull().default(""),
  max_players: integer("max_players").notNull().default(8),
  current_game: integer("current_game").notNull().default(0),
  games: text("games").notNull().default("[]"),
  stance: integer("stance").notNull().default(0),
  ping: integer("ping").notNull().default(0),
  common: text("common").notNull().default("{}"),
  rules: text("rules").notNull().default("{}"),
  status: integer("status").notNull().default(0),
  created_at: timestamp("created_at").default(sql`now()`),
});

export type Game = typeof gamesTable.$inferSelect;
export type NewGame = typeof gamesTable.$inferInsert;
