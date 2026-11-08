using Mgo2Server.GameplayServer.Bots;
using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.Tests;

/// <summary>
/// The navigation data the build ships: its codec, the placement families a rule
/// picks from, and the grid a route runs over. The stage reader itself lives in
/// <c>tools/stage-navigation</c>, because the server never opens a stage file.
/// </summary>
public sealed class StageNavigationTests
{
    [Fact]
    public void TheNavigationDataSurvivesARoundTrip()
    {
        var stage = new StageNavigationMap(
            "n001a",
            new StageBounds(-50000f, -1000f, -37000f, 50000f, 1000f, 47000f),
            1000f,
            [
                new StageSpawnSet(
                    StageSpawnFamily.TeamDeathmatch,
                    [new BotPosition(100f, 200f, 300f), new BotPosition(-100f, 0f, 50f)]),
            ],
            [new BotPosition(500f, 500f, 500f), new BotPosition(1500f, 500f, 500f)]);

        using var stream = new MemoryStream();
        StageNavigationFileUtils.Write(stream, [stage]);
        stream.Position = 0;

        var read = StageNavigationFileUtils.Read(stream);

        var restored = Assert.Single(read).Value;
        Assert.Equal(stage.Stage, restored.Stage);
        Assert.Equal(stage.Bounds, restored.Bounds);
        Assert.Equal(stage.CellSize, restored.CellSize);
        Assert.Equal(stage.Cells, restored.Cells);
        Assert.Equal(
            stage.Placements(StageSpawnFamily.TeamDeathmatch),
            restored.Placements(StageSpawnFamily.TeamDeathmatch));
    }

    [Fact]
    public void DataThatDoesNotOpenWithItsMagicIsRefused()
    {
        using var stream = new MemoryStream("MGO2NAV0"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => StageNavigationFileUtils.Read(stream));
    }

    [Fact]
    public void DataThatEndsInsideAStageIsRefused()
    {
        var stage = new StageNavigationMap(
            "n001a",
            new StageBounds(0f, 0f, 0f, 1f, 1f, 1f),
            1000f,
            [],
            [new BotPosition(0f, 0f, 0f)]);

        using var complete = new MemoryStream();
        StageNavigationFileUtils.Write(complete, [stage]);
        var truncated = complete.ToArray()[..^4];

        using var stream = new MemoryStream(truncated);
        Assert.ThrowsAny<Exception>(() => StageNavigationFileUtils.Read(stream));
    }

    [Fact]
    public void ARulePicksItsOwnFamilyAndFallsBackToDeathmatch()
    {
        var map = new StageNavigationMap(
            "n001a",
            new StageBounds(0f, 0f, 0f, 1000f, 1000f, 1000f),
            1000f,
            [
                new StageSpawnSet(
                    StageSpawnFamily.TeamDeathmatch,
                    [new BotPosition(10f, 0f, 10f)]),
                new StageSpawnSet(
                    StageSpawnFamily.Rescue,
                    [new BotPosition(20f, 0f, 20f)]),
            ],
            []);

        Assert.Equal(
            [new BotPosition(20f, 0f, 20f)],
            StageSpawnRuleUtils.Select(map, MatchRuleUtils.Rescue));
        Assert.Equal(
            [new BotPosition(10f, 0f, 10f)],
            StageSpawnRuleUtils.Select(map, MatchRuleUtils.Base));
    }

    [Fact]
    public void ARouteRunsOverTheExtractedCells()
    {
        var map = new StageNavigationMap(
            "n001a",
            new StageBounds(0f, 0f, 0f, 4000f, 1000f, 1000f),
            1000f,
            [],
            [
                new BotPosition(500f, 500f, 500f),
                new BotPosition(1500f, 500f, 500f),
                new BotPosition(2500f, 500f, 500f),
                new BotPosition(3500f, 500f, 500f),
            ]);
        var grid = NavigationGrid.Build(map);

        Assert.Equal(4, grid.Count);
        Assert.Equal(new BotPosition(500f, 500f, 500f), grid.Nearest(new BotPosition(0f, 0f, 0f)));

        var path = grid.FindPath(
            new BotPosition(500f, 500f, 500f),
            new BotPosition(3500f, 500f, 500f),
            out var reason);

        Assert.NotNull(path);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(4, path.Count);
        Assert.Equal(new BotPosition(500f, 500f, 500f), path[0]);
        Assert.Equal(new BotPosition(3500f, 500f, 500f), path[^1]);
    }

    [Fact]
    public void AGridWithNoCellRoutesNothing()
    {
        var map = new StageNavigationMap(
            "n001a",
            new StageBounds(0f, 0f, 0f, 1f, 1f, 1f),
            1000f,
            [],
            []);
        var grid = NavigationGrid.Build(map);

        Assert.True(grid.IsEmpty);
        Assert.Null(
            grid.FindPath(new BotPosition(0f, 0f, 0f), new BotPosition(1f, 1f, 1f), out var reason));
        Assert.Contains("no cell", reason, StringComparison.Ordinal);
    }
}
