using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class ResourceTutorNominationConfiguration
    : IEntityTypeConfiguration<ResourceTutorNomination>
{
    public void Configure(EntityTypeBuilder<ResourceTutorNomination> builder)
    {
        builder.HasKey(item => new
        {
            item.TutorId,
            item.ProgrammeModuleId
        });

        builder.Property(item => item.IsActive)
            .HasDefaultValue(true)
            .IsConcurrencyToken();

        builder.HasOne(item => item.Tutor)
            .WithMany(tutor => tutor.ResourceTutorNominations)
            .HasForeignKey(item => item.TutorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.ProgrammeModule)
            .WithMany(module => module.ResourceTutorNominations)
            .HasForeignKey(item => item.ProgrammeModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BcUser>()
            .WithMany()
            .HasForeignKey(item => item.NominatedByBcUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BcUser>()
            .WithMany()
            .HasForeignKey(item => item.DenominatedByBcUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
