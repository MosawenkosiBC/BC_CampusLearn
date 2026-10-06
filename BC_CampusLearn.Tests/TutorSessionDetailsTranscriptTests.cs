using System.Text;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using BC_CampusLearn.Services.Sessions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorSessionDetailsTranscriptTests
{
    [Fact]
    public async Task DeregisteredTutorCanViewHistoryButCannotChangeSessions()
    {
        await using var context = CreateContext();
        var booking = CreateCompletedBooking();
        booking.TutorCourseModule.Tutor.Status = TutorStatus.Deregistered;
        booking.TutorCourseModule.Tutor.IsActive = false;
        booking.TutorCourseModule.IsActive = false;
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var page = CreatePage(context, Path.GetTempPath());
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(
            await page.OnGetAsync(booking.BookingId, true, CancellationToken.None));
        Assert.True(page.IsDeregistered);
        Assert.False(page.OpenReviewPanel);
        Assert.IsType<ForbidResult>(await page.OnPostReviewAsync(booking.BookingId, CancellationToken.None));
        Assert.IsType<ForbidResult>(await page.OnPostStartAsync(booking.BookingId, CancellationToken.None));
        Assert.Equal(BookingStatus.Completed, (await context.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task ReviewStoresWordTranscriptOutsideWebRoot()
    {
        string contentRoot = Directory.CreateTempSubdirectory(
            "campus-learn-transcript-").FullName;
        try
        {
            await using ApplicationDbContext context = CreateContext();
            Booking booking = CreateCompletedBooking();
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            byte[] contents = Encoding.UTF8.GetBytes("Meeting transcript");
            var page = CreatePage(context, contentRoot);
            page.EvaluationInput = ValidEvaluation(new FormFile(
                new MemoryStream(contents),
                0,
                contents.Length,
                "EvaluationInput.Transcript",
                "meeting-notes.docx"));

            await page.OnPostReviewAsync(
                booking.BookingId,
                CancellationToken.None);

            TutorStudentEvaluation evaluation = await context
                .TutorStudentEvaluations
                .SingleAsync();
            Assert.Equal(
                "meeting-notes.docx",
                evaluation.TranscriptOriginalFileName);
            Assert.Equal(contents.Length, evaluation.TranscriptSizeBytes);
            Assert.StartsWith(
                "App_Data/tutor-review-transcripts/",
                evaluation.TranscriptStoragePath);
            string storedPath = Path.Combine(
                contentRoot,
                evaluation.TranscriptStoragePath!);
            Assert.True(File.Exists(storedPath));
            Assert.Equal(contents, await File.ReadAllBytesAsync(storedPath));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReviewRejectsUnsupportedTranscriptType()
    {
        string contentRoot = Directory.CreateTempSubdirectory(
            "campus-learn-transcript-").FullName;
        try
        {
            await using ApplicationDbContext context = CreateContext();
            Booking booking = CreateCompletedBooking();
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var page = CreatePage(context, contentRoot);
            page.EvaluationInput = ValidEvaluation(new FormFile(
                new MemoryStream([1, 2, 3]),
                0,
                3,
                "EvaluationInput.Transcript",
                "transcript.exe"));

            await page.OnPostReviewAsync(
                booking.BookingId,
                CancellationToken.None);

            Assert.Empty(context.TutorStudentEvaluations);
            Assert.True(page.SessionActionError);
            Assert.Contains("PDF or Word", page.SessionActionMessage!);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("PRG101")]
    [InlineData("PRG-D-101")]
    public async Task ReviewRequiresTranscript(string moduleCode)
    {
        string contentRoot = Directory.CreateTempSubdirectory(
            "campus-learn-transcript-").FullName;
        try
        {
            await using ApplicationDbContext context = CreateContext();
            Booking booking = CreateCompletedBooking();
            booking.ProgrammeModule.ModuleCode = moduleCode;
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            var page = CreatePage(context, contentRoot);
            page.EvaluationInput = ValidEvaluation(transcript: null);

            await page.OnPostReviewAsync(
                booking.BookingId,
                CancellationToken.None);

            Assert.Empty(context.TutorStudentEvaluations);
            Assert.True(page.SessionActionError);
            Assert.Equal(
                "Upload the meeting transcript.",
                page.SessionActionMessage);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("D-PRG101", false)]
    [InlineData("d-PRG101", false)]
    [InlineData("D-PRG101", true)]
    public async Task DeafModuleReviewDoesNotStoreOrRequireTranscript(
        string moduleCode,
        bool includeTranscript)
    {
        string contentRoot = Directory.CreateTempSubdirectory(
            "campus-learn-transcript-").FullName;
        try
        {
            await using ApplicationDbContext context = CreateContext();
            Booking booking = CreateCompletedBooking();
            booking.ProgrammeModule.ModuleCode = moduleCode;
            context.Bookings.Add(booking);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var page = CreatePage(context, contentRoot);
            page.EvaluationInput = ValidEvaluation(includeTranscript
                ? new FormFile(new MemoryStream([1, 2, 3]), 0, 3,
                    "EvaluationInput.Transcript", "transcript.exe")
                : null);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                page.EvaluationInput,
                new System.ComponentModel.DataAnnotations.ValidationContext(page.EvaluationInput),
                validationResults,
                validateAllProperties: true));

            await page.OnPostReviewAsync(booking.BookingId, CancellationToken.None);

            TutorStudentEvaluation evaluation = await context.TutorStudentEvaluations.SingleAsync();
            Assert.False(page.SessionActionError);
            Assert.Null(evaluation.TranscriptOriginalFileName);
            Assert.Null(evaluation.TranscriptStoragePath);
            Assert.Null(evaluation.TranscriptContentType);
            Assert.Null(evaluation.TranscriptSizeBytes);
            Assert.Empty(Directory.EnumerateFileSystemEntries(contentRoot));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("D-PRG101", false)]
    [InlineData("d-PRG101", false)]
    [InlineData("PRG101", true)]
    [InlineData("PRG-D-101", true)]
    public async Task ReviewFormRequiresTranscriptOnlyForNonDeafModules(
        string moduleCode,
        bool expected)
    {
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateCompletedBooking();
        booking.ProgrammeModule.ModuleCode = moduleCode;
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var page = CreatePage(context, Path.GetTempPath());

        await page.OnGetAsync(booking.BookingId, true, CancellationToken.None);

        Assert.Equal(expected, page.IsTranscriptRequired);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SessionDetailsModel CreatePage(
        ApplicationDbContext context,
        string contentRoot) => new(
            context,
            new TestCurrentUserService(),
            new TestWebHostEnvironment(contentRoot),
            new TestSessionLifecycleService(),
            TimeProvider.System);

    private static TutorStudentEvaluationInput ValidEvaluation(
        IFormFile? transcript) => new()
        {
            SessionPlan = true,
            StudentPreparationInfo = true,
            StudentPunctuality = true,
            StudentPrepared = true,
            StudentInteract = "Participated",
            StudentFocus = "Focused",
            StudentIssues = "None",
            TutorComments = "Good progress",
            RecordingLink = "https://example.com/recording",
            Transcript = transcript
        };

    private static Booking CreateCompletedBooking()
    {
        var programme = new ProgrammeOfStudy { Id = 1, Name = "Computing" };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 1,
            Programme = programme,
            ModuleCode = "PRG101",
            ModuleName = "Programming"
        };
        var tutor = new Tutor
        {
            TutorId = 1,
            BcUserId = 1,
            BcUser = new BcUser
            {
                BcUserId = 1,
                PersonnelNumber = "T001",
                DisplayName = "Taylor Tutor",
                Role = BcUserRole.Tutor
            },
            Programme = programme,
            Status = TutorStatus.Approved,
            IsActive = true,
            ReasonForTutoring = "Help students",
            TeachingStyle = "Interactive",
            PreviousTutoringExperience = "Experienced",
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = string.Empty
        };
        var assignment = new TutorCourseModule
        {
            Tutor = tutor,
            ProgrammeModule = module,
            IsActive = true
        };
        return new Booking
        {
            TutorId = tutor.TutorId,
            TutorCourseModule = assignment,
            ProgrammeModule = module,
            ProgrammeModuleId = module.ProgrammeModuleId,
            StudentName = "Student",
            Location = "Online",
            Status = BookingStatus.Completed,
            Duration = SessionDuration.OneHour,
            ScheduledStartTime = DateTimeOffset.UtcNow.AddHours(-1),
            DateBooked = DateTimeOffset.UtcNow.AddDays(-1),
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public CurrentUser GetRequiredUser() => new(
            1,
            "T001",
            "Taylor Tutor",
            "tutor@example.com",
            BcUserRole.Tutor);
    }

    private sealed class TestWebHostEnvironment(string contentRoot)
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(
            contentRoot,
            "wwwroot");
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class TestSessionLifecycleService : ISessionLifecycleService
    {
        public Task ProcessDueTransitionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SessionLifecycleResult> ConfirmAsync(
            int tutorId,
            int changedByBcUserId,
            int bookingId,
            string? meetingLink,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SessionLifecycleResult.Success());

        public Task<SessionLifecycleResult> DeclineAsync(
            int tutorId,
            int changedByBcUserId,
            int bookingId,
            string? reason,
            bool reopenAvailability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SessionLifecycleResult.Success());

        public Task<SessionLifecycleResult> CancelByStudentAsync(
            int studentBcUserId,
            int bookingId,
            string? reason,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SessionLifecycleResult.Success());

        public Task<SessionLifecycleResult> StartAsync(
            int tutorId,
            int changedByBcUserId,
            int bookingId,
            SessionStartSource source,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SessionLifecycleResult.Success());
    }
}
