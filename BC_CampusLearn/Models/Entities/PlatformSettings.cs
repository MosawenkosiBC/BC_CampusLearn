namespace BC_CampusLearn.Models.Entities;

public class PlatformSettings
{
    public const int SingletonId = 1;

    public int PlatformSettingsId { get; set; } = SingletonId;
    public string SupportEmail { get; set; } = "tutors@belgiumcampus.ac.za";
    public string CampusTimeZoneId { get; set; } = "Africa/Johannesburg";
    public string CurrencyCode { get; set; } = "ZAR";
    public decimal? TutorPaymentAmount { get; set; }
    public decimal? TutorHeadPaymentAmount { get; set; }
    public int AcademicYear { get; set; } = DateTime.UtcNow.Year;
    public string AcademicSemester { get; set; } = "Semester 1";
    public string DateTimeFormat { get; set; } = "dd MMMM yyyy, HH:mm";
    public string? Announcement { get; set; }
    public bool IsAnnouncementEnabled { get; set; }
    public bool IsMaintenanceModeEnabled { get; set; }
    public string BookingTermsAndConditions { get; set; } = DefaultBookingTerms;
    public DateOnly TutorHeadReviewPeriodStartDate { get; set; } =
        new(2026, 9, 1);
    public DateOnly TutorHeadReviewPeriodEndDate { get; set; } =
        new(2026, 9, 30);
    public DateOnly TutorHeadReviewDeadline { get; set; } =
        new(2026, 10, 5);
    public bool IsTutorHeadReviewDeadlineRecurring { get; set; } = true;
    public bool UseLastDayOfMonthForTutorHeadReviewDeadline { get; set; }
    public DateOnly AdminSessionReviewDeadline { get; set; } =
        new(2026, 10, 31);
    public DateOnly AdminSessionReviewPeriodStartDate { get; set; } =
        new(2026, 9, 6);
    public bool IsAdminSessionReviewDeadlineRecurring { get; set; } = true;
    public bool UseLastDayOfMonthForAdminSessionReviewDeadline { get; set; } = true;
    public int? UpdatedByBcUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public BcUser? UpdatedBy { get; set; }

    public const string DefaultBookingTerms =
        "All appointments with tutors must be scheduled a day ahead.\n" +
        "All sessions are limited to 1 hour.\n" +
        "You must come prepared for the sessions.\n" +
        "You will be given exercises to complete during your sessions.\n" +
        "All online sessions via MS Teams are recorded.\n" +
        "All face-to-face sessions are held in the study room.\n" +
        "Respect the time and effort of your tutor.\n" +
        "You will be required to complete a tutor evaluation form.";
}
