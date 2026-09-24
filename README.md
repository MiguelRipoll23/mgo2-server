# MGO2 server

A server implementation for Metal Gear Online 2, written in C# on .NET 10.
It provides the gate, account and gameplay lobby TCP servers, the UDP peer-to-peer
gameplay host, an HTTP API, a name server for domain redirection, a port-check
responder for NAT discovery and a PostgreSQL-backed persistence layer.

> [!NOTE]
> [Join our Discord and play on this server!](https://discord.gg/pg5fZRUnB)

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

Then go to the `Metal Gear Online` tab in RPCS3 settings and set the DNS server to your private IP.

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

```sh
docker compose up                               # run the published images
docker compose -f compose.dev.yaml up --build   # build and run from source
```

### Deployment

The k3s cluster is reconciled from `deploy/` by ArgoCD. One pipeline per service
builds an image tagged with the commit SHA and commits that tag to the service's
manifest folder; nothing in CI applies anything to the cluster. See
[`deploy/README.md`](deploy/README.md) for the layout, the bootstrap and the
migration rules, and [`docs/CICD.md`](docs/CICD.md) for the pipelines.

## Development

```sh
dotnet build                             # Build every project
dotnet test                              # Run the unit tests
dotnet run --project src/GateLobbyServer     # Run the gate
dotnet run --project src/AccountLobbyServer  # Run the account server
dotnet run --project src/GameLobbyServer     # Run one game lobby
dotnet run --project src/GameplayServer      # Run a gameplay server
dotnet run --project src/Stun                # Run the port-check responder
```

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
pushes flash news back down the same connection.

### Telemetry

The servers support OpenTelemetry: they export their metrics over OTLP/gRPC to
a collector that listens on port 4317.

## Acknowledgements

Protocol research and reverse engineering that made this project possible:

- [GHzGangster/Nomad](https://github.com/GHzGangster/Nomad)
- [mgo2pc.com](https://mgo2pc.com/)
- [boiln/echo](https://github.com/boiln/echo)
- [comradesean/mgo2server](https://github.com/comradesean/mgo2server)
