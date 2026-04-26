import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { GameService } from "../../../../../../modules/game/game-service.ts";
import { LobbyService } from "../../../../../../modules/lobby/lobby-service.ts";
import { ActiveGameSessionsService } from "../../../../services/active-game-sessions-service.ts";
import { sendPacket, sendStartEndPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const SEARCH_QUERY_LENGTH = 16;

// Per-entry (59 bytes):
//   charId(4) + name(16) + isOnline(1) + lobbyId(2) + lobbyName(16) + gameId(4) + hostName(16)
async function buildSearchEntry(
  writer: PacketWriter,
  character: { id: number; name: string },
  activeSessions: ActiveGameSessionsService,
  gameService: GameService,
  lobbyService: LobbyService,
  characterService: CharacterService,
): Promise<void> {
  writer.writeUint32(character.id);
  writer.writeFixedString(character.name, 16);

  const targetSession = activeSessions.list().find((s) => s.characterId === character.id);
  const isOnline = targetSession != null;
  writer.writeUint8(isOnline ? 1 : 0);

  let wroteGameInfo = false;
  if (isOnline && targetSession!.gameId && targetSession!.lobbyId) {
    try {
      const [game, lobby] = await Promise.all([
        gameService.findById(targetSession!.gameId),
        lobbyService.findById(targetSession!.lobbyId),
      ]);
      if (game && lobby) {
        const hostChar = await characterService.findById(game.host_id);
        writer.writeUint16(lobby.id);
        writer.writeFixedString(lobby.name, 16);
        writer.writeUint32(game.id);
        writer.writeFixedString(hostChar?.name ?? "", 16);
        wroteGameInfo = true;
      }
    } catch {
      // lobby or game not found — fall through
    }
  }
  if (!wroteGameInfo) {
    writer.writeUint16(0);
    writer.writeFixedString("", 16);
    writer.writeUint32(0);
    writer.writeFixedString("", 16);
  }
}

@injectable()
@GameCommandHandler(0x4600)
export class SearchPlayerHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private activeSessions = inject(ActiveGameSessionsService),
    private gameService = inject(GameService),
    private lobbyService = inject(LobbyService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const query = reader.readFixedString(SEARCH_QUERY_LENGTH);

    await sendStartEndPacket(session, 0x4601);

    if (query.length > 0) {
      const character = await this.characterService.findByName(query);
      if (character) {
        const writer = new PacketWriter();
        await buildSearchEntry(
          writer,
          character,
          this.activeSessions,
          this.gameService,
          this.lobbyService,
          this.characterService,
        );
        await sendPacket(session, 0x4602, writer.build());
      }
    }

    await sendStartEndPacket(session, 0x4603);
  }
}
