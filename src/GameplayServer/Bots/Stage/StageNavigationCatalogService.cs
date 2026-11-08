using Mgo2Server.GameplayServer.Match;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The stages this build ships navigation data for, keyed by stage name.
/// </summary>
/// <remarks>
/// <para>
/// <b>It reads the data the build carries and nothing off the disk.</b> The file is
/// an embedded resource written by <c>tools/stage-navigation</c>, so a gameplay
/// host has navigation for the maps the build was generated from without a client
/// install, a mounted directory or a lookup at run time. A map id becomes a stage
/// name through the room's own table (<see cref="MatchMapUtils.Stage"/>), and a
/// stage the data does not hold is refused with a sentence naming both.
/// </para>
/// <para>
/// It is loaded once, on the first question, and kept: the map does not change
/// while the process runs.
/// </para>
/// </remarks>
public sealed class StageNavigationCatalogService
{
    private readonly ILogger<StageNavigationCatalogService> logger;
    private readonly Lazy<IReadOnlyDictionary<string, StageNavigationMap>> stages;

    /// <summary>Creates the catalog of this build's stage data.</summary>
    /// <param name="logger">Logger of this service.</param>
    public StageNavigationCatalogService(ILogger<StageNavigationCatalogService> logger)
    {
        this.logger = logger;
        stages = new Lazy<IReadOnlyDictionary<string, StageNavigationMap>>(Load);
    }

    /// <summary>How many stages the build ships data for.</summary>
    public int StageCount => stages.Value.Count;

    /// <summary>
    /// The navigation data of a map, or why there is none.
    /// </summary>
    /// <param name="map">Map, in the replay decoder's numbering.</param>
    /// <param name="data">The map's data, when this returns <c>true</c>.</param>
    /// <param name="reason">Why there is none, when this returns <c>false</c>.</param>
    /// <returns><c>true</c> when the map has data.</returns>
    public bool TryRead(int map, out StageNavigationMap data, out string reason)
    {
        var stage = MatchMapUtils.Stage(map);
        if (stage is null)
        {
            data = null!;
            reason = $"Map {map} is named by no stage, so no navigation data exists for it.";
            return false;
        }

        if (stages.Value.TryGetValue(stage, out data!))
        {
            reason = string.Empty;
            return true;
        }

        reason = $"This build ships no navigation data for stage {stage} (map {map}); "
            + "run tools/stage-navigation over the client's stages and rebuild.";
        return false;
    }

    /// <summary>Why a map has no data, as a sentence for a log or a decision.</summary>
    /// <param name="map">Map, in the replay decoder's numbering.</param>
    /// <returns>The reason.</returns>
    public string Reason(int map) =>
        TryRead(map, out _, out var reason)
            ? string.Empty
            : reason;

    /// <summary>Reads the embedded data, once.</summary>
    /// <returns>The stages, empty when the build carries none.</returns>
    private IReadOnlyDictionary<string, StageNavigationMap> Load()
    {
        using var stream = StageNavigationFileUtils.OpenEmbedded();
        if (stream is null)
        {
            logger.LogWarning(
                "This build ships no stage navigation data, so bots have no map to route over");
            return new Dictionary<string, StageNavigationMap>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var maps = StageNavigationFileUtils.Read(stream);
            logger.LogInformation(
                "Read navigation data for {StageCount} stage(s) from the build's own {Resource}",
                maps.Count,
                StageNavigationFileUtils.ResourceName);
            return maps;
        }
        catch (InvalidDataException exception)
        {
            logger.LogError(
                exception,
                "The stage navigation data this build ships could not be read");
            return new Dictionary<string, StageNavigationMap>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
