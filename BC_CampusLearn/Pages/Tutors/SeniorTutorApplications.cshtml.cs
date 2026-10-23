using System.Globalization;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

public class SeniorTutorApplicationsModel : PageModel
{
    private static readonly HashSet<string> AllowedDescriptions =
        new(StringComparer.Ordinal)
        {
            "Self-driven",
            "Self-disciplined",
            "Motivated",
            "Professional",
            "Good time management skills",
            "Good interpersonal skills",
            "Good leadership skills"
        };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SeniorTutorApplicationsModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public string DisplayName { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;
    public string StudentNumber { get; private set; } = string.Empty;
    public string? ProfileImagePath { get; private set; }

    [BindProperty]
    public SeniorTutorApplicationInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? ApplicationId { get; set; }

    public bool OpenApplicationModal { get; private set; }
    public bool ShowClosedApplicationsModal { get; private set; }
    public bool ApplicationsOpen { get; private set; }
    public bool IsSeniorTutor { get; private set; }
    public bool IsHeadOfTutors { get; private set; }

    public SeniorTutorApplication? LatestApplication { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadPageAsync(cancellationToken))
        {
            return Forbid();
        }

        OpenApplicationModal = ApplicationId.HasValue &&
            LatestApplication is
            {
                Status: TutorAccountRequestStatus.Pending,
                IsHeadOfTutorApplication: false
            } &&
            LatestApplication.SeniorTutorApplicationId == ApplicationId.Value &&
            LatestApplication.AcademicAverage >= 72m;
        return Page();
    }

