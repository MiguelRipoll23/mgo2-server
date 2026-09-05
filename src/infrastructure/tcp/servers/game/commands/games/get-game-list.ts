import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { sendPacket, sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

// One 0x4302 entry, exactly 55 bytes (0x37) — the client reads a fixed-width
// record and the browser squeezes most host settings out of two bitfield bytes.
// Layout: u32 id, char[16] name, u8 hostOptions, u8 unknown(0x8), u8 rule,
// u8 map, u8 pad, u8 maxPlayers, u8 stance, u8 commonA, u8 commonB,
// u8 playerCount, s32 ping, u8 friendBlock, u8 levelLimitTolerance,
// u32 levelLimitBase, u32 averageExp, u32 hostScore, u32 hostVotes,
// u16 pad, u8 trailing(0x63).
const GAME_ELEMENT_SIZE = 55;
const MAX_PLAYERS = 18;

// hostOptions bits
const HOST_PASSWORD = 0b1;
// commonA bits (A_ALWAYS is set by the original; meaning unknown)
const A_ALWAYS = 0b100;
// commonB bits
const B_AUTO_ASSIGN = 0b10;
const B_VOICE_CHAT = 0b1000000;
// Trailing byte constant the original writes verbatim.
const TRAILING_BYTE = 0x63;

@injectable()
@GameCommandHandler(0x4300)
export class GetGameListHandler implements ICommandHandler {
  constructor(private gameService = inject(GameService)) {}

  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const lobbyId = session.lobbyId ?? 0;
    const games =
      lobbyId > 0 ? await this.gameService.findByLobby(lobbyId) : [];

    // Per-host lifetime rating aggregates for the browser's score/votes
    // columns — votes are history and outlive their games.
    const hostIds = [...new Set(games.map((game) => game.host_id))];
    const ratings = await this.gameService.getHostRatingSums(hostIds);

    await sendStartEndPacket(session, 0x4301);

    for (const game of games) {
      const writer = new PacketWriter();
      const hasPassword = game.password !== "";

      const hostOptions = hasPassword ? HOST_PASSWORD : 0;
      const commonA = A_ALWAYS;
      const commonB = B_AUTO_ASSIGN | B_VOICE_CHAT;
      const playerCount = Math.min(
        await this.gameService.countPlayers(game.id),
        Math.min(game.max_players, MAX_PLAYERS),
      );
      const rating = ratings.get(game.host_id) ?? { ratingSum: 0, votes: 0 };

      writer.writeUint32(game.id);
      writer.writeFixedString(game.name, 16);
      writer.writeUint8(hostOptions);
      writer.writeUint8(0x8); // unknown constant, written verbatim by the original
      writer.writeUint8(0); // rule
      writer.writeUint8(0); // map
      writer.writeUint8(0); // pad
      writer.writeUint8(Math.min(game.max_players, MAX_PLAYERS));
      writer.writeUint8(game.stance);
      writer.writeUint8(commonA);
      writer.writeUint8(commonB);
      writer.writeUint8(playerCount);
      writer.writeUint32(game.ping);
      writer.writeUint8(0); // friendBlock
      writer.writeUint8(0); // levelLimitTolerance
      writer.writeUint32(0); // levelLimitBase
      writer.writeUint32(0); // averageExperience
      writer.writeUint32(rating.ratingSum); // hostScore — lifetime rating sum
      writer.writeUint32(rating.votes); // hostVotes — lifetime vote count
      writer.writeUint16(0); // pad
      writer.writeUint8(TRAILING_BYTE);
      writer.writePadding(Math.max(0, GAME_ELEMENT_SIZE - writer.size));

      await sendPacket(session, 0x4302, writer.build());
    }

    await sendStartEndPacket(session, 0x4303);
  }
}
