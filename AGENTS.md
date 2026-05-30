# MGO2 Server — Project Guide

## Technology Stack

- **Runtime:** Deno (TypeScript)
- **HTTP:** Hono (`npm:@hono/zod-openapi`)
- **ORM:** Drizzle ORM + PostgreSQL
- **DI:** `@needle-di/core`
- **Validation:** Zod
- **STUN:** `npm:stun`

## Project Hierarchy

```
mgo2-server/
├── src/
│   ├── main.ts                  # Entry point — wires all servers
│   ├── container.ts             # Global DI container
│   ├── core/                    # Domain-agnostic abstractions
│   │   ├── constants/           # Command IDs, crypto keys, error codes
│   │   ├── services/            # Crypto service, logger
│   │   └── tcp/
│   │       ├── types/           # Packet, session, server type definitions
│   │       ├── services/        # BaseTcpServer, PacketCodec, CommandRegistry
│   │       ├── utils/           # Crypto (XOR/MD5/Blowfish), PacketBuilder, session helpers
│   │       ├── interfaces/      # ICommandHandler
│   │       └── decorators/      # @GateCommandHandler, @AccountCommandHandler, @GameCommandHandler
│   ├── infrastructure/
│   │   ├── tcp/
│   │   │   ├── servers/         # GateServer, AccountServer, GameLobbyServer + command handlers
│   │   │   └── services/        # LobbyTrackerService, ActiveGameSessionsService
│   │   ├── http/                # Hono REST API (routers, services)
│   │   └── dns/                 # UDP DNS server
│   ├── modules/                 # Business logic (auth, character, clan, etc.)
│   ├── db/                      # Drizzle schema definitions
│   └── tasks/                   # CLI entry points (seed, dns)
├── static/                      # HTTP static assets
├── drizzle/                     # Migration files
├── docker-entrypoint.sh         # Docker startup script
├── compose.yaml                 # Docker Compose
└── Dockerfile                   # Container build
```

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Files/directories | `kebab-case` | `crypto-service.ts`, `base-tcp-server-service.ts` |
| Classes | `PascalCase` | `CryptoService`, `PacketWriter`, `BaseTcpServer` |
| Interfaces | `PascalCase` (I prefix rare) | `ICommandHandler`, `TcpSession`, `Packet` |
| Constants/enums | `UPPER_SNAKE_CASE` | `XOR_KEY`, `HEADER_SIZE`, `BLOWFISH_KEY_PACKET` |
| Functions/variables | `camelCase` | `xorBuffer()`, `sequenceOut` |
| Decorators | `PascalCase` | `@GateCommandHandler(0x2005)` |
| DB columns | `snake_case` | `display_name`, `player_count` |

## Packet Structure (TCP Wire Format)

### Header (24 bytes) + Variable Payload

```
Offset  Size  Field         Endianness  Description
------  ----  -----         ----------  -----------
 0       2    command       big-endian  Command ID (u16)
 2       2    payloadLength big-endian  Payload length (u16, max 0x3ff)
 4       4    sequence      big-endian  Sequence number (u32)
 8      16    checksum      —           HMAC-MD5 (16 bytes) over [0..8) + payload
24     var    payload       —           Variable-length payload (max 1023 bytes)
```

The entire buffer (header + payload) is XOR-encrypted on the wire.

### Pipeline

**Decode:** XOR decrypt → parse header → validate payloadLength → extract payload → verify HMAC-MD5 → optionally Blowfish decrypt payload → return `Packet`

**Encode:** optionally Blowfish encrypt payload → write header fields → compute HMAC-MD5 checksum → write checksum and payload → XOR encrypt entire buffer → wire-ready bytes

## Encryption

### 1. XOR (Full Packet Obfuscation)

- **Key (4 bytes):** `0x5a, 0x70, 0x85, 0xaf` → `0x5a7085af`
- Applied to **every byte** of the full packet (header + payload), cycling through the 4 bytes
- Used for "peeking" at the command and payload length fields before full decode

**Key location:** `src/core/constants/crypto-keys-constants.ts` — `XOR_KEY`, `XOR_KEY_BYTES`
**Implementation:** `src/core/tcp/utils/crypto-util.ts` — `xorBuffer()`, `xorBufferWithKey()`

### 2. HMAC-MD5 (Packet Integrity)

- **Key (16 bytes):** Hardcoded in `crypto-keys-constants.ts`
- Computed over bytes `[0..8)` of the header (command + payloadLength) concatenated with the payload
- The 16-byte digest is stored in header bytes `[8..24)`
- Packets with invalid checksums are silently dropped

**Key location:** `src/core/constants/crypto-keys-constants.ts` — `HMAC_MD5_KEY`
**Implementation:** `src/core/tcp/utils/crypto-util.ts` — `computeHmacMd5()`

### 3. Blowfish (Payload Encryption — Custom 8-Round Variant)

- Ported from `Crypto.java` reference — big-endian, 8 rounds, custom key schedule
- Block size: 8 bytes (payloads are zero-padded to block alignment)
- Two key tables (4168 bytes each):
  - **`BLOWFISH_KEY_PACKET`** — For packet payload encryption/decryption
  - **`BLOWFISH_KEY_AUTH`** — For auth token encryption

**When Blowfish is applied:**

| Direction | Command IDs |
|-----------|-------------|
| Inbound (client→server) | `0x3003`, `0x4310`, `0x4320`, `0x43c0`, `0x4700`, `0x4990` |
| Outbound (server→client) | `0x4305` |

