# MGO2 server

A server implementation for Metal Gear Online 2, written in C# on .NET 10.
It provides the gate, account and gameplay lobby TCP servers, the UDP peer-to-peer
gameplay host, an HTTP API, a name server for domain redirection, a port-check
responder for NAT discovery and a PostgreSQL-backed persistence layer.

## Quick start

The install scripts pull the published images and bring up the whole
deployment. Run them as your own user: no elevation is needed, the Docker
daemon does the privileged work. The only requirement is access to the daemon
(membership of the `docker` group on Linux, or of `docker-users` with Docker
Desktop; running the script with `sudo` instead installs the machine-wide
deployment into `/opt/mgo2`).

Linux and macOS:

```sh
curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-linux-macos.sh | bash
```

Windows:

```powershell
irm https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-windows.ps1 | iex
```

Then go to the `Metal Gear Online` tab in RPCS3 settings and set the DNS server to your private IP (the value of `ADVERTISED_ADDRESS` in `appsettings.json`).

The install scripts download the deployment into a platform default directory
(`/opt/mgo2` for a root run on Linux, `~/.local/share/mgo2` on Linux or
`~/Library/Application Support/mgo2` on macOS for a user run, `%ProgramData%\mgo2`
for an elevated run on Windows and `%LOCALAPPDATA%\mgo2` for a user run). The
compose file and the `appsettings.json` the containers read their configuration
from live there together: edit that file and run the same command again to
apply it.

### Project layout

```
src/
├── Shared/            # Domain and protocol, shared by every executable
│   ├── Constants/     # Command identifiers, crypto keys, error codes, ports
│   ├── Domain/        # One service per domain: lobbies, games, clans, …
│   ├── Errors/        # ServerException
│   ├── InternalGrpc/  # Server-to-server coordination contract
│   ├── Interfaces/    # Command handler contracts
│   ├── Options/       # Bound configuration
│   ├── Persistence/   # EF Core entities and database context
│   ├── Tcp/           # Packet codec and command registry
│   ├── Types/         # Packets, sessions, frames, messages
│   ├── Udp/           # Peer session and command registry
│   └── Utils/         # Binary, crypto, LZSS, packet and string helpers
├── Infrastructure/    # Service registration and database initialisation
├── GateLobbyServer/
├── AccountLobbyServer/
├── GameLobbyServer/
├── GameplayServer/
├── Http/
├── Dns/
└── Stun/
```

### Container images

`.github/workflows/build-deploy.yml` runs the tests and then builds and
publishes one image per standalone project to the GitHub container registry,
using `docker/Dockerfile` with the project and its assembly as build arguments:

| Image                        | Project                              |
| ---------------------------- | ------------------------------------ |
| `mgo2-postgres`              | custom image; carries the migration bundle |
| `mgo2-gate-lobby-server`     | `src/GateLobbyServer`                |
| `mgo2-account-lobby-server`  | `src/AccountLobbyServer`             |
| `mgo2-game-lobby-server`     | `src/GameLobbyServer`                |
| `mgo2-gameplay-server`       | `src/GameplayServer`                 |
| `mgo2-http`                  | `src/Http`                           |
| `mgo2-dns`                   | `src/Dns`                            |
| `mgo2-stun`                  | `src/Stun`                           |

The `mgo2-postgres` image is the official PostgreSQL image extended with the
Entity Framework migration bundle; it applies the schema migrations on startup,
before it accepts connections. A push to `main` publishes `latest` and the
branch tag; a `v*` tag publishes the version. Pull requests only run the tests.
A push that matches no project skips the build and the deployment without
failing the run, and a manual run from the Actions tab builds and deploys every
image, whether or not anything changed.

After the images are published, the workflow deploys them on the self-hosted
runner carrying the `mgo2-server` label. The deploy job reads the connection
string from `/opt/mgo2/appsettings.json`; when that file does not exist it
downloads `appsettings.example.json` to `/opt/mgo2/appsettings.json` and stops
with an error. It applies the pending migrations with the bundle carried by the
`mgo2-postgres` image, and then recreates only the containers whose image this
run rebuilt. A failed migration stops the job, so no container is recreated
against a schema the migration has not applied, and `mgo2-postgres` is never
touched because it is managed separately.

The migration and every server read the same `DATABASE_CONNECTION_STRING`. Write
it in the keyword form, because the servers hand the value to Npgsql as it is
and Npgsql does not read the `postgres://…?sslmode=require` URL the Neon console
shows; the migration step also accepts that URL and rewrites it the way
`scripts/run-linux-macos.sh` does, so a URL left in the file does not stop the
migration. Either way, use the **direct** (non-pooled) host, which is the one
Neon recommends for migrations, and `SSL Mode=Require`, since Neon accepts TLS
connections only. Npgsql validates Neon's certificate against the system trust
store, so no client certificate or root certificate is needed, and Neon's
`channel_binding=require` needs no counterpart because Npgsql negotiates channel
binding by default — the workflow's rewrite carries it over when a URL is used.
For example:

