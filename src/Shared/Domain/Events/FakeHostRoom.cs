using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// One dedicated event host room that exists only in this lobby's memory.
/// <para>
/// It is the memory twin of a <c>games</c> row, with the same shape the
/// eligibility rule reads: the name that names the role, the settings blob
/// carrying the dedicated flag, the host, the size, and the roster that says
/// the host is present. That is deliberate. A pairing does not announce itself
/// until a room has been claimed for it, so a host room that only half-existed
/// — real name, no row, or a row with no roster — is a pairing that pairs
/// correctly and then sits waiting forever. Projecting the room into the same
/// <see cref="Game"/> the real rooms are means the eligibility rule and the
/// assignment packets cannot tell which of the two they are holding.
/// </para>
/// <para>
/// Nothing about it is persisted, so a lobby restart forgets it the way it
/// forgets a fake player. A room that outlived the process that made it would
/// look exactly like a real one, and would then be a room no client is in.
/// </para>
/// </summary>
public sealed class FakeHostRoom
{
    /// <summary>
    /// Builds the room. The host is passed in rather than looked up, because
    /// choosing a character that can host is the store's decision and this is
    /// only the shape of the answer.
    /// </summary>
    /// <param name="gameIdentifier">Identifier from the fake range.</param>
    /// <param name="lobbyIdentifier">Lobby the room belongs to.</param>
    /// <param name="mode">Mode the room is dedicated to.</param>
    /// <param name="hostIdentifier">Character hosting the room.</param>
    /// <param name="hostName">Name the room carries, naming the role it fills.</param>
    public static FakeHostRoom Create(
        int gameIdentifier,
        int lobbyIdentifier,
        int mode,
        int hostIdentifier,
        string hostName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        // The room is sized for the largest match the eligibility rule accepts
        // plus the dedicated host's own slot. A room at a player's usual eight
        // would pass the capacity check and then find nowhere to seat anybody.
        var room = new Game
        {
            Identifier = gameIdentifier,
            HostIdentifier = hostIdentifier,
            LobbyIdentifier = lobbyIdentifier,
            Name = hostName,
            MaximumPlayers =
                EventHostEligibilityUtils.MatchPlayerCapacity
                + EventHostEligibilityUtils.DedicatedHostPlayerSlots,
            Comment = "Created by the event testing tools.",

            // The flag the host-eligibility rule reads. The room is dedicated to
            // one mode, which is what makes it eligible for that mode's matches
            // and nothing else.
            Common = """{"dedicated":true}""",
        };

        // The sweep requires the host to be present in the room, and the roster
        // is what says so. A character at or below zero is not a host at all:
        // the roster spells zero as "nobody here", so a row carrying it would
        // claim a host is present in an empty room, and the idle rule would read
        // that room as ready. This is the rule GameService.AddPlayerAsync
        // applies to a real room's roster.
        if (hostIdentifier > 0)
        {
            room.Players.Add(new GamePlayer
            {
                GameIdentifier = gameIdentifier,
                CharacterIdentifier = hostIdentifier,
            });
        }

        return new FakeHostRoom(room, mode, hostIdentifier);
    }

    private FakeHostRoom(Game room, int mode, int hostIdentifier)
    {
        Room = room;
        Mode = mode;
        HostIdentifier = hostIdentifier;
    }

    /// <summary>The room as the eligibility rule and the assignment read it.</summary>
    public Game Room { get; }

    /// <summary>Mode the room is dedicated to, which a match has to match.</summary>
    public int Mode { get; }

    /// <summary>Character hosting the room.</summary>
    public int HostIdentifier { get; }
}