**Key location:** `src/core/constants/crypto-keys-constants.ts` — `BLOWFISH_KEY_PACKET`, `BLOWFISH_KEY_AUTH`
**Implementation:** `src/core/tcp/utils/crypto-util.ts` — `blowfishEncrypt()`, `blowfishDecrypt()`
**Command list:** `src/core/tcp/types/tcp-constants-type.ts` — `BLOWFISH_ENCRYPTED_INBOUND`, `BLOWFISH_ENCRYPTED_OUTBOUND`

## Command ID Space

| Range | Server | Purpose |
|-------|--------|---------|
| `0x0003` | All | DISCONNECT |
| `0x0005` | All | KEEP_ALIVE |
| `0x2002-0x2008` | Gate | Lobby list, news |
| `0x3003-0x3105` | Account | Session validation, character CRUD |
| `0x4100-0x4220` | Game | Character info, stats, settings |
| `0x4300-0x44xx` | Game | Game room management, chat |
| `0x4500-0x4680` | Game | Friends, search, match history |
| `0x4800-0x4860` | Game | Messages |
| `0x4900-0x4990` | Game | Hub info |
| `0x4b00-0x4b90` | Game | Clan management |

**Defined in:** `src/core/constants/commands-constants.ts`
**Error codes:** `src/core/constants/error-codes-constants.ts` (OR'd with `0xC0FFEE00`)

## TCP Transport

### Architecture

```
BaseTcpServer (abstract)
 ├── GateServer       (port from DB, default 5731)
 ├── AccountServer    (port from DB, default 5732)
 └── GameLobbyServer  (port from DB, default 5733+)
```

### Connection Lifecycle

1. `Deno.listen({ port })` accepts TCP connections
2. Each connection creates a `TcpSession` (tracking sequence, userId, characterId, lobbyId, gameId)
3. `runReadLoop` accumulates bytes from the socket
4. `processAccumulatedBytes` peeks at XOR'd payload length to determine packet boundaries
5. Complete packets are decoded via `decodePacket()` and dispatched via `CommandRegistry`
6. `COMMAND_DISCONNECT` (`0x0003`) closes the session
7. `COMMAND_KEEPALIVE` (`0x0005`) sends an ACK back

### Sequence Numbers

- Client (inbound): starts at `0`
- Server (outbound): starts at `1`
- Both are incremented with each packet sent/received

### Session State (`TcpSession`)

```typescript
interface TcpSession {
  serverType: "gate" | "account" | "game";
  connection: Deno.TcpConn;
  remoteAddress: string;
  sequenceIn: number;
  sequenceOut: number;
  userId: number | null;
  characterId: number | null;
  lobbyId: number | null;
  gameId: number | null;
}
```

### Command Handler Registration

Handlers are registered via decorators that insert into a global `CommandRegistry` map:
- `@GateCommandHandler(commandId)` → `"gate:0x2005" -> Handler`
- `@AccountCommandHandler(commandId)` → `"account:0x3003" -> Handler`
- `@GameCommandHandler(commandId)` → `"game:0x4320" -> Handler`

All handlers implement `ICommandHandler` with a `handle(session, packet)` method.

## Other Services

| Service | Port | Protocol | Description |
|---------|------|----------|-------------|
| DNS | 53 | UDP | Resolves configured domains to LISTENING_IP |
| HTTP API | 80 | TCP | REST API (Hono), JWT auth, public + authenticated routes |
| STUN | 3478 | UDP | NAT traversal via `npm:stun` |

## Databases (PostgreSQL via Drizzle ORM)

**Tables:** `users`, `characters`, `character_details`, `character_stats`, `sessions`, `lobbies`, `lobby_game_types`, `lobby_instance_counts`, `games`, `clans`, `news`

**Migrations:** `drizzle/` directory, applied via `deno task migrate`

## Key File Index

| Purpose | File |
|---------|------|
| Crypto keys | `src/core/constants/crypto-keys-constants.ts` |
| XOR/MD5/Blowfish impl | `src/core/tcp/utils/crypto-util.ts` |
| Packet codec | `src/core/tcp/services/packet-codec-service.ts` |
| Packet types | `src/core/tcp/types/packet-type.ts` |
| TCP base server | `src/core/tcp/services/base-tcp-server-service.ts` |
| Session type | `src/core/tcp/types/session-type.ts` |
| Command registry | `src/core/tcp/services/command-registry-service.ts` |
| Command constants | `src/core/constants/commands-constants.ts` |
| Error codes | `src/core/constants/error-codes-constants.ts` |
| Packet builder | `src/core/tcp/utils/packet-builder-util.ts` |
| Session helpers | `src/core/tcp/utils/session-helpers-util.ts` |
| Blowfish commands | `src/core/tcp/types/tcp-constants-type.ts` |
| Decorators | `src/core/tcp/decorators/*.ts` |
| Gate handlers | `src/infrastructure/tcp/servers/gate/commands/*.ts` |
| Account handlers | `src/infrastructure/tcp/servers/account/commands/*.ts` |
| Game handlers | `src/infrastructure/tcp/servers/game/commands/*.ts` |
| HTTP API | `src/infrastructure/http/` |
| DNS server | `src/infrastructure/dns/dns-server.ts` |
| DB schema | `src/db/schema.ts` |
| DI container | `src/container.ts` |
| Entry point | `src/main.ts` |

## Development Commands

```sh
deno task dev       # Start with hot reload
deno task check     # Type-check
deno task migrate   # Run DB migrations
deno task setup     # Seed initial data
deno task generate  # Generate migrations from schema changes
deno task studio    # Drizzle Studio
```
