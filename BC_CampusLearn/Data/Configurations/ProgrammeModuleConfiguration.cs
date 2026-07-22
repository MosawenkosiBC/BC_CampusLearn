using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class ProgrammeModuleConfiguration
    : IEntityTypeConfiguration<ProgrammeModule>
{
    public void Configure(
        EntityTypeBuilder<ProgrammeModule> builder)
    {
        builder.ToTable("ProgrammeModule");

        builder.HasKey(module => module.ProgrammeModuleId);

        builder.Property(module => module.ProgrammeModuleId)
            .ValueGeneratedOnAdd();

        builder.Property(module => module.ProgrammeId)
            .IsRequired();

        builder.Property(module => module.ModuleName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(module => module.ModuleCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(module => module.YearOfStudy)
            .IsRequired();

        builder.HasOne(module => module.Programme)
            .WithMany(programme => programme.ProgrammeModules)
            .HasForeignKey(module => module.ProgrammeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}