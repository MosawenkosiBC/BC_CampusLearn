using BC_CampusLearn.Data.Seed;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations
{
    public class ProgrammeModuleConfiguration
        : IEntityTypeConfiguration<ProgrammeModule>
{
    public void Configure(
        EntityTypeBuilder<ProgrammeModule> builder)
    {
        builder.ToTable("ProgrammeModule");

        /*
         * Composite primary key.
         *
         * This allows the table to have no separate Id column.
         *
         * The same module code may appear in different programmes,
         * but it cannot appear twice in the same programme.
         */
        builder.HasKey(module => new
        {
            module.ProgrammeId,
            module.ModuleCode
        });

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
            .WithMany(programme => programme.Modules)
            .HasForeignKey(module => module.ProgrammeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            ProgrammeModuleSeedData.GetModules()
        );
    }
}
}