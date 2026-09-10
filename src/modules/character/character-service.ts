import { injectable, inject } from "@needle-di/core";
import { and, eq } from "drizzle-orm";
import { calculateAnimalRank } from "./animal-rank-service.ts";
import { DatabaseService } from "../../core/services/database-service.ts";
import {
  characterAppearanceTable,
  characterChatMacrosTable,
  characterEquippedSkillsTable,
  characterFriendsTable,
  characterHostSettingsTable,
  characterSetGearTable,
  characterSetSkillsTable,
  charactersTable,
  clanMembersTable,
  clansTable,
} from "../../db/schema.ts";
import type {
  Character,
  CharacterAppearance,
  CharacterChatMacros,
  CharacterEquippedSkills,
  CharacterFriend,
  CharacterHostSettings,
  CharacterSetGear,
  CharacterSetSkills,
  CharacterStats,
  NewCharacter,
  NewCharacterAppearance,
} from "../../db/schema.ts";

type AppearanceInput = Omit<NewCharacterAppearance, "id" | "character_id">;
type SkillSetInput = Omit<CharacterSetSkills, "id" | "character_id">;
type GearSetInput = Omit<CharacterSetGear, "id" | "character_id">;
type ChatMacroInput = Omit<CharacterChatMacros, "id" | "character_id">;
type EquippedSkillsInput = Omit<CharacterEquippedSkills, "character_id">;

/**
 * The client's own validator zeroes any skill record whose experience exceeds
 * this (ELF 0x93E418: `cmpwi cr7,r0,24576; ble+` — the value is level 3, the
 * cap of min(experience >> 13, 3)). Storing an over-cap value does not merely
 * render oddly — it makes the skill disappear from the player's list the next
 * time 0x4125 sends it back. Clamping keeps the store inside the range the
 * client will accept.
 */
export const MAX_SKILL_EXPERIENCE = 24576;

// ── Skill catalogue (all characters own every skill, maxed out) ───────────

// This build (MGO2PC EBOOT, NPMG00020) defines 25 skills. Capstone on
// MGO2.ELF: the id→name pointer table at 0x11d36e8 runs exactly 25 entries
// (Skill_HAND_MAS … Skill_CHARM), and the 0x4125 parser at 0xf096ac accepts
// every id > 0 up to its 0x80-record loop bound (128). The retail disc
// (BLUS30109) defines only 17, which is where the earlier "17 skills" bound
// came from — it does not apply to this client.
export const NUM_DEFINED_SKILLS = 25;

// Skill level is `experience >> 13`, capped at 3 (the ELF's own validator
// zeroes any record above MAX_SKILL_EXPERIENCE, which is exactly level 3).
export const SKILL_LEVEL_CAP = 3;

// Skills with no meaningful experience path: id 17 (Instructor, renders
// without a level bar — 0x8DB8A8) and the two "EX" unlockables, ids 20
// (CQC Expert) and 22 (Scanner EX). They are advertised at 0x2000 (level 1),
// matching both the reference LoadoutWriter table and the 0x4129 post-game
// reply; every other skill is served at 0x6000 (level 3).
export const SKILL_EXP_NO_PATH = 0x2000;
export const LOW_EXP_SKILL_IDS = new Set([17, 20, 22]);

export interface MaxLevelSkill {
  skill_id: number;
  experience: number;
  flag: number;
}

/**
 * Every defined skill at its max level. Served verbatim by 0x4125 — skill
 * levels are no longer persisted, so this catalogue IS the character's skill
 * state; the 0x43a4 reports are acknowledged but not stored.
 */
export function maxLevelSkills(): MaxLevelSkill[] {
  return Array.from(
    { length: NUM_DEFINED_SKILLS },
    (_, index) => {
      const skillId = index + 1;
      return {
        skill_id: skillId,
        experience: LOW_EXP_SKILL_IDS.has(skillId)
          ? SKILL_EXP_NO_PATH
          : MAX_SKILL_EXPERIENCE,
        flag: 0,
      };
    },
  );
}

/** The level the client derives from a skill's experience: `exp >> 13`, capped. */
export function skillLevelFromExperience(experience: number): number {
  return Math.min(Math.max(0, experience) >> 13, SKILL_LEVEL_CAP);
}

/** Max level of a skill id, 0 for unassigned slots and undefined ids. */
export function skillLevelAtMax(skillId: number): number {
  if (skillId < 1 || skillId > NUM_DEFINED_SKILLS) return 0;
  return LOW_EXP_SKILL_IDS.has(skillId)
    ? SKILL_EXP_NO_PATH >> 13
    : SKILL_LEVEL_CAP;
}

@injectable()
export class CharacterService {
  constructor(private readonly databaseService = inject(DatabaseService)) { }

  public async findByUserId(userId: number): Promise<Character[]> {
    const db = this.databaseService.get();
    return await db
      .select()
      .from(charactersTable)
      .where(and(eq(charactersTable.user_id, userId), eq(charactersTable.active, 1)));
  }

