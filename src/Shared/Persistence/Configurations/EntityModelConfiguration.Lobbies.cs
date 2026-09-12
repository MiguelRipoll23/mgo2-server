using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Lobby mappings.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureLobbies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lobby>(entity =>
        {
            entity.HasIndex(lobby => lobby.Port).IsUnique();
            entity.HasOne(lobby => lobby.Subtype)
                .WithMany()
                .HasForeignKey(lobby => lobby.SubtypeIdentifier)
                .OnDelete(DeleteBehavior.Restrict);
        });

    }
}
