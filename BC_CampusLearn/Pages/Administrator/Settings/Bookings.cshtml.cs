using System.ComponentModel.DataAnnotations;

using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class BookingsModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    SettingsAuditService auditService) : PageModel
{
    [BindProperty]
    public BookingTermsInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Input = await context.PlatformSettings.AsNoTracking()
            .Where(settings => settings.PlatformSettingsId == PlatformSettings.SingletonId)
            .Select(settings => new BookingTermsInput
            {
                Terms = settings.BookingTermsAndConditions,
                ReviewDeadline = settings.TutorHeadReviewDeadline,
                IsReviewDeadlineRecurring =
                    settings.IsTutorHeadReviewDeadlineRecurring,
                UseLastDayOfMonth = settings
                    .UseLastDayOfMonthForTutorHeadReviewDeadline,
                AdminReviewDeadline = settings.AdminSessionReviewDeadline,
                IsAdminReviewDeadlineRecurring =
                    settings.IsAdminSessionReviewDeadlineRecurring,
                UseLastDayOfMonthForAdminReview = settings
                    .UseLastDayOfMonthForAdminSessionReviewDeadline
            })
            .SingleAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        Input.Terms = NormalizeLines(Input.Terms ?? string.Empty);
        if (Input.Terms.Length is < 20 or > 8000)
        {
            ModelState.AddModelError(
                "Input.Terms",
                "Booking terms must contain between 20 and 8,000 characters.");
        }
        if (!ModelState.IsValid) return Page();

        CurrentUser currentUser = currentUserService.GetRequiredUser();
        PlatformSettings settings = await context.PlatformSettings.SingleAsync(
            item => item.PlatformSettingsId == PlatformSettings.SingletonId,
            cancellationToken);
        bool useLastDayOfMonth = Input.IsReviewDeadlineRecurring &&
            Input.UseLastDayOfMonth;
        DateOnly effectiveDeadline = MonthlyReviewPeriod.NormalizeDeadline(
            Input.ReviewDeadline,
            useLastDayOfMonth);
        bool useLastDayOfMonthForAdmin =
            Input.IsAdminReviewDeadlineRecurring &&
            Input.UseLastDayOfMonthForAdminReview;
        DateOnly effectiveAdminDeadline =
            MonthlyReviewPeriod.NormalizeDeadline(
                Input.AdminReviewDeadline,
                useLastDayOfMonthForAdmin);
        bool changed = auditService.Record(
            "Bookings and sessions",
            "Booking terms and conditions",
            settings.BookingTermsAndConditions,
            Input.Terms,
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Tutor Head review deadline",
            FormatDate(settings.TutorHeadReviewDeadline),
            FormatDate(effectiveDeadline),
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Tutor Head review deadline recurrence",
            settings.IsTutorHeadReviewDeadlineRecurring
                ? "Monthly"
                : "One time",
            Input.IsReviewDeadlineRecurring
                ? "Monthly"
                : "One time",
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Tutor Head review deadline monthly rule",
            settings.UseLastDayOfMonthForTutorHeadReviewDeadline
                ? "Last day of the month"
                : "Same date each month",
            useLastDayOfMonth
                ? "Last day of the month"
                : "Same date each month",
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Administrator review deadline",
            FormatDate(settings.AdminSessionReviewDeadline),
            FormatDate(effectiveAdminDeadline),
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Administrator review deadline recurrence",
            settings.IsAdminSessionReviewDeadlineRecurring
                ? "Monthly"
                : "One time",
            Input.IsAdminReviewDeadlineRecurring
                ? "Monthly"
                : "One time",
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Administrator review deadline monthly rule",
            settings.UseLastDayOfMonthForAdminSessionReviewDeadline
                ? "Last day of the month"
                : "Same date each month",
            useLastDayOfMonthForAdmin
                ? "Last day of the month"
                : "Same date each month",
            currentUser);
        DateOnly previousDeadline = MonthlyReviewPeriod.PreviousOccurrence(
            effectiveDeadline,
            useLastDayOfMonth);
        settings.BookingTermsAndConditions = Input.Terms;
        settings.TutorHeadReviewPeriodStartDate =
            previousDeadline.AddDays(1);
        settings.TutorHeadReviewPeriodEndDate =
            effectiveDeadline;
        settings.TutorHeadReviewDeadline = effectiveDeadline;
        settings.IsTutorHeadReviewDeadlineRecurring =
            Input.IsReviewDeadlineRecurring;
        settings.UseLastDayOfMonthForTutorHeadReviewDeadline =
            useLastDayOfMonth;
        settings.AdminSessionReviewDeadline = effectiveAdminDeadline;
        settings.IsAdminSessionReviewDeadlineRecurring =
            Input.IsAdminReviewDeadlineRecurring;
        settings.UseLastDayOfMonthForAdminSessionReviewDeadline =
            useLastDayOfMonthForAdmin;
        settings.UpdatedByBcUserId = currentUser.BcUserId;
        settings.UpdatedAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);

        SuccessMessage = changed
            ? "Booking and session settings saved."
            : "Booking and session settings are already up to date.";
        return RedirectToPage();
    }

    private static string NormalizeLines(string value) =>
        string.Join('\n', value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries));

    private static string FormatDate(DateOnly value) =>
        value.ToString("yyyy-MM-dd");

    public sealed class BookingTermsInput
    {
        [Required, StringLength(8000, MinimumLength = 20)]
        [Display(Name = "Booking terms and conditions")]
        public string Terms { get; set; } = string.Empty;

        [Display(Name = "Tutor Head review deadline")]
        public DateOnly ReviewDeadline { get; set; } = new(2026, 10, 5);

        [Display(Name = "Repeat this deadline monthly")]
        public bool IsReviewDeadlineRecurring { get; set; } = true;

        [Display(Name = "Use the last day of each month")]
        public bool UseLastDayOfMonth { get; set; }

        [Display(Name = "Administrator review deadline")]
        public DateOnly AdminReviewDeadline { get; set; } =
            new(2026, 10, 10);

        [Display(Name = "Repeat the administrator deadline monthly")]
        public bool IsAdminReviewDeadlineRecurring { get; set; } = true;

        [Display(Name = "Use the last day of each month")]
        public bool UseLastDayOfMonthForAdminReview { get; set; }
    }
}
