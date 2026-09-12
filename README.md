# MGO2 server (.NET)

A server implementation for Metal Gear Online 2, written in C# on .NET 10.
It provides the gate, account and gameplay lobby TCP servers, the peer-to-peer
gameplay host, an HTTP REST API, a name server for domain redirection and a
PostgreSQL-backed persistence layer.

## Quick start

The install scripts pull the published images and bring up the whole
deployment: the gate, the account server, one gameplay lobby per container, a
dedicated host, the HTTP API, the name server and PostgreSQL. A clone is
optional, and the only requirement is Docker with the compose plugin.

Linux and macOS:

```sh
curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-linux-macos.sh | bash
```

Windows:

```powershell
irm https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-windows.ps1 | iex
```

Without a clone the script installs into `./mgo2-server` (or `MGO2_HOME`), where
it keeps `compose.yaml`, `.env` and `.env.example`. On the first run it creates
`.env` from `.env.example`; review it, especially `JWT_SECRET` and
`LISTENING_IP`, then run the same command again.

The gate, the account server, the gameplay lobbies and the dedicated hosts all
wait for the healthcheck of the PostgreSQL container before they start, so the
schema is created exactly once no matter how many containers come up at once.

**Running the script again installs the update.** It refreshes the compose file
of the deployment, pulls the newest images and recreates the containers whose
image or configuration changed, so a new release is one command away:

```sh
curl -fsSL https://raw.githubusercontent.com/MiguelRipoll23/mgo2-server/main/scripts/install-linux-macos.sh | bash
```

Inside a clone the scripts of `scripts/` do the same against the local compose
file. The database lives in a named volume, so updates keep the accounts,
characters and clans. Stop the deployment with `docker compose down`.

## Architecture

| Component            | Port         | Container            | Description                                                     |
| -------------------- | ------------ | -------------------- | --------------------------------------------------------------- |
| Name server          | 53/udp       | `mgo2-dns`           | Resolves configured local domains to `LISTENING_IP`             |
| HTTP API             | 80/tcp       | `mgo2-http`          | REST API for administration and game client integration         |
| Gate server          | 5731/tcp     | `mgo2-gate`          | First connection point; delivers the lobby list and the news    |
| Account server       | 5732/tcp     | `mgo2-account`       | Character creation, deletion, selection and session validation  |
| Gameplay lobbies     | 5733+/tcp    | one container each   | Room management, player sessions and match statistics           |
| Dedicated hosts      | 5730+/udp    | one container each   | Peer-to-peer gameplay host owning matches in a lobby            |

Every listener is its own process, so a lobby or a dedicated host can be
restarted, scaled or replaced without touching the others.

### Gameplay lobbies are registered, not seeded

The database is seeded with the gate and the account server only. A gameplay
lobby exists because a `GameLobbyServer` container was started for it: it
registers its own `lobbies` row from the environment, keyed by port, and then
heartbeats that row every `LOBBIES_REFRESH_INTERVAL_MINUTES` (five minutes by
default). The gate serves a gameplay lobby only while its heartbeat is
recent, and a game lobby server removes the gameplay lobbies that stopped being
heartbeated, so a deleted container disappears from the list on its own.

