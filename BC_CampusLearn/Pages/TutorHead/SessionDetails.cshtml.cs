using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Gemini;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.TutorHead;

[Authorize(Roles = nameof(BcUserRole.HeadOfTutors))]
public class SessionDetailsModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    IWebHostEnvironment environment,
    TimeProvider timeProvider,
    IGeminiApiKeyProtector? apiKeyProtector = null,
    IGeminiSessionAssessmentService? assessmentService = null) : PageModel
{
    private static readonly JsonSerializerOptions AssessmentJsonOptions = new(
        JsonSerializerDefaults.Web);

    public Booking Session { get; private set; } = null!;

    public IReadOnlyList<ReviewAnswer> StudentReviewAnswers { get; private set; }
        = [];

    public IReadOnlyList<ReviewAnswer> TutorReviewAnswers { get; private set; }
        = [];

    public SessionReview? TutorHeadReview { get; private set; }

    [BindProperty]
    public TutorHeadSessionReviewInput TutorHeadReviewInput { get; set; } = new();

    [TempData]
    public string? TutorHeadReviewMessage { get; set; }

    public string? RecordingUrl { get; private set; }

    public bool CanWatchRecording => RecordingUrl is not null;

    public bool HasTranscript =>
        !string.IsNullOrWhiteSpace(Session?.TutorEvaluation?.TranscriptStoragePath);

    public bool HasGeminiApiKey { get; private set; }

    public GeminiSessionAssessment? AiAssessment { get; private set; }

    public DateTimeOffset? AiAssessmentGeneratedAt { get; private set; }

    public bool OpenAiAssessmentModal { get; private set; }

    public string? AiAssessmentError { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken) =>
        await LoadPageAsync(id, populateInput: true, cancellationToken);

    public async Task<IActionResult> OnPostTutorHeadReviewAsync(
        int id,
        CancellationToken cancellationToken)
    {
        Booking? booking = await context.Bookings
            .Include(item => item.StudentEvaluation)
            .Include(item => item.TutorEvaluation)
            .Include(item => item.SessionReviews)
            .Include(item => item.TutorCourseModule)
                .ThenInclude(item => item.Tutor)
            .SingleOrDefaultAsync(item => item.BookingId == id,
                cancellationToken);

        if (booking is null)
        {
            return NotFound();
        }

        if (booking.Status != BookingStatus.Completed ||
            booking.StudentEvaluation is null ||
            booking.TutorEvaluation is null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return await LoadPageAsync(
                id,
                populateInput: false,
                cancellationToken);
        }

        CurrentUser currentUser = currentUserService.GetRequiredUser();
        SessionReview review = booking.SessionReviews.FirstOrDefault(item =>
            item.ReviewerBcUserId == currentUser.BcUserId) ?? new SessionReview
            {
                BookingId = booking.BookingId,
                ReviewerBcUserId = currentUser.BcUserId,
                RevieweeBcUserId = booking.TutorCourseModule.Tutor.BcUserId
            };

        review.ModuleAndTopicCoverage =
            TutorHeadReviewInput.ModuleAndTopicCoverage;
        review.ExplanationClarity = TutorHeadReviewInput.ExplanationClarity;
        review.SessionStructure = TutorHeadReviewInput.SessionStructure;
        review.StudentEngagement = TutorHeadReviewInput.StudentEngagement;
        review.EvidenceConsistency = TutorHeadReviewInput.EvidenceConsistency;
        review.ConcernLevel = TutorHeadReviewInput.ConcernLevel;
        review.OverallAssessment = TutorHeadReviewInput.OverallAssessment;
        review.Decision = TutorHeadReviewInput.Decision;
        review.Comment = string.IsNullOrWhiteSpace(
            TutorHeadReviewInput.AdditionalComments)
            ? null
            : TutorHeadReviewInput.AdditionalComments.Trim();
        review.Rating = AssessmentRating(
            TutorHeadReviewInput.OverallAssessment!);
        review.CreatedAt = timeProvider.GetUtcNow();

        if (review.SessionReviewId == 0)
        {
            context.SessionReviews.Add(review);
        }

        await context.SaveChangesAsync(cancellationToken);
        TutorHeadReviewMessage = "Tutor Head review saved.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGenerateAiAssessmentAsync(
        int id,
        CancellationToken cancellationToken)
    {
        IActionResult loadResult = await LoadPageAsync(
            id,
            populateInput: true,
            cancellationToken);
        if (loadResult is not PageResult)
        {
            return loadResult;
        }

        CurrentUser currentUser = currentUserService.GetRequiredUser();
        string? protectedKey = await context.BcUsers
            .AsNoTracking()
            .Where(user => user.BcUserId == currentUser.BcUserId)
            .Select(user => user.EncryptedGeminiApiKey)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(protectedKey))
        {
            AiAssessmentError =
                "Add a Gemini API key from the Session Reviews helper first.";
            return Page();
        }

        if (apiKeyProtector is null || assessmentService is null)
        {
            AiAssessmentError = "The AI review helper is not available right now.";
            return Page();
        }

        string apiKey;
        try
        {
            apiKey = apiKeyProtector.Unprotect(protectedKey);
        }
        catch (CryptographicException)
        {
            AiAssessmentError =
                "The saved Gemini API key could not be read. Please replace it from Session Reviews.";
            return Page();
        }

        try
        {
            AiAssessment = await assessmentService.AssessAsync(
                apiKey,
                await BuildEvidenceAsync(cancellationToken),
                cancellationToken);
            SessionAiAssessment storedAssessment = await context
                .SessionAiAssessments
                .SingleOrDefaultAsync(item => item.BookingId == id,
                    cancellationToken) ?? new SessionAiAssessment
                    {
                        BookingId = id
                    };
            storedAssessment.GeneratedByBcUserId = currentUser.BcUserId;
            storedAssessment.GeneratedAt = timeProvider.GetUtcNow();
            storedAssessment.AssessmentJson = JsonSerializer.Serialize(
                AiAssessment,
                AssessmentJsonOptions);
            if (storedAssessment.SessionAiAssessmentId == 0)
            {
                context.SessionAiAssessments.Add(storedAssessment);
            }
            await context.SaveChangesAsync(cancellationToken);
            AiAssessmentGeneratedAt = storedAssessment.GeneratedAt;
            OpenAiAssessmentModal = true;
            TutorHeadReviewMessage = "AI assessment generated and saved.";
        }
        catch (GeminiAssessmentException exception)
        {
            AiAssessmentError = exception.Message;
        }

        return Page();
    }

    private async Task<IActionResult> LoadPageAsync(
        int id,
        bool populateInput,
        CancellationToken cancellationToken)
    {
        Booking? session = await context.Bookings
            .AsNoTracking()
            .AsSplitQuery()
            .Include(booking => booking.ProgrammeModule)
            .Include(booking => booking.StudentEvaluation)
            .Include(booking => booking.TutorEvaluation)
            .Include(booking => booking.SessionReviews)
                .ThenInclude(review => review.Reviewer)
            .Include(booking => booking.TutorCourseModule)
                .ThenInclude(assignment => assignment.Tutor)
                .ThenInclude(tutor => tutor.BcUser)
            .SingleOrDefaultAsync(booking =>
                booking.BookingId == id &&
                booking.Status == BookingStatus.Completed &&
                booking.StudentEvaluation != null &&
                booking.TutorEvaluation != null,
                cancellationToken);

        if (session is null)
        {
            return NotFound();
        }

        Session = session;
        StudentReviewAnswers = BuildStudentReviewAnswers(
            session.StudentEvaluation!);
        TutorReviewAnswers = BuildTutorReviewAnswers(
            session.TutorEvaluation!);
        TutorHeadReview = session.SessionReviews.FirstOrDefault(review =>
            review.Reviewer.Role == BcUserRole.HeadOfTutors);
        if (populateInput && TutorHeadReview is { } savedReview)
        {
            TutorHeadReviewInput = new TutorHeadSessionReviewInput
            {
                ModuleAndTopicCoverage = savedReview.ModuleAndTopicCoverage,
                ExplanationClarity = savedReview.ExplanationClarity,
                SessionStructure = savedReview.SessionStructure,
                StudentEngagement = savedReview.StudentEngagement,
                EvidenceConsistency = savedReview.EvidenceConsistency,
                ConcernLevel = savedReview.ConcernLevel,
                OverallAssessment = savedReview.OverallAssessment,
                Decision = savedReview.Decision,
                AdditionalComments = savedReview.Comment
            };
        }
        RecordingUrl = ValidHttpUrl(session.TutorEvaluation!.RecordingLink);
        int currentUserId = currentUserService.GetRequiredUser().BcUserId;
        HasGeminiApiKey = await context.BcUsers
            .AsNoTracking()
            .Where(user => user.BcUserId == currentUserId)
            .AnyAsync(user => user.EncryptedGeminiApiKey != null,
                cancellationToken);
        SessionAiAssessment? storedAssessment = await context
            .SessionAiAssessments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.BookingId == id,
                cancellationToken);
        if (storedAssessment is not null)
        {
            try
            {
                AiAssessment = JsonSerializer.Deserialize<
                    GeminiSessionAssessment>(
                    storedAssessment.AssessmentJson,
                    AssessmentJsonOptions);
                AiAssessmentGeneratedAt = storedAssessment.GeneratedAt;
            }
            catch (JsonException)
            {
                AiAssessmentError =
                    "The saved AI assessment could not be read. Generate it again.";
            }
        }
        return Page();
    }

    private async Task<GeminiSessionEvidence> BuildEvidenceAsync(
        CancellationToken cancellationToken)
    {
        GeminiTranscriptDocument? uploadedTranscript =
            await ReadUploadedTranscriptAsync(cancellationToken);

        return new GeminiSessionEvidence(
            Answer(Session.Summary),
            $"{Session.ProgrammeModule.ModuleCode} - {Session.ProgrammeModule.ModuleName}",
            StudentReviewAnswers.ToDictionary(
                answer => answer.Question,
                answer => answer.Value),
            TutorReviewAnswers.ToDictionary(
                answer => answer.Question,
                answer => answer.Value),
            uploadedTranscript);
    }

    private async Task<GeminiTranscriptDocument?> ReadUploadedTranscriptAsync(
        CancellationToken cancellationToken)
    {
        TutorStudentEvaluation evaluation = Session.TutorEvaluation!;
        if (string.IsNullOrWhiteSpace(evaluation.TranscriptStoragePath) ||
            string.IsNullOrWhiteSpace(evaluation.TranscriptOriginalFileName) ||
            string.IsNullOrWhiteSpace(evaluation.TranscriptContentType))
        {
            return null;
        }

        if (!TryResolveTranscriptPath(evaluation, out string fullPath))
        {
            throw new GeminiAssessmentException(
                "The uploaded transcript file could not be found. Ask the tutor to upload it again.");
        }

        string extension = Path.GetExtension(
            evaluation.TranscriptOriginalFileName);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            byte[] bytes = await System.IO.File.ReadAllBytesAsync(
                fullPath,
                cancellationToken);
            return new GeminiTranscriptDocument(
                evaluation.TranscriptOriginalFileName,
                "application/pdf",
                null,
                Convert.ToBase64String(bytes));
        }

        if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            string text;
            try
            {
                text = ExtractDocxText(fullPath);
            }
            catch (Exception exception) when (exception is IOException or
                XmlException)
            {
                throw new GeminiAssessmentException(
                    "The uploaded Word transcript could not be read. Ask the tutor to upload it again.",
                    exception);
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new GeminiAssessmentException(
                    "No readable text was found in the uploaded Word transcript.");
            }

            return new GeminiTranscriptDocument(
                evaluation.TranscriptOriginalFileName,
                "text/plain",
                text,
                null);
        }

        throw new GeminiAssessmentException(
            "Legacy .doc transcripts cannot be read by the AI helper. Upload the transcript as a PDF or .docx file.");
    }

    private static string ExtractDocxText(string fullPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(fullPath);
        ZipArchiveEntry? documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
        {
            return string.Empty;
        }

        using Stream stream = documentEntry.Open();
        XDocument document = XDocument.Load(stream);
        XNamespace word =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join(
            Environment.NewLine,
            document.Descendants(word + "p")
                .Select(paragraph => string.Concat(
                    paragraph.Descendants(word + "t")
                        .Select(text => text.Value)))
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    public async Task<IActionResult> OnGetTranscriptAsync(
        int id,
        CancellationToken cancellationToken)
    {
        TutorStudentEvaluation? evaluation = await context
            .TutorStudentEvaluations
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.BookingId == id &&
                item.Booking.Status == BookingStatus.Completed &&
                item.Booking.StudentEvaluation != null,
                cancellationToken);
        if (evaluation is null ||
            string.IsNullOrWhiteSpace(evaluation.TranscriptStoragePath) ||
            string.IsNullOrWhiteSpace(evaluation.TranscriptOriginalFileName) ||
            string.IsNullOrWhiteSpace(evaluation.TranscriptContentType))
        {
            return NotFound();
        }

        if (!TryResolveTranscriptPath(evaluation, out string fullPath))
        {
            return NotFound();
        }

        string extension = Path.GetExtension(
            evaluation.TranscriptOriginalFileName);
        if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            string transcriptText;
            try
            {
                transcriptText = ExtractDocxText(fullPath);
            }
            catch (Exception exception) when (exception is IOException or
                XmlException)
            {
                return Content(
                    BuildTranscriptPreview(
                        evaluation.TranscriptOriginalFileName,
                        "This Word transcript could not be previewed."),
                    "text/html; charset=utf-8");
            }

            return Content(
                BuildTranscriptPreview(
                    evaluation.TranscriptOriginalFileName,
                    transcriptText),
                "text/html; charset=utf-8");
        }

        if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase))
        {
            return Content(
                BuildTranscriptPreview(
                    evaluation.TranscriptOriginalFileName,
                    "This legacy Word transcript cannot be previewed in the browser. Ask the tutor to upload it as a PDF or .docx file."),
                "text/html; charset=utf-8");
        }

        return new PhysicalFileResult(fullPath, "application/pdf");
    }

    private static string BuildTranscriptPreview(
        string fileName,
        string transcriptText)
    {
        string encodedTitle = System.Net.WebUtility.HtmlEncode(fileName);
        string paragraphs = string.Join(
            Environment.NewLine,
            transcriptText.Split(
                    ["\r\n", "\n", "\r"],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                    $"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>"));
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{encodedTitle}}</title>
                <style>
                    body { color: #25282d; font-family: Arial, sans-serif; line-height: 1.6; margin: 0 auto; max-width: 900px; padding: 2rem; }
                    h1 { border-bottom: 1px solid #e1e5e6; font-size: 1.25rem; margin: 0 0 1.5rem; padding-bottom: 1rem; }
                    p { margin: 0 0 1rem; white-space: pre-wrap; }
                </style>
            </head>
            <body>
                <h1>{{encodedTitle}}</h1>
                <main>{{paragraphs}}</main>
            </body>
            </html>
            """;
    }

    private bool TryResolveTranscriptPath(
        TutorStudentEvaluation evaluation,
        out string fullPath)
    {
        string transcriptRoot = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "tutor-review-transcripts"));
        fullPath = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            evaluation.TranscriptStoragePath!));
        string allowedPrefix = transcriptRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(
                allowedPrefix,
                StringComparison.OrdinalIgnoreCase) &&
            System.IO.File.Exists(fullPath);
    }

    private static byte AssessmentRating(string assessment) => assessment switch
    {
        "Excellent" => 5,
        "Good" => 4,
        "Satisfactory" => 3,
        _ => 2
    };

    private static IReadOnlyList<ReviewAnswer> BuildStudentReviewAnswers(
        StudentEvaluation review) =>
    [
        new("Mode of tutoring", Answer(review.TutoringMode)),
        new("Platform experience", Answer(review.PlatformExperience)),
        new("Tutor listened and responded clearly", Answer(review.TutorResponse)),
        new("Tutor showed interest in teaching the module", Answer(review.TutorInterest)),
        new("Tutor was friendly and created a comfortable environment", Answer(review.TutorFriendliness)),
        new("Tutor explained the subject clearly", Answer(review.TutorExplanation)),
        new("Tutor encouraged participation", Answer(review.TutorParticipation)),
        new("Tutor kept to the arranged session time", Answer(review.TutorPunctuality)),
        new("Tutor suggested ways to improve study habits", Answer(review.TutorAdvice)),
        new("How the tutor helped", Answer(review.TutorHelp)),
        new("Topic covered", Answer(review.TutorTopic)),
        new("Would use the tutoring service again", Answer(review.TutoringService)),
        new("Suggestions for the peer-tutoring programme", Answer(review.ImproveBCProgramme)),
        new("Overall tutoring experience", $"{review.ModeRating}/5"),
        new("Tutoring platform rating", $"{review.PlatformRating}/5")
    ];

    private static IReadOnlyList<ReviewAnswer> BuildTutorReviewAnswers(
        TutorStudentEvaluation review) =>
    [
        new("Session planned in advance", YesNo(review.SessionPlan)),
        new("Student provided information beforehand", YesNo(review.StudentPreparationInfo)),
        new("Student was punctual", YesNo(review.StudentPunctuality)),
        new("Student was prepared", YesNo(review.StudentPrepared)),
        new("Previous homework", Answer(review.PreviousHomework)),
        new("Student interaction", Answer(review.StudentInteract)),
        new("Student focus and engagement", Answer(review.StudentFocus)),
        new("Conflicts or issues", Answer(review.StudentIssues)),
        new("Tutor comments", Answer(review.TutorComments))
    ];

    private static string Answer(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not provided" : value;

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static string? ValidHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    public sealed record ReviewAnswer(string Question, string Value);
}
