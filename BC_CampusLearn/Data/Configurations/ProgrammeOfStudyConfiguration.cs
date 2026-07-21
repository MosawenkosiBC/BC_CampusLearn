using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace BC_CampusLearn.Models.Entities
{
    public class ProgrammeOfStudyConfiguration
        : IEntityTypeConfiguration<ProgrammeOfStudy>
{
    public void Configure(
        EntityTypeBuilder<ProgrammeOfStudy> builder)
    {
        // Set the exact database table name
        builder.ToTable("ProgrammeOfStudy");

        // Configure the primary key
        builder.HasKey(programme => programme.Id);

        // Configure the Id column
        builder.Property(programme => programme.Id)
            .ValueGeneratedOnAdd();

        // Configure the Name column
        builder.Property(programme => programme.Name)
            .HasMaxLength(100)
            .IsRequired();

        // Prevent duplicate programme names
        builder.HasIndex(programme => programme.Name)
            .IsUnique();

        // Insert the initial programme names
        builder.HasData(
            new ProgrammeOfStudy
            {
                Id = 1,
                Name = "Bachelor of Computing"
            },
            new ProgrammeOfStudy
            {
                Id = 2,
                Name = "Bachelor of Information Technology"
            },
            new ProgrammeOfStudy
            {
                Id = 3,
                Name = "Diploma in Information Technology"
            },
            new ProgrammeOfStudy
            {
                Id = 4,
                Name = "Diploma for Deaf Students"
            }
        );
    }
}
}