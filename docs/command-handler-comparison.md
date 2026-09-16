# Command handlers: this server vs `comradesean/mgo2server`

A command-by-command comparison of the TCP command handlers, written after reading both
registration tables and the handler bodies on each side.

| | this repository | reference |
|---|---|---|
| Revision | `0e31234` (2026-09-15), re-checked 2026-09-16 | `comradesean/mgo2server` `2e3b4dc` (2026-08-04) |
| Stack | C# / .NET 10, one handler class per command | Java / Netty, one controller per subsystem |
| Source | `src/*/Commands/**` | `src/main/java/mgo2server/game/controller/**` |
| Answered request ids | **85** (84 of them in the client's own send list) | **84** (83) |

The two servers cover **the same opcodes** — every command the reference answers is answered
here, and the one difference is `0x4348`, a client-sent command the reference leaves as a
`gap` — so almost every difference below is behavioural, not a matter of coverage. Where the
reference is ahead it is usually because its handlers carry years of reverse-engineering notes
on what the client does with the bytes, and this server's handlers were written against the
wire shape without all of that context.

The per-command status, in the reference's own vocabulary and section order, is
[`COMMAND_STATUS.md`](COMMAND_STATUS.md); that file is generated from this repository's source
by `tools/command_status_report.py`, so it is the side to trust when the two disagree. Each
count below can be reproduced offline with `tools/compare_command_inventory.py`, which needs a
checkout of the reference beside this one to read its ELF-derived id lists from.

### The 2026-09-16 pass

1. **`0x0003` and `0x0005` are registered commands.** They were answered by special cases in
   `TcpServerBase`, which meant the socket loop answered two commands the registry — the only
   inventory a reader can count — did not know about. `DisconnectHandler` marks the session
   for teardown (a handler returns nothing, so `TcpSession.DisconnectRequested` is how it
   reaches the read loop) and `KeepAliveHandler` replies through `SessionHelper`.
2. **Eleven registrations for ids the client never sends are gone**, with the nine handler
   classes behind them: `0x411a`, `0x411b`, `0x4122`, `0x4124`, `0x4125`, `0x4140`–`0x4143`,
   `0x4350`, `0x43ca`. The pushes they answered for are untouched.
3. **`0x43ca`/`0x43cb` are deleted**, including the branch in `StartRoundHandler` that chose
   between them; see §4.3.
4. **Every skill is served at maximum experience.** `CharacterSkillCatalogue` held skills 17,
   20 and 22 at the minimum visible experience on a list inherited from another server with
   no evidence behind it; skill 17 is the one training and graduation gates read, so nothing
   below the maximum is defensible on a server whose whole policy is "everything unlocked".
5. **`0x4103` carries the worn title and the title collection**; see §3.12.
6. **Six command constants that named ids outside the client's space are deleted**
   (`GetChatMacros`, `GetGameplayOptions`, `UpdateSkillSets`, `UpdateGearSets`, `UpdateStats`,
   `UpdateStatsResult`). Nothing referenced them, which is the point: an unreferenced constant
   for an id no client has is an invitation to wire it back up.

Confirmed against the live deployment, not just the source: across every `mgo2-*` container's
retained logs, the inbound opcodes total sixteen and all are answered, there is not one
`no-handler` line, and the five ids above that remain on the wire appear only outbound.

---

## 1. Dispatch, and what happens when something goes wrong

| Concern | this server | reference |
|---|---|---|
| Table | `CommandRegistry`, keyed by `(server type, opcode)`, value is a handler **type** resolved from DI | `GameServerHandler`, keyed by opcode, value is a `Consumer` built in the constructor |
| Duplicate registration | silently overwrites (`handlers[key] = type`) | **throws at construction**, naming both owners |
| Unknown opcode | logs `no-handler` with the payload hex, then sends a **4-byte masked-error packet on the same opcode** (deliberate — see §4.4) | logs `No handler for command X; ignoring. Payload: <hex>` and sends **nothing** |
| Handler throws | caught, logged at Error with the opcode and payload hex, connection kept | logged at Error with the opcode and payload hex, connection kept |
| Per-packet hooks | none | every controller's `onPacket` runs before dispatch; every controller's `onDisconnect` runs on socket close |
| `0x0003` / `0x0005` | registered commands (`DisconnectHandler`, `KeepAliveHandler`), one per TCP server | registered commands in `CommonGameController` |

**The reference is more careful about the two paths that stay silent, and the gap is not
cosmetic.**

* The unknown-opcode reply here is deliberate (§4.4): answering a command nobody handles is
  how the client is told to stop waiting. It is worth knowing what it costs, because the
  reference's own notes explain the failure mode — a masked-error reply on a command whose
  reply type has no result word is read as that reply's own first field, so a 4-byte `0x4101`
  "set the character id to `0xC0FFEE02` and zeroed the rest of the record … the client then
  entered the lobby with a garbage id and put it in every subsequent packet". Any opcode
  whose reply carries no result word inherits that shape, and `0x4100` / `0x4101` / `0x3049`
  are three of them — which is why every one of them now has a registered handler.
* **Both paths that answer nothing now log the payload hex**, matching the reference, and a
  throwing handler no longer tears the connection down: the command and the payload are the
  only evidence that names a stalled screen, and a closed socket hides which command died.
* The duplicate-registration throw would have caught the reference's own historic bug
  (`0x4b90` registered twice), where declaration order silently picked the winner. With
  this server's silent overwrite, a second `Register` call for the same opcode in a later
  registration method wins with no diagnostic, and (unlike an exception) it only shows up
  when a screen stops answering.
* No `onPacket` / `onDisconnect` equivalents: this server's per-connection teardown lives in
  the session/room services and maintenance timers instead. Both work; the reference's is
  push-driven, so a dropped socket tears down immediately rather than on the next sweep.

---

## 2. Opcode coverage

Registered request opcodes, by subsystem. "—" means no handler is registered, so the
reference logs a warning and replies nothing.

### Gate and account

| Opcode | this server | reference |
|---|---|---|
| `0x2005` | `GetLobbyListHandler` | `getLobbyList` |
| `0x2008` | `GetNewsHandler` | `getNewsItems` |
| `0x3003` | `CheckSessionHandler` (account) / `GameCheckSessionHandler` (gameplay) | `checkSession` — one handler serving both lobby types |
| `0x3048` | `GetCharacterListHandler` | `getCharacterList` |
| `0x3101` / `0x3103` / `0x3105` | `CreateCharacterHandler` / `SelectCharacterHandler` / `DeleteCharacterHandler` | `createCharacter` / `selectCharacter` / `deleteCharacter` |
| `0x3107` | `CheckCharacterNameHandler` | `checkCharacterName` |

### Lobby lifecycle and connect

| Opcode | this server | reference |
|---|---|---|
| `0x0001` | `EchoHandler` | `echo` |
| `0x4100` | `GetCharacterInfoHandler` (the connect burst) | `connect` |
| `0x4112` | `UpdateUiSettingsHandler` (ack, not stored) | `UNKNOWN_WRITE_BACK` (ack, not stored) |
| `0x4114` | `UpdateChatMacrosHandler` | `updateChatMacros` |
| `0x411a` | — (was `GetChatMacrosHandler`; the id is not one the client sends) | — |
| `0x411b` | — (was `GetGameplayOptionsHandler`; same) | — |
| `0x4700` | `GetPlayerDataHandler` | `updateConnectionInfo` |
| `0x4150` | `GetLobbyDisconnectHandler` | `lobbyDisconnect` |
| `0x43d0` | `TrainingConnectHandler` | `getTrainingParams` |
| `0x4440` | `ChatEchoHandler` | `UNKNOWN_4440` |
| `0x4900` / `0x4990` | `GetGameLobbyInfoHandler` / `GetGameEntryInfoHandler` | `getGameLobbyInfo` / `getGameEntryInfo` |

### Characters

| Opcode | this server | reference |
|---|---|---|
| `0x4102` | `GetPersonalStatsHandler` | `getPersonalStats` |
| `0x4110` | `UpdateGameplayOptionsHandler` | `updateSettings` |
| `0x4122` | pushed only, in the connect burst | pushed only, in the connect burst |
| `0x4124` / `0x4125` | pushed only, in the connect burst | pushed only, in the connect burst |
| `0x4128` | `GetPostGameInfoHandler` | `postGameInfo` |
| `0x4130` / `0x4132` | `UpdatePersonalInfoHandler` / `CommitOutfitHandler` | `updatePersonalInfo` / `commitOutfit` |
| `0x4140` / `0x4142` | pushed only, in the connect burst | pushed only, in the connect burst |
| `0x4141` / `0x4143` | — (the ids do not exist in the client) | — |
| `0x4220` | `GetCharacterCardHandler` | `getPlayerDetails` |
| `0x4500` / `0x4510` / `0x4580` | `AddFriendsBlockedHandler` / `RemoveFriendsBlockedHandler` / `GetFriendsBlockedListHandler` | `addList` / `removeList` / `listRoster` |
| `0x4600` / `0x4680` / `0x4684` | `SearchPlayerHandler` / `GetMatchHistoryHandler` / `GetMatchDetailsHandler` | `playerSearch` / `getMatchHistory` / `getMatchDetails` |

### Rooms, hosting and automatch

| Opcode | this server | reference |
|---|---|---|
| `0x4300` / `0x4312` | `GetGameListHandler` / `GetGameDetailsHandler` | `getGameList` / `getGameDetails` |
| `0x4304` / `0x4310` | `GetHostSettingsHandler` / `CheckHostSettingsHandler` | `getHostSettings` / `checkHostSettings` |
| `0x4316` / `0x4320` / `0x4322` | `CreateGameHandler` / `JoinGameHandler` / `JoinGameFailedHandler` | `createGame` / `joinGame` / `joinFailed` |
| `0x4340`–`0x4347` | `HostPeerRegistrationHandler`, `HostPlayerDisconnectedHandler`, `HostSetPlayerTeamHandler`, `HostPlayerConnectFinishHandler` | `playerConnection` (one handler for all four) |
| `0x4348` | `HostPassHandler` | — |
| `0x4350` | — (was `UpdateStatsHandler`; the id does not exist in the client) | — |
| `0x4380` | `QuitGameHandler` | `quitGame` |
| `0x4390` / `0x43a2` | `HostUpdateStatsHandler` / `HostWeaponTalliesHandler` | `updateStats` / `roundEnd` |
| `0x4392` / `0x4398` | `SetGameHandler` / `UpdatePingsHandler` | `setGame` / `updatePings` |
| `0x43a0` | `PassRoundHandler` | `hostMigration` |
| `0x43a4` | `HostSkillExperienceHandler` (ack, not stored) | `reportSkillExperience` (stored) |
| `0x43a6` | `PutClientSettingHandler` (ack, not stored) | `PUT_CLIENT_SETTING` (ack, not stored) |
| `0x43c0` / `0x43c4` | `HostInGameInfoHandler` / `RateHostHandler` | `editHostSettings` / `rateHost` |
| `0x43c8` | `StartRoundHandler` | `startRound` — `0x43ca`/`0x43cb` are gone; they were a misnumbering, not a second pairing |
| `0x43e0` / `0x43e2` | `StartAutomatchHandler` / `CancelAutomatchHandler` | `startAutomatch` / `cancelAutomatch` |
| `0x4400` | `SendChatHandler` | `sendChat` |

### Mail and clans

Mail matches one-for-one (`0x4800`, `0x4820`, `0x4840`, `0x4880`).
Clans match one-for-one as well: this server registers 23 clan opcodes
(`0x4b00`–`0x4b93`) and the reference registers the same 23, including both
emblem-fetch ids (`0x4b48`, `0x4b4a`, `0x4b4c`) and clan statistics (`0x4b70`).

---

## 3. Where the reference is more complete

### 3.0 Status of this section

The divergences below have since been ported, except where noted. The subsections
that follow are kept as written, because they explain *why* each difference
mattered; this table says what the port actually did.

| § | Divergence | Status |
|---|---|---|
| 3.1 | Skill progression not persisted | **Not ported — intentional.** Every skill is served at its maximum level, so a stored report could never change what the client sees; persisting it would only add a table nothing reads. |
| 3.2 | No awards or titles | **Ported (titles).** `characters_titles` plus `CharacterTitleService`, evaluated at round end and on lobby entry, with the best latched rank written into the `0x4122` payload. There is still no separate awards table. |
| 3.3 | Rankings unimplemented | **Ported.** Two POST endpoints, `RankingScrambleUtils` and `RankingBodyUtils`; see §3.11. |
| 3.4 | Clan statistics wrong shape | **Ported.** `0x4b70` answers the grid + blocks pair. |
| 3.5 | Clan list paging | **Ported.** `0x4b10` reads `{u8 kind, s32 amount, u8}`, pages 100 at a time, and emits the `0x4b11` list header. |
| 3.6 | Beginners-only never enforced | **Ported.** `0x3003` refuses with `LOBBY_ENTRY_REFUSED` when the character is above the measured level-4 threshold. |
| 3.7 | Weak check-session gate | **Ported.** The gameplay gate verifies the character is active; both gates record the account's current character. |
| 3.8 | Connect never refuses | **Ported.** `0x4100` answers nothing when the session has no usable character. |
| 3.9 | No automatch policy seam | **Ported.** `AutomatchOptions` (enabled, windows, tick) and an `AUTOMATCH_NOT_OPEN` refusal; the live ticker is `AutomatchTickerService`. |
| 3.10 | Per-account entitlements | **Not ported — intentional.** The unlock-all policy is the whole point of the fork and is documented in `docs/expansion-entitlements.md`. |
| 3.12 | No instructor subsystem | **Ported.** Graduation, the saved instructor, the award letter, the score gauge, students trained, training seconds and the instructor ranking board; see §3.12. |

Two items in §4 are likewise deliberate and stay as they are: the unlock-all content
mask, and the general error packet sent for an unhandled opcode.

### 3.11 Rankings, as implemented here

The two endpoints are `POST /jp/mgo2/rank/mgogetrank.html` and
`POST /jp/mgo2/rank/mgogetrank_clan.html`, matching this server's `/jp/mgo2` base URL
rather than the reference's `/us/mgo2`.

Six form fields, clamped the way the client clamps them: `term`, `rule` (masked to four
bits), `skey` (masked to three), `from`, `records`, and `pid` or `cid`.

Boards (`skey`): `0` score by game mode, `2` activeness, `3` grade points, `4` host rating,
`5` instructor rating, `6` clan score. Every board is derived at query time; nothing is
precomputed. Where this server has no source the board is **empty rather than zero-filled**
— the client renders a zero-total board as an unselectable row, which is the honest answer,
while a column of zeroes would look identical on screen while claiming something was
measured. Concretely:

* `skey 5` (instructor rating) reads `instructor_reviews`, the same source the reference
  uses; see §3.12. It was empty in the first cut of this port, before the instructor
  subsystem existed.
* `skey 0` reads the per-mode statistics blob (`stats_dm` … `stats_scap`), which yields a
  lifetime score per mode. This server does not store a per-round score, so the MONTH half
  of the toggle is ignored on this board. `skey 2` (activeness) and `skey 4` (host rating)
  are the boards that do answer the monthly window, from `round_reports.created_at` and
  `host_reviews.reviewed_at` respectively.
* A `rule` outside 0–10 answers an empty board rather than silently falling back to the
  deathmatch blob.

The reply is `12 + 28×N` bytes, little-endian, every byte XORed with the eight-byte key and
period-20 keystream. `N` is the number of rows actually serialised, never what the client
asked for, and the window is clamped to 100. A name that fills all sixteen bytes is
clamped to fifteen characters so the field keeps a NUL, because the client reads it with
`strlen` into a buffer it never clears.

### 3.12 The instructor subsystem, as implemented here

This was the largest gap the first comparison missed entirely: the reference runs a whole
instructor career and this server had none of it. What is now implemented:

**Becoming an instructor.** A combat training session (lobby subtype **8** — the subtype is
the behaviour, the name is not) is hosted by the instructor and joined by students. When it
ends, the student sends `0x43c8` — the same id a host uses to start a round — carrying
`{u32 rating, u8 answer}`. In a subtype-8 lobby, from someone who is not the host, that is a
review rather than a round start, and it is recorded in `instructor_reviews`.

The answer byte decides the rest. Only an exact `0x01` is a recognition; `0x00` is a declined
prompt and `0x21` is a prompt never shown. The tempting `(answer & 0x20) == 0` shorthand reads
a *declined* prompt as an acceptance and would permanently name an instructor the player
refused — `InstructorGraduationUtils.IsRecognised` is the only test, and `InstructorTests`
pins all three values.

A recognition writes the relationship into `characters_instructors`, one row per student
with the instructor's name, a generation one past the instructor's own, and the rating.
Every review is stored whether or not it was recognised, so a rating without recognition is
not lost and the score gauge has a source.

**The award and the letter.** The skill is *not* granted by the graduation. It rides the
end-of-round statistics report (`0x4390`) instead, because the host reports every player as
they leave — combat training included — so the award is a consequence of the session ending
and one that is missed is picked up by the next report. Eligibility is Konami's documented
rule: **level 3 or above and 20 or more hours of gameplay**, enforced against this server's
own level table and the round reports. On a pass the relationship latches and the
announcement letter is delivered to the student's mailbox, reproduced verbatim from the
original — Konami URL included.

> **The skill is already held.** Every character is served the whole skill catalogue at its
> maximum level, deliberately, so granting skill 17 changes nothing the client displays. The
> latch therefore guards the *letter*, not the skill: without it every later report would
deliver another copy. `InstructorService.AwardPendingInstructorSkillAsync` claims the latch
with a conditional update before delivering, so two reports in flight send one letter.

**The readings.**

| Where | What |
|---|---|
| `0x4122` last field | the saved instructor's character id, or zero. This was the fixed word `00 A7 00 0D` copied from another server, which announced "I already have an instructor" for *every* character and so suppressed the recognition prompt for all of them. |
| `0x4103` instructor block | the instructor's name (16 bytes) and the generation |
| `0x4103` rating block, entries 5/6 | host rating numerator and denominator |
| `0x4103` after the clan emblem flag | instructor score numerator and denominator |
| `0x4107` slot 36 | distinct students graduated, both periods |
| `0x4107` slots 46/47/48 | training, instructor and student seconds — cumulative only |
| rankings `skey 5` | average instructor rating as 8.8 fixed point |

**Presence.** Training sessions report nothing at all — the client sends no end-of-round
frame for one — so the training totals are the only measurement that exists for them, and
they are the interval between joining a room and leaving it. `GameService` credits them on
every path that ends presence: a player leaving, a room being torn down (the host quitting),
and a whole lobby going away. The credit shares a transaction with the removal, because the
roster row's join time is the only record of the interval. Only subtypes 7 and 8 create a
row; every other lobby's play time comes from the round reports instead.

**The two title fields are written as of 2026-09-16.** `0x4103` carries the worn rank at
wire 541 (the same value `0x4122` writes, from the rank the title service latched) and the
collection as rating-block entry 3, `CharacterTitleService.BuildTitleMask` packing each
latched rank into bit `rank - 1`. **Ranks past the client's 22-title table set no bit**:
`AnimalRankService` also returns the post-1.30 ranks (Killer Whale 26, Octopus 40, and so
on) which have no badge, and the client's popcount loop walks the table once per title, so
shifting one of those would make it read past the end. `CharacterTitleTests` pins both
halves.

The medal bits stay zero, which is deliberate — the client mints medals from those words,
so anything we cannot measure honestly must be zero.

---

### 3.1 Skill progression is not persisted here (`0x43a4`)

The reference calls `0x43a4` **the only way skill progression can persist**: skills level by
use, which only the client can observe, and this command is the client's report of the
absolute per-skill values. It writes them to the character.

`HostSkillExperienceHandler` parses the report, logs it, and answers `result(0)` — the reply
is correct, the write is not. Per the handler's own comment this is deliberate: every
character is served the maximum-level skill catalogue (`CharacterSkillCatalogue`), so there
is nothing to store.

**Intentional divergence, not an oversight, and it stays that way.** Because the catalogue is
the whole answer, a stored report could never be served differently from the maximum — the
value would be write-only. Porting the write path would therefore add a table, a service and
a read path that change nothing a player sees. If progression is ever wanted, the change is
to make the stored value authoritative instead of a floor, and only then does the table earn
its place.

### 3.2 Awards and titles do not exist here

The reference has `AwardService` + `awards.json`, re-evaluates titles on lobby entry
("stamping the visit also re-tests the titles … so an absence-based requirement can still
see the gap it is measuring") and at round end, and writes the worn title into the `0x4122`
payload. This server has no award or title model at all. Its closest equivalent,
`AnimalRankService`, only derives the animal rank from lifetime statistics.

### 3.3 Rankings are unimplemented here (HTTP, not TCP)

The reference serves the Rankings screens: a **POST** to `rank/mgogetrank.html` and
`rank/mgogetrank_clan.html` with a form body of six parameters, answered with a
little-endian binary blob whose entire body is XOR-scrambled with an eight-byte key
(`RankingScramble`, traced instruction-by-instruction, period 20 bytes). The reference is
explicit that this is *not* lobby protocol — the rankings screens do not touch `0x4xxx`.

This server has no ranking endpoints; its `src/Http` serves files, help, policy, version,
login and a JSON API only. Adding the two POST endpoints plus the scramble is self-contained
work with a fully documented wire format on the reference side.

### 3.4 Clan statistics answer the wrong shape (`0x4b70`)

`GetClanStatsHandler` sends two packets with **no payload**. The reference builds both: a
grid of `result(0)` + a page word + zero fill, and a blocks packet of `result(0)` + zero
fill — and its comment explains the shape is load-bearing, because sending the first packet
twice "completed the request slot on the first reply and the second arrived unexpected, which
is the `unable to acquire clan information (1931:FFFFFF60)` stall on Clan Affiliation".

A payload-less reply to a command whose parser reads a result word is the same class of bug.
Neither server has real clan statistics; the reference at least answers with the layout the
client parses.

### 3.5 Clan list paging

The reference reads the clan-list request as `{u8 kind, s32 amount, u8}` where `kind` selects
the arm (1 = back 100, 2 = forward 100, 4 = absolute, 0/3 = first page), sizes pages at the
client's own 100-entry array (`cmpwi r4,99` at `0xD561E4`), and sends a list header
(`0x4b11`) carrying total-and-offset before the entries.

`GetClanListHandler` ignores the request body entirely and pages only by packet size. For a
shard with more than one page of clans, the browser's cursor has nothing to page against.

### 3.6 Beginners-only lobbies are published but never enforced (`0x3003`)

Both servers put the restriction bit in the gate's lobby list. The reference then *enforces*
it at connect: on the gameplay lobby's check-session it compares the claimed character's
experience against a measured level-4 threshold and refuses with `LOBBY_ENTRY_REFUSED`,
because "the client has the whole mechanism … and observed 2026-07-28 it simply does not
fire".

This server publishes the bit (`GetLobbyListHandler`) and never checks it on the way in.
`BeginnerOnly` is read from configuration and stored, but no handler consults it.

### 3.7 Check-session is a weaker gate here

The reference's `checkSession` serves both lobby types and, for the gameplay lobby, verifies
that the claimed character exists, **belongs to the account and is active**
(`!claimed.isActive()` → refuse), then records the current character. This server's
`GameCheckSessionHandler` verifies existence and ownership but not the active flag, and the
account-lobby `CheckSessionHandler` clears only the session's character, not the account's
stored current character.

### 3.8 Connect (`0x4100`) never refuses

The reference refuses the connect burst by answering **nothing**, with a long note on why:
`0x4101` is a fixed record with no result word, so a short reply or a zero id is worse than
silence — the client carries a garbage character id into every later packet and eventually
times out with `1037:FFFFFF60`. It also logs the three conditions that trigger a refusal as
"unreachable, `0x3003` refuses these first".

`GetCharacterInfoHandler` has no such path: with no character on the session it sends the
whole burst with `characterIdentifier = 0` and an empty name, which is the "full-length reply
with a zero id" the reference explicitly judges no better than silence. Worth adopting the
refusal, since the burst is nine packets and the failure mode is silent.

### 3.9 Automatching has no policy seam

The matchmaking numbers are **identical** on both sides (min players 12 → 2, 30 s per step;
band 1 → 22, 30 s per step; mode relax 90 s). The difference is configurability:

* Reference: `AutomatchPolicy` is built from `MGO2SERVER_AUTOMATCH_*` environment variables
  and carries `enabled`, `windows` (an open-hours schedule with an explicit zone; empty means
  never), `tick`, and `slotInLobbies`. When closed, `0x43e0` is refused with
  `AUTOMATCH_NOT_OPEN` — "the alternative is the ~20-minute silent stall this feature
  replaced".
* This server: `AutomatchService` constructs `AutomatchPolicy` with the defaults inline;
  there is no enable flag, no schedule and no tick setting. The start reply (`0x43e1`) always
  carries `result(0)`, so the client always arms its push channel and waits — the reference's
  note that "a zero result is not an acknowledgement" applies here.

### 3.10 Entitlements are per account on the reference, fixed here

The reference reads the `0x3049` trailer from the **account row**
(`entitlements_byte1`, `entitlements_byte3`), so "an `UPDATE` takes effect on the next
character-list fetch, with no restart and without touching anyone else", and marks lobbies
with `expansion_required` so the client's own gate is exercised. Its notes also flag the
value it inherited: index 3 bit 0 grants the day-one **paid** MGO Codec Pack, "we were
handing out purchased content because a reference server did".

This server hardcodes the trailer (`0x07` expansion, `0x03` codec) for every account and
sets every bit of the `0x4101` content mask, so nothing is ever gated. See §4.1.

### 3.11 Client-version handling on `0x4900`

`writeEntry` in the reference takes a `ClientVersion` and sizes the entry accordingly, with a
comment on why the attributes word is written byte-by-byte. This server writes a fixed
35-byte stride.

The 35 bytes **are** the 1.36 layout — 4-byte index, subtype, three bytes, clan id, 16-byte
name, then the open/close times and the open flag, with no text block — and 1.36 is the build
this server targets, so nothing is wrong here today. What the reference has that this one does
not is a second build: on 1.0 the entry is 99 bytes, the extra 64 being a per-lobby text block
whose only consumer (the subtype-5 branch) 1.36 deleted. Sizing it per build is worth doing
the day a 1.0 deployment is wanted, and the value belongs in configuration beside the other
build-specific ones rather than in the payload builder, where it would be the first of several
such constants to drift apart.

---

## 4. Where this server is ahead, or deliberately different

### 4.1 Unlocking everything (intentional)

`docs/expansion-entitlements.md` documents this as a deliberate policy:

* `CharacterListPayloadBuilder` writes `0x07` at the `0x3049` trailer's index 1 (GENE +
  MEME + SCENE) and `0x03` at index 3 (codec packs).
* `FeatureFlags.ContentMask` sets bits 0–55 of the `0x4101` content-availability mask, so
  every mask-gated map, rule and character is offered, and `ExpansionByte = 0x0f`.
* `PersonalInfoHandlers` unlocks all thirty-two face-paint colours; `GearCatalogue` serves
  the full catalogue with nothing highlighted as locked; skills are served at maximum level
  (§3.1).
* `LOBBY_EXPANSION_ONLY` exists purely so a lobby can *be* expansion-only; with everyone
  entitled, it changes nothing.

So the reference's per-account entitlement model (§3.10) and this server's unlock-all policy
are the same bytes carrying opposite intent. Any future decision to gate content should
start by choosing between them — the mask constant is the single switch.

### 4.2 Answers requests the reference only pushes — retracted, and the registrations removed

| Opcode | This server | Reference |
|---|---|---|
| `0x411a` | no longer registered | pushed only, in the connect burst |
| `0x411b` | no longer registered | pushed only, in the connect burst |
| `0x4141` / `0x4143` | no longer registered | pushed only, in the connect burst |

This section used to argue that answering the read-side commands let a client re-fetch a
screen instead of reading the login snapshot. **The client cannot send them.** None of the
four ids is in the ELF's client-to-server list, and none appears in the deployment's inbound
traffic, so the handlers were unreachable code rather than extra coverage — the same defect
the reference calls PHANTOM, one layer further in. The registrations and their handler
classes are gone; the writes they duplicated (`0x4114`, `0x4110`, `0x4130`, `0x4132`) are
untouched.

The connect burst is unchanged and still pushes the full catalogue in the original's order
(character info, gameplay settings, chat macros ×2, personal info, gear, skills, skill sets,
gear sets). `0x4122`, `0x4124`, `0x4125`, `0x4140` and `0x4142` are emitted and still are —
the deployment's logs show all five leaving the server, and none arriving.

### 4.3 Extra opcodes answered

`0x4348` (`HostPassHandler`). The reference registers nothing for it, on the reference a
stray `0x4348` gets a hex dump in the log and no reply, and the client genuinely sends it
(`0xd4a8a4` in its own list). This is the one place the two servers' answered sets differ,
and it is deliberate: matching the reference exactly would mean removing the answer to a
command the client issues.

Two former entries here are gone. `0x43ca` (`StartRoundAlias`) was a **misnumbering** of
`0x43c8`, kept because "each client build parses one of the two pairings" sounded plausible;
the ELF has no builder and no parser for `0x43ca` or `0x43cb`, and the reply branch that
chose between them is deleted with the constants. `0x4350` (`UpdateStatsHandler`) is not in
the id space either — no builder, no parser, no inbound sighting — so its registration is
gone; the round statistics it fed arrive through `0x4390` and `0x43a2` as they always did.

### 4.4 A general error for an unhandled opcode (intentional)

`TcpServerBase` answers an unregistered opcode with a general error packet on that same
opcode, so a client waiting on a reply is told there will not be one rather than left to
time out. The reference is silent instead, and is right that silence is safer for the reply
types that carry no result word — see §1 for what that costs. The protection against it is
coverage, not silence: every such opcode on the wire (`0x4100`, `0x4101`, `0x3049`, `0x43c8`)
has a handler registered, and a new one should get the same treatment rather than relying on
the fallback's shape.

---

## 5. Naming differences on shared opcodes

Both know what the command *is* in most cases; the names disagree where reverse engineering
refined the reading. Worth aligning comments, not code.

| Opcode | This server | Reference | Note |
|---|---|---|---|
| `0x4100` | `GetCharacterInfoHandler` / `GetCharacterInfo` | `connect` | The reference treats it as "the connect burst"; the name here reads as a plain read |
| `0x4112` | `UpdateUiSettings` | `UNKNOWN_WRITE_BACK` | Same behaviour on both: acknowledged, not stored |
| `0x4120` / `0x4121` | `GetGameplayOptionsResult` / `GetChatMacrosResult` | `GAMEPLAY_SETTINGS` / `CHAT_MACROS` | Same ids, both pushes from the connect burst |
| `0x43a0` | `PassRound` ("host migration") | `hostMigration` | The reference's key correction: this is sent when the **host quits**, not when a player deliberately hands the game on — only the host sends it, everyone else sends `0x4380`. The successor is elected silently by connection-quality score. The name here is fine; the comment should say "quit" |
| `0x43a2` | `HostWeaponTallies` | `ROUND_END` | Both read it as end-of-round data; the reference warns the exact meaning is unconfirmed |
| `0x4390` | `HostUpdateStats` | `updateStats` | |
| `0x4440` | `ChatEcho` | `UNKNOWN_4440` | Both answer a bare result; the reference notes two upstream references disagree on the shape and that this server's `0x4441` result matches "echo" |
| `0x43d0` | `TrainingConnect` | `getTrainingParams` | |
| `0x4b20` | `GetClanMemberInfo` | `clanProfile` | |
| `0x4b40` | `LeaveClan` | `withdraw` | |
| `0x4b46` | `UpdateClanState` | `clanInfo` | |
| `0x4b48` | `GetClanEmblemLobby` | `CLAN_DATA` (768-byte block) | |
| `0x4b4c` | `GetClanEmblemWorkInProgress` | `clanDetail` | |
| `0x4580` | `GetFriendsBlockedList` | `listRoster` | |
| `0x4220` | `GetCharacterCard` | `getPlayerDetails` | |
| `0x4840` | `GetMessageContents` | `readMessage` | |

---

## 6. Behaviours that already agree

Worth recording so a later diff does not re-open them:

* **Round statistics frame**: identical field offsets (target at `0x00`, team win `0x23`,
  seconds `0x25`, absolute experience `0x27`, aborted `0xB7`, fifteen signed counters from
  `0x05`), the same "apply only if the target verifiably played" rule, the same round
  snapshot so a mid-round quitter still counts, and the same `round_report` row as the
  source of the history screens.
* **Host migration (`0x43a0`)**: the payload reading (`{own id, elected successor}`), the
  roster membership test, the re-key to the successor and the sender leaving the roster, and
  the fallback that tears the game down when the client's elected successor is already gone
  ("a ghost row the browser still lists and nobody can join").
* **Automatch policy numbers**: every default matches (see §3.9).
* **Training parameters (`0x43d0`)**: both send the same five `u16`s — 10, 21, 58, 8, 97 —
  and both label them placeholders with a plausible shape rather than protocol.
* **`0x4110` gameplay options**: stored and replayed on both; the reference notes that
  acknowledging without storing "is why every Gameplay Option reverted after a session".
* **ADDLIST (`0x4500` / `0x4510`)**: persisted on both and replayed as the `0x4101` friend
  and blocked arrays, with a separate reply for the added and the removed entry.
* **Host ratings (`0x43c4`)**: the 1..5 range is enforced on both; this server also records
  one vote per player per game, which the client keeps no memory of across joins.
* **Character deletion cooldown**: both refuse to delete a character below a fixed age.
* **Connection registration (`0x4700`)**: both take the public address from the socket, not
  the payload.
* **Lobby restriction bits**: beginner `0x01`, expansion `0x08`, no-headshots `0x10`.

---

## 7. Open disagreement: the `0x4101` grid

The two servers send different lengths for the same packet, each citing disassembly:

| | This server | Reference |
|---|---|---|
| Grid | `0x229` (4 + 16 + 8 + 12 + 1, then **64** friend and **64** blocked ids of 256 bytes each) | `0x142` (**32** friend and 32 blocked ids of 128 bytes each, then a zeroed 25-byte tail) |
| Content mask | 16 bytes at `0x22a`, then a feature byte at `0x242` | not sent; "anything past `0x142` is never read" |
| Evidence | parser at `0x00f08a18` reads the mask at `0x22a` | parser at `0xD3C120` consumes a fixed `0x142` grid |

These cannot both be right, and the consequence differs by which one is:

* If the reference is right, this server's mask at `0x22a` is **never read**, and the
  map/rule gating it is meant to open is actually governed by something else — the whole
  "unlock all maps and modes" policy would then depend only on the expansion word.
* If this server is right, the reference's 32-id arrays put its blocked list at the wrong
  offset and its tail where the mask belongs.

The cheap test is a live client in a lobby with a deliberately narrowed mask: if narrowing
it changes which map rows the create-game screen offers, the mask is read; if nothing
changes, the reference's grid is the one to adopt. Worth doing before either side's copy of
the layout is quoted as settled.

---

## 8. Follow-ups

Items 1–4 of this list have since been ported (see §3.0); they are kept here with their
resolution so the reasoning stays attached to the change.

1. ~~Make `CommandRegistry.Register` throw on a duplicate `(server type, opcode)`.~~
   **Done** — it throws, naming both registrations. The general error for an *unhandled*
   opcode was left in place deliberately (see §4.4).
2. ~~Fix `0x4b70` to answer the two-packet shape.~~ **Done.**
3. ~~Enforce beginners-only at `0x3003`.~~ **Done.**
4. ~~Give automatching a policy seam.~~ **Done** — `AutomatchOptions` plus
   `AUTOMATCH_NOT_OPEN`, with `AutomatchTickerService` driving the push channel the refusal
   used to leave armed.
5. **Settle the `0x4101` layout question** with the narrowed-mask test in §7. Still open,
   and still the one item here that decides whether the unlock-all content mask has any
   effect on the create-game screen.
6. **Verify the ranking boards against a live client.** The wire format is settled (and
   covered by `RankingTests`), but the `skey` meanings are inferred on both servers, and no
   capture of a real Konami ranking response exists to check them against.
7. **Remove the three remaining phantoms** once each is settled: `0x4115` (the chat-macro
   write-back, which the reference says to stop sending outright — needs one live client,
   because a client that waits on the slot stalls when nothing answers), and `0x4140` /
   `0x4142` (skill and gear sets in the connect burst, which no client parses; the reference's
   own advice is to trace `0x4133` first rather than delete on inference). They are listed in
   `COMMAND_STATUS.md` under "Ids we touch that the client does not".
8. **Run a combat training session end to end.** The instructor flow is untested against a
   client: the subtype-8 branch in `0x43c8`, the saved-instructor field in `0x4122` (which
   must now let the recognition prompt actually appear, where the old constant suppressed
   it for everyone), and the award letter all need one live graduation to confirm. Publish a
   lobby with `LOBBY_SUBTYPE=8` — the seeded row is named "Combat Training" — and watch for
   the prompt.
