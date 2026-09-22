using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Tutors;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorPublicProfileTests
{
    [Fact]
    public async Task PublicProfileIncludesTutorsAccountEmail()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var user = new BcUser
        {
            BcUserId = 1,
            PersonnelNumber = "600001",
            DisplayName = "Lebo Nkosi",
            Email = "600001@student.belgiumcampus.ac.za"
        };
        context.Tutors.Add(new Tutor
        {
            TutorId = 1,
            BcUser = user,
            Programme = new ProgrammeOfStudy
            {
                Id = 1,
                Name = "Bachelor of Computing"
            },
            OverallAverage = 75,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Teaching style",
            PreviousTutoringExperience = "Experience",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            CampusOfStudy = "Pretoria Campus",
            DemonstrationVideoUrl = "https://example.test/video",
            Status = TutorStatus.Approved,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var page = new PublicProfileModel(
            context,
            new TestCurrentUserService(new CurrentUser(
                user.BcUserId,
                user.PersonnelNumber,
                user.DisplayName,
                user.Email,
                BcUserRole.Tutor)));

        var result = await page.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(user.Email, page.EmailAddress);
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }
}
