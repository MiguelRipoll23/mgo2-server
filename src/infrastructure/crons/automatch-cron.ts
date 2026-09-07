import { container } from "../../container.ts";
import { AutomatchService } from "../../modules/game/automatch-service.ts";
import { GameService } from "../../modules/game/game-service.ts";
import { ActiveGameSessionsService } from "../tcp/services/active-game-sessions-service.ts";
import { LobbyTrackerService } from "../tcp/services/lobby-tracker-service.ts";
import { LobbyService } from "../../modules/lobby/lobby-service.ts";
import {
  writeMatchFound,
  writeMatchGame,
  writeMatchFailed,
  writeSearchPanel,
  MATCH_FOUND,
  MATCH_GAME,
  MATCH_FAILED,
  SEARCH_PANEL,
  SEARCH_PANEL_SIZE,
  SETTINGS_BLOCK_SIZE,
  MATCH_FOUND_SIZE,
} from "../tcp/servers/game/commands/games/automatch.ts";
import { PacketWriter } from "../../core/tcp/utils/packet-builder-util.ts";
import { encodePacket } from "../../core/tcp/services/packet-codec-service.ts";
import { writeLoggedBytes } from "../../core/tcp/utils/traffic-logger-util.ts";
import type { TcpSession } from "../../core/tcp/types/session-type.ts";
import { buildAutomatchSettingsBlock } from "../tcp/servers/game/commands/games/automatch-settings-block.ts";

const AUTOMATCH_TICK_MS = 5_000;

// One service instance per process; the lobby binding is refreshed each tick
// because the lobby list can change (instance mode re-seeds).
async function tick(): Promise<void> {
  const automatch = container.get(AutomatchService);
  const gameService = container.get(GameService);
  const sessions = container.get(ActiveGameSessionsService);
  const lobbyTracker = container.get(LobbyTrackerService);
  const lobbyService = container.get(LobbyService);

  const gameLobby = lobbyService.getCached().find((lobby) => lobby.typeId === 2);
  if (!gameLobby) return;

  automatch.setLobby(gameLobby.id, {
    hostedGameId: async (lobbyId, charaId) => {
      const games = await gameService.findByLobby(lobbyId);
      return games.find((game) => game.host_id === charaId)?.id ?? 0;
    },
    renameAndUnlock: async (gameId, name) => {
      await gameService.update(gameId, { name, password: "" });
    },
  });

  // Liveness: a searcher whose socket died is marked inactive so the next
  // reap drops them (the session set is the truth for this instance).
  const liveIds = new Set(
    sessions.list()
      .filter((session) => session.characterId !== null)
      .map((session) => session.characterId as number),
  );
  for (const searcher of automatch.searchersSnapshot()) {
    searcher.active = liveIds.has(searcher.charaId);
  }

  const byId = new Map<number, TcpSession>();
  for (const session of sessions.list()) {
    if (session.characterId !== null) byId.set(session.characterId, session);
  }

  async function push(session: TcpSession, command: number, payload: Uint8Array): Promise<void> {
    const bytes = encodePacket(command, payload, session.sequenceOut, "[automatch]");
    session.sequenceOut++;
    await writeLoggedBytes(session, bytes);
  }

  // 1. Release/form pass.
  await automatch.tick();
  const releasedMatches = automatch.takeFormedMatches();
  const failedMatches = automatch.takeFailedMatches();

  // 2. Match announcements to newly formed groups, then the release pushes.
  for (const match of releasedMatches) {
    const announcedRule = match.rules[0] ?? 0;
    const settings = buildAutomatchSettingsBlock(match.rules, match.map);
    for (const charaId of match.members) {
      const session = byId.get(charaId);
      if (!session) continue;
      const writer = new PacketWriter();
      writeMatchFound(writer, match.hostCharaId, gameLobby.id, gameLobby.subtypeId, announcedRule, settings);
      await push(session, MATCH_FOUND, writer.build());
    }
  }
  for (const match of releasedMatches) {
    for (const charaId of match.members) {
      const session = byId.get(charaId);
      if (!session) continue;
      const writer = new PacketWriter();
      writeMatchGame(writer, match.gameId);
      await push(session, MATCH_GAME, writer.build());
    }
  }
  // 3. Host-never-created failures: 0x43f3 to everyone still waiting.
  for (const match of failedMatches) {
    for (const charaId of match.members) {
      const session = byId.get(charaId);
      if (!session) continue;
      const writer = new PacketWriter();
      writeMatchFailed(writer, 0);
      await push(session, MATCH_FAILED, writer.build());
    }
  }

  // 4. Repaint every searcher's panel (fire-and-forget; the client never asks).
  for (const searcher of automatch.searchersSnapshot()) {
    if (searcher.state !== 0 /* SEARCHING */) continue;
    const session = byId.get(searcher.charaId);
    if (!session) continue;

    const matching = new Array<number>(23).fill(0);
    const inGame = new Array<number>(23).fill(0);
    for (const other of automatch.searchersSnapshot()) {
      if (other.state === 0) {
        const level = other.level();
        if (level >= 0 && level < 23) matching[level] = Math.min(matching[level] + 1, 15);
      }
    }

    const writer = new PacketWriter();
    writeSearchPanel(writer, matching, inGame, automatch.band(searcher), automatch.playersNeeded(searcher));
    await push(session, SEARCH_PANEL, writer.build());
  }
}

export function startAutomatchTicker(): void {
  setInterval(() => {
    tick().catch((error) => {
      console.error("[automatch] tick failed:", error);
    });
  }, AUTOMATCH_TICK_MS);
}

// Size sanity: the fixed 0x43e4 is 36 bytes; 0x43f1 is 19 + 204.
void SEARCH_PANEL_SIZE;
void MATCH_FOUND_SIZE;
void SETTINGS_BLOCK_SIZE;
