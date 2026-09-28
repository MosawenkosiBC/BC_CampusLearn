using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using BC_CampusLearn.Services.Students;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorApplicationTests
{
    [Fact]
    public async Task SubmittingApplicationCreatesNotificationAndSendsEmail()
    {
        string contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"campuslearn-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var context = new ApplicationDbContext(options);
            var programme = new ProgrammeOfStudy
            {
                Id = 1,
                Name = "Bachelor of Computing",
                ProgrammeModules =
                [
                    new ProgrammeModule
                    {
                        ProgrammeModuleId = 1,
                        ModuleCode = "PRG101",
                        ModuleName = "Programming",
                        YearOfStudy = 1
                    },
                    new ProgrammeModule
                    {
                        ProgrammeModuleId = 2,
                        ModuleCode = "DBS101",
                        ModuleName = "Databases",
                        YearOfStudy = 1
                    }
                ]
            };
            context.ProgrammesOfStudy.Add(programme);
            DateTime closeDate = DateTime.UtcNow.Date.AddDays(7);
            context.TutorApplicationSettings.Add(
                new TutorApplicationSettings
                {
                    IsOpen = true,
                    ShortlistLimit = 2,
                    OpenDate = DateTime.UtcNow.Date.AddDays(-1),
                    CloseDate = closeDate,
                    UpdatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();

            var emailSender = new RecordingTutorApplicationEmailSender();
            var page = new TutorApplicationModel(
                context,
                new TestCurrentUserService(new CurrentUser(
                    1,
                    "600001",
                    "Login Name",
                    "login@example.test")),
                new TestWebHostEnvironment(contentRoot),
                new TestStudentDetailsService(new StudentDetails(
                    "600001",
                    "Lebo",
                    "Lee",
                    "Nkosi",
                    "600001@student.belgiumcampus.ac.za",
                    programme.Name,
                    2,
                    "Pretoria Campus")),
                emailSender)
            {
                Input = new TutorApplicationStageOneInput
                {
                    PhoneNumber = "0123456789",
                    OverallAverage = 80,
                    ProgrammeId = programme.Id,
                    YearOfStudy = 2
                },
                ProfileInput = new TutorApplicationStageTwoInput
                {
                    ReasonForTutoring = "I enjoy helping students.",
                    TeachingStyle = "Patient and practical.",
                    PreviousTutoringExperience = "Peer tutoring.",
                    CampusOfStudy = "Pretoria Campus",
                    DemonstrationVideoUrl = "https://example.test/demo"
                },
                FinalInput = new TutorApplicationStageThreeInput
                {
                    PreferredTutoringMode = PreferredTutoringMode.Both,
                    ProgrammeModuleIds = [1, 2],
                    Transcript = CreateFile("transcript.pdf")
                }
            };

            IActionResult result = await page.OnPostAsync(
                CancellationToken.None);

            Assert.IsType<RedirectToPageResult>(result);
            Tutor tutor = await context.Tutors.SingleAsync();
            Assert.Equal(TutorApplicationStage.Submitted, tutor.ApplicationStage);
            UserNotification notification = await context.UserNotifications
                .SingleAsync();
            Assert.Equal("Tutor application submitted", notification.Title);
            Assert.Contains(
                $"apply for the {closeDate.Year + 1} tutor programme",
                notification.Message);
            Assert.Contains(
                "received your application and supporting documents",
                notification.Message);
            Assert.Contains(
                "carefully review your submission",
                notification.Message);
            Assert.Equal(
                "600001@student.belgiumcampus.ac.za",
                emailSender.SubmittedRecipientEmail);
            Assert.Equal("Lee Nkosi", emailSender.SubmittedRecipientName);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ApplicationUsesVerifiedStudentDetails()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.ProgrammesOfStudy.Add(new ProgrammeOfStudy
        {
            Id = 1,
            Name = "Bachelor of Computing"
        });
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            ShortlistLimit = 2,
            OpenDate = DateTime.UtcNow.Date.AddDays(-1),
            CloseDate = DateTime.UtcNow.Date.AddDays(7),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var currentUser = new CurrentUser(
            1,
            "600001",
            "Unverified Login Name",
            "login@example.test");
        var page = new TutorApplicationModel(
            context,
            new TestCurrentUserService(currentUser),
            new TestWebHostEnvironment(Path.GetTempPath()),
            new TestStudentDetailsService(new StudentDetails(
                "600001",
                "Lebo",
                "Lee",
                "Nkosi",
                "600001@student.belgiumcampus.ac.za",
                "Bachelor of Computing",
                3,
                "Pretoria Campus")),
            new RecordingTutorApplicationEmailSender());

        await page.OnGetAsync(CancellationToken.None);

        Assert.True(page.StudentDetailsVerified);
        Assert.Equal("Lee", page.FirstName);
        Assert.Equal("Nkosi", page.LastName);
        Assert.Equal("600001@student.belgiumcampus.ac.za", page.EmailAddress);
        Assert.Equal(1, page.Input.ProgrammeId);
        Assert.Equal(3, page.Input.YearOfStudy);
        Assert.Equal("Pretoria Campus", page.ProfileInput.CampusOfStudy);
    }

    [Fact]
    public async Task RejectedStudentCannotReapplyWhileCycleIsOpen()
    {
        string contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"campuslearn-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var context = new ApplicationDbContext(options);
            var programme = new ProgrammeOfStudy
            {
                Id = 1,
                Name = "Bachelor of Computing"
            };
            var firstModule = new ProgrammeModule
            {
                ProgrammeModuleId = 1,
                Programme = programme,
                ModuleCode = "PRG101",
                ModuleName = "Programming",
                YearOfStudy = 1
            };
            var secondModule = new ProgrammeModule
            {
                ProgrammeModuleId = 2,
                Programme = programme,
                ModuleCode = "DBS101",
                ModuleName = "Databases",
                YearOfStudy = 1
            };
            var user = new BcUser
            {
                BcUserId = 1,
                PersonnelNumber = "ST6001",
                DisplayName = "Returning Applicant",
                Email = "student@example.test"
            };
            var rejected = new Tutor
            {
                TutorId = 1,
                BcUser = user,
                Programme = programme,
                OverallAverage = 66,
                YearOfStudy = 2,
                PhoneNumber = "0100000000",
                ReasonForTutoring = "Old reason",
                TeachingStyle = "Old style",
                PreviousTutoringExperience = "Old experience",
                CampusOfStudy = "Old campus",
                DemonstrationVideoUrl = "https://example.test/old",
                PreferredTutoringMode = PreferredTutoringMode.Online,
                Status = TutorStatus.Rejected,
                ApplicationStage = TutorApplicationStage.Rejected,
                ShortlistReason = "Previously unsuccessful",
                SubmittedAt = DateTime.UtcNow.AddMonths(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                TutorCourseModules =
                [
                    new TutorCourseModule { ProgrammeModule = firstModule }
                ]
            };
            context.Tutors.Add(rejected);
            context.ProgrammeModules.Add(secondModule);
            context.TutorApplicationSettings.Add(new TutorApplicationSettings
            {
                IsOpen = true,
                ShortlistLimit = 2,
                OpenDate = DateTime.UtcNow.Date.AddDays(-1),
                CloseDate = DateTime.UtcNow.Date.AddDays(7),
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var page = new TutorApplicationModel(
                context,
                new TestCurrentUserService(new CurrentUser(
                    user.BcUserId,
                    user.PersonnelNumber,
                    user.DisplayName,
                    user.Email)),
                new TestWebHostEnvironment(contentRoot),
                new TestStudentDetailsService(new StudentDetails(
                    user.PersonnelNumber,
                    "Returning",
                    null,
                    "Applicant",
                    user.Email!,
                    programme.Name,
                    2,
                    "Pretoria")),
                new RecordingTutorApplicationEmailSender())
            {
                Input = new TutorApplicationStageOneInput
                {
                    ProgrammeId = programme.Id,
                    OverallAverage = 80,
                    YearOfStudy = 2,
                    PhoneNumber = "0123456789"
                },
                ProfileInput = new TutorApplicationStageTwoInput
                {
                    ReasonForTutoring = "New reason",
                    TeachingStyle = "New style",
                    PreviousTutoringExperience = "New experience",
                    CampusOfStudy = "Pretoria",
                    DemonstrationVideoUrl = "https://example.test/new"
                },
                FinalInput = new TutorApplicationStageThreeInput
                {
                    PreferredTutoringMode = PreferredTutoringMode.Both,
                    ProgrammeModuleIds = [1, 2],
                    Transcript = CreateFile("transcript.pdf")
                }
            };

            IActionResult result = await page.OnPostAsync(CancellationToken.None);

            Assert.IsType<PageResult>(result);
            Assert.Equal(1, await context.Tutors.CountAsync());
            Tutor retainedApplication = await context.Tutors
                .Include(item => item.TutorCourseModules)
                .SingleAsync();
            Assert.Equal(rejected.TutorId, retainedApplication.TutorId);
            Assert.Equal(TutorStatus.Rejected, retainedApplication.Status);
            Assert.Equal(
                TutorApplicationStage.Rejected,
                retainedApplication.ApplicationStage);
            Assert.Equal(
                "Previously unsuccessful",
                retainedApplication.ShortlistReason);
            Assert.Equal("Old reason", retainedApplication.ReasonForTutoring);
            Assert.Single(retainedApplication.TutorCourseModules);
            Assert.Contains(
                page.ModelState[string.Empty]!.Errors,
                error => error.ErrorMessage.Contains(
                    "cannot submit another application",
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    private static FormFile CreateFile(string fileName)
    {
        var stream = new MemoryStream("test document"u8.ToArray());
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }

    private sealed class RecordingTutorApplicationEmailSender
        : ITutorApplicationEmailSender
    {
        public string? SubmittedRecipientEmail { get; private set; }
        public string? SubmittedRecipientName { get; private set; }

        public Task SendComposedAsync(
            string recipientEmail,
            string subject,
            string body,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendApplicationSubmittedAsync(
            string recipientEmail,
            string recipientName,
            CancellationToken cancellationToken)
        {
            SubmittedRecipientEmail = recipientEmail;
            SubmittedRecipientName = recipientName;
            return Task.CompletedTask;
        }

        public Task SendInterviewRejectionAsync(
            string recipientEmail,
            string recipientName,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestStudentDetailsService(StudentDetails details)
        : IStudentDetailsService
    {
        public Task<StudentDetailsResult> GetAsync(
            string personnelNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(StudentDetailsResult.Success(details));
    }

    private sealed class TestWebHostEnvironment(string contentRoot)
        : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
