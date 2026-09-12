using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>Stores the weapon tallies the host reports for one player.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="roundReportService">Service that owns the round reports.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class HostWeaponTalliesHandler(
    GameService gameService,
    RoundReportService roundReportService,
    SessionHelper sessionHelper,
    ILogger<HostWeaponTalliesHandler> logger) : ICommandHandler
{
    /// <summary>The client's own cap on tallied weapons.</summary>
    private const int MaximumWeaponTallies = 50;

    /// <summary>Bytes one tally occupies on the wire.</summary>
    private const int WeaponTallyBytes = 7;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var game = session.GameIdentifier is { } gameIdentifier
            ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken)
            : null;

        if (game is not null && packet.Payload.Length >= 8)
        {
            var reader = new PacketReader(packet.Payload);
            var characterIdentifier = (int)reader.ReadUInt32();
            var count = (int)reader.ReadUInt32();

            if (count > MaximumWeaponTallies)
            {
                logger.LogWarning(
                    "Game {GameIdentifier}: weapon tallies for character {CharacterIdentifier} declared {Count} entries, past the client's cap of {Cap}; dropped as a mis-parse.",
                    game.Identifier,
                    characterIdentifier,
                    count,
                    MaximumWeaponTallies);
            }
            else if (packet.Payload.Length < 8 + count * WeaponTallyBytes)
            {
                logger.LogWarning(
                    "Game {GameIdentifier}: weapon tallies for character {CharacterIdentifier} declared {Count} entries but carries {Length} bytes; dropped rather than stored in part.",
                    game.Identifier,
                    characterIdentifier,
                    count,
                    packet.Payload.Length - 8);
            }
            else if (await gameService.IsInGameAsync(game.Identifier, game.HostIdentifier, characterIdentifier, cancellationToken))
            {
                var tallies = new List<WeaponTallyInput>(count);
                for (var index = 0; index < count; index++)
                {
                    var at = 8 + index * WeaponTallyBytes;
                    tallies.Add(new WeaponTallyInput(
                        game.Identifier,
                        characterIdentifier,
                        packet.Payload[at],
                        (short)BinaryUtility.ReadUInt16BigEndian(packet.Payload, at + 1),
                        (short)BinaryUtility.ReadUInt16BigEndian(packet.Payload, at + 3),
                        (short)BinaryUtility.ReadUInt16BigEndian(packet.Payload, at + 5)));
                }

                await roundReportService.InsertTalliesAsync(tallies, cancellationToken);
            }
            else
            {
                logger.LogWarning(
                    "Game {GameIdentifier}: weapon tallies for character {CharacterIdentifier}, who neither is in the game nor played the round; dropped.",
                    game.Identifier,
                    characterIdentifier);
            }
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.HostWeaponTalliesResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}
