import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { ClanService } from "../../../../../../modules/clan/clan-service.ts";
import { sendError, sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const EMBLEM_SIZE = 565;
const CLAN_DOES_NOT_EXIST = 0x40;

// Response format: int(0) + 565 bytes emblem data (zeros if no emblem)
function buildEmblemPayload(emblemBytes: Uint8Array | null): Uint8Array {
  const writer = new PacketWriter();
  writer.writeUint32(0); // status = no error

  if (emblemBytes && emblemBytes.length > 0) {
    if (emblemBytes.length >= EMBLEM_SIZE) {
      writer.writeBytes(emblemBytes.slice(0, EMBLEM_SIZE));
    } else {
      writer.writeBytes(emblemBytes);
      writer.writePadding(EMBLEM_SIZE - emblemBytes.length);
    }
  } else {
    writer.writePadding(EMBLEM_SIZE);
  }

  return writer.build();
}

// ============================================================================
// GET_EMBLEM_1 (0x4b48) → response (0x4b49) — lobby emblem (published)
// ============================================================================

@injectable()
@GameCommandHandler(0x4b48)
export class GetClanEmblemLobbyHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length < 4) {
      await sendError(session, 0x4b49, CLAN_DOES_NOT_EXIST);
      return;
    }
    const clanId = new PacketReader(packet.payload).readUint32();
    const clan = await this.clanService.findById(clanId);
    if (!clan) {
      await sendError(session, 0x4b49, CLAN_DOES_NOT_EXIST);
      return;
    }
    await sendPacket(session, 0x4b49, buildEmblemPayload(clan.emblem));
  }
}

// ============================================================================
// GET_EMBLEM_2 (0x4b4a) → response (0x4b4b) — published emblem
// ============================================================================

@injectable()
@GameCommandHandler(0x4b4a)
export class GetClanEmblemHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length < 4) {
      await sendError(session, 0x4b4b, CLAN_DOES_NOT_EXIST);
      return;
    }
    const clanId = new PacketReader(packet.payload).readUint32();
    const clan = await this.clanService.findById(clanId);
    if (!clan) {
      await sendError(session, 0x4b4b, CLAN_DOES_NOT_EXIST);
      return;
    }
    await sendPacket(session, 0x4b4b, buildEmblemPayload(clan.emblem));
  }
}

// ============================================================================
// GET_EMBLEM_3 (0x4b4c) → response (0x4b4d) — work-in-progress emblem
// ============================================================================

@injectable()
@GameCommandHandler(0x4b4c)
export class GetClanEmblemWipHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length < 4) {
      await sendError(session, 0x4b4d, CLAN_DOES_NOT_EXIST);
      return;
    }
    const clanId = new PacketReader(packet.payload).readUint32();
    const clan = await this.clanService.findById(clanId);
    if (!clan) {
      await sendError(session, 0x4b4d, CLAN_DOES_NOT_EXIST);
      return;
    }
    // Prefer WIP emblem; fall back to published emblem
    const emblem = clan.emblem_wip ?? clan.emblem;
    await sendPacket(session, 0x4b4d, buildEmblemPayload(emblem));
  }
}

// ============================================================================
// SET_EMBLEM (0x4b50) → response (0x4b51)
// Input: clanId(4) + emblemLength(4) + emblemBytes(N)
// ============================================================================

@injectable()
@GameCommandHandler(0x4b50)
export class SetClanEmblemHandler implements ICommandHandler {
  constructor(private clanService = inject(ClanService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length >= 8) {
      const reader = new PacketReader(packet.payload);
      const clanId = reader.readUint32();
      const emblemLength = reader.readUint32();
      if (emblemLength > 0 && reader.remaining() >= emblemLength) {
        const emblemBytes = reader.readBytes(emblemLength);
        await this.clanService.setEmblem(clanId, emblemBytes);
      }
    }
    await sendPacket(session, 0x4b51, null);
  }
}
