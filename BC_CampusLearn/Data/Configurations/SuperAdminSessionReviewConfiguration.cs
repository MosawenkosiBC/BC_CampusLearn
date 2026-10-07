using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SuperAdminSessionReviewConfiguration
    : IEntityTypeConfiguration<SuperAdminSessionReview>
{
    public void Configure(EntityTypeBuilder<SuperAdminSessionReview> builder)
    {
        builder.ToTable("SuperAdminSessionReview");
        builder.HasKey(item => item.SuperAdminSessionReviewId);
        builder.Property(item => item.SuperAdminSessionReviewId)
            .ValueGeneratedOnAdd();
        builder.Property(item => item.RecordedAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.CompensationAmount).HasPrecision(18, 2);
        builder.HasOne(item => item.Booking)
            .WithOne(booking => booking.SuperAdminSessionReview)
            .HasForeignKey<SuperAdminSessionReview>(item => item.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Reviewer)
            .WithMany()
            .HasForeignKey(item => item.ReviewerBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.BookingId).IsUnique();
    }
}
