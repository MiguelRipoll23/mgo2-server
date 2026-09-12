# Code review — MGO2 server (.NET 10)

**Scope:** whole-repository correctness pass over the tree as it stands (~213 files / 22k
LOC). Transport, protocol, crypto/codec, domain, persistence, HTTP and DNS layers were read.
No git history is available in this checkout, so there is no diff to review.

**Not verified by execution:** `dotnet` is not installed in the review environment, so the
project could not be built and the test suite could not be run. Every finding below comes
from reading code, not from a failing build or test.

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
  principle (see finding 10 for the one gap).

---

## Critical — unauthenticated remote crash

These four make a container exit rather than log an error. They are reachable by any host
that can send a UDP packet to the published ports.

### 1. DNS: compression-pointer recursion has no cycle guard

`src/Dns/DnsMessageCodec.cs` — `ParseDomainName`

A compression pointer is followed by calling `ParseDomainName` recursively with no visited
set and no depth limit. A query whose name is `c0 0c` (a pointer back to offset 12) recurses
forever. `StackOverflowException` cannot be caught, so `mgo2-dns` dies with no log line and
no chance for a graceful restart.

**Fix:** reject a pointer that has already been visited, and cap the label/pointer count (a
DNS name is at most 255 bytes / 127 labels).

### 2. DNS: label length and pointer bytes are not bounds-checked

`src/Dns/DnsMessageCodec.cs` — `ParseDomainName`

`Encoding.UTF8.GetString(data, offset, length)` is called with a label length taken from the
datagram (up to 63) that can run past the end of `data`, which throws
`ArgumentOutOfRangeException`. The same happens for `data[offset + 1]` when a `0xc0` byte is
the last byte of the datagram. `DnsServer.HandleQueryAsync` has no `try`/`catch`, and
`src/Dns/Program.cs` only catches `OperationCanceledException`, so any of these terminates
the process.

**Fix:** check `offset + length <= data.Length` before decoding, check `offset + 1 <
data.Length` before reading a pointer, and wrap per-query handling in a `try`/`catch`.

### 3. DNS: the response builder copies past the end of a truncated query

`src/Dns/DnsMessageCodec.cs` — `BuildAddressResponse`

`ParseQuery` only guarantees that the QTYPE field is present (`nextOffset + 1 <
data.Length`), but `BuildAddressResponse` assumes QCLASS follows it and computes
`questionLength = nameEnd + 4 - 12`. For a query that stops after QTYPE, `questionLength`
exceeds the datagram and `Array.Copy` throws `ArgumentException` — process-fatal through the
same unguarded path as finding 2. A query for any configured local domain (`mgo2pc.com`, …)
with the QCLASS field omitted triggers it.

**Fix:** require a complete question (name + QTYPE + QCLASS) in `ParseQuery`, and validate the
recomputed `questionLength` against the datagram length in `BuildAddressResponse`.

### 4. Dedicated host: a two-byte datagram kills the UDP host

`src/Shared/Utils/FrameCryptoUtility.cs` — `ScramblePositions`, `UnscrambleHeaderOnly`

`ScramblePositions(length)` computes `span = (uint)(length - 2)` and takes a modulo by it. For
`length == 2` that is a division by zero; for `length` 0 or 1 the derived swap indices read
outside the buffer. `HandleDatagramAsync` is awaited directly from `ReceiveLoopAsync`
(`src/GameplayServer/DedicatedHostService.cs`) with no `try`/`catch`, so
`DivideByZeroException` / `IndexOutOfRangeException` escapes `RunAsync` and ends the process.

**Fix:** require a minimum datagram length (`HeaderSize + TailSize`, matching what
`VerifyTailDigest` already assumes) before unscrambling, and wrap per-datagram handling in a
`try`/`catch`.

### 5. Dedicated host: a truncated handshake crashes the parser

`src/Shared/Utils/FrameBuilderUtility.cs` — `ParseHandshakeBody`

The guard is `body.Length < 0x10`, but the pairing loop then reads up to
`16 + pairCount * 6 + 5` (offset 27 for `pairCount == 2`). A body of, say, 17 bytes with
`pairCount = 2` throws from `body[offset]` or from
`BinaryUtilities.ReadUInt16LittleEndian`. The pre-handshake digest key is a compile-time
constant, so a forged pre-keyed frame reaches this parser from an unauthenticated sender and
kills the host through the same unguarded path as finding 4.

**Fix:** guard with `body.Length < HandshakeBodySize` (0x1c), or check `offset + 6 <=
body.Length` per pair, and add the per-datagram `try`/`catch`.

---

## High — authorization gaps

### 6. Clan write commands trust the clan identifier from the packet

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
leader check where the operation requires it.

### 7. Room mutations are not host-gated

`src/GameLobbyServer/Commands/Game/Rooms/RoomHostHandlers.cs`

`SetGameHandler`, `UpdatePingsHandler` and `StartRoundHandler` only require the caller to be
in the room. Any member can change the staged rotation entry, push arbitrary pings for other
players, and snapshot the roster (`MarkRoundPlayersAsync`) — which is the attribution check
that `HostWeaponTalliesHandler` and the round-statistics handlers rely on to decide whose
statistics count. `QuitGameHandler`, `PassRoundHandler`, `HostInGameInfoHandler` and
`RateHostHandler` all verify the host, so these three look like oversights rather than a
deliberate design.

---

## Medium — thread-safety of state shared by all connections

