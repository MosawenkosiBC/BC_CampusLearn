using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class CampusEventConfiguration : IEntityTypeConfiguration<CampusEvent>
{
    public void Configure(EntityTypeBuilder<CampusEvent> builder)
    {
        builder.ToTable("CampusEvent");
        builder.HasKey(item => item.CampusEventId);
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.Disclaimer).HasMaxLength(2000);
        builder.Property(item => item.Location).HasMaxLength(300).IsRequired();
        builder.Property(item => item.BannerImagePath).HasMaxLength(500).IsRequired();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(item => new { item.IsPublished, item.PublishAt, item.EndsAt });
    }
}
