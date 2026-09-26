using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static Mgo2Server.Http.Discord.DiscordInteractionOptionUtils;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Runs the staff command that fills an event with fake players.
/// <para>
/// A Survival lobby with one player in it cannot be tested: the team forms, it
/// enters, and then it waits for an opponent that nobody is going to join. This
/// command is how a moderator puts enough players in the lobby for that to
/// actually happen.
/// </para>
/// <para>
/// The players are not real accounts. They are held in the lobby's memory, they
/// have no row in the accounts table, and they are gone the moment the lobby
/// restarts — a fake player that outlived the process that made it would be a
/// real player nobody can log in as. The team they form is a real row, because
/// matchmaking pairs teams and the pairing is what a real client has to be
/// able to see.
/// </para>
/// </summary>
/// <param name="responder">Service that answers the interactions.</param>
/// <param name="dispatch">Service that carries the request to the lobby.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordFakePlayerCommandService(
    IDiscordInteractionResponder responder,
    FakePlayerDispatchService dispatch,
    IOptions<DiscordOptions> options,
    ILogger<DiscordFakePlayerCommandService> logger)
{
    /// <summary>Interaction of a command a member used.</summary>
    private const int ApplicationCommandInteractionType = 2;

    /// <summary>Option naming the lobby.</summary>
    private const string ModeOptionName = "mode";

    /// <summary>Option naming how many players to add.</summary>
    private const string CountOptionName = "count";

    /// <summary>Option naming the team the players are put in.</summary>
    private const string TeamOptionName = "team";

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
                DiscordOptions.FakePlayerCommandName);
            return;
        }

        var roles = interaction.Member?.Roles ?? [];
        if (!options.AllowsStaffCommand(roles))
        {
            logger.LogWarning(
                "A member without the moderator or manager role used the {Command} command",
                DiscordOptions.FakePlayerCommandName);
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

        // The count is bounded by the team rather than by the option: a request
        // for more players than a roster holds would be quietly truncated, and a
        // moderator who asked for eight and got six would be looking for two
        // that were never created.
        var count = BoundedNumber(interaction, CountOptionName, 1, FakePlayerDispatchService.MaximumCount);
        if (count is null)
        {
            return $"Say how many players to add, from 1 to {FakePlayerDispatchService.MaximumCount}.";
        }

        var teamName = Text(interaction, TeamOptionName)?.Trim() ?? string.Empty;
        var result = await dispatch.DispatchAsync(mode, count.Value, teamName, string.Empty, cancellationToken);

        return result.Outcome switch
        {
            FakePlayerDispatchService.DispatchOutcome.Sent =>
                $"Adding {count} fake {(count == 1 ? "player" : "players")} to "
                + $"{EventScheduleService.ModeName(mode)}"
                + (string.IsNullOrWhiteSpace(teamName) ? "." : $" as **{teamName}**."),
            FakePlayerDispatchService.DispatchOutcome.InvalidCount =>
                $"Ask for between 1 and {FakePlayerDispatchService.MaximumCount} players.",
            FakePlayerDispatchService.DispatchOutcome.NoSuchLobby =>
                $"There is no {EventScheduleService.ModeName(mode)} lobby running.",
            _ =>
                $"The {EventScheduleService.ModeName(mode)} lobby is not connected, so nobody was added. "
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
    public static bool IsFakePlayerCommand(Contracts.DiscordInteraction interaction) =>
        interaction.Type == ApplicationCommandInteractionType &&
        string.Equals(
            interaction.Data?.Name,
            DiscordOptions.FakePlayerCommandName,
            StringComparison.OrdinalIgnoreCase);
}
