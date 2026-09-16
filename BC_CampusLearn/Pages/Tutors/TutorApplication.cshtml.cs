using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

[Authorize]
public class TutorApplicationModel : PageModel
{
    private const long MaximumDocumentSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedDocumentExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx",
            ".png",
            ".jpg",
            ".jpeg"
        };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;

    public TutorApplicationModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _currentUserService = currentUserService;
        _environment = environment;
    }

    [BindProperty]
    public TutorApplicationStageOneInput Input { get; set; } = new();

    [BindProperty]
    public TutorApplicationStageTwoInput ProfileInput { get; set; } = new();

    [BindProperty]
    public TutorApplicationStageThreeInput FinalInput { get; set; } = new();

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string StudentNumber { get; private set; } = string.Empty;
    public string EmailAddress { get; private set; } = string.Empty;
    public bool ApplicationsOpen { get; private set; }
    public bool ShowClosedApplicationsModal { get; private set; }

    public int InitialStep { get; private set; }

    public string? ExistingApplicationMessage { get; private set; }

    [TempData]
    public string? SubmissionMessage { get; set; }

    public List<SelectListItem> ProgrammeOptions { get; private set; }
        = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        await LoadPageDataAsync(currentUser, cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        await LoadPageDataAsync(currentUser, cancellationToken);

        if (!ApplicationsOpen)
        {
            InitialStep = 0;
            ShowClosedApplicationsModal = true;
            ModelState.AddModelError(
                string.Empty,
                "Tutor applications are currently closed.");
            return Page();
        }

        InitialStep = 3;

        if (!string.IsNullOrWhiteSpace(ExistingApplicationMessage))
        {
            ModelState.AddModelError(
                string.Empty,
                ExistingApplicationMessage);
        }

        if (Input.ProgrammeId.HasValue &&
            !await _context.ProgrammesOfStudy
                .AsNoTracking()
                .AnyAsync(
                    programme => programme.Id == Input.ProgrammeId.Value,
                    cancellationToken))
        {
            ModelState.AddModelError(
                "Input.ProgrammeId",
                "Select a valid programme.");
        }

        List<int> moduleIds = FinalInput.ProgrammeModuleIds
            .Distinct()
            .ToList();

        if (moduleIds.Count is < 2 or > 5)
        {
            ModelState.AddModelError(
                "FinalInput.ProgrammeModuleIds",
                "Select between two and five different modules.");
        }
        else if (Input.YearOfStudy.HasValue)
        {
            int eligibleModuleCount = await _context.ProgrammeModules
                .AsNoTracking()
                .CountAsync(
                    module =>
                        moduleIds.Contains(module.ProgrammeModuleId) &&
                        module.YearOfStudy <= Input.YearOfStudy.Value,
                    cancellationToken);

            if (eligibleModuleCount != moduleIds.Count)
            {
                ModelState.AddModelError(
                    "FinalInput.ProgrammeModuleIds",
                    "One or more selected modules are not eligible for your year of study.");
            }
        }

        ValidateDocument(FinalInput.Transcript, "FinalInput.Transcript");
        ValidateDocument(
            FinalInput.AdditionalCertificate,
            "FinalInput.AdditionalCertificate");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        DateTime submittedAt = DateTime.UtcNow;
        Tutor? tutor = await _context.Tutors
            .Include(item => item.TutorCourseModules)
            .Include(item => item.TutorDocuments)
            .SingleOrDefaultAsync(
                item => item.BcUserId == currentUser.BcUserId,
                cancellationToken);
        if (tutor is not null)
        {
            ModelState.AddModelError(
                string.Empty,
                "You already have a tutor application or profile.");
            return Page();
        }

        tutor = new Tutor
        {
            BcUserId = currentUser.BcUserId,
            CreatedAt = submittedAt
        };

        tutor.ProgrammeId = Input.ProgrammeId!.Value;
        tutor.OverallAverage = Input.OverallAverage!.Value;
        tutor.YearOfStudy = Input.YearOfStudy!.Value;
        tutor.PhoneNumber = Input.PhoneNumber!.Trim();
        tutor.ReasonForTutoring = ProfileInput.ReasonForTutoring!.Trim();
        tutor.TeachingStyle = ProfileInput.TeachingStyle!.Trim();
        tutor.PreviousTutoringExperience =
            ProfileInput.PreviousTutoringExperience!.Trim();
        tutor.CampusOfStudy = ProfileInput.CampusOfStudy!.Trim();
        tutor.DemonstrationVideoUrl =
            ProfileInput.DemonstrationVideoUrl!.Trim();
        tutor.PreferredTutoringMode =
            FinalInput.PreferredTutoringMode!.Value;
        tutor.Status = TutorStatus.Pending;
        tutor.ApplicationStage = TutorApplicationStage.Submitted;
        tutor.ShortlistReason = null;
        tutor.ReviewedAt = null;
        tutor.IsActive = false;
        tutor.SubmittedAt = submittedAt;
        tutor.UpdatedAt = null;

        foreach (int moduleId in moduleIds)
        {
            tutor.TutorCourseModules.Add(
                new TutorCourseModule
                {
                    ProgrammeModuleId = moduleId
                });
        }

        string directoryName = Guid.NewGuid().ToString("N");
        string relativeDirectory = Path.Combine(
            "App_Data",
            "tutor-documents",
            directoryName);
        string documentDirectory = Path.Combine(
            _environment.ContentRootPath,
            relativeDirectory);

        try
        {
            Directory.CreateDirectory(documentDirectory);

            await AddDocumentAsync(
                tutor,
                FinalInput.Transcript!,
                TutorDocumentType.AcademicTranscript,
                relativeDirectory,
                documentDirectory,
                submittedAt,
                cancellationToken);

            if (FinalInput.AdditionalCertificate is not null)
            {
                await AddDocumentAsync(
                    tutor,
                    FinalInput.AdditionalCertificate,
                    TutorDocumentType.ExternalCertificate,
                    relativeDirectory,
                    documentDirectory,
                    submittedAt,
                    cancellationToken);
            }

            _context.Tutors.Add(tutor);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            DeleteDocumentDirectory(documentDirectory);
            throw;
        }
        catch (IOException)
        {
            DeleteDocumentDirectory(documentDirectory);
            ModelState.AddModelError(
                string.Empty,
                "Your documents could not be stored. Please try again.");
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            DeleteDocumentDirectory(documentDirectory);
            ModelState.AddModelError(
                string.Empty,
                "Your documents could not be stored. Please try again.");
            return Page();
        }
        catch (DbUpdateException)
        {
            DeleteDocumentDirectory(documentDirectory);
            ModelState.AddModelError(
                string.Empty,
                "Your tutor application could not be submitted. Please try again.");
            return Page();
        }
        catch
        {
            DeleteDocumentDirectory(documentDirectory);
            throw;
        }

        SubmissionMessage =
            "Your tutor application has been submitted for review.";
        return RedirectToPage();
    }

    public async Task<JsonResult> OnGetModulesAsync(
        int yearOfStudy,
        CancellationToken cancellationToken)
    {
        if (yearOfStudy is < 1 or > 4)
        {
            return new JsonResult(Array.Empty<object>());
        }

        var modules = await _context.ProgrammeModules
            .AsNoTracking()
            .Where(module => module.YearOfStudy <= yearOfStudy)
            .OrderBy(module => module.YearOfStudy)
            .ThenBy(module => module.ModuleCode)
            .Select(module => new
            {
                module.ProgrammeModuleId,
                module.ModuleCode,
                module.ModuleName,
                module.YearOfStudy,
                ProgrammeName = module.Programme.Name
            })
            .ToListAsync(cancellationToken);

        return new JsonResult(modules);
    }

    private void SetIdentityDetails(CurrentUser currentUser)
    {
        string[] displayNameParts = currentUser.DisplayName
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        FirstName = displayNameParts.FirstOrDefault() ?? string.Empty;
        LastName = displayNameParts.Length > 1
            ? string.Join(' ', displayNameParts.Skip(1))
            : string.Empty;
        StudentNumber = currentUser.PersonnelNumber;
        EmailAddress = currentUser.Email ?? string.Empty;
    }

    private async Task LoadPageDataAsync(
        CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        SetIdentityDetails(currentUser);

        TutorApplicationSettings? applicationSettings = await _context
            .TutorApplicationSettings
            .AsNoTracking()
            .Where(settings => settings.TutorApplicationSettingsId ==
                TutorApplicationSettings.SingletonId)
            .SingleOrDefaultAsync(cancellationToken);
        ApplicationsOpen = applicationSettings?.IsAcceptingApplications(
            DateTime.UtcNow) ?? false;

        if (!ApplicationsOpen)
        {
            await TutorApplicationReview
                .RemoveRejectedApplicationsWhenCycleClosedAsync(
                    _context,
                    cancellationToken);
        }

        ProgrammeOptions = await _context.ProgrammesOfStudy
            .AsNoTracking()
            .OrderBy(programme => programme.Name)
            .Select(programme => new SelectListItem
            {
                Value = programme.Id.ToString(),
                Text = programme.Name
            })
            .ToListAsync(cancellationToken);

        var existingApplication = await _context.Tutors
            .AsNoTracking()
            .Where(tutor => tutor.BcUserId == currentUser.BcUserId)
            .Select(tutor => new
            {
                tutor.Status,
                tutor.ApplicationStage,
                tutor.ShortlistReason
            })
            .SingleOrDefaultAsync(cancellationToken);

        ExistingApplicationMessage = existingApplication switch
        {
            { Status: TutorStatus.Pending,
                ApplicationStage: TutorApplicationStage.Shortlisted } =>
                "Your tutor application has been shortlisted. " +
                (string.IsNullOrWhiteSpace(existingApplication.ShortlistReason)
                    ? "You will be contacted about the next phase."
                    : $"Reason: {existingApplication.ShortlistReason}"),
            { Status: TutorStatus.Pending } =>
                "You have already applied to become a tutor. " +
                "Your application is currently under review.",
            { Status: TutorStatus.Approved } =>
                "Your tutor application has been approved. " +
                "You cannot submit another application.",
            { Status: TutorStatus.Rejected } =>
                "Your tutor application was not approved. " +
                "You cannot submit another application during this application cycle. " +
                (string.IsNullOrWhiteSpace(existingApplication.ShortlistReason)
                    ? string.Empty
                    : $"Reason: {existingApplication.ShortlistReason}"),
            { Status: TutorStatus.Suspended } =>
                "Your tutor profile is currently suspended. " +
                "Please contact Student Support for assistance.",
            _ => null
        };
    }

    private void ValidateDocument(IFormFile? document, string modelKey)
    {
        if (document is null)
        {
            return;
        }

        string originalFileName = Path.GetFileName(document.FileName);
        string extension = Path.GetExtension(originalFileName);

        if (string.IsNullOrWhiteSpace(originalFileName) ||
            originalFileName.Length > 255 ||
            !AllowedDocumentExtensions.Contains(extension))
        {
            ModelState.AddModelError(
                modelKey,
                "Use a PDF, Word, PNG, or JPG file.");
        }

        if (document.Length <= 0 || document.Length > MaximumDocumentSize)
        {
            ModelState.AddModelError(
                modelKey,
                "The document must be larger than 0 bytes and no more than 10 MB.");
        }
    }

    private static async Task AddDocumentAsync(
        Tutor tutor,
        IFormFile document,
        TutorDocumentType documentType,
        string relativeDirectory,
        string documentDirectory,
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        string originalFileName = Path.GetFileName(document.FileName);
        string extension = Path.GetExtension(originalFileName)
            .ToLowerInvariant();
        string storedFileName = $"{Guid.NewGuid():N}{extension}";
        string storedFilePath = Path.Combine(
            documentDirectory,
            storedFileName);

        await using var stream = new FileStream(
            storedFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await document.CopyToAsync(stream, cancellationToken);

        tutor.TutorDocuments.Add(
            new TutorDocument
            {
                DocumentType = documentType,
                FilePath = Path.Combine(
                        relativeDirectory,
                        storedFileName)
                    .Replace('\\', '/'),
                OriginalFileName = originalFileName,
                IsVerified = false,
                UploadedAt = uploadedAt
            });
    }

    private static void DeleteDocumentDirectory(string documentDirectory)
    {
        try
        {
            if (Directory.Exists(documentDirectory))
            {
                Directory.Delete(documentDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
