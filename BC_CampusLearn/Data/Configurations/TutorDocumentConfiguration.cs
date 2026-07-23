using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorDocumentConfiguration : IEntityTypeConfiguration<TutorDocument>
{
    public void Configure(EntityTypeBuilder<TutorDocument> builder)
    {
        builder.ToTable("TutorDocuments");
        builder.HasKey(document => document.TutorDocumentId);
        builder.Property(document => document.DocumentType).HasConversion<int>();
        builder.Property(document => document.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(document => document.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.IsVerified).HasDefaultValue(false);
        builder.Property(document => document.UploadedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(document => document.Tutor)
            .WithMany(tutor => tutor.TutorDocuments)
            .HasForeignKey(document => document.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
