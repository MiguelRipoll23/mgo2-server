import { inject, injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../core/tcp/utils/packet-builder-util.ts";
import { LobbyService } from "../../../../../modules/lobby/lobby-service.ts";
import { LobbyType } from "../../../../../db/schema.ts";
import type { NewWeaponTally } from "../../../../../db/schema.ts";
import { GameService } from "../../../../../modules/game/game-service.ts";
import { RoundReportService } from "../../../../../modules/game/round-report-service.ts";
import { LobbyTrackerService } from "../../../services/lobby-tracker-service.ts";
import {
  sendPacket,
  sendResult,
  sendStartEndPacket,
} from "../../../../../core/tcp/utils/session-helpers-util.ts";
import { RESULT_NONE } from "../../../../../core/constants/error-codes-constants.ts";

const MAX_LOBBIES_PER_PACKET = 8;

// The ELF's caller caps 0x43a2 entries at 50; each entry is 7 bytes.
const MAX_WEAPON_TALLIES = 50;
const WEAPON_TALLY_BYTES = 7;

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
          // No 64-byte text block: this server serves the 1.36 build, whose 0x4902
          // parser reads entries at a FIXED 35-byte stride (the text block only
          // exists on the 1.0 disc build). Sending the disc layout to 1.36 makes
          // the client parse every entry after the first from the wrong offset —
          // Lobby Select then shows no Training row. The reference project
          // reproduced exactly this live (mgo2server ClientVersion.java).
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

// Round end (0x43a2 → 0x43a3): the host submits weapon tallies for one
// player — {u32 charaId, u32 count, count × {u8 weapon, u16 a, u16 b, u16 c}}.
// The ELF's caller caps entries at 50; a larger count is a mis-parse, and a
// short payload is dropped rather than stored in part. Participation mirrors
// 0x4390: the target must be in the game or in the round snapshot.
@injectable()
@GameCommandHandler(0x43a2)
export class HostUnknown43a2Handler implements ICommandHandler {
  constructor(
    private gameService = inject(GameService),
    private reportService = inject(RoundReportService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const gameId = session.gameId;
    const game = gameId !== null ? await this.gameService.findById(gameId) : null;

    if (game !== null && packet.payload.length >= 8) {
      const view = new DataView(
        packet.payload.buffer,
        packet.payload.byteOffset,
      );
      const charaId = view.getUint32(0, false);
      const count = view.getUint32(4, false);

      if (count > MAX_WEAPON_TALLIES) {
        console.warn(
          `[tcp][game] game ${game.id}: 0x43a2 for character ${charaId} declared ${count} weapon entries, past the client's own cap of ${MAX_WEAPON_TALLIES}; dropped as a mis-parse.`,
        );
      } else if (packet.payload.length < 8 + count * WEAPON_TALLY_BYTES) {
        console.warn(
          `[tcp][game] game ${game.id}: 0x43a2 for character ${charaId} declared ${count} weapon entries but carries ${packet.payload.length - 8} bytes; dropped rather than stored in part.`,
        );
      } else if (await this.gameService.isInGame(game.id, game.host_id, charaId)) {
        const tallies: NewWeaponTally[] = [];
        for (let i = 0; i < count; i++) {
          const at = 8 + i * WEAPON_TALLY_BYTES;
          tallies.push({
            game_id: game.id,
            character_id: charaId,
            weapon_id: packet.payload[at],
            value_a: view.getUint16(at + 1, false),
            value_b: view.getUint16(at + 3, false),
            value_c: view.getUint16(at + 5, false),
          });
        }
        await this.reportService.insertTallies(tallies);
      } else {
        console.warn(
          `[tcp][game] game ${game.id}: weapon tallies for character ${charaId} who neither is in the game nor played the round; dropped.`,
        );
      }
    }

    await sendResult(session, 0x43a3, RESULT_NONE);
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
