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

namespace BC_CampusLearn.Pages.Tutors;

[Authorize]
public class SessionsModel : PageModel
{
    private static readonly TimeSpan SouthAfricaOffset =
        TimeSpan.FromHours(2);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionLifecycleService _lifecycleService;

    public SessionsModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISessionLifecycleService lifecycleService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _lifecycleService = lifecycleService;
    }

    [BindProperty(SupportsGet = true)]
    public string? StudentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ModuleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? DateFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? LocationFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public BookingStatus? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "date";

    [BindProperty(SupportsGet = true)]
    public string SortDirection { get; set; } = "desc";

    public IReadOnlyList<TutorSessionListItemViewModel> Sessions
    { get; private set; } = Array.Empty<TutorSessionListItemViewModel>();

    public IEnumerable<SelectListItem> StatusOptions =>
        Enum.GetValues<BookingStatus>()
            .Select(status => new SelectListItem(
                status.ToString(),
                status.ToString()));

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(StudentFilter) ||
        !string.IsNullOrWhiteSpace(ModuleFilter) ||
        DateFilter.HasValue ||
        !string.IsNullOrWhiteSpace(LocationFilter) ||
        StatusFilter.HasValue;

    public bool IsSortedBy(string column) =>
        string.Equals(
            SortBy,
            column,
            StringComparison.OrdinalIgnoreCase);

    public string GetNextSortDirection(string column) =>
        IsSortedBy(column) &&
        string.Equals(
            SortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase)
            ? "desc"
            : "asc";

    public IDictionary<string, string> GetRouteData(string column)
    {
        Dictionary<string, string> routeData = new()
        {
            ["SortBy"] = column,
            ["SortDirection"] = GetNextSortDirection(column)
        };

        AddRouteValue(routeData, "StudentFilter", StudentFilter);
        AddRouteValue(routeData, "ModuleFilter", ModuleFilter);
        AddRouteValue(
            routeData,
            "DateFilter",
            DateFilter?.ToString("yyyy-MM-dd"));
        AddRouteValue(routeData, "LocationFilter", LocationFilter);
        AddRouteValue(
            routeData,
            "StatusFilter",
            StatusFilter?.ToString());

        return routeData;
    }

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();
        int? tutorId = await _context.Tutors
            .AsNoTracking()
            .Where(tutor =>
                tutor.BcUserId == currentUser.BcUserId &&
                tutor.IsActive)
            .Select(tutor => (int?)tutor.TutorId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        await _lifecycleService.ProcessDueTransitionsAsync(cancellationToken);

        IQueryable<Booking> query = _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.TutorId == tutorId.Value);

        if (!string.IsNullOrWhiteSpace(StudentFilter))
        {
            string studentFilter = StudentFilter.Trim();
            query = query.Where(booking =>
                booking.StudentName.Contains(studentFilter));
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

        List<TutorSessionListItemViewModel> sessions = await query
            .Select(booking => new TutorSessionListItemViewModel
            {
                BookingId = booking.BookingId,
                StudentName = booking.StudentName,
                ModuleCode = booking.ProgrammeModule.ModuleCode,
                ModuleName = booking.ProgrammeModule.ModuleName,
                Location = booking.Location,
                ScheduledStartTime = booking.ScheduledStartTime,
                Duration = booking.Duration,
                Status = booking.Status
            })
            .ToListAsync(cancellationToken);

        bool descending = string.Equals(
            SortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        sessions = SortBy.Trim().ToLowerInvariant() switch
        {
            "student" => descending
                ? sessions.OrderByDescending(session => session.StudentName)
                    .ToList()
                : sessions.OrderBy(session => session.StudentName).ToList(),
            "module" => descending
                ? sessions.OrderByDescending(session => session.ModuleCode)
                    .ToList()
                : sessions.OrderBy(session => session.ModuleCode).ToList(),
            "location" => descending
                ? sessions.OrderByDescending(session => session.Location)
                    .ToList()
                : sessions.OrderBy(session => session.Location).ToList(),
            "status" => descending
                ? sessions.OrderByDescending(session => session.Status)
                    .ToList()
                : sessions.OrderBy(session => session.Status).ToList(),
            _ => descending
                ? sessions.OrderByDescending(
                    session => session.ScheduledStartTime).ToList()
                : sessions.OrderBy(
                    session => session.ScheduledStartTime).ToList()
        };

        Sessions = sessions;
        return Page();
    }

    private static void AddRouteValue(
        IDictionary<string, string> routeData,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            routeData[key] = value;
        }
    }
}
