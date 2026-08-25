using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BC_CampusLearn.Pages.Tutors;

public class ManageResourcesModel : PageModel
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private const int MaximumFileCount = 5;
    private const int MaximumContentOperations = 1000;
    private const int MaximumContentTextLength = 100000;
    private static readonly HashSet<string> AllowedContentAttributes =
        new(StringComparer.Ordinal)
        {
            "header", "bold", "italic", "underline", "list",
            "blockquote", "code-block", "link"
        };
    private static readonly TimeSpan SouthAfricaOffset =
        TimeSpan.FromHours(2);
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".ppt", ".pptx",
            ".xls", ".xlsx", ".txt", ".zip"
        };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;

    public ManageResourcesModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _currentUserService = currentUserService;
        _environment = environment;
    }

    [BindProperty]
    public LearningResourceInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ModuleCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public LearningResourceStatus? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? PublishedFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? PublishedTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? Engagement { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Create { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public IReadOnlyList<SelectListItem> ModuleOptions { get; private set; }
        = Array.Empty<SelectListItem>();
    public IReadOnlyList<ResourceListItem> Resources { get; private set; }
        = Array.Empty<ResourceListItem>();
    public IReadOnlyList<ResourceDocumentItem> ExistingDocuments { get; private set; }
        = Array.Empty<ResourceDocumentItem>();
    public bool IsEditing => Input.LearningResourceId.HasValue;
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(ModuleCode) ||
        Status.HasValue ||
        PublishedFrom.HasValue ||
        PublishedTo.HasValue ||
        Engagement == true;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        if (EditId.HasValue)
        {
            LearningResource? resource = await _context.LearningResources
                .AsNoTracking()
                .Include(item => item.Documents)
                .SingleOrDefaultAsync(item =>
                    item.LearningResourceId == EditId.Value &&
                    item.TutorId == tutorId.Value,
                    cancellationToken);

            if (resource is null)
            {
                return NotFound();
            }

            Input = new LearningResourceInput
            {
                LearningResourceId = resource.LearningResourceId,
                ProgrammeModuleId = resource.ProgrammeModuleId,
                Topic = resource.Topic,
                Content = resource.Content,
                AllowSubscriberComments = resource.AllowSubscriberComments,
                Link1 = resource.Link1,
                Link2 = resource.Link2
            };
            ExistingDocuments = resource.Documents
                .OrderBy(document => document.DocumentName)
                .Select(document => new ResourceDocumentItem
                {
                    ResourceDocumentId = document.ResourceDocumentId,
                    DocumentName = document.DocumentName,
                    FileUrl = document.FileUrl,
                    FileType = document.FileType,
                    FileSizeBytes = document.FileSizeBytes
                }).ToList();
        }

        await LoadPageAsync(tutorId.Value, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(
        string submitAction,
        CancellationToken cancellationToken)
    {
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        bool ownsModule = await _context.TutorCourseModules
            .AsNoTracking()
            .AnyAsync(item =>
                item.TutorId == tutorId.Value &&
                item.ProgrammeModuleId == Input.ProgrammeModuleId,
                cancellationToken);
        if (!ownsModule)
        {
            ModelState.AddModelError("Input.ProgrammeModuleId",
                "Select one of the modules you tutor.");
        }

        ValidateDocuments(Input.Documents ?? new List<IFormFile>());
        ValidateLearningContent(Input.Content);
        LearningResourceStatus requestedStatus =
            submitAction.Equals("publish", StringComparison.OrdinalIgnoreCase)
                ? LearningResourceStatus.Published
                : LearningResourceStatus.Draft;

        LearningResource? resource = null;
        if (Input.LearningResourceId.HasValue)
        {
            resource = await _context.LearningResources
                .Include(item => item.Documents)
                .SingleOrDefaultAsync(item =>
                    item.LearningResourceId == Input.LearningResourceId.Value &&
                    item.TutorId == tutorId.Value,
                    cancellationToken);
            if (resource is null)
            {
                return NotFound();
            }
            ExistingDocuments = MapDocuments(resource.Documents);
        }

        if (!ModelState.IsValid)
        {
            await LoadPageAsync(tutorId.Value, cancellationToken);
            return Page();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool isNew = resource is null;
        resource ??= new LearningResource
        {
            TutorId = tutorId.Value,
            DateCreated = now
        };
        resource.ProgrammeModuleId = Input.ProgrammeModuleId;
        resource.Topic = Input.Topic.Trim();
        resource.Content = Input.Content.Trim();
        resource.AllowSubscriberComments = Input.AllowSubscriberComments;
        resource.Link1 = CleanOptional(Input.Link1);
        resource.Link2 = CleanOptional(Input.Link2);
        resource.Status = requestedStatus;
        resource.DateUpdated = isNew ? null : now;
        if (requestedStatus == LearningResourceStatus.Published)
        {
            resource.DatePublished ??= now;
        }

        if (isNew)
        {
            _context.LearningResources.Add(resource);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await SaveDocumentsAsync(
            resource,
            Input.Documents ?? new List<IFormFile>(),
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        SuccessMessage = requestedStatus == LearningResourceStatus.Published
            ? $"“{resource.Topic}” was published successfully."
            : $"“{resource.Topic}” was saved as a draft.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(
        int resourceId,
        LearningResourceStatus newStatus,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(newStatus))
        {
            return BadRequest();
        }

        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        LearningResource? resource = await _context.LearningResources
            .SingleOrDefaultAsync(item =>
                item.LearningResourceId == resourceId &&
                item.TutorId == tutorId.Value,
                cancellationToken);
        if (resource is null)
        {
            return NotFound();
        }

        resource.Status = newStatus;
        resource.DateUpdated = DateTimeOffset.UtcNow;
        if (newStatus == LearningResourceStatus.Published)
        {
            resource.DatePublished ??= DateTimeOffset.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"“{resource.Topic}” is now {newStatus.ToString().ToLowerInvariant()}.";
        return RedirectToPage(new
        {
            Search,
            ModuleCode,
            Status,
            PublishedFrom,
            PublishedTo,
            Engagement
        });
    }

    public async Task<IActionResult> OnPostToggleEngagementAsync(
        int resourceId,
        CancellationToken cancellationToken)
    {
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        LearningResource? resource = await _context.LearningResources
            .SingleOrDefaultAsync(item =>
                item.LearningResourceId == resourceId &&
                item.TutorId == tutorId.Value,
                cancellationToken);
        if (resource is null)
        {
            return NotFound();
        }

        resource.AllowSubscriberComments =
            !resource.AllowSubscriberComments;
        resource.DateUpdated = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToPage(new
        {
            Search,
            ModuleCode,
            Status,
            PublishedFrom,
            PublishedTo,
            Engagement
        });
    }

    public async Task<IActionResult> OnPostDeleteDocumentAsync(
        int documentId,
        int resourceId,
        CancellationToken cancellationToken)
    {
        int? tutorId = await GetCurrentTutorIdAsync(cancellationToken);
        if (!tutorId.HasValue)
        {
            return Forbid();
        }

        LearningResourceDocument? document =
            await _context.LearningResourceDocuments
                .Include(item => item.Resource)
                .SingleOrDefaultAsync(item =>
                    item.ResourceDocumentId == documentId &&
                    item.ResourceId == resourceId &&
                    item.Resource.TutorId == tutorId.Value,
                    cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        _context.LearningResourceDocuments.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);
        DeleteUploadedFile(document.FileUrl);
        SuccessMessage = $"{document.DocumentName} was removed.";
        return RedirectToPage(new { editId = resourceId });
    }

    private async Task LoadPageAsync(int tutorId, CancellationToken cancellationToken)
    {
        ModuleOptions = await _context.TutorCourseModules
            .AsNoTracking()
            .Where(item => item.TutorId == tutorId)
            .OrderBy(item => item.ProgrammeModule.ModuleCode)
            .Select(item => new SelectListItem
            {
                Value = item.ProgrammeModuleId.ToString(),
                Text = item.ProgrammeModule.ModuleCode + " — " +
                    item.ProgrammeModule.ModuleName
            }).ToListAsync(cancellationToken);

        IQueryable<LearningResource> baseQuery = _context.LearningResources
            .AsNoTracking().Where(item => item.TutorId == tutorId);
        IQueryable<LearningResource> query = baseQuery;
        if (Status.HasValue)
        {
            query = query.Where(item => item.Status == Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(ModuleCode))
        {
            string moduleCode = ModuleCode.Trim();
            query = query.Where(item =>
                item.ProgrammeModule.ModuleCode.Contains(moduleCode));
        }
        if (!string.IsNullOrWhiteSpace(Search))
        {
            string term = Search.Trim();
            query = query.Where(item =>
                item.Topic.Contains(term) || item.Content.Contains(term));
        }
        if (PublishedFrom.HasValue)
        {
            DateTimeOffset publishedStart = new(
                PublishedFrom.Value.ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Unspecified),
                SouthAfricaOffset);
            query = query.Where(item =>
                item.DatePublished >= publishedStart.ToUniversalTime());
        }
        if (PublishedTo.HasValue)
        {
            DateTimeOffset publishedEnd = new(
                PublishedTo.Value.AddDays(1).ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Unspecified),
                SouthAfricaOffset);
            query = query.Where(item =>
                item.DatePublished < publishedEnd.ToUniversalTime());
        }
        if (Engagement == true)
        {
            query = query.Where(item =>
                item.AllowSubscriberComments);
        }

        Resources = await query
            .OrderByDescending(item => item.DateUpdated ?? item.DateCreated)
            .Select(item => new ResourceListItem
            {
                LearningResourceId = item.LearningResourceId,
                Topic = item.Topic,
                ModuleCode = item.ProgrammeModule.ModuleCode,
                ModuleName = item.ProgrammeModule.ModuleName,
                Status = item.Status,
                DateCreated = item.DateCreated,
                DatePublished = item.DatePublished,
                DateUpdated = item.DateUpdated,
                DocumentCount = item.Documents.Count,
                AllowSubscriberComments = item.AllowSubscriberComments,
                UnreadCommentCount = item.AllowSubscriberComments
                    ? item.Comments.Count(comment =>
                        !comment.IsDeleted &&
                        comment.AuthorUserId != item.Tutor.BcUserId &&
                        (!item.TutorLastViewedDiscussionAt.HasValue ||
                         comment.DateCreated >
                         item.TutorLastViewedDiscussionAt.Value))
                    : 0
            }).ToListAsync(cancellationToken);
    }

    private void ValidateDocuments(IEnumerable<IFormFile> documents)
    {
        List<IFormFile> files = documents.Where(file => file.Length > 0).ToList();
        if (files.Count > MaximumFileCount)
        {
            ModelState.AddModelError("Input.Documents", "Upload a maximum of 5 files at a time.");
        }
        foreach (IFormFile file in files)
        {
            string extension = Path.GetExtension(Path.GetFileName(file.FileName));
            if (!AllowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Input.Documents",
                    $"{Path.GetFileName(file.FileName)} is not a supported file type.");
            }
            if (file.Length > MaximumFileSize)
            {
                ModelState.AddModelError("Input.Documents",
                    $"{Path.GetFileName(file.FileName)} exceeds the 10 MB limit.");
            }
        }
    }

    private void ValidateLearningContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        string trimmedContent = content.Trim();
        if (!trimmedContent.StartsWith('{'))
        {
            if (trimmedContent.Length > MaximumContentTextLength)
            {
                ModelState.AddModelError("Input.Content",
                    "The learning content is too long.");
            }
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(trimmedContent,
                new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("ops", out JsonElement operations) ||
                operations.ValueKind != JsonValueKind.Array)
            {
                AddInvalidContentError();
                return;
            }

            int operationCount = 0;
            int textLength = 0;
            bool hasVisibleText = false;
            foreach (JsonElement operation in operations.EnumerateArray())
            {
                operationCount++;
                if (operationCount > MaximumContentOperations ||
                    operation.ValueKind != JsonValueKind.Object ||
                    !operation.TryGetProperty("insert", out JsonElement insert) ||
                    insert.ValueKind != JsonValueKind.String)
                {
                    AddInvalidContentError();
                    return;
                }

                string insertedText = insert.GetString() ?? string.Empty;
                textLength += insertedText.Length;
                hasVisibleText |= !string.IsNullOrWhiteSpace(insertedText);
                if (textLength > MaximumContentTextLength ||
                    operation.TryGetProperty("delete", out _) ||
                    operation.TryGetProperty("retain", out _))
                {
                    AddInvalidContentError();
                    return;
                }

                if (!operation.TryGetProperty("attributes", out JsonElement attributes))
                {
                    continue;
                }
                if (attributes.ValueKind != JsonValueKind.Object)
                {
                    AddInvalidContentError();
                    return;
                }
                foreach (JsonProperty attribute in attributes.EnumerateObject())
                {
                    if (!AllowedContentAttributes.Contains(attribute.Name) ||
                        (attribute.Name == "link" && !IsAllowedLearningLink(attribute.Value)))
                    {
                        AddInvalidContentError();
                        return;
                    }
                }
            }

            if (!hasVisibleText)
            {
                ModelState.AddModelError("Input.Content", "Add the learning content.");
            }
        }
        catch (JsonException)
        {
            AddInvalidContentError();
        }
    }

    private static bool IsAllowedLearningLink(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(value.GetString(), UriKind.Absolute, out Uri? uri))
        {
            return false;
        }
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private void AddInvalidContentError() =>
        ModelState.AddModelError("Input.Content",
            "The learning content contains unsupported formatting. Please edit and try again.");

    private async Task SaveDocumentsAsync(
        LearningResource resource,
        IEnumerable<IFormFile> documents,
        CancellationToken cancellationToken)
    {
        string relativeDirectory = Path.Combine(
            "uploads", "learning-resources", resource.LearningResourceId.ToString());
        string directory = Path.Combine(_environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(directory);

        foreach (IFormFile file in documents.Where(file => file.Length > 0))
        {
            string originalName = Path.GetFileName(file.FileName);
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            string storedName = $"{Guid.NewGuid():N}{extension}";
            string path = Path.Combine(directory, storedName);
            await using FileStream stream = new(path, FileMode.CreateNew);
            await file.CopyToAsync(stream, cancellationToken);

            resource.Documents.Add(new LearningResourceDocument
            {
                DocumentName = originalName,
                FileUrl = "/" + Path.Combine(relativeDirectory, storedName).Replace('\\', '/'),
                FileType = extension.TrimStart('.').ToUpperInvariant(),
                FileSizeBytes = file.Length,
                DateUploaded = DateTimeOffset.UtcNow
            });
        }
    }

    private void DeleteUploadedFile(string fileUrl)
    {
        string relativePath = fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relativePath));
        string uploadRoot = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath, "uploads", "learning-resources")) +
            Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase) &&
            System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }

    private async Task<int?> GetCurrentTutorIdAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        return await _context.Tutors.AsNoTracking()
            .Where(tutor => tutor.BcUserId == currentUser.BcUserId)
            .Select(tutor => (int?)tutor.TutorId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<ResourceDocumentItem> MapDocuments(
        IEnumerable<LearningResourceDocument> documents) =>
        documents.OrderBy(item => item.DocumentName).Select(item =>
            new ResourceDocumentItem
            {
                ResourceDocumentId = item.ResourceDocumentId,
                DocumentName = item.DocumentName,
                FileUrl = item.FileUrl,
                FileType = item.FileType,
                FileSizeBytes = item.FileSizeBytes
            }).ToList();
}

