import { boolean, integer, pgTable, serial, smallint, timestamp } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";
import { gamesTable } from "./games-table.ts";
import { sql } from "drizzle-orm";

/**
 * One host-reported round stat frame (0x4390) per row — the match-history
 * source. "Met players" (0x4680) derives at query time from rows that share a
 * game: distinct target characters of games the viewer also appears in,
 * newest encounter first. Game rows are deleted on teardown, so the report
 * carries its own lobby subtype for the 0x4682 type label.
 */
export const roundReportsTable = pgTable("round_reports", {
  id: serial("id").primaryKey(),
  game_id: integer("game_id")
    .notNull()
    .references(() => gamesTable.id, { onDelete: "cascade" }),
  host_character_id: integer("host_character_id")
    .notNull()
    .references(() => charactersTable.id),
  target_character_id: integer("target_character_id")
    .notNull()
    .references(() => charactersTable.id),
  team_win: smallint("team_win").notNull().default(0),
  seconds: integer("seconds").notNull().default(0),
  experience: integer("experience").notNull().default(0),
  aborted: boolean("aborted").notNull().default(false),
  lobby_subtype: smallint("lobby_subtype").notNull().default(0),
  created_at: timestamp("created_at").notNull().default(sql`now()`),
});

export type RoundReport = typeof roundReportsTable.$inferSelect;
export type NewRoundReport = typeof roundReportsTable.$inferInsert;

/**
 * End-of-round weapon tallies (0x43a2): {u8 weapon, u16 a, u16 b, u16 c} per
 * entry, up to 50 per report. Only stored — nothing derives from them yet.
 */
export const weaponTalliesTable = pgTable("weapon_tallies", {
  id: serial("id").primaryKey(),
  game_id: integer("game_id")
    .notNull()
    .references(() => gamesTable.id, { onDelete: "cascade" }),
  character_id: integer("character_id")
    .notNull()
    .references(() => charactersTable.id),
  weapon_id: smallint("weapon_id").notNull(),
  value_a: smallint("value_a").notNull().default(0),
  value_b: smallint("value_b").notNull().default(0),
  value_c: smallint("value_c").notNull().default(0),
});

export type WeaponTally = typeof weaponTalliesTable.$inferSelect;
export type NewWeaponTally = typeof weaponTalliesTable.$inferInsert;