A dedicated host works the same way for its match: it creates (or reuses) the
match in its lobby, heartbeats its `updated_at` every interval, and expires the
dedicated-host matches whose heartbeat stopped. Player-hosted rooms do not
carry a heartbeat and keep their current lifetime.

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
├── GateLobbyServer/   # Executable: gate (5731/tcp)
├── AccountLobbyServer/ # Executable: account server (5732/tcp)
├── GameLobbyServer/   # Executable: one gameplay lobby per container
├── GameplayServer/    # Executable: dedicated UDP gameplay host
├── Http/              # Executable: REST API and its Scalar reference
└── Dns/               # Executable: name server
```

No source file exceeds 400 lines, and every domain is reached through its own
service rather than by touching another domain's tables directly.

## Setup with Docker

`compose.yaml` starts PostgreSQL, the gate, the account server, one container per
gameplay lobby and one per dedicated host. Copy `.env.example` to `.env`, set
`LISTENING_IP` to the address clients should connect to and provide a
`JWT_SECRET`, then:

```sh
docker compose up --build
```

Adding another gameplay lobby means adding a service block that reuses the
`x-game-lobby-server` anchor with its own `LOBBY_NAME`, `LOBBY_SUBTYPE`,
`LOBBY_PORT` and matching TCP port mapping. Adding another dedicated host means
a block that reuses the `x-gameplay-server` anchor with its own
`DEDICATED_HOST_PORT`, UDP port mapping and lobby name.

### Container images

`.github/workflows/docker-images.yml` runs the tests and then builds and
publishes one image per standalone project to the GitHub container registry,
using `docker/Dockerfile` with the project and its assembly as build arguments:

| Image                        | Project                              |
| ---------------------------- | ------------------------------------ |
| `mgo2-gate-lobby-server`     | `src/GateLobbyServer`                |
| `mgo2-account-lobby-server`  | `src/AccountLobbyServer`             |
| `mgo2-game-lobby-server`     | `src/GameLobbyServer`                |
| `mgo2-gameplay-server`       | `src/GameplayServer`                 |
| `mgo2-http`                  | `src/Http`                           |
| `mgo2-dns`                   | `src/Dns`                            |

A push to `main` publishes `latest` and the branch tag; a `v*` tag publishes the
version. Pull requests only run the tests.

### Starting from the published images

`scripts/install-linux-macos.sh` (Linux and macOS) and
`scripts/install-windows.ps1` (Windows) pull every image and install every
container in one go: they create `.env` from `.env.example` on the first run,
pull the images, install all fifteen containers (the gate, the account server,
the nine gameplay lobbies, a dedicated host, the HTTP API, the name server and
PostgreSQL) and report which of them are running. They can be run from a clone
or straight from the repository over `curl`; either way, running them again
installs the update.

```sh
scripts/install-linux-macos.sh                # ghcr.io/miguelripoll23/mgo2-server
scripts/install-linux-macos.sh ghcr.io/your-account/your-repository
```

```powershell
.\scripts\install-windows.ps1 -ImagePrefix ghcr.io/your-account/your-repository
```

The registry, `ghcr.io/miguelripoll23/mgo2-server`, is the default, so the
argument is only needed for a fork or a private registry; `MGO2_IMAGE_PREFIX` in
`.env` sets it permanently and `MGO2_IMAGE_TAG` selects the version, `latest` by
default. To run locally built images instead, use `docker compose build` before
the script and point `MGO2_IMAGE_PREFIX` at the tags you built.

Two host details matter on Linux: the name server claims UDP port 53, which
conflicts with `systemd-resolved` when it is enabled, and the HTTP API claims
port 80.

## Configuration

All options are set through environment variables. Every setting is a single
upper-case name: there are no sections and no `Section__Key` pairs.

There is one environment file for the whole deployment, not one per container.
`.env` carries what every container shares (the database, the listening address,
`JWT_SECRET`, the log level), while the identity of a gameplay lobby belongs to
that lobby alone and is set on its own service block in `compose.yaml`. Keeping
those values out of `.env` is what stops two lobbies from claiming the same name
or port by accident.

| Variable                                | Default                          | Description                                                     |
| --------------------------------------- | -------------------------------- | --------------------------------------------------------------- |
| `LISTENING_IP`                          | `0.0.0.0`                        | Address the servers bind to and that the name server resolves   |
| `LOG_LEVEL`                             | `Debug`                          | Minimum level the servers log at                                |
| `MGO2_IMAGE_PREFIX`                     | `ghcr.io/miguelripoll23/mgo2-server/` | Registry prefix the install scripts pull the images from   |
| `MGO2_IMAGE_TAG`                        | `latest`                         | Version of the images the install scripts pull                  |
| `DATABASE_CONNECTION_STRING`            | `Host=localhost;Database=mgo2;…` | PostgreSQL connection string                                    |
| `LOBBIES_REFRESH_INTERVAL_MINUTES`      | `5`                              | Heartbeat interval; a row is stale after 2×, the lobby list refreshes every tenth of it |
| `LOBBY_NAME`                            | —                                | Name of the gameplay lobby this container hosts **(required)**  |
| `LOBBY_SUBTYPE`                         | —                                | Game type as text, e.g. `FREE BATTLE`, `TRAINING` **(required)** |
| `LOBBY_PORT`                            | `0`                              | TCP port the gameplay lobby listens on **(required)**           |
| `LOBBY_BEGINNER_ONLY`                   | `false`                          | Lobby accepts beginners only                                    |
| `LOBBY_EXPANSION_ONLY`                  | `false`                          | Lobby accepts expansion owners only                             |
| `LOBBY_NO_HEADSHOT`                     | `false`                          | Lobby disables headshots                                        |
| `LOBBY_REPLAYS_ONLY`                    | `false`                          | Lobby accepts replays only                                      |
| `DEDICATED_HOST_PORT`                   | `5730`                           | UDP port a dedicated host listens on (`UDP_PORT` also accepted) |
| `DEDICATED_HOST_LOBBY_NAME`             | `Free Battle`                    | Lobby a dedicated host publishes its match in                   |
| `P2P_HOST`                              | `127.0.0.1`                      | Address a dedicated host announces to its peers                 |
| `HTTP_PORT` / `LAUNCHER_SERVER` / `JWT_SECRET` | `80` / — / —               | HTTP API port, upstream launcher and token secret **(required)** |
| `DNS_PORT` / `ALTERNATIVE_DNS_SERVER` / `ALTERNATIVE_DNS_PORT` | `53` / `8.8.8.8` / `53` | Name server settings              |
| `LOCAL_RESOLVED_DOMAINS`                | `mgo2pc.com,game.mgo2pc.com`     | Domains resolved to `LISTENING_IP`                              |

## Development

```sh
dotnet build                             # Build every project
dotnet test                              # Run the unit tests
dotnet run --project src/GateLobbyServer     # Run the gate
dotnet run --project src/AccountLobbyServer  # Run the account server
dotnet run --project src/GameLobbyServer     # Run one gameplay lobby
dotnet run --project src/GameplayServer      # Run a dedicated host
```

The scripts of `scripts/` split building from running: `build-linux-macos.sh`
and `build-windows.ps1` build every project, and `run-linux-macos.sh` and
`run-windows.ps1` start the whole deployment from the built binaries against
the remote database. The run scripts build nothing on their own, so run the
build script again after changing the code.

```sh
scripts/build-linux-macos.sh             # Build every project
scripts/run-linux-macos.sh               # Run the whole deployment
```

## Tests

`tests/Mgo2Server.Tests` holds the unit tests:

- `PersistenceModelTests` builds the Entity Framework model and asserts that
  every entity has a primary key and is mapped onto the table of the original
  schema.
- `DnsMessageCodecTests` parses queries and checks the address response of the
  name server byte by byte.

The encrypted protocol is covered by the implementation itself: the key material
and the wire formats are fixed constants of `Shared/Constants`, and the handlers
are exercised against a running server.

## Acknowledgements

Protocol research and reverse engineering that made this project possible:

- GHzGangster — [Nomad](https://github.com/GHzGangster/Nomad)
- [MGO2PC's team](https://mgo2pc.com/)
- boiln — [echo](https://github.com/boiln/echo)
