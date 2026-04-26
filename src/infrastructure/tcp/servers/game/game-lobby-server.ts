import { BaseTcpServer } from "../../../../core/tcp/services/base-tcp-server-service.ts";
import { container } from "../../../../container.ts";
import type { TcpSession } from "../../../../core/tcp/types/session-type.ts";
import { ActiveGameSessionsService } from "../../services/active-game-sessions-service.ts";
import { LobbyTrackerService } from "../../services/lobby-tracker-service.ts";
import "./commands/check-session.ts";
import "./commands/hub.ts";
import "./commands/chat.ts";
import "./commands/messages.ts";
import "./commands/characters/index.ts";
import "./commands/games/index.ts";
import "./commands/clans/index.ts";

export class GameLobbyServer extends BaseTcpServer {
  protected readonly serverType = "game" as const;
  protected readonly port: number;
  private readonly name: string;
  private readonly activeGameSessionsService = container.get(ActiveGameSessionsService);
  private readonly lobbyTrackerService = container.get(LobbyTrackerService);

  constructor(port: number, name: string) {
    super();
    this.port = port;
    this.name = name;
  }

  protected override get logPrefix(): string {
    return `tcp:${this.name.toLowerCase().replace(/\s+/g, "_")}`;
  }

  protected override onSessionCreated(session: TcpSession): void {
    this.activeGameSessionsService.add(session);
  }

  protected override onSessionDestroyed(session: TcpSession): void {
    this.activeGameSessionsService.remove(session);
    this.lobbyTrackerService.leaveLobby(session);
  }
}
