using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Character mappings, including the customisation, progression and
/// social tables that hang off a character.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureCharacters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Character>(entity =>
        {
            entity.HasIndex(character => character.Name).IsUnique();
            // The user and character tables reference each other, so this side
            // of the cycle cannot cascade.
            entity.HasOne(character => character.User)
                .WithMany(user => user.Characters)
                .HasForeignKey(character => character.UserIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(character => character.Lobby)
                .WithMany()
                .HasForeignKey(character => character.LobbyIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CharacterAppearance>(entity =>
        {
            entity.HasIndex(appearance => appearance.CharacterIdentifier).IsUnique();
            entity.HasOne(appearance => appearance.Character)
                .WithOne(character => character.Appearance)
                .HasForeignKey<CharacterAppearance>(appearance => appearance.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterStatistics>(entity =>
        {
            entity.HasIndex(statistics => statistics.CharacterIdentifier).IsUnique();
            entity.HasOne(statistics => statistics.Character)
                .WithOne(character => character.Statistics)
                .HasForeignKey<CharacterStatistics>(statistics => statistics.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterFriend>(entity =>
        {
            entity.HasIndex(friend => new { friend.CharacterIdentifier, friend.TargetIdentifier }).IsUnique();
            entity.HasOne(friend => friend.Character)
                .WithMany()
                .HasForeignKey(friend => friend.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(friend => friend.Target)
                .WithMany()
                .HasForeignKey(friend => friend.TargetIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CharacterSkillSet>(entity =>
        {
            entity.HasOne(skillSet => skillSet.Character)
                .WithMany()
                .HasForeignKey(skillSet => skillSet.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterEquippedSkill>(entity =>
        {
            entity.HasOne(equipped => equipped.Character)
                .WithOne()
                .HasForeignKey<CharacterEquippedSkill>(equipped => equipped.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterGearSet>(entity =>
        {
            entity.HasOne(gearSet => gearSet.Character)
                .WithMany()
                .HasForeignKey(gearSet => gearSet.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterHostSettings>(entity =>
        {
            entity.HasOne(settings => settings.Character)
                .WithMany()
                .HasForeignKey(settings => settings.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterChatMacro>(entity =>
        {
            entity.HasOne(macro => macro.Character)
                .WithMany()
                .HasForeignKey(macro => macro.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterConnection>(entity =>
        {
            entity.HasOne(connection => connection.Character)
                .WithOne()
                .HasForeignKey<CharacterConnection>(connection => connection.CharacterIdentifier)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
