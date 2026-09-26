using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Event subsystem mappings: teams, matches, leases and tournaments.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventTeam>(entity =>
        {
            entity.HasIndex(team => new { team.LobbyIdentifier, team.MatchType });
        });

        modelBuilder.Entity<EventTeamMember>(entity =>
        {
            // Slot zero is the leader, so the slot is the natural key within the
            // team and a character may hold at most one slot in it.
            entity.HasKey(member => new { member.TeamIdentifier, member.Slot });
            entity.HasIndex(member => new { member.TeamIdentifier, member.CharacterIdentifier })
                .IsUnique();
            entity.HasOne(member => member.Team)
                .WithMany(team => team.Members)
                .HasForeignKey(member => member.TeamIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventMatch>(entity =>
        {
            // A team plays one match at a time. The guard is scoped to the live
            // states, so a finished match keeps its history and its teams can
            // play again — and it is one index per side rather than one on the
            // pair, because what must be unique is a team's participation, not
            // which two teams met: the same pair can meet in a later round.
            entity.HasIndex(match => match.FirstTeamIdentifier)
                .IsUnique()
                .HasFilter("state in (1, 2)");
            entity.HasIndex(match => match.SecondTeamIdentifier)
                .IsUnique()
                .HasFilter("state in (1, 2)");
            entity.HasIndex(match => match.LobbyIdentifier);
        });

        modelBuilder.Entity<EventHostLease>(entity =>
        {
            entity.HasIndex(lease => lease.MatchIdentifier).IsUnique();
            // Scoped to the active status: a released lease keeps its history,
            // and the room it named returns to the pool for the next match.
            entity.HasIndex(lease => lease.GameIdentifier)
                .IsUnique()
                .HasFilter("status = 1");
            // The claim goes with the room. A lease says "this match is playing
            // in that room", and a room that no longer exists cannot be playing
            // anything — worse, the stale-room reaper deletes games in one
            // statement, so a lease left behind would fail that delete and stop
            // every abandoned room in the lobby from ever being reaped.
            entity.HasOne<Game>()
                .WithMany()
                .HasForeignKey(lease => lease.GameIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(lease => lease.Version).IsRowVersion();
        });

        modelBuilder.Entity<EventRoundReward>(entity =>
        {
            entity.HasIndex(reward => new { reward.MatchIdentifier, reward.CharacterIdentifier })
                .IsUnique();
        });

        modelBuilder.Entity<EventStatReport>(entity =>
        {
            // One report per character per match: the guard is what makes a
            // repeated report a no-op instead of a second vote.
            entity.HasIndex(report => new { report.MatchIdentifier, report.CharacterIdentifier })
                .IsUnique();
            entity.HasIndex(report => report.MatchIdentifier);
        });

        modelBuilder.Entity<EventSchedule>(entity =>
        {
            // The published events are listed by mode, so that is the axis a
            // lobby screen reads rather than an index over the whole table.
            entity.HasIndex(schedule => schedule.LobbySubtype);

            // The name is how an operator addresses an event, so two schedules
            // sharing one would make the name ambiguous. The service refuses a
            // duplicate it can see; this is the backstop against the race two
            // operators would otherwise both win.
            entity.HasIndex(schedule => schedule.Name).IsUnique();

            entity.Property(schedule => schedule.PublishStart).HasDefaultValue(0L);
            entity.Property(schedule => schedule.PublishEnd).HasDefaultValue(0L);
            entity.Property(schedule => schedule.TeamCapacity)
                .HasDefaultValue(Domain.Events.EventConstants.BracketMaximumEntrants);
        });

        modelBuilder.Entity<TournamentRegistration>(entity =>
        {
            entity.HasIndex(registration => new { registration.EventIdentifier, registration.CharacterIdentifier })
                .IsUnique();
        });

        modelBuilder.Entity<TournamentSeed>(entity =>
        {
            entity.HasKey(seed => new { seed.EventIdentifier, seed.SeedIndex });
            entity.HasIndex(seed => new { seed.EventIdentifier, seed.TeamIdentifier }).IsUnique();
            entity.HasOne(seed => seed.Bracket)
                .WithMany()
                .HasForeignKey(seed => seed.EventIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TournamentResult>(entity =>
        {
            entity.HasIndex(result => new { result.EventIdentifier, result.MatchIdentifier }).IsUnique();
        });

        modelBuilder.Entity<TournamentRosterMember>(entity =>
        {
            // The position is the natural key inside the roster, and a character
            // holds at most one place in it: a roster that named somebody twice
            // would put them in their own pairing.
            entity.HasKey(member => new { member.EventIdentifier, member.TeamIdentifier, member.MemberIndex });
            entity.HasIndex(member => new { member.EventIdentifier, member.CharacterIdentifier })
                .IsUnique();
            // The roster goes with the bracket it was submitted for, so a bracket
            // that is removed takes the roster the draw was made with with it.
            entity.HasOne<TournamentBracket>()
                .WithMany()
                .HasForeignKey(member => member.EventIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
