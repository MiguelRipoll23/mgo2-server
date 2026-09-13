# Code review — MGO2 server (.NET 10)

**Scope:** whole-repository correctness pass over the tree as it stands (~213 files / 22k
LOC). Transport, protocol, crypto/codec, domain, persistence, HTTP and DNS layers were read.
No git history is available in this checkout, so there is no diff to review.

**Clean build verified:** `dotnet` was installed during the fixes, the solution builds with
`dotnet build Mgo2Server.slnx --configuration Release` with no warnings, and the test suite
(`dotnet test`) passes with `Passed! - Failed: 0, Passed: 25, Skipped: 0, Total: 25`.

---

## What is in good shape

- Consistent per-operation `DbContext` through `IDbContextFactory` (`DomainService`), so
  long-lived connection handlers never share context state.
- `ON CONFLICT` upserts for hot rows, and real transactions where an interrupted write would
  be unrecoverable (`ClanService.CreateAsync`, `ClanService.DisbandAsync`,
  `CharacterService.CreateAsync`).
- Wire-format hardening in `PacketCodecService`: length validation, HMAC-MD5 checksum,
  XOR obfuscation, and a null return instead of an exception on a malformed packet.
- JWT verification uses `CryptographicOperations.FixedTimeEquals` and refuses to start
  without `JWT_SECRET`.
- Session ownership is checked rather than trusted: `CheckSessionHandler` and
  `GameCheckSessionHandler` both verify the presented session belongs to the claimed
  account/character, and `GetPlayerDataHandler` takes the public address from the socket
  rather than the payload.
- Peer-to-peer frames are digest-gated before their chain is removed
  (`FrameCryptoUtility.VerifyTailDigest`), and the compression output is bounded
  (`LzssMaximumOutput`).
- The concurrency design — one receive loop per process, writes serialized by
  `TcpSession.WriteAsync`, sequence numbers advanced through `SessionHelper` — is sound in
  principle (see finding 7 for the one gap).

---

## Critical — unauthenticated remote crash

These two make a container exit rather than log an error. They are reachable by any host
that can send a UDP packet to the published ports.

### 1. Gameplay server: a two-byte datagram kills the UDP host — fixed

`src/Shared/Utils/FrameCryptoUtility.cs` — `ScramblePositions`, `UnscrambleHeaderOnly`

`ScramblePositions(length)` computes `span = (uint)(length - 2)` and takes a modulo by it. For
`length == 2` that is a division by zero; for `length` 0 or 1 the derived swap indices read
outside the buffer. `HandleDatagramAsync` is awaited directly from `ReceiveLoopAsync`
(`src/GameplayServer/GameplayServerService.cs`) with no `try`/`catch`, so
`DivideByZeroException` / `IndexOutOfRangeException` escapes `RunAsync` and ends the process.

**Fix:** require a minimum datagram length (`HeaderSize + TailSize`, matching what
`VerifyTailDigest` already assumes) before unscrambling, and wrap per-datagram handling in a
`try`/`catch`. **Done** in `FrameCryptoUtility.UnscrambleHeaderOnly`,
`GameplayServerService.HandleDatagramAsync`, and `FrameBuilderUtility.ParseHandshakeBody`
(the last one is also part of finding 2).

### 2. Gameplay server: a truncated handshake crashes the parser — fixed

`src/Shared/Utils/FrameBuilderUtility.cs` — `ParseHandshakeBody`

The guard is `body.Length < 0x10`, but the pairing loop then reads up to
`16 + pairCount * 6 + 5` (offset 27 for `pairCount == 2`). A body of, say, 17 bytes with
`pairCount = 2` throws from `body[offset]` or from
`BinaryUtilities.ReadUInt16LittleEndian`. The pre-handshake digest key is a compile-time
constant, so a forged pre-keyed frame reaches this parser from an unauthenticated sender and
kills the host through the same unguarded path as finding 1.

