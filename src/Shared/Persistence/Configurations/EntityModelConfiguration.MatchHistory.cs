using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Mappings of the tables behind the match history.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureMatchHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HostReview>(entity =>
        {
            entity.HasKey(review => new { review.GameIdentifier, review.VoterCharacterIdentifier });
            entity.HasIndex(review => new { review.HostCharacterIdentifier, review.ReviewedAt });
            entity.HasOne(review => review.Host)
                .WithMany()
                .HasForeignKey(review => review.HostCharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(review => review.Voter)
                .WithMany()
                .HasForeignKey(review => review.VoterCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RoundReport>(entity =>
        {
            entity.HasOne(report => report.Game)
                .WithMany()
                .HasForeignKey(report => report.GameIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(report => report.Host)
                .WithMany()
                .HasForeignKey(report => report.HostCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(report => report.Target)
                .WithMany()
                .HasForeignKey(report => report.TargetCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WeaponTally>(entity =>
        {
            entity.HasOne(tally => tally.Game)
                .WithMany()
                .HasForeignKey(tally => tally.GameIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(tally => tally.Character)
                .WithMany()
                .HasForeignKey(tally => tally.CharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
