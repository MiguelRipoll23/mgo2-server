import { integer, pgTable, serial, text } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";

export const characterStatsTable = pgTable("character_stats", {
  id: serial("id").primaryKey(),
  character_id: integer("character_id").notNull().unique().references(() => charactersTable.id),

  // Core
  kills: integer("kills").notNull().default(0),
  deaths: integer("deaths").notNull().default(0),
  wins: integer("wins").notNull().default(0),
  score: integer("score").notNull().default(0),
  rounds: integer("rounds").notNull().default(0),

  // Stuns
  stuns: integer("stuns").notNull().default(0),
  stuns_received: integer("stuns_received").notNull().default(0),
  stuns_friendly: integer("stuns_friendly").notNull().default(0),

  // Headshots
  headshot_kills: integer("headshot_kills").notNull().default(0),
  headshot_deaths: integer("headshot_deaths").notNull().default(0),
  headshot_stuns: integer("headshot_stuns").notNull().default(0),
  headshot_stuns_received: integer("headshot_stuns_received").notNull().default(0),

  // Lock-on
  lock_kills: integer("lock_kills").notNull().default(0),
  lock_deaths: integer("lock_deaths").notNull().default(0),
  lock_stuns: integer("lock_stuns").notNull().default(0),
  lock_stuns_received: integer("lock_stuns_received").notNull().default(0),

  // Streaks (best recorded value)
  consecutive_kills: integer("consecutive_kills").notNull().default(0),
  consecutive_deaths: integer("consecutive_deaths").notNull().default(0),
  consecutive_headshots: integer("consecutive_headshots").notNull().default(0),
  consecutive_tdm: integer("consecutive_tdm").notNull().default(0),

  // Spotting
  spotted: integer("spotted").notNull().default(0),
  self_spotted: integer("self_spotted").notNull().default(0),
  snake_spotted: integer("snake_spotted").notNull().default(0),
  snake_self_spotted: integer("snake_self_spotted").notNull().default(0),

  // Misc combat
  suicides: integer("suicides").notNull().default(0),
  salutes: integer("salutes").notNull().default(0),
  radio: integer("radio").notNull().default(0),
  chat: integer("chat").notNull().default(0),
  cqc_given: integer("cqc_given").notNull().default(0),
  cqc_taken: integer("cqc_taken").notNull().default(0),
  rolls: integer("rolls").notNull().default(0),
  catapult: integer("catapult").notNull().default(0),
  falls: integer("falls").notNull().default(0),
  trapped: integer("trapped").notNull().default(0),
  melee: integer("melee").notNull().default(0),
  melee_rec: integer("melee_rec").notNull().default(0),

  // Box
  box_time: integer("box_time").notNull().default(0),
  box_uses: integer("box_uses").notNull().default(0),

  // Base / Capture
  bases_captured: integer("bases_captured").notNull().default(0),
  bases_destroyed: integer("bases_destroyed").notNull().default(0),
  sop_destab: integer("sop_destab").notNull().default(0),

  // Rescue
  gako_saved: integer("gako_saved").notNull().default(0),
  gako_defended: integer("gako_defended").notNull().default(0),
  gako_first: integer("gako_first").notNull().default(0),
  res_defend: integer("res_defend").notNull().default(0),
  res_gako_time: integer("res_gako_time").notNull().default(0),
  res_first_grab: integer("res_first_grab").notNull().default(0),

  // Bomb
  bomb_disarms: integer("bomb_disarms").notNull().default(0),

  // SDM
  sdm_survivals: integer("sdm_survivals").notNull().default(0),

  // Race
  race_checkpoints: integer("race_checkpoints").notNull().default(0),

  // Snake / TSNE
  wins_snake: integer("wins_snake").notNull().default(0),
  kills_snake: integer("kills_snake").notNull().default(0),
  snake_holdups: integer("snake_holdups").notNull().default(0),
  snake_tags_spawned: integer("snake_tags_spawned").notNull().default(0),
  snake_tags_taken: integer("snake_tags_taken").notNull().default(0),
  snake_injured: integer("snake_injured").notNull().default(0),
  tsne_grab1: integer("tsne_grab1").notNull().default(0),
  tsne_grab2: integer("tsne_grab2").notNull().default(0),

  // Knife
  knife_kills: integer("knife_kills").notNull().default(0),
  knife_stuns: integer("knife_stuns").notNull().default(0),

  // Equipment / Skills
  boosts: integer("boosts").notNull().default(0),
  scans: integer("scans").notNull().default(0),
  evg_time: integer("evg_time").notNull().default(0),
  wakeups: integer("wakeups").notNull().default(0),

  // Team / Misc
  team_kills: integer("team_kills").notNull().default(0),
  withdrawals: integer("withdrawals").notNull().default(0),

  // Points breakdown
  points_assist: integer("points_assist").notNull().default(0),
  points_base: integer("points_base").notNull().default(0),

  // Training
  trained_soldiers: integer("trained_soldiers").notNull().default(0),
  time_training: integer("time_training").notNull().default(0),
  time_instructor: integer("time_instructor").notNull().default(0),
  time_student: integer("time_student").notNull().default(0),

  // Time (seconds)
  time: integer("time").notNull().default(0),
  time_snake: integer("time_snake").notNull().default(0),
  time_dedi: integer("time_dedi").notNull().default(0),

  // Per-mode stats (JSON)
  stats_dm: text("stats_dm"),
  stats_tdm: text("stats_tdm"),
  stats_res: text("stats_res"),
  stats_cap: text("stats_cap"),
  stats_base: text("stats_base"),
  stats_bomb: text("stats_bomb"),
  stats_sne: text("stats_sne"),
  stats_tsne: text("stats_tsne"),
  stats_sdm: text("stats_sdm"),
  stats_scap: text("stats_scap"),
  stats_race: text("stats_race"),

  last_updated: integer("last_updated"),
});

export type CharacterStats = typeof characterStatsTable.$inferSelect;
export type NewCharacterStats = typeof characterStatsTable.$inferInsert;
