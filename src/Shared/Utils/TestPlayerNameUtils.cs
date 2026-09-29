namespace Mgo2Server.Shared.Utils;

/// <summary>
/// The naming convention the in-memory test characters are given, and the one
/// question asked about it.
/// <para>
/// An in-memory test character is not a row: it has an identifier from the test
/// range and no account behind it. The only thing that marks it is its name,
/// which starts with <see cref="Prefix"/> followed by a number, so it is
/// visible to a person reading a roster and the test players can be told from
/// real ones by name as well as by identifier.
/// </para>
/// </summary>
public static class TestPlayerNameUtils
{
    /// <summary>Prefix every in-memory test character is named with.</summary>
    public const string Prefix = "server-";

    /// <summary>Name of the in-memory team a test pairs with the sender's own.</summary>
    public const string TeamName = "server-team";

    /// <summary>Whether a character name is one the test made.</summary>
    /// <param name="name">Name to test.</param>
    public static bool IsTestPlayer(string? name) =>
        !string.IsNullOrEmpty(name)
        && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Composes the name of the numbered test character.</summary>
    /// <param name="number">Number to name the character with.</param>
    public static string ComposeName(int number) => $"{Prefix}{number}";
}
