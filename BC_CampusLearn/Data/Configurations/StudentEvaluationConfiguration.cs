using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class StudentEvaluationConfiguration
    : IEntityTypeConfiguration<StudentEvaluation>
{
    public void Configure(EntityTypeBuilder<StudentEvaluation> builder)
    {
        builder.ToTable("StudentEvaluations", table =>
        {
            table.HasCheckConstraint(
                "CK_StudentEvaluations_ModeRating",
                "[ModeRating] BETWEEN 1 AND 5");
            table.HasCheckConstraint(
                "CK_StudentEvaluations_PlatformRating",
                "[PlatformRating] BETWEEN 1 AND 5");
        });
        builder.HasKey(item => item.StudentEvaluationId);
        builder.Property(item => item.StudentEvaluationId).ValueGeneratedOnAdd();
        builder.Property(item => item.TutoringMode).HasMaxLength(30).IsRequired();
        builder.Property(item => item.PlatformExperience).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.TutorResponse).HasMaxLength(10).IsRequired();
        builder.Property(item => item.TutorInterest).HasMaxLength(10).IsRequired();
        builder.Property(item => item.TutorFriendliness).HasMaxLength(40).IsRequired();
        builder.Property(item => item.TutorExplanation).HasMaxLength(40).IsRequired();
        builder.Property(item => item.TutorParticipation).HasMaxLength(40).IsRequired();
        builder.Property(item => item.TutorPunctuality).HasMaxLength(10).IsRequired();
        builder.Property(item => item.TutorAdvice).HasMaxLength(40).IsRequired();
        builder.Property(item => item.TutorHelp).HasMaxLength(50).IsRequired();
        builder.Property(item => item.TutorTopic).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.TutoringService).HasMaxLength(10).IsRequired();
        builder.Property(item => item.ImproveBCProgramme).HasMaxLength(2000).IsRequired();
        builder.HasOne(item => item.Booking)
            .WithOne(booking => booking.StudentEvaluation)
            .HasForeignKey<StudentEvaluation>(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.BookingId).IsUnique();
    }
}
