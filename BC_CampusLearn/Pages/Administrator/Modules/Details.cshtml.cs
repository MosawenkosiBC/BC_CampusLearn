using System.Data;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Modules;

public class DetailsModel(ApplicationDbContext context) : PageModel
{
    public ProgrammeModule Module { get; private set; } = null!;
    public IReadOnlyList<Tutor> AvailableTutors { get; private set; } = [];
    public IReadOnlyList<TutorCourseModule> AssignedTutors { get; private set; } = [];
    public IReadOnlyList<ProgrammeOfStudy> AssignedTutorProgrammes { get; private set; } = [];
    public IReadOnlyDictionary<int, int> OutstandingSessionCounts { get; private set; } =
        new Dictionary<int, int>();
    public int AssignedTutorCount { get; private set; }
    public int FilteredAssignedTutorCount { get; private set; }
    public int TutorTotalPages { get; private set; }
    [BindProperty(SupportsGet = true)] public string? TutorSearch { get; set; }
    [BindProperty(SupportsGet = true)] public int? TutorProgrammeId { get; set; }
    [BindProperty(SupportsGet = true)] public int TutorPage { get; set; } = 1;
    public bool ShowEdit { get; private set; }
    public bool ShowAssign { get; private set; }
    [BindProperty] public IndexModel.ModuleInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken)) return NotFound();
        Input = new() { ProgrammeId = Module.ProgrammeId, ModuleName = Module.ModuleName,
            ModuleCode = Module.ModuleCode, YearOfStudy = Module.YearOfStudy };
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int id, CancellationToken cancellationToken)
        => await context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => SaveCoreAsync(id, cancellationToken));

    private async Task<IActionResult> SaveCoreAsync(int id, CancellationToken cancellationToken)
    {
        ShowEdit = true;
        var module = await context.ProgrammeModules.FindAsync([id], cancellationToken);
        if (module is null) return NotFound();
        Input.ModuleCode = (Input.ModuleCode ?? "").Trim().ToUpperInvariant();
        Input.ModuleName = (Input.ModuleName ?? "").Trim();
        if (Input.ModuleCode.Length == 0 || Input.ModuleName.Length == 0)
            ModelState.AddModelError("", "Enter a module code and name.");
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        if (await context.ProgrammeModules.AnyAsync(m => m.ProgrammeId == module.ProgrammeId &&
            m.ProgrammeModuleId != id && m.ModuleCode.ToUpper() == Input.ModuleCode, cancellationToken))
            ModelState.AddModelError("Input.ModuleCode", "This module code already exists in this programme.");
        if (!ModelState.IsValid) { await LoadAsync(id, cancellationToken); return Page(); }
        module.ModuleName = Input.ModuleName;
        module.ModuleCode = Input.ModuleCode;
        module.YearOfStudy = Input.YearOfStudy;
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        TempData["ModuleSuccess"] = "Module details saved.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAssignmentAsync(int id, int tutorId, bool add, CancellationToken cancellationToken)
        => await context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => AssignmentCoreAsync(id, tutorId, add, cancellationToken));

    private async Task<IActionResult> AssignmentCoreAsync(int id, int tutorId, bool add, CancellationToken cancellationToken)
    {
        ModelState.Clear();
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var management = new AdminModuleManagement(context);
        var error = await management.ChangeAssignmentAsync(tutorId, id, add, cancellationToken);
        if (error is null)
        {
            var tutor = await context.Tutors.FindAsync([tutorId], cancellationToken);
            var module = await context.ProgrammeModules.FindAsync([id], cancellationToken);
            // Resolve matching pending requests so direct administration cannot leave stale requests behind.
            var pending = await context.TutorModuleChangeRequests.Where(r => r.TutorId == tutorId &&
                r.ProgrammeModuleId == id && r.Status == TutorAccountRequestStatus.Pending).ToListAsync(cancellationToken);
            foreach (var request in pending)
            {
                request.Status = (request.RequestType == TutorModuleChangeRequestType.Add) == add
                    ? TutorAccountRequestStatus.Approved : TutorAccountRequestStatus.Declined;
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewedBy = User.Identity?.Name ?? "Administrator";
                request.ReviewNote = "Resolved when an administrator updated the module assignment.";
            }
            management.Notify(tutor!, module!, add ? "An administrator assigned you to this module." : "An administrator removed your assignment. Past sessions remain available.");
            try { await context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { error = "The assignment changed. Refresh and try again."; }
        }
        if (error is not null)
        {
            ShowAssign = add;
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            ModelState.AddModelError("", error);
            if (!await LoadAsync(id, cancellationToken)) return NotFound();
            Input = new() { ProgrammeId = Module.ProgrammeId, ModuleName = Module.ModuleName, ModuleCode = Module.ModuleCode, YearOfStudy = Module.YearOfStudy };
            return Page();
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        TempData["ModuleSuccess"] = add ? "Tutor assigned and notified." : "Tutor removed and notified. Session history has been preserved.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(int id, CancellationToken ct)
    {
        var module = await context.ProgrammeModules.AsNoTracking().Include(m => m.Programme)
            .Include(m => m.TutorCourseModules.Where(a => a.IsActive && a.Tutor.IsActive &&
                a.Tutor.Status == TutorStatus.Approved &&
                a.Tutor.ApplicationStage == TutorApplicationStage.Placement))
            .ThenInclude(a => a.Tutor).ThenInclude(t => t.BcUser)
            .Include(m => m.TutorCourseModules.Where(a => a.IsActive && a.Tutor.IsActive &&
                a.Tutor.Status == TutorStatus.Approved &&
                a.Tutor.ApplicationStage == TutorApplicationStage.Placement))
            .ThenInclude(a => a.Tutor).ThenInclude(t => t.Programme)
            .SingleOrDefaultAsync(m => m.ProgrammeModuleId == id, ct);
        if (module is null) return false;
        Module = module;
        var assignments = module.TutorCourseModules.OrderBy(a => a.Tutor.BcUser.DisplayName).ToList();
        AssignedTutorCount = assignments.Count;
        AssignedTutorProgrammes = assignments.Select(a => a.Tutor.Programme)
            .DistinctBy(programme => programme.Id).OrderBy(programme => programme.Name).ToList();
        IEnumerable<TutorCourseModule> filteredAssignments = assignments;
        if (!string.IsNullOrWhiteSpace(TutorSearch))
        {
            var term = TutorSearch.Trim();
            filteredAssignments = filteredAssignments.Where(assignment =>
                assignment.Tutor.BcUser.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                assignment.Tutor.BcUser.PersonnelNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (assignment.Tutor.BcUser.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (TutorProgrammeId.HasValue)
            filteredAssignments = filteredAssignments.Where(a => a.Tutor.ProgrammeId == TutorProgrammeId.Value);
        var filteredAssignmentList = filteredAssignments.ToList();
        FilteredAssignedTutorCount = filteredAssignmentList.Count;
        TutorTotalPages = Math.Max(1, (int)Math.Ceiling(FilteredAssignedTutorCount / 12d));
        TutorPage = Math.Clamp(TutorPage, 1, TutorTotalPages);
        AssignedTutors = filteredAssignmentList.Skip((TutorPage - 1) * 12).Take(12).ToList();
        OutstandingSessionCounts = await context.Bookings.AsNoTracking()
            .Where(booking => booking.ProgrammeModuleId == id &&
                (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed ||
                 booking.Status == BookingStatus.InProgress))
            .GroupBy(booking => booking.TutorId)
            .Select(group => new { TutorId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.TutorId, item => item.Count, ct);
        AvailableTutors = await context.Tutors.AsNoTracking().Include(t => t.BcUser).Include(t => t.Programme)
            .Where(t => t.IsActive && t.Status == TutorStatus.Approved &&
                t.ApplicationStage == TutorApplicationStage.Placement &&
                !t.TutorCourseModules.Any(a => a.ProgrammeModuleId == id && a.IsActive))
            .OrderBy(t => t.BcUser.DisplayName).ToListAsync(ct);
        return true;
    }
}
