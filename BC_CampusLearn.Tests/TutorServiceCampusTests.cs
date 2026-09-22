using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorServiceCampusTests
{
    [Fact]
    public async Task GetTutorsOrdersPreferredCampusFirstWithoutHidingOthers()
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
        context.Tutors.AddRange(
            CreateTutor(1, "Pretoria Tutor", "Pretoria Campus", programme),
            CreateTutor(2, "Stellenbosch Tutor", "Stellenbosch Campus", programme));
        await context.SaveChangesAsync();
        var service = new TutorService(context);

        IReadOnlyList<TutorCardViewModel> tutors =
            await service.GetTutorsAsync(
                programmeModuleId: null,
                preferredCampus: "Stellenbosch Campus");

        Assert.Equal(2, tutors.Count);
        Assert.Equal("Stellenbosch Tutor", tutors[0].DisplayName);
        Assert.Equal("Pretoria Tutor", tutors[1].DisplayName);
    }

    private static Tutor CreateTutor(
        int id,
        string displayName,
        string campus,
        ProgrammeOfStudy programme) => new()
        {
            TutorId = id,
            BcUser = new BcUser
            {
                BcUserId = id,
                PersonnelNumber = $"60{id:D4}",
                DisplayName = displayName
            },
            Programme = programme,
            OverallAverage = 75,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Teaching style",
            PreviousTutoringExperience = "Experience",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            CampusOfStudy = campus,
            DemonstrationVideoUrl = "https://example.test/video",
            Status = TutorStatus.Approved,
            IsActive = true
        };
}
