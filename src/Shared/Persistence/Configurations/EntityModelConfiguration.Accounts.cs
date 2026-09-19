using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Account mappings.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(account => account.DisplayName).IsUnique();
            entity.HasOne(account => account.CurrentCharacter)
                .WithMany()
                .HasForeignKey(account => account.CurrentCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(account => account.MainCharacter)
                .WithMany()
                .HasForeignKey(account => account.MainCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            // An account has at most one session row, so the upsert that
            // replaces it cannot race another login into a duplicate.
            entity.HasIndex(session => session.AccountIdentifier).IsUnique();
        });
    }
}
