using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorConfiguration : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> builder)
    {
        builder.HasKey(tutor => tutor.TutorId);

        builder.Property(tutor => tutor.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(tutor => tutor.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(tutor => tutor.Biography)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(tutor => tutor.ProfileImagePath)
            .HasMaxLength(500);

        builder.Property(tutor => tutor.EntraObjectId)
            .HasMaxLength(36);

        builder.Property(tutor => tutor.EntraTenantId)
            .HasMaxLength(36);

        //adding the links for the tutor profile
        builder.Property(tutor => tutor.LinkedInUrl)
            .HasMaxLength(500);

        builder.Property(tutor => tutor.GitHubUrl)
            .HasMaxLength(500);


        builder.HasIndex(tutor => new
        {
            tutor.EntraTenantId,
            tutor.EntraObjectId
        })
            .IsUnique()
            .HasFilter(
                "[EntraTenantId] IS NOT NULL AND [EntraObjectId] IS NOT NULL");
    }
}