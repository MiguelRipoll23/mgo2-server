namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// A match with the room it was leased and the two team snapshots the
/// assignment packets are written from. It is a read-time view assembled from
/// three rows rather than a fourth thing stored: the match, its lease and the
/// two teams stay authoritative, so a restart re-reads the same assignment
/// instead of reconstructing it.
/// </summary>
public sealed class EventAssignment
{
    /// <summary>Match that was assigned.</summary>
    public int MatchIdentifier { get; init; }

    /// <summary>Room the match was leased to.</summary>
    public int GameIdentifier { get; init; }

    /// <summary>Active-state identifier the client correlates its cache with.</summary>
    public int ActiveStateIdentifier { get; init; }

    /// <summary>Sequence of the active state when the lease was taken.</summary>
    public int Sequence { get; init; }

    /// <summary>
    /// Absolute base time the assignment advertises. It is the committed lease
    /// time rather than the send time, so every recipient of one assignment sees
    /// the same clock even when the packets are written at different moments.
    /// </summary>
    public int ActivationTimeSeconds { get; init; }

    /// <summary>Lobby both teams and the room belong to.</summary>
    public int LobbyIdentifier { get; init; }

    /// <summary>Game type of the lobby.</summary>
    public int LobbySubtype { get; init; }

    /// <summary>First of the paired teams.</summary>
    public EventSnapshot FirstTeam { get; init; } = new();

    /// <summary>Second of the paired teams.</summary>
    public EventSnapshot SecondTeam { get; init; } = new();

    /// <summary>Returns the team a character plays for, or null when they are not in the match.</summary>
    /// <param name="characterIdentifier">Character to look for.</param>
    public EventSnapshot? TeamOfCharacter(int characterIdentifier)
    {
        if (FirstTeam.IndexOfParticipant(characterIdentifier) >= 0)
        {
            return FirstTeam;
        }

        return SecondTeam.IndexOfParticipant(characterIdentifier) >= 0
            ? SecondTeam
            : null;
    }

    /// <summary>Returns the opposing team of one team.</summary>
    /// <param name="team">Team to find the opponent of.</param>
    public EventSnapshot OpponentOf(EventSnapshot team) =>
        ReferenceEquals(team, FirstTeam) ? SecondTeam : FirstTeam;
}
