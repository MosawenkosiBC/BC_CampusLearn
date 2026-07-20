using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class BookingDocumentConfiguration
    : IEntityTypeConfiguration<BookingDocument>
{
    public void Configure(
        EntityTypeBuilder<BookingDocument> builder)
    {
        builder.HasKey(document => document.BookingDocumentId);

        builder.Property(document => document.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(document => document.StoragePath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(document => document.UploadedAt)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(document => new
        {
            document.BookingId,
            document.Position
        })
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_BookingDocuments_Position",
                "[Position] BETWEEN 1 AND 2"));

        builder.HasOne(document => document.Booking)
            .WithMany(booking => booking.Documents)
            .HasForeignKey(document => document.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
