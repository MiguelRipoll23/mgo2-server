import { container } from "../../container.ts";
import { LobbyService } from "../../modules/lobby/lobby-service.ts";
import { LobbyTrackerService } from "../tcp/services/lobby-tracker-service.ts";

const LOBBIES_REFRESH_CRON = Deno.env.get("LOBBIES_REFRESH_CRON") ?? "*/15 * * * *";

Deno.cron("refresh-lobbies", LOBBIES_REFRESH_CRON, async () => {
  const lobbyService = container.get(LobbyService);
  // Must reload with this instance's id: without it the cache is rebuilt from the raw
  // players_count column (never updated in instance mode), wiping the live counts
  // — idle players included — until the next join/leave.
  const instanceId = container.get(LobbyTrackerService).getInstanceId() ?? undefined;
  await lobbyService.loadCache(instanceId);
  console.log("[cron] Lobby cache refreshed");
});