Every domain service is registered as a singleton, and connection handlers resolve from the
container and run concurrently on the thread pool while `PeriodicWorker`s and HTTP requests
touch the same objects.

### 8. `TcpServerBase.sessions` is an unsynchronized `Dictionary`

`src/Shared/Tcp/TcpServerBase.cs`

`HandleConnectionAsync` runs fire-and-forget from the accept loop, so
`sessions[remoteAddress] = session` and `sessions.Remove(remoteAddress)` execute
concurrently on the thread pool. A plain `Dictionary` is not safe for concurrent mutation.

The `Sessions` property that exposes the dictionary is never read anywhere in the repository
(`ActiveGameSessionsService` is the collection the command handlers actually use), so the
state is simultaneously dead and unsafe.

**Fix:** delete the dictionary and the property, or make it a `ConcurrentDictionary` if a
future reader is intended.

### 9. `LobbyTrackerService` is read and written concurrently

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
storm under connect/disconnect churn.

### 10. Session sequence numbers are advanced without synchronization

`src/Shared/Utils/SessionHelper.cs`, `src/Shared/Tcp/TcpServerBase.cs` — `SendKeepAliveAsync`

`session.SequenceOut++` is a non-atomic read-modify-write. `TcpSession.WriteAsync` serializes
the *writes*, but the sequence number is read and incremented before the lock is taken, so
the HTTP-triggered `/flash-news/broadcast` (which writes to every active session) can
interleave with that session's own handler and emit two packets carrying the same sequence
number. Use `Interlocked.Increment` and take the value inside the write lock.

---

## Medium — data integrity and correctness

### 11. Check-then-insert races on uniqueness

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

### 12. `EncodePacket` has no payload-length guard

`src/Shared/Tcp/PacketCodecService.cs` — `EncodePacket`

The payload length is written with `(ushort)encodedPayload.Length`. Decode enforces
`PacketConstants.MaximumPayloadLength` on the way in; nothing enforces anything on the way
out, so a reply larger than 65535 bytes silently wraps the length field and desynchronizes the
connection instead of failing. No handler reaches that size today (mail bodies are 708 bytes
and letters are paged one packet each, and the flash-news message is capped at 255 characters
by `FlashNewsBroadcastRequest`), so this is a defensive gap — add an explicit throw or clamp.

### 13. `AutomatchState.Matched` is unreachable

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

### 14. The dedicated host account is publicly loginable

`src/GameplayServer/Identity/DedicatedHostAccountService.cs`

The service creates `server` / `server` (MD5) in `users`, and the login endpoint is public,
so anyone who knows the password can log in as that account and select the character whose
identifier the peer-to-peer host identity is derived from. The password is also written to
the log in clear text at `LogInformation`. Read the credentials from configuration, and do
not log the password.

Separately, password hashing is unsalted MD5 (`CryptographyService.ComputeMd5Hex`). That is
forced by the client protocol — the client sends a hash it computed itself — but the reason
belongs in a comment so it is not "fixed" into an incompatibility later.

---

## Low — nits and future hazards

- `PacketReader.ReadBytes`/`ReadFixedString` clamp with `Math.Min(length, Remaining)`, which
  returns a negative count for a negative argument and then throws from `AsSpan`/`Slice`. All
  callers pass constants today, but the "reads past the end are clamped so a short packet
  cannot fault a handler" contract should be `Math.Max(0, …)`.
- `StringUtility.WriteFixedString` writes `(byte)value[index]`, so a character above `U+00FF`
  becomes a NUL and truncates the field early. Validate or transliterate non-Latin-1 input at
  the name rules (`IsValidName` already rejects it for characters, not for clan or lobby
  names).
- `ClanService.UpdateNoticeAsync` stores the notice time as `(int)` Unix seconds (2038).
- `DatabaseInitializer.SynchronizeSequenceAsync` and `DedicatedHostAccountService` call
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

---

## Test coverage

`tests/Mgo2Server.Tests` holds two files: `PersistenceModelTests` (the EF model maps every
entity and table) and `DnsMessageCodecTests`. That leaves the highest-risk code untested:

- `LzssUtility.Decompress` — bit-level, ring buffer, bounded output.
- `FrameCryptoUtility` — scramble positions, XOR chain, tail digest (findings 4 and 5 are
  directly in this area).
- `GameplayOptionsCodec` and `HostSettingsBlobCodec` — fixed-offset codecs whose two
  directions must be changed together.
- `PacketCodecService` — encode/decode round-trip and malformed input.

The DNS tests did not catch the three crash paths in the codec, which suggests they only
cover well-formed queries; adding a fuzz-style case per parser is cheap and would cover all
five crashes.

---

## Suggested order of work

1. **DNS hardening (findings 1–3).** Bounds-check names, guard pointer recursion, wrap
   per-query handling in a `try`/`catch`. One file, low risk, removes three remote crash
   vectors.
2. **UDP datagram hardening (findings 4–5).** Minimum-length check before unscrambling, a
   `HandshakeBodySize` bound in `ParseHandshakeBody`, and a `try`/`catch` per datagram so one
   bad packet can never end the host.
3. **Authorization (findings 6–7).** Add the membership/leadership checks the clan handlers
   are missing, and the host checks the room handlers are missing.
4. **Concurrency (findings 8–10).** Make the two session collections thread-safe, remove the
   dead `TcpServerBase.Sessions`, and make sequence-number advances atomic.
5. **Data integrity (findings 11–14), then the nits.**
