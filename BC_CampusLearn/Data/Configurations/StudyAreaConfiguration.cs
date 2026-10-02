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

        builder.HasIndex(studyArea => studyArea.Name)
            .IsUnique();

        builder.HasData(
            Create(1, "Online", 1),
            Create(2, "Chi study", 2),
            Create(3, "Rou", 3),
            Create(4, "Waterloop", 4),
            Create(5, "Flourrenville", 5),
            Create(6, "West Campus", 6),
            Create(7, "Brugge", 7),
            Create(8, "Main Library & Study area", 8));
    }

    private static StudyArea Create(
        int id,
        string name,
        int displayOrder) => new()
        {
            StudyAreaId = id,
            Name = name,
            DisplayOrder = displayOrder,
            IsActive = true
        };
}
