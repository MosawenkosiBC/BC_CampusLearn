using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class LearningResourceConfiguration
    : IEntityTypeConfiguration<LearningResource>
{
    public void Configure(EntityTypeBuilder<LearningResource> builder)
    {
        builder.ToTable("LearningResource");
        builder.HasKey(resource => resource.LearningResourceId);
        builder.Property(resource => resource.LearningResourceId)
            .ValueGeneratedOnAdd();
        builder.Property(resource => resource.Topic)
            .HasMaxLength(200).IsRequired();
        builder.Property(resource => resource.Content).IsRequired();
        builder.Property(resource => resource.Link1).HasMaxLength(1000);
        builder.Property(resource => resource.Link2).HasMaxLength(1000);
        builder.Property(resource => resource.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(resource => resource.DateCreated)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(resource => resource.Tutor)
            .WithMany(tutor => tutor.LearningResources)
            .HasForeignKey(resource => resource.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(resource => resource.ProgrammeModule)
            .WithMany(module => module.LearningResources)
            .HasForeignKey(resource => resource.ProgrammeModuleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(resource => new
        {
            resource.TutorId,
            resource.Status,
            resource.DateCreated
        });
    }
}
