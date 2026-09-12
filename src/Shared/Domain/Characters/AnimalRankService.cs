using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// Derives a character's animal rank from its statistics. Rank identifiers are
/// the client's own: 1 Foxhound, 2 Fox, 3 Doberman, 4 Hound, 5 Crocodile,
/// 6 Eagle, 7 Jaws, 8 Water Bear, 9 Sloth, 10 Flying Squirrel, 11 Pigeon,
/// 12 Owl, 13 Tsuchinoko, 14 Snake, 15 Kerotan, 16 GA-KO, 17 Chameleon,
/// 18 Chicken, 19 Bear, 20 Tortoise, 21 Bee, 22 Rat, 23 Fighting Fish,
/// 24 Komodo Dragon, 26 Killer Whale, 27 Elephant, 28 Cuckoo, 29 Hog,
/// 40 Octopus, 42 Panda, 43 Puma, 44 Scorpion, 46 Mantis, 48 Night Owl,
/// 50 Hawk, 52 Ocelot. Zero means no rank.
/// </summary>
public static class AnimalRankService
{
    /// <summary>Calculates the animal rank of a character.</summary>
    /// <param name="statistics">Statistics of the character.</param>
    /// <param name="daysSinceLastLogin">Days since the character last logged in.</param>
    public static int CalculateRank(CharacterStatistics statistics, int daysSinceLastLogin)
    {
        var totalRounds = statistics.Rounds == 0 ? 1 : statistics.Rounds;

        var deathmatch = ModeStatisticsCodec.ForMode(statistics, 0);
        var teamDeathmatch = ModeStatisticsCodec.ForMode(statistics, 1);
        var sneaking = ModeStatisticsCodec.ForMode(statistics, 2);
        var capture = ModeStatisticsCodec.ForMode(statistics, 3);
        var baseMode = ModeStatisticsCodec.ForMode(statistics, 4);
        var bomb = ModeStatisticsCodec.ForMode(statistics, 5);
        var rescue = ModeStatisticsCodec.ForMode(statistics, 6);
        var race = ModeStatisticsCodec.ForMode(statistics, 7);
        var teamSneaking = ModeStatisticsCodec.ForMode(statistics, 8);
        var survivalDeathmatch = ModeStatisticsCodec.ForMode(statistics, 9);

        // Foxhound family: reached only on sustained, high-quality play. The
        // thresholds narrow as more rounds are played, and the branches are
        // evaluated as a chain, so the order is significant.
        if (statistics.Rounds >= 100)
        {
            var killDeathStunReviveRatio = CalculateKillDeathStunReviveRatio(deathmatch, teamDeathmatch, sneaking);
            var winRate = CalculateWinRate(capture, baseMode, bomb, rescue, teamSneaking);
            var baseCaptureRate = baseMode.Rounds > 0 ? (double)statistics.BasesCaptured / baseMode.Rounds : 0;
            var withdrawalRate = statistics.Rounds > 0 ? (double)statistics.Withdrawals / statistics.Rounds : 0;
            var raceWinRate = race.Rounds > 0 ? (double)race.Wins / race.Rounds : 1;

            if (killDeathStunReviveRatio >= 1.45 && winRate >= 0.525 && raceWinRate >= 0.50 &&
                baseCaptureRate >= 1.60 && withdrawalRate <= 0.02)
            {
                return 1; // Foxhound
            }

            if (killDeathStunReviveRatio >= 1.40 && winRate >= 0.475 && raceWinRate >= 0.45 &&
                baseCaptureRate >= 1.40 && withdrawalRate <= 0.02 && statistics.Rounds >= 50)
            {
                return 2; // Fox
            }

            if (killDeathStunReviveRatio >= 1.35 && winRate >= 0.45 && raceWinRate >= 0.425 &&
                baseCaptureRate >= 1.20 && withdrawalRate <= 0.04 && statistics.Rounds >= 25)
            {
                return 3; // Doberman
            }

            if (killDeathStunReviveRatio >= 1.30 && winRate >= 0.425 && raceWinRate >= 0.40 &&
                baseCaptureRate >= 1.00 && withdrawalRate <= 0.04 && statistics.Rounds >= 5)
            {
                return 4; // Hound
            }
        }
        else if (statistics.Rounds >= 50)
        {
            var killDeathStunReviveRatio = CalculateKillDeathStunReviveRatio(deathmatch, teamDeathmatch, sneaking);
            var winRate = CalculateWinRate(capture, baseMode, bomb, rescue, teamSneaking);
            var baseCaptureRate = baseMode.Rounds > 0 ? (double)statistics.BasesCaptured / baseMode.Rounds : 0;
            var withdrawalRate = statistics.Rounds > 0 ? (double)statistics.Withdrawals / statistics.Rounds : 0;
            var raceWinRate = race.Rounds > 0 ? (double)race.Wins / race.Rounds : 1;

            if (killDeathStunReviveRatio >= 1.40 && winRate >= 0.475 && raceWinRate >= 0.45 &&
                baseCaptureRate >= 1.40 && withdrawalRate <= 0.02)
            {
                return 2; // Fox
            }

            if (killDeathStunReviveRatio >= 1.35 && winRate >= 0.45 && raceWinRate >= 0.425 &&
                baseCaptureRate >= 1.20 && withdrawalRate <= 0.04 && statistics.Rounds >= 25)
            {
                return 3; // Doberman
            }

            if (killDeathStunReviveRatio >= 1.30 && winRate >= 0.425 && raceWinRate >= 0.40 &&
                baseCaptureRate >= 1.00 && withdrawalRate <= 0.04 && statistics.Rounds >= 5)
            {
                return 4; // Hound
            }
        }
        else if (statistics.Rounds >= 25)
        {
            var killDeathStunReviveRatio = CalculateKillDeathStunReviveRatio(deathmatch, teamDeathmatch, sneaking);
            var winRate = CalculateWinRate(capture, baseMode, bomb, rescue, teamSneaking);
            var baseCaptureRate = baseMode.Rounds > 0 ? (double)statistics.BasesCaptured / baseMode.Rounds : 0;
            var withdrawalRate = statistics.Rounds > 0 ? (double)statistics.Withdrawals / statistics.Rounds : 0;
            var raceWinRate = race.Rounds > 0 ? (double)race.Wins / race.Rounds : 1;

            if (killDeathStunReviveRatio >= 1.35 && winRate >= 0.45 && raceWinRate >= 0.425 &&
                baseCaptureRate >= 1.20 && withdrawalRate <= 0.04)
            {
                return 3; // Doberman
            }

            if (killDeathStunReviveRatio >= 1.30 && winRate >= 0.425 && raceWinRate >= 0.40 &&
                baseCaptureRate >= 1.00 && withdrawalRate <= 0.04 && statistics.Rounds >= 5)
            {
                return 4; // Hound
            }
        }
        else if (statistics.Rounds >= 5)
        {
            var killDeathStunReviveRatio = CalculateKillDeathStunReviveRatio(deathmatch, teamDeathmatch, sneaking);
            var winRate = CalculateWinRate(capture, baseMode, bomb, rescue, teamSneaking);
            var baseCaptureRate = baseMode.Rounds > 0 ? (double)statistics.BasesCaptured / baseMode.Rounds : 0;
            var withdrawalRate = statistics.Rounds > 0 ? (double)statistics.Withdrawals / statistics.Rounds : 0;
            var raceWinRate = race.Rounds > 0 ? (double)race.Wins / race.Rounds : 1;

            if (killDeathStunReviveRatio >= 1.30 && winRate >= 0.425 && raceWinRate >= 0.40 &&
                baseCaptureRate >= 1.00 && withdrawalRate <= 0.04)
            {
                return 4; // Hound
            }
        }

        // Combat style.
        var overallRatio = (statistics.Kills + statistics.Stuns + statistics.Deaths + statistics.StunsReceived)
            / (double)totalRounds;

        if (overallRatio >= 1.50)
        {
            return 5; // Crocodile
        }

        if (overallRatio >= 1.30 && statistics.Kills > 0 &&
            (double)statistics.HeadshotKills / statistics.Kills >= 0.50)
        {
            return 6; // Eagle
        }

        if (overallRatio >= 1.25 && statistics.Kills > 0 &&
            (double)statistics.KnifeKills / statistics.Kills >= 0.075)
        {
            return 7; // Jaws
        }

        if (statistics.Kills > 0 && statistics.StunsReceived > 0 &&
            (double)statistics.Stuns / statistics.Kills >= 1.20 &&
            (double)statistics.Stuns / statistics.StunsReceived >= 1.20)
        {
            return 11; // Pigeon
        }

        if ((double)statistics.KnifeStuns / totalRounds >= 1.0)
        {
            return 44; // Scorpion
        }

        if (statistics.Kills > 0 && (double)statistics.LockKills / statistics.Kills >= 0.40)
        {
            return 52; // Ocelot
        }

        // Equipment and play style.
        if ((double)statistics.CqcGiven / totalRounds >= 5.0)
        {
            return 19; // Bear
        }

        if ((double)statistics.BoxUses / totalRounds >= 15.0)
        {
            return 20; // Tortoise
        }

        if ((double)statistics.Scans / totalRounds >= 0.30)
        {
            return 21; // Bee
        }

        if ((double)statistics.Trapped / totalRounds >= 0.30)
        {
            return 22; // Rat
        }

        if (statistics.TotalTime > 0 && (double)statistics.EvasionTime / statistics.TotalTime >= 0.05)
        {
            return 12; // Owl
        }

        var sneakingRounds = teamSneaking.Rounds + sneaking.Rounds;
        if (sneakingRounds >= 15)
        {
            var spottedRatio = (double)(statistics.Spotted + statistics.SnakeSpotted) / sneakingRounds;
            if (spottedRatio <= 0.15)
            {
                return 40; // Octopus
            }

            if (spottedRatio <= 0.50)
            {
                return 48; // Night Owl
            }
        }

        if (teamSneaking.Rounds > 0 && (double)statistics.Spotted / teamSneaking.Rounds >= 0.30)
        {
            return 50; // Hawk
        }

        if ((double)statistics.Rolls / totalRounds >= 15.0)
        {
            return 10; // Flying Squirrel
        }

        // Game-mode specialists.
        if (IsModeSpecialist(deathmatch, totalRounds, 30))
        {
            return 23; // Fighting Fish
        }

        if (IsModeSpecialist(survivalDeathmatch, totalRounds, 30))
        {
            return 24; // Komodo Dragon
        }

        if (IsModeSpecialist(teamDeathmatch, totalRounds, 30))
        {
            return 26; // Killer Whale
        }

        if (IsModeSpecialist(baseMode, totalRounds, 30))
        {
            return 27; // Elephant
        }

        if (IsModeSpecialist(bomb, totalRounds, 30))
        {
            return 28; // Cuckoo
        }

        if (IsModeSpecialist(race, totalRounds, 30))
        {
            return 29; // Hog
        }

        if (IsModeSpecialist(sneaking, totalRounds, 30))
        {
            return 14; // Snake
        }

        if (IsModeSpecialist(capture, totalRounds, 30))
        {
            return 15; // Kerotan
        }

        if (IsModeSpecialist(rescue, totalRounds, 30))
        {
            return 16; // GA-KO
        }

        if (IsModeSpecialist(teamSneaking, totalRounds, 30))
        {
            return 17; // Chameleon
        }

        // Mode-specific behaviour.
        if (sneaking.Rounds >= 15 && (double)statistics.SnakeHoldups / sneaking.Rounds >= 2.0)
        {
            return 43; // Puma
        }

        if (baseMode.Rounds >= 15 && (double)statistics.BasesCaptured / baseMode.Rounds >= 2.5)
        {
            return 42; // Panda
        }

        if (teamSneaking.Rounds > 0 && (double)statistics.Wakeups / teamSneaking.Rounds >= 0.30)
        {
            return 46; // Mantis
        }

        // Survival and defense.
        var defenseRounds = rescue.Rounds + teamSneaking.Rounds;
        if (defenseRounds > 0 && (double)(rescue.Deaths + teamSneaking.Deaths) / defenseRounds <= 0.50)
        {
            return 8; // Water Bear
        }

        // Negative and passive play.
        if (statistics.Deaths > 0 && statistics.StunsReceived > 0 &&
            (double)statistics.Kills / statistics.Deaths <= 0.85 &&
            (double)statistics.HeadshotDeaths / statistics.Deaths >= 0.60 &&
            (double)statistics.Stuns / statistics.StunsReceived <= 0.85)
        {
            return 9; // Sloth
        }

        if ((double)statistics.Kills / totalRounds <= 0.30 &&
            (double)statistics.Stuns / totalRounds <= 0.30 &&
            (double)statistics.StunsReceived / totalRounds <= 0.50 &&
            (double)statistics.Deaths / totalRounds <= 0.50)
        {
            return 18; // Chicken
        }

        return daysSinceLastLogin >= 30 ? 13 : 0; // Tsuchinoko, or no rank
    }

