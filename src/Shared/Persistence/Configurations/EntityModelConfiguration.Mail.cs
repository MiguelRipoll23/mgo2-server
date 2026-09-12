using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Configurations;

/// <summary>Mailbox mappings.</summary>
internal static partial class EntityModelConfiguration
{
    private static void ConfigureMail(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailMessage>(entity =>
        {
            entity.HasOne(mail => mail.Sender)
                .WithMany()
                .HasForeignKey(mail => mail.SenderCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(mail => mail.Recipient)
                .WithMany()
                .HasForeignKey(mail => mail.RecipientCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GameMasterMail>(entity =>
        {
            entity.HasOne(mail => mail.Sender)
                .WithMany()
                .HasForeignKey(mail => mail.SenderCharacterIdentifier)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
