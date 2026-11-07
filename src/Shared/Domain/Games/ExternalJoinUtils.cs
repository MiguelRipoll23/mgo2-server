using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// Decides whether the advice about making a game joinable from outside the network
/// is worth sending, and writes it.
/// <para>
/// Only a host inside the tailnet range needs it: every other advertised address is
/// one the players can already reach, so the line would be advice about nothing. No
/// code travels in the line, because the page it names settles which game is the
/// player's by the account they sign in with.
/// </para>
/// </summary>
public static class ExternalJoinUtils
{
    /// <summary>
    /// The line the host is shown, naming the page they sign in at to make their
    /// game reachable from outside the network.
    /// </summary>
    /// <param name="hostname">Hostname of that page, as configured.</param>
    public static string BuildHintMessage(string hostname) =>
        $"Go to {hostname.TrimEnd('/')} and sign in to make your game joinable to other players outside your network";

    /// <summary>
    /// Whether an advertised address is one of the tailnet addresses the range
    /// 100.64.0.0/10 covers. Only an address inside it makes the advice worth
    /// sending: every other address is already reachable by the players.
    /// </summary>
    /// <param name="address">Advertised address, or <c>null</c> when none is set.</param>
    public static bool IsTailnetAddress(string? address) =>
        ExternalAddressUtils.IsTailnetAddress(address);
}
