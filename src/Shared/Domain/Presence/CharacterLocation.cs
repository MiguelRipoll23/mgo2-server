namespace Mgo2Server.Shared.Domain.Presence;

/// <summary>
/// Where a character is right now, in the shape the roster, the player search
/// and the clan roster want it: which lobby, and the room they are in when they
/// are in one.
/// <para>
/// Game membership is joined rather than stored. The room roster already records
/// which room a character is in, so this answers only which lobby; a character
/// sitting in a lobby without a room gets <see cref="GameIdentifier"/> zero.
/// </para>
/// </summary>
/// <param name="LobbyIdentifier">Identifier of the lobby the character is connected to; never zero.</param>
/// <param name="LobbyName">Name of that lobby.</param>
/// <param name="LobbySubtype">Game type of that lobby.</param>
/// <param name="GameIdentifier">Room the character is in, or zero when they are in the lobby only.</param>
/// <param name="GameName">Name of that room, or empty.</param>
public sealed record CharacterLocation(
    int LobbyIdentifier,
    string LobbyName,
    int LobbySubtype,
    int GameIdentifier,
    string GameName);
