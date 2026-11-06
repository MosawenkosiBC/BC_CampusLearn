using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorPersonalInformationConfiguration
    : IEntityTypeConfiguration<TutorPersonalInformation>
{
    public void Configure(EntityTypeBuilder<TutorPersonalInformation> builder)
    {
        builder.ToTable("TutorPersonalInformation");
        builder.HasKey(info => info.Id);
        builder.HasIndex(info => info.TutorId).IsUnique();
        builder.Property(info => info.Surname).HasMaxLength(100).IsRequired();
        builder.Property(info => info.FullNames).HasMaxLength(200).IsRequired();
        builder.Property(info => info.PhysicalHomeAddress).HasMaxLength(500).IsRequired();
        builder.Property(info => info.StreetAddress).HasMaxLength(200).IsRequired();
        builder.Property(info => info.Suburb).HasMaxLength(100).IsRequired();
        builder.Property(info => info.City).HasMaxLength(100).IsRequired();
        builder.Property(info => info.Province).HasMaxLength(100).IsRequired();
        builder.Property(info => info.Country).HasMaxLength(100).IsRequired();
        builder.Property(info => info.PostalCode).HasMaxLength(20).IsRequired();
        builder.Property(info => info.IncomeTaxNumber).HasMaxLength(50).IsRequired();
        builder.Property(info => info.HomeLanguage).HasMaxLength(100).IsRequired();
        builder.Property(info => info.Gender).HasMaxLength(50).IsRequired();
        builder.Property(info => info.Nationality).HasMaxLength(100).IsRequired();
        builder.Property(info => info.IdentityOrPassportNumber).HasMaxLength(100).IsRequired();
        builder.Property(info => info.AsylumNumber).HasMaxLength(100);
        builder.Property(info => info.EmergencyContactName).HasMaxLength(200).IsRequired();
        builder.Property(info => info.EmergencyContactPrimaryPhone).HasMaxLength(32).IsRequired();
        builder.Property(info => info.EmergencyContactRelationship).HasMaxLength(100).IsRequired();
        builder.Property(info => info.SignatureName).HasMaxLength(200);
        builder.Property(info => info.SignatureImage).HasColumnType("varbinary(max)");
        builder.Property(info => info.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(info => info.Tutor)
            .WithOne(tutor => tutor.PersonalInformation)
            .HasForeignKey<TutorPersonalInformation>(info => info.TutorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
