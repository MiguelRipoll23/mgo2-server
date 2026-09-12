namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Command identifiers exchanged over the Metal Gear Online 2 TCP protocol.
/// Grouped by the server that owns them, mirroring the client's dispatch table.
/// The gameplay commands live in the companion partial file.
/// </summary>
public static partial class CommandConstants
{
    // Gate commands (0x2xxx) -------------------------------------------------

    /// <summary>Requests the lobby list from the gate server.</summary>
    public const ushort GetLobbyList = 0x2005;

    /// <summary>Opens the lobby-list reply stream.</summary>
    public const ushort GetLobbyListStart = 0x2002;

    /// <summary>Carries one lobby-list page.</summary>
    public const ushort GetLobbyListPage = 0x2003;

    /// <summary>Requests the news feed from the gate server.</summary>
    public const ushort GetNews = 0x2008;

    /// <summary>Opens the news reply stream.</summary>
    public const ushort GetNewsStart = 0x2009;

    /// <summary>Carries one news article.</summary>
    public const ushort GetNewsPage = 0x200a;

    /// <summary>Closes the news reply stream.</summary>
    public const ushort GetNewsEnd = 0x200b;

    /// <summary>Closes the lobby-list reply stream.</summary>
    public const ushort GetLobbyListEnd = 0x2004;

    // Common commands (all servers) ------------------------------------------

    /// <summary>Client-initiated disconnect; closes the session.</summary>
    public const ushort Disconnect = 0x0003;

    /// <summary>Keep-alive ping; answered with an empty keep-alive ACK.</summary>
    public const ushort KeepAlive = 0x0005;

    // Account commands (0x3xxx) ----------------------------------------------

    /// <summary>Validates the session field presented by the client.</summary>
    public const ushort CheckSession = 0x3003;

    /// <summary>Carries the result of a session check.</summary>
    public const ushort CheckSessionResult = 0x3004;

    /// <summary>Lists the characters owned by the authenticated user.</summary>
    public const ushort GetCharacterList = 0x3048;

    /// <summary>Creates a new character for the authenticated user.</summary>
    public const ushort CreateCharacter = 0x3101;

    /// <summary>Selects the character the client will play as.</summary>
    public const ushort SelectCharacter = 0x3103;

    /// <summary>Deletes one of the authenticated user's characters.</summary>
    public const ushort DeleteCharacter = 0x3105;

    /// <summary>Checks whether a character name is available.</summary>
    public const ushort CheckCharacterName = 0x3107;
}
