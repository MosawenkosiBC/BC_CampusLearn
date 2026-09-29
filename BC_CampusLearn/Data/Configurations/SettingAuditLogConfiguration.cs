using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SettingAuditLogConfiguration :
    IEntityTypeConfiguration<SettingAuditLog>
{
    public void Configure(EntityTypeBuilder<SettingAuditLog> builder)
    {
        builder.ToTable("SettingAuditLogs");
        builder.HasKey(log => log.SettingAuditLogId);
        builder.Property(log => log.Category).HasMaxLength(80).IsRequired();
        builder.Property(log => log.SettingName).HasMaxLength(160).IsRequired();
        builder.Property(log => log.PreviousValue).HasMaxLength(4000);
        builder.Property(log => log.NewValue).HasMaxLength(4000);
        builder.Property(log => log.ChangedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(log => log.Reason).HasMaxLength(1000);
        builder.HasIndex(log => log.ChangedAt);
        builder.HasOne(log => log.ChangedBy)
            .WithMany(user => user.SettingAuditLogs)
            .HasForeignKey(log => log.ChangedByBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
