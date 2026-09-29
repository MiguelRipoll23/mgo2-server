using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Holds the dedicated event host rooms that exist only in this lobby's memory,
/// and the claims a match takes on them.
/// <para>
/// A pairing does not announce itself. The match row is written when two teams
/// are queued, but the match-found packet is only pushed once a room has been
/// claimed, and a room is only claimed from one that is named for the role, says
/// it is dedicated, and is sitting idle with its host present. Those are things a
/// real host client does merely by existing, so a pairing made without one sits
/// in "paired, waiting for a host" — which reads as a pairing that never worked,
/// when in fact it paired correctly and had nowhere to go.
/// </para>
/// <para>
/// So the test makes the room here rather than writing a games row. It is
/// projected into the same <see cref="Game"/> a real room is, so everything
/// downstream — the eligibility rule, the claim and the assignment packets —
/// reads one kind of room and cannot tell them apart.
/// </para>
/// <para>
/// A real room's claim is a lease row, because the lobby that pairs the teams and
/// the gameplay server that hosts the game are separate processes. An in-memory
/// room has no row to point at and no second process to race with, so its claim
/// is held here, under the same lock the rooms are, refusing exactly the two
/// things the lease's unique indexes refuse: one room plays one match, and one
/// match plays in one room.
/// </para>
/// </summary>
public sealed class EventHostRoomMemoryService
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, Game> rooms = [];
    private readonly Dictionary<int, int> matchByRoom = [];
    private readonly Dictionary<int, EventAssignmentState> claimByMatch = [];
    private int nextRoomIdentifier = TestIdentifierUtils.FirstIdentifier;

    /// <summary>The in-memory host rooms of a lobby, in the order they were created.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    public IReadOnlyList<Game> List(int lobbyIdentifier)
    {
        lock (gate)
        {
            return [.. rooms.Values
                .Where(room => room.LobbyIdentifier == lobbyIdentifier)
                .OrderBy(room => room.Identifier)];
        }
    }

    /// <summary>Returns one in-memory room, when it is held here.</summary>
    /// <param name="gameIdentifier">Room to find.</param>
    public Game? Find(int gameIdentifier)
    {
        lock (gate)
        {
            return rooms.GetValueOrDefault(gameIdentifier);
        }
    }

    /// <summary>Whether an identifier names a room held here.</summary>
    /// <param name="gameIdentifier">Identifier to test.</param>
    public bool Holds(int gameIdentifier)
    {
        lock (gate)
        {
            return rooms.ContainsKey(gameIdentifier);
        }
    }

    /// <summary>
    /// Creates a dedicated host room of a mode. The room is sized for the largest
    /// match the eligibility rule accepts plus the dedicated host's own slot, and
    /// its roster carries the host, which is what the idle rule reads.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the room belongs to.</param>
    /// <param name="host">Test character hosting the room.</param>
    /// <param name="name">Name the room carries, which names the role it fills.</param>
    public Game CreateHostRoom(int lobbyIdentifier, Character host, string name)
    {
        ArgumentNullException.ThrowIfNull(host);

        var now = DateTimeOffset.UtcNow;

        lock (gate)
        {
            var room = new Game
            {
                // The identifier comes from the test range, so it can never
                // collide with a row's and a claim naming one is recognisable as
                // a claim on a room that exists in this process only.
                Identifier = nextRoomIdentifier++,
                HostIdentifier = host.Identifier,
                LobbyIdentifier = lobbyIdentifier,
                Name = name,
                Comment = "Created by the survival self-test.",
                MaximumPlayers =
                    EventHostEligibilityUtils.MatchPlayerCapacity
                    + EventHostEligibilityUtils.DedicatedHostPlayerSlots,
                Common = """{"dedicated":true}""",
                CreatedAt = now,
                UpdatedAt = now,
            };

            room.Players.Add(new GamePlayer
            {
                GameIdentifier = room.Identifier,
                CharacterIdentifier = host.Identifier,
                JoinedAt = now,
            });

            rooms[room.Identifier] = room;
            return room;
        }
    }

    /// <summary>Claims a room for a match.</summary>
    /// <param name="matchIdentifier">Match the claim belongs to.</param>
    /// <param name="roomIdentifier">Room being claimed.</param>
    /// <param name="activeStateIdentifier">Active state the client will correlate with.</param>
    /// <param name="sequence">Initial sequence of the active state.</param>
    /// <returns>The claim, or null when the room or the match is already claimed.</returns>
    public EventAssignmentState? TryClaim(
        int matchIdentifier,
        int roomIdentifier,
        int activeStateIdentifier,
        int sequence)
    {
        if (matchIdentifier <= 0 || roomIdentifier <= 0)
        {
            throw new ArgumentException("A claim requires both a match and a room.");
        }

        lock (gate)
        {
            if (claimByMatch.ContainsKey(matchIdentifier) || matchByRoom.ContainsKey(roomIdentifier))
            {
                return null;
            }

            var claim = new EventAssignmentState(
                activeStateIdentifier,
                sequence,
                DateTimeOffset.UtcNow,
                roomIdentifier);
            matchByRoom[roomIdentifier] = matchIdentifier;
            claimByMatch[matchIdentifier] = claim;
            return claim;
        }
    }

    /// <summary>Finds the claim a match holds.</summary>
    /// <param name="matchIdentifier">Match to look for.</param>
    public EventAssignmentState? FindClaimByMatch(int matchIdentifier)
    {
        lock (gate)
        {
            return claimByMatch.TryGetValue(matchIdentifier, out var claim) ? claim : null;
        }
    }

    /// <summary>
    /// Finds the claim a room holds, together with the match it was taken for. The
    /// match comes back beside the claim because a caller that holds a room
    /// identifier has nothing else to name the match with.
    /// </summary>
    /// <param name="gameIdentifier">Room to look for.</param>
    public (int MatchIdentifier, EventAssignmentState Claim)? FindClaimByRoom(int gameIdentifier)
    {
        lock (gate)
        {
            if (matchByRoom.TryGetValue(gameIdentifier, out var matchIdentifier)
                && claimByMatch.TryGetValue(matchIdentifier, out var claim))
            {
                return (matchIdentifier, claim);
            }

            return null;
        }
    }

    /// <summary>Returns a room to the pool by releasing its claim.</summary>
    /// <param name="roomIdentifier">Room being released.</param>
    /// <returns>Whether a claim was released.</returns>
    public bool Release(int roomIdentifier)
    {
        lock (gate)
        {
            if (!matchByRoom.Remove(roomIdentifier, out var matchIdentifier))
            {
                return false;
            }

            claimByMatch.Remove(matchIdentifier);
            return true;
        }
    }

    /// <summary>Forgets every in-memory room and claim. Each test run begins here.</summary>
    public void Reset()
    {
        lock (gate)
        {
            rooms.Clear();
            matchByRoom.Clear();
            claimByMatch.Clear();
        }
    }
}
