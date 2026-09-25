namespace Mgo2Server.Shared.Constants;

/// <summary>
/// TCP command identifiers whose payloads are Blowfish-encrypted on the wire.
/// </summary>
public static class BlowfishEncryptedCommandConstants
{
    /// <summary>
    /// Command identifiers whose inbound payloads are Blowfish-encrypted.
    /// <para>
    /// <c>0x4910</c> is here on a live capture rather than on the builder scan the
    /// rest of the list came from. The scan read the client's own builder call
    /// sites, and the <c>0x49xx</c> event family was never among them because no
    /// client build served a Tournament or Survival lobby; a capture on
    /// 2026-09-25 shows the Create Team request arriving as 184 bytes of
    /// ciphertext — an 8-byte block repeating fourteen times across the comment
    /// field, which is what an all-zero plaintext looks like through this cipher.
    /// Decrypted it is a well-formed record: the name the player typed, the
    /// comment they typed, and <c>lobby_subtype</c> 4 for the Survival lobby they
    /// formed it in. Decoded as plaintext, every one of those checks fails, and the
    /// client is answered with the generic refusal instead of a team.
    /// </para>
    /// </summary>
    public static ReadOnlySpan<ushort> Inbound =>
    [
        0x3003, 0x4310, 0x4320, 0x43c0, 0x4700, 0x4990, 0x4910,
    ];

    /// <summary>Command identifiers whose outbound payloads are Blowfish-encrypted.</summary>
    public static ReadOnlySpan<ushort> Outbound =>
    [
        0x4305, 0x4349,
    ];

    /// <summary>Returns whether the payload of <paramref name="command"/> is Blowfish-encrypted.</summary>
    /// <param name="command">Command identifier to test.</param>
    public static bool IsEncrypted(ushort command) =>
        Inbound.Contains(command) || Outbound.Contains(command);
}