    public async Task<IActionResult> OnPostApplySeniorAsync(
        string? academicAverage,
        CancellationToken cancellationToken,
        List<int>? programmeModuleIds = null,
        bool? applyForHeadOfTutor = null)
        => await _context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ApplySeniorCoreAsync(
                academicAverage,
                programmeModuleIds,
                applyForHeadOfTutor,
                cancellationToken));

    private async Task<IActionResult> ApplySeniorCoreAsync(
        string? academicAverage,
        List<int>? programmeModuleIds,
        bool? applyForHeadOfTutor,
        CancellationToken cancellationToken)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken)
            : null;

        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        var tutor = await _context.Tutors
            .Where(item =>
                item.BcUserId == currentUser.BcUserId &&
                item.Status == TutorStatus.Approved &&
                item.IsActive)
            .Select(item => new
            {
                item.TutorId,
                item.BcUser.Role,
                ActiveModuleIds = item.TutorCourseModules
                    .Where(module => module.IsActive)
                    .Select(module => module.ProgrammeModuleId)
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return Forbid();
        }
        if (tutor.Role is not (BcUserRole.Tutor or BcUserRole.SeniorTutor or
            BcUserRole.HeadOfTutors))
        {
            return Forbid();
        }

        TutorApplicationSettings? applicationSettings = await _context
            .TutorApplicationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        bool applicationsOpen = applicationSettings?
            .IsAcceptingSeniorTutorApplications(DateTime.UtcNow) ?? false;
        if (!applicationsOpen)
        {
            TempData["SeniorTutorApplicationError"] =
                "Senior Tutor applications are currently closed.";
            return RedirectToPage("/Tutors/TutorDashboard");
        }

        List<int> requestedModuleIds = (programmeModuleIds ?? [])
            .Where(moduleId => moduleId > 0)
            .Distinct()
            .ToList();
        TempData["SeniorTutorApplicationModuleIds"] =
            string.Join(',', requestedModuleIds);
        if (applyForHeadOfTutor.HasValue)
        {
            TempData["SeniorTutorApplicationHeadChoice"] =
                applyForHeadOfTutor.Value.ToString();
        }

        string averageText = academicAverage?.Trim() ?? string.Empty;
        if (!decimal.TryParse(
                averageText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal average) || average < 0m || average > 100m)
        {
            TempData["SeniorTutorApplicationError"] =
                "Enter an academic average from 0 to 100.";
            TempData["SeniorTutorApplicationAverage"] = averageText;
            return RedirectToPage(
                "/Tutors/TutorDashboard",
                new { openSeniorApplication = true });
        }

        if (requestedModuleIds.Intersect(tutor.ActiveModuleIds).Any() ||
            await _context.ProgrammeModules.CountAsync(
                module => requestedModuleIds.Contains(module.ProgrammeModuleId),
                cancellationToken) != requestedModuleIds.Count)
        {
            TempData["SeniorTutorApplicationError"] =
                "Select valid additional modules that are not already assigned to you.";
            TempData["SeniorTutorApplicationAverage"] = averageText;
            return RedirectToPage(
                "/Tutors/TutorDashboard",
                new { openSeniorApplication = true });
        }

        SeniorTutorApplication? pendingApplication = await _context
            .SeniorTutorApplications
            .OrderByDescending(application => application.SubmittedAt)
            .FirstOrDefaultAsync(application =>
                application.TutorId == tutor.TutorId &&
                application.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);
        if (pendingApplication is not null)
        {
            TempData["SeniorTutorApplicationError"] =
                "You already have a Senior Tutor application awaiting review.";
            return RedirectToPage("/Tutors/TutorDashboard");
        }

        if (average >= 72m &&
            tutor.Role != BcUserRole.HeadOfTutors &&
            !applyForHeadOfTutor.HasValue)
        {
            TempData["SeniorTutorApplicationError"] =
                "Choose whether you want to apply for Head of Tutor.";
            TempData["SeniorTutorApplicationAverage"] = averageText;
            return RedirectToPage(
                "/Tutors/TutorDashboard",
                new { openSeniorApplication = true });
        }

        var application = new SeniorTutorApplication
        {
            TutorId = tutor.TutorId,
            AcademicAverage = average,
            IsHeadOfTutorApplication = false,
            Status = TutorAccountRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
        foreach (int moduleId in requestedModuleIds)
        {
            application.RequestedModules.Add(new SeniorTutorApplicationModule
            {
                ProgrammeModuleId = moduleId
            });
        }
        _context.SeniorTutorApplications.Add(application);

        await _context.SaveChangesAsync(cancellationToken);
        TempData.Remove("SeniorTutorApplicationModuleIds");
        TempData.Remove("SeniorTutorApplicationHeadChoice");
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        if (average >= 72m &&
            tutor.Role != BcUserRole.HeadOfTutors &&
            applyForHeadOfTutor == true)
        {
            TempData["SeniorTutorApplicationSaved"] = true;
            return RedirectToPage(new
            {
                applicationId = application.SeniorTutorApplicationId
            });
        }

        TempData["SuccessMessage"] =
            "Your Senior Tutor application has been submitted for review.";
        return RedirectToPage("/Tutors/TutorDashboard");
    }

    public async Task<IActionResult> OnPostApplyForHeadAsync(
        int applicationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Input.BestDescription) ||
            !AllowedDescriptions.Contains(Input.BestDescription))
        {
            ModelState.AddModelError(
                "Input.BestDescription",
                "Choose the option that describes you best.");
        }

        string reason = Input.SuitabilityReason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            ModelState.AddModelError(
                "Input.SuitabilityReason",
                "Explain why you are suitable for the Head of Tutor role.");
        }
        else if (reason.Length > 2000)
        {
            ModelState.AddModelError(
                "Input.SuitabilityReason",
                "Keep your answer to 2,000 characters or fewer.");
        }

        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        SeniorTutorApplication? application = await _context
            .SeniorTutorApplications
            .Include(item => item.Tutor)
            .SingleOrDefaultAsync(item =>
                item.SeniorTutorApplicationId == applicationId &&
                item.Tutor.BcUserId == currentUser.BcUserId &&
                item.Tutor.BcUser.Role != BcUserRole.HeadOfTutors &&
                item.Status == TutorAccountRequestStatus.Pending &&
                item.AcademicAverage >= 72m,
                cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ApplicationId = applicationId;
            OpenApplicationModal = true;
            return await LoadPageAsync(cancellationToken) ? Page() : Forbid();
        }

        application.IsHeadOfTutorApplication = true;
        application.BestDescription = Input.BestDescription;
        application.SuitabilityReason = Input.SuitabilityReason!.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        TempData["HeadOfTutorApplicationSaved"] = true;
        return RedirectToPage();
    }

    private async Task<bool> LoadPageAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        var tutor = await _context.Tutors
            .AsNoTracking()
            .Where(item =>
                item.BcUserId == currentUser.BcUserId &&
                item.Status == TutorStatus.Approved &&
                item.IsActive)
            .Select(item => new
            {
                item.TutorId,
                item.BcUser.DisplayName,
                item.BcUser.PersonnelNumber,
                item.BcUser.Role,
                item.ProfileImagePath
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return false;
        }

        DisplayName = !string.IsNullOrWhiteSpace(tutor.DisplayName)
            ? tutor.DisplayName
            : !string.IsNullOrWhiteSpace(currentUser.DisplayName)
                ? currentUser.DisplayName
                : "Tutor";
        StudentNumber = tutor.PersonnelNumber ?? currentUser.PersonnelNumber ?? string.Empty;
        ProfileImagePath = tutor.ProfileImagePath;
        IsSeniorTutor = tutor.Role == BcUserRole.SeniorTutor;
        IsHeadOfTutors = tutor.Role == BcUserRole.HeadOfTutors;
        TutorApplicationSettings? applicationSettings = await _context
            .TutorApplicationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        ApplicationsOpen = applicationSettings?
            .IsAcceptingSeniorTutorApplications(DateTime.UtcNow) ?? false;
        string[] nameParts = DisplayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        Initials = nameParts.Length switch
        {
            > 1 => $"{nameParts[0][0]}{nameParts[^1][0]}".ToUpperInvariant(),
            1 => nameParts[0][..1].ToUpperInvariant(),
            _ => "T"
        };

        LatestApplication = await _context.SeniorTutorApplications
            .AsNoTracking()
            .Where(application =>
                application.TutorId == tutor.TutorId &&
                (application.Status != TutorAccountRequestStatus.Approved ||
                    tutor.Role == BcUserRole.SeniorTutor ||
                    tutor.Role == BcUserRole.HeadOfTutors))
            .OrderByDescending(application => application.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return true;
    }
}
