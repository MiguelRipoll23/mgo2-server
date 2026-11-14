namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// How far a bot's aim point wanders from the spot it aims at.
/// </summary>
/// <remarks>
/// <b>The recorded host's shots are not the same round twice.</b> Its victims take
/// hits across the body rather than on one point (<c>BotShotUtils</c> names the
/// point a hit is drawn at), and a bot that wrote the same three coordinates every
/// beat was the perfect shot no capture shows. The aim body carries tenths of a
/// unit (<c>BotActorRecordUtils.BuildAimBody</c>), so these offsets are whole
/// units: about a metre across and a torso tall.
/// </remarks>
public static class BotAimJitterUtils
{
    /// <summary>Largest horizontal miss, in units.</summary>
    public const float HorizontalUnits = 900f;

    /// <summary>Largest vertical miss, in units, above or below the aim point.</summary>
    public const float VerticalUnits = 400f;

    /// <summary>One beat's horizontal offset.</summary>
    /// <returns>A value within <see cref="HorizontalUnits"/> of the aim point.</returns>
    public static float Horizontal() => (Random.Shared.NextSingle() * 2f - 1f) * HorizontalUnits;

    /// <summary>One beat's vertical offset.</summary>
    /// <returns>A value within <see cref="VerticalUnits"/> of the aim point.</returns>
    public static float Vertical() => (Random.Shared.NextSingle() * 2f - 1f) * VerticalUnits;
}
