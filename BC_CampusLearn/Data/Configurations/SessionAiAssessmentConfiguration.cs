using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SessionAiAssessmentConfiguration
    : IEntityTypeConfiguration<SessionAiAssessment>
{
    public void Configure(EntityTypeBuilder<SessionAiAssessment> builder)
    {
        builder.ToTable("SessionAiAssessments");
        builder.HasKey(item => item.SessionAiAssessmentId);
        builder.Property(item => item.SessionAiAssessmentId)
            .ValueGeneratedOnAdd();
        builder.Property(item => item.AssessmentJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        builder.Property(item => item.GeneratedAt)
            .HasColumnType("datetimeoffset");
        builder.HasOne(item => item.Booking)
            .WithOne(booking => booking.AiAssessment)
            .HasForeignKey<SessionAiAssessment>(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.GeneratedBy)
            .WithMany()
            .HasForeignKey(item => item.GeneratedByBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.BookingId).IsUnique();
    }
}
