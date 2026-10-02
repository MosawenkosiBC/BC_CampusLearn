using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class StudyAreaConfiguration
    : IEntityTypeConfiguration<StudyArea>
{
    public void Configure(EntityTypeBuilder<StudyArea> builder)
    {
        builder.HasKey(studyArea => studyArea.StudyAreaId);

        builder.Property(studyArea => studyArea.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(studyArea => studyArea.Description)
            .HasMaxLength(300);

        builder.HasIndex(studyArea => studyArea.Name)
            .IsUnique();

        builder.HasData(
            Create(1, "Online", null, 1),
            Create(
                2,
                "Chi study",
                "Located next to the Chi classroom.",
                2),
            Create(
                3,
                "Rou",
                "Located next to the Pi classroom on Main Campus.",
                3),
            Create(
                4,
                "Waterloop",
                "Located at the Waterloop residence.",
                4),
            Create(
                5,
                "Florenville",
                "Located at the Florenville residence.",
                5),
            Create(
                6,
                "West Campus",
                "Located at the West Campus residence.",
                6),
            Create(7, "Brugge", null, 7),
            Create(
                8,
                "Main Library & Study area",
                "Located next to the Academia building.",
                8));
    }

    private static StudyArea Create(
        int id,
        string name,
        string? description,
        int displayOrder) => new()
        {
            StudyAreaId = id,
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            IsActive = true
        };
}
