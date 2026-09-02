using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SessionMessageConfiguration
    : IEntityTypeConfiguration<SessionMessage>
{
    public void Configure(EntityTypeBuilder<SessionMessage> builder)
    {
        builder.ToTable("SessionMessages");
        builder.HasKey(item => item.SessionMessageId);
        builder.Property(item => item.MessageText)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(item => item.SentAt)
            .HasColumnType("datetimeoffset");
        builder.Property(item => item.EditedAt)
            .HasColumnType("datetimeoffset");
        builder.Property(item => item.DeletedAt)
            .HasColumnType("datetimeoffset");
        builder.Property(item => item.ReadAt)
            .HasColumnType("datetimeoffset");
        builder.HasOne(item => item.Booking)
            .WithMany(booking => booking.SessionMessages)
            .HasForeignKey(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Sender)
            .WithMany()
            .HasForeignKey(item => item.SenderBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Recipient)
            .WithMany()
            .HasForeignKey(item => item.RecipientBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.BookingId, item.SentAt });
        builder.HasIndex(item => new
        {
            item.RecipientBcUserId,
            item.ReadAt,
            item.SentAt
        });
    }
}
