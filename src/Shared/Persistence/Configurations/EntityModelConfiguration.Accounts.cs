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

        modelBuilder.Entity<AccountRole>(entity =>
        {
            // The role is revoked by deleting the row, so a grant is removed by the
            // same delete that removes its cascade on the account. A cascade the
            // other way would take the roles with the account, which is the only
            // direction that needs saying.
            entity.HasOne(role => role.Account)
                .WithMany(account => account.Roles)
                .HasForeignKey(role => role.AccountIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
