namespace Mgo2Server.Shared.Domain.Presence;

/// <summary>
/// The label a location block carries for the lobby a character is in, and the
/// rules that keep it apart from the other game-type labels of the protocol.
/// </summary>
public static class CharacterLocationUtils
{
    /// <summary>Highest lobby subtype the client's location table names.</summary>
    private const int MaximumLabelledSubtype = 8;

    /// <summary>
    /// The trailing byte of a location block: the game type of the lobby, which
    /// the client's search and roster rows decode through an eight-arm table
    /// (1 free battle, 2 automatching, 3 tournament, 4 survival, 5 and 6
    /// official tournament, 7 and 8 training).
    /// <para>
    /// <b>This table is not the match-history one, and the two must not be
    /// swapped.</b> The history byte is decoded through a nine-arm table that
    /// disagrees with this one at 5 and 6 and has a ninth arm this one has no
    /// name for, so reusing either helper for the other packet transfers a
    /// label between two different vocabularies.
    /// </para>
    /// <para>
    /// Out of range clamps to zero rather than to one: the client renders zero
    /// as a blank column, and a blank column is honest about not knowing where
    /// the client would print a label we do not have.
    /// </para>
    /// </summary>
    /// <param name="lobbySubtype">Game type of the lobby.</param>
    public static int LobbyLabel(int lobbySubtype) =>
        lobbySubtype is >= 1 and <= MaximumLabelledSubtype ? lobbySubtype : 0;
}