**Fix:** guard with `body.Length < HandshakeBodySize` (0x1c), or check `offset + 6 <=
body.Length` per pair, and add the per-datagram `try`/`catch`. **Done**:
`ParseHandshakeBody` now guards with `HandshakeBodySize`, and the per-datagram
`try`/`catch` covers it.

---

## High — authorization gaps

### 3. Clan write commands trust the clan identifier from the packet

`src/GameLobbyServer/Commands/Game/Clans/ClanManagementHandlers.cs` and
`src/GameLobbyServer/Commands/Game/Clans/ClanEmblemHandlers.cs`

`TransferClanLeadershipHandler`, `SetEmblemEditorHandler`, `UpdateClanCommentHandler`,
`UpdateClanNoticeHandler` and `SetClanEmblemHandler` all take `clanIdentifier` straight off
the wire with no membership or leadership check. Any player in a gameplay lobby can:

- rewrite any clan's comment, notice or published emblem;
- assign the emblem editor of any clan;
- hand any clan's leadership to a membership row of their choosing.

`DisbandClanHandler` is the only handler in the family that validates the caller (it walks
the clans looking for one the caller leads). That is the pattern the others need —
`ClanService.GetMemberAsync(clanIdentifier, session.CharacterIdentifier)` followed by a
leader check where the operation requires it. **Done**: all five handlers resolve the
caller's membership row; leadership transfer and emblem-editor assignment additionally
require the caller to be the leader (and the target to be a member of that clan), and the
notice records the caller's membership row rather than its character identifier.

### 4. Room mutations are not host-gated

`src/GameLobbyServer/Commands/Game/Rooms/RoomHostHandlers.cs`

`SetGameHandler`, `UpdatePingsHandler` and `StartRoundHandler` only require the caller to be
in the room. Any member can change the staged rotation entry, push arbitrary pings for other
players, and snapshot the roster (`MarkRoundPlayersAsync`) — which is the attribution check
that `HostWeaponTalliesHandler` and the round-statistics handlers rely on to decide whose
statistics count.`QuitGameHandler`, `PassRoundHandler`,
`HostInGameInfoHandler` and `RateHostHandler` all verify the host, so these three look like
oversights rather than a deliberate design. **Done**: all three now require
`game.HostIdentifier == session.CharacterIdentifier` before they mutate anything.

---

## Medium — thread-safety of state shared by all connections

Every domain service is registered as a singleton, and connection handlers resolve from the
container and run concurrently on the thread pool while `PeriodicWorker`s and HTTP requests
touch the same objects.

### 5. `TcpServerBase.sessions` is an unsynchronized `Dictionary`

`src/Shared/Tcp/TcpServerBase.cs`

`HandleConnectionAsync` runs fire-and-forget from the accept loop, so
`sessions[remoteAddress] = session` and `sessions.Remove(remoteAddress)` execute
concurrently on the thread pool. A plain `Dictionary` is not safe for concurrent mutation.

The `Sessions` property that exposes the dictionary is never read anywhere in the repository
(`ActiveGameSessionsService` is the collection the command handlers actually use), so the
state is simultaneously dead and unsafe.

**Fix:** delete the dictionary and the property, or make it a `ConcurrentDictionary` if a
future reader is intended. **Done**: both were deleted.

### 6. `LobbyTrackerService` is read and written concurrently

`src/Shared/Domain/Lobbies/LobbyTrackerService.cs`

`sessionsByLobby` (`Dictionary<int, HashSet<TcpSession>>`) is:

- written by `JoinLobby` from a connection handler (`GameCheckSessionHandler`) and by
  `LeaveLobby` from `GameplayLobbyServer.OnSessionDestroyed`;
- read by `GetPlayerCount`, and iterated by `SynchronizeAllLobbyCountsAsync` from the
  heartbeat worker **and** from a `Task.Run` spawned per disconnect in
  `GameplayLobbyServer.OnSessionDestroyed`.

