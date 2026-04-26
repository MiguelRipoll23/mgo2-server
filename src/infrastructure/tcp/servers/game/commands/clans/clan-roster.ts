import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { ClanService } from "../../../../../../modules/clan/clan-service.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { LobbyService } from "../../../../../../modules/lobby/lobby-service.ts";
import { ActiveGameSessionsService } from "../../../../services/active-game-sessions-service.ts";
import { sendError, sendPacket, sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const CLAN_DOES_NOT_EXIST = 0x40;
const MAX_PER_PACKET = 15;

// Per-clan list entry: id(4) + name(16) + leaderCharId(4) + leaderCharName(16)
//                      + isNew(1) + pad(3) + time(4) = 48 bytes
function buildClanListEntry(
  writer: PacketWriter,
  clan: { clanId: number; clanName: string; leaderCharId: number; leaderCharName: string },
): void {
  const now = Math.floor(Date.now() / 1000);
  writer.writeUint32(clan.clanId);
  writer.writeFixedString(clan.clanName, 16);
  writer.writeUint32(clan.leaderCharId);
  writer.writeFixedString(clan.leaderCharName, 16);
  writer.writeUint8(0); // isNew = false
  writer.writePadding(3);
  writer.writeUint32(now);
}

// ============================================================================
// GET_LIST (0x4b10) → START(0x4b11) DATA(0x4b12)* END(0x4b13)
// Entry: 48 bytes each, max 15 per packet
// ============================================================================

@injectable()
@GameCommandHandler(0x4b10)
export class GetClanListHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const clans = await this.clanService.findAllWithLeader();

    await sendStartEndPacket(session, 0x4b11);

    for (let offset = 0; offset < clans.length; offset += MAX_PER_PACKET) {
      const batch = clans.slice(offset, offset + MAX_PER_PACKET);
      const writer = new PacketWriter();
      for (const clan of batch) {
        buildClanListEntry(writer, clan);
      }
      await sendPacket(session, 0x4b12, writer.build());
    }

    await sendStartEndPacket(session, 0x4b13);
  }
}

// ============================================================================
// GET_INFORMATION_MEMBER (0x4b20) → response (0x4b21) — 777 bytes
// Full clan detail view; emblem editor visibility depends on requester membership
// ============================================================================

@injectable()
@GameCommandHandler(0x4b20)
export class GetClanMemberInfoHandler implements ICommandHandler {
  constructor(
    private clanService = inject(ClanService),
    private characterService = inject(CharacterService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length < 4) {
      await sendError(session, 0x4b21, CLAN_DOES_NOT_EXIST);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const clanId = reader.readUint32();

    const [clanFull, clan] = await Promise.all([
      this.clanService.findByIdFull(clanId),
      this.clanService.findById(clanId),
    ]);

    if (!clanFull || !clan) {
      await sendError(session, 0x4b21, CLAN_DOES_NOT_EXIST);
      return;
    }

    // Determine if the requesting character is a clan member
    const memberCheck = session.characterId !== null
      ? await this.clanService.getMember(clanId, session.characterId)
      : null;

    // Emblem editor: show to members; default to leader if no editor set
    let emblemEditorCharId = 0;
    if (memberCheck) {
      if (clan.emblem_editor_id) {
        emblemEditorCharId =
          (await this.clanService.getCharacterIdByMemberId(clan.emblem_editor_id)) ??
          clanFull.leaderCharId;
      } else {
        emblemEditorCharId = clanFull.leaderCharId;
      }
    }

    // Notice writer name
    let noticeWriterCharName = "";
    if (clan.notice && clan.notice_writer_id) {
      const writerCharId = await this.clanService.getCharacterIdByMemberId(clan.notice_writer_id);
      if (writerCharId) {
        const writerChar = await this.characterService.findById(writerCharId);
        noticeWriterCharName = writerChar?.name ?? "[Deleted]";
      }
    }

    const gradePoints = clan.id; // placeholder — ref uses clan.getId()
    const comment = clan.comment || "No comment.";
    const notice = clan.notice ?? "";

    const writer = new PacketWriter();
    writer.writeUint32(0);
    writer.writeUint32(clan.id);
    writer.writeFixedString(clan.name, 16);
    writer.writeUint8(0); // open flag
    writer.writeUint32(clanFull.leaderCharId);
    writer.writeFixedString(clanFull.leaderCharName, 16);
    writer.writeUint32(0);
    writer.writeFixedString("", 16);
    writer.writePadding(32); // 8 × int(0)
    writer.writePadding(2);  // 2 × byte(0)
    writer.writeUint8(clan.emblem_wip != null ? 2 : 0); // WIP emblem status
    writer.writeUint8(clan.emblem != null ? 3 : 0);     // emblem status
    writer.writeFixedString(comment, 128);
    writer.writeUint32(emblemEditorCharId);
    writer.writeFixedString(notice, 512);
    writer.writeUint32(clan.notice_time ?? 0);
    writer.writeFixedString(noticeWriterCharName, 16);
    writer.writeUint32(0);
    writer.writeUint32(0);
    writer.writeUint32(gradePoints);

    await sendPacket(session, 0x4b21, writer.build());
  }
}

// ============================================================================
// ACCEPT_JOIN (0x4b30) → response (0x4b31)
// ============================================================================

@injectable()
@GameCommandHandler(0x4b30)
export class AcceptClanJoinHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length >= 8) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      const applicantCharacterId = reader.readUint32();
      await this.clanService.addMember(clanId, applicantCharacterId);
    }
    await sendPacket(session, 0x4b31, null);
  }
}

