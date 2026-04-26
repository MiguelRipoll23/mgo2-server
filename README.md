# MGO2 server

A server implementation for Metal Gear Online 2, written in TypeScript/Deno.
Provides the Gate, Account, and Game Lobby TCP servers, an HTTP REST API, a DNS
server for domain redirection, and a PostgreSQL-backed persistence layer.

## Architecture

| Component         | Port      | Description                                                     |
| ----------------- | --------- | --------------------------------------------------------------- |
| DNS server        | 53/udp    | Resolves configured local domains to `LISTENING_IP`             |
| HTTP API          | 80/tcp    | REST API for administration and game client integration         |
| STUN server       | 3478/udp  | NAT traversal for peer-to-peer connections                      |
| Gate server       | 5731/tcp  | First connection point; delivers lobby list and news to clients |
| Account server    | 5732/tcp  | Character creation, deletion, selection, and session validation |
| Game lobby server | 5733+/tcp | Game room management, player sessions, and match statistics     |

## Setup with Docker

Replace `<YOUR_IP>` with the public or LAN IP address that game clients should
connect to:

```sh
docker run -d \
  --name mgo2-server \
  --network host \
  -e LISTENING_IP=<YOUR_IP> \
  -e JWT_SECRET=changeme \
  -v mgo2-data:/app/data \
  -v postgres-data:/var/lib/postgresql \
  ghcr.io/miguelripoll23/mgo2-server:main
```

PostgreSQL is bundled inside the image and starts automatically. On first
startup the database is initialized, migrated, and seeded with the default lobby
configuration. Subsequent restarts skip seeding.

## RPCS3 configuration

Using the custom RPCS3 build for Metal Gear Online 2, to to the `Config`
section, go to the tab `Metal Gear Onlien 2` and change the DNS IP address to
your listening IP.

## Server configuration

All options are set via environment variables.

| Variable                      | Default                                | Description                                                   |
| ----------------------------- | -------------------------------------- | ------------------------------------------------------------- |
| `LISTENING_IP`                | `0.0.0.0`                              | IP the server binds to and that DNS resolves local domains to |
| `JWT_SECRET`                  | —                                      | Secret used to sign authentication tokens **(required)**      |
| `DATABASE_URL`                | `postgresql://postgres@localhost/mgo2` | PostgreSQL connection string                                  |
| `INSTANCE_ID`                 | _(auto-generated UUID)_                | Unique identifier for this server instance                    |
| `DISABLE_DNS_SERVER`          | `false`                                | Set to `true` to disable the DNS server                       |
| `LOCAL_RESOLVED_DOMAINS`      | `mgo2pc.com,game.mgo2pc.com`           | Comma-separated domains resolved to `LISTENING_IP`            |
| `ALTERNATIVE_DNS_SERVER`      | `8.8.8.8`                              | Upstream DNS for non-local queries                            |
| `ALTERNATIVE_DNS_PORT`        | `53`                                   | Port of the upstream DNS server                               |
| `LOBBIES_REFRESH_CRON`        | `*/15 * * * *`                         | Cron schedule for refreshing the in-memory lobby cache        |
| `HDX_API_KEY`                 | —                                      | HyperDX API key; omit to disable OpenTelemetry telemetry      |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | —                                      | OpenTelemetry Collector endpoint                              |

## Development

**Prerequisites:** [Deno](https://deno.com/) and a running PostgreSQL instance.

```sh
# Copy and edit environment variables
cp .env.example .env

# Apply database migrations and seed initial data
deno task migrate
deno task setup

# Start with hot reload
deno task dev
```

Other useful tasks:

| Task                 | Description                                 |
| -------------------- | ------------------------------------------- |
| `deno task check`    | Type-check the codebase                     |
| `deno task generate` | Generate migrations from schema changes     |
| `deno task studio`   | Open Drizzle Studio for database inspection |

## Acknowledgements

Protocol research and reverse engineering that made this project possible:

- GHzGangster — [Nomad](https://github.com/GHzGangster/Nomad)
- [MGO2PC's team](https://mgo2pc.com/)
- boiln — [echo](https://github.com/boiln/echo)
