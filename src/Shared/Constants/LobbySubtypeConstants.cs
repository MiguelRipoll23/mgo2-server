namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Game types a lobby can be published as, as carried by the wire format.
/// <para>
/// These are the identifiers of the seeded <c>lobby_game_types</c> rows and the
/// values the client itself matches on, so a lobby's subtype is a behaviour rather
/// than a label: naming a row "Combat Training" does nothing, while giving it the
/// subtype below is what makes the client run the instructor flow in it.
/// </para>
/// </summary>
public static class LobbySubtypeConstants
{
    /// <summary>No game type.</summary>
    public const int None = 0;

    /// <summary>Free battle.</summary>
    public const int FreeBattle = 1;

    /// <summary>Automatching.</summary>
    public const int Automatching = 2;

    /// <summary>Tournament.</summary>
    public const int Tournament = 3;

    /// <summary>Survival.</summary>
    public const int Survival = 4;

    /// <summary>
    /// Survival Hosts. It is not a mode a player plays: it is the game type the
    /// dedicated Survival hosts publish under, so a host room carries a mode of
    /// its own rather than sharing the Survival one with the player lobby.
    /// </summary>
    public const int SurvivalHosts = 5;

    /// <summary>
    /// Tournament Hosts. It is the same kind of thing as
    /// <see cref="SurvivalHosts"/>: not a mode a player plays, but the game type
    /// the dedicated Tournament hosts publish under, so a host room carries a
    /// mode of its own rather than sharing the Tournament one with the player
    /// lobby.
    /// </summary>
    public const int TournamentHosts = 6;

    /// <summary>
    /// Basic training. Its sessions report nothing at all, so the only measurement
    /// of time spent in one is presence.
    /// </summary>
    public const int BasicTraining = 7;

    /// <summary>
    /// Combat training. This is the lobby the instructor flow runs in: its host is the
    /// instructor, everyone else is a student, and a session ending is what a student
    /// reviews.
    /// </summary>
    public const int CombatTraining = 8;

    /// <summary>Tournament registration.</summary>
    public const int TournamentRegistration = 10;
}
