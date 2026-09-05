import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";
import {
  RESULT_GENERAL,
  RESULT_GAME_FULL,
  RESULT_GAME_PASSWORD_INCORRECT,
  RESULT_NONE,
} from "../../../../../../core/constants/error-codes-constants.ts";

// Join reply (0x4321) layout read by the client's parser:
//   result u32 | public ip[16] | public port u16 | private ip[16] |
//   private port u16 | canRateHost u8   = 41 bytes.
// The parser stops after the trailing u8; the two extra bytes below (rule,
// map) are what echo appends and are read by nothing — reproduced for parity.
const JOIN_SUCCESS_SIZE = 43;
const IP_LENGTH = 16;
// The 0x4320 request: {u32 gameId, char password[16]} (password read only if
// present). Blowfish-encrypted inbound.
const PASSWORD_LENGTH = 16;
const MAX_PLAYERS = 18;

@injectable()
@GameCommandHandler(0x4320)
export class JoinGameHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    if (reader.remaining() < 4) {
      await sendResultReply(session, RESULT_GENERAL, null, 0);
      return;
    }

    const gameId = reader.readUint32();
    const password = reader.remaining() >= PASSWORD_LENGTH
      ? readNulString(reader.readBytes(PASSWORD_LENGTH))
      : "";

    const game = gameId > 0 ? await this.gameService.findById(gameId) : null;

    if (!game) {
      await sendResultReply(session, RESULT_GENERAL, null, 0);
      return;
    }

    if (game.password !== "" && game.password !== password) {
      await sendResultReply(session, RESULT_GAME_PASSWORD_INCORRECT, null, 0);
      return;
    }

    const capacity = game.max_players > 0
      ? Math.min(game.max_players, MAX_PLAYERS)
      : MAX_PLAYERS;
    const occupants = await this.gameService.countPlayers(game.id);
    if (occupants >= capacity) {
      await sendResultReply(session, RESULT_GAME_FULL, null, 0);
      return;
    }

    // The host's registered peer-to-peer endpoint (from its 0x4700 push).
    // Without it a peer connection is impossible, so this is a failure, not
    // an empty success.
    const endpoint = await this.gameService.getConnectionInfo(game.host_id);
    if (!endpoint) {
      await sendResultReply(session, RESULT_GENERAL, null, 0);
      return;
    }

    await this.gameService.addPlayer(game.id, session.characterId ?? 0);
    session.gameId = game.id;

    // THE HOST-RATING GATE, and the place it actually belongs. 0x4313's gate
    // byte is necessary but not sufficient: this reply writes the SAME slot
    // and lands after any pre-join 0x4313, overwriting it. The client keeps
    // no memory across joins — its own "already voted" latches are cleared
    // when the picker is re-armed — so only the server can stop a player
    // rejoining and voting repeatedly. It is 0 for the host's own session
    // (the client must not offer rating yourself) and for anyone who has
    // already voted on this game.
    const canRate = session.characterId !== game.host_id &&
      !(await this.gameService.hasRatedHostOf(game.id, session.characterId ?? 0));

    await sendResultReply(
      session,
      RESULT_NONE,
      endpoint,
      game.current_game,
      canRate,
    );
  }
}

/** Writes the join reply. On a nonzero result the client skips the body, so a failure is a bare 4-byte result. */
async function sendResultReply(
  session: TcpSession,
  result: number,
  endpoint: { publicIp: string; publicPort: number; privateIp: string; privatePort: number } | null,
  currentGame: number,
  canRateHost = false,
): Promise<void> {
  if (result !== RESULT_NONE || endpoint === null) {
    const writer = new PacketWriter().writeUint32(result);
    await sendPacket(session, 0x4321, writer.build());
    return;
  }

  const writer = new PacketWriter();
  writer.writeUint32(result);
  writer.writeFixedString(endpoint.publicIp, IP_LENGTH);
  writer.writeUint16(endpoint.publicPort);
  writer.writeFixedString(endpoint.privateIp, IP_LENGTH);
  writer.writeUint16(endpoint.privatePort);
  writer.writeUint8(canRateHost ? 1 : 0); // can-rate-host gate
  writer.writeUint8(currentGame); // echo's trailing rule (unread)
  writer.writeUint8(0); // echo's trailing map (unread)
  writer.writePadding(Math.max(0, JOIN_SUCCESS_SIZE - writer.size));
  await sendPacket(session, 0x4321, writer.build());
}

function readNulString(bytes: Uint8Array): string {
  const chars: string[] = [];
  for (const byte of bytes) {
    if (byte === 0) break;
    chars.push(String.fromCharCode(byte));
  }
  return chars.join("");
}
