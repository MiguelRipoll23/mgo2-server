import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { LobbyService } from "../../../../../modules/lobby/lobby-service.ts";
import { LobbyType } from "../../../../../db/schema.ts";
import { LobbyTrackerService } from "../../../services/lobby-tracker-service.ts";
import {
  sendPacket,
  sendResult,
  sendStartEndPacket,
} from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../core/constants/error-codes-constants.ts";

const MAX_LOBBIES_PER_PACKET = 8;

@injectable()
@GameCommandHandler(0x4150)
export class GetLobbyDisconnectHandler implements ICommandHandler {
  constructor(private lobbyTrackerService = inject(LobbyTrackerService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    this.lobbyTrackerService.leaveLobby(session);
    await this.lobbyTrackerService.syncAllLobbyCounts();
    await sendResult(session, 0x4151, RESULT_NONE);
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
    // Only lobbies of type GAME are listed on the hub's Lobby Select.
    const lobbies = this.lobbyService
      .getCached()
      .filter((lobby) => lobby.typeId === LobbyType.GAME);

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
          // Attributes u32: subtype in the top byte. Byte 0x06 of the entry must be 3
          // for the subtype-5 category; left 0 like the reference servers.
          .writeUint32(((lobby.subtypeId & 0xff) << 24) >>> 0)
          .writeUint16(lobby.id)
          .writeFixedString(lobby.name, 16)
          // 64-byte text block at 0x1a: the parser reads entries at a FIXED 99-byte
          // stride and bound-checks the receive buffer, not the payload length —
          // omitting it made every entry after the first parse as rubbish.
          .writeFixedString("", 64)
          .writeUint32(0) // open time
          .writeUint32(0) // close time
          .writeUint8(1); // open flag
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
    // The parser does NOT skip this block: result, a word into rec+0x120,
    // then a FIXED loop of four 57-byte records — 4 + 4 + 4*57 = 236 bytes.
    // The previous 172-byte reply was read out of stale receive buffer.
    const writer = new PacketWriter()
      .writeUint32(RESULT_NONE) // result
      .writeUint32(4) // record count (client overwrites this slot with 4)
      .writePadding(4 * 57); // four 57-byte records, layout undecoded
    await sendPacket(session, 0x4991, writer.build());
  }
}

@injectable()
@GameCommandHandler(0x43a2)
export class HostUnknown43a2Handler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, 0x43a3, RESULT_NONE);
  }
}

@injectable()
@GameCommandHandler(0x43c0)
export class HostUnknown43c0Handler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, 0x43c1, RESULT_NONE);
  }
}

// Sole 0x4440 handler (was registered in both hub.ts and check-session.ts,
// one silently shadowing the other). Echo answers {u32 result=0}.
@injectable()
@GameCommandHandler(0x4440)
export class ChatUnknown4440Handler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    await sendResult(session, 0x4441, RESULT_NONE);
  }
}
