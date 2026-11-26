namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// Chooses the side a joining player is placed on when the room assigns teams
/// itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client's picker balances by team count and breaks a tie with its LCG.</b>
/// MGO2.ELF's auto-balance picker compares the per-team counters and returns 0 or 1.
/// When the counts are equal it advances the client's shared LCG
/// (<c>state = state * 0x5d588b65 + 1</c>) and takes the new state's low bit, so a
/// tie is not decided by side order. The ELF seeds that state with <c>1</c>; this
/// server keeps its own process-wide state from the same seed, because the client
/// advances the same state with every other random call and the exact sequence
/// cannot be reproduced.
/// </para>
/// <para>
/// <b>The counts are the players the room's rows already put on each side.</b> The
/// player being assigned has no side yet and is in neither count.
/// </para>
/// </remarks>
internal static class TeamBalanceUtils
{
    private const uint LinearCongruentialMultiplier = 0x5d588b65;
    private const uint LinearCongruentialIncrement = 1;

    // The client's state holds 1 in the ELF, so the server starts from the seed the
    // game's own rand() state begins with.
    private const uint LinearCongruentialSeed = 1;

    private static readonly object StateLock = new();
    private static uint linearCongruentialState = LinearCongruentialSeed;

    /// <summary>
    /// The side holding fewer players, or the side the client's tiebreak picks when
    /// both hold the same number.
    /// </summary>
    /// <param name="firstSideCount">Players the room puts on side 0.</param>
    /// <param name="secondSideCount">Players the room puts on side 1.</param>
    /// <returns>The side to assign, 0 or 1.</returns>
    internal static short ChooseSide(int firstSideCount, int secondSideCount)
    {
        if (firstSideCount < secondSideCount)
        {
            return 0;
        }

        if (secondSideCount < firstSideCount)
        {
            return 1;
        }

        lock (StateLock)
        {
            linearCongruentialState = unchecked(
                (linearCongruentialState * LinearCongruentialMultiplier) +
                LinearCongruentialIncrement);
            return (short)(linearCongruentialState & 1);
        }
    }
}
