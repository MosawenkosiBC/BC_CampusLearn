using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminApplicationsTests
{
    [Fact]
    public async Task ApplicationsPageCountsAndSearchesVisibleCandidates()
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
        context.BcUsers.AddRange(
            CreateUser(1, "Pending Candidate", "ST1001"),
            CreateUser(2, "Placed Candidate", "ST1002"),
            CreateUser(3, "Rejected Candidate", "ST1003"));
        context.Tutors.AddRange(
            CreateTutor(1, TutorStatus.Pending),
            CreateTutor(2, TutorStatus.Approved),
            CreateTutor(3, TutorStatus.Rejected));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            ShortlistLimit = 2,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context)
        {
            Stage = "applications",
            Search = "Pending"
        };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, page.ApplicationCount);
        Assert.Equal(0, page.PlacementCount);
        Assert.Equal(0, page.ShortlistCount);
        Assert.Equal(0, page.InterviewCount);
        Assert.Single(page.Candidates);
        Assert.Equal("Pending Candidate", page.Candidates[0].DisplayName);
    }

    [Fact]
    public async Task ClosedSettingDoesNotHideApplicationsFromAdministrator()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.ProgrammesOfStudy.Add(new ProgrammeOfStudy
        {
            Id = 1,
            Name = "Diploma in IT"
        });
        context.BcUsers.Add(CreateUser(1, "Pending Candidate", "ST2001"));
        context.Tutors.Add(CreateTutor(1, TutorStatus.Pending));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = false,
            ShortlistLimit = 1,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context)
        {
            Stage = "applications"
        };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, page.ApplicationCount);
        Assert.False(page.OpenApplications);
        Assert.Single(page.Candidates);
        Assert.Equal(TutorStatus.Pending, page.Candidates[0].Status);
    }

    [Fact]
    public async Task ReachingShortlistTargetRejectsUncheckedApplications()
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
        context.BcUsers.AddRange(
            CreateUser(1, "First Candidate", "ST3001"),
            CreateUser(2, "Final Candidate", "ST3002"),
            CreateUser(3, "Unchecked Candidate", "ST3003"));
        context.Tutors.AddRange(
            CreateTutor(1, TutorStatus.Pending),
            CreateTutor(2, TutorStatus.Pending),
            CreateTutor(3, TutorStatus.Pending));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            ShortlistLimit = 2,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context);
        await page.OnPostShortlistAsync(1, false, CancellationToken.None);
        await page.OnPostShortlistAsync(2, true, CancellationToken.None);

        Tutor first = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Tutor second = await context.Tutors.FindAsync(2)
            ?? throw new InvalidOperationException();
        Tutor uncheckedCandidate = await context.Tutors.FindAsync(3)
            ?? throw new InvalidOperationException();

        Assert.Equal(TutorApplicationStage.Shortlisted, first.ApplicationStage);
        Assert.Equal(TutorApplicationStage.Shortlisted, second.ApplicationStage);
        Assert.Equal(TutorStatus.Rejected, uncheckedCandidate.Status);
        Assert.NotNull(uncheckedCandidate.ReviewedAt);
    }

    [Fact]
    public async Task FinalShortlistPlaceRequiresExplicitConfirmation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.ProgrammesOfStudy.Add(new ProgrammeOfStudy
        {
            Id = 1,
            Name = "Diploma in IT"
        });
        context.BcUsers.Add(CreateUser(1, "Only Candidate", "ST4001"));
        context.Tutors.Add(CreateTutor(1, TutorStatus.Pending));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            ShortlistLimit = 1,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context);
        await page.OnPostShortlistAsync(1, false, CancellationToken.None);

        Tutor candidate = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Assert.Equal(TutorApplicationStage.Submitted, candidate.ApplicationStage);
        Assert.Contains("Confirm", page.PageError);
    }

    [Fact]
    public async Task OpenCycleWithoutTargetAllowsUnlimitedShortlisting()
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
        context.BcUsers.AddRange(
            CreateUser(1, "First Candidate", "ST5001"),
            CreateUser(2, "Second Candidate", "ST5002"));
        context.Tutors.AddRange(
            CreateTutor(1, TutorStatus.Pending),
            CreateTutor(2, TutorStatus.Pending));
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context);
        await page.OnPostSettingsAsync(
            true,
            null,
            true,
            true,
            CancellationToken.None);
        await page.OnPostShortlistAsync(1, false, CancellationToken.None);

        TutorApplicationSettings settings = await context
            .TutorApplicationSettings.SingleAsync();
        Tutor shortlisted = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Tutor uncheckedCandidate = await context.Tutors.FindAsync(2)
            ?? throw new InvalidOperationException();

        Assert.True(settings.IsOpen);
        Assert.True(settings.NotifyStudents);
        Assert.Null(settings.ShortlistLimit);
        Assert.Equal(
            TutorApplicationStage.Shortlisted,
            shortlisted.ApplicationStage);
        Assert.Equal(TutorStatus.Pending, uncheckedCandidate.Status);
    }

    [Fact]
    public async Task AdministratorCanManuallyPlaceStudentAsTutor()
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
        context.BcUsers.Add(CreateUser(1, "Manual Tutor", "ST6001"));
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context)
        {
            ManualTutor = new ApplicationsModel.ManualTutorInput
            {
                BcUserId = 1,
                ProgrammeId = 1,
                YearOfStudy = 3,
                OverallAverage = 78,
                CampusOfStudy = "Pretoria",
                PhoneNumber = "0123456789",
                PreferredTutoringMode = PreferredTutoringMode.Both
            }
        };

        await page.OnPostAddTutorAsync(CancellationToken.None);

        BcUser user = await context.BcUsers
            .Include(item => item.Tutor)
            .SingleAsync();
        Assert.Equal(BcUserRole.Tutor, user.Role);
        Assert.NotNull(user.Tutor);
        Assert.Equal(TutorStatus.Approved, user.Tutor.Status);
        Assert.Equal(
            TutorApplicationStage.Placement,
            user.Tutor.ApplicationStage);
        Assert.True(user.Tutor.IsActive);
    }

    private static BcUser CreateUser(
        int id,
        string displayName,
        string personnelNumber) => new()
    {
        BcUserId = id,
        DisplayName = displayName,
        PersonnelNumber = personnelNumber
    };

    private static Tutor CreateTutor(int id, TutorStatus status) => new()
    {
        TutorId = id,
        BcUserId = id,
        ProgrammeId = 1,
        ReasonForTutoring = "Test reason",
        TeachingStyle = "Test style",
        PreviousTutoringExperience = "Test experience",
        CampusOfStudy = "Pretoria",
        DemonstrationVideoUrl = "https://example.com/demo",
        Status = status,
        SubmittedAt = new DateTime(2026, 9, 1).AddDays(id),
        YearOfStudy = 2,
        OverallAverage = 75
    };
}