```json
"DATABASE_CONNECTION_STRING": "Host=ep-xxx.eu-central-1.aws.neon.tech;Port=5432;Database=mgo2;Username=neondb_owner;Password=REPLACE_ME;SSL Mode=Require"
```

`compose.yaml` references these images directly, so a deployment pulls the
published stack and never builds:

```sh
docker compose up                               # run the published images
docker compose -f compose.dev.yaml up --build   # build and run from source
```

`compose.dev.yaml` is a local-build overlay: it includes `compose.yaml` and adds
the build step of every image, tagging them `:dev` locally.

The servers read their shared settings from an `appsettings.json` baked into each
image from `appsettings.example.json`. A deployment mounts its own
`appsettings.json` over the baked one, read-only, so the settings live in one
file: the install scripts create it from the example on the first run and never
write it again, so edits survive every update and are applied by restarting the
container. The example ships the two settings that belong to the deployment
rather than to the servers — a random `JWT_SECRET` and the detected
`ADVERTISED_ADDRESS` — as the `REPLACE_ME` placeholder, which the first run
substitutes: a random secret replaces the first, a detected address the second,
and a placeholder left after a run that could not detect the address is how the
operator sets it by hand. A real environment variable of the same upper-case
name still overrides any file value. The per-container identity
(the `LOBBY_*` and `GAMEPLAY_SERVER_*` blocks) stays in `compose.yaml`, where a
change needs `up -d` to recreate the containers instead of a restart.

## Development

```sh
dotnet build                             # Build every project
dotnet test                              # Run the unit tests
dotnet run --project src/GateLobbyServer     # Run the gate
dotnet run --project src/AccountLobbyServer  # Run the account server
dotnet run --project src/GameLobbyServer     # Run one gameplay lobby
dotnet run --project src/GameplayServer      # Run a gameplay server
dotnet run --project src/Stun                # Run the port-check responder
```

Every lobby server publishes the lobby it hosts, so it takes its identity from
the environment: the gate runs with `LOBBY_NAME=GATE LOBBY_PORT=5731`, the
account server with `LOBBY_NAME=ACCOUNT LOBBY_PORT=5732`, and a gameplay lobby
adds the `LOBBY_SUBTYPE` that selects its game type.

The scripts of `scripts/` split building from running: `build-linux-macos.sh`
and `build-windows.ps1` build every project, and `run-linux-macos.sh` and
`run-windows.ps1` start the whole deployment from the built binaries against
the database.

```sh
scripts/build-linux-macos.sh             # Build every project
scripts/run-linux-macos.sh               # Run the whole deployment
```

To run the whole stack in containers from the source tree instead, build the
images locally with `compose.dev.yaml` (the production `compose.yaml` only
pulls them):

```sh
docker compose -f compose.dev.yaml up --build
```

### Coordination and Discord

Every gameplay lobby opens one persistent, bidirectional gRPC connection to the
HTTP API: the lobby reports the players that connect and disconnect, and the API
pushes flash news back down the same connection. The API keeps the per-lobby and
the global player counts from those events, and relays a flash to every
connected lobby whether it was raised through `POST /flash-news/broadcast` or
through the Discord `/flash` command. The channel named `players [n]` mirrors
the global count and every connection and disconnection is written in it.

The API and a lobby are independent of each other: a lobby that cannot reach the
coordination endpoint keeps serving its players and retries once a minute, and
Discord is optional and never on the path of a game. The bot receives the
`/flash` command over its gateway WebSocket connection and answers it, and
`POST /discord/messages` writes a message from the bot into a channel.
`docs/discord-integration.md` describes the contract, the settings and the
Discord application to create.

### Telemetry

The servers support OpenTelemetry: they export their metrics over OTLP/gRPC to
a collector that listens on port 4317.

Telemetry is always on: `appsettings.example.json` ships `OTEL_ENABLED=true`
and `OTEL_PORT=4317`, the install scripts leave them alone, and the run scripts
export the same values. The scripts never touch the collector; the Alloy
receiver running on the host is configured by hand, and its port has to match
`OTEL_PORT`. The servers log at `Warning` by default; `LOG_LEVEL` of
`appsettings.json` changes it.

## Acknowledgements

Protocol research and reverse engineering that made this project possible:

- GHzGangster — [Nomad](https://github.com/GHzGangster/Nomad)
- [mgo2pc.com](https://mgo2pc.com/) community server
- boiln — [echo](https://github.com/boiln/echo)
