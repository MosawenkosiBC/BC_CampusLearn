using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class BookingPreparationLinkConfiguration
    : IEntityTypeConfiguration<BookingPreparationLink>
{
    public void Configure(
        EntityTypeBuilder<BookingPreparationLink> builder)
    {
        builder.HasKey(link => link.BookingPreparationLinkId);

        builder.Property(link => link.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.HasIndex(link => new
        {
            link.BookingId,
            link.Position
        })
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BookingPreparationLinks_Position",
                "[Position] BETWEEN 1 AND 3"));

        builder.HasOne(link => link.Booking)
            .WithMany(booking => booking.PreparationLinks)
            .HasForeignKey(link => link.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
