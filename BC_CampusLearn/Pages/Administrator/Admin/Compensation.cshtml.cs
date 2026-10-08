using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using BC_CampusLearn.Services.Compensation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Admin;

[Authorize(Roles = nameof(BcUserRole.SuperAdmin))]
public class CompensationModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : PageModel
{
    public static readonly TimeSpan CampusOffset = TimeSpan.FromHours(2);
    public const int PageSize = 11;

    [BindProperty(SupportsGet = true)]
    public string? Period { get; set; } = "current";
    [BindProperty(SupportsGet = true)]
    public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)]
    public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)]
    public int TutorPage { get; set; } = 1;
    public int TotalPages { get; private set; } = 1;

    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public string CurrencyCode { get; private set; } = "ZAR";
    public string PeriodLabel => $"{PeriodStart:dd MMM yyyy} – {PeriodEnd:dd MMM yyyy}";
    public IReadOnlyList<EarningSession> Sessions { get; private set; } = [];
    public IReadOnlyList<TutorEarnings> Tutors { get; private set; } = [];
    public IReadOnlyList<TutorEarnings> PagedTutors { get; private set; } = [];
    public int ApprovedSessionCount => Sessions.Count;
    public int TutorCount => Tutors.Count;
    public int UnpricedSessionCount => Sessions.Count(session => session.Amount is null);
    public decimal? TotalEarnings => UnpricedSessionCount > 0 ? null : Sessions.Sum(session => session.Amount ?? 0m);
    public bool CanExport => ModelState.IsValid && UnpricedSessionCount == 0;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.GetRequiredUser().Role != BcUserRole.SuperAdmin) return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.GetRequiredUser().Role != BcUserRole.SuperAdmin) return Forbid();
        await LoadAsync(cancellationToken);
        if (!ModelState.IsValid) return Page();
        if (UnpricedSessionCount > 0)
        {
            ModelState.AddModelError(string.Empty,
                "Set the tutor payment amount in Settings before downloading this report.");
            return Page();
        }

        return File(CompensationWorkbook.Create(PeriodStart, PeriodEnd, CurrencyCode,
            Tutors, Sessions, timeProvider.GetUtcNow()),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Tutor-compensation_{PeriodStart:yyyy-MM-dd}_to_{PeriodEnd:yyyy-MM-dd}.xlsx");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var settings = await context.PlatformSettings.AsNoTracking()
            .SingleAsync(settings => settings.PlatformSettingsId == PlatformSettings.SingletonId,
                cancellationToken);
        CurrencyCode = settings.CurrencyCode;
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(CampusOffset).DateTime);
        var configured = settings.IsAdminSessionReviewDeadlineRecurring
            ? MonthlyReviewPeriod.ResolveByCalendarMonth(settings.AdminSessionReviewPeriodStartDate,
                settings.AdminSessionReviewDeadline, settings.UseLastDayOfMonthForAdminSessionReviewDeadline, today)
            : new ReviewPeriodWindow(settings.AdminSessionReviewPeriodStartDate,
                settings.AdminSessionReviewDeadline, settings.AdminSessionReviewDeadline);

        if (Period == "custom")
        {
            if (!From.HasValue || !To.HasValue)
                ModelState.AddModelError(string.Empty, "Choose both a start date and an end date.");
            else if (From > To)
                ModelState.AddModelError(string.Empty, "The start date must be on or before the end date.");
            PeriodStart = From ?? configured.StartDate;
            PeriodEnd = To ?? configured.EndDate;
        }
        else
        {
            Period = "current";
            PeriodStart = configured.StartDate;
            PeriodEnd = configured.EndDate;
            From = PeriodStart;
            To = PeriodEnd;
        }
        if (PeriodEnd == DateOnly.MaxValue || PeriodStart > PeriodEnd)
            ModelState.AddModelError(string.Empty, "Choose a valid date period ending before 31 December 9999.");
        if (!ModelState.IsValid) return;

        var start = new DateTimeOffset(PeriodStart.ToDateTime(TimeOnly.MinValue), CampusOffset);
        var end = new DateTimeOffset(PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), CampusOffset);
        Sessions = await context.Bookings.AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Completed &&
                booking.SuperAdminSessionReview != null && booking.SuperAdminSessionReview.IsAccepted &&
                (booking.CompletedAt ?? booking.ScheduledStartTime) >= start &&
                (booking.CompletedAt ?? booking.ScheduledStartTime) < end)
            .OrderBy(booking => booking.CompletedAt ?? booking.ScheduledStartTime)
            .ThenBy(booking => booking.BookingId)
            .Select(booking => new EarningSession(booking.BookingId, booking.TutorId,
                booking.TutorCourseModule.Tutor.BcUser.DisplayName,
                booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber ?? "",
                booking.TutorCourseModule.Tutor.BcUser.Email ?? "",
                booking.TutorCourseModule.Tutor.CampusOfStudy,
                booking.ProgrammeModule.ModuleCode,
                booking.CompletedAt ?? booking.ScheduledStartTime,
                booking.SuperAdminSessionReview!.RecordedAt,
                booking.SuperAdminSessionReview.CompensationAmount,
                booking.TutorCourseModule.Tutor.BcUser.Role))
            .ToListAsync(cancellationToken);

        Tutors = Sessions.GroupBy(session => session.TutorId)
            .Select(group => new TutorEarnings(group.Key, group.First().TutorName,
                group.First().PersonnelNumber, group.First().Email, group.First().Campus,
                group.Count(), group.Any(session => session.Amount is null)
                    ? null : group.Sum(session => session.Amount ?? 0m), group.First().TutorRole))
            .OrderBy(tutor => tutor.Name).ThenBy(tutor => tutor.TutorId).ToList();
        TotalPages = Math.Max(1, (int)Math.Ceiling(Tutors.Count / (double)PageSize));
        TutorPage = Math.Clamp(TutorPage, 1, TotalPages);
        PagedTutors = Tutors.Skip((TutorPage - 1) * PageSize).Take(PageSize).ToList();
    }

    public sealed record TutorEarnings(int TutorId, string Name, string PersonnelNumber,
        string Email, string Campus, int ApprovedSessions, decimal? Earnings, BcUserRole TutorRole = BcUserRole.Tutor)
    {
        public string RoleLabel => TutorRole switch
        {
            BcUserRole.HeadOfTutors => "Head of Tutor",
            BcUserRole.SeniorTutor => "Senior Tutor",
            _ => "Tutor"
        };
    }
    public sealed record EarningSession(int BookingId, int TutorId, string TutorName,
        string PersonnelNumber, string Email, string Campus, string ModuleCode,
        DateTimeOffset SessionDate, DateTimeOffset ApprovedAt, decimal? Amount, BcUserRole TutorRole = BcUserRole.Tutor);
}
