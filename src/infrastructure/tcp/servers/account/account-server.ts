import { BaseTcpServer } from "../../../../core/tcp/services/base-tcp-server-service.ts";
import type { TcpSession } from "../../../../core/tcp/types/session-type.ts";
import { CharacterService } from "../../../../modules/character/character-service.ts";
import "./commands/check-session.ts";
import "./commands/get-character-list.ts";
import "./commands/create-character.ts";
import "./commands/select-character.ts";
import "./commands/delete-character.ts";
import "./commands/check-character-name.ts";

export class AccountServer extends BaseTcpServer {
  protected readonly serverType = "account" as const;
  protected readonly port: number;

  constructor(port: number, private characterService: CharacterService) {
    super();
    this.port = port;
  }

  protected override onSessionDestroyed(session: TcpSession): void {
    if (session.characterId !== null) {
      this.characterService.setLobby(session.characterId, null).catch((e) =>
        this.log.error("setLobby on disconnect failed:", e)
      );
    }
  }
}
