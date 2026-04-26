import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { LobbyService } from "../../../../../modules/lobby/lobby-service.ts";
import { LobbyTrackerService } from "../../../services/lobby-tracker-service.ts";
import {
  sendPacket,
  sendStartEndPacket,
} from "../../../../../core/tcp/utils/session-helpers-util.ts";

const MAX_LOBBIES_PER_PACKET = 8;

@injectable()
@GameCommandHandler(0x4150)
export class GetLobbyDisconnectHandler implements ICommandHandler {
  constructor(private lobbyTrackerService = inject(LobbyTrackerService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    this.lobbyTrackerService.leaveLobby(session);
    await this.lobbyTrackerService.syncAllLobbyCounts();
    await sendPacket(session, 0x4151, null);
  }
}

@injectable()
@GameCommandHandler(0x43d0)
export class TrainingConnectHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const payload = new Uint8Array([
      0x00,
      0x0a,
      0x00,
      0x15,
      0x00,
      0x3a,
      0x00,
      0x08,
      0x00,
      0x61,
    ]);
    await sendPacket(session, 0x43d1, payload);
  }
}

@injectable()
@GameCommandHandler(0x4900)
export class GetGameLobbyInfoHandler implements ICommandHandler {
  constructor(private lobbyService = inject(LobbyService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const lobbies = this.lobbyService.getCached();

    await sendStartEndPacket(session, 0x4901);

    for (
      let offset = 0;
      offset < lobbies.length;
      offset += MAX_LOBBIES_PER_PACKET
    ) {
      const batch = lobbies.slice(offset, offset + MAX_LOBBIES_PER_PACKET);
      const writer = new PacketWriter();
      for (let index = 0; index < batch.length; index++) {
        const lobby = batch[index];
        writer
          .writeUint32(offset + index)
          .writeUint32(((lobby.subtypeId & 0xff) << 24) >>> 0)
          .writeUint16(lobby.id)
          .writeFixedString(lobby.name, 16)
          .writeUint32(0)
          .writeUint32(0)
          .writeUint8(1);
      }
      if (writer.size > 0) {
        await sendPacket(session, 0x4902, writer.build());
      }
    }

    await sendStartEndPacket(session, 0x4903);
  }
}

@injectable()
@GameCommandHandler(0x4990)
export class GetGameEntryInfoHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const writer = new PacketWriter()
      .writeUint32(0)
      .writeUint32(1)
      .writePadding(0xa4);
    await sendPacket(session, 0x4991, writer.build());
  }
}

@injectable()
@GameCommandHandler(0x43a2)
export class HostUnknown43a2Handler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x43a3, null);
  }
}

@injectable()
@GameCommandHandler(0x43c0)
export class HostUnknown43c0Handler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x43c1, null);
  }
}

@injectable()
@GameCommandHandler(0x4440)
export class ChatUnknown4440Handler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendPacket(session, 0x4441, null);
  }
}
