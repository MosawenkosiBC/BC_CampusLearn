using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class TutorApplicationSettingsConfiguration :
    IEntityTypeConfiguration<TutorApplicationSettings>
{
    public void Configure(
        EntityTypeBuilder<TutorApplicationSettings> builder)
    {
        builder.ToTable("TutorApplicationSettings", table =>
        {
            table.HasCheckConstraint(
                "CK_TutorApplicationSettings_Singleton",
                "[TutorApplicationSettingsId] = 1");
            table.HasCheckConstraint(
                "CK_TutorApplicationSettings_ShortlistLimit",
                "[ShortlistLimit] IS NULL OR [ShortlistLimit] > 0");
        });

        builder.HasKey(settings =>
            settings.TutorApplicationSettingsId);
        builder.Property(settings => settings.IsOpen)
            .HasDefaultValue(false);
        builder.Property(settings => settings.NotifyStudents)
            .HasDefaultValue(false);
        builder.Property(settings => settings.OpenDate)
            .HasColumnType("date");
        builder.Property(settings => settings.CloseDate)
            .HasColumnType("date");
        builder.Property(settings => settings.ContinueAfterShortlistLimit)
            .HasDefaultValue(false);
        builder.Property(settings => settings.UpdatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasData(new TutorApplicationSettings
        {
            TutorApplicationSettingsId =
                TutorApplicationSettings.SingletonId,
            IsOpen = false,
            NotifyStudents = false,
            ShortlistLimit = null,
            OpenDate = null,
            CloseDate = null,
            ContinueAfterShortlistLimit = false,
            UpdatedAt = new DateTime(2026, 9, 14, 0, 0, 0,
                DateTimeKind.Utc)
        });
    }
}
