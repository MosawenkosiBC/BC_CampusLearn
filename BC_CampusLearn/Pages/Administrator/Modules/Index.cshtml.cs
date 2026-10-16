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
    [BindProperty] public int BulkTutorId { get; set; }
    [BindProperty] public string BulkAction { get; set; } = "assign";
    [BindProperty] public List<int> BulkModuleIds { get; set; } = [];
    public IReadOnlyList<ProgrammeOfStudy> Programmes { get; private set; } = [];
    public IReadOnlyList<ModuleRow> Modules { get; private set; } = [];
    public IReadOnlyList<Tutor> AssignmentTutors { get; private set; } = [];
    public IReadOnlyList<ProgrammeModule> AssignmentModules { get; private set; } = [];
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
    public bool ShowBulkAssign { get; private set; }
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

    public async Task<IActionResult> OnPostBulkAssignAsync(
        CancellationToken cancellationToken)
        => await context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => BulkAssignCoreAsync(cancellationToken));

    private async Task<IActionResult> BulkAssignCoreAsync(
        CancellationToken cancellationToken)
    {
        Tab = "catalogue";
        ShowBulkAssign = true;
        // This page contains multiple forms. Ignore binding errors produced by
        // fields that belong to the create-module form and validate only the
        // bulk module-management inputs below.
        ModelState.Clear();
        bool addAssignments = !string.Equals(
            BulkAction,
            "remove",
            StringComparison.OrdinalIgnoreCase);
        BulkAction = addAssignments ? "assign" : "remove";
        BulkModuleIds = BulkModuleIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        Tutor? tutor = await context.Tutors
            .Include(item => item.BcUser)
            .SingleOrDefaultAsync(item => item.TutorId == BulkTutorId,
                cancellationToken);
        if (tutor is null || !tutor.IsActive ||
            tutor.Status != TutorStatus.Approved ||
            tutor.ApplicationStage != TutorApplicationStage.Placement)
        {
            ModelState.AddModelError(nameof(BulkTutorId),
                "Choose an active, approved tutor in placement.");
        }
        if (BulkModuleIds.Count == 0)
        {
            ModelState.AddModelError(nameof(BulkModuleIds),
                addAssignments
                    ? "Select at least one module to assign."
                    : "Select at least one module to remove.");
        }

        List<ProgrammeModule> selectedModules = BulkModuleIds.Count == 0
            ? []
            : await context.ProgrammeModules
                .Where(module => BulkModuleIds.Contains(module.ProgrammeModuleId))
                .OrderBy(module => module.ModuleCode)
                .ToListAsync(cancellationToken);
        if (selectedModules.Count != BulkModuleIds.Count)
        {
            ModelState.AddModelError(nameof(BulkModuleIds),
                "One or more selected modules no longer exist.");
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        HashSet<int> assignedModuleIds = await context.TutorCourseModules
            .Where(assignment => assignment.TutorId == BulkTutorId &&
                assignment.IsActive &&
                BulkModuleIds.Contains(assignment.ProgrammeModuleId))
            .Select(assignment => assignment.ProgrammeModuleId)
            .ToHashSetAsync(cancellationToken);
        List<ProgrammeModule> modulesToChange = selectedModules
            .Where(module => addAssignments !=
                assignedModuleIds.Contains(module.ProgrammeModuleId))
            .ToList();
        if (modulesToChange.Count == 0)
        {
            ModelState.AddModelError(nameof(BulkModuleIds),
                addAssignments
                    ? "This tutor is already assigned to every selected module."
                    : "This tutor is not assigned to any selected module.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;
        var management = new AdminModuleManagement(context);
        string? error = null;
        foreach (ProgrammeModule module in modulesToChange)
        {
            error = await management.ChangeAssignmentAsync(
                BulkTutorId,
                module.ProgrammeModuleId,
                add: addAssignments,
                cancellationToken: cancellationToken);
            if (error is not null)
            {
                break;
            }
        }

        if (error is null)
        {
            int[] moduleIds = modulesToChange
                .Select(module => module.ProgrammeModuleId)
                .ToArray();
            List<TutorModuleChangeRequest> pendingRequests =
                await context.TutorModuleChangeRequests
                    .Where(request => request.TutorId == BulkTutorId &&
                        moduleIds.Contains(request.ProgrammeModuleId) &&
                        request.Status == TutorAccountRequestStatus.Pending)
                    .ToListAsync(cancellationToken);
            foreach (TutorModuleChangeRequest request in pendingRequests)
            {
                request.Status = request.RequestType == (addAssignments
                    ? TutorModuleChangeRequestType.Add
                    : TutorModuleChangeRequestType.Remove)
                        ? TutorAccountRequestStatus.Approved
                        : TutorAccountRequestStatus.Declined;
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewedBy = User.Identity?.Name ?? "Administrator";
                request.ReviewNote =
                    "Resolved when an administrator updated the module assignment.";
            }
            management.NotifyAssignmentChanges(
                tutor!,
                modulesToChange,
                addAssignments);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                error = "One or more assignments changed. Refresh and try again.";
            }
            catch (DbUpdateException)
            {
                error = "The module changes could not be saved. Refresh and try again.";
            }
        }

        if (error is not null)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            context.ChangeTracker.Clear();
            ModelState.AddModelError("", error);
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        TempData["ModuleSuccess"] = addAssignments
            ? modulesToChange.Count == 1
                ? $"{tutor!.BcUser.DisplayName} was assigned to 1 module."
                : $"{tutor!.BcUser.DisplayName} was assigned to {modulesToChange.Count} modules."
            : modulesToChange.Count == 1
                ? $"{tutor!.BcUser.DisplayName} was removed from 1 module."
                : $"{tutor!.BcUser.DisplayName} was removed from {modulesToChange.Count} modules.";
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: new
        {
            Search,
            ProgrammeId,
            Year,
            Unassigned,
            Sort,
            SortDirection,
            ModulePage
        }, fragment: "module-catalogue");
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Tab = Tab == "requests" ? "requests" : "catalogue";
        Sort = Sort is "module" or "programme" or "year" or "tutors" ? Sort : "tutors";
        SortDirection = SortDirection == "asc" ? "asc" : "desc";
        if (!Enum.IsDefined(RequestStatus)) RequestStatus = TutorAccountRequestStatus.Pending;
        Programmes = await context.ProgrammesOfStudy.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        AssignmentTutors = await context.Tutors
            .AsNoTracking()
            .Include(tutor => tutor.BcUser)
            .Include(tutor => tutor.Programme)
            .Include(tutor => tutor.TutorCourseModules.Where(assignment => assignment.IsActive))
            .Where(tutor => tutor.IsActive &&
                tutor.Status == TutorStatus.Approved &&
                tutor.ApplicationStage == TutorApplicationStage.Placement)
            .OrderBy(tutor => tutor.BcUser.DisplayName)
            .ToListAsync(ct);
        AssignmentModules = await context.ProgrammeModules
            .AsNoTracking()
            .Include(module => module.Programme)
            .OrderBy(module => module.ModuleCode)
            .ThenBy(module => module.ModuleName)
            .ToListAsync(ct);
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
