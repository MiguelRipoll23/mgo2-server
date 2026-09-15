using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Instructor relationship, review and training-time mappings.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureInstructors(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstructorReview>(entity =>
        {
            // The score gauge and the monthly board both read one instructor's
            // reviews over a window, so the index is ordered to serve that.
            entity.HasIndex(review => new { review.InstructorCharacterIdentifier, review.ReviewedAt });
            entity.HasOne(review => review.Instructor)
                .WithMany()
                .HasForeignKey(review => review.InstructorCharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(review => review.Student)
                .WithMany()
                .HasForeignKey(review => review.StudentCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CharacterInstructor>(entity =>
        {
            // One relationship per student, and the student's identifier is the
            // key rather than a generated value.
            entity.HasKey(relationship => relationship.CharacterIdentifier);
            entity.Property(relationship => relationship.CharacterIdentifier)
                .ValueGeneratedNever();
            entity.HasOne(relationship => relationship.Character)
                .WithMany()
                .HasForeignKey(relationship => relationship.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(relationship => relationship.Instructor)
                .WithMany()
                .HasForeignKey(relationship => relationship.InstructorCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TrainingTime>(entity =>
        {
            entity.HasKey(totals => totals.CharacterIdentifier);
            entity.Property(totals => totals.CharacterIdentifier)
                .ValueGeneratedNever();
            entity.HasOne(totals => totals.Character)
                .WithMany()
                .HasForeignKey(totals => totals.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
