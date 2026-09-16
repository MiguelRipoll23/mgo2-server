# Internal coordination, flash news and Discord

The HTTP API is the coordinator of the deployment. Every gameplay lobby opens
one persistent gRPC connection to it, the lobby reports its players up that
connection, and the API pushes flash news back down the same connection. Discord
is an optional observer of the API: it relays the flash command and publishes
the global player count. None of it touches the MGO2 protocol, and none of it is
a dependency of a lobby or of a game.

## The shape of it

```
GameLobbyServer (one per lobby)                  Http (one)
  LobbyCoordinationClientService                   LobbyCoordinationGrpcService
   ├─ opens the stream, retries every minute       ├─ one stream per connected lobby
   ├─ reports connect / disconnect ───────────────▶├─ PlayerPresenceNotificationService
   └─ writes the ticker to its own clients ◀───────┤  ├─ LobbyPresenceService (global count)
                                                   │  └─ IPlayerPresenceObserver
                                                   │     └─ DiscordPlayerCountService
                                                   └─ FlashNewsDispatcherService
                                                      ▲
                        POST /flash-news/broadcast ────┤
                        Discord /flash command ────────┘
```

* **Http owns the coordination.** It keeps the streams, the per-lobby counts and
  the global total. It does not hold the state of a lobby: that stays in the
  lobby process, which is the only thing that knows who is really connected.
* **The stream is initiated by the lobby**, which is the side that knows the
  endpoint (`INTERNAL_GRPC_URL`). One bidirectional stream serves both
  directions, so there is no second connection to keep and no polling.
* **gRPC is internal only.** It is served on `INTERNAL_GRPC_PORT` (5743 by
  default) over HTTP/2 without TLS, inside the network of the deployment. It is
  never published and no game client can reach it.
* **The lobby is never blocked by the API.** If the endpoint cannot be reached,
  the lobby keeps serving players and retries once a minute. While it is not
  coordinated, only the global count and the reception of flash news are
  affected, and a lobby that reconnects registers the players it holds, so the
  coordinator starts from the truth instead of from the deltas it missed.
* **The contract** is `src/Shared/InternalGrpc/Protos/lobby_coordination.proto`,
  compiled into the shared assembly so both sides speak exactly the same
  messages.

### What a lobby reports

Each connect and disconnect is an event that carries the lobby and the character
(`PlayerPresenceChange`). The character name is not part of it: the API reads it
from the database, and counts the character even when that read fails.

The first message of every stream is a registration that names the lobby and
carries the players connected at that moment. The coordinator replaces what it
knew about the lobby with it, which is what makes a reconnect (or an API
restart) converge on the truth. A stream that ends releases the players of its
lobby, because a lobby that is not connected cannot have anybody in it.

### Flash news

Both paths end in the same call — `FlashNewsDispatcherService.Dispatch` — so a
flash reaches the lobbies exactly the same way whether it came from the API or
from Discord:

* `POST /flash-news/broadcast` and `POST /flash-news/emergency` (bearer token
  required) relay a ticker announcement to every connected lobby.
* The Discord `/flash` command relays a server-message announcement.

The payload of a ticker packet is the client protocol, so it is built where the
protocol lives: the lobby rebuilds the announcement and writes it to its own
sessions with the `FlashNewsService` it always had. An announcement queued for a
lobby that never reads it is dropped rather than kept forever; the queue of a
lobby holds 64 announcements.

## Discord

Discord is switched on with `DISCORD_ENABLED=true` and never becomes a
requirement of anything:

* Every call to Discord is wrapped, and a failure is logged and forgotten. An
  unreachable Discord, a revoked token or a rate limit cannot fail a request of
  the API, a flash, or a game lobby.
* The integration runs its initialization in the background, so the API starts
  whether or not Discord answers.
* A gameplay lobby knows nothing about Discord. The integration lives in the
  HTTP API because the global player count is what it publishes, and no lobby
  has that count.

### Settings

| Variable                           | Meaning                                                          |
| ---------------------------------- | ---------------------------------------------------------------- |
| `DISCORD_ENABLED`                  | Switches the whole integration on.                                |
| `DISCORD_BOT_TOKEN`                | Bot token of the application. Required for everything.            |
| `DISCORD_APPLICATION_ID`           | Application identifier, used to register the command.             |
| `DISCORD_PUBLIC_KEY`               | Hex Ed25519 public key that verifies every interaction.           |
| `DISCORD_GUILD_ID`                 | Guild the commands are registered in and the channel lives in.    |
| `DISCORD_PLAYER_COUNT_CHANNEL_ID`  | Channel of the player count; found by name and created when empty. |
| `DISCORD_MODERATOR_ROLE_ID`        | Role that may use `/flash`.                                       |
| `DISCORD_MANAGER_ROLE_ID`          | Other role that may use `/flash`.                                 |

The API answers nothing until `DISCORD_BOT_TOKEN`, `DISCORD_APPLICATION_ID` and
`DISCORD_PUBLIC_KEY` are set, and publishes no count until `DISCORD_BOT_TOKEN`
and `DISCORD_GUILD_ID` are set.

### Preparing the application

1. Create an application in the Discord developer portal and a bot in it. Give
   the bot the `Manage Channels` and `Send Messages` permissions and invite it
   to the guild with those permissions.
2. Put the bot token in `DISCORD_BOT_TOKEN` and the application identifier in
   `DISCORD_APPLICATION_ID`. Create a guild and set `DISCORD_GUILD_ID`.
3. Copy the public key of the application into `DISCORD_PUBLIC_KEY`. It is the
   Ed25519 key Discord signs every interaction with, and the endpoint verifies
   that signature before it reads anything.
4. Point the interactions endpoint URL of the application at
   `https://<host>/discord/interactions`. Discord sends a signed ping when the
   URL is saved; the API answers it, which is what proves the URL.
5. Set `DISCORD_MODERATOR_ROLE_ID` and `DISCORD_MANAGER_ROLE_ID` to the roles
   that may broadcast. Without them nobody may use the command.
6. Start the deployment. The API registers `/flash` in the guild and finds or
   creates the channel of the player count.

The `/flash` command takes one required option, `message`, and answers
ephemerally in the channel it was used in. It is registered per guild, so it is
available immediately instead of waiting for Discord to publish a global
command.

### The player count channel

The channel is created once, when the integration is initialized, and is named
`players [n]` with the number of players connected across every game lobby.
Every connection and disconnection is also written in it as a message:

```
players [42]
Snake connected.
Snake disconnected.
```

The channel is remembered by its name (`players [n]`), so a restart adopts the
channel it created instead of adding a second one; setting
`DISCORD_PLAYER_COUNT_CHANNEL_ID` skips the search entirely.

Discord allows only a couple of renames of a channel per ten minutes, and the
count moves far more often than that. The integration therefore treats a rename
as best effort: a refusal is logged, the count is applied again at the next
change, and nothing else is affected.
