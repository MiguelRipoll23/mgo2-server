namespace Mgo2Server.Shared.Utils;

/// <summary>
/// The identifier range every in-memory test entity is given, and the one
/// question the rest of the server asks about it.
/// <para>
/// A test team, test character and test host room have no row behind them, so
/// the only thing that tells one apart from a real one is the number it is
/// handed. That makes the range load-bearing well beyond the test that creates
/// it: the team store asks it to route a lookup, the queue asks it to decide
/// whose decision byte matters, and the claim layer asks it to decide where a
/// room is claimed. Putting the range and the test of it here keeps those
/// readers from each drawing the boundary for themselves.
/// </para>
/// <para>
/// It sits far above any identifier a database sequence reaches, so an
/// in-memory identifier is never mistaken for a row's and never collides with
/// one. It is positive on purpose: the client resolves a roster entry by its
/// identifier, and a synthetic negative value has no meaning to it.
/// </para>
/// </summary>
public static class TestIdentifierUtils
{
    /// <summary>First identifier an in-memory team, character or room is given.</summary>
    public const int FirstIdentifier = 1_000_000_000;

    /// <summary>Whether an identifier names an in-memory test entity.</summary>
    /// <param name="identifier">Identifier to test.</param>
    public static bool IsTest(int identifier) => identifier >= FirstIdentifier;
}
