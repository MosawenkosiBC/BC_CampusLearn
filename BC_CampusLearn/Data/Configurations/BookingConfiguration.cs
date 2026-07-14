using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class BookingConfiguration
    : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(booking => booking.BookingId);

        builder.Property(booking => booking.StudentObjectId)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(booking => booking.StudentTenantId)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(booking => booking.StudentName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(booking => booking.StudentEmail)
            .HasMaxLength(320);

        builder.Property(booking => booking.Reason)
            .HasMaxLength(500);

        builder.Property(booking => booking.SessionStart)
            .HasColumnType("datetimeoffset");

        builder.Property(booking => booking.SessionEnd)
            .HasColumnType("datetimeoffset");

        builder.Property(booking => booking.CreatedAt)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(booking => booking.TutorAvailabilityId)
            .IsUnique();

        builder.HasOne(booking => booking.Tutor)
            .WithMany(tutor => tutor.Bookings)
            .HasForeignKey(booking => booking.TutorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.TutorAvailability)
            .WithOne(slot => slot.Booking)
            .HasForeignKey<Booking>(
                booking => booking.TutorAvailabilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}