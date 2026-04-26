import { injectable, inject } from "@needle-di/core";
import { and, like, sql, eq } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import { clanMembersTable, clansTable, charactersTable } from "../../db/schema.ts";
import type { Clan, ClanMember } from "../../db/schema.ts";

@injectable()
export class ClanService {
  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  async findAll(offset = 0, limit = 50): Promise<Clan[]> {
    return await this.db.select().from(clansTable).offset(offset).limit(limit);
  }

  async findById(clanId: number): Promise<Clan | null> {
    const rows = await this.db
      .select()
      .from(clansTable)
      .where(eq(clansTable.id, clanId))
      .limit(1);
    return rows[0] ?? null;
  }

  async findByName(name: string): Promise<Clan | null> {
    const rows = await this.db
      .select()
      .from(clansTable)
      .where(eq(clansTable.name, name))
      .limit(1);
    return rows[0] ?? null;
  }

  async create(name: string, leaderId: number): Promise<Clan> {
    return await this.db.transaction(async (transaction) => {
      const [clan] = await transaction
        .insert(clansTable)
        .values({ name })
        .returning();

      const [member] = await transaction
        .insert(clanMembersTable)
        .values({ clan_id: clan.id, character_id: leaderId })
        .returning();

      const [updated] = await transaction
        .update(clansTable)
        .set({ leader_id: member.id })
        .where(eq(clansTable.id, clan.id))
        .returning();

      return updated;
    });
  }

  async disband(clanId: number): Promise<void> {
    await this.db.transaction(async (transaction) => {
      await transaction
        .delete(clanMembersTable)
        .where(eq(clanMembersTable.clan_id, clanId));

      await transaction.delete(clansTable).where(eq(clansTable.id, clanId));
    });
  }

  async getMember(
    clanId: number,
    characterId: number,
  ): Promise<ClanMember | null> {
    const rows = await this.db
      .select()
      .from(clanMembersTable)
      .where(
        and(
          eq(clanMembersTable.clan_id, clanId),
          eq(clanMembersTable.character_id, characterId),
        ),
      )
      .limit(1);
    return rows[0] ?? null;
  }

  async getMembers(clanId: number): Promise<ClanMember[]> {
    return await this.db
      .select()
      .from(clanMembersTable)
      .where(eq(clanMembersTable.clan_id, clanId));
  }

  async addMember(clanId: number, characterId: number): Promise<ClanMember> {
    const rows = await this.db
      .insert(clanMembersTable)
      .values({ clan_id: clanId, character_id: characterId })
      .returning();
    return rows[0];
  }

  async removeMember(clanId: number, characterId: number): Promise<void> {
    await this.db
      .delete(clanMembersTable)
      .where(
        and(
          eq(clanMembersTable.clan_id, clanId),
          eq(clanMembersTable.character_id, characterId),
        ),
      );
  }

  async setLeader(clanId: number, memberId: number): Promise<void> {
    await this.db
      .update(clansTable)
      .set({ leader_id: memberId })
      .where(eq(clansTable.id, clanId));
  }

  async setEmblemEditor(clanId: number, memberId: number): Promise<void> {
    await this.db
      .update(clansTable)
      .set({ emblem_editor_id: memberId })
      .where(eq(clansTable.id, clanId));
  }

  async updateComment(clanId: number, comment: string): Promise<void> {
    await this.db.update(clansTable).set({ comment }).where(eq(clansTable.id, clanId));
  }

  async updateNotice(
    clanId: number,
    notice: string,
    writerId: number,
  ): Promise<void> {
    await this.db
      .update(clansTable)
      .set({
        notice,
        notice_writer_id: writerId,
        notice_time: sql`extract(epoch from now())::int`,
      })
      .where(eq(clansTable.id, clanId));
  }

  async setEmblem(clanId: number, emblem: Uint8Array): Promise<void> {
    await this.db.update(clansTable).set({ emblem }).where(eq(clansTable.id, clanId));
  }

  async setEmblemWip(clanId: number, emblem: Uint8Array): Promise<void> {
    await this.db
      .update(clansTable)
      .set({ emblem_wip: emblem })
      .where(eq(clansTable.id, clanId));
  }

  async search(query: string): Promise<Clan[]> {
    return await this.db
      .select()
      .from(clansTable)
      .where(like(clansTable.name, `%${query}%`));
  }