// ============================================================================
// DECLINE_JOIN (0x4b32) → response (0x4b33)
// ============================================================================

@injectable()
@GameCommandHandler(0x4b32)
export class DeclineClanJoinHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4b33, null);
  }
}

// ============================================================================
// BANISH (0x4b36) → response (0x4b37)
// ============================================================================

@injectable()
@GameCommandHandler(0x4b36)
export class BanishClanMemberHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length >= 8) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      const targetCharacterId = reader.readUint32();
      await this.clanService.removeMember(clanId, targetCharacterId);
    }
    await sendPacket(session, 0x4b37, null);
  }
}

// ============================================================================
// GET_ROSTER (0x4b52) → START(0x4b53) DATA(0x4b54)* END(0x4b55)
// Per entry (68 bytes): charId(4) + name(16) + isMember(1) + pad(4) + rewards(4)
//                       + lobbyId(2) + lobbyName(16) + gameId(4) + hostName(16) + subtype(1)
// ============================================================================

@injectable()
@GameCommandHandler(0x4b52)
export class GetClanRosterHandler implements ICommandHandler {
  constructor(
    private clanService = inject(ClanService),
    private activeSessions = inject(ActiveGameSessionsService),
    private gameService = inject(GameService),
    private lobbyService = inject(LobbyService),
    private characterService = inject(CharacterService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    let clanId = 0;
    if (packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      clanId = reader.readUint32();
    }

    const members = clanId > 0 ? await this.clanService.getMembersWithNames(clanId) : [];

    // Build per-member game info lookup from active sessions
    const sessionsByCharId = new Map<number, TcpSession>();
    for (const s of this.activeSessions.list()) {
      if (s.characterId !== null && s.gameId !== null) {
        sessionsByCharId.set(s.characterId, s);
      }
    }

    await sendStartEndPacket(session, 0x4b53);

    for (let offset = 0; offset < members.length; offset += MAX_PER_PACKET) {
      const batch = members.slice(offset, offset + MAX_PER_PACKET);
      const writer = new PacketWriter();

      for (const member of batch) {
        writer.writeUint32(member.charId);
        writer.writeFixedString(member.charName, 16);
        writer.writeUint8(1); // isMember = true
        writer.writeUint32(0); // padding
        writer.writeUint32(member.charId); // rewards placeholder (ref uses charId)

        const memberSession = sessionsByCharId.get(member.charId);
        let wroteGameInfo = false;
        if (memberSession?.gameId && memberSession.lobbyId) {
          try {
            const [game, lobby] = await Promise.all([
              this.gameService.findById(memberSession.gameId),
              this.lobbyService.findById(memberSession.lobbyId),
            ]);
            if (game && lobby) {
              const hostChar = await this.characterService.findById(game.host_id);
              writer.writeUint16(lobby.id);
              writer.writeFixedString(lobby.name, 16);
              writer.writeUint32(game.id);
              writer.writeFixedString(hostChar?.name ?? "", 16);
              writer.writeUint8(lobby.subtypeId);
              wroteGameInfo = true;
            }
          } catch {
            // game or lobby not found — fall through to zeros
          }
        }
        if (!wroteGameInfo) {
          writer.writeUint16(0);
          writer.writeFixedString("", 16);
          writer.writeUint32(0);
          writer.writeFixedString("", 16);
          writer.writeUint8(0);
        }
      }

      await sendPacket(session, 0x4b54, writer.build());
    }

    await sendStartEndPacket(session, 0x4b55);
  }
}

// ============================================================================
// GET_STATS (0x4b70) → response (0x4b71)
// Static clan stats — no asset files available, send empty responses
// ============================================================================

@injectable()
@GameCommandHandler(0x4b70)
export class GetClanStatsHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4b71, null);
    await sendPacket(session, 0x4b72, null);
  }
}

