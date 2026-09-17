# MGO2 server

A server implementation for Metal Gear Online 2, written in C# on .NET 10.
It provides the gate, account and gameplay lobby TCP servers, the UDP peer-to-peer
gameplay host, an HTTP API, a name server for domain redirection, a port-check
responder for NAT discovery and a PostgreSQL-backed persistence layer.

## Quick start

The install scripts pull the published images and bring up the whole
deployment.

Linux and macOS:

```sh
curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-linux-macos.sh | bash
```

Windows:

```powershell
irm https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-windows.ps1 | iex
```

Then go to the `Metal Gear Online` tab in RPCS3 settings and set the DNS server to your private IP (the value of `ADVERTISED_ADDRESS`).

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

`.github/workflows/docker-images.yml` runs the tests and then builds and
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

`compose.yaml` references these images directly, so a deployment pulls the
published stack and never builds:

```sh
docker compose up                               # run the published images
docker compose -f compose.dev.yaml up --build   # build and run from source
```

`compose.dev.yaml` is a local-build overlay: it includes `compose.yaml` and adds
the build step of every image, tagging them `:dev` locally.

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
Discord is optional and never on the path of a game. `docs/discord-integration.md`
describes the contract, the settings and the Discord application to create.

### Telemetry

The servers support OpenTelemetry: they export their metrics over OTLP/gRPC to
a collector that listens on port 4317.

The install scripts ask whether the deployment should send telemetry and
default to yes. When it is on, they write `OTEL_ENABLED=true` and `OTEL_PORT`
into `.env`; when it is off, no OpenTelemetry integration is configured. They
never touch the collector; the Alloy receiver running on the host is configured
by hand, and its port has to match `OTEL_PORT`. The run scripts always enable
telemetry on port 4317.

## Acknowledgements

Protocol research and reverse engineering that made this project possible:

- GHzGangster — [Nomad](https://github.com/GHzGangster/Nomad)
- [mgo2pc.com](https://mgo2pc.com/) community server
- boiln — [echo](https://github.com/boiln/echo)
