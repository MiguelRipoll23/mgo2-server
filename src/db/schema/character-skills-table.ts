import { integer, pgTable, smallint, primaryKey, index } from "drizzle-orm/pg-core";
import { charactersTable } from "./characters-table.ts";

/**
 * Per-character skill ownership — the state behind 0x4125 (the skill list the
 * client shows) and 0x43a4 (the host's end-of-round experience report, the
 * ONLY route by which skill progression persists: skills level by use, which
 * the server cannot observe).
 *
 * The wire record is 4 bytes ({u8 skillId, u16 experience, u8 flag}) and the
 * client scatters it into a 128-entry array indexed by skill id; a missing row
 * is a zeroed slot (level 0, flag 0).
 *
 * The client defines exactly 17 skills (its own bound is `(id - 1) <= 16`;
 * ids 18..127 are addressable by the parser and defined by nothing), so ids
 * here are restricted to 1..17.
 */
export const characterSkillsTable = pgTable(
  "character_skills",
  {
    character_id: integer("character_id")
      .notNull()
      .references(() => charactersTable.id, { onDelete: "cascade" }),
    // u8 on the wire; the client derives level as min(experience >> 13, 3), so
    // only 0 / 8192 / 16384 / 24576 change what is displayed — intermediate
    // values are stored faithfully rather than rounded.
    skill_id: smallint("skill_id").notNull(),
    // u16 on the wire. Stored as integer: PostgreSQL's smallint is signed and
    // would silently wrap at 32767.
    experience: integer("experience").notNull().default(0),
    // u8 on the wire. Read in exactly one place in the client binary (skill
    // 17's training-menu gate) and never yet sent as anything but 0.
    flag: smallint("flag").notNull().default(0),
  },
  (table) => [
    primaryKey({ columns: [table.character_id, table.skill_id] }),
    index("character_skills_skill_id_idx").on(table.skill_id),
  ],
);

export type CharacterSkill = typeof characterSkillsTable.$inferSelect;
export type NewCharacterSkill = typeof characterSkillsTable.$inferInsert;
