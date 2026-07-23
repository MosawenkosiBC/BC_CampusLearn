using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorConfiguration : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> builder)
    {
        builder.HasKey(tutor => tutor.TutorId);

        builder.ToTable("Tutors", table =>
        {
            table.HasCheckConstraint("CK_Tutors_YearOfStudy", "[YearOfStudy] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_Tutors_OverallAverage", "[OverallAverage] BETWEEN 0 AND 100");
        });
        builder.HasIndex(tutor => tutor.BcUserId).IsUnique();
        builder.Property(tutor => tutor.OverallAverage).HasPrecision(5, 2).IsRequired();
        builder.Property(tutor => tutor.YearOfStudy).IsRequired();
        builder.Property(tutor => tutor.ReasonForTutoring).HasMaxLength(1000).IsRequired();
        builder.Property(tutor => tutor.TeachingStyle).HasMaxLength(1000).IsRequired();
        builder.Property(tutor => tutor.DemonstrationVideoUrl).HasMaxLength(500).IsRequired();
        builder.Property(tutor => tutor.Status).HasConversion<int>().HasDefaultValue(TutorStatus.Pending);
        builder.Property(tutor => tutor.IsActive).HasDefaultValue(false);
        builder.Property(tutor => tutor.SubmittedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(tutor => tutor.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(tutor => tutor.BcUser)
            .WithOne(user => user.Tutor)
            .HasForeignKey<Tutor>(tutor => tutor.BcUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(tutor => tutor.Programme)
            .WithMany(programme => programme.Tutors)
            .HasForeignKey(tutor => tutor.ProgrammeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(tutor => tutor.Biography)
            .HasMaxLength(500);

        builder.Property(tutor => tutor.ProfileImagePath)
            .HasMaxLength(500);

        //adding the links for the tutor profile
        builder.Property(tutor => tutor.LinkedInUrl)
            .HasMaxLength(500);

        builder.Property(tutor => tutor.GitHubUrl)
            .HasMaxLength(500);

    }
}
