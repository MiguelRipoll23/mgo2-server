using Mgo2Server.Shared.Domain.Characters;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Reads the clan id a player-profile record names, from the character behind the
/// character id the peer announced.
/// </summary>
/// <remarks>
/// <para>
/// <b>The field is what a row's clan emblem hangs off.</b> A profile record carries
/// the clan's name and never its id, so the id is the server's to write — and the
/// client fetches the emblem with it, over TCP, when it joins the game
/// (<c>0x4b4a</c> by clan id, answered by a 569-byte <c>EMBD</c> block). An entry
/// left at zero therefore names no clan to ask about, and the row draws no emblem
/// even when the clan name beside it is right.
/// </para>
/// <para>
/// It is a read through the characters domain's own service rather than a query of
/// its tables, which is the only way this domain is allowed to use another's data.
/// A failed read answers zero: an emblem-less row is what a player in no clan gets,
/// and a join must not be left unanswered over one field.
/// </para>
/// </remarks>
/// <param name="characters">Service that owns the characters.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class RosterClanLookupService(
    CharacterService characters,
    ILogger<RosterClanLookupService> logger)
{
    /// <summary>
    /// The clan id a character belongs to, or zero when it is in none or the read
    /// fails.
    /// </summary>
    /// <param name="characterIdentifier">Character id the peer's handshake announced.</param>
    /// <returns>The clan id, or zero.</returns>
    public async Task<int> FindAsync(uint characterIdentifier)
    {
        if (characterIdentifier is 0 or > int.MaxValue)
        {
            return 0;
        }

        try
        {
            var clan = await characters.GetClanInformationAsync((int)characterIdentifier);
            return clan?.ClanIdentifier ?? 0;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The clan id of character {CharacterId} could not be read; its roster entry names no clan",
                characterIdentifier);
            return 0;
        }
    }
}
