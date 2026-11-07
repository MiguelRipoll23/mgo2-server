using Mgo2Server.Shared.Utils;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The external-address half of the game service: the public address a character's
/// peer-to-peer endpoint is reachable at, as the login request states it.
/// </summary>
public sealed partial class GameService
{
    /// <summary>
    /// Longest address the connection table holds: the column is an IPv4 literal
    /// (<c>documents/protocol</c> and the join reply both carry it as four octets).
    /// </summary>
    private const int PublicAddressMaximumLength = 16;

    /// <summary>
    /// Stores the public address a player presented at the login on every
    /// connection row of the account's characters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The address is a property of where the player runs from rather than of one
    /// of its characters, so it is written to every row the account holds and not
    /// to the character the login happens to be followed by: the client names its
    /// character after the login and never states the address again.
    /// </para>
    /// <para>
    /// Only the public half of the endpoint is written. The port and the private
    /// address are the client's own report from the connection push
    /// (<see cref="SaveConnectionInformationAsync"/>), and a login says nothing
    /// about either.
    /// </para>
    /// <para>
    /// A tailnet address changes nothing, and neither does a value that is not an
    /// address or one longer than the column holds: the row keeps whatever real
    /// address it was last given (<see cref="ExternalAddressUtils.Resolve"/>).
    /// </para>
    /// </remarks>
    /// <param name="accountIdentifier">Account that signed in.</param>
    /// <param name="publicAddress">Address the login request named, or <c>null</c>.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Number of connection rows the address was written to.</returns>
    public async Task<int> UpdatePublicAddressAsync(
        int accountIdentifier,
        string? publicAddress,
        CancellationToken cancellationToken = default)
    {
        var address = ExternalAddressUtils.Resolve(publicAddress, null);
        if (address is null || address.Length > PublicAddressMaximumLength)
        {
            return 0;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Database.ExecuteSqlAsync(
            $"""
             UPDATE character_connections AS connection
             SET public_ip = {address},
                 updated_at = {DateTimeOffset.UtcNow}
             FROM characters AS character
             WHERE character.id = connection.character_id
                 AND character.account_id = {accountIdentifier}
             """,
            cancellationToken);
    }
}
