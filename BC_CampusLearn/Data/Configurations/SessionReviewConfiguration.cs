using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SessionReviewConfiguration
    : IEntityTypeConfiguration<SessionReview>
{
    public void Configure(EntityTypeBuilder<SessionReview> builder)
    {
        builder.ToTable("SessionReviews", table =>
            table.HasCheckConstraint(
                "CK_SessionReviews_Rating",
                "[Rating] BETWEEN 1 AND 5"));
        builder.HasKey(item => item.SessionReviewId);
        builder.Property(item => item.Comment).HasMaxLength(2000);
        builder.Property(item => item.CreatedAt)
            .HasColumnType("datetimeoffset");
        builder.HasOne(item => item.Booking)
            .WithMany(booking => booking.SessionReviews)
            .HasForeignKey(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Reviewer)
            .WithMany()
            .HasForeignKey(item => item.ReviewerBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Reviewee)
            .WithMany()
            .HasForeignKey(item => item.RevieweeBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new
        {
            item.BookingId,
            item.ReviewerBcUserId
        }).IsUnique();
    }
}
