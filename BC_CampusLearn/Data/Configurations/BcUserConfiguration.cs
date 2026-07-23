using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class BcUserConfiguration : IEntityTypeConfiguration<BcUser>
{
    public void Configure(EntityTypeBuilder<BcUser> builder)
    {
        builder.ToTable("BcUsers");
        builder.HasKey(user => user.BcUserId);
        builder.Property(user => user.PersonnelNumber).HasMaxLength(50).IsRequired();
        builder.Property(user => user.IsPublicActivityEnabled).HasDefaultValue(true);
        builder.Property(user => user.PublicActivityDisabledReason).HasMaxLength(500);
        builder.Property(user => user.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(user => user.PersonnelNumber).IsUnique();
        builder.HasIndex(user => new { user.EntraTenantId, user.EntraObjectId }).IsUnique();
    }
}
