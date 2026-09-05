import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { ActiveGameSessionsService } from "../../../../services/active-game-sessions-service.ts";
import { LobbyService } from "../../../../../../modules/lobby/lobby-service.ts";
import { sendPacket, sendResult, sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const MAX_PER_PACKET = 15;

// Per-entry in the GET list (43 bytes):
//   type(1) + targetId(4) + targetName(16) + isOnline(1) + pad(3) + lobbyId(2) + lobbyName(16)
async function buildFriendEntry(
  writer: PacketWriter,
  entry: { targetId: number; targetName: string; type: number },
  activeSessions: ActiveGameSessionsService,
  lobbyService: LobbyService,
): Promise<void> {
  writer.writeUint8(entry.type);
  writer.writeUint32(entry.targetId);
  writer.writeFixedString(entry.targetName, 16);

  const targetSession = activeSessions.list().find((s) => s.characterId === entry.targetId);
  const isOnline = targetSession != null;
  writer.writeUint8(isOnline ? 1 : 0);
  writer.writePadding(3);

  if (isOnline && targetSession!.lobbyId) {
    try {
      const lobby = await lobbyService.findById(targetSession!.lobbyId);
      writer.writeUint16(lobby.id);
      writer.writeFixedString(lobby.name, 16);
    } catch {
      writer.writeUint16(0);
      writer.writeFixedString("", 16);
    }
  } else {
    writer.writeUint16(0);
    writer.writeFixedString("", 16);
  }
}

// ============================================================================
// ADD_FRIEND_OR_BLOCKED (0x4500) → ack (0x4501) + data (0x4502, 25 bytes)
// Data: type(1) + targetId(4) + targetName(16) + pad(4)
// ============================================================================

@injectable()
@GameCommandHandler(0x4500)
export class AddFriendsBlockedHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length >= 5) {
      const reader = new PacketReader(packet.payload);
      const type = reader.readUint8();
      const targetId = reader.readUint32();
      await this.characterService.addFriendOrBlocked(characterId, targetId, type);

      const target = await this.characterService.findById(targetId);
      const dataWriter = new PacketWriter();
      // Reply is a SINGLE 0x4502 entry: {u32 lead(0), u32 target, u8 state,
      // char[16] name}. ADDLIST breaks the start/entries/end idiom — no
      // separate ack packet exists in the client's parser.
      dataWriter.writeUint32(0);
      dataWriter.writeUint32(targetId);
      dataWriter.writeUint8(type);
      dataWriter.writeFixedString(target?.name ?? "", 16);
      await sendPacket(session, 0x4502, dataWriter.build());
      return;
    }
    await sendResult(session, 0x4502, 1);
  }
}

// ============================================================================
// REMOVE_FRIEND_OR_BLOCKED (0x4510) → ack (0x4511) + data (0x4512, 9 bytes)
// Data: type(1) + targetId(4) + pad(4)
// ============================================================================

@injectable()
@GameCommandHandler(0x4510)
export class RemoveFriendsBlockedHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length >= 5) {
      const reader = new PacketReader(packet.payload);
      const type = reader.readUint8();
      const targetId = reader.readUint32();
      await this.characterService.removeFriendOrBlocked(characterId, targetId);

      const dataWriter = new PacketWriter();
      // Reply is a SINGLE 0x4512 entry: {u32 lead(0), u8 state, u32 target} —
      // note the field order differs from 0x4502 (state before id, no name).
      dataWriter.writeUint32(0);
      dataWriter.writeUint8(type);
      dataWriter.writeUint32(targetId);
      await sendPacket(session, 0x4512, dataWriter.build());
      return;
    }
    await sendResult(session, 0x4512, 1);
  }
}

// ============================================================================
// GET_FRIENDS_BLOCKED_LIST (0x4580) → START(0x4581) DATA(0x4582)* END(0x4583)
// Per entry (43 bytes): type(1) + targetId(4) + targetName(16) + isOnline(1)
//                       + pad(3) + lobbyId(2) + lobbyName(16)
// ============================================================================

@injectable()
@GameCommandHandler(0x4580)
export class GetFriendsBlockedListHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private activeSessions = inject(ActiveGameSessionsService),
    private lobbyService = inject(LobbyService),
  ) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const characterId = session.characterId ?? 0;
    const entries = characterId > 0
      ? await this.characterService.getFriendsAndBlockedWithNames(characterId)
      : [];

    await sendStartEndPacket(session, 0x4581);

    for (let offset = 0; offset < entries.length; offset += MAX_PER_PACKET) {
      const batch = entries.slice(offset, offset + MAX_PER_PACKET);
      const writer = new PacketWriter();
      for (const entry of batch) {
        await buildFriendEntry(writer, entry, this.activeSessions, this.lobbyService);
      }
      await sendPacket(session, 0x4582, writer.build());
    }

    await sendStartEndPacket(session, 0x4583);
  }
}
