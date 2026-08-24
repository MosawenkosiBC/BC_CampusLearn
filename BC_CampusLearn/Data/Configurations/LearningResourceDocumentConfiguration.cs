using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class LearningResourceDocumentConfiguration
    : IEntityTypeConfiguration<LearningResourceDocument>
{
    public void Configure(EntityTypeBuilder<LearningResourceDocument> builder)
    {
        builder.ToTable("LearningResourceDocuments");
        builder.HasKey(document => document.ResourceDocumentId);
        builder.Property(document => document.DocumentName)
            .HasMaxLength(255).IsRequired();
        builder.Property(document => document.FileUrl)
            .HasMaxLength(1000).IsRequired();
        builder.Property(document => document.FileType)
            .HasMaxLength(20).IsRequired();
        builder.Property(document => document.DateUploaded)
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(document => document.Resource)
            .WithMany(resource => resource.Documents)
            .HasForeignKey(document => document.ResourceId)
            .HasPrincipalKey(resource => resource.LearningResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
