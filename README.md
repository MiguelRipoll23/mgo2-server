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

The uninstall scripts remove everything the install put in place: every
container, image, volume and network of the deployment, and the configuration
(`.env`, and the whole downloaded deployment directory when a piped script
installed it). They ask before removing anything; `--yes` (Linux/macOS),
`-Yes` (Windows) or `MGO2_ASSUME_YES=1` answers for you. Docker itself is not
removed.

Linux and macOS:

```sh
curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/uninstall-linux-macos.sh | bash -s -- --yes
```

Windows:

```powershell
irm https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/uninstall-windows.ps1 | iex
```

Then go to the `Metal Gear Online` tab in RPCS3 settings and set the DNS server to your private IP (the value of `ADVERTISED_ADDRESS`).

The port-check responder answers on 3478/udp and 3479/udp and names
`ADVERTISED_ADDRESS` in its answers, the address the console dials, so like the name
server it does not have to own that address: the published ports hand the datagrams
over and on Linux they also carry the console's own address and port through
unchanged, which is what it is told it appears as. The second address is the one
thing the port mapping cannot carry, because a reply leaving the container leaves
from the host's primary address; `STUN_SECONDARY_ADDRESS` in `.env.example` needs
`network_mode: host` on that one service, or the responder run outside Docker.

### Project layout

```
src/
├── Shared/            # Domain and protocol, shared by every executable
│   ├── Constants/     # Command identifiers, crypto keys, error codes, ports
│   ├── Domain/        # One service per domain: lobbies, games, clans, …
│   ├── Errors/        # ServerException
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
└── Stun/                # Port-check responder (STUN)
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
