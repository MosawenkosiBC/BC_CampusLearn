using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class ApplicationDetailsModel(
    ApplicationDbContext context,
    IWebHostEnvironment environment,
    ICurrentUserService? currentUserService = null,
    ITutorApplicationEmailSender? emailSender = null) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Stage { get; set; } = "applications";

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public Tutor Application { get; private set; } = null!;

    [BindProperty]
    [StringLength(1000)]
    public string? ReviewReason { get; set; }

    [BindProperty]
    public ApplicationMessageInput RejectionMessage { get; set; } = new();

    public string? ReviewError { get; private set; }

    [TempData]
    public string? PageMessage { get; set; }

    [TempData]
    public bool ShowReviewResultModal { get; set; }

    [TempData]
    public string? PageError { get; set; }

    [TempData]
    public bool ShowShortlistThresholdModal { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Application.BcUser.DisplayName)
            ? Application.BcUser.PersonnelNumber
            : Application.BcUser.DisplayName;

    public string PreferredTutoringModeLabel =>
        Application.PreferredTutoringMode switch
        {
            PreferredTutoringMode.FaceToFace => "Face-To-Face",
            PreferredTutoringMode.Online => "Online",
            PreferredTutoringMode.Both => "Face-To-Face and online",
            _ => Application.PreferredTutoringMode.ToString()
        };

    public bool HasDemonstrationVideoLink =>
        Uri.TryCreate(
            Application.DemonstrationVideoUrl,
            UriKind.Absolute,
            out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttps ||
            uri.Scheme == Uri.UriSchemeHttp);

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await LoadApplicationAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostShortlistAsync(
        int id,
        CancellationToken cancellationToken)
    {
        ShortlistResult result = await TutorApplicationReview.ShortlistAsync(
            context,
            id,
            ReviewReason,
            cancellationToken,
            currentUserService?.GetRequiredUser().BcUserId);

        if (result.Succeeded)
        {
            PageMessage = result.Message;
            ShowShortlistThresholdModal = result.ShortlistLimitReached;
            return RedirectToPage(
                "/Administrator/Admin/Applications",
                new { Stage = "shortlist", Search });
        }

        if (result.RequiresContinuation)
        {
            PageError = result.Message;
            ShowShortlistThresholdModal = true;
            return RedirectToPage(
                "/Administrator/Admin/Applications",
                new { Stage = "shortlist", Search });
        }

        ReviewError = result.Message;
        if (!await LoadApplicationAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(
        int id,
        CancellationToken cancellationToken)
    {
        string subject = RejectionMessage.Subject?.Trim() ?? string.Empty;
        string emailBody = RejectionMessage.EmailBody?.Trim() ?? string.Empty;
        string notificationMessage =
            RejectionMessage.NotificationMessage?.Trim() ?? string.Empty;
        if (subject.Length is < 1 or > 200 ||
            emailBody.Length is < 1 or > 5000 ||
            notificationMessage.Length is < 1 or > 1000)
        {
            ReviewError = "Compose an email and notification message before rejecting the application.";
            if (!await LoadApplicationAsync(id, cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }

        var recipient = await context.Tutors
            .AsNoTracking()
            .Where(tutor => tutor.TutorId == id)
            .Select(tutor => new
            {
                tutor.BcUser.Email
            })
            .SingleOrDefaultAsync(cancellationToken);

        ShortlistResult result = await TutorApplicationReview.RejectAsync(
            context,
            id,
            ReviewReason,
            cancellationToken,
            notificationMessage);

        if (result.Succeeded)
        {
            if (emailSender is not null &&
                !string.IsNullOrWhiteSpace(recipient?.Email))
            {
                await emailSender.SendComposedAsync(
                    recipient.Email,
                    subject,
                    emailBody,
                    cancellationToken);
            }

            PageMessage = result.Message;
            ShowReviewResultModal = true;
            return RedirectToPage(
                "/Administrator/Admin/Applications",
                new { Stage = "applications" });
        }

        ReviewError = result.Message;
        if (!await LoadApplicationAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    private async Task<bool> LoadApplicationAsync(
        int id,
        CancellationToken cancellationToken)
    {
        Tutor? application = await context.Tutors
            .AsNoTracking()
            .Include(tutor => tutor.BcUser)
            .Include(tutor => tutor.Programme)
            .Include(tutor => tutor.TutorCourseModules)
                .ThenInclude(item => item.ProgrammeModule)
            .Include(tutor => tutor.TutorDocuments)
            .Include(tutor => tutor.ApplicationReviewDecisions)
                .ThenInclude(decision => decision.Reviewer)
            .SingleOrDefaultAsync(
                tutor => tutor.TutorId == id,
                cancellationToken);

        if (application is null)
        {
            return false;
        }

        Application = application;
        return true;
    }

    public async Task<IActionResult> OnGetDocumentAsync(
        int id,
        int documentId,
        CancellationToken cancellationToken)
    {
        TutorDocument? document = await context.TutorDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TutorId == id &&
                    item.TutorDocumentId == documentId,
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        string documentRoot = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "tutor-documents"));
        string fullPath = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            document.FilePath.Replace(
                '/',
                Path.DirectorySeparatorChar)));
        string documentRootPrefix = documentRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                documentRootPrefix,
                StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return new PhysicalFileResult(
            fullPath,
            GetContentType(document.OriginalFileName))
        {
            FileDownloadName = document.OriginalFileName
        };
    }

    public static string GetDocumentTypeLabel(TutorDocumentType type) =>
        type switch
        {
            TutorDocumentType.AcademicTranscript => "Academic transcript",
            TutorDocumentType.ExternalCertificate => "Additional certificate",
            _ => type.ToString()
        };

    private static string GetContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" =>
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
}
