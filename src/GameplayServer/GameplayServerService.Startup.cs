using System.Net;
using System.Net.Sockets;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The host's own bootstrap: the account's character, the clan its roster entry
/// names, and the settings row the room is published from.
/// </summary>
/// <remarks>
/// Split from <see cref="GameplayServerService"/> so the file that runs the socket
/// stays inside the size a source file is allowed. The order of the three writes is
/// the room's and is stated where they are written.
/// </remarks>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Creates the host's character if it does not exist, names the clan its roster
    /// entry carries, and restates the settings row a browsing client's game details
    /// are answered from.
    /// </summary>
    /// <param name="cancellationToken">Token that stops the host.</param>
    private async Task EnsureHostCharacterAsync(CancellationToken cancellationToken) =>
        await StartupUtils.RetryAsync(
            "publish the host account and character settings",
            async token =>
            {
                var characterIdentifier = await accountService.GetOrCreateCharacterIdentifierAsync(token);
                hostIdentity.SetCharacterIdentifier(characterIdentifier);

                // The host's own roster entry names its clan so a peer can fetch the
                // emblem for it: the client asks for the block by clan id, and an
                // entry at zero names no clan to ask about.
                var clan = await characterService.GetClanInformationAsync(characterIdentifier, token);
                hostIdentity.SetClanIdentifier(clan?.ClanIdentifier ?? 0);

                // The row is written whole, as a host with no earlier row would be: it
                // is what a browsing client's details name and where a joining client
                // reads the rotation it loads, so the values it is given are logged
                // here rather than left to be compared by eye later.
                var settings = MatchSettingsUtils.HostSettings(options);
                await characterService.EnsureDedicatedHostSettingsAsync(
                    characterIdentifier,
                    settings,
                    token);
                if (await characterService.EnsureDedicatedHostAppearanceAsync(characterIdentifier, token))
                {
                    logger.LogInformation(
                        "[{LogPrefix}] Host character {CharacterIdentifier} had no appearance row; the reference face was written",
                        LogPrefix, characterIdentifier);
                }

                logger.LogInformation(
                    "[{LogPrefix}] Host settings row restated for character {CharacterIdentifier}: a "
                    + "{BriefingTime}-minute briefing, {RoundCount} round(s), maps {Maps}",
                    LogPrefix,
                    characterIdentifier,
                    settings.BriefingTime,
                    settings.RotationMaps?.Length ?? 0,
                    string.Join(",", settings.RotationMaps ?? []));
            },
            logger,
            cancellationToken);
}
