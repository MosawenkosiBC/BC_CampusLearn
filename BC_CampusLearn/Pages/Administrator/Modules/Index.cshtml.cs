using System.ComponentModel.DataAnnotations;
using System.Data;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Modules;

public class IndexModel(ApplicationDbContext context) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProgrammeId { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }
    [BindProperty(SupportsGet = true)] public bool Unassigned { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; } = "tutors";
    [BindProperty(SupportsGet = true)] public string? SortDirection { get; set; } = "desc";
    [BindProperty(SupportsGet = true)] public int ModulePage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; } = "catalogue";
    [BindProperty(SupportsGet = true)] public TutorAccountRequestStatus RequestStatus { get; set; }
    [BindProperty(SupportsGet = true)] public int RequestPage { get; set; } = 1;
    [BindProperty] public ModuleInput Input { get; set; } = new();
    [BindProperty] public List<int> SelectedRequestIds { get; set; } = [];
    public IReadOnlyList<ProgrammeOfStudy> Programmes { get; private set; } = [];
    public IReadOnlyList<ModuleRow> Modules { get; private set; } = [];
    public IReadOnlyList<TutorModuleChangeRequest> Requests { get; private set; } = [];
    public IReadOnlyList<TutorRequestGroup> RequestGroups { get; private set; } = [];
    public int TotalModules { get; private set; }
    public int UncoveredModules { get; private set; }
    public int PendingRequests { get; private set; }
    public int FilteredCount { get; private set; }
    public int FilteredRequestCount { get; private set; }
    public int TotalPages { get; private set; }
    public int RequestPages { get; private set; }
    public bool ShowCreate { get; private set; }
    public sealed record ModuleRow(int Id, string Code, string Name, string Programme, int Year, int TutorCount);
    public sealed record RequestModuleRow(int RequestId, int ModuleId, string Code, string Name, string Programme);
    public sealed record RequestSubmission(
        int RequestId,
        TutorModuleChangeRequestType RequestType,
        TutorAccountRequestStatus Status,
        string? Reason,
        DateTime SubmittedAt,
        DateTime? ReviewedAt,
        string? ReviewedBy,
        string? ReviewNote,
        IReadOnlyList<RequestModuleRow> Modules);
    public sealed record TutorRequestGroup(
        int TutorId,
        string TutorName,
        string Programme,
        IReadOnlyList<RequestSubmission> Submissions);
    public sealed class ModuleInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a programme.")] public int ProgrammeId { get; set; }
        [Required, StringLength(150)] public string ModuleName { get; set; } = "";
        [Required, StringLength(10)] public string ModuleCode { get; set; } = "";
        [Range(1, 4)] public int YearOfStudy { get; set; } = 1;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
        => await context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => CreateCoreAsync(cancellationToken));

    private async Task<IActionResult> CreateCoreAsync(CancellationToken cancellationToken)
    {
        ShowCreate = true;
        Input.ModuleCode = (Input.ModuleCode ?? "").Trim().ToUpperInvariant();
        Input.ModuleName = (Input.ModuleName ?? "").Trim();
        if (Input.ModuleName.Length == 0 || Input.ModuleCode.Length == 0)
            ModelState.AddModelError("", "Enter a module name and code.");
        if (!await context.ProgrammesOfStudy.AnyAsync(p => p.Id == Input.ProgrammeId, cancellationToken))
            ModelState.AddModelError("Input.ProgrammeId", "Choose an existing programme.");
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        if (await context.ProgrammeModules.AnyAsync(m => m.ProgrammeId == Input.ProgrammeId &&
            m.ModuleCode.ToUpper() == Input.ModuleCode, cancellationToken))
            ModelState.AddModelError("Input.ModuleCode", "This module code already exists in the selected programme.");
        if (!ModelState.IsValid) { await LoadAsync(cancellationToken); return Page(); }
        var module = new ProgrammeModule { ProgrammeId = Input.ProgrammeId, ModuleCode = Input.ModuleCode,
            ModuleName = Input.ModuleName, YearOfStudy = Input.YearOfStudy };
        context.ProgrammeModules.Add(module);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        TempData["ModuleSuccess"] = "Module created. You can now assign tutors.";
        return RedirectToPage("Details", new { id = module.ProgrammeModuleId });
    }

    public async Task<IActionResult> OnPostReviewAsync(int requestId, bool approve, string? reviewNote,
        CancellationToken cancellationToken)
        => await context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ReviewCoreAsync(requestId, approve, reviewNote, cancellationToken));

    private async Task<IActionResult> ReviewCoreAsync(int requestId, bool approve, string? reviewNote,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();
        Tab = "requests";
        reviewNote = reviewNote?.Trim();
        if (reviewNote?.Length > 500 || (!approve && string.IsNullOrWhiteSpace(reviewNote)))
        {
            ModelState.AddModelError("", "Provide a reason when declining, using no more than 500 characters.");
            await LoadAsync(cancellationToken); return Page();
        }
        SelectedRequestIds = SelectedRequestIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (SelectedRequestIds.Count == 0)
        {
            ModelState.AddModelError("", "Select at least one module to review.");
            await LoadAsync(cancellationToken);
            return Page();
        }
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var request = await context.TutorModuleChangeRequests.Include(r => r.Tutor)
            .Include(r => r.ProgrammeModule).SingleOrDefaultAsync(r => r.TutorModuleChangeRequestId == requestId, cancellationToken);
        if (request is null) return NotFound();
        List<TutorModuleChangeRequest> submission = await context.TutorModuleChangeRequests
            .Include(r => r.Tutor)
            .Include(r => r.ProgrammeModule)
            .Where(r => r.TutorId == request.TutorId &&
                r.RequestType == request.RequestType &&
                r.SubmittedAt == request.SubmittedAt &&
                r.Reason == request.Reason)
            .OrderBy(r => r.TutorModuleChangeRequestId)
            .ToListAsync(cancellationToken);
        List<TutorModuleChangeRequest> selectedRequests = submission
            .Where(item => SelectedRequestIds.Contains(item.TutorModuleChangeRequestId))
            .ToList();
        string? error = selectedRequests.Count != SelectedRequestIds.Count
            ? "The selected modules do not belong to this request."
            : selectedRequests.Any(item => item.Status != TutorAccountRequestStatus.Pending)
                ? "One or more selected modules have already been reviewed."
                : null;
        var management = new AdminModuleManagement(context);
        if (error is null && approve)
        {
            if (!Enum.IsDefined(request.RequestType)) error = "The request type is invalid.";
            else
            {
                foreach (TutorModuleChangeRequest item in selectedRequests)
                {
                    error = await management.ChangeAssignmentAsync(
                        item.TutorId,
                        item.ProgrammeModuleId,
                        item.RequestType == TutorModuleChangeRequestType.Add,
                        cancellationToken);
                    if (error is not null) break;
                }
            }
        }
        if (error is not null)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            ModelState.AddModelError("", error);
            await LoadAsync(cancellationToken);
            return Page();
        }
        TutorAccountRequestStatus reviewedStatus = approve
            ? TutorAccountRequestStatus.Approved
            : TutorAccountRequestStatus.Declined;
        DateTime reviewedAt = DateTime.UtcNow;
        string reviewedBy = User.Identity?.Name ?? "Administrator";
        foreach (TutorModuleChangeRequest item in selectedRequests)
        {
            item.Status = reviewedStatus;
            item.ReviewedAt = reviewedAt;
            item.ReviewedBy = reviewedBy;
            item.ReviewNote = reviewNote;
            management.Notify(item.Tutor, item.ProgrammeModule,
                $"Your request to {item.RequestType.ToString().ToLowerInvariant()} this module was {reviewedStatus.ToString().ToLowerInvariant()}. {reviewNote}");
        }
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            ModelState.AddModelError("", "The request or assignment changed. Refresh and review the latest state.");
            await LoadAsync(cancellationToken); return Page();
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        TempData["ModuleSuccess"] = approve
            ? "Request approved and module assignments updated."
            : "Request declined. The tutor has been notified.";
        return RedirectToPage(new { Tab = "requests", RequestStatus });
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Tab = Tab == "requests" ? "requests" : "catalogue";
        Sort = Sort is "module" or "programme" or "year" or "tutors" ? Sort : "tutors";
        SortDirection = SortDirection == "asc" ? "asc" : "desc";
        if (!Enum.IsDefined(RequestStatus)) RequestStatus = TutorAccountRequestStatus.Pending;
        Programmes = await context.ProgrammesOfStudy.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        var query = context.ProgrammeModules.AsNoTracking();
        TotalModules = await query.CountAsync(ct);
        UncoveredModules = await query.CountAsync(m => !m.TutorCourseModules.Any(a =>
            a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved &&
            a.Tutor.ApplicationStage == TutorApplicationStage.Placement), ct);
        var pendingRequestRows = await context.TutorModuleChangeRequests.AsNoTracking().Where(r =>
            r.Status == TutorAccountRequestStatus.Pending && r.Tutor.IsActive &&
            r.Tutor.Status == TutorStatus.Approved &&
            r.Tutor.ApplicationStage == TutorApplicationStage.Placement)
            .Select(r => new { r.TutorId, r.RequestType, r.SubmittedAt, r.Reason })
            .ToListAsync(ct);
        PendingRequests = pendingRequestRows
            .DistinctBy(r => new { r.TutorId, r.RequestType, r.SubmittedAt, r.Reason })
            .Count();
        if (ProgrammeId.HasValue) query = query.Where(m => m.ProgrammeId == ProgrammeId);
        if (Year.HasValue) query = query.Where(m => m.YearOfStudy == Year);
        if (!string.IsNullOrWhiteSpace(Search)) { var term = Search.Trim(); query = query.Where(m => m.ModuleName.Contains(term) || m.ModuleCode.Contains(term)); }
        if (Unassigned) query = query.Where(m => !m.TutorCourseModules.Any(a =>
            a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved &&
            a.Tutor.ApplicationStage == TutorApplicationStage.Placement));
        FilteredCount = await query.CountAsync(ct);
        TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredCount / 12d));
        ModulePage = Math.Clamp(ModulePage, 1, TotalPages);
        var orderedQuery = (Sort, SortDirection) switch
        {
            ("module", "asc") => query.OrderBy(m => m.ModuleCode).ThenBy(m => m.ModuleName),
            ("module", _) => query.OrderByDescending(m => m.ModuleCode).ThenByDescending(m => m.ModuleName),
            ("programme", "asc") => query.OrderBy(m => m.Programme.Name).ThenBy(m => m.ModuleCode),
            ("programme", _) => query.OrderByDescending(m => m.Programme.Name).ThenBy(m => m.ModuleCode),
            ("year", "asc") => query.OrderBy(m => m.YearOfStudy).ThenBy(m => m.ModuleCode),
            ("year", _) => query.OrderByDescending(m => m.YearOfStudy).ThenBy(m => m.ModuleCode),
            ("tutors", "asc") => query.OrderBy(m => m.TutorCourseModules.Count(a =>
                a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved &&
                a.Tutor.ApplicationStage == TutorApplicationStage.Placement)).ThenBy(m => m.ModuleCode),
            _ => query.OrderByDescending(m => m.TutorCourseModules.Count(a =>
                a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved &&
                a.Tutor.ApplicationStage == TutorApplicationStage.Placement)).ThenBy(m => m.ModuleCode)
        };
        Modules = await orderedQuery
            .Skip((ModulePage - 1) * 12).Take(12)
            .Select(m => new ModuleRow(m.ProgrammeModuleId, m.ModuleCode, m.ModuleName, m.Programme.Name,
                m.YearOfStudy, m.TutorCourseModules.Count(a =>
                    a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved &&
                    a.Tutor.ApplicationStage == TutorApplicationStage.Placement))).ToListAsync(ct);
        var requests = context.TutorModuleChangeRequests.AsNoTracking().Where(r =>
            r.Status == RequestStatus && r.Tutor.IsActive && r.Tutor.Status == TutorStatus.Approved &&
            r.Tutor.ApplicationStage == TutorApplicationStage.Placement);
        List<TutorModuleChangeRequest> requestRows = await requests
            .Include(r => r.Tutor).ThenInclude(t => t.BcUser)
            .Include(r => r.Tutor).ThenInclude(t => t.Programme)
            .Include(r => r.ProgrammeModule).ThenInclude(m => m.Programme)
            .OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.TutorModuleChangeRequestId)
            .ToListAsync(ct);
        var requestSubmissions = requestRows
            .GroupBy(r => new
            {
                r.TutorId,
                r.RequestType,
                r.SubmittedAt,
                r.Reason,
                r.ReviewedAt,
                r.ReviewedBy,
                r.ReviewNote
            })
            .OrderBy(group => group.First().Tutor.BcUser.DisplayName)
            .ThenByDescending(group => group.Key.SubmittedAt)
            .ToList();
        FilteredRequestCount = requestSubmissions.Count;
        RequestPages = Math.Max(1, (int)Math.Ceiling(FilteredRequestCount / 12d));
        RequestPage = Math.Clamp(RequestPage, 1, RequestPages);
        var visibleSubmissions = requestSubmissions
            .Skip((RequestPage - 1) * 12)
            .Take(12)
            .ToList();
        Requests = visibleSubmissions.SelectMany(group => group).ToList();
        RequestGroups = visibleSubmissions
            .GroupBy(submission => submission.Key.TutorId)
            .Select(tutorSubmissions =>
        {
            TutorModuleChangeRequest first = tutorSubmissions.First().First();
            IReadOnlyList<RequestSubmission> submissions = tutorSubmissions
                .Select(submission =>
                {
                    TutorModuleChangeRequest submissionFirst = submission.First();
                    IReadOnlyList<RequestModuleRow> requestModules = submission
                        .OrderBy(r => r.ProgrammeModule.ModuleCode)
                        .Select(r => new RequestModuleRow(
                            r.TutorModuleChangeRequestId,
                            r.ProgrammeModuleId,
                            r.ProgrammeModule.ModuleCode,
                            r.ProgrammeModule.ModuleName,
                            r.ProgrammeModule.Programme.Name))
                        .ToList();
                    return new RequestSubmission(
                        submissionFirst.TutorModuleChangeRequestId,
                        submissionFirst.RequestType,
                        submissionFirst.Status,
                        submissionFirst.Reason,
                        submissionFirst.SubmittedAt,
                        submissionFirst.ReviewedAt,
                        submissionFirst.ReviewedBy,
                        submissionFirst.ReviewNote,
                        requestModules);
                })
                .ToList();
            return new TutorRequestGroup(
                first.TutorId,
                first.Tutor.BcUser.DisplayName,
                first.Tutor.Programme.Name,
                submissions);
        }).ToList();
    }
}
