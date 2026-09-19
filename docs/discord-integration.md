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

      Discord ──(gateway events)──▶ DiscordCommandService
        ├─ /flash   ──▶ FlashNewsDispatcherService
        └─ /message ──▶ IDiscordMessageService (channel message over REST)
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

* The bot connects to the Discord gateway over a WebSocket, which is where the
  `/flash` and `/message` commands and the events arrive. The gateway only
  carries what Discord pushes down, so the command registration, the answers to
  the interactions and the channel messages go over the REST API.
* Every call to Discord is wrapped, and a failure is logged and forgotten. An
  unreachable Discord, a revoked token or a rate limit cannot fail a request of
  the API, a flash, or a game lobby.
* The integration runs its initialization in the background, so the API starts
  whether or not Discord answers.
* A gameplay lobby knows nothing about Discord. The integration lives in the
  HTTP API because the global player count is what it publishes, and no lobby
  has that count.

### Settings

| Variable                           | Meaning                                                                |
| ---------------------------------- | ---------------------------------------------------------------------- |
| `DISCORD_ENABLED`                  | Switches the whole integration on.                                      |
| `DISCORD_BOT_TOKEN`                | Bot token of the application. Required for everything.                  |
| `DISCORD_GATEWAY_URL`              | WebSocket URL of the gateway; defaults to the public Discord gateway.   |
| `DISCORD_GUILD_ID`                 | Guild the commands are registered in, the channel lives in, and the guild the staff commands are honored in. |
| `DISCORD_PLAYER_COUNT_CHANNEL_ID`  | Channel of the player count; found by name and created when empty.      |
| `DISCORD_MODERATOR_ROLE_ID`        | Role that may use `/flash` and `/message`.                             |
| `DISCORD_MANAGER_ROLE_ID`          | Other role that may use `/flash` and `/message`.                       |

The integration does nothing until `DISCORD_BOT_TOKEN` is set, and publishes no
count until `DISCORD_GUILD_ID` is set as well. Nothing else is needed: the
application identifier the command registration requires is read out of the bot
token.

### Preparing the application

1. Create an application in the Discord developer portal and a bot in it. Give
   the bot the `Manage Channels` and `Send Messages` permissions and invite it
   to the guild with those permissions.
2. Put the bot token in `DISCORD_BOT_TOKEN` and the guild identifier in
   `DISCORD_GUILD_ID`.
3. Set `DISCORD_MODERATOR_ROLE_ID` and `DISCORD_MANAGER_ROLE_ID` to the roles
   that may broadcast. Without them nobody may use the commands.
4. Start the deployment. The API registers `/flash` and `/message` in the guild,
   connects the bot to the gateway and finds or creates the channel of the
   player count.

Both commands take one required option — `/flash` a `message`, `/message` a
`body` — and are registered per guild, so they are available immediately instead
of waiting for Discord to publish a global command. They arrive over the gateway
socket: a member runs one, the API sees the interaction and answers the member
alone, so the channel is not cluttered with the outcome. A command from a guild
that is not `DISCORD_GUILD_ID` is ignored.

### Sending messages

The bot has two ways to write a channel message:

* The Discord `/message` command writes an official message from the bot into
  the channel it was used in, with the text of its `body` option. It is only run
  for the moderator and manager roles, and the message is never allowed to ping
  a role or everyone in the guild.
* `POST /discord/messages` (bearer token required) with a `channelId`
  and a `content` writes a message from the bot into that channel of the guild.
  The message is never allowed to ping a role or everyone.

### The player count channel

The channel is created once, when the integration is initialized, and is named
`players [n]` with the number of players connected across every game lobby.
Every connection and disconnection is also written in it as a message:

```
players [42]
Snake connected
Snake disconnected
```

The channel is remembered by its name (`players [n]`), so a restart adopts the
channel it created instead of adding a second one; setting
`DISCORD_PLAYER_COUNT_CHANNEL_ID` skips the search entirely.

A player who moves from one lobby to another is reported by both lobbies, so
the move arrives as a departure and an arrival of the same character. The
integration holds an event back for a few seconds: when the opposite event
lands inside that window the two cancel out, so a move is neither written in the
channel nor allowed to flip its name through the transient count. A genuine
connection or disconnection still waits out the window and is then written.

Discord allows only a couple of renames of a channel per ten minutes, and the
count moves far more often than that. The integration therefore treats a rename
as best effort: a refusal is logged, the count is applied again at the next
change, and nothing else is affected.
