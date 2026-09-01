using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class BookingStatusHistoryConfiguration
    : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("BookingStatusHistory");
        builder.HasKey(item => item.BookingStatusHistoryId);
        builder.Property(item => item.ReasonCode).HasMaxLength(64);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.ChangedAt)
            .HasColumnType("datetimeoffset");
        builder.HasOne(item => item.Booking)
            .WithMany(booking => booking.StatusHistory)
            .HasForeignKey(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ChangedByBcUser)
            .WithMany()
            .HasForeignKey(item => item.ChangedByBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.BookingId, item.ChangedAt });
    }
}
