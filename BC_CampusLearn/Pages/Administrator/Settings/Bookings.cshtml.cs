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
                PeriodStartDate = settings.TutorHeadReviewPeriodStartDate,
                PeriodEndDate = settings.TutorHeadReviewPeriodEndDate,
                ReviewDeadline = settings.TutorHeadReviewDeadline
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
        if (Input.PeriodEndDate < Input.PeriodStartDate)
        {
            ModelState.AddModelError(
                "Input.PeriodEndDate",
                "The period end date must be on or after the start date.");
        }
        if (Input.ReviewDeadline < Input.PeriodEndDate)
        {
            ModelState.AddModelError(
                "Input.ReviewDeadline",
                "The review deadline must be on or after the period end date.");
        }
        if (!ModelState.IsValid) return Page();

        CurrentUser currentUser = currentUserService.GetRequiredUser();
        PlatformSettings settings = await context.PlatformSettings.SingleAsync(
            item => item.PlatformSettingsId == PlatformSettings.SingletonId,
            cancellationToken);
        bool changed = auditService.Record(
            "Bookings and sessions",
            "Booking terms and conditions",
            settings.BookingTermsAndConditions,
            Input.Terms,
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Tutor Head review period start date",
            FormatDate(settings.TutorHeadReviewPeriodStartDate),
            FormatDate(Input.PeriodStartDate),
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Tutor Head review period end date",
            FormatDate(settings.TutorHeadReviewPeriodEndDate),
            FormatDate(Input.PeriodEndDate),
            currentUser);
        changed |= auditService.Record(
            "Bookings and sessions",
            "Tutor Head review deadline",
            FormatDate(settings.TutorHeadReviewDeadline),
            FormatDate(Input.ReviewDeadline),
            currentUser);
        settings.BookingTermsAndConditions = Input.Terms;
        settings.TutorHeadReviewPeriodStartDate = Input.PeriodStartDate;
        settings.TutorHeadReviewPeriodEndDate = Input.PeriodEndDate;
        settings.TutorHeadReviewDeadline = Input.ReviewDeadline;
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

        [Display(Name = "Period start date")]
        public DateOnly PeriodStartDate { get; set; } = new(2026, 9, 1);

        [Display(Name = "Period end/cut-off date")]
        public DateOnly PeriodEndDate { get; set; } = new(2026, 9, 30);

        [Display(Name = "Tutor Head review deadline")]
        public DateOnly ReviewDeadline { get; set; } = new(2026, 10, 5);
    }
}
