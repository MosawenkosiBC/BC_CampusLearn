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
public class SessionDetailsModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    IWebHostEnvironment environment,
    TimeProvider timeProvider) : PageModel
{
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
        return Page();
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

        string transcriptRoot = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            "App_Data",
            "tutor-review-transcripts"));
        string fullPath = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            evaluation.TranscriptStoragePath));
        string allowedPrefix = transcriptRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return new PhysicalFileResult(fullPath, evaluation.TranscriptContentType)
        {
            FileDownloadName = evaluation.TranscriptOriginalFileName
        };
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
