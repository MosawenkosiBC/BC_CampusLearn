using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BC_CampusLearn.Data.Configurations;

public class PlatformSettingsConfiguration :
    IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        builder.ToTable("PlatformSettings", table =>
        {
            table.HasCheckConstraint(
                "CK_PlatformSettings_Singleton",
                "[PlatformSettingsId] = 1");
            table.HasCheckConstraint(
                "CK_PlatformSettings_AcademicYear",
                "[AcademicYear] BETWEEN 2000 AND 2200");
            table.HasCheckConstraint(
                "CK_PlatformSettings_TutorHeadReviewPeriod",
                "[TutorHeadReviewPeriodEndDate] >= " +
                "[TutorHeadReviewPeriodStartDate] AND " +
                "[TutorHeadReviewDeadline] >= " +
                "[TutorHeadReviewPeriodEndDate]");
            table.HasCheckConstraint(
                "CK_PlatformSettings_AdminSessionReviewPeriod",
                "[AdminSessionReviewDeadline] >= " +
                "[AdminSessionReviewPeriodStartDate]");
        });
        builder.HasKey(settings => settings.PlatformSettingsId);
        builder.Property(settings => settings.SupportEmail)
            .HasMaxLength(320).IsRequired();
        builder.Property(settings => settings.CampusTimeZoneId)
            .HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.CurrencyCode)
            .HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(settings => settings.AcademicSemester)
            .HasMaxLength(80).IsRequired();
        builder.Property(settings => settings.DateTimeFormat)
            .HasMaxLength(80).IsRequired();
        builder.Property(settings => settings.Announcement)
            .HasMaxLength(1000);
        builder.Property(settings => settings.BookingTermsAndConditions)
            .HasMaxLength(8000).IsRequired();
        builder.Property(settings => settings.UpdatedAt)
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(settings => settings.RowVersion).IsRowVersion();
        builder.Property(settings => settings.TutorPaymentAmount).HasPrecision(18, 2);
        builder.Property(settings => settings.SeniorTutorPaymentAmount).HasPrecision(18, 2);
        builder.Property(settings => settings.TutorHeadPaymentAmount).HasPrecision(18, 2);
        builder.HasOne(settings => settings.UpdatedBy)
            .WithMany()
            .HasForeignKey(settings => settings.UpdatedByBcUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasData(new PlatformSettings
        {
            PlatformSettingsId = PlatformSettings.SingletonId,
            SupportEmail = "tutors@belgiumcampus.ac.za",
            CampusTimeZoneId = "Africa/Johannesburg",
            CurrencyCode = "ZAR",
            AcademicYear = 2026,
            AcademicSemester = "Semester 1",
            DateTimeFormat = "dd MMMM yyyy, HH:mm",
            BookingTermsAndConditions = PlatformSettings.DefaultBookingTerms,
            TutorHeadReviewPeriodStartDate = new DateOnly(2026, 9, 1),
            TutorHeadReviewPeriodEndDate = new DateOnly(2026, 9, 30),
            TutorHeadReviewDeadline = new DateOnly(2026, 10, 5),
            IsTutorHeadReviewDeadlineRecurring = true,
            UseLastDayOfMonthForTutorHeadReviewDeadline = false,
            AdminSessionReviewDeadline = new DateOnly(2026, 10, 31),
            AdminSessionReviewPeriodStartDate = new DateOnly(2026, 9, 6),
            IsAdminSessionReviewDeadlineRecurring = true,
            UseLastDayOfMonthForAdminSessionReviewDeadline = true,
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
    }
}
