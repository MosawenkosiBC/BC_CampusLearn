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
    [BindProperty(SupportsGet = true)] public int ModulePage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; } = "catalogue";
    [BindProperty(SupportsGet = true)] public TutorAccountRequestStatus RequestStatus { get; set; }
    [BindProperty(SupportsGet = true)] public int RequestPage { get; set; } = 1;
    [BindProperty] public ModuleInput Input { get; set; } = new();
    public IReadOnlyList<ProgrammeOfStudy> Programmes { get; private set; } = [];
    public IReadOnlyList<ModuleRow> Modules { get; private set; } = [];
    public IReadOnlyList<TutorModuleChangeRequest> Requests { get; private set; } = [];
    public int TotalModules { get; private set; }
    public int UncoveredModules { get; private set; }
    public int PendingRequests { get; private set; }
    public int FilteredCount { get; private set; }
    public int TotalPages { get; private set; }
    public int RequestPages { get; private set; }
    public bool ShowCreate { get; private set; }
    public sealed record ModuleRow(int Id, string Code, string Name, string Programme, int Year, int TutorCount);
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
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var request = await context.TutorModuleChangeRequests.Include(r => r.Tutor)
            .Include(r => r.ProgrammeModule).SingleOrDefaultAsync(r => r.TutorModuleChangeRequestId == requestId, cancellationToken);
        if (request is null) return NotFound();
        string? error = request.Status != TutorAccountRequestStatus.Pending ? "This request has already been reviewed." : null;
        var management = new AdminModuleManagement(context);
        if (error is null && approve)
        {
            if (!Enum.IsDefined(request.RequestType)) error = "The request type is invalid.";
            else error = await management.ChangeAssignmentAsync(request.TutorId, request.ProgrammeModuleId,
                request.RequestType == TutorModuleChangeRequestType.Add, cancellationToken);
        }
        if (error is not null) { ModelState.AddModelError("", error); await LoadAsync(cancellationToken); return Page(); }
        request.Status = approve ? TutorAccountRequestStatus.Approved : TutorAccountRequestStatus.Declined;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedBy = User.Identity?.Name ?? "Administrator";
        request.ReviewNote = reviewNote;
        management.Notify(request.Tutor, request.ProgrammeModule,
            $"Your request to {request.RequestType.ToString().ToLowerInvariant()} this module was {request.Status.ToString().ToLowerInvariant()}. {reviewNote}");
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            ModelState.AddModelError("", "The request or assignment changed. Refresh and review the latest state.");
            await LoadAsync(cancellationToken); return Page();
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        TempData["ModuleSuccess"] = approve ? "Request approved and assignment updated." : "Request declined. The tutor has been notified.";
        return RedirectToPage(new { Tab = "requests", RequestStatus });
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Tab = Tab == "requests" ? "requests" : "catalogue";
        if (!Enum.IsDefined(RequestStatus)) RequestStatus = TutorAccountRequestStatus.Pending;
        Programmes = await context.ProgrammesOfStudy.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        var query = context.ProgrammeModules.AsNoTracking();
        TotalModules = await query.CountAsync(ct);
        UncoveredModules = await query.CountAsync(m => !m.TutorCourseModules.Any(a => a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved), ct);
        PendingRequests = await context.TutorModuleChangeRequests.CountAsync(r => r.Status == TutorAccountRequestStatus.Pending, ct);
        if (ProgrammeId.HasValue) query = query.Where(m => m.ProgrammeId == ProgrammeId);
        if (Year.HasValue) query = query.Where(m => m.YearOfStudy == Year);
        if (!string.IsNullOrWhiteSpace(Search)) { var term = Search.Trim(); query = query.Where(m => m.ModuleName.Contains(term) || m.ModuleCode.Contains(term)); }
        if (Unassigned) query = query.Where(m => !m.TutorCourseModules.Any(a => a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved));
        FilteredCount = await query.CountAsync(ct);
        TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredCount / 12d));
        ModulePage = Math.Clamp(ModulePage, 1, TotalPages);
        Modules = await query.OrderBy(m => m.Programme.Name).ThenBy(m => m.YearOfStudy).ThenBy(m => m.ModuleCode)
            .Skip((ModulePage - 1) * 12).Take(12)
            .Select(m => new ModuleRow(m.ProgrammeModuleId, m.ModuleCode, m.ModuleName, m.Programme.Name,
                m.YearOfStudy, m.TutorCourseModules.Count(a => a.IsActive && a.Tutor.IsActive && a.Tutor.Status == TutorStatus.Approved))).ToListAsync(ct);
        var requests = context.TutorModuleChangeRequests.AsNoTracking().Where(r => r.Status == RequestStatus);
        RequestPages = Math.Max(1, (int)Math.Ceiling(await requests.CountAsync(ct) / 12d));
        RequestPage = Math.Clamp(RequestPage, 1, RequestPages);
        Requests = await requests.Include(r => r.Tutor).ThenInclude(t => t.BcUser)
            .Include(r => r.ProgrammeModule).ThenInclude(m => m.Programme)
            .OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.TutorModuleChangeRequestId)
            .Skip((RequestPage - 1) * 12).Take(12).ToListAsync(ct);
    }
}
