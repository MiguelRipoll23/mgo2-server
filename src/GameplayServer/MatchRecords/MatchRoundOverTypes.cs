namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The byte the operations stream's round-over record (<c>0x0e</c>) carries.
/// </summary>
/// <remarks>
/// The image's <c>P2P_PublishOpcode0E</c> passes one of these literals; there is
/// no <c>3</c> in its callers. The code is rule-specific, so it is selected by
/// <see cref="MatchRoundOverBodiesUtils.VerdictFor"/> rather than written at a
/// call site.
/// </remarks>
public enum RoundOverVerdict : byte
{
    /// <summary>No verdict; the round closed without one of the other reasons.</summary>
    None = 0,

    /// <summary>BASE's clock ran out (<c>0x7A58D0</c>).</summary>
    BaseClock = 1,

    /// <summary>BASE's bases were all captured (<c>0x7A58D0</c>).</summary>
    BaseAllBasesCaptured = 2,

    /// <summary>The side judge's clock ran out (<c>0x762F38</c>, <c>0x764228</c>).</summary>
    Clock = 4,

    /// <summary>The side judge's ticket pool reached zero (<c>0x762F38</c>, <c>0x764228</c>).</summary>
    Tickets = 5,

    /// <summary>Side 0 took the round (<c>0x762F38</c>, <c>0x764228</c>).</summary>
    SideZero = 6,

    /// <summary>Side 1 took the round (<c>0x762F38</c>, <c>0x764228</c>).</summary>
    SideOne = 7,
}

/// <summary>
/// Why this host closed a round, before the rule decides which verdict byte that
/// reason is written as.
/// </summary>
public enum RoundOverReason
{
    /// <summary>This host has no verdict for the close.</summary>
    None,

    /// <summary>The round's clock ran out.</summary>
    Clock,

    /// <summary>A side spent its ticket pool.</summary>
    Tickets,

    /// <summary>BASE's bases were all captured.</summary>
    AllBasesCaptured,

    /// <summary>A side won the round.</summary>
    SideWin,
}
