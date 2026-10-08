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
        builder.Property(user => user.PersonnelNumber).HasMaxLength(50);
        builder.Property(user => user.EncryptedEntraTenantId).HasMaxLength(512);
        builder.Property(user => user.EncryptedEntraObjectId).HasMaxLength(512);
        builder.Property(user => user.EntraIdentityLookupHash)
            .HasMaxLength(64)
            .IsFixedLength();
        builder.Property(user => user.EncryptedGeminiApiKey)
            .HasMaxLength(2048);
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(320);
        builder.Property(user => user.Role)
            .HasConversion<int>()
            .HasDefaultValue(BcUserRole.Student)
            .HasSentinel((BcUserRole)0)
            .IsRequired();
        builder.Property(user => user.IsAdministrativeAccessActive)
            .HasDefaultValue(true);
        builder.Property(user => user.IsPublicActivityEnabled).HasDefaultValue(true);
        builder.Property(user => user.PublicActivityDisabledReason).HasMaxLength(500);
        builder.Property(user => user.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(user => user.SessionReviewsLastViewedAt)
            .HasColumnType("datetimeoffset");
        builder.HasIndex(user => user.PersonnelNumber)
            .IsUnique()
            .HasFilter("[PersonnelNumber] IS NOT NULL");
        builder.HasIndex(user => user.EntraIdentityLookupHash)
            .IsUnique()
            .HasFilter("[EntraIdentityLookupHash] IS NOT NULL");
        builder.ToTable("BcUsers", table =>
        {
            table.HasCheckConstraint(
                "CK_BcUsers_Role",
                "[Role] BETWEEN 1 AND 7");
        });
    }
}