  public async findById(characterId: number): Promise<Character | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select()
      .from(charactersTable)
      .where(eq(charactersTable.id, characterId))
      .limit(1);
    return rows[0] ?? null;
  }

  public async findByName(name: string): Promise<Character | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select()
      .from(charactersTable)
      .where(eq(charactersTable.name, name))
      .limit(1);
    return rows[0] ?? null;
  }

  public async create(
    data: NewCharacter,
    appearanceData: AppearanceInput,
  ): Promise<Character> {
    const db = this.databaseService.get();
    return await db.transaction(async (transaction) => {
      const [character] = await transaction
        .insert(charactersTable)
        .values(data)
        .returning();

      await transaction
        .insert(characterAppearanceTable)
        .values({ ...appearanceData, character_id: character.id });

      return character;
    });
  }

  public async softDelete(characterId: number): Promise<void> {
    const character = await this.findById(characterId);
    if (!character) return;

    const db = this.databaseService.get();
    await db
      .update(charactersTable)
      .set({
        active: 0,
        old_name: character.name,
        name: `:#${characterId}`,
      })
      .where(eq(charactersTable.id, characterId));
  }

  public async setLobby(
    characterId: number,
    lobbyId: number | null,
  ): Promise<void> {
    const db = this.databaseService.get();
    await db
      .update(charactersTable)
      .set({ lobby_id: lobbyId })
      .where(eq(charactersTable.id, characterId));
  }

  public async getAppearance(
    characterId: number,
  ): Promise<CharacterAppearance | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select()
      .from(characterAppearanceTable)
      .where(eq(characterAppearanceTable.character_id, characterId))
      .limit(1);
    return rows[0] ?? null;
  }

  public async updateAppearance(
    characterId: number,
    data: AppearanceInput,
  ): Promise<void> {
    const db = this.databaseService.get();
    await db
      .insert(characterAppearanceTable)
      .values({ ...data, character_id: characterId })
      .onConflictDoUpdate({
        target: characterAppearanceTable.character_id,
        set: data,
      });
  }

  public async getFriendsAndBlocked(
    characterId: number,
  ): Promise<CharacterFriend[]> {
    const db = this.databaseService.get();
    return await db
      .select()
      .from(characterFriendsTable)
      .where(eq(characterFriendsTable.character_id, characterId));
  }

  public async getFriendsAndBlockedWithNames(
    characterId: number,
  ): Promise<Array<{ targetId: number; targetName: string; type: number }>> {
    const db = this.databaseService.get();
    const rows = await db
      .select({
        targetId: characterFriendsTable.target_id,
        targetName: charactersTable.name,
        type: characterFriendsTable.type,
      })
      .from(characterFriendsTable)
      .innerJoin(charactersTable, eq(characterFriendsTable.target_id, charactersTable.id))
      .where(eq(characterFriendsTable.character_id, characterId));
    return rows;
  }

  public async addFriendOrBlocked(
    characterId: number,
    targetId: number,
    type: number,
  ): Promise<void> {
    const db = this.databaseService.get();
    await db
      .insert(characterFriendsTable)
      .values({ character_id: characterId, target_id: targetId, type });
  }

  public async removeFriendOrBlocked(
    characterId: number,
    targetId: number,
  ): Promise<void> {
    const db = this.databaseService.get();
    await db
      .delete(characterFriendsTable)
      .where(
        and(
          eq(characterFriendsTable.character_id, characterId),
          eq(characterFriendsTable.target_id, targetId),
        ),
      );
  }

  public async getSkillSets(
    characterId: number,
  ): Promise<CharacterSetSkills[]> {
    const db = this.databaseService.get();
    return await db
      .select()
      .from(characterSetSkillsTable)
      .where(eq(characterSetSkillsTable.character_id, characterId));
  }

  public async updateSkillSets(
    characterId: number,
    sets: SkillSetInput[],
  ): Promise<void> {
    const db = this.databaseService.get();
    await db.transaction(async (transaction) => {
      await transaction
        .delete(characterSetSkillsTable)
        .where(eq(characterSetSkillsTable.character_id, characterId));

      if (sets.length > 0) {
        await transaction
          .insert(characterSetSkillsTable)
          .values(sets.map((set) => ({ ...set, character_id: characterId })));
      }
    });
  }

  public async getGearSets(characterId: number): Promise<CharacterSetGear[]> {
    const db = this.databaseService.get();
    return await db
      .select()
      .from(characterSetGearTable)
      .where(eq(characterSetGearTable.character_id, characterId));
  }

  public async updateGearSets(
    characterId: number,
    sets: GearSetInput[],
  ): Promise<void> {
    const db = this.databaseService.get();
    await db.transaction(async (transaction) => {
      await transaction
        .delete(characterSetGearTable)
        .where(eq(characterSetGearTable.character_id, characterId));

      if (sets.length > 0) {
        await transaction
          .insert(characterSetGearTable)
          .values(sets.map((set) => ({ ...set, character_id: characterId })));
      }
    });
  }

  public async getChatMacros(
    characterId: number,
  ): Promise<CharacterChatMacros[]> {
    const db = this.databaseService.get();
    return await db
      .select()
      .from(characterChatMacrosTable)
      .where(eq(characterChatMacrosTable.character_id, characterId));
  }

  public async updateChatMacros(
    characterId: number,
    macros: ChatMacroInput[],
  ): Promise<void> {
    const db = this.databaseService.get();
    await db.transaction(async (transaction) => {
      await transaction
        .delete(characterChatMacrosTable)
        .where(eq(characterChatMacrosTable.character_id, characterId));

      if (macros.length > 0) {
        await transaction
          .insert(characterChatMacrosTable)
          .values(
            macros.map((macro) => ({ ...macro, character_id: characterId })),
          );
      }
    });
  }

  public async getHostSettings(
    characterId: number,
  ): Promise<CharacterHostSettings[]> {
    const db = this.databaseService.get();
    return await db
      .select()
      .from(characterHostSettingsTable)
      .where(eq(characterHostSettingsTable.character_id, characterId));
  }

  public async updateHostSettings(
    characterId: number,
    type: number,
    settings: string,
  ): Promise<void> {
    const db = this.databaseService.get();
    await db.transaction(async (transaction) => {
      await transaction
        .delete(characterHostSettingsTable)
        .where(
          and(
            eq(characterHostSettingsTable.character_id, characterId),
            eq(characterHostSettingsTable.type, type),
          ),
        );

      await transaction
        .insert(characterHostSettingsTable)
        .values({ character_id: characterId, type, settings });
    });
  }

  public async updateGameplayOptions(
    characterId: number,
    options: string,
  ): Promise<void> {
    const db = this.databaseService.get();
    await db
      .update(charactersTable)
      .set({ gameplay_options: options })
      .where(eq(charactersTable.id, characterId));
  }

  public async getEquippedSkills(
    characterId: number,
  ): Promise<CharacterEquippedSkills | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select()
      .from(characterEquippedSkillsTable)
      .where(eq(characterEquippedSkillsTable.character_id, characterId))
      .limit(1);
    return rows[0] ?? null;
  }

  public async updateEquippedSkills(
    characterId: number,
    skills: EquippedSkillsInput,
  ): Promise<void> {
    const db = this.databaseService.get();
    await db
      .insert(characterEquippedSkillsTable)
      .values({ ...skills, character_id: characterId })
      .onConflictDoUpdate({
        target: characterEquippedSkillsTable.character_id,
        set: skills,
      });
  }

  public async updateRank(
    characterId: number,
    stats: CharacterStats,
    daysSinceLastLogin = 0,
  ): Promise<void> {
    const db = this.databaseService.get();
    const rank = calculateAnimalRank(stats, daysSinceLastLogin);
    await db
      .update(charactersTable)
      .set({ rank })
      .where(eq(charactersTable.id, characterId));
  }

  public async getClanInfo(
    characterId: number,
  ): Promise<{ clanId: number; clanName: string; hasEmblem: boolean } | null> {
    const db = this.databaseService.get();
    const rows = await db
      .select({
        clanId: clansTable.id,
        clanName: clansTable.name,
        hasEmblem: clansTable.emblem,
      })
      .from(clanMembersTable)
      .innerJoin(clansTable, eq(clanMembersTable.clan_id, clansTable.id))
      .where(eq(clanMembersTable.character_id, characterId))
      .limit(1);

    if (!rows[0]) return null;
    return {
      clanId: rows[0].clanId,
      clanName: rows[0].clanName,
      hasEmblem: rows[0].hasEmblem != null,
    };
  }

  public async addExperience(
    characterId: number,
    amount: number,
    aborted = false,
  ): Promise<void> {
    const db = this.databaseService.get();
    const character = await this.findById(characterId);
    if (!character) return;
    const current = character.experience ?? 0;
    const newExp = aborted ? Math.max(0, current - 60) : amount;
    await db
      .update(charactersTable)
      .set({ experience: newExp })
      .where(eq(charactersTable.id, characterId));
  }

  public static calculateLevel(experience: number): number {
    if (experience < 125) return 0;
    if (experience < 250) return 1;
    if (experience < 375) return 2;
    if (experience < 500) return 3;
    if (experience < 650) return 4;
    if (experience < 800) return 5;
    if (experience < 950) return 6;
    if (experience < 1100) return 7;
    if (experience < 1250) return 8;
    if (experience < 1400) return 9;
    if (experience < 1550) return 10;
    if (experience < 1700) return 11;
    if (experience < 1850) return 12;
    if (experience < 2000) return 13;
    if (experience < 2175) return 14;
    if (experience < 2350) return 15;
    if (experience < 2525) return 16;
    if (experience < 2725) return 17;
    if (experience < 2925) return 18;
    if (experience < 3275) return 19;
    return 20;
  }

  // ── Skill progression (0x43a4) ─────────────────────────────────
  // Skill levels are no longer persisted: every character serves the max-level
  // catalogue (see maxLevelSkills), so the host's 0x43a4 experience reports
  // are acknowledged and discarded by their handler.
}
