using System.Text.Json;
using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Runs the staff event scheduling command.
/// <para>
/// The command is the moderators' way to publish an event without an operator
/// opening the database, which is the same reason the API exposes the schedules:
/// a schedule is data somebody has to be able to write, and a table nothing can
/// write is a table that drifts.
/// </para>
/// <para>
/// It is a separate service from the message commands because it is the only one
/// that writes: everything it does goes through the same service the API uses, so
/// the two surfaces cannot disagree about what a schedule means.
/// </para>
/// </summary>
/// <param name="responder">Service that answers the interactions.</param>
/// <param name="scheduleService">Service that owns the schedules.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordEventScheduleCommandService(
    IDiscordInteractionResponder responder,
    EventScheduleService scheduleService,
    IOptions<DiscordOptions> options,
    ILogger<DiscordEventScheduleCommandService> logger)
{
    /// <summary>Interaction of a command a member used.</summary>
    private const int ApplicationCommandInteractionType = 2;

    /// <summary>Option naming what the command should do.</summary>
    private const string ActionOptionName = "action";

    /// <summary>Option naming the event.</summary>
    private const string EventOptionName = "event";

    /// <summary>Option naming the lobby mode.</summary>
    private const string ModeOptionName = "mode";

    /// <summary>Option naming the field size.</summary>
    private const string TeamsOptionName = "teams";

    /// <summary>Option naming the opening moment of the window.</summary>
    private const string FromOptionName = "from";

    /// <summary>Option naming the closing moment of the window.</summary>
    private const string UntilOptionName = "until";

    /// <summary>Option naming whether the event is published at all.</summary>
    private const string EnabledOptionName = "enabled";

    private readonly DiscordOptions options = options.Value;

    /// <summary>Handles one interaction of the scheduling command.</summary>
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
                DiscordOptions.ScheduleCommandName);
            return;
        }

        var roles = interaction.Member?.Roles ?? [];
        if (!options.AllowsStaffCommand(roles))
        {
            logger.LogWarning(
                "A member without the moderator or manager role used the {Command} command",
                DiscordOptions.ScheduleCommandName);
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "You need the Moderator or Manager role to use this command.",
                cancellationToken);
            return;
        }

        var action = TextOption(interaction, ActionOptionName);
        var reply = action?.ToLowerInvariant() switch
        {
            "list" => await ListAsync(cancellationToken),
            "create" => await CreateAsync(interaction, cancellationToken),
            "update" => await UpdateAsync(interaction, cancellationToken),
            "withdraw" => await WithdrawAsync(interaction, cancellationToken),
            _ => "The action must be list, create, update or withdraw.",
        };

        if (reply.StartsWith("Scheduled", StringComparison.Ordinal)
            || reply.StartsWith("Updated", StringComparison.Ordinal)
            || reply.StartsWith("Withdrew", StringComparison.Ordinal))
        {
            logger.LogInformation(
                "The {Command} command ran a {Action} action",
                DiscordOptions.ScheduleCommandName,
                action);
        }

        await ReplyAsync(interactionIdentifier, interactionToken, reply, cancellationToken);
    }

    private async Task<string> ListAsync(CancellationToken cancellationToken)
    {
        var schedules = await scheduleService.ListAllAsync(cancellationToken);
        if (schedules.Count == 0)
        {
            // An empty table is the normal state of a deployment that has not
            // scheduled anything, so it is reported as a fact rather than as a
            // failure the moderator has to interpret.
            return "No events are scheduled.";
        }

        var lines = new List<string>(schedules.Count);
        foreach (var schedule in schedules)
        {
            lines.Add(
                $"`{schedule.Identifier}` mode {schedule.LobbySubtype} "
                + $"{(schedule.Enabled ? "published" : "unpublished")}, "
                + $"{EventScheduleService.TeamCapacityOf(schedule)} teams, "
                + $"window {schedule.PublishStart}..{schedule.PublishEnd}");
        }

        return string.Join('\n', lines);
    }

    private async Task<string> CreateAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var mode = IntOption(interaction, ModeOptionName);
        if (mode is null || !EventConstants.IsEventSelector(mode.Value))
        {
            return "The mode is required: 4 Survival, 3 Tournament or 10 registration.";
        }

        try
        {
            var schedule = await scheduleService.ScheduleAsync(
                mode.Value,
                IntOption(interaction, TeamsOptionName) ?? EventConstants.BracketMaximumEntrants,
                NumberOption(interaction, FromOptionName) ?? 0,
                NumberOption(interaction, UntilOptionName) ?? 0,
                FlagOption(interaction, EnabledOptionName) ?? true,
                cancellationToken);

            logger.LogInformation(
                "Event {EventIdentifier} was scheduled in mode {Mode}",
                schedule.Identifier,
                mode.Value);
            return $"Scheduled event {schedule.Identifier} in mode {mode.Value}.";
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    private async Task<string> UpdateAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var eventIdentifier = IntOption(interaction, EventOptionName);
        if (eventIdentifier is null)
        {
            return "The event option is required.";
        }

        // A change states the whole window, so the parts the moderator left out
        // are read from the schedule rather than defaulted: a default would
        // silently close an event somebody meant to leave running.
        var existing = await scheduleService.FindAsync(eventIdentifier.Value, cancellationToken);
        if (existing is null)
        {
            return $"There is no event {eventIdentifier.Value}.";
        }

        var outcome = await scheduleService.UpdateAsync(
            eventIdentifier.Value,
            FlagOption(interaction, EnabledOptionName) ?? existing.Enabled,
            NumberOption(interaction, FromOptionName) ?? existing.PublishStart,
            NumberOption(interaction, UntilOptionName) ?? existing.PublishEnd,
            IntOption(interaction, TeamsOptionName) ?? EventScheduleService.TeamCapacityOf(existing),
            cancellationToken);

        return outcome switch
        {
            ScheduleWriteOutcome.Written => $"Updated event {eventIdentifier.Value}.",
            ScheduleWriteOutcome.InvalidWindow =>
                "The window must close after it opens, or state 0 for an open-ended event.",
            _ => $"There is no event {eventIdentifier.Value}.",
        };
    }

    private async Task<string> WithdrawAsync(
        Contracts.DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var eventIdentifier = IntOption(interaction, EventOptionName);
        if (eventIdentifier is null)
        {
            return "The event option is required.";
        }

        return await scheduleService.WithdrawAsync(eventIdentifier.Value, cancellationToken)
            ? $"Withdrew event {eventIdentifier.Value}."
            : $"There is no event {eventIdentifier.Value}.";
    }

    private static string? TextOption(Contracts.DiscordInteraction interaction, string name) =>
        RawOption(interaction, name) is { ValueKind: JsonValueKind.String } option
            ? option.GetString()
            : null;

    /// <summary>
    /// Reads an option Discord declares as an integer. Discord sends integers
    /// as JSON numbers, so a window's epoch second arrives the same way a team
    /// count does; the two are read apart because the one is bounded and the
    /// other is not.
    /// </summary>
    private static long? NumberOption(Contracts.DiscordInteraction interaction, string name)
    {
        var value = RawOption(interaction, name);
        return value is { ValueKind: JsonValueKind.Number } number && number.TryGetInt64(out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Reads a numeric option that has to fit an identifier or a count.</summary>
    private static int? IntOption(Contracts.DiscordInteraction interaction, string name)
    {
        var value = NumberOption(interaction, name);
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    private static bool? FlagOption(Contracts.DiscordInteraction interaction, string name)
    {
        var value = RawOption(interaction, name);
        return value?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static JsonElement? RawOption(Contracts.DiscordInteraction interaction, string name) =>
        interaction.Data?.Options?
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                name,
                StringComparison.OrdinalIgnoreCase))?
            .Value;

    private Task ReplyAsync(
        string interactionIdentifier,
        string interactionToken,
        string message,
        CancellationToken cancellationToken) =>
        responder.RespondAsync(interactionIdentifier, interactionToken, message, cancellationToken);

    /// <summary>Reports whether the interaction carries this command.</summary>
    /// <param name="interaction">Interaction to look at.</param>
    public static bool IsEventCommand(Contracts.DiscordInteraction interaction) =>
        interaction.Type == ApplicationCommandInteractionType &&
        string.Equals(
            interaction.Data?.Name,
            DiscordOptions.ScheduleCommandName,
            StringComparison.OrdinalIgnoreCase);
}
