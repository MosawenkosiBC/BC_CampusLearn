using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class CourseModuleConfiguration
    : IEntityTypeConfiguration<CourseModule>
{
    public void Configure(EntityTypeBuilder<CourseModule> builder)
    {
        builder.HasKey(module => module.CourseModuleId);

        builder.Property(module => module.Code)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(module => module.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(module => module.Code)
            .IsUnique();
    }
}