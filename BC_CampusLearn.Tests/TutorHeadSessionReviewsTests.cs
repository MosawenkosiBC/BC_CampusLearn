using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.TutorHead;
using BC_CampusLearn.Services.Gemini;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorHeadSessionReviewsTests
{
    [Theory]
    [InlineData("No Concerns", "Approve", true)]
    [InlineData("Concerns", "Reject", true)]
    [InlineData("Concerns", "Escalate", true)]
    [InlineData("Minor concerns", "Approve", false)]
    [InlineData("Serious concerns", "Approve", false)]
    [InlineData("No Concerns", "Approve with feedback", false)]
    [InlineData("No Concerns", "Request clarification", false)]
    public void ReviewValidatesRemainingQuestionsAndChoices(
        string concerns, string decision, bool expectedValid)
    {
        var input = new TutorHeadSessionReviewInput
        {
            StudentEngagement = "Yes",
            ConcernLevel = concerns,
            OverallAssessment = "Good",
            Decision = decision
        };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        bool valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            input,
            new System.ComponentModel.DataAnnotations.ValidationContext(input),
            results,
            validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
    }

    [Fact]
    public async Task TutorHeadCanSaveEncryptedGeminiApiKey()
    {
        await using ApplicationDbContext context = CreateContext();
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        });
        await context.SaveChangesAsync();

        var protector = new TestGeminiApiKeyProtector();
        var page = new SessionReviewsModel(
            context,
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            new TestCurrentUserService(new CurrentUser(
                2,
                "TH001",
                "Tutor Head",
                "tutorhead@example.com",
                BcUserRole.HeadOfTutors)),
            protector)
        {
            GeminiApiKey = "gemini-secret-key"
        };

        IActionResult result = await page.OnPostSaveGeminiKeyAsync(
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        BcUser user = await context.BcUsers.SingleAsync(item =>
            item.BcUserId == 2);
        Assert.Equal("protected:gemini-secret-key", user.EncryptedGeminiApiKey);
        Assert.DoesNotContain("gemini-secret-key", page.GeminiKeyMessage ?? "");
    }

    [Fact]
    public async Task OpeningSessionReviewsRecordsTutorHeadLastViewedTime()
    {
        await using ApplicationDbContext context = CreateContext();
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        });
        await context.SaveChangesAsync();
        DateTimeOffset now = new(
            2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var page = new SessionReviewsModel(
            context,
            new FixedTimeProvider(now),
            new TestCurrentUserService(new CurrentUser(
                2,
                "TH001",
                "Tutor Head",
                "tutorhead@example.com",
                BcUserRole.HeadOfTutors)));

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            now,
            context.BcUsers.Single().SessionReviewsLastViewedAt);
    }

    [Fact]
    public async Task PageOnlyShowsCompletedSessionsWithBothReviews()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        context.Bookings.AddRange(
            CreateBooking(
                1,
                assignment,
                "Ready Student",
                BookingStatus.Completed,
                new StudentEvaluation(),
                new TutorStudentEvaluation()),
            CreateBooking(
                2,
                assignment,
                "Missing Tutor Review",
                BookingStatus.Completed,
                new StudentEvaluation(),
                null),
            CreateBooking(
                3,
                assignment,
                "Not Completed",
                BookingStatus.Confirmed,
                new StudentEvaluation(),
                new TutorStudentEvaluation()));
        await context.SaveChangesAsync();

        var page = CreatePage(context);
        await page.OnGetAsync(CancellationToken.None);

        SessionReviewsModel.SessionReviewListItem session =
            Assert.Single(page.Sessions);
        Assert.Equal("Ready Student", session.StudentName);
        Assert.Equal("Taylor Tutor", session.TutorDisplayName);
        Assert.Equal("PROG101", session.ModuleCode);
        Assert.False(session.HasTutorHeadReview);
        Assert.Equal("Awaiting review", session.ReviewStatus);
        Assert.Equal(1, page.PeriodSummary.TotalSessions);
        Assert.Equal(1, page.PeriodSummary.AwaitingReview);
        Assert.Equal(0, page.PeriodSummary.Reviewed);

        SessionReviewsModel filteredPage = CreatePage(context);
        filteredPage.TutorFilter = "Taylor";
        filteredPage.StudentFilter = "Ready";
        filteredPage.DateFrom = new DateOnly(2026, 9, 21);
        filteredPage.DateTo = new DateOnly(2026, 9, 21);
        await filteredPage.OnGetAsync(CancellationToken.None);

        Assert.Single(filteredPage.Sessions);
        Assert.True(filteredPage.HasActiveFilters);

        SessionReviewsModel awaitingPage = CreatePage(context);
        awaitingPage.ReviewStatusFilter = "awaiting";
        await awaitingPage.OnGetAsync(CancellationToken.None);

        Assert.Single(awaitingPage.Sessions);
        Assert.All(awaitingPage.Sessions, session =>
            Assert.False(session.HasTutorHeadReview));
    }

    [Fact]
    public async Task PageMarksSessionReviewedWhenTutorHeadReviewExists()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking booking = CreateBooking(
            1,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation(),
            new TutorStudentEvaluation());
        var tutorHead = new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        };
        booking.SessionReviews.Add(new SessionReview
        {
            Booking = booking,
            Reviewer = tutorHead,
            ReviewerBcUserId = tutorHead.BcUserId,
            Rating = 5,
            Comment = "Review complete",
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.TutorCourseModules.Add(assignment);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var page = CreatePage(context);
        await page.OnGetAsync(CancellationToken.None);

        SessionReviewsModel.SessionReviewListItem session =
            Assert.Single(page.Sessions);
        Assert.True(session.HasTutorHeadReview);
        Assert.Equal("Reviewed", session.ReviewStatus);
        Assert.Equal(1, page.PeriodSummary.TotalSessions);
        Assert.Equal(0, page.PeriodSummary.AwaitingReview);
        Assert.Equal(1, page.PeriodSummary.Reviewed);

        SessionReviewsModel reviewedPage = CreatePage(context);
        reviewedPage.ReviewStatusFilter = "reviewed";
        await reviewedPage.OnGetAsync(CancellationToken.None);

        Assert.Single(reviewedPage.Sessions);
        Assert.All(reviewedPage.Sessions, session =>
            Assert.True(session.HasTutorHeadReview));
    }

    [Fact]
    public async Task SessionDetailsShowsBothReviewsAndRecording()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking booking = CreateBooking(
            1,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation
            {
                TutoringMode = "Online",
                TutorHelp = "Worked through programming examples",
                ModeRating = 5,
                PlatformRating = 4
            },
            new TutorStudentEvaluation
            {
                SessionPlan = true,
                StudentPreparationInfo = true,
                StudentPunctuality = true,
                StudentPrepared = true,
                StudentInteract = "The student participated well.",
                StudentFocus = "The student remained focused.",
                StudentIssues = "No issues.",
                TutorComments = "Good progress was made.",
                RecordingLink = "https://example.com/session-recording",
                TranscriptOriginalFileName = "session-transcript.docx",
                TranscriptStoragePath =
                    "App_Data/tutor-review-transcripts/1/transcript.docx",
                TranscriptContentType =
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                TranscriptSizeBytes = 1024
            });
        context.TutorCourseModules.Add(assignment);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        SessionDetailsModel page = CreateDetailsPage(context);
        var result = await page.OnGetAsync(1, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Reviewed Student", page.Session.StudentName);
        Assert.NotEmpty(page.StudentReviewAnswers);
        Assert.NotEmpty(page.TutorReviewAnswers);
        Assert.True(page.CanWatchRecording);
        Assert.True(page.HasTranscript);
        Assert.Equal(
            "https://example.com/session-recording",
            page.RecordingUrl?.TrimEnd('/'));
    }

    [Fact]
    public async Task PagePrioritizesSessionsAwaitingTutorHeadReview()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking awaiting = CreateBooking(
            1,
            assignment,
            "Awaiting Student",
            BookingStatus.Completed,
            new StudentEvaluation(),
            new TutorStudentEvaluation());
        Booking reviewed = CreateBooking(
            2,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation(),
            new TutorStudentEvaluation());
        var tutorHead = new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        };
        reviewed.SessionReviews.Add(new SessionReview
        {
            Booking = reviewed,
            Reviewer = tutorHead,
            ReviewerBcUserId = tutorHead.BcUserId,
            Rating = 5,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.TutorCourseModules.Add(assignment);
        context.Bookings.AddRange(awaiting, reviewed);
        await context.SaveChangesAsync();

        var page = CreatePage(context);
        await page.OnGetAsync(CancellationToken.None);

        Assert.Collection(
            page.Sessions,
            session => Assert.False(session.HasTutorHeadReview),
            session => Assert.True(session.HasTutorHeadReview));
    }

    [Fact]
    public async Task TutorHeadCanSaveStructuredSessionReview()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking booking = CreateBooking(
            1,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation(),
            new TutorStudentEvaluation());
        context.TutorCourseModules.Add(assignment);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        });
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        SessionDetailsModel page = CreateDetailsPage(context);
        page.TutorHeadReviewInput = new TutorHeadSessionReviewInput
        {
            StudentEngagement = "Partially",
            ConcernLevel = "Concerns",
            OverallAssessment = "Good",
            Decision = "Reject",
            AdditionalComments = "Follow up on student engagement."
        };

        IActionResult result = await page.OnPostTutorHeadReviewAsync(
            1,
            CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(1, redirect.RouteValues?["id"]);
        SessionReview review = Assert.Single(context.SessionReviews);
        Assert.Equal(2, review.ReviewerBcUserId);
        Assert.Equal(assignment.Tutor.BcUserId, review.RevieweeBcUserId);
        Assert.Equal("Concerns", review.ConcernLevel);
        Assert.Equal("Reject", review.Decision);
        Assert.Equal((byte)4, review.Rating);
        Assert.Equal("Follow up on student engagement.", review.Comment);
    }

    [Fact]
    public async Task AiAssessmentUsesSavedKeyAndSessionEvidence()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        Booking booking = CreateBooking(
            1,
            assignment,
            "Reviewed Student",
            BookingStatus.Completed,
            new StudentEvaluation { TutorTopic = "Loops" },
            new TutorStudentEvaluation { TutorComments = "Covered loops." });
        booking.Summary = "Please cover loops.";
        context.TutorCourseModules.Add(assignment);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "TH001",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors,
            EncryptedGeminiApiKey = "protected:gemini-secret-key"
        });
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var assessmentService = new TestGeminiAssessmentService();
        var page = new SessionDetailsModel(
            context,
            new TestCurrentUserService(new CurrentUser(
                2,
                "TH001",
                "Tutor Head",
                "tutorhead@example.com",
                BcUserRole.HeadOfTutors)),
            new TestWebHostEnvironment(),
            new FixedTimeProvider(DateTimeOffset.UtcNow),
            new TestGeminiApiKeyProtector(),
            assessmentService);

        IActionResult result = await page.OnPostGenerateAiAssessmentAsync(
            1,
            CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(page.AiAssessment);
        Assert.True(page.OpenAiAssessmentModal);
        Assert.Equal(
            "52 minutes from transcript timestamps",
            page.AiAssessment.TranscriptDuration);
        Assert.Equal("gemini-secret-key", assessmentService.ApiKey);
        Assert.Equal("Please cover loops.", assessmentService.Evidence?.BookingSummary);
        Assert.Contains("Loops", assessmentService.Evidence?.StudentReview.Values ?? []);

        SessionAiAssessment storedAssessment = Assert.Single(
            context.SessionAiAssessments);
        Assert.Equal(booking.BookingId, storedAssessment.BookingId);
        Assert.Equal(2, storedAssessment.GeneratedByBcUserId);
        Assert.Contains(
            "The evidence is consistent.",
            storedAssessment.AssessmentJson);

        Assert.IsType<PageResult>(await page.OnPostGenerateAiAssessmentAsync(
            booking.BookingId,
            CancellationToken.None));
        Assert.Equal(
            storedAssessment.SessionAiAssessmentId,
            Assert.Single(context.SessionAiAssessments)
                .SessionAiAssessmentId);

        SessionDetailsModel reloadedPage = CreateDetailsPage(context);
        Assert.IsType<PageResult>(await reloadedPage.OnGetAsync(
            booking.BookingId,
            CancellationToken.None));
        Assert.Equal(
            "The evidence is consistent.",
            reloadedPage.AiAssessment?.Summary);
        Assert.Equal(
            storedAssessment.GeneratedAt,
            reloadedPage.AiAssessmentGeneratedAt);
        Assert.False(reloadedPage.OpenAiAssessmentModal);
    }

    [Fact]
    public async Task AiAssessmentIncludesUploadedPdfTranscript()
    {
        string contentRoot = Directory.CreateTempSubdirectory(
            "campus-learn-ai-transcript-").FullName;
        try
        {
            const string relativePath =
                "App_Data/tutor-review-transcripts/1/transcript.pdf";
            string fullPath = Path.Combine(contentRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            byte[] transcriptBytes = "%PDF transcript evidence"u8.ToArray();
            await File.WriteAllBytesAsync(fullPath, transcriptBytes);

            await using ApplicationDbContext context = CreateContext();
            TutorCourseModule assignment = CreateAssignment();
            Booking booking = CreateBooking(
                1,
                assignment,
                "Reviewed Student",
                BookingStatus.Completed,
                new StudentEvaluation { TutorTopic = "Loops" },
                new TutorStudentEvaluation
                {
                    TutorComments = "Covered loops.",
                    TranscriptOriginalFileName = "transcript.pdf",
                    TranscriptStoragePath = relativePath,
                    TranscriptContentType = "application/pdf",
                    TranscriptSizeBytes = transcriptBytes.Length
                });
            context.TutorCourseModules.Add(assignment);
            context.BcUsers.Add(new BcUser
            {
                BcUserId = 2,
                PersonnelNumber = "TH001",
                DisplayName = "Tutor Head",
                Role = BcUserRole.HeadOfTutors,
                EncryptedGeminiApiKey = "protected:gemini-secret-key"
            });
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            var assessmentService = new TestGeminiAssessmentService();
            var page = new SessionDetailsModel(
                context,
                new TestCurrentUserService(new CurrentUser(
                    2,
                    "TH001",
                    "Tutor Head",
                    "tutorhead@example.com",
                    BcUserRole.HeadOfTutors)),
                new TestWebHostEnvironment(contentRoot),
                new FixedTimeProvider(DateTimeOffset.UtcNow),
                new TestGeminiApiKeyProtector(),
                assessmentService);

            IActionResult result = await page.OnPostGenerateAiAssessmentAsync(
                1,
                CancellationToken.None);

            Assert.IsType<PageResult>(result);
            GeminiTranscriptDocument transcript = Assert.IsType<
                GeminiTranscriptDocument>(
                assessmentService.Evidence?.UploadedTranscript);
            Assert.Equal("application/pdf", transcript.ContentType);
            Assert.Equal(
                Convert.ToBase64String(transcriptBytes),
                transcript.Base64Data);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TranscriptOpensInlineForBrowserPreview()
    {
        string contentRoot = Directory.CreateTempSubdirectory(
            "campus-learn-transcript-preview-").FullName;
        try
        {
            const string relativePath =
                "App_Data/tutor-review-transcripts/1/transcript.pdf";
            string fullPath = Path.Combine(contentRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(
                fullPath,
                "%PDF transcript evidence"u8.ToArray());

            await using ApplicationDbContext context = CreateContext();
            TutorCourseModule assignment = CreateAssignment();
            Booking booking = CreateBooking(
                1,
                assignment,
                "Reviewed Student",
                BookingStatus.Completed,
                new StudentEvaluation(),
                new TutorStudentEvaluation
                {
                    TranscriptOriginalFileName = "transcript.pdf",
                    TranscriptStoragePath = relativePath,
                    TranscriptContentType = "application/pdf"
                });
            context.TutorCourseModules.Add(assignment);
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            var page = new SessionDetailsModel(
                context,
                new TestCurrentUserService(new CurrentUser(
                    2,
                    "TH001",
                    "Tutor Head",
                    "tutorhead@example.com",
                    BcUserRole.HeadOfTutors)),
                new TestWebHostEnvironment(contentRoot),
                new FixedTimeProvider(DateTimeOffset.UtcNow));

            PhysicalFileResult result = Assert.IsType<PhysicalFileResult>(
                await page.OnGetTranscriptAsync(1, CancellationToken.None));

            Assert.Equal("application/pdf", result.ContentType);
            Assert.True(string.IsNullOrEmpty(result.FileDownloadName));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SessionReviewsModel CreatePage(
        ApplicationDbContext context) => new(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 9, 29, 10, 0, 0, TimeSpan.Zero)));

    private static SessionDetailsModel CreateDetailsPage(
        ApplicationDbContext context) => new(
            context,
            new TestCurrentUserService(new CurrentUser(
                2,
                "TH001",
                "Tutor Head",
                "tutorhead@example.com",
                BcUserRole.HeadOfTutors)),
            new TestWebHostEnvironment(),
            new FixedTimeProvider(new DateTimeOffset(
                2026, 9, 29, 10, 0, 0, TimeSpan.Zero)));

    private static TutorCourseModule CreateAssignment()
    {
        var programme = new ProgrammeOfStudy
        {
            Id = 1,
            Name = "Computing"
        };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 1,
            Programme = programme,
            ModuleCode = "PROG101",
            ModuleName = "Programming"
        };
        var tutor = new Tutor
        {
            TutorId = 1,
            BcUserId = 1,
            ProgrammeId = 1,
            Programme = programme,
            BcUser = new BcUser
            {
                BcUserId = 1,
                PersonnelNumber = "T001",
                DisplayName = "Taylor Tutor",
                Role = BcUserRole.Tutor
            },
            ReasonForTutoring = "Help students",
            TeachingStyle = "Interactive",
            PreviousTutoringExperience = "Experienced",
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = "https://example.com/video",
            Status = TutorStatus.Approved,
            IsActive = true
        };

        return new TutorCourseModule
        {
            Tutor = tutor,
            ProgrammeModule = module,
            IsActive = true
        };
    }

    private static Booking CreateBooking(
        int id,
        TutorCourseModule assignment,
        string studentName,
        BookingStatus status,
        StudentEvaluation? studentReview,
        TutorStudentEvaluation? tutorReview) => new()
        {
            BookingId = id,
            TutorId = assignment.Tutor.TutorId,
            ProgrammeModuleId =
                assignment.ProgrammeModule.ProgrammeModuleId,
            TutorCourseModule = assignment,
            ProgrammeModule = assignment.ProgrammeModule,
            StudentName = studentName,
            Location = "Online",
            Status = status,
            Duration = SessionDuration.OneHour,
            ScheduledStartTime = new DateTimeOffset(
                2026,
                9,
                20 + id,
                10,
                0,
                0,
                TimeSpan.Zero),
            DateBooked = new DateTimeOffset(
                2026,
                9,
                1,
                10,
                0,
                0,
                TimeSpan.Zero),
            CompletedAt = status == BookingStatus.Completed
                ? new DateTimeOffset(
                    2026,
                    9,
                    20 + id,
                    11,
                    0,
                    0,
                    TimeSpan.Zero)
                : null,
            StudentEvaluation = studentReview,
            TutorEvaluation = tutorReview
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public CurrentUser GetRequiredUser() => user;
    }

    private sealed class TestGeminiApiKeyProtector : IGeminiApiKeyProtector
    {
        public string Protect(string apiKey) => $"protected:{apiKey}";

        public string Unprotect(string protectedApiKey) =>
            protectedApiKey["protected:".Length..];
    }

    private sealed class TestGeminiAssessmentService
        : IGeminiSessionAssessmentService
    {
        public string? ApiKey { get; private set; }

        public GeminiSessionEvidence? Evidence { get; private set; }

        public Task<GeminiSessionAssessment> AssessAsync(
            string apiKey,
            GeminiSessionEvidence evidence,
            CancellationToken cancellationToken)
        {
            ApiKey = apiKey;
            Evidence = evidence;
            return Task.FromResult(new GeminiSessionAssessment(
                "Valid",
                "All requested topics covered",
                "The evidence is consistent.",
                ["The reviews mention loops."],
                [],
                "Verify and complete the human review.",
                "52 minutes from transcript timestamps"));
        }
    }

    private sealed class TestWebHostEnvironment(
        string? contentRoot = null) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } =
            contentRoot ?? AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
