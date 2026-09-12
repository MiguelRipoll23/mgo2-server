using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Game room mappings.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureGames(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasOne(game => game.Host)
                .WithMany()
                .HasForeignKey(game => game.HostIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(game => game.Lobby)
                .WithMany()
                .HasForeignKey(game => game.LobbyIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GamePlayer>(entity =>
        {
            entity.HasKey(player => new { player.GameIdentifier, player.CharacterIdentifier });
            entity.HasOne(player => player.Game)
                .WithMany(game => game.Players)
                .HasForeignKey(player => player.GameIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(player => player.Character)
                .WithMany()
                .HasForeignKey(player => player.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameRound>(entity =>
        {
            entity.HasKey(round => new { round.GameIdentifier, round.CharacterIdentifier });
            entity.HasOne(round => round.Game)
                .WithMany()
                .HasForeignKey(round => round.GameIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(round => round.Character)
                .WithMany()
                .HasForeignKey(round => round.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
