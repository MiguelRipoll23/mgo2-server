namespace Mgo2Server.Shared.Domain.Rankings;

/// <summary>
/// The board selectors of a ranking request.
/// <para>
/// The client picks a board with <c>skey</c>, clamped to three bits, and a game
/// mode with <c>rule</c>, clamped to four bits. The player endpoint uses
/// <see cref="Score"/>, <see cref="Activeness"/>, <see cref="GradePoint"/>,
/// <see cref="HostRating"/> and <see cref="InstructorRating"/>; the clan endpoint
/// uses <see cref="Activeness"/>, <see cref="GradePoint"/> and
/// <see cref="ClanScore"/>. A key nothing can source answers with an empty board,
/// which the client renders as an unselectable row.
/// </para>
/// </summary>
public static class RankingKeys
{
    /// <summary>Score, split by game mode — the <c>rule</c> parameter selects the mode.</summary>
    public const int Score = 0;

    /// <summary>Time played. The one player row with a TOTAL/MONTH toggle.</summary>
    public const int Activeness = 2;

    /// <summary>Grade points.</summary>
    public const int GradePoint = 3;

    /// <summary>Host rating, carried as 8.8 fixed point.</summary>
    public const int HostRating = 4;

    /// <summary>Instructor rating, carried as 8.8 fixed point.</summary>
    public const int InstructorRating = 5;

    /// <summary>Clan-only key: the clan's combined score.</summary>
    public const int ClanScore = 6;

    /// <summary>Highest game mode the client can name, which is the last statistics blob.</summary>
    public const int MaximumGameMode = 10;

    /// <summary>Number of bits <c>skey</c> is clamped to, as the client clamps it.</summary>
    public const int SortKeyBits = 3;

    /// <summary>Number of bits <c>rule</c> is clamped to, as the client clamps it.</summary>
    public const int RuleBits = 4;
}
