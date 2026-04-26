import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { sendPacket } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { ActiveGameSessionsService } from "../../../services/active-game-sessions-service.ts";

const SERVER_PREFIX = "Server | ";
const MAX_MESSAGE_LENGTH = 127;

function buildChatPayload(
  characterId: number,
  flag2: number,
  message: string,
): Uint8Array {
  const writer = new PacketWriter();
  writer.writeUint32(characterId);
  writer.writeUint8(flag2);
  // null-terminated string: write message length + 1 bytes (string + null)
  writer.writeFixedString(message, message.length + 1);
  return writer.build();
}

function stripMessagePrefix(message: string): string {
  let msg = message;
  if (msg.startsWith("/all")) {
    msg = msg.slice(4);
  } else if (msg.startsWith("/team")) {
    msg = msg.slice(5);
  }
  if (msg.startsWith(" ")) {
    msg = msg.trimStart();
  }
  return msg;
}

@injectable()
@GameCommandHandler(0x4400)
export class SendChatHandler implements ICommandHandler {
  constructor(
    private activeSessions = inject(ActiveGameSessionsService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (session.gameId === null || session.characterId === null) {
      return;
    }

    if (packet.payload.length < 1) return;

    const reader = new PacketReader(packet.payload);
    const flag2 = reader.readUint8();
    const raw = reader.remaining() > 0
      ? reader.readFixedString(Math.min(reader.remaining(), MAX_MESSAGE_LENGTH))
      : "";

    const message = stripMessagePrefix(raw);

    // Block server message impersonation
    if (message.toLowerCase().startsWith(SERVER_PREFIX.toLowerCase())) {
      const selfPayload = buildChatPayload(
        session.characterId,
        flag2,
        SERVER_PREFIX + "You can't send server messages.",
      );
      await sendPacket(session, 0x4401, selfPayload);
      return;
    }

    // Broadcast to all sessions in the same game room
    const payload = buildChatPayload(session.characterId, flag2, message);
    const gameId = session.gameId;

    for (const target of this.activeSessions.list()) {
      if (target.gameId === gameId) {
        await sendPacket(target, 0x4401, payload);
      }
    }
  }
}