A `foreach` over `sessionsByLobby.Values` that overlaps an insert throws
`InvalidOperationException`. The heartbeat's copy is swallowed and logged by `PeriodicWorker`
(by design), so the visible symptom is that **published lobby player counts silently stop
updating** while the server keeps running.

**Fix:** guard the tracker with a lock (or use `ConcurrentDictionary` plus an
`ImmutableHashSet`), and debounce the per-disconnect `Task.Run` — the heartbeat already
republishes every lobby's count on a timer, so syncing on every disconnect is a DB write
storm under connect/disconnect churn. **Done**: the tracker is guarded by a `Lock` and
snapshots the counts under it before writing; the per-disconnect republish goes through a
one-second debounce timer that coalesces a burst into one write.

### 7. Session sequence numbers are advanced without synchronization

`src/Shared/Utils/SessionHelper.cs`, `src/Shared/Tcp/TcpServerBase.cs` — `SendKeepAliveAsync`

`session.SequenceOut++` is a non-atomic read-modify-write. `TcpSession.WriteAsync` serializes
the *writes*, but the sequence number is read and incremented before the lock is taken, so
the HTTP-triggered `/flash-news/broadcast` (which writes to every active session) can
interleave with that session's own handler and emit two packets carrying the same sequence
number. Use `Interlocked.Increment` and take the value inside the write lock. **Done**:
`TcpSession.NextSequenceOut` reserves the number with `Interlocked.Increment`, and every
writer (`SessionHelper`, `SendKeepAliveAsync`) draws it before encoding.

---

## Medium — data integrity and correctness

### 8. Check-then-insert races on uniqueness

| Path | Check | Insert |
| ---- | ----- | ------ |
| `RegistrationService.RegisterAsync` | `AnyAsync(display_name == …)` | `users` |
| `SessionService.CreateSessionAsync` | `FirstOrDefault(user_id == …)` | `sessions` |
| `ClanService.CreateAsync` (via `CreateClanHandler`) | `FindByNameAsync` | `clans` |
| `CharacterService.CreateAsync` (via `CreateCharacterHandler`) | `FindByNameAsync` | `characters` |
| `MailService.SendAsync` | `MailboxSizeAsync >= 16` | `mail` |

`users.display_name` has a unique index, so a lost race there fails loudly. Verify the same
for `sessions.user_id`, `clans.name` and `characters.name`; without the index, concurrent
requests create duplicate rows. The mailbox cap can be exceeded by two simultaneous senders.

**Done**: `clans.name` and `characters.name` already carried unique indexes; `sessions.user_id`
gained one, registration now catches the lost insert race and re-checks before reporting
`CONFLICT`, session creation serializes on the account row (`FOR UPDATE`), and mail delivery
serializes on the recipient's character row so the cap holds under concurrent senders.

### 9. `EncodePacket` has no payload-length guard

`src/Shared/Tcp/PacketCodecService.cs` — `EncodePacket`

The payload length is written with `(ushort)encodedPayload.Length`. Decode enforces
`PacketConstants.MaximumPayloadLength` on the way in; nothing enforces anything on the way
out, so a reply larger than 65535 bytes silently wraps the length field and desynchronizes the
connection instead of failing. No handler reaches that size today (mail bodies are 708 bytes
and letters are paged one packet each, and the flash-news message is capped at 255 characters
by `FlashNewsBroadcastRequest`), so this is a defensive gap — add an explicit throw or clamp.
**Done**: `EncodePacket` throws an `InvalidOperationException` once the encoded payload
exceeds `PacketConstants.MaximumPayloadLength`, the same bound decode enforces.

### 10. `AutomatchState.Matched` is unreachable

`src/Shared/Domain/Automatch/AutomatchMatchmaking.cs` — `ReleaseMatchAsync`

```csharp
foreach (var characterIdentifier in match.Members)
{
    if (searchers.TryGetValue(characterIdentifier, out var searcher))
    {
        searcher.State = AutomatchState.Matched;
    }

    searchers.Remove(characterIdentifier);
}
```

