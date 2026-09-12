using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Clan mappings.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureClans(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clan>(entity =>
        {
            entity.HasIndex(clan => clan.Name).IsUnique();
            // The clan and membership tables reference each other, so the
            // clan-side references cannot cascade.
            entity.HasOne(clan => clan.Leader)
                .WithMany()
                .HasForeignKey(clan => clan.LeaderIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ClanMember>()
                .WithMany()
                .HasForeignKey(clan => clan.NoticeWriterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<ClanMember>()
                .WithMany()
                .HasForeignKey(clan => clan.EmblemEditorIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ClanMember>(entity =>
        {
            entity.HasIndex(member => new { member.ClanIdentifier, member.CharacterIdentifier }).IsUnique();
            entity.HasOne(member => member.Clan)
                .WithMany(clan => clan.Members)
                .HasForeignKey(member => member.ClanIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(member => member.Character)
                .WithMany()
                .HasForeignKey(member => member.CharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ClanApplication>(entity =>
        {
            entity.HasIndex(application => new { application.ClanIdentifier, application.CharacterIdentifier }).IsUnique();
            entity.HasOne(application => application.Clan)
                .WithMany()
                .HasForeignKey(application => application.ClanIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(application => application.Character)
                .WithMany()
                .HasForeignKey(application => application.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