public class ResourceListItem
{
    private static readonly TimeSpan SouthAfricaOffset =
        TimeSpan.FromHours(2);

    public int LearningResourceId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public LearningResourceStatus Status { get; set; }
    public DateTimeOffset DateCreated { get; set; }
    public DateTimeOffset? DatePublished { get; set; }
    public DateTimeOffset? DateUpdated { get; set; }
    public int DocumentCount { get; set; }
    public bool AllowSubscriberComments { get; set; }
    public int UnreadCommentCount { get; set; }

    public string TopicDisplay
    {
        get
        {
            string[] words = Topic.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            return words.Length <= 4
                ? Topic
                : string.Join(" ", words.Take(4)) + "…";
        }
    }

    public string PublishedDateDisplay
    {
        get
        {
            if (!DatePublished.HasValue)
            {
                return "Not yet published";
            }

            DateTimeOffset localPublished = DatePublished.Value
                .ToOffset(SouthAfricaOffset);
            DateTime localToday = DateTimeOffset.UtcNow
                .ToOffset(SouthAfricaOffset)
                .Date;

            if (localPublished.Date == localToday)
            {
                TimeSpan elapsed =
                    DateTimeOffset.UtcNow -
                    DatePublished.Value.ToUniversalTime();

                if (elapsed < TimeSpan.FromMinutes(1))
                {
                    return "Just now";
                }

                if (elapsed <= TimeSpan.FromMinutes(30))
                {
                    int elapsedMinutes = Math.Max(
                        1,
                        (int)Math.Floor(elapsed.TotalMinutes));
                    string unit = elapsedMinutes == 1
                        ? "minute"
                        : "minutes";

                    return $"{elapsedMinutes} {unit} ago";
                }

                return "Today";
            }

            if (localPublished.Date == localToday.AddDays(-1))
            {
                return "Yesterday";
            }

            int daysSinceMonday =
                ((int)localToday.DayOfWeek + 6) % 7;
            DateTime currentWeekStart = localToday
                .AddDays(-daysSinceMonday);

            if (localPublished.Date >= currentWeekStart &&
                localPublished.Date < currentWeekStart.AddDays(7))
            {
                return localPublished.ToString("dddd");
            }

            return localPublished.ToString("dd/MM/yyyy");
        }
    }

    public string? PublishedTimeDisplay =>
        DatePublished?.ToOffset(SouthAfricaOffset).ToString("HH:mm");
}

public class ResourceDocumentItem
{
    public int ResourceDocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}