The state is assigned and the searcher is removed from the dictionary in the same iteration,
so nothing can ever observe `Matched`. Either keep the entry until the client has been told
about the match, or drop the assignment — as written it reads like a lost step.

**Done**: the searcher is now retired as `Matched` but kept until the reap drops it, so a
cancel that arrives after the group formed can still report `TooLate`; the formed-match
notification still comes from the match itself, not from the searcher state.

### 11. The Gameplay server account is publicly loginable

`src/GameplayServer/Identity/GameplayServerAccountService.cs`

The service creates `server` / `server` (MD5) in `users`, and the login endpoint is public,
so anyone who knows the password can log in as that account and select the character whose
identifier the peer-to-peer host identity is derived from. The password is also written to
the log in clear text at `LogInformation`. Read the credentials from configuration, and do
not log the password. **Done**: the account name, password and character name come from
`GAMEPLAY_SERVER_ACCOUNT_NAME` / `GAMEPLAY_SERVER_ACCOUNT_PASSWORD` /
`GAMEPLAY_SERVER_CHARACTER_NAME`; when no password is configured a random one is generated
per process, the password is no longer logged, and the protocol-forced MD5 is documented.

Separately, password hashing is unsalted MD5 (`CryptographyService.ComputeMd5Hex`). That is
forced by the client protocol — the client sends a hash it computed itself — but the reason
belongs in a comment so it is not "fixed" into an incompatibility later.

---

## Low — nits and future hazards

- `PacketReader.ReadBytes`/`ReadFixedString` clamp with `Math.Min(length, Remaining)`, which
  returns a negative count for a negative argument and then throws from `AsSpan`/`Slice`. All
  callers pass constants today, but the "reads past the end are clamped so a short packet
  cannot fault a handler" contract should be `Math.Max(0, …)`. **Done** in `PacketReader`.
- `PacketReader` only read unsigned values from the wire. Added `ReadInt16` in the same
  guarded style for callers that want signed 16-bit integers.
- `StringUtility.WriteFixedString` writes `(byte)value[index]`, so a character above `U+00FF`
  becomes a NUL and truncates the field early. Validate or transliterate non-Latin-1 input at
  the name rules (`IsValidName` already rejects it for characters, not for clan or lobby
  names). **Done** in `StringUtility.WriteFixedString` and `WriteFixedStringInto`: a code
  unit above U+00FF now writes a NUL instead of being widened to `(byte)`, and both writers
  normalize the out-of-range case the same way.
- `ClanService.UpdateNoticeAsync` stores the notice time as `(int)` Unix seconds (2038).
- `DatabaseInitializer.SynchronizeSequenceAsync` and `GameplayServerAccountService` call
  `setval(..., (SELECT MAX(id) …))`, which errors on an empty table. Use
  `coalesce(max(id), 1)`.
- `RoundReportService.InsertAsync` is called once per player per round (N round-trips);
  `InsertTalliesAsync` already shows the batched pattern.
- `GameService.MarkRoundPlayersAsync` builds SQL by string concatenation and runs it with
  `ExecuteSqlRawAsync`. Safe today (every value comes from the database as an `int`, as the
  comment says), but inlined parameters would keep it safe after a future edit.
- `GetFriendsBlockedListHandler.WriteEntryAsync` calls `activeGameSessions.List()` inside the
  per-entry loop and one `FindByIdAsync` per online friend → O(n²) allocation plus N+1
  queries. `ClanRosterHandlers` already snapshots the session list once; copy that.
- `TcpServerBase.ProcessAccumulatedBytesAsync` accumulates in a `List<byte>` and calls
  `RemoveRange(0, n)` per packet — O(n) shifts on every read. A `MemoryStream` with a
  consumed offset is cheaper on a busy lobby.
