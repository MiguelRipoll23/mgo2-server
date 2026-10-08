namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The name prefix that marks a room as a dedicated host's own.
/// <para>
/// A gameplay server hosts without playing, and the room it publishes is named
/// <c>server-{port}</c> for that reason. The prefix is therefore reserved for
/// it: a player cannot create a room that carries it, because a room named as a
/// dedicated host is one the server itself opened and a second writer would
/// make the name say something the row does not.
/// </para>
/// <para>
/// It is a separate rule from the host-role names in
/// <c>EventHostEligibilityUtils</c> even though both are read off a room name.
/// Those say which event a room may host and are not refused; this one reserves
/// the gameplay server's room-name prefix so a player cannot claim it.
/// </para>
/// </summary>
public static class DedicatedHostNameUtils
{
    /// <summary>Prefix a room name carries when its host is a dedicated host.</summary>
    public const string DedicatedHostNamePrefix = "server";

    /// <summary>
    /// Whether a room name names a dedicated host's own room. The comparison is
    /// case-insensitive, because the gameplay server writes the prefix in lower
    /// case and a row created by an older build of it may not have.
    /// </summary>
    /// <param name="name">Name of the room.</param>
    public static bool IsDedicatedHostName(string? name) =>
        name is not null && name.StartsWith(DedicatedHostNamePrefix, StringComparison.OrdinalIgnoreCase);
}