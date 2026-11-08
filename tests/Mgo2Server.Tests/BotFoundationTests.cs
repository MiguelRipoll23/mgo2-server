using Mgo2Server.GameplayServer.Bots;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// The bot settings, names, side resolution and assignment: the parts a room's
/// population is made of. The decision stack is exercised through them and not
/// asserted on its own, because its scores are design parameters rather than
/// protocol facts.
/// </summary>
public sealed class BotFoundationTests
{
    [Theory]
    [InlineData("NONE", BotTeamPolicy.None)]
    [InlineData("both", BotTeamPolicy.Both)]
    [InlineData(" Enemy ", BotTeamPolicy.Enemy)]
    public void TheTeamPolicyIsReadFromItsOwnWord(string value, BotTeamPolicy expected)
    {
        Assert.True(BotOptionsUtils.TryParseTeam(value, out var policy));
        Assert.Equal(expected, policy);
    }

    [Theory]
    [InlineData("ENEMYS")]
    [InlineData("0")]
    [InlineData("")]
    public void ATeamPolicyThatIsNoPolicyIsRefused(string value)
    {
        Assert.False(BotOptionsUtils.TryParseTeam(value, out _));
    }

    [Fact]
    public void EveryUnreadableBotSettingIsNamed()
    {
        var options = new BotOptions
        {
            Team = "ENEMYS",
            PopulationMode = "SOMETIMES",
            Count = -1,
            PopulationIntervalSeconds = 0,
        };

        var faults = BotOptionsUtils.Validate(options);

        Assert.Equal(4, faults.Count);
        Assert.Contains(faults, fault => fault.Contains("BOT_TEAM", StringComparison.Ordinal));
        Assert.Contains(faults, fault => fault.Contains("BOT_POPULATION_MODE", StringComparison.Ordinal));
        Assert.Contains(faults, fault => fault.Contains("BOT_COUNT", StringComparison.Ordinal));
        Assert.Contains(
            faults,
            fault => fault.Contains("BOT_POPULATION_INTERVAL_SECONDS", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDefaultsAreReadable()
    {
        Assert.Empty(BotOptionsUtils.Validate(new BotOptions()));
        Assert.Equal(BotTeamPolicy.Enemy, BotOptionsUtils.Team(new BotOptions()));
        Assert.Equal(BotPopulationMode.Fill, BotOptionsUtils.PopulationMode(new BotOptions()));
    }

    [Fact]
    public void BotNamesRunInOrderAndAReleasedNumberIsNeverHandedOutAgain()
    {
        var identities = new BotIdentityService();

        var first = identities.Allocate();
        var second = identities.Allocate();
        Assert.Equal("BOT-1", first.Name);
        Assert.Equal("BOT-2", second.Name);

        Assert.True(identities.Release(first.Name));
        Assert.False(identities.Release(first.Name));

        var third = identities.Allocate();
        Assert.Equal("BOT-3", third.Name);
        Assert.Equal(2, identities.ActiveCount);
    }

    [Theory]
    [InlineData("BOT-1", true)]
    [InlineData("BOT-42", true)]
    [InlineData("BOT-01", false)]
    [InlineData("bot-1", false)]
    [InlineData("BOT-", false)]
    [InlineData("BOT-0", false)]
    [InlineData("BOT-X", false)]
    public void OnlyTheConfiguredShapeIsABotName(string name, bool expected) =>
        Assert.Equal(expected, BotIdentityService.IsBotName(name));

    [Fact]
    public void EnemyBotsTakeTheSideNoHumanHolds()
    {
        var teams = new BotTeamAssignmentService(NullLogger<BotTeamAssignmentService>.Instance);

        Assert.Equal((short?)1, teams.ChooseTeam(BotTeamPolicy.Enemy, [0], []));
        Assert.Equal((short?)0, teams.ChooseTeam(BotTeamPolicy.Enemy, [1], []));
    }

    [Fact]
    public void EnemyBotsOpposeTheSmallerHumanPresence()
    {
        var teams = new BotTeamAssignmentService(NullLogger<BotTeamAssignmentService>.Instance);

        // One human on side 1 against two on side 0: the smaller presence is side
        // 1, so that is the side the bot opposes.
        Assert.Equal((short?)1, teams.ChooseTeam(BotTeamPolicy.Enemy, [1, 0, 0], []));
        Assert.Equal((short?)0, teams.ChooseTeam(BotTeamPolicy.Enemy, [0, 1, 1], []));
    }

    [Fact]
    public void NoHumanSideLeavesNoSideForABot()
    {
        var teams = new BotTeamAssignmentService(NullLogger<BotTeamAssignmentService>.Instance);

        Assert.Null(teams.ChooseTeam(BotTeamPolicy.Enemy, [], []));
        Assert.Null(teams.ChooseTeam(BotTeamPolicy.None, [0], []));
    }

    [Fact]
    public void BothSidesPutABotWhereTheFewestBotsAre()
    {
        var teams = new BotTeamAssignmentService(NullLogger<BotTeamAssignmentService>.Instance);

        Assert.Equal((short?)1, teams.ChooseTeam(BotTeamPolicy.Both, [0], [0]));
        Assert.Equal((short?)0, teams.ChooseTeam(BotTeamPolicy.Both, [0], [1]));
        Assert.Equal((short?)0, teams.ChooseTeam(BotTeamPolicy.Both, [], []));
    }

    [Fact]
    public void ObjectivesAreSharedBeforeAnyIsTakenTwice()
    {
        var coordination = new BotTeamCoordinationService(
            NullLogger<BotTeamCoordinationService>.Instance);
        var bots = new[]
        {
            Participant(1, 1, BotRole.Assault),
            Participant(2, 2, BotRole.Support),
            Participant(3, 3, BotRole.Sniper),
        };

        var assignments = coordination.Assign(bots, new BaseBotObjective());

        Assert.Equal(
            [
                BotObjectiveKind.DefendObjective,
                BotObjectiveKind.CaptureObjective,
                BotObjectiveKind.DefendObjective,
            ],
            assignments.Select(assignment => assignment.Objective));
        Assert.Equal(
            [BotRole.Assault, BotRole.Support, BotRole.Sniper],
            assignments.Select(assignment => assignment.Role));
    }

    [Fact]
    public void FiveBotsHoldFiveRoles()
    {
        var roles = new BotRoleAssignmentService();
        var chosen = Enumerable
            .Range(1, 5)
            .Select(number => roles.Choose(number, MatchRuleUtils.TeamDeathmatch))
            .ToList();

        Assert.Equal(5, chosen.Distinct().Count());
        Assert.Equal(chosen, Enumerable
            .Range(1, 5)
            .Select(number => roles.Choose(number, MatchRuleUtils.TeamDeathmatch)));
    }

    [Fact]
    public void ASneakingRoundLeansTowardsReconnaissance()
    {
        var roles = new BotRoleAssignmentService();

        Assert.Equal(BotRole.Recon, roles.Choose(1, MatchRuleUtils.Sneaking));
        Assert.Equal(BotRole.Recon, roles.Choose(1, MatchRuleUtils.TeamSneaking));
        Assert.Equal(BotRole.Assault, roles.Choose(1, MatchRuleUtils.TeamDeathmatch));
    }

    [Fact]
    public void FillingBotsGiveUpTheSlotsHumansTake()
    {
        // FILL answers a room a player is in: with no human on the roster the room
        // holds no bots, and the first human to arrive brings them back. The count is
        // the room's own capacity — a fill takes every seat a player is not in — and
        // the deployment's BOT_COUNT bounds a PERMANENT population instead.
        Assert.Equal(0, BotPopulationUtils.TargetCount(BotPopulationMode.Fill, 4, 0, 16));
        Assert.Equal(15, BotPopulationUtils.TargetCount(BotPopulationMode.Fill, 4, 1, 16));
        // Six slots and two humans leave four free.
        Assert.Equal(4, BotPopulationUtils.TargetCount(BotPopulationMode.Fill, 4, 2, 6));
        Assert.Equal(4, BotPopulationUtils.TargetCount(BotPopulationMode.Fill, 3, 2, 6));
        Assert.Equal(0, BotPopulationUtils.TargetCount(BotPopulationMode.Fill, 4, 6, 6));
    }

    [Fact]
    public void PermanentBotsHoldTheirSlotsWhateverTheHumansTake()
    {
        Assert.Equal(4, BotPopulationUtils.TargetCount(BotPopulationMode.Permanent, 4, 0, 16));
        Assert.Equal(4, BotPopulationUtils.TargetCount(BotPopulationMode.Permanent, 4, 12, 16));
        Assert.Equal(6, BotPopulationUtils.TargetCount(BotPopulationMode.Permanent, 9, 12, 6));
    }

    private static BotParticipant Participant(int number, byte slot, BotRole role) => new(
        new BotIdentity(BotIdentityService.NameForNumber(number), number),
        slot,
        (sbyte)(slot - 1),
        Team: 0,
        role,
        BotConfiguration.Default.WithRole(role));
}
