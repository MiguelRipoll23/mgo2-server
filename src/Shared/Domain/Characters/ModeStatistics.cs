namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The per-mode tally of a character, summed from its round reports. Property
/// order is fixed by the wire format, so the payload builders stay byte-identical
/// to what the client expects.
/// </summary>
public sealed class ModeStatistics
{
    /// <summary>Rounds won.</summary>
    public int Wins { get; set; }

    /// <summary>Rounds played.</summary>
    public int Rounds { get; set; }

    /// <summary>Score earned.</summary>
    public int Score { get; set; }

    /// <summary>Seconds played.</summary>
    public int Time { get; set; }

    /// <summary>Kills.</summary>
    public int Kills { get; set; }

    /// <summary>Deaths.</summary>
    public int Deaths { get; set; }

    /// <summary>Stuns delivered.</summary>
    public int Stuns { get; set; }

    /// <summary>Stuns received.</summary>
    public int StunsRec { get; set; }

    /// <summary>Headshot kills.</summary>
    public int HsKills { get; set; }

    /// <summary>Headshot deaths.</summary>
    public int HsDeaths { get; set; }

    /// <summary>Headshot stuns delivered.</summary>
    public int HsStuns { get; set; }

    /// <summary>Headshot stuns received.</summary>
    public int HsStunsRec { get; set; }

    /// <summary>Lock-on kills.</summary>
    public int LockKills { get; set; }

    /// <summary>Lock-on deaths.</summary>
    public int LockDeaths { get; set; }

    /// <summary>Lock-on stuns delivered.</summary>
    public int LockStuns { get; set; }

    /// <summary>Lock-on stuns received.</summary>
    public int LockStunsRec { get; set; }
}
