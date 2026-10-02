using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class CampusEventDetailConfiguration
    : IEntityTypeConfiguration<CampusEventDetail>
{
    public void Configure(EntityTypeBuilder<CampusEventDetail> builder)
    {
        builder.ToTable("CampusEventDetail");
        builder.HasKey(item => item.CampusEventDetailId);
        builder.Property(item => item.Title).HasMaxLength(120).IsRequired();
        builder.Property(item => item.DataType)
            .HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.Value).HasMaxLength(2000).IsRequired();
        builder.HasOne(item => item.CampusEvent)
            .WithMany(item => item.Details)
            .HasForeignKey(item => item.CampusEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.CampusEventId, item.Position });
    }
}
