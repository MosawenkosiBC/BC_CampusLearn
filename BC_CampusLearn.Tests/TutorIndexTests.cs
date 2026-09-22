using BC_CampusLearn.Authentication;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using BC_CampusLearn.Services.Students;
using BC_CampusLearn.Services.Tutors;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorIndexTests
{
    [Fact]
    public async Task DisplaysAuthenticatedStudentsVerifiedCampus()
    {
        var currentUser = new CurrentUser(
            1,
            "600001",
            "Lebo Nkosi",
            "600001@student.belgiumcampus.ac.za");
        var tutorService = new EmptyTutorService();
        var page = new IndexModel(
            tutorService,
            new TestCurrentUserService(currentUser),
            new TestStudentDetailsService(new StudentDetails(
                "600001",
                "Lebo",
                null,
                "Nkosi",
                "600001@student.belgiumcampus.ac.za",
                "Bachelor of Computing",
                2,
                "Stellenbosch Campus")));

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("Stellenbosch Campus", tutorService.RequestedCampus);
        Assert.Null(page.Campus);
        Assert.Equal(2, page.Tutors.Count);
        Assert.Equal(2, page.CampusOptions.Count);
    }

    [Fact]
    public async Task CampusSelectionFiltersTutorsButKeepsAllCampusOptions()
    {
        var currentUser = new CurrentUser(
            1,
            "600001",
            "Lebo Nkosi",
            "600001@student.belgiumcampus.ac.za");
        var page = new IndexModel(
            new EmptyTutorService(),
            new TestCurrentUserService(currentUser),
            new TestStudentDetailsService(new StudentDetails(
                "600001",
                "Lebo",
                null,
                "Nkosi",
                "600001@student.belgiumcampus.ac.za",
                "Bachelor of Computing",
                2,
                "Stellenbosch Campus")))
        {
            Campus = "Pretoria Campus"
        };

        await page.OnGetAsync(CancellationToken.None);

        TutorCardViewModel tutor = Assert.Single(page.Tutors);
        Assert.Equal("Pretoria Tutor", tutor.DisplayName);
        Assert.Equal(2, page.CampusOptions.Count);
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }

    private sealed class TestStudentDetailsService(StudentDetails details)
        : IStudentDetailsService
    {
        public Task<StudentDetailsResult> GetAsync(
            string personnelNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(StudentDetailsResult.Success(details));
    }

    private sealed class EmptyTutorService : ITutorService
    {
        public string? RequestedCampus { get; private set; }

        public Task<IReadOnlyList<TutorCardViewModel>> GetTutorsAsync(
            int? programmeModuleId,
            string? preferredCampus,
            CancellationToken cancellationToken = default)
        {
            RequestedCampus = preferredCampus;
            return Task.FromResult<IReadOnlyList<TutorCardViewModel>>(
            [
                new TutorCardViewModel
                {
                    TutorId = 1,
                    DisplayName = "Stellenbosch Tutor",
                    CampusOfStudy = "Stellenbosch Campus"
                },
                new TutorCardViewModel
                {
                    TutorId = 2,
                    DisplayName = "Pretoria Tutor",
                    CampusOfStudy = "Pretoria Campus"
                }
            ]);
        }

        public Task<TutorDetailsViewModel?> GetTutorDetailsAsync(
            int tutorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TutorDetailsViewModel?>(null);

        public Task<IReadOnlyList<ProgrammeModule>> GetModulesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProgrammeModule>>([]);

        public Task<IReadOnlyList<ProgrammeOfStudy>> GetProgrammesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProgrammeOfStudy>>([]);

        public Task<IReadOnlyList<string>> GetCampusesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(
            [
                "Pretoria Campus",
                "Stellenbosch Campus"
            ]);
    }
}
