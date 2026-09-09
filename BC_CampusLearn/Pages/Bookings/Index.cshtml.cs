using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BC_CampusLearn.Services.Sessions;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class IndexModel : PageModel
{
    private static readonly TimeSpan SouthAfricaOffset =
        TimeSpan.FromHours(2);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionLifecycleService _lifecycleService;

    public IndexModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISessionLifecycleService lifecycleService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _lifecycleService = lifecycleService;
    }

    [BindProperty(SupportsGet = true)]
    public string? TutorFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ModuleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? LocationFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public BookingStatus? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool PendingReviewOnly { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "date";

    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = "desc";

    public IReadOnlyList<BookingListItemViewModel> Bookings
    { get; private set; } = new List<BookingListItemViewModel>();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IEnumerable<SelectListItem> StatusOptions =>
        Enum.GetValues<BookingStatus>()
            .Select(status => new SelectListItem(
                status.ToDisplayText(),
                status.ToString()));

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(TutorFilter) ||
        !string.IsNullOrWhiteSpace(ModuleFilter) ||
        DateFilter.HasValue ||
        !string.IsNullOrWhiteSpace(LocationFilter) ||
        StatusFilter.HasValue ||
        PendingReviewOnly;

    public bool IsSortedBy(string column) =>
        string.Equals(
            SortBy,
            column,
            StringComparison.OrdinalIgnoreCase);

    public string GetNextSortDirection(string column)
    {
        return IsSortedBy(column) &&
            string.Equals(
                SortDirection,
                "asc",
                StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";
    }

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser student =
            _currentUserService.GetRequiredUser();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        await _lifecycleService.ProcessDueTransitionsAsync(cancellationToken);

        IQueryable<Booking> query = _context.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.StudentObjectId == student.ObjectId &&
                booking.StudentTenantId == student.TenantId);

        if (PendingReviewOnly)
        {
            query = query.Where(booking =>
                booking.Status == BookingStatus.Completed &&
                booking.StudentEvaluation == null);
        }

        if (!string.IsNullOrWhiteSpace(ModuleFilter))
        {
            string moduleFilter = ModuleFilter.Trim();
            query = query.Where(booking =>
                booking.ProgrammeModule.ModuleCode.Contains(moduleFilter) ||
                booking.ProgrammeModule.ModuleName.Contains(moduleFilter));
        }

        if (DateFilter.HasValue)
        {
            DateTime localDate = DateFilter.Value.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);
            DateTimeOffset dateStart = new(
                localDate,
                SouthAfricaOffset);
            DateTimeOffset dateEnd = dateStart.AddDays(1);

            query = query.Where(booking =>
                booking.ScheduledStartTime >= dateStart &&
                booking.ScheduledStartTime < dateEnd);
        }

        if (!string.IsNullOrWhiteSpace(LocationFilter))
        {
            string locationFilter = LocationFilter.Trim();
            query = query.Where(booking =>
                booking.Location.Contains(locationFilter));
        }

        if (StatusFilter.HasValue)
        {
            query = query.Where(booking =>
                booking.Status == StatusFilter.Value);
        }

        List<BookingListItemViewModel> bookings = await query
            .Select(booking =>
                new BookingListItemViewModel
                {
                    BookingId = booking.BookingId,
                    TutorId = booking.TutorId,
                    TutorName = string.IsNullOrWhiteSpace(
                        booking.TutorCourseModule.Tutor.BcUser.DisplayName)
                        ? booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber
                        : booking.TutorCourseModule.Tutor.BcUser.DisplayName,
                    ModuleName = booking.ProgrammeModule.ModuleName,
                    ModuleCode = booking.ProgrammeModule.ModuleCode,
                    Location = booking.Location,
                    AvailableTime = booking.ScheduledStartTime,
                    Duration = booking.Duration,
                    Status = booking.Status,
                    Summary = booking.Summary,
                    StudentReviewSubmitted =
                        booking.StudentEvaluation != null
                })
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(TutorFilter))
        {
            string tutorFilter = TutorFilter.Trim();
            bookings = bookings
                .Where(booking => booking.TutorName.Contains(
                    tutorFilter,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        bool descending = string.Equals(
            SortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        bookings = SortBy.Trim().ToLowerInvariant() switch
        {
            "tutor" => descending
                ? bookings.OrderByDescending(booking => booking.TutorName)
                    .ToList()
                : bookings.OrderBy(booking => booking.TutorName).ToList(),
            "module" => descending
                ? bookings.OrderByDescending(booking => booking.ModuleCode)
                    .ToList()
                : bookings.OrderBy(booking => booking.ModuleCode).ToList(),
            "location" => descending
                ? bookings.OrderByDescending(booking => booking.Location)
                    .ToList()
                : bookings.OrderBy(booking => booking.Location).ToList(),
            "status" => descending
                ? bookings.OrderByDescending(booking => booking.Status)
                    .ToList()
                : bookings.OrderBy(booking => booking.Status).ToList(),
            _ => descending
                ? bookings.OrderByDescending(booking => booking.AvailableTime)
                    .ToList()
                : bookings.OrderBy(booking => booking.AvailableTime).ToList()
        };

        Bookings = bookings;
    }

    public async Task<IActionResult> OnPostCancelAsync(
        int bookingId,
        string? cancellationReason,
        CancellationToken cancellationToken)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        SessionLifecycleResult result =
            await _lifecycleService.CancelByStudentAsync(
                student.BcUserId,
                student.ObjectId,
                student.TenantId,
                bookingId,
                cancellationReason,
                cancellationToken);

        if (result.Succeeded)
        {
            SuccessMessage = "Session cancelled.";
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage();
    }
}
