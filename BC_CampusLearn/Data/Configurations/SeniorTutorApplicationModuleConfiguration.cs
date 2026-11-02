using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class SeniorTutorApplicationModuleConfiguration
    : IEntityTypeConfiguration<SeniorTutorApplicationModule>
{
    public void Configure(EntityTypeBuilder<SeniorTutorApplicationModule> builder)
    {
        builder.HasKey(item => new
        {
            item.SeniorTutorApplicationId,
            item.ProgrammeModuleId
        });

        builder.HasOne(item => item.SeniorTutorApplication)
            .WithMany(application => application.RequestedModules)
            .HasForeignKey(item => item.SeniorTutorApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.ProgrammeModule)
            .WithMany(module => module.SeniorTutorApplicationModules)
            .HasForeignKey(item => item.ProgrammeModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
