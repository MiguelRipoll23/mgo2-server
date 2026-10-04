namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The stored host-settings slot the client reads and writes.
/// <para>
/// It lives beside the rows rather than beside the codec, because the service
/// that reads a character's settings has to know which of its rows is the
/// current one, and a constant it cannot name is a constant it guesses at.
/// </para>
/// </summary>
public static class HostSettingsType
{
    /// <summary>The single current settings row of a character.</summary>
    public const short Value = 0;
}
