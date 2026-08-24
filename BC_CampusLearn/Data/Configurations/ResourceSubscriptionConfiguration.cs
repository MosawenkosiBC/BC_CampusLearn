using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class ResourceSubscriptionConfiguration
    : IEntityTypeConfiguration<ResourceSubscription>
{
    public void Configure(EntityTypeBuilder<ResourceSubscription> builder)
    {
        builder.ToTable("ResourceSubscriptions");
        builder.HasKey(subscription => subscription.ResourceSubscriptionId);
        builder.Property(subscription => subscription.PersonnelNumber)
            .HasMaxLength(50).IsRequired();
        builder.Property(subscription => subscription.ModuleCode)
            .HasMaxLength(10).IsRequired();
        builder.Property(subscription => subscription.DateSubscribed)
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(subscription => subscription.IsActive)
            .HasDefaultValue(true);
        builder.HasIndex(subscription => new
        {
            subscription.PersonnelNumber,
            subscription.ModuleCode
        }).IsUnique();
    }
}
