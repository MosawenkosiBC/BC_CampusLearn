using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public sealed class EmailNotificationOutboxConfiguration
    : IEntityTypeConfiguration<EmailNotificationOutbox>
{
    public void Configure(EntityTypeBuilder<EmailNotificationOutbox> builder)
    {
        builder.ToTable("EmailNotificationOutbox");
        builder.HasKey(item => item.EmailNotificationOutboxId);
        builder.Property(item => item.EventKey).HasMaxLength(300).IsRequired();
        builder.Property(item => item.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(item => item.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Subject).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Body).HasMaxLength(8000).IsRequired();
        builder.Property(item => item.ActionPath).HasMaxLength(2048);
        builder.Property(item => item.CreatedAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.NextAttemptAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.SentAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.FailedAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.LastError).HasMaxLength(500);
        builder.HasIndex(item => item.EventKey).IsUnique();
        builder.HasIndex(item => new
        {
            item.SentAt,
            item.FailedAt,
            item.NextAttemptAt
        });
    }
}
