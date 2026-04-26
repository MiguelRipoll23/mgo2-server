import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { SessionsService } from "../../../../../modules/auth/sessions-service.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";
import { LobbyTrackerService } from "../../../services/lobby-tracker-service.ts";
import { LobbyService } from "../../../../../modules/lobby/lobby-service.ts";
import { sendError, sendPacket, sendStartEndPacket } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { ERROR_INVALID_SESSION } from "../../../../../core/constants/error-codes-constants.ts";
import { XOR_SESSION_ID_BYTES } from "../../../../../core/constants/crypto-keys-constants.ts";
import {
  xorBufferWithKey,
  encryptAuthPayload,
} from "../../../../../core/tcp/utils/crypto-util.ts";

const INVALID_SESSION_ERROR = 0x80000201;

@injectable()
@GameCommandHandler(0x3003)
export class CheckSessionHandler implements ICommandHandler {
  constructor(
    private sessionsService = inject(SessionsService),
    private characterService = inject(CharacterService),
    private lobbyTrackerService = inject(LobbyTrackerService),
    private lobbyService = inject(LobbyService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const characterId = reader.readUint32();
    const rawSessionBytes = reader.readBytes(8);
    const _unknown = reader.readBytes(8);

    xorBufferWithKey(rawSessionBytes, XOR_SESSION_ID_BYTES);
    encryptAuthPayload(rawSessionBytes);

    const sessionToken = new TextDecoder("iso-8859-1").decode(rawSessionBytes);
    const user = await this.sessionsService.findSessionByToken(sessionToken);

    if (user === null) {
      console.log(`[tcp][game] session not found: ${sessionToken}`);
      await sendError(session, 0x3004, ERROR_INVALID_SESSION);
      return;
    }

    session.userId = user.user_id;

    const character = await this.characterService.findById(characterId);
    if (!character || character.user_id !== user.user_id) {
      await sendError(session, 0x3004, INVALID_SESSION_ERROR);
      return;
    }

    session.characterId = characterId;
    session.lobbyId = character.lobby_id;
    session.userId = user.user_id;

    if (session.lobbyId !== null) {
      this.lobbyTrackerService.joinLobby(session, session.lobbyId);
      await this.lobbyTrackerService.syncAllLobbyCounts();
    }

    await sendStartEndPacket(session, 0x3004);
  }
}

@injectable()
@GameCommandHandler(0x4700)
export class GetPlayerDataHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    reader.readBytes(16);

    await sendStartEndPacket(session, 0x4701);
  }
}

@injectable()
@GameCommandHandler(0x4440)
export class GetPlayerOptionsHandler implements ICommandHandler {
  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    reader.readUint8();

    const writer = new PacketWriter();
    writer.writeUint32(0);
    writer.writeUint8(0);
    await sendPacket(session, 0x4441, writer.build());
  }
}
