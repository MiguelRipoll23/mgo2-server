namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The naming convention the testing tools give the characters they create, and
/// the one question every part of the event subsystem asks about it.
/// <para>
/// A test character is an ordinary row: it has an account, an identifier from
/// the same sequence as every other character, and no separate range or flag.
/// The only thing that marks it is its name, which starts with
/// <see cref="Prefix"/> followed by a number. That makes the marker visible to a
/// person reading a roster, and it keeps the tools from mistaking a real player
/// for one of theirs.
/// </para>
/// <para>
/// The name is load-bearing well beyond the tools that write it: the matchmaking
/// readiness rule reads it to decide whether a member's decision byte matters, a
/// pair listing reads it to label a side, and the member-state tool reads it to
/// decide whose byte it may move. Three readers with three reasons to disagree
/// is three chances to get the marker subtly wrong, so the prefix and the test
/// of it live here together.
/// </para>
/// </summary>
public static class TestPlayerNameUtils
{
    /// <summary>Prefix every character the testing tools create is named with.</summary>
    public const string Prefix = "server-";

    /// <summary>
    /// Most test characters one pool operation may add, so a mistyped count
    /// cannot fill the characters table in one request.
    /// </summary>
    public const int MaximumPerAdd = 32;

    /// <summary>Whether a character name is one the testing tools made.</summary>
    /// <param name="name">Name to test.</param>
    public static bool IsTestPlayer(string? name) =>
        !string.IsNullOrEmpty(name)
        && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Composes the name of the numbered test character.</summary>
    /// <param name="number">Number to name the character with.</param>
    public static string ComposeName(int number) => $"{Prefix}{number}";
}
