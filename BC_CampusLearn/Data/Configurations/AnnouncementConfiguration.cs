using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasKey(item => item.AnnouncementId);
        builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Content).IsRequired();
        builder.Property(item => item.SentAt).HasColumnType("datetimeoffset");
        builder.HasOne(item => item.Sender).WithMany()
            .HasForeignKey(item => item.SenderBcUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Notifications).WithOne(item => item.Announcement)
            .HasForeignKey(item => item.AnnouncementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.SentAt);
    }
}
