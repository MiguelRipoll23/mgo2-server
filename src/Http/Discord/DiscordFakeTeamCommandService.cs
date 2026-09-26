using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static Mgo2Server.Http.Discord.DiscordInteractionOptionUtils;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Runs the staff command that creates a team which exists only in a lobby's
/// memory.
/// <para>
/// A team list or a roster screen cannot be tested without a team to look at,
/// and forming one by hand takes a real client and real players. This command
/// creates the team in the lobby itself: it is listed, it can be filled by
/// fake-player, and it is gone when the lobby restarts, so nothing a moderator
/// makes here can be mistaken for a team somebody formed.
/// </para>
/// </summary>
/// <param name="responder">Service that answers the interactions.</param>
/// <param name="dispatch">Service that carries the request to the lobby.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordFakeTeamCommandService(
    IDiscordInteractionResponder responder,
    FakeTeamDispatchService dispatch,
    IOptions<DiscordOptions> options,
    ILogger<DiscordFakeTeamCommandService> logger)
{
    /// <summary>Interaction of a command a member used.</summary>
    private const int ApplicationCommandInteractionType = 2;

    /// <summary>Option naming the lobby.</summary>
    private const string ModeOptionName = "mode";

    /// <summary>Option naming how many players the team holds.</summary>
    private const string CountOptionName = "count";

    /// <summary>Option naming the team.</summary>
    private const string TeamOptionName = "team";

    /// <summary>Option naming the players.</summary>
    private const string PrefixOptionName = "prefix";

    /// <summary>Players a team is created with when none are asked for.</summary>
    private const int DefaultCount = 1;

    private readonly DiscordOptions options = options.Value;

    /// <summary>Handles one interaction of the command.</summary>
    /// <param name="interaction">Interaction to answer.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var interactionIdentifier = interaction.Identifier;
        var interactionToken = interaction.Token;
        if (string.IsNullOrWhiteSpace(interactionIdentifier) || string.IsNullOrWhiteSpace(interactionToken))
        {
            logger.LogWarning(
                "The {Command} command arrived without a way to answer it",
                DiscordOptions.FakeTeamCommandName);
            return;
        }

        var roles = interaction.Member?.Roles ?? [];
        if (!options.AllowsStaffCommand(roles))
        {
            logger.LogWarning(
                "A member without the moderator or manager role used the {Command} command",
                DiscordOptions.FakeTeamCommandName);
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "You need the Moderator or Manager role to use this command.",
                cancellationToken);
            return;
        }

        var reply = await RunAsync(interaction, cancellationToken);
        await ReplyAsync(interactionIdentifier, interactionToken, reply, cancellationToken);
    }

    private async Task<string> RunAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        if (!TryReadMode(interaction, out var mode))
        {
            return "The lobby is required: Survival or Tournament.";
        }

        // A team is created with a leader even when no count is given, because a
        // roster with no slot at all is not a team any screen can show.
        var count = Number(interaction, CountOptionName) ?? DefaultCount;
        if (count < 1 || count > FakeTeamDispatchService.MaximumCount)
        {
            return $"Ask for between 1 and {FakeTeamDispatchService.MaximumCount} players, "
                + "or leave it out for a leader.";
        }

        var teamName = Text(interaction, TeamOptionName)?.Trim() ?? string.Empty;
        var prefix = Text(interaction, PrefixOptionName)?.Trim() ?? string.Empty;
        var result = await dispatch.DispatchAsync(mode, count, teamName, prefix, cancellationToken);

        var displayName = string.IsNullOrWhiteSpace(teamName) ? "a made-up name" : $"**{teamName}**";
        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent =>
                $"Creating an in-memory team called {displayName} with "
                + $"{count} {(count == 1 ? "player" : "players")} in "
                + $"{EventScheduleService.ModeName(mode)}.",
            FakeTeamDispatchService.DispatchOutcome.InvalidCount =>
                $"Ask for between 1 and {FakeTeamDispatchService.MaximumCount} players.",
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby =>
                $"There is no {EventScheduleService.ModeName(mode)} lobby running.",
            _ =>
                $"The {EventScheduleService.ModeName(mode)} lobby is not connected, so no team was created. "
                + "Try again in a moment.",
        };
    }

    /// <summary>
    /// Reads the lobby as the word the moderator chose, rather than as the
    /// number the lobby is stored under.
    /// </summary>
    private static bool TryReadMode(Contracts.DiscordInteraction interaction, out int mode)
    {
        mode = 0;
        var text = Text(interaction, ModeOptionName);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        mode = text.Trim().ToLowerInvariant() switch
        {
            "survival" => EventConstants.SurvivalSelector,
            "tournament" or "registration" => EventConstants.TournamentRegistrationSelector,
            _ => 0,
        };

        return mode != 0;
    }

    private Task ReplyAsync(
        string interactionIdentifier,
        string interactionToken,
        string message,
        CancellationToken cancellationToken) =>
        responder.RespondAsync(interactionIdentifier, interactionToken, message, cancellationToken);

    /// <summary>Reports whether the interaction carries this command.</summary>
    /// <param name="interaction">Interaction to look at.</param>
    public static bool IsFakeTeamCommand(Contracts.DiscordInteraction interaction) =>
        interaction.Type == ApplicationCommandInteractionType &&
        string.Equals(
            interaction.Data?.Name,
            DiscordOptions.FakeTeamCommandName,
            StringComparison.OrdinalIgnoreCase);
}
