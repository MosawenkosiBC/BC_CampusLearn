using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorApplicationTests
{
    [Fact]
    public async Task RejectedStudentCanReplaceApplicationWhileCycleIsOpen()
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
                ApplicationStage = TutorApplicationStage.Submitted,
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
                new TestWebHostEnvironment(contentRoot))
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

            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal(1, await context.Tutors.CountAsync());
            Tutor resubmitted = await context.Tutors
                .Include(item => item.TutorCourseModules)
                .SingleAsync();
            Assert.Equal(rejected.TutorId, resubmitted.TutorId);
            Assert.Equal(TutorStatus.Pending, resubmitted.Status);
            Assert.Equal(TutorApplicationStage.Submitted, resubmitted.ApplicationStage);
            Assert.Null(resubmitted.ShortlistReason);
            Assert.Equal("New reason", resubmitted.ReasonForTutoring);
            Assert.Equal(2, resubmitted.TutorCourseModules.Count);
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
