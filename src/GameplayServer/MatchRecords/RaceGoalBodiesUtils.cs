using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The operations stream's Race goal update: <c>12 &lt;flags&gt; &lt;goal&gt;
/// &lt;spawn&gt;</c>, the record that moves a team's target to its next goal.
/// </summary>
/// <remarks>
/// <para>
/// <b>The flags are the rule's own state.</b> Bit 7 is set when the team scored
/// and clear when its target was reset; bit 0 is the team. The two bytes behind
/// them are point numbers: the goal the team now has and the spawn point it
/// uses. The replay parser measures the spawn as the goal the previous record
/// gave that team, on all 14 scores and 14 resets it reads.
/// </para>
/// <para>
/// <b>The scorer is not in this body.</b> The slot comes from the objective feed
/// written in the same tick (<c>04 12 &lt;slot&gt;</c>), which is why the
/// checkpoint count is credited by <see cref="ObjectiveFeedBodiesUtils"/> and
/// this record is decoded for the goal and the team it moves.
/// </para>
/// </remarks>
public static class RaceGoalBodiesUtils
{
    /// <summary>Opcode of the Race goal update on the operations stream.</summary>
    public const byte Opcode = (byte)OperationsOpcode.RaceGoal;

    /// <summary>Shortest body that carries the flags and the two point numbers.</summary>
    public const int BodyLength = 4;

    /// <summary>Flag bit set when the team scored; clear when its target was reset.</summary>
    public const byte ScoredFlag = 0x80;

    /// <summary>Flag bit naming the team.</summary>
    public const byte TeamMask = 0x01;

    /// <summary>
    /// Whether a body is a Race goal update: the opcode and the four bytes the
    /// flags, goal and spawn need.
    /// </summary>
    /// <param name="body">Body of an inbound operations record.</param>
    /// <returns><c>true</c> when the body is a Race goal update.</returns>
    public static bool IsRaceGoal(ReadOnlySpan<byte> body) =>
        body.Length >= BodyLength && body[0] == Opcode;

    /// <summary>Whether the record says the team scored.</summary>
    /// <param name="body">The Race goal body.</param>
    /// <returns><c>true</c> when the scored flag is set.</returns>
    public static bool ScoredOf(ReadOnlySpan<byte> body) => (body[1] & ScoredFlag) != 0;

    /// <summary>The team the goal belongs to.</summary>
    /// <param name="body">The Race goal body.</param>
    /// <returns>Zero or one, the flag's team bit.</returns>
    public static byte TeamOf(ReadOnlySpan<byte> body) => (byte)(body[1] & TeamMask);

    /// <summary>The goal the team now has.</summary>
    /// <param name="body">The Race goal body.</param>
    /// <returns>The goal's point number.</returns>
    public static byte GoalOf(ReadOnlySpan<byte> body) => body[2];

    /// <summary>The spawn point the team uses.</summary>
    /// <param name="body">The Race goal body.</param>
    /// <returns>The spawn's point number.</returns>
    public static byte SpawnOf(ReadOnlySpan<byte> body) => body[3];
}
