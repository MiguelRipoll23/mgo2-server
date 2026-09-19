namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The lifetime statistics of a character, derived from its round reports at read
/// time. There is no accumulator table: every figure here is a sum over the stored
/// reports, which is what the reference server does and the reason a newly
/// identified counter becomes readable without a replay of history.
/// </summary>
public sealed class CharacterStatistics
{
    /// <summary>Number of game modes the client's matrix and title rules address.</summary>
    public const int ModeCount = 11;

    /// <summary>Rounds played.</summary>
    public int Rounds { get; init; }

    /// <summary>Rounds won.</summary>
    public int Wins { get; init; }

    /// <summary>Kills.</summary>
    public int Kills { get; init; }

    /// <summary>Deaths.</summary>
    public int Deaths { get; init; }

    /// <summary>Stuns delivered.</summary>
    public int Stuns { get; init; }

    /// <summary>Stuns received.</summary>
    public int StunsReceived { get; init; }

    /// <summary>Headshot kills.</summary>
    public int HeadshotKills { get; init; }

    /// <summary>Headshot deaths.</summary>
    public int HeadshotDeaths { get; init; }

    /// <summary>Lock-on kills.</summary>
    public int LockKills { get; init; }

    /// <summary>Knife kills.</summary>
    public int KnifeKills { get; init; }

    /// <summary>Knife stuns.</summary>
    public int KnifeStuns { get; init; }

    /// <summary>Close-quarters-combat holds applied.</summary>
    public int CqcGiven { get; init; }

    /// <summary>Times a box was used.</summary>
    public int BoxUses { get; init; }

    /// <summary>Scans performed.</summary>
    public int Scans { get; init; }

    /// <summary>Times trapped.</summary>
    public int Trapped { get; init; }

    /// <summary>Total seconds played.</summary>
    public int TotalTime { get; init; }

    /// <summary>Seconds spent with evasion gear.</summary>
    public int EvasionTime { get; init; }

    /// <summary>Times spotted while sneaking.</summary>
    public int SnakeSpotted { get; init; }

    /// <summary>Enemies spotted.</summary>
    public int Spotted { get; init; }

    /// <summary>Rolls performed.</summary>
    public int Rolls { get; init; }

    /// <summary>Bases captured.</summary>
    public int BasesCaptured { get; init; }

    /// <summary>Times the character withdrew.</summary>
    public int Withdrawals { get; init; }

    /// <summary>Hold-ups performed in snake mode.</summary>
    public int SnakeHoldups { get; init; }

    /// <summary>Teammates woken up.</summary>
    public int Wakeups { get; init; }

    /// <summary>Score earned.</summary>
    public int Score { get; init; }

    /// <summary>Per-mode tallies, indexed by the game mode the round was played under.</summary>
    public ModeStatistics[] Modes { get; init; } = CreateEmptyModes();

    /// <summary>Returns the tally of a game mode, empty when the mode is out of range.</summary>
    /// <param name="mode">Game mode index, as carried by the wire format.</param>
    public ModeStatistics ForMode(int mode) =>
        mode >= 0 && mode < Modes.Length ? Modes[mode] : Modes[0];

    /// <summary>Creates one empty tally per game mode.</summary>
    public static ModeStatistics[] CreateEmptyModes()
    {
        var modes = new ModeStatistics[ModeCount];
        for (var mode = 0; mode < modes.Length; mode++)
        {
            modes[mode] = new ModeStatistics();
        }

        return modes;
    }
}
