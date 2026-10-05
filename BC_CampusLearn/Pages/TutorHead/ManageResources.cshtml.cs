using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.TutorHead;

[Authorize(Roles = nameof(BcUserRole.HeadOfTutors))]
public class ManageResourcesModel(
    ApplicationDbContext context,
    IWebHostEnvironment environment,
    ICurrentUserService currentUserService) : PageModel
{
    public const int PageSize = 9;

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "resources";

    public bool IsNominationsTab => string.Equals(
        Tab,
        "nominated",
        StringComparison.OrdinalIgnoreCase);

    [BindProperty(SupportsGet = true)]
    public string? SearchName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchModule { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchCourse { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ResourcePage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? NomineeSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? NominationModuleSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? NominationCourseSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public int NomineePage { get; set; } = 1;

    public int TotalResources { get; private set; }
    public int TotalPages { get; private set; }
    public IReadOnlyList<TutorHeadLearningResourceListItem> Resources
    { get; private set; } = [];
    public int TotalNominatedTutors { get; private set; }
    public int TotalNomineePages { get; private set; }
    public IReadOnlyList<NominatedTutorListItem> NominatedTutors
    { get; private set; } = [];
    public IReadOnlyList<NominationTutorOption> AvailableTutorOptions
    { get; private set; } = [];

    [TempData]
    public string? ResourceMessage { get; set; }

    [TempData]
    public bool ResourceMessageIsError { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadPageAsync(cancellationToken);

    public async Task<IActionResult> OnPostNominateAsync(
        int tutorId,
        int programmeModuleId,
        CancellationToken cancellationToken)
    {
        TutorCourseModule? assignment = await context.TutorCourseModules
            .Include(item => item.Tutor)
                .ThenInclude(tutor => tutor.BcUser)
            .Include(item => item.ProgrammeModule)
            .SingleOrDefaultAsync(item =>
                item.TutorId == tutorId &&
                item.ProgrammeModuleId == programmeModuleId &&
                item.IsActive &&
                item.Tutor.IsActive &&
                item.Tutor.Status == TutorStatus.Approved &&
                item.Tutor.ApplicationStage == TutorApplicationStage.Placement,
                cancellationToken);
        if (assignment is null)
        {
            SetNominationError(
                "Select an active tutor and one of their assigned modules.");
            return RedirectToNominations();
        }

        ResourceTutorNomination? nomination = await context
            .ResourceTutorNominations
            .SingleOrDefaultAsync(item =>
                item.TutorId == tutorId &&
                item.ProgrammeModuleId == programmeModuleId,
                cancellationToken);
        if (nomination?.IsActive == true)
        {
            SetNominationError(
                $"{GetTutorName(assignment.Tutor)} is already nominated for " +
                $"{assignment.ProgrammeModule.ModuleCode}.");
            return RedirectToNominations();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        int tutorHeadUserId = currentUserService.GetRequiredUser().BcUserId;
        if (nomination is null)
        {
            nomination = new ResourceTutorNomination
            {
                TutorId = tutorId,
                ProgrammeModuleId = programmeModuleId
            };
            context.ResourceTutorNominations.Add(nomination);
        }

        nomination.IsActive = true;
        nomination.NominatedAt = now;
        nomination.NominatedByBcUserId = tutorHeadUserId;
        nomination.DenominatedAt = null;
        nomination.DenominatedByBcUserId = null;
        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = assignment.Tutor.BcUserId,
            Title = "Resource nomination added",
            Message = $"You can now create learning resources for " +
                $"{assignment.ProgrammeModule.ModuleCode}.",
            LinkUrl = "/Tutors/ManageResources",
            CreatedAt = now
        });
        await context.SaveChangesAsync(cancellationToken);

        ResourceMessage = $"{GetTutorName(assignment.Tutor)} was nominated " +
            $"for {assignment.ProgrammeModule.ModuleCode}.";
        return RedirectToNominations();
    }

    public async Task<IActionResult> OnPostDenominateAsync(
        int tutorId,
        int programmeModuleId,
        CancellationToken cancellationToken)
    {
        ResourceTutorNomination? nomination = await context
            .ResourceTutorNominations
            .Include(item => item.Tutor)
                .ThenInclude(tutor => tutor.BcUser)
            .Include(item => item.ProgrammeModule)
            .SingleOrDefaultAsync(item =>
                item.TutorId == tutorId &&
                item.ProgrammeModuleId == programmeModuleId &&
                item.IsActive,
                cancellationToken);
        if (nomination is null)
        {
            SetNominationError("That nomination is no longer active.");
            return RedirectToNominations();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        nomination.IsActive = false;
        nomination.DenominatedAt = now;
        nomination.DenominatedByBcUserId =
            currentUserService.GetRequiredUser().BcUserId;
        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = nomination.Tutor.BcUserId,
            Title = "Resource nomination removed",
            Message = $"You can no longer create learning resources for " +
                $"{nomination.ProgrammeModule.ModuleCode}. Existing resources " +
                "remain available.",
            LinkUrl = "/Tutors/ManageResources",
            CreatedAt = now
        });
        await context.SaveChangesAsync(cancellationToken);

        ResourceMessage = $"{GetTutorName(nomination.Tutor)} was de-nominated " +
            $"from {nomination.ProgrammeModule.ModuleCode}.";
        return RedirectToNominations();
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int resourceId,
        CancellationToken cancellationToken)
    {
        LearningResource? resource = await context.LearningResources
            .Include(item => item.Documents)
            .Include(item => item.Tutor)
            .SingleOrDefaultAsync(item =>
                item.LearningResourceId == resourceId,
                cancellationToken);
        if (resource is null)
        {
            return NotFound();
        }

        List<ResourceComment> comments = await context.ResourceComments
            .Where(comment => comment.ResourceId == resourceId)
            .ToListAsync(cancellationToken);
        string[] documentUrls = resource.Documents
            .Select(document => document.FileUrl)
            .ToArray();

        context.ResourceComments.RemoveRange(comments);
        context.LearningResources.Remove(resource);
        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = resource.Tutor.BcUserId,
            Title = "Learning resource removed",
            Message = $"Your learning resource \"{resource.Topic}\" was removed by the Tutor Head.",
            LinkUrl = "/Tutors/ManageResources",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);

        foreach (string documentUrl in documentUrls)
        {
            DeleteUploadedFile(documentUrl);
        }

        ResourceMessage = $"{resource.Topic} was deleted.";
        return RedirectToPage(new
        {
            Tab = "resources",
            SearchName,
            SearchModule,
            SearchCourse,
            ResourcePage
        });
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        if (IsNominationsTab)
        {
            await LoadNominationsAsync(cancellationToken);
            await LoadAvailableTutorOptionsAsync(cancellationToken);
            return;
        }

        Tab = "resources";
        await LoadResourcesAsync(cancellationToken);
    }

    private async Task LoadResourcesAsync(CancellationToken cancellationToken)
    {
        IQueryable<LearningResource> query = context.LearningResources
            .AsNoTracking()
            .Where(resource =>
                resource.Status == LearningResourceStatus.Published);

        if (!string.IsNullOrWhiteSpace(SearchName))
        {
            string name = SearchName.Trim();
            query = query.Where(resource =>
                resource.Topic.Contains(name) ||
                resource.Tutor.BcUser.DisplayName.Contains(name) ||
                (resource.Tutor.BcUser.PersonnelNumber ?? string.Empty)
                    .Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(SearchModule))
        {
            string module = SearchModule.Trim();
            query = query.Where(resource =>
                resource.ProgrammeModule.ModuleCode.Contains(module) ||
                resource.ProgrammeModule.ModuleName.Contains(module));
        }
        if (!string.IsNullOrWhiteSpace(SearchCourse))
        {
            string course = SearchCourse.Trim();
            query = query.Where(resource =>
                resource.ProgrammeModule.Programme.Name.Contains(course));
        }

        TotalResources = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(
            1,
            (int)Math.Ceiling(TotalResources / (double)PageSize));
        ResourcePage = Math.Clamp(ResourcePage, 1, TotalPages);

        Resources = await query
            .OrderByDescending(resource =>
                resource.DateUpdated ??
                resource.DatePublished ??
                resource.DateCreated)
            .ThenByDescending(resource => resource.LearningResourceId)
            .Skip((ResourcePage - 1) * PageSize)
            .Take(PageSize)
            .Select(resource => new TutorHeadLearningResourceListItem
            {
                LearningResourceId = resource.LearningResourceId,
                Topic = resource.Topic,
                Content = resource.Content,
                ModuleCode = resource.ProgrammeModule.ModuleCode,
                ModuleName = resource.ProgrammeModule.ModuleName,
                TutorId = resource.TutorId,
                TutorName = string.IsNullOrWhiteSpace(
                    resource.Tutor.BcUser.DisplayName)
                    ? resource.Tutor.BcUser.PersonnelNumber ?? "Tutor"
                    : resource.Tutor.BcUser.DisplayName,
                TutorProfileImagePath = resource.Tutor.ProfileImagePath,
                DatePublished = resource.DatePublished,
                DateCreated = resource.DateCreated,
                Status = resource.Status,
                CommentCount = resource.Comments.Count(comment =>
                    !comment.IsDeleted)
            })
            .ToListAsync(cancellationToken);
    }

    private async Task LoadNominationsAsync(CancellationToken cancellationToken)
    {
        IQueryable<Tutor> tutorQuery = context.Tutors
            .AsNoTracking()
            .Where(tutor => tutor.ResourceTutorNominations.Any(nomination =>
                nomination.IsActive));

        if (!string.IsNullOrWhiteSpace(NomineeSearch))
        {
            string name = NomineeSearch.Trim();
            tutorQuery = tutorQuery.Where(tutor =>
                tutor.BcUser.DisplayName.Contains(name) ||
                (tutor.BcUser.PersonnelNumber ?? string.Empty).Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(NominationModuleSearch))
        {
            string module = NominationModuleSearch.Trim();
            tutorQuery = tutorQuery.Where(tutor =>
                tutor.ResourceTutorNominations.Any(nomination =>
                    nomination.IsActive &&
                    (nomination.ProgrammeModule.ModuleCode.Contains(module) ||
                     nomination.ProgrammeModule.ModuleName.Contains(module))));
        }
        if (!string.IsNullOrWhiteSpace(NominationCourseSearch))
        {
            string course = NominationCourseSearch.Trim();
            tutorQuery = tutorQuery.Where(tutor =>
                tutor.ResourceTutorNominations.Any(nomination =>
                    nomination.IsActive &&
                    nomination.ProgrammeModule.Programme.Name.Contains(course)));
        }

        TotalNominatedTutors = await tutorQuery.CountAsync(cancellationToken);
        TotalNomineePages = Math.Max(
            1,
            (int)Math.Ceiling(TotalNominatedTutors / (double)PageSize));
        NomineePage = Math.Clamp(NomineePage, 1, TotalNomineePages);

        List<int> tutorIds = await tutorQuery
            .OrderBy(tutor => tutor.BcUser.DisplayName)
            .ThenBy(tutor => tutor.TutorId)
            .Skip((NomineePage - 1) * PageSize)
            .Take(PageSize)
            .Select(tutor => tutor.TutorId)
            .ToListAsync(cancellationToken);

        List<NominationRow> rows = await context.ResourceTutorNominations
            .AsNoTracking()
            .Where(nomination =>
                nomination.IsActive &&
                tutorIds.Contains(nomination.TutorId))
            .OrderBy(nomination => nomination.ProgrammeModule.ModuleCode)
            .Select(nomination => new NominationRow(
                nomination.TutorId,
                nomination.Tutor.BcUser.DisplayName,
                nomination.Tutor.BcUser.PersonnelNumber,
                nomination.Tutor.ProfileImagePath,
                nomination.ProgrammeModuleId,
                nomination.ProgrammeModule.ModuleCode,
                nomination.ProgrammeModule.ModuleName,
                nomination.ProgrammeModule.Programme.Name,
                nomination.NominatedAt))
            .ToListAsync(cancellationToken);

        NominatedTutors = rows
            .GroupBy(row => new
            {
                row.TutorId,
                row.TutorName,
                row.PersonnelNumber,
                row.ProfileImagePath
            })
            .OrderBy(group => tutorIds.IndexOf(group.Key.TutorId))
            .Select(group => new NominatedTutorListItem(
                group.Key.TutorId,
                string.IsNullOrWhiteSpace(group.Key.TutorName)
                    ? group.Key.PersonnelNumber ?? "Tutor"
                    : group.Key.TutorName,
                group.Key.PersonnelNumber,
                group.Key.ProfileImagePath,
                group.Select(row => new NominatedModuleListItem(
                    row.ProgrammeModuleId,
                    row.ModuleCode,
                    row.ModuleName,
                    row.CourseName,
                    row.NominatedAt)).ToList()))
            .ToList();
    }

    private async Task LoadAvailableTutorOptionsAsync(
        CancellationToken cancellationToken)
    {
        List<NominationOptionRow> rows = await context.TutorCourseModules
            .AsNoTracking()
            .Where(assignment =>
                assignment.IsActive &&
                assignment.Tutor.IsActive &&
                assignment.Tutor.Status == TutorStatus.Approved &&
                assignment.Tutor.ApplicationStage ==
                    TutorApplicationStage.Placement &&
                !context.ResourceTutorNominations.Any(nomination =>
                    nomination.TutorId == assignment.TutorId &&
                    nomination.ProgrammeModuleId ==
                        assignment.ProgrammeModuleId &&
                    nomination.IsActive))
            .OrderBy(assignment => assignment.Tutor.BcUser.DisplayName)
            .ThenBy(assignment => assignment.ProgrammeModule.ModuleCode)
            .Select(assignment => new NominationOptionRow(
                assignment.TutorId,
                assignment.Tutor.BcUser.DisplayName,
                assignment.Tutor.BcUser.PersonnelNumber,
                assignment.ProgrammeModuleId,
                assignment.ProgrammeModule.ModuleCode,
                assignment.ProgrammeModule.ModuleName))
            .ToListAsync(cancellationToken);

        AvailableTutorOptions = rows
            .GroupBy(row => new
            {
                row.TutorId,
                row.TutorName,
                row.PersonnelNumber
            })
            .Select(group => new NominationTutorOption(
                group.Key.TutorId,
                string.IsNullOrWhiteSpace(group.Key.TutorName)
                    ? group.Key.PersonnelNumber ?? "Tutor"
                    : group.Key.TutorName,
                group.Key.PersonnelNumber,
                group.Select(row => new NominationModuleOption(
                    row.ProgrammeModuleId,
                    row.ModuleCode,
                    row.ModuleName)).ToList()))
            .ToList();
    }

    private void SetNominationError(string message)
    {
        ResourceMessage = message;
        ResourceMessageIsError = true;
    }

    private RedirectToPageResult RedirectToNominations() =>
        RedirectToPage(new
        {
            Tab = "nominated",
            NomineeSearch,
            NominationModuleSearch,
            NominationCourseSearch,
            NomineePage
        });

    private static string GetTutorName(Tutor tutor) =>
        string.IsNullOrWhiteSpace(tutor.BcUser.DisplayName)
            ? tutor.BcUser.PersonnelNumber ?? "Tutor"
            : tutor.BcUser.DisplayName;

    private void DeleteUploadedFile(string fileUrl)
    {
        string relativePath = fileUrl.TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(
            environment.WebRootPath,
            relativePath));
        string uploadRoot = Path.GetFullPath(Path.Combine(
            environment.WebRootPath,
            "uploads",
            "learning-resources")) + Path.DirectorySeparatorChar;

        if (fullPath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase) &&
            System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}

public sealed record NominatedTutorListItem(
    int TutorId,
    string TutorName,
    string? PersonnelNumber,
    string? ProfileImagePath,
    IReadOnlyList<NominatedModuleListItem> Modules)
{
    public string TutorInitials
    {
        get
        {
            string[] words = TutorName.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(words.Take(2).Select(word =>
                char.ToUpperInvariant(word[0])));
        }
    }
}

public sealed record NominatedModuleListItem(
    int ProgrammeModuleId,
    string ModuleCode,
    string ModuleName,
    string CourseName,
    DateTimeOffset NominatedAt);

public sealed record NominationTutorOption(
    int TutorId,
    string TutorName,
    string? PersonnelNumber,
    IReadOnlyList<NominationModuleOption> Modules);

public sealed record NominationModuleOption(
    int ProgrammeModuleId,
    string ModuleCode,
    string ModuleName);

internal sealed record NominationRow(
    int TutorId,
    string TutorName,
    string? PersonnelNumber,
    string? ProfileImagePath,
    int ProgrammeModuleId,
    string ModuleCode,
    string ModuleName,
    string CourseName,
    DateTimeOffset NominatedAt);

internal sealed record NominationOptionRow(
    int TutorId,
    string TutorName,
    string? PersonnelNumber,
    int ProgrammeModuleId,
    string ModuleCode,
    string ModuleName);

public sealed class TutorHeadLearningResourceListItem
    : StudentLearningResourceListItem
{
    private static readonly TimeSpan SouthAfricaOffset = TimeSpan.FromHours(2);

    public LearningResourceStatus Status { get; set; }
    public DateTimeOffset DateCreated { get; set; }
    public int CommentCount { get; set; }

    public string DateLabel => Status == LearningResourceStatus.Published
        ? "Posted"
        : "Created";

    public string DateDisplay => (DatePublished ?? DateCreated)
        .ToOffset(SouthAfricaOffset)
        .ToString("dd/MM/yyyy");
}
