namespace Mgo2Server.Shared.Constants;

/// <summary>
/// TCP command identifiers whose payloads are Blowfish-encrypted on the wire.
/// </summary>
public static class BlowfishEncryptedCommandConstants
{
    /// <summary>Command identifiers whose inbound payloads are Blowfish-encrypted.</summary>
    public static ReadOnlySpan<ushort> Inbound =>
    [
        0x3003, 0x4310, 0x4320, 0x43c0, 0x4700, 0x4990,
    ];

    /// <summary>Command identifiers whose outbound payloads are Blowfish-encrypted.</summary>
    public static ReadOnlySpan<ushort> Outbound =>
    [
        0x4305,
    ];

    /// <summary>Returns whether the payload of <paramref name="command"/> is Blowfish-encrypted.</summary>
    /// <param name="command">Command identifier to test.</param>
    public static bool IsEncrypted(ushort command) =>
        Inbound.Contains(command) || Outbound.Contains(command);
}
