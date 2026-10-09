using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class NotificationTemplateSettingConfiguration : IEntityTypeConfiguration<NotificationTemplateSetting>
{
    public void Configure(EntityTypeBuilder<NotificationTemplateSetting> builder)
    {
        builder.ToTable("NotificationTemplateSettings");
        builder.HasKey(item => item.TemplateKey);
        builder.Property(item => item.TemplateKey).HasMaxLength(100);
        builder.Property(item => item.Message).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetimeoffset");
        builder.HasOne(item => item.UpdatedBy).WithMany()
            .HasForeignKey(item => item.UpdatedByBcUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
