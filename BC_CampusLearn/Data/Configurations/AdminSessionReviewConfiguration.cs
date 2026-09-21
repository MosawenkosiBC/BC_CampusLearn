using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class AdminSessionReviewConfiguration
    : IEntityTypeConfiguration<AdminSessionReview>
{
    public void Configure(EntityTypeBuilder<AdminSessionReview> builder)
    {
        builder.ToTable("AdminSessionReview");
        builder.HasKey(item => item.AdminSessionReviewId);
        builder.Property(item => item.AdminSessionReviewId).ValueGeneratedOnAdd();
        builder.Property(item => item.RecordedAt).HasColumnType("datetimeoffset");
        builder.HasOne(item => item.Booking)
            .WithOne(booking => booking.AdminSessionReview)
            .HasForeignKey<AdminSessionReview>(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Reviewer)
            .WithMany()
            .HasForeignKey(item => item.ReviewerBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.BookingId).IsUnique();
    }
}
