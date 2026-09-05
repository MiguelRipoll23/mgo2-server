import type { TcpSession } from "../../../core/tcp/types/session-type.ts";
import { LobbyService } from "../../../modules/lobby/lobby-service.ts";
import { injectable, inject } from "@needle-di/core";

@injectable()
export class LobbyTrackerService {
  private readonly sessionsByLobby = new Map<number, Set<TcpSession>>();
  private instanceId: string | null = null;

  constructor(private readonly lobbyService = inject(LobbyService)) {}

  setInstanceId(id: string): void {
    this.instanceId = id;
  }

  getInstanceId(): string | null {
    return this.instanceId;
  }

  joinLobby(session: TcpSession, lobbyId: number): void {
    this.leaveLobby(session);

    if (!this.sessionsByLobby.has(lobbyId)) {
      this.sessionsByLobby.set(lobbyId, new Set());
    }
    this.sessionsByLobby.get(lobbyId)!.add(session);
  }

  leaveLobby(session: TcpSession): void {
    for (const sessions of this.sessionsByLobby.values()) {
      sessions.delete(session);
    }
  }

  getPlayerCount(lobbyId: number): number {
    return this.sessionsByLobby.get(lobbyId)?.size ?? 0;
  }

  async syncAllLobbyCounts(): Promise<void> {
    for (const [lobbyId, sessions] of this.sessionsByLobby) {
      if (this.instanceId) {
        await this.lobbyService.upsertInstanceCount(
          this.instanceId,
          lobbyId,
          sessions.size,
        );
      } else {
        await this.lobbyService.updatePlayerCount(lobbyId, sessions.size);
      }
    }
  }
}