  /** Clan list entries joined with the leader's character name. */
  async findAllWithLeader(offset = 0, limit = 50): Promise<Array<{
    clanId: number;
    clanName: string;
    leaderCharId: number;
    leaderCharName: string;
    hasEmblem: boolean;
  }>> {
    const rows = await this.db
      .select({
        clanId: clansTable.id,
        clanName: clansTable.name,
        leaderCharId: clanMembersTable.character_id,
        leaderCharName: charactersTable.name,
        emblem: clansTable.emblem,
      })
      .from(clansTable)
      .leftJoin(clanMembersTable, eq(clansTable.leader_id, clanMembersTable.id))
      .leftJoin(charactersTable, eq(clanMembersTable.character_id, charactersTable.id))
      .offset(offset)
      .limit(limit);

    return rows.map((r) => ({
      clanId: r.clanId,
      clanName: r.clanName,
      leaderCharId: r.leaderCharId ?? 0,
      leaderCharName: r.leaderCharName ?? "",
      hasEmblem: r.emblem != null,
    }));
  }

  /** Clan details with leader character name and total member count. */
  async findByIdFull(clanId: number): Promise<{
    id: number;
    name: string;
    comment: string;
    hasEmblem: boolean;
    leaderCharId: number;
    leaderCharName: string;
    memberCount: number;
  } | null> {
    const clanRows = await this.db
      .select({
        id: clansTable.id,
        name: clansTable.name,
        comment: clansTable.comment,
        emblem: clansTable.emblem,
        leaderCharId: clanMembersTable.character_id,
        leaderCharName: charactersTable.name,
      })
      .from(clansTable)
      .leftJoin(clanMembersTable, eq(clansTable.leader_id, clanMembersTable.id))
      .leftJoin(charactersTable, eq(clanMembersTable.character_id, charactersTable.id))
      .where(eq(clansTable.id, clanId))
      .limit(1);

    if (!clanRows[0]) return null;

    const countRows = await this.db
      .select({ count: sql<number>`count(*)` })
      .from(clanMembersTable)
      .where(eq(clanMembersTable.clan_id, clanId));

    return {
      id: clanRows[0].id,
      name: clanRows[0].name,
      comment: clanRows[0].comment,
      hasEmblem: clanRows[0].emblem != null,
      leaderCharId: clanRows[0].leaderCharId ?? 0,
      leaderCharName: clanRows[0].leaderCharName ?? "",
      memberCount: Number(countRows[0]?.count ?? 0),
    };
  }

  /** Members of a clan joined with their character name. */
  async getMembersWithNames(clanId: number): Promise<Array<{
    memberId: number;
    charId: number;
    charName: string;
  }>> {
    const rows = await this.db
      .select({
        memberId: clanMembersTable.id,
        charId: clanMembersTable.character_id,
        charName: charactersTable.name,
      })
      .from(clanMembersTable)
      .innerJoin(charactersTable, eq(clanMembersTable.character_id, charactersTable.id))
      .where(eq(clanMembersTable.clan_id, clanId));

    return rows.map((r) => ({
      memberId: r.memberId,
      charId: r.charId,
      charName: r.charName,
    }));
  }

  /** Clan membership info for a character — null if not in any clan. */
  async findMembershipByCharacterId(characterId: number): Promise<{
    memberId: number;
    clanId: number;
    clanName: string;
    isLeader: boolean;
    hasEmblem: boolean;
    leaderId: number | null;
  } | null> {
    const rows = await this.db
      .select({
        memberId: clanMembersTable.id,
        clanId: clansTable.id,
        clanName: clansTable.name,
        leaderId: clansTable.leader_id,
        emblem: clansTable.emblem,
      })
      .from(clanMembersTable)
      .innerJoin(clansTable, eq(clanMembersTable.clan_id, clansTable.id))
      .where(eq(clanMembersTable.character_id, characterId))
      .limit(1);

    if (!rows[0]) return null;

    return {
      memberId: rows[0].memberId,
      clanId: rows[0].clanId,
      clanName: rows[0].clanName,
      isLeader: rows[0].leaderId === rows[0].memberId,
      hasEmblem: rows[0].emblem != null,
      leaderId: rows[0].leaderId,
    };
  }

  /** Resolve a clan member row ID to its character ID. */
  async getCharacterIdByMemberId(memberId: number): Promise<number | null> {
    const rows = await this.db
      .select({ characterId: clanMembersTable.character_id })
      .from(clanMembersTable)
      .where(eq(clanMembersTable.id, memberId))
      .limit(1);
    return rows[0]?.characterId ?? null;
  }
}
