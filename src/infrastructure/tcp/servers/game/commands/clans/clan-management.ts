import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { ClanService } from "../../../../../../modules/clan/clan-service.ts";
import { sendError, sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const CLAN_NAME_NAMETAKEN_ERROR = 0x80000501;

@injectable()
@GameCommandHandler(0x4b00)
export class CreateClanHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId === null) {
      await sendPacket(session, 0x4b01, null);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const name = reader.readFixedString(15);
    const _openFlag = reader.remaining() > 0 ? reader.readUint8() : 1;

    const existing = await this.clanService.findByName(name);
    if (existing) {
      await sendError(session, 0x4b01, CLAN_NAME_NAMETAKEN_ERROR);
      return;
    }

    await this.clanService.create(name, characterId);
    await sendPacket(session, 0x4b01, null);
  }
}

@injectable()
@GameCommandHandler(0x4b04)
export class DisbandClanHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null) {
      const clans = await this.clanService.findAll();
      for (const clan of clans) {
        const member = await this.clanService.getMember(clan.id, characterId);
        if (member && clan.leader_id === member.id) {
          await this.clanService.disband(clan.id);
          break;
        }
      }
    }
    await sendPacket(session, 0x4b05, null);
  }
}

@injectable()
@GameCommandHandler(0x4b40)
export class LeaveClanHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      await this.clanService.removeMember(clanId, characterId);
    }
    await sendPacket(session, 0x4b41, null);
  }
}

@injectable()
@GameCommandHandler(0x4b42)
export class ApplyToClanHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4b43, null);
  }
}

// UPDATE_STATE (0x4b46) → response (0x4b47)
// Response packet format (28 bytes each):
//   int(0) + int(clanId) + byte(role) + short(notifications)
//   + byte(emblemStatus) + string(clanName, 16)
// role: 0xff = not in clan, 0 = pending applicant, 1 = member, 2 = leader
// Members and applicants get 2 packets with the same payload; no-clan gets 1.
@injectable()
@GameCommandHandler(0x4b46)
export class UpdateClanStateHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    if (session.characterId === null) {
      await sendPacket(session, 0x4b47, null);
      return;
    }

    const membership = await this.clanService.findMembershipByCharacterId(session.characterId);

    if (membership) {
      const role = membership.isLeader ? 2 : 1;
      const emblemStatus = membership.hasEmblem ? 3 : 0;

      const buildPayload = () => {
        const w = new PacketWriter();
        w.writeUint32(0);
        w.writeUint32(membership.clanId);
        w.writeUint8(role);
        w.writeUint16(0); // notifications (pending applications count — not tracked yet)
        w.writeUint8(emblemStatus);
        w.writeFixedString(membership.clanName, 16);
        return w.build();
      };

      // Send twice — client expects two packets in the member case
      await sendPacket(session, 0x4b47, buildPayload());
      await sendPacket(session, 0x4b47, buildPayload());
      return;
    }

    // Not in any clan
    const writer = new PacketWriter();
    writer.writeUint32(0);
    writer.writeUint32(0);
    writer.writeUint8(0xff);
    writer.writeUint16(0);
    writer.writeUint8(0);
    writer.writeFixedString("", 16);
    await sendPacket(session, 0x4b47, writer.build());
  }
}

@injectable()
@GameCommandHandler(0x4b60)
export class TransferClanLeadershipHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length >= 8) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      const newLeaderMemberId = reader.readUint32();
      await this.clanService.setLeader(clanId, newLeaderMemberId);
    }
    await sendPacket(session, 0x4b61, null);
  }
}

@injectable()
@GameCommandHandler(0x4b62)
export class SetEmblemEditorHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length >= 8) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      const memberId = reader.readUint32();
      await this.clanService.setEmblemEditor(clanId, memberId);
    }
    await sendPacket(session, 0x4b63, null);
  }
}

@injectable()
@GameCommandHandler(0x4b64)
export class UpdateClanCommentHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      const comment = reader.readFixedString(128);
      await this.clanService.updateComment(clanId, comment);
    }
    await sendPacket(session, 0x4b65, null);
  }
}

@injectable()
@GameCommandHandler(0x4b66)
export class UpdateClanNoticeHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId ?? 0;
    if (packet.payload.length >= 4) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      const notice = reader.readFixedString(512);
      await this.clanService.updateNotice(clanId, notice, characterId);
    }
    await sendPacket(session, 0x4b67, null);
  }
}
