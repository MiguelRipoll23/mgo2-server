namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The hash a stage's effect names are stored under.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is Haven's hash, ported rather than guessed.</b>
/// <c>.gametools/Haven/HavenStudio/Utils/Utils.cs</c> implements it over the
/// <c>PRP_*</c> strings a stage's dictionary holds — rotate the accumulator left by
/// five bits, add the character's code, keep twenty-four bits — and the values
/// here are checked against it, so a marker a stage ships can be found by name.
/// </para>
/// <para>
/// The markers this server reads are the ones Haven reads too
/// (<c>Services/MinimapSpawns.cs</c>): the two corners of the stage's box, its
/// centre, and the spawn placements the stages name.
/// </para>
/// </remarks>
public static class StageNameHashUtils
{
    /// <summary>Bits the hash keeps.</summary>
    private const uint HashMask = 0x00FFFFFF;

    /// <summary>First corner of the stage's own bounds.</summary>
    public static readonly uint StageSizeOne = Hash("PRP_STAGE_SIZE_01");

    /// <summary>Second corner of the stage's own bounds.</summary>
    public static readonly uint StageSizeTwo = Hash("PRP_STAGE_SIZE_02");

    /// <summary>The stage's centre marker.</summary>
    public static readonly uint StageCenter = Hash("PRP_STAGE_CENTER");

    /// <summary>A team's centre spawn placement.</summary>
    public static readonly uint CenterSpawn = Hash("PRP_CENTER_SPAWN");

    /// <summary>A team's corner spawn placement.</summary>
    public static readonly uint CornerSpawn = Hash("PRP_CORNER_SPAWN");

    /// <summary>
    /// The hash a <c>PRP_*</c> name is stored under.
    /// </summary>
    /// <param name="name">The name, such as <c>PRP_STAGE_CENTER</c>.</param>
    /// <returns>The twenty-four-bit hash.</returns>
    public static uint Hash(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var hash = 0u;
        foreach (var character in name)
        {
            hash = ((hash >> 19) | (hash << 5)) + character;
            hash &= HashMask;
        }

        return hash;
    }

    /// <summary>Whether a hash names a placement a bot can stand on.</summary>
    /// <param name="hash">Name hash from a stage effect.</param>
    /// <returns><c>true</c> for one of the spawn markers.</returns>
    public static bool IsSpawn(uint hash) => hash is var value &&
        (value == CenterSpawn || value == CornerSpawn);

    /// <summary>Whether a hash names one of the stage's own bounds markers.</summary>
    /// <param name="hash">Name hash from a stage effect.</param>
    /// <returns><c>true</c> for one of the bounds markers.</returns>
    public static bool IsBounds(uint hash) => hash is var value &&
        (value == StageSizeOne || value == StageSizeTwo || value == StageCenter);
}
