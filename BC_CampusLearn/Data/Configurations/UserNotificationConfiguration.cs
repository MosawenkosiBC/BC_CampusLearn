using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class UserNotificationConfiguration
    : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.HasKey(item => item.UserNotificationId);
        builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(1400).IsRequired();
        builder.Property(item => item.LinkUrl).HasMaxLength(2048).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.ReadAt).HasColumnType("datetimeoffset");
        builder.HasOne(item => item.Recipient)
            .WithMany(user => user.Notifications)
            .HasForeignKey(item => item.RecipientBcUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new
        {
            item.RecipientBcUserId,
            item.ReadAt,
            item.CreatedAt
        });
    }
}
