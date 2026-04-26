import { container } from "../../container.ts";
import { LobbyService } from "../../modules/lobby/lobby-service.ts";

const LOBBIES_REFRESH_CRON = Deno.env.get("LOBBIES_REFRESH_CRON") ?? "*/15 * * * *";

Deno.cron("refresh-lobbies", LOBBIES_REFRESH_CRON, async () => {
  const lobbyService = container.get(LobbyService);
  await lobbyService.loadCache();
  console.log("[cron] Lobby cache refreshed");
});
