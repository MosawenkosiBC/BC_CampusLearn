using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorAvailabilityConfiguration
    : IEntityTypeConfiguration<TutorAvailability>
{
    public void Configure(
        EntityTypeBuilder<TutorAvailability> builder)
    {
        builder.HasKey(slot => slot.TutorAvailabilityId);

        builder.Property(slot => slot.StartTime)
            .HasColumnType("datetimeoffset");

        builder.Property(slot => slot.EndTime)
            .HasColumnType("datetimeoffset");

        builder.Property(slot => slot.RowVersion)
            .IsRowVersion();

        builder.HasOne(slot => slot.Tutor)
            .WithMany(tutor => tutor.AvailabilitySlots)
            .HasForeignKey(slot => slot.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}