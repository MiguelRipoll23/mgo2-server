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

        ConfigureLobbyGameTypes(modelBuilder);
    }

    /// <summary>
    /// Declares the game types a lobby can be configured with. They are reference
    /// data with fixed identifiers rather than rows a server creates, so they are
    /// seeded through the model: the identifiers equal the game identifier of the
    /// wire format (see <c>LobbySubtypeConstants</c>), a later edit to this list
    /// becomes a migration instead of a boot-time repair, and no lobby server has
    /// to write them for the gate to serve a subtype.
    /// </summary>
    /// <param name="modelBuilder">Model being built.</param>
    private static void ConfigureLobbyGameTypes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LobbyGameType>().HasData(
            new LobbyGameType { Identifier = 0, GameIdentifier = 0, Name = "None" },
            new LobbyGameType { Identifier = 1, GameIdentifier = 1, Name = "Free Battle" },
            new LobbyGameType { Identifier = 2, GameIdentifier = 2, Name = "Automatching" },
            new LobbyGameType { Identifier = 3, GameIdentifier = 3, Name = "Tournament" },
            new LobbyGameType { Identifier = 4, GameIdentifier = 4, Name = "Survival" },
            new LobbyGameType { Identifier = 5, GameIdentifier = 5, Name = "Unknown" },
            new LobbyGameType { Identifier = 6, GameIdentifier = 6, Name = "Unknown" },
            new LobbyGameType { Identifier = 7, GameIdentifier = 7, Name = "Basic Training" },
            new LobbyGameType { Identifier = 8, GameIdentifier = 8, Name = "Combat Training" },
            new LobbyGameType { Identifier = 10, GameIdentifier = 10, Name = "Tournament Registration" });
    }
}
