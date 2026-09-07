# Command handler comparison — this server vs comradesean/mgo2server

Compares this repo's TCP command handlers (Deno) against the Java reference
server (`comradesean/mgo2server`, cloned snapshot of `main`). The reference
was analyzed from its `IGameController` registrations, `LoadoutWriter`,
`GameError`, `Automatch` and its `dev/docs` (GEAR.md, BACKLOG.md, OBSERVED.md,
POST_LAUNCH.md, PROTOCOL.md, AUTOMATCH.md).

Legend: ✅ same behaviour · 🟡 partial / simplified · 🔴 different semantics ·
➕ this server has it, reference does not · ➖ reference has it, this server
stubs or omits it.

## Architecture

| | this server | reference |
|---|---|---|
| Dispatch | `@Gate/Account/GameCommandHandler(id)` decorators into a global registry | controllers register a `Map<Integer, Consumer<Context>>`; duplicate claims throw |
| Disconnect / keepalive (0x0003 / 0x0005) | handled in `BaseTcpServer.dispatchPacket` | `CommonGameController` |
| Unknown command | WARN `no-handler`, connection stays up | WARN + payload dump, ignored |
| Reply helpers | `sendResult` (explicit `{u32 result}`) | `resultPacket` / `writeResult` — same rationale |

## Gate

| Command | Delta | This server | Reference |
|---|---|---|---|
| 0x2005 lobby list | ✅ | `0x2002 start → 0x2003 entries (22/packet) → 0x2004 end`, capped at 32 entries (client bound) | same triple |
| 0x2008 news | ✅ | `0x2009/0x200a/0x200b`, items from the `news` table | same, from its news table |

## Account

| Command | Delta | This server | Reference |
|---|---|---|---|
| 0x3003 check session | ✅ | validates session token against claimed user id; `INVALID_SESSION` on failure | same shape (`CHECK_SESSION_RESULT`) |
| 0x3048 character list | ✅ | paged list from DB | same |
| 0x3101 create character | 🟡 | persists character + appearance | also grants `chara_gear` from the creation picks (moot here: gear is served all-unlocked) |
| 0x3103 / 0x3105 select / delete | ✅ | DB-backed, delete honours cooldown error code | same |
| 0x3107 name check | ✅ | enforces the same taken-name rule as create | same |

## Game — connect & character

