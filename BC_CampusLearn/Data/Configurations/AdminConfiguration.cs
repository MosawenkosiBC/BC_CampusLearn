using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.ToTable("Admins");
        builder.HasKey(admin => admin.AdminId);
        builder.HasIndex(admin => admin.BcUserId).IsUnique();
        builder.Property(admin => admin.CreatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(admin => admin.BcUser)
            .WithOne(user => user.Admin)
            .HasForeignKey<Admin>(admin => admin.BcUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
