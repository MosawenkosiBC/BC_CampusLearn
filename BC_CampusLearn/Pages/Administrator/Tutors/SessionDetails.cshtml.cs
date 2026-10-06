using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Tutors;

public class SessionDetailsModel(
    ApplicationDbContext context,
    IWebHostEnvironment environment,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : PageModel
{
    public Booking Session { get; private set; } = null!;

    public IReadOnlyList<ReviewAnswer> StudentReviewAnswers { get; private set; } = [];

    public IReadOnlyList<ReviewAnswer> TutorReviewAnswers { get; private set; } = [];

    public SessionReview? TutorHeadReview { get; private set; }

    public IReadOnlyList<ReviewAnswer> TutorHeadReviewAnswers { get; private set; } = [];

    public sealed record ReviewAnswer(string Question, string Value);

    [BindProperty]
    public AdminSessionReviewInput AdminReviewInput { get; set; } = new();

    [TempData]
    public string? AdminReviewMessage { get; set; }

    public bool CanRecordAdminReview =>
        Session.Status == BookingStatus.Completed &&
        Session.StudentEvaluation is not null &&
        Session.TutorEvaluation is not null &&
        TutorHeadReview is not null;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken) =>
        await LoadPageAsync(id, populateInput: true, cancellationToken);

    private async Task<IActionResult> LoadPageAsync(
        int id,
        bool populateInput,
        CancellationToken cancellationToken)
    {
        var session = await context.Bookings.AsNoTracking().AsSplitQuery()
            .Include(item => item.ProgrammeModule)
            .Include(item => item.StudentEvaluation)
            .Include(item => item.TutorEvaluation)
            .Include(item => item.AdminSessionReview)
            .Include(item => item.SessionReviews).ThenInclude(item => item.Reviewer)
            .Include(item => item.PreparationLinks)
            .Include(item => item.Documents)
            .Include(item => item.MeetingLink)
            .Include(item => item.TutorCourseModule).ThenInclude(item => item.Tutor).ThenInclude(item => item.BcUser)
            .FirstOrDefaultAsync(item => item.BookingId == id &&
                (item.Status == BookingStatus.Completed || item.Status == BookingStatus.Cancelled), cancellationToken);
        if (session is null) return NotFound();
        Session = session;
        StudentReviewAnswers = BuildStudentReviewAnswers(session);
        TutorReviewAnswers = BuildTutorReviewAnswers(session);
        TutorHeadReview = session.SessionReviews.FirstOrDefault(review =>
            review.Reviewer.Role == BcUserRole.HeadOfTutors);
        TutorHeadReviewAnswers = BuildTutorHeadReviewAnswers(TutorHeadReview);
        if (populateInput && session.AdminSessionReview is { } saved)
        {
            AdminReviewInput = new AdminSessionReviewInput
            {
                HeadConfirmedQuality = saved.HeadConfirmedQuality,
                ReviewEvidenceIsConsistent = saved.ReviewEvidenceIsConsistent,
                ConcernsResolvedOrDocumented = saved.ConcernsResolvedOrDocumented,
                EvidenceSupportsApproval = saved.EvidenceSupportsApproval
            };
        }
        if (ViewData is not null)
        {
            ViewData["AdminTutorProfileId"] = session.TutorId;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAdminReviewAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (!AdminReviewInput.HeadConfirmedQuality.HasValue ||
            !AdminReviewInput.ReviewEvidenceIsConsistent.HasValue ||
            !AdminReviewInput.ConcernsResolvedOrDocumented.HasValue ||
            !AdminReviewInput.EvidenceSupportsApproval.HasValue)
        {
            ModelState.AddModelError(string.Empty,
                "Complete every review check and choose Approve or Decline before saving.");
        }

        Booking? booking = await context.Bookings
            .Include(item => item.StudentEvaluation)
            .Include(item => item.TutorEvaluation)
            .Include(item => item.AdminSessionReview)
            .Include(item => item.SessionReviews).ThenInclude(item => item.Reviewer)
            .SingleOrDefaultAsync(item => item.BookingId == id,
                cancellationToken);
        if (booking is null) return NotFound();
        if (booking.Status != BookingStatus.Completed ||
            booking.StudentEvaluation is null ||
            booking.TutorEvaluation is null ||
            !booking.SessionReviews.Any(review =>
                review.Reviewer.Role == BcUserRole.HeadOfTutors))
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return await LoadPageAsync(id, populateInput: false, cancellationToken);
        }

        AdminSessionReview review = booking.AdminSessionReview ?? new AdminSessionReview
        {
            BookingId = booking.BookingId
        };
        review.ReviewerBcUserId = currentUserService.GetRequiredUser().BcUserId;
        review.ReviewEvidenceIsConsistent =
            AdminReviewInput.ReviewEvidenceIsConsistent!.Value;
        review.HeadConfirmedQuality = AdminReviewInput.HeadConfirmedQuality!.Value;
        review.ConcernsResolvedOrDocumented =
            AdminReviewInput.ConcernsResolvedOrDocumented!.Value;
        review.EvidenceSupportsApproval =
            AdminReviewInput.EvidenceSupportsApproval!.Value;
        review.RecordedAt = timeProvider.GetUtcNow();
        if (booking.AdminSessionReview is null)
        {
            context.AdminSessionReviews.Add(review);
        }
        await context.SaveChangesAsync(cancellationToken);
        AdminReviewMessage = "Administrator review recorded.";
        return RedirectToPage(new { id });
    }

    private static IReadOnlyList<ReviewAnswer> BuildStudentReviewAnswers(Booking session)
    {
        List<ReviewAnswer> answers = [];
        if (session.StudentEvaluation is { } review)
        {
            answers.AddRange([
                new("1. Mode of tutoring", Answer(review.TutoringMode)),
                new("2. What was your experience with the platform?", Answer(review.PlatformExperience)),
                new("3. Did the tutor listen carefully and respond clearly and directly?", Answer(review.TutorResponse)),
                new("4. Did the tutor understand and show genuine interest in teaching the module?", Answer(review.TutorInterest)),
                new("5. The tutor was friendly and helped me feel comfortable.", Answer(review.TutorFriendliness)),
                new("6. The tutor explained the subject in a way I understood and could apply independently.", Answer(review.TutorExplanation)),
                new("7. The tutor encouraged my participation in the session.", Answer(review.TutorParticipation)),
                new("8. Did the tutor commit to the arranged session times?", Answer(review.TutorPunctuality)),
                new("9. Did the tutor suggest ways to improve your study habits and become a more independent learner?", Answer(review.TutorAdvice)),
                new("10. In what way did the tutor help you?", Answer(review.TutorHelp)),
                new("11. What topic did the tutor assist you with?", Answer(review.TutorTopic)),
                new("12. Do you plan on using the tutoring service again?", Answer(review.TutoringService)),
                new("13. Do you have suggestions to improve the BC Peer-Tutoring programme?", Answer(review.ImproveBCProgramme)),
                new("14. How would you rate the overall tutoring experience?", $"{review.ModeRating}/5"),
                new("15. How would you rate the tutoring platform?", $"{review.PlatformRating}/5")
            ]);
        }

        foreach (SessionReview additionalReview in session.SessionReviews
            .Where(item => item.ReviewerBcUserId == session.StudentBcUserId))
        {
            answers.Add(new("Additional session rating", $"{additionalReview.Rating}/5"));
            answers.Add(new("Additional session comment", Answer(additionalReview.Comment)));
        }
        return answers;
    }

    private static IReadOnlyList<ReviewAnswer> BuildTutorReviewAnswers(Booking session)
    {
        List<ReviewAnswer> answers = [];
        if (session.TutorEvaluation is { } review)
        {
            answers.AddRange([
                new("1. Was the session planned in advance?", YesNo(review.SessionPlan)),
                new("2. Did the student provide information beforehand?", YesNo(review.StudentPreparationInfo)),
                new("3. Was the student punctual?", YesNo(review.StudentPunctuality)),
                new("4. Was the student prepared?", YesNo(review.StudentPrepared)),
                new("5. If homework was given in the previous session, was it completed?", Answer(review.PreviousHomework)),
                new("6. Did the student interact when encouraged?", Answer(review.StudentInteract)),
                new("7. Was the student engaged and focused throughout the session?", Answer(review.StudentFocus)),
                new("8. Did any conflict or issues arise between you and the student?", Answer(review.StudentIssues)),
                new("9. Comments", Answer(review.TutorComments)),
                new("10. Recording link for the meeting", Answer(review.RecordingLink))
            ]);
        }

        foreach (SessionReview additionalReview in session.SessionReviews
            .Where(item => item.ReviewerBcUserId == session.TutorCourseModule.Tutor.BcUserId))
        {
            answers.Add(new("Additional session rating", $"{additionalReview.Rating}/5"));
            answers.Add(new("Additional session comment", Answer(additionalReview.Comment)));
        }
        return answers;
    }

    private static IReadOnlyList<ReviewAnswer> BuildTutorHeadReviewAnswers(
        SessionReview? review) => review is null
        ? []
        :
        [
            new("1. Was the correct module and topic covered?", Answer(review.ModuleAndTopicCoverage)),
            new("2. How clear were the tutor's explanations?", Answer(review.ExplanationClarity)),
            new("3. Was the session structured effectively?", Answer(review.SessionStructure)),
            new("4. Did the tutor engage the student appropriately?", Answer(review.StudentEngagement)),
            new("5. Do the recording and submitted reviews match?", Answer(review.EvidenceConsistency)),
            new("6. Were any concerns identified?", Answer(review.ConcernLevel)),
            new("7. Overall session assessment", Answer(review.OverallAssessment)),
            new("8. Decision", Answer(review.Decision)),
            new("Additional comments", Answer(review.Comment))
        ];

    private static string Answer(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not provided" : value;

    private static string YesNo(bool value) => value ? "Yes" : "No";

    public async Task<IActionResult> OnGetDocumentAsync(
        int id,
        int documentId,
        CancellationToken cancellationToken)
    {
        BookingDocument? document = await context.BookingDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.BookingDocumentId == documentId &&
                item.BookingId == id &&
                (item.Booking.Status == BookingStatus.Completed ||
                 item.Booking.Status == BookingStatus.Cancelled),
                cancellationToken);
        if (document is null) return NotFound();

        string documentRoot = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath, "App_Data", "booking-documents"));
        string fullPath = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath, document.StoragePath));
        string allowedPrefix = documentRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return new PhysicalFileResult(fullPath, document.ContentType)
        {
            FileDownloadName = document.OriginalFileName
        };
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
                (item.Booking.Status == BookingStatus.Completed ||
                 item.Booking.Status == BookingStatus.Cancelled),
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
}
