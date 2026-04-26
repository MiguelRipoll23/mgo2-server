import { integer, pgTable, serial, varchar } from "drizzle-orm/pg-core";
import type { AnyPgColumn } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";

export const usersTable = pgTable("users", {
  id: serial("id").primaryKey(),
  display_name: varchar("display_name", { length: 32 }).notNull().unique(),
  password: varchar("password", { length: 255 }).notNull(),
  role: integer("role").notNull().default(0),
  banned_until: integer("banned_until"),
  ban_reason: varchar("ban_reason", { length: 255 }),
  slots: integer("slots").notNull().default(3),
  current_character_id: integer("current_character_id").references((): AnyPgColumn => charactersTable.id),
  main_character_id: integer("main_character_id").references((): AnyPgColumn => charactersTable.id),
  main_exp: integer("main_exp").notNull().default(0),
  alt_exp: integer("alt_exp").notNull().default(0),
});

export type User = typeof usersTable.$inferSelect;
export type NewUser = typeof usersTable.$inferInsert;
