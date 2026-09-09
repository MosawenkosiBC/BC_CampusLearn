using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class MeetingLinkConfiguration
    : IEntityTypeConfiguration<MeetingLink>
{
    public void Configure(EntityTypeBuilder<MeetingLink> builder)
    {
        builder.ToTable("MeetingLink");

        builder.HasKey(link => link.BookingId);

        builder.Property(link => link.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.HasOne(link => link.Booking)
            .WithOne(booking => booking.MeetingLink)
            .HasForeignKey<MeetingLink>(link => link.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