    private static double CalculateKillDeathStunReviveRatio(
        ModeStatistics deathmatch,
        ModeStatistics teamDeathmatch,
        ModeStatistics sneaking)
    {
        var totalRounds = deathmatch.Rounds + teamDeathmatch.Rounds + sneaking.Rounds;
        if (totalRounds == 0)
        {
            return 0;
        }

        var total = deathmatch.Kills + teamDeathmatch.Kills + sneaking.Kills +
            deathmatch.Stuns + teamDeathmatch.Stuns + sneaking.Stuns +
            deathmatch.Deaths + teamDeathmatch.Deaths + sneaking.Deaths +
            deathmatch.StunsRec + teamDeathmatch.StunsRec + sneaking.StunsRec;

        return (double)total / totalRounds;
    }

    private static double CalculateWinRate(params ModeStatistics[] modes)
    {
        var totalRounds = 0;
        var totalWins = 0;
        foreach (var mode in modes)
        {
            totalRounds += mode.Rounds;
            totalWins += mode.Wins;
        }

        return totalRounds == 0 ? 0 : (double)totalWins / totalRounds;
    }

    private static bool IsModeSpecialist(ModeStatistics mode, int totalRounds, int minimumRounds) =>
        mode.Rounds >= minimumRounds && totalRounds > 0 && (double)mode.Rounds / totalRounds >= 0.60;
}
