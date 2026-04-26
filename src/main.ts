import { container } from "./container.ts";
import { init as initTelemetry } from "./core/services/telemetry-service.ts";
import { DatabaseService } from "./core/services/database-service.ts";
import { LobbyService } from "./modules/lobby/lobby-service.ts";
import { LobbyType } from "./db/schema.ts";
import { CharacterService } from "./modules/character/character-service.ts";
import { LobbyTrackerService } from "./infrastructure/tcp/services/lobby-tracker-service.ts";
import { HTTPService } from "./infrastructure/http/services/http-service.ts";
import { GateServer } from "./infrastructure/tcp/servers/gate/gate-server.ts";
import { AccountServer } from "./infrastructure/tcp/servers/account/account-server.ts";
import { GameLobbyServer } from "./infrastructure/tcp/servers/game/game-lobby-server.ts";
import { DnsServer } from "./infrastructure/dns/dns-server.ts";
import "./infrastructure/crons/refresh-lobbies-cron.ts";

const instanceId = Deno.env.get("INSTANCE_ID") ?? crypto.randomUUID();

initTelemetry();

container.get(DatabaseService).init();

container.get(LobbyTrackerService).setInstanceId(instanceId);

const lobbyService = container.get(LobbyService);
await lobbyService.loadCache(instanceId);

const allLobbies = lobbyService.getCached();
const gatePort =
  allLobbies.find((lobby) => lobby.typeId === LobbyType.GATE)!.port;
const accountPort =
  allLobbies.find((lobby) => lobby.typeId === LobbyType.ACCOUNT)!.port;

const gate = new GateServer(gatePort);
const account = new AccountServer(accountPort, container.get(CharacterService));
const httpService = container.get(HTTPService);

const gameLobbyServers = allLobbies
  .filter((lobby) => lobby.typeId === LobbyType.GAME)
  .map((lobby) => new GameLobbyServer(lobby.port, lobby.name));

const disableDns = Deno.env.get("DISABLE_DNS_SERVER") === "true";
const servers = [
  gate.start(),
  account.start(),
  httpService.listen(),
  ...gameLobbyServers.map((gameLobby) => gameLobby.start()),
];

if (!disableDns) {
  servers.push(new DnsServer().start());
}

await Promise.all(servers);
