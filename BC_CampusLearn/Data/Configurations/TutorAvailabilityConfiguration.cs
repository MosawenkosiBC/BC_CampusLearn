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

        builder.Property(slot => slot.AvailableTime)
            .HasColumnType("datetimeoffset");

        builder.Property(slot => slot.RowVersion)
            .IsRowVersion();

        builder.HasOne(slot => slot.Tutor)
            .WithMany(tutor => tutor.TutorAvailabilities)
            .HasForeignKey(slot => slot.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(slot => slot.ProgrammeModule)
            .WithMany(module => module.TutorAvailabilities)
            .HasForeignKey(slot => slot.ProgrammeModuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(slot => new
        {
            slot.TutorId,
            slot.ProgrammeModuleId,
            slot.AvailableTime
        })
            .IsUnique();
    }
}
