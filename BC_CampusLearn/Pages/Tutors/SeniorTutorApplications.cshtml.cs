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

    public bool OpenApplicationModal { get; private set; }

    public SeniorTutorApplication? LatestApplication { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await LoadPageAsync(cancellationToken) ? Page() : Forbid();
    }

    public async Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken)
        => await _context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ApplyCoreAsync(cancellationToken));

    private async Task<IActionResult> ApplyCoreAsync(CancellationToken cancellationToken)
    {
        OpenApplicationModal = true;

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
            .Select(item => new { item.TutorId })
            .SingleOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return Forbid();
        }

        ValidateInput();

        bool hasPendingApplication = await _context.SeniorTutorApplications
            .AnyAsync(application =>
                application.TutorId == tutor.TutorId &&
                application.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);
        if (hasPendingApplication)
        {
            ModelState.AddModelError(
                string.Empty,
                "You already have a senior tutor application awaiting review.");
        }

        if (!ModelState.IsValid)
        {
            return await LoadPageAsync(cancellationToken) ? Page() : Forbid();
        }

        decimal academicAverage = decimal.Parse(
            Input.AcademicAverage!.Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture);

        _context.SeniorTutorApplications.Add(new SeniorTutorApplication
        {
            TutorId = tutor.TutorId,
            AcademicAverage = academicAverage,
            BestDescription = Input.BestDescription!,
            SuitabilityReason = Input.SuitabilityReason!.Trim(),
            Status = TutorAccountRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        TempData["SeniorTutorApplicationSaved"] = true;
        return RedirectToPage();
    }

    private void ValidateInput()
    {
        string averageText = Input.AcademicAverage?.Trim() ?? string.Empty;
        if (averageText.Length == 0)
        {
            ModelState.AddModelError(
                "Input.AcademicAverage",
                "Enter your academic average.");
        }
        else if (!decimal.TryParse(
            averageText,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal average) || average < 72m || average > 100m)
        {
            ModelState.AddModelError(
                "Input.AcademicAverage",
                "Enter an academic average from 72 to 100.");
        }

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
                "Explain why you are suitable for the senior tutor role.");
        }
        else if (reason.Length > 2000)
        {
            ModelState.AddModelError(
                "Input.SuitabilityReason",
                "Keep your answer to 2,000 characters or fewer.");
        }
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
            .Where(application => application.TutorId == tutor.TutorId)
            .OrderByDescending(application => application.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return true;
    }
}
