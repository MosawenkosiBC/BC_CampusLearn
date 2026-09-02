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

        builder.Property(booking => booking.Location)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(booking => booking.Summary)
            .HasMaxLength(1000);

        builder.Property(booking => booking.MeetingLink)
            .HasMaxLength(2048);

        builder.Property(booking => booking.RowVersion)
            .IsRowVersion();

        builder.Property(booking => booking.DateBooked)
            .HasColumnType("datetimeoffset");

        builder.Property(booking => booking.CompletedAt)
            .HasColumnType("datetimeoffset");

        builder.Property(booking => booking.ScheduledStartTime)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Bookings_Duration_OneHour",
                "[Duration] = 1"));

        builder.HasIndex(booking => new
        {
            booking.TutorId,
            booking.ScheduledStartTime
        });

        builder.HasOne(booking => booking.ProgrammeModule)
            .WithMany(module => module.Bookings)
            .HasForeignKey(booking => booking.ProgrammeModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.TutorCourseModule)
            .WithMany(assignment => assignment.Bookings)
            .HasForeignKey(booking => new
            {
                booking.TutorId,
                booking.ProgrammeModuleId
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(booking => booking.StudentBcUser)
            .WithMany()
            .HasForeignKey(booking => booking.StudentBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
