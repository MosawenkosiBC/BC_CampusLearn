using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.LearningResources;

public class IndexModel(
    ApplicationDbContext context,
    IWebHostEnvironment environment) : PageModel
{
    public const int ResourcesPerRow = 3;
    public const int RowsPerPage = 3;
    public const int PageSize = ResourcesPerRow * RowsPerPage;

    [BindProperty(SupportsGet = true)]
    public string? SearchName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchModule { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchCourse { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ResourcePage { get; set; } = 1;

    public int TotalResources { get; private set; }
    public int TotalPages { get; private set; }
    public IReadOnlyList<AdminLearningResourceListItem> Resources
    { get; private set; } = [];

    [TempData]
    public string? ResourceMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

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
            Message = $"Your learning resource \"{resource.Topic}\" was removed by an administrator.",
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
            SearchName,
            SearchModule,
            SearchCourse,
            ResourcePage
        });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IQueryable<LearningResource> query = context.LearningResources
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(SearchName))
        {
            string name = SearchName.Trim();
            query = query.Where(resource =>
                resource.Topic.Contains(name) ||
                resource.Tutor.BcUser.DisplayName.Contains(name) ||
                resource.Tutor.BcUser.PersonnelNumber.Contains(name));
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
            .Select(resource => new AdminLearningResourceListItem
            {
                LearningResourceId = resource.LearningResourceId,
                Topic = resource.Topic,
                Content = resource.Content,
                ModuleCode = resource.ProgrammeModule.ModuleCode,
                ModuleName = resource.ProgrammeModule.ModuleName,
                TutorId = resource.TutorId,
                TutorName = string.IsNullOrWhiteSpace(
                    resource.Tutor.BcUser.DisplayName)
                    ? resource.Tutor.BcUser.PersonnelNumber
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

public sealed class AdminLearningResourceListItem
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