// ============================================================================
// GET_INFORMATION (0x4b80) → response (0x4b81) — 217 bytes
// Simpler clan info view (no notice/emblem editor details)
// ============================================================================

@injectable()
@GameCommandHandler(0x4b80)
export class GetClanInfoHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    let clanId = 0;
    if (packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      clanId = reader.readUint32();
    }

    if (clanId === 0) {
      await sendError(session, 0x4b81, CLAN_DOES_NOT_EXIST);
      return;
    }

    const clanFull = await this.clanService.findByIdFull(clanId);
    if (!clanFull) {
      await sendError(session, 0x4b81, CLAN_DOES_NOT_EXIST);
      return;
    }

    const comment = clanFull.comment || "No comment.";
    const totalReward = clanFull.id; // placeholder — ref uses clan.getId()

    const writer = new PacketWriter();
    writer.writeUint32(0);
    writer.writeUint32(clanFull.id);
    writer.writeFixedString(clanFull.name, 16);
    writer.writeUint32(clanFull.leaderCharId);
    writer.writeFixedString(clanFull.leaderCharName, 16);
    writer.writeUint8(clanFull.hasEmblem ? 3 : 0);
    writer.writeFixedString(comment, 128);
    writer.writeUint32(0);
    writer.writeUint32(clanFull.memberCount);
    writer.writeUint32(totalReward);
    writer.writePadding(32); // 8 × int(0)

    await sendPacket(session, 0x4b81, writer.build());
  }
}

// ============================================================================
// SEARCH (0x4b90) → START(0x4b91) DATA(0x4b92)* END(0x4b93)
// Input: exactOnly(1) + caseSensitive(1) + name(16)
// Entry: same 48-byte format as clan list
// ============================================================================

@injectable()
@GameCommandHandler(0x4b90)
export class SearchClanHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    let query = "";
    let exactOnly = false;
    if (packet.payload.length >= 18) {
      const reader = new PacketReader(packet.payload);
      exactOnly = reader.readUint8() !== 0;
      reader.skip(1); // caseSensitive flag (unused)
      query = reader.readFixedString(16);
    }

    await sendStartEndPacket(session, 0x4b91);

    if (query.length > 0) {
      const allWithLeader = await this.clanService.findAllWithLeader();
      const clans = exactOnly
        ? allWithLeader.filter((c) => c.clanName === query)
        : allWithLeader.filter((c) =>
          c.clanName.toLowerCase().includes(query.toLowerCase())
        );

      for (let offset = 0; offset < clans.length; offset += MAX_PER_PACKET) {
        const batch = clans.slice(offset, offset + MAX_PER_PACKET);
        const writer = new PacketWriter();
        for (const clan of batch) {
          buildClanListEntry(writer, clan);
        }
        await sendPacket(session, 0x4b92, writer.build());
      }
    }

    await sendStartEndPacket(session, 0x4b93);
  }
}
