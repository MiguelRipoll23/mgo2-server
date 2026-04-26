import { inject, injectable } from "@needle-di/core";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { GateCommandHandler } from "../../../../../core/tcp/decorators/gate-command-handler-decorator.ts";
import { PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { forEachPage } from "../../../../../core/tcp/utils/payload-helper-util.ts";
import { LobbyService } from "../../../../../modules/lobby/lobby-service.ts";
import {
  sendError,
  sendPacket,
  sendStartEndPacket,
} from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { ERROR_GENERAL } from "../../../../../core/constants/error-codes-constants.ts";
import { createLogger } from "../../../../../core/tcp/utils/logger-util.ts";

const MAX_LOBBIES_PER_PACKET = 22;
const LOBBY_NAME_LENGTH = 16;
const LOBBY_IP_LENGTH = 15;

type Logger = ReturnType<typeof createLogger>;

@injectable()
@GateCommandHandler(0x2005)
export class GetLobbyListHandler implements ICommandHandler {
  private log: Logger | null = null;

  constructor(private lobbyService = inject(LobbyService)) {}

  setLogger(log: Logger): void {
    this.log = log;
  }

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    try {
      const lobbies = this.lobbyService.getCached();
      const writer = new PacketWriter();
      let baseIndex = 0;

      forEachPage(lobbies, MAX_LOBBIES_PER_PACKET, (pageLobbies) => {
        for (const lobby of pageLobbies) {
          const beginnerOnly = lobby.beginnerOnly ?? false;
          const expansionOnly = lobby.expansionOnly ?? false;
          const noHeadshot = lobby.noHeadshot ?? false;

          let restriction = 0;
          restriction |= beginnerOnly ? 0x01 : 0;
          restriction |= expansionOnly ? 0x08 : 0;
          restriction |= noHeadshot ? 0x10 : 0;

          writer.writeUint32(baseIndex++);
          writer.writeUint32(lobby.typeId);
          writer.writeFixedString(lobby.name, LOBBY_NAME_LENGTH);
          writer.writeFixedString(lobby.ipAddress, LOBBY_IP_LENGTH);
          writer.writeUint16(lobby.port);
          writer.writeUint16(lobby.playersCount);
          writer.writeUint16(lobby.id);
          writer.writeUint8(restriction);
        }
      });

      await sendPacket(session, 0x2002, new Uint8Array(4)); // refactor this is just to send a int32 32
      await sendPacket(session, 0x2003, writer.build());
      await sendStartEndPacket(session, 0x2004);
    } catch (error) {
      this.log?.error("getLobbyList error:", error);
      await sendError(session, 0x2002, ERROR_GENERAL);
    }
  }
}
