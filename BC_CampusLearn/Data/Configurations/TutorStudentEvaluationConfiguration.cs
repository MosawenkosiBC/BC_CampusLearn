using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorStudentEvaluationConfiguration
    : IEntityTypeConfiguration<TutorStudentEvaluation>
{
    public void Configure(
        EntityTypeBuilder<TutorStudentEvaluation> builder)
    {
        builder.ToTable("TutorStudentEvaluations");
        builder.HasKey(item => item.TutorEvaluationId);
        builder.Property(item => item.TutorEvaluationId)
            .ValueGeneratedOnAdd();
        builder.Property(item => item.PreviousHomework)
            .HasMaxLength(250);
        builder.Property(item => item.StudentInteract)
            .HasMaxLength(250)
            .IsRequired();
        builder.Property(item => item.StudentFocus)
            .HasMaxLength(250)
            .IsRequired();
        builder.Property(item => item.StudentIssues)
            .HasMaxLength(250)
            .IsRequired();
        builder.Property(item => item.TutorComments)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        builder.Property(item => item.RecordingLink)
            .HasMaxLength(2048)
            .IsRequired();
        builder.HasOne(item => item.Booking)
            .WithOne(booking => booking.TutorEvaluation)
            .HasForeignKey<TutorStudentEvaluation>(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.BookingId)
            .IsUnique();
    }
}
