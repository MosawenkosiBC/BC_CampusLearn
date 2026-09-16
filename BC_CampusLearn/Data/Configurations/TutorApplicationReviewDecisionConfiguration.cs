using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public sealed class TutorApplicationReviewDecisionConfiguration
    : IEntityTypeConfiguration<TutorApplicationReviewDecision>
{
    public void Configure(
        EntityTypeBuilder<TutorApplicationReviewDecision> builder)
    {
        builder.ToTable("TutorApplicationReviewDecisions");
        builder.HasKey(item => item.TutorApplicationReviewDecisionId);
        builder.Property(item => item.PreviousStage)
            .HasConversion<int>();
        builder.Property(item => item.NewStage)
            .HasConversion<int>();
        builder.Property(item => item.Reason)
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(item => item.AdminName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(item => item.ReviewedAt)
            .HasColumnType("datetime2");

        builder.HasOne(item => item.Tutor)
            .WithMany(tutor => tutor.ApplicationReviewDecisions)
            .HasForeignKey(item => item.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Reviewer)
            .WithMany(user => user.TutorApplicationReviewDecisions)
            .HasForeignKey(item => item.ReviewerBcUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new
        {
            item.TutorId,
            item.ReviewedAt
        });
    }
}
