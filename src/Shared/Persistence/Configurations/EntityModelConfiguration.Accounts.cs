using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Account mappings.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.DisplayName).IsUnique();
            entity.HasOne(user => user.CurrentCharacter)
                .WithMany()
                .HasForeignKey(user => user.CurrentCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(user => user.MainCharacter)
                .WithMany()
                .HasForeignKey(user => user.MainCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            // An account has at most one session row, so the upsert that
            // replaces it cannot race another login into a duplicate.
            entity.HasIndex(session => session.UserIdentifier).IsUnique();
        });
    }
}
