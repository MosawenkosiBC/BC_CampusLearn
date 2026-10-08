using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SeniorTutorApplicationConfiguration
    : IEntityTypeConfiguration<SeniorTutorApplication>
{
    public void Configure(EntityTypeBuilder<SeniorTutorApplication> builder)
    {
        builder.HasKey(application => application.SeniorTutorApplicationId);
        builder.Property(application => application.AcademicAverage)
            .HasPrecision(5, 2)
            .IsRequired();
        builder.Property(application => application.BestDescription)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(application => application.SuitabilityReason)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(application => application.Status)
            .HasConversion<int>()
            .HasDefaultValue(TutorAccountRequestStatus.Pending)
            .IsConcurrencyToken();
        builder.Property(application => application.SubmittedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(application => application.ReviewedBy)
            .HasMaxLength(256);
        builder.Property(application => application.ReviewNote)
            .HasMaxLength(500);

        builder.HasIndex(application => new
        {
            application.TutorId,
            application.Status
        });

        builder.HasOne(application => application.Tutor)
            .WithMany(tutor => tutor.SeniorTutorApplications)
            .HasForeignKey(application => application.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
