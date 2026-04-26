import { integer, pgTable, serial, text, varchar } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";

export const characterAppearanceTable = pgTable("characters_appearance", {
  id: serial("id").primaryKey(),
  character_id: integer("character_id").notNull().references(() => charactersTable.id).unique(),
  gender: integer("gender").notNull().default(0),
  face: integer("face").notNull().default(0),
  voice: integer("voice").notNull().default(0),
  pitch: integer("pitch").notNull().default(0),
  head: integer("head").notNull().default(0),
  head_color: integer("head_color").notNull().default(0),
  upper: integer("upper").notNull().default(0),
  upper_color: integer("upper_color").notNull().default(0),
  lower: integer("lower").notNull().default(0),
  lower_color: integer("lower_color").notNull().default(0),
  chest: integer("chest").notNull().default(0),
  chest_color: integer("chest_color").notNull().default(0),
  waist: integer("waist").notNull().default(0),
  waist_color: integer("waist_color").notNull().default(0),
  hands: integer("hands").notNull().default(0),
  hands_color: integer("hands_color").notNull().default(0),
  feet: integer("feet").notNull().default(0),
  feet_color: integer("feet_color").notNull().default(0),
  accessory1: integer("accessory1").notNull().default(0),
  accessory1_color: integer("accessory1_color").notNull().default(0),
  accessory2: integer("accessory2").notNull().default(0),
  accessory2_color: integer("accessory2_color").notNull().default(0),
  face_paint: integer("face_paint").notNull().default(0),
});

export type CharacterAppearance = typeof characterAppearanceTable.$inferSelect;
export type NewCharacterAppearance = typeof characterAppearanceTable.$inferInsert;

export const characterFriendsTable = pgTable("characters_friends", {
  id: serial("id").primaryKey(),
  character_id: integer("character_id").notNull().references(() => charactersTable.id),
  target_id: integer("target_id").notNull().references(() => charactersTable.id),
  type: integer("type").notNull().default(0),
}, (table) => [
  { name: "character_friends_unique", columns: [table.character_id, table.target_id] },
]);

export type CharacterFriend = typeof characterFriendsTable.$inferSelect;
export type NewCharacterFriend = typeof characterFriendsTable.$inferInsert;

export const characterSetSkillsTable = pgTable("characters_sets_skills", {
  id: serial("id").primaryKey(),
  character_id: integer("character_id").notNull().references(() => charactersTable.id),
  idx: integer("idx").notNull().default(0),
  name: varchar("name", { length: 63 }).notNull().default(""),
  modes: integer("modes").notNull().default(0),
  skill_1: integer("skill_1").notNull().default(0),
  skill_2: integer("skill_2").notNull().default(0),
  skill_3: integer("skill_3").notNull().default(0),
  skill_4: integer("skill_4").notNull().default(0),
  level_1: integer("level_1").notNull().default(0),
  level_2: integer("level_2").notNull().default(0),
  level_3: integer("level_3").notNull().default(0),
  level_4: integer("level_4").notNull().default(0),
});

export type CharacterSetSkills = typeof characterSetSkillsTable.$inferSelect;
export type NewCharacterSetSkills = typeof characterSetSkillsTable.$inferInsert;

export const characterSetGearTable = pgTable("characters_sets_gear", {
  id: serial("id").primaryKey(),
  character_id: integer("character_id").notNull().references(() => charactersTable.id),
  idx: integer("idx").notNull().default(0),
  name: varchar("name", { length: 63 }).notNull().default(""),
  stages: integer("stages").notNull().default(0),
  face: integer("face").notNull().default(0),
  head: integer("head").notNull().default(0),
  head_color: integer("head_color").notNull().default(0),
  upper: integer("upper").notNull().default(0),
  upper_color: integer("upper_color").notNull().default(0),
  lower: integer("lower").notNull().default(0),
  lower_color: integer("lower_color").notNull().default(0),
  chest: integer("chest").notNull().default(0),
  chest_color: integer("chest_color").notNull().default(0),
  waist: integer("waist").notNull().default(0),
  waist_color: integer("waist_color").notNull().default(0),
  hands: integer("hands").notNull().default(0),
  hands_color: integer("hands_color").notNull().default(0),
  feet: integer("feet").notNull().default(0),
  feet_color: integer("feet_color").notNull().default(0),
  accessory1: integer("accessory1").notNull().default(0),
  accessory1_color: integer("accessory1_color").notNull().default(0),
  accessory2: integer("accessory2").notNull().default(0),
  accessory2_color: integer("accessory2_color").notNull().default(0),
  face_paint: integer("face_paint").notNull().default(0),
});

export type CharacterSetGear = typeof characterSetGearTable.$inferSelect;
export type NewCharacterSetGear = typeof characterSetGearTable.$inferInsert;

export const characterHostSettingsTable = pgTable("characters_hostsettings", {
  id: serial("id").primaryKey(),
  character_id: integer("character_id").notNull().references(() => charactersTable.id),
  type: integer("type").notNull(),
  settings: text("settings").notNull(),
});

export type CharacterHostSettings = typeof characterHostSettingsTable.$inferSelect;
export type NewCharacterHostSettings = typeof characterHostSettingsTable.$inferInsert;

export const characterChatMacrosTable = pgTable("characters_chatmacros", {
  id: serial("id").primaryKey(),
  character_id: integer("character_id").notNull().references(() => charactersTable.id),
  type: integer("type").notNull().default(0),
  idx: integer("idx").notNull().default(0),
  text: varchar("text", { length: 64 }).notNull().default(""),
});

export type CharacterChatMacros = typeof characterChatMacrosTable.$inferSelect;
export type NewCharacterChatMacros = typeof characterChatMacrosTable.$inferInsert;
