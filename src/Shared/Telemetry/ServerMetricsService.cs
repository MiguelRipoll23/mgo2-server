using System.Diagnostics.Metrics;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Telemetry;

/// <summary>
/// Publishes the server totals over OpenTelemetry. The instruments follow the
/// metric conventions: names are lower case with underscores, the unit is a
/// separate field, and the dimension a dashboard filters on is an attribute
/// rather than part of the name, so total_players and total_matches each carry
/// the lobby they belong to. Every metric is recorded when its value changes
/// rather than on a timer, so nothing polls the database. The instruments are
/// no-ops while no collector is listening, which is what makes the reports
/// harmless on a server started without telemetry.
/// </summary>
public sealed class ServerMetricsService : IDisposable
{
    /// <summary>Name of the meter every server publishes its metrics under.</summary>
    public const string MeterName = "Mgo2Server";

    /// <summary>Name of the metric that carries the number of accounts.</summary>
    public const string TotalUsersMetricName = "total_users";

    /// <summary>Name of the metric that carries the players of a lobby.</summary>
    public const string TotalPlayersMetricName = "total_players";

    /// <summary>Name of the metric that carries the matches of a lobby.</summary>
    public const string TotalMatchesMetricName = "total_matches";

    /// <summary>Name of the attribute that carries the lobby of a total.</summary>
    public const string LobbyAttributeName = "lobby";

    private readonly IDbContextFactory<Mgo2DatabaseContext> contextFactory;
    private readonly Meter meter;
    private readonly Gauge<long> totalUsers;
    private readonly Gauge<long> totalPlayers;
    private readonly Gauge<long> totalMatches;

    /// <summary>Creates the instruments of the server.</summary>
    /// <param name="contextFactory">Factory used to count the rows.</param>
    public ServerMetricsService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    {
        this.contextFactory = contextFactory;
        meter = new Meter(MeterName);
        totalUsers = meter.CreateGauge<long>(
            TotalUsersMetricName,
            unit: "{account}",
            description: "Number of registered accounts.");
        totalPlayers = meter.CreateGauge<long>(
            TotalPlayersMetricName,
            unit: "{player}",
            description: "Number of players currently in a lobby.");
        totalMatches = meter.CreateGauge<long>(
            TotalMatchesMetricName,
            unit: "{match}",
            description: "Number of matches currently published in a lobby.");
    }

    /// <summary>Records the number of accounts.</summary>
    /// <param name="value">New total.</param>
    public void RecordTotalUsers(long value) => totalUsers.Record(value);

    /// <summary>Records the number of players in a lobby.</summary>
    /// <param name="value">New total.</param>
    /// <param name="lobby">Lobby the total belongs to.</param>
    public void RecordTotalPlayers(long value, string lobby) =>
        totalPlayers.Record(value, LobbyAttribute(lobby));

    /// <summary>Records the number of matches of a lobby.</summary>
    /// <param name="value">New total.</param>
    /// <param name="lobby">Lobby the total belongs to.</param>
    public void RecordTotalMatches(long value, string lobby) =>
        totalMatches.Record(value, LobbyAttribute(lobby));

    /// <summary>
    /// Counts the accounts and records the total. Nothing is read while no
    /// collector is listening, so a server without telemetry pays no query.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task ReportTotalUsersAsync(CancellationToken cancellationToken = default)
    {
        if (!totalUsers.Enabled)
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        RecordTotalUsers(await context.Users.LongCountAsync(cancellationToken));
    }

    /// <summary>
    /// Records the population of the lobby this server hosts and counts its
    /// matches, which is what a lobby server publishes. The account total is the
    /// API's, so a lobby server does not report it.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the hosted lobby.</param>
    /// <param name="playerCount">Players currently in the hosted lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task ReportLobbyTotalsAsync(
        int lobbyIdentifier,
        long playerCount,
        CancellationToken cancellationToken = default)
    {
        if (!totalPlayers.Enabled && !totalMatches.Enabled)
        {
            return;
        }

        var lobby = await ResolveLobbyNameAsync(lobbyIdentifier, cancellationToken);
        RecordTotalPlayers(playerCount, lobby);
        await ReportTotalMatchesAsync(lobbyIdentifier, lobby, cancellationToken);
    }

    /// <summary>
    /// Counts the matches of one lobby and records the total under that lobby, so
    /// a dashboard can filter the metric by lobby. Nothing is read while no
    /// collector is listening.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="lobbyName">Name the lobby is reported under.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task ReportTotalMatchesAsync(
        int lobbyIdentifier,
        string? lobbyName,
        CancellationToken cancellationToken = default)
    {
        if (!totalMatches.Enabled)
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var count = await context.Games
            .LongCountAsync(game => game.LobbyIdentifier == lobbyIdentifier, cancellationToken);
        RecordTotalMatches(count, lobbyName ?? FormatLobbyIdentifier(lobbyIdentifier));
    }

    /// <summary>
    /// Reports the name a lobby is labelled with, falling back to its identifier
    /// when the row is gone, so a total is always attributable to one lobby.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Name of the lobby.</returns>
    public async Task<string> ResolveLobbyNameAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var name = await context.Lobbies
            .AsNoTracking()
            .Where(lobby => lobby.Identifier == lobbyIdentifier)
            .Select(lobby => lobby.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return name ?? FormatLobbyIdentifier(lobbyIdentifier);
    }

    /// <summary>Labels a total of a lobby that no longer has a row.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    public static string FormatLobbyIdentifier(int lobbyIdentifier) => $"lobby_{lobbyIdentifier}";

    /// <inheritdoc />
    public void Dispose() => meter.Dispose();

    private static KeyValuePair<string, object?> LobbyAttribute(string lobby) =>
        new(LobbyAttributeName, lobby);
}