- `GetPersonalStatsHandler.BuildHeader` depends on hand-computed offsets, and `WritePadding`
  silently does nothing when its length is negative, so a field added or removed later would
  produce a wrong-length payload with no error. A `Debug.Assert(header.Size <= InfoSize)`
  would catch it.
- `CreateClanHandler` does not reject a caller who already belongs to a clan (only
  `ApplyToClanHandler` checks that), and does not validate the clan name, so an empty or
  reserved name can be created. `IsValidName` is only used by `CreateCharacterHandler`.
- `TrafficLogger.LogInboundPacket` indexes `buffer[0]`/`buffer[1]` with no length guard. Fine
  today (the caller only passes complete packets), fragile.
- `RoundReportService.MetPlayersAsync` reports `grouping.Max(report.LobbySubtype)` — the
  highest subtype seen in that room rather than the subtype of the most recent encounter. If
  a room is reused across lobbies the label can be wrong.
- `LobbyService.GetCached()` throws `InvalidOperationException` before the first
  `LoadCacheAsync`. Both the gate runner and the game lobby runner load the cache before
  starting the listener, so this is only reachable on a future startup-order change — but
  `GetLobbyListHandler` catching it (and `GetGameLobbyInfoHandler` not) makes the failure mode
  inconsistent.

**Done (nits):** the notice time is a `long`, `setval` uses `coalesce(MAX(id), 1)`, the
round-roster insert is parameterized, the friends list snapshots the session set and resolves
each lobby once, the accumulation buffer is a `MemoryStream` with a read offset, and
`BuildHeader`/`BuildTail` assert their fixed sizes. The personal-stats padding, `GetCached`
(now returns an empty list instead of throwing) and the inbound-log command label were fixed
too. `RoundReportService.InsertAsync` remains one insert per report frame — each frame is one
handler call, so there is nothing to batch — and the met-players subtype is still
`Max(LobbySubtype)`, because the correct "subtype of the most recent encounter" projection
does not translate to SQL (verified against the model).

---

## Test coverage

`tests/Mgo2Server.Tests` now holds four files: `PersistenceModelTests` (the EF model maps
every entity and table), `DnsMessageCodecTests`, `WireFormatTests` (frame cipher round-trip
and malformed input, handshake parsing, packet-codec round-trip and the length guard, plus
the reader clamp/signed-read and Latin-1 writer contracts) and the original suite. The DNS tests additionally cover compression-pointer cycles, labels that
run past the datagram and a missing QCLASS. That leaves the following untested:

- `LzssUtility.Decompress` — bit-level, ring buffer, bounded output.
- `FrameCryptoUtility` — scramble positions, XOR chain, tail digest (findings 1 and 2 are
  directly in this area).
- `GameplayOptionsCodec` and `HostSettingsBlobCodec` — fixed-offset codecs whose two
  directions must be changed together.
- `PacketCodecService` — encode/decode round-trip and malformed input.

The DNS tests should be verified to cover malformed queries (compression pointer cycles,
truncated labels, missing QCLASS) to ensure the crash paths are guarded.

---

## Suggested order of work

1. **UDP datagram hardening (findings 1–2).** Minimum-length check before unscrambling, a
   `HandshakeBodySize` bound in `ParseHandshakeBody`, and a `try`/`catch` per datagram so one
   bad packet can never end the host. **Done**.
2. **Authorization (findings 3–4).** Add the membership/leadership checks the clan handlers
   are missing, and the host checks the room handlers are missing.
3. **Concurrency (findings 5–7).** Make the two session collections thread-safe, remove the
   dead `TcpServerBase.Sessions`, and make sequence-number advances atomic.
4. **Data integrity (findings 8–11), then the nits.**

Low-priority nits done alongside the crash fixes: `PacketReader.ReadBytes`/
`ReadFixedString` now clamp with `Math.Max(0, …)` and `PacketReader` has a signed
`ReadInt16` in the same style; `StringUtility.WriteFixedString` and
`WriteFixedStringInto` now write a NUL for code points above U+00FF instead of widening.