| Command | Delta | This server | Reference |
|---|---|---|---|
| 0x0001 echo | ✅ | payload echo | same |
| 0x3003 check session (game) | 🟡 | ownership check (character must belong to the account); failure code `LOBBY_LOGIN_AGAIN` (-240) | `INVALID_SESSION` family; join/connect FSM equivalent |
| 0x4100 → 0x4101 character info | 🔴 | replies on request with a 0x142 grid: 29-byte header, **32 friend + 32 blocked ids**, 25-byte tail (the reference servers send 0x243 with 256-id regions, which lands friend ids 33-64 in the blocked list) | connect burst pushes `0x4101` sized for its own build; same fixed-grid idea |
| 0x411a / 0x411b | ➕ | inbound GETs of chat macros (→0x4121) and gameplay-options/UI settings (→0x4120) — present because this build's client requests them | not registered; 1.36 expects the server to **push** 0x4120/0x4121 in the connect burst |
| 0x4110 gameplay options | 🔴 | parsed (full codec) and persisted; reply 0x4111 | **acked but unparsed** (explicitly BACKLOG'd) |
| 0x4112 UI settings write | ✅ | ack `{result=0}` | same (`UNKNOWN_WRITE_BACK`) |
| 0x4114 chat macros | ✅ | parsed and persisted (0x4121 layout) | parsed and persisted |
| 0x4122 personal info | 🟡 | equipped skills (levels forced max), clan header, appearance, comment; experience words = 24576 | `PersonalInfoWriter` additionally carries saved instructor and worn title |
| 0x4130 / 0x4131 update personal info | ✅ | persists appearance/comment; echoes skills + 24576 exp words (was the out-of-spec 0x600000) | persists; POST_LAUNCH fixed the same 0x600000 bug |
| 0x4132 → 0x4133 commit outfit | ✅ | re-sends the gear catalogue | same, "both packets write the same client table" |
| 0x4124 gear | 🔴 | **static all-unlocked catalogue**: the 67 wardrobe-renderable ids, each with its real legal colour mask `(1<<n)-1`; 0xff highlight tail | per-character `chara_gear` (creation picks) + DB-driven highlight pairs |
| 0x4125 skills | 🔴 | **static all-max catalogue**: all 17 defined skills at the legal maximum (24576 / level 3; skill 17 level 1), nothing persisted | per-character `chara_skill`, fed by 0x43a4 reports |
| 0x4140 / 0x4141 / 0x4142 skill sets | ✅ | sets persisted; served levels forced to max | sets persisted, stored levels served |
| 0x4143 / 0x4142 gear sets | ✅ | persisted | persisted (`GEAR_SETS`) |
| 0x4124-adjacent 0x4125 sizes | ✅ | 4 + 5·n and 4-byte-count gear | identical math in `LoadoutWriter` |

## Game — hosting & round lifecycle

| Command | Delta | This server | Reference |
|---|---|---|---|
| 0x4304 → 0x4305 host settings | ➕ | re-maps the stored 0x4310 blob into the 0x15C reply (settings restore works); 128 zero bytes when nothing saved | always 128 zero bytes — "no saved settings, use defaults" |
| 0x4310 / 0x4311 settings push | ✅ | stores the raw Blowfish-decrypted blob per character | parses it (rotation at 163, etc.) and stores |
| 0x4316 / 0x4317 create game | ✅ | reads name/comment/password/max/rotation from the stored blob, inserts row, host joins roster, reply `{result, gameId}` | same flow + a `gameCreatedListener` hook for automatch |
| 0x4300 game list | 🟡 | full 55-byte entries; rule/map always 0 (rotation only stored per round 0) | same structure; rotation carried where known |
| 0x4312 → 0x4313 game details | ✅ | 372-byte fixed part + up to 18 player entries, host-rating gate, rotation, pings, lifetime rating sums | `GameDetails` — same fields |
| 0x4320 join | ➕ | capacity check (`GAME_FULL`), password check, host-endpoint gate, **can-rate-host gate** in the reply | password check only — capacity code exists but "joinGame has no capacity check at all" |
| 0x4322 join failed | ✅ | ack | same |
| 0x4340 / 0x4344 / 0x4346 peer registration | ✅ | all three reply `{u32 result, u32 echoed key}` (the parsers read exactly that) | `playerConnection` — same three round-trips |
| 0x4342 player disconnected | ✅ | ack | same |
| 0x4380 quit | ✅ | host quit deletes the game (roster cascades), member quit removes the row | same behaviour verified live |
| 0x4392 set game (rotation) | 🟡 | **acks only** — the one-byte rotation index is not applied to the game row | applies the index ("confirmed twice" live) |
| 0x4398 pings | ✅ | `{u32 hostPing, {charaId, ping}*}` to game + roster rows; explicit 4-byte reply | same |
| 0x4350 self stats | ➕/🔴 | parses a byte-offset layout and persists stats + experience for the session character, replies 0x4351 | **not registered** — the 1.36 build's end-of-round conversation is re-registration + 0x4390 only |
| 0x4390 host stats | 🔴 | same byte-offset parse, applied to the session (host) character only | 167-byte reports: target chara id at 0x00, seconds-in-game at 0x23, **absolute** experience at 0x27, attribution validated against roster/round snapshot — the only reliable stats path |
| 0x43a2 round end | 🟡 | bare ack | ack'd (`ROUND_END`), semantics also unconfirmed there |
| 0x43a4 skill experience | 🔴 | parsed for the log, **acknowledged, not persisted** (skills serve max) | **the only skill-progression path**: absolute values, host reports for everyone, persisted |
| 0x43a0 host migration | 🟡 | bare ack `{result=0}` | `{u32 own id, u32 target id}`; re-keys the game, drops the old host from the roster, joiner takes over the 0x4398 heartbeat — full succession verified live |
| 0x4348 | ➕ | acks 0x4349 `{result}` | not registered (their host pass is 0x43a0) |
| 0x43c0 in-game info edit | 🟡 | bare ack | parses name/comment/password (payload mirrors the 0x4310 blob) and **updates the hosted game**; reply must be 4 bytes `{result}` |
| 0x43c4 rate host | ✅ | validates 1..5, resolves game by membership, one vote per game, lifetime sums | same validation, persisted |
| 0x43c8 start round | ✅ | roster snapshot + `{u32 result=0, u32 token=0}` on 0x43c9 | `START_ROUND=0x43c8` → 0x43c9, token must be zero (instructor-prompt flag) |
| 0x43ca start round (this build) | ➕ | same handler registered as an **alias**, replying 0x43cb (paired id proven in this repo's MGO2.ELF: builder 0xF0E260, waiter 0xF0D4C0 completes slot 0x7f) | not registered — their build never sends it |
| 0x43a6 client setting | ✅ | ack `{u32 0}` on 0x43a7, nothing persisted | same (`PUT_CLIENT_SETTING`) |
| 0x4128 post-game info | ✅ | 0x8B buffer matching the observed layout | `postGameInfo` → 0x4129 |

## Game — social

| Command | Delta | This server | Reference |
|---|---|---|---|
| 0x4600 player search | 🟡 | 16-byte name query; presence + game columns per entry | reads `{u8 partial-match, u8 case-sensitive, name[16]}` and honours both toggles |
| 0x4680 match history | ➖ | empty list triple (0x4681/0x4682/0x4683) | real per-match records from its DB |
| 0x4684 match details | ✅ | empty list triple (0x4685-0x4687) — honest "no details" | same: empty list, never invented rows |
| 0x4220 player details card | ✅ | 207-byte card with stats + clan | `GET_PLAYER_DETAILS` → 0x4221 |
| 0x4500 / 0x4510 friends/blocked | 🔴 | persists a friend/block state per target with entries | `{state, id}` toggles; reference suspects it may be a *query* (its constant ack made targets render permanently as friends) |
| 0x4580 list roster | ✅ | 59-byte entries with live presence (online, lobby, game) | same layout, lobby column from PresenceService |

## Game — clans

| Command | Delta | This server | Reference |
|---|---|---|---|
| 0x4b00 create / 0x4b04 disband | ✅ | DB-backed, name-taken error | same |
| 0x4b10 clan list | ✅ | 48-byte entries, paged | paged `{u8 kind, s32 amount, u8}` with ±100 steps — paging request honoured; this server always sends page 0 |
| 0x4b20 clan profile | ✅ | 777-byte detail view, emblem-editor visibility | same |
| 0x4b30 / 0x4b32 accept/decline join | 🟡 | adds/removes membership rows directly, no leader validation | `judgeApplicant` (also no role gate observed) |
| 0x4b36 banish / 0x4b60 transfer / 0x4b62 emblem editor | 🟡 | applies the action, acks | `leaderAction` validates the actor leads the clan |
| 0x4b40 withdraw / 0x4b42 apply | ✅ | membership row insert/delete | same (applications persisted there; here apply = direct member insert) |
| 0x4b46 clan info update | ✅ | persists | same |
| 0x4b48 / 0x4b4a / 0x4b4c / 0x4b50 emblems | ✅ | block replies / upload / publish | same |
| 0x4b52 members | ✅ | one batch, members + applicants flagged per row (mixing them as two packets loses applicants — same lesson as the reference) | same fix documented |
| 0x4b64 / 0x4b66 comment / notice | ✅ | persisted with writer id + time | same |
| 0x4b70 clan stats | 🟡 | grid + blocks served (static-ish) | one 0x4b71 (584 B) **then** 0x4b72 (580 B) — never two 0x4b71s (documented stall) |
| 0x4b73 applicants | ➖ | always-empty list (no application persistence) | real pending-applicant records (93-byte rows) |
| 0x4b80 clan info | ✅ | served | `CLAN_PROFILE_PART` (`viewedClanInfo`) |
| 0x4b90 clan search | ✅ | exact/substring filters | exact-match + case toggles honoured |

## Game — hub, chat, mail, automatch

| Command | Delta | This server | Reference |
|---|---|---|---|
| 0x4150 lobby disconnect | ✅ | leaves tracker, syncs counts, acks | same |
| 0x43d0 training params | 🟡 | static 10-byte 0x43d1 payload | `GET_TRAINING_PARAMS` with computed params |
| 0x4900 lobby select | ✅ | GAME-type lobbies, 35-byte stride (1.36 build; the disc build's 64-byte text block breaks every entry after the first) | same fix reproduced in `ClientVersion.java` |
| 0x4990 game entry info | ✅ | fixed 236-byte reply (result + 4 × 57-byte records) | `GAME_ENTRY_INFO_RESULT` |
| 0x4440 | ✅ | ack `{u32 0}` | same (`UNKNOWN_4440`) |
| 0x4400 chat | ✅ | parses `/all`, broadcasts `{charaId, flag, message}` | `ChatGameController` push model, same idea |
| 0x4800 send mail | ➖ | success stub `{u32 0, u8 flags=0x01}` — the flags bit stops a 969-byte 0x4860 re-send loop | real mail delivery, persisted |
| 0x4820 / 0x4840 / 0x4860 / 0x4880 mail | ➖ | list/contents/append/delete served as stubs or empty | fully implemented over its message tables |
| 0x43e0 / 0x43e2 automatch | 🔴 | start is **refused** with the official `AUTOMATCH_NOT_OPEN` (-970) — answering 0 would register the client's push channel and stall it ~20 min; cancel acks 0 | full matchmaker: policy windows, experience-banded queue, 0x43e4 pushes, host election wired to `gameCreatedListener` |

## Notable asymmetries (summary)

**This server is ahead of the reference:**
- 0x4320 join capacity + password + endpoint + can-rate gates (reference has no capacity check)
- 0x4304/0x4305 saved-settings restore (reference always sends zeros)
- 0x4110 gameplay options parsed and persisted (reference acks unparsed)
- 0x43ca start-round support for this repo's client build (reference renumbered it away)
- Gear/skills served all-unlocked/all-max by design choice (reference models the original service's per-character ownership)

**The reference is ahead of this server:**
- Automatch: a real queue vs a polite refusal
- 0x4390 stat attribution (target id on the wire, roster/round-snapshot validation, absolute experience) vs this server's rough byte-offset parse applied to the host only
- 0x43a0 host migration actually migrates (this server only acks)
- 0x43c0 in-game host-settings edits applied (this server acks)
- 0x4392 rotation index applied (this server acks)
- Mail (0x48xx) and match history (0x4680) backed by real rows
- Clan applicants (0x4b73) backed by applications; leader validation on leader actions

**Shared deliberate non-behaviour:** 0x43a2 and 0x4684 are ack/empty in both;
0x43a6 persists nothing in both; the 0x4128/0x4220 shapes match the observed
layouts rather than invented ones.
