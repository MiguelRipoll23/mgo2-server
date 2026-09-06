import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { SessionsService } from "../../../../../modules/auth/sessions-service.ts";
import { CharacterService } from "../../../../../modules/character/character-service.ts";
import { LobbyTrackerService } from "../../../services/lobby-tracker-service.ts";
import { sendResult } from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { storedSessionFieldFromWire } from "../../../../../core/tcp/utils/crypto-util.ts";
import {
  RESULT_NONE,
  RESULT_LOBBY_LOGIN_AGAIN,
} from "../../../../../core/constants/error-codes-constants.ts";

import { GameService } from "../../../../../modules/game/game-service.ts";

// Field length of the check-session value the client derives from its login
// token. The payload is {u32 claimedCharacterId, byte[16] sessionField}.
const SESSION_FIELD_LENGTH = 16;

@injectable()
@GameCommandHandler(0x3003)
export class CheckSessionHandler implements ICommandHandler {
  constructor(
    private sessionsService = inject(SessionsService),
    private characterService = inject(CharacterService),
    private lobbyTrackerService = inject(LobbyTrackerService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length < 4 + SESSION_FIELD_LENGTH) {
      console.log(
        `[tcp][game] check session: payload too short (${packet.payload.length} bytes)`,
      );
      await sendResult(session, 0x3004, RESULT_LOBBY_LOGIN_AGAIN);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const claimedCharacterId = reader.readUint32();
    const sessionField = storedSessionFieldFromWire(reader.readBytes(SESSION_FIELD_LENGTH));

    // The client derived this from its login token, so the session row
    // already holds the same value and it is matched directly. Nothing is
    // decoded back into a token.
    const user = await this.sessionsService.findSessionByToken(sessionField);

    if (user === null) {
      console.log(
        `[tcp][game] check session: no account holds the presented session`,
      );
      await sendResult(session, 0x3004, RESULT_LOBBY_LOGIN_AGAIN);
      return;
    }

    session.userId = user.user_id;

    // The client decides which of its characters is entering, so the check is
    // OWNERSHIP, not equality with whatever we last recorded. Requiring the
    // two to match rejected a legitimate login: creating a character points
    // current_character_id at the new one, and entering the lobby as a
    // DIFFERENT character then failed with a bogus invalid-session error.
    //
    // A leaked token still cannot claim someone else's character: the id has
    // to belong to this account. What it can do is pick between this
    // account's own characters, which is exactly what the character-select
    // screen is for.
    const character = await this.characterService.findById(claimedCharacterId);
    if (!character || character.user_id !== user.user_id) {
      console.log(
        `[tcp][game] check session: user ${user.user_id} claimed character ${claimedCharacterId}, which it does not own`,
      );
      await sendResult(session, 0x3004, RESULT_LOBBY_LOGIN_AGAIN);
      return;
    }

    session.characterId = claimedCharacterId;

    // The lobby is the server the client connected to (stamped on the session
    // at connection time), not characters.lobby_id — that column was never
    // written, so trusting it rejected create-game with invalid-session.
    // Persist it so the character's parked lobby stays accurate (e.g. after
    // the account server clears it on disconnect).
    if (session.lobbyId !== null) {
      await this.characterService.setLobby(claimedCharacterId, session.lobbyId);
      this.lobbyTrackerService.joinLobby(session, session.lobbyId);
      await this.lobbyTrackerService.syncAllLobbyCounts();
    }

    await sendResult(session, 0x3004, RESULT_NONE);
  }
}

// ── 0x4700: the client's peer-to-peer endpoint ──────────────────────────────
// Payload: {u16 privatePort, char privateIp[16], u16 publicPort, u16 unknown}.
// The endpoint is persisted against the character: the join reply (0x4321)
// hands it to every joining player, so a join cannot proceed past the P2P
// handoff unless the host registered here. The public IP is taken from the
// socket rather than trusted from the payload.

const PRIVATE_IP_LENGTH = 16;
const CONNECTION_INFO_SIZE = 2 + PRIVATE_IP_LENGTH + 2;

@injectable()
@GameCommandHandler(0x4700)
export class GetPlayerDataHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    if (packet.payload.length < CONNECTION_INFO_SIZE) {
      // Nothing readable to register — acknowledge anyway: the client blocks
      // on this reply and an unanswered command is a guaranteed hang.
      await sendResult(session, 0x4701, RESULT_NONE);
      return;
    }

    const reader = new PacketReader(packet.payload);
    const privatePort = reader.readUint16();
    const privateIp = readNulString(reader.readBytes(PRIVATE_IP_LENGTH));
    const publicPort = reader.readUint16();

    const charaId = session.characterId;
    const publicIp = publicIpFromRemote(session.remoteAddress);
    if (charaId === null || publicIp === null) {
      await sendResult(session, 0x4701, RESULT_NONE);
      return;
    }

    await this.gameService.saveConnectionInfo(charaId, {
      publicIp,
      publicPort,
      privateIp,
      privatePort,
    });

    await sendResult(session, 0x4701, RESULT_NONE);
  }
}

function readNulString(bytes: Uint8Array): string {
  const chars: string[] = [];
  for (const byte of bytes) {
    if (byte === 0) break;
    chars.push(String.fromCharCode(byte));
  }
  return chars.join("");
}

// Strips the port from "host:port" (IPv4) or "[host]:port" (IPv6).
function publicIpFromRemote(remoteAddress: string): string | null {
  if (remoteAddress.startsWith("[")) {
    const end = remoteAddress.indexOf("]");
    return end > 0 ? remoteAddress.slice(1, end) : null;
  }
  const colon = remoteAddress.lastIndexOf(":");
  return colon > 0 ? remoteAddress.slice(0, colon) : remoteAddress;
}
