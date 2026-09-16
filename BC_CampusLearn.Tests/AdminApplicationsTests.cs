using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using BC_CampusLearn.Services.Tutors;
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

    [Theory]
    [InlineData("applications")]
    [InlineData("shortlist")]
    [InlineData("interview")]
    [InlineData("placement")]
    public async Task ApplicationTablesExcludeRejectedTutorsRegardlessOfStage(
        string stage)
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
        context.BcUsers.Add(CreateUser(1, "Rejected Candidate", "ST1004"));
        Tutor rejected = CreateTutor(1, TutorStatus.Rejected);
        rejected.ApplicationStage = stage switch
        {
            "shortlist" => TutorApplicationStage.Shortlisted,
            "interview" => TutorApplicationStage.Interview,
            "placement" => TutorApplicationStage.Placement,
            _ => TutorApplicationStage.Submitted
        };
        context.Tutors.Add(rejected);
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context) { Stage = stage };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.Candidates);
        Assert.Equal(0, page.ApplicationCount);
        Assert.Equal(0, page.ShortlistCount);
        Assert.Equal(0, page.InterviewCount);
        Assert.Equal(0, page.PlacementCount);
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
    public async Task ShortlistingCandidatesLeavesUncheckedApplicationsOpen()
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
        await page.OnPostShortlistAsync(
            1, "Strong academic results", CancellationToken.None);
        await page.OnPostShortlistAsync(
            2, "Clear tutoring motivation", CancellationToken.None);

        Tutor first = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Tutor second = await context.Tutors.FindAsync(2)
            ?? throw new InvalidOperationException();
        Tutor uncheckedCandidate = await context.Tutors.FindAsync(3)
            ?? throw new InvalidOperationException();

        Assert.Equal(TutorApplicationStage.Shortlisted, first.ApplicationStage);
        Assert.Equal(TutorApplicationStage.Shortlisted, second.ApplicationStage);
        Assert.Equal("Strong academic results", first.ShortlistReason);
        Assert.Equal(2, await context.UserNotifications.CountAsync());
        Assert.Equal(TutorStatus.Pending, uncheckedCandidate.Status);
        Assert.Equal(
            TutorApplicationStage.Submitted,
            uncheckedCandidate.ApplicationStage);

        page.Stage = "applications";
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(1, page.ApplicationCount);
        Assert.Single(page.Candidates);

        page.Stage = "shortlist";
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, page.ShortlistCount);
        Assert.Equal(2, page.Candidates.Count);
    }

    [Fact]
    public async Task ShortlistingSavesStageReasonAndReviewerAudit()
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
            CreateUser(1, "Candidate", "ST3901"),
            new BcUser
            {
                BcUserId = 2,
                DisplayName = "Reviewing Administrator",
                PersonnelNumber = "AD3902",
                Role = BcUserRole.Admin
            });
        context.Tutors.Add(CreateTutor(1, TutorStatus.Pending));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            ShortlistLimit = 2,
            OpenDate = DateTime.UtcNow.Date.AddDays(-1),
            CloseDate = DateTime.UtcNow.Date.AddDays(7),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        ShortlistResult result = await TutorApplicationReview.ShortlistAsync(
            context,
            1,
            "Strong academic results and tutoring experience.",
            CancellationToken.None,
            reviewerBcUserId: 2);

        Assert.True(result.Succeeded);
        Tutor shortlistedTutor = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Assert.Equal(1, (int)shortlistedTutor.ApplicationStage);

        TutorApplicationReviewDecision decision = await context
            .TutorApplicationReviewDecisions.SingleAsync();
        Assert.Equal(1, decision.TutorId);
        Assert.Equal(2, decision.ReviewerBcUserId);
        Assert.Equal("Reviewing Administrator", decision.AdminName);
        Assert.Equal(TutorApplicationStage.Submitted, decision.PreviousStage);
        Assert.Equal(TutorApplicationStage.Shortlisted, decision.NewStage);
        Assert.Equal(
            "Strong academic results and tutoring experience.",
            decision.Reason);

        var shortlistPage = new ApplicationsModel(context)
        {
            Stage = "shortlist"
        };
        await shortlistPage.OnGetAsync(CancellationToken.None);
        Assert.Single(shortlistPage.Candidates);
        Assert.Equal(1, shortlistPage.Candidates[0].TutorId);
    }

    [Fact]
    public async Task AdministratorCanMoveShortlistedCandidateToInterview()
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
            CreateUser(1, "Shortlisted Candidate", "ST3951"),
            new BcUser
            {
                BcUserId = 2,
                DisplayName = "Interview Administrator",
                PersonnelNumber = "AD3952",
                Role = BcUserRole.Admin
            });
        Tutor candidate = CreateTutor(1, TutorStatus.Pending);
        candidate.ApplicationStage = TutorApplicationStage.Shortlisted;
        candidate.ShortlistReason = "Strong initial application.";
        context.Tutors.Add(candidate);
        await context.SaveChangesAsync();

        ShortlistResult result = await TutorApplicationReview.MoveToInterviewAsync(
            context,
            1,
            new InterviewPreparationDetails(
                "Ask about the candidate's teaching demonstration.",
                new DateTime(2026, 10, 12),
                new TimeSpan(10, 30, 0),
                45,
                "Microsoft Teams",
                "Interview Administrator"),
            CancellationToken.None,
            reviewerBcUserId: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(TutorApplicationStage.Interview, candidate.ApplicationStage);
        Assert.Equal(new DateTime(2026, 10, 12, 10, 30, 0),
            candidate.InterviewScheduledAt);
        Assert.Equal(45, candidate.InterviewDurationMinutes);
        Assert.Equal("Microsoft Teams", candidate.InterviewLocation);
        Assert.Equal("Interview Administrator", candidate.AssignedInterviewer);
        TutorApplicationReviewDecision decision = await context
            .TutorApplicationReviewDecisions.SingleAsync();
        Assert.Equal(TutorApplicationStage.Shortlisted, decision.PreviousStage);
        Assert.Equal(TutorApplicationStage.Interview, decision.NewStage);
        Assert.Equal(
            "Ask about the candidate's teaching demonstration.",
            decision.Reason);
        Assert.Contains(
            "interview stage",
            (await context.UserNotifications.SingleAsync()).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SavingInterviewPreparationDoesNotChangeCandidateStage()
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
            CreateUser(1, "Shortlisted Candidate", "ST3953"),
            new BcUser
            {
                BcUserId = 2,
                DisplayName = "Preparing Administrator",
                PersonnelNumber = "AD3954",
                Role = BcUserRole.Admin
            });
        Tutor candidate = CreateTutor(1, TutorStatus.Pending);
        candidate.ApplicationStage = TutorApplicationStage.Shortlisted;
        context.Tutors.Add(candidate);
        await context.SaveChangesAsync();

        ShortlistResult result = await TutorApplicationReview
            .SaveInterviewPreparationAsync(
                context,
                1,
                new InterviewPreparationDetails(
                    "Focus on first-year module support.",
                    new DateTime(2026, 10, 14),
                    new TimeSpan(14, 0, 0),
                    30,
                    "Room B14",
                    "Tutor Selection Panel"),
                CancellationToken.None,
                reviewerBcUserId: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(TutorApplicationStage.Shortlisted, candidate.ApplicationStage);
        Assert.Equal(
            "Focus on first-year module support.",
            candidate.InterviewPreparationNotes);
        Assert.Equal(new DateTime(2026, 10, 14, 14, 0, 0),
            candidate.InterviewScheduledAt);

        TutorApplicationReviewDecision audit = await context
            .TutorApplicationReviewDecisions.SingleAsync();
        Assert.Equal(TutorApplicationStage.Shortlisted, audit.PreviousStage);
        Assert.Equal(TutorApplicationStage.Shortlisted, audit.NewStage);
        Assert.Equal("Preparing Administrator", audit.AdminName);
    }

    [Fact]
    public async Task AdministratorCanRejectCandidateFromShortlist()
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
        context.BcUsers.Add(CreateUser(1, "Shortlisted Candidate", "ST3961"));
        Tutor candidate = CreateTutor(1, TutorStatus.Pending);
        candidate.ApplicationStage = TutorApplicationStage.Shortlisted;
        context.Tutors.Add(candidate);
        await context.SaveChangesAsync();

        ShortlistResult result = await TutorApplicationReview
            .RejectShortlistedAsync(
                context,
                1,
                "Availability does not meet the programme needs.",
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(TutorStatus.Rejected, candidate.Status);
        Assert.Equal(TutorApplicationStage.Rejected, candidate.ApplicationStage);
        Assert.False(candidate.IsActive);
        Assert.Contains(
            "not progress",
            (await context.UserNotifications.SingleAsync()).Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdministratorCanRejectApplicationWithReasonAndNotification()
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

        ShortlistResult result = await TutorApplicationReview.RejectAsync(
            context,
            1,
            "The application does not meet the academic requirements.",
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Tutor rejected = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Assert.Equal(TutorStatus.Rejected, rejected.Status);
        Assert.Equal(
            TutorApplicationStage.Rejected,
            rejected.ApplicationStage);
        Assert.Equal(
            "The application does not meet the academic requirements.",
            rejected.ShortlistReason);
        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(1, notification.RecipientBcUserId);
        Assert.Contains("not moved", notification.Message);
        Assert.Contains(
            "The application does not meet the academic requirements.",
            notification.Message);
    }

    [Fact]
    public async Task AdministratorCanRejectApplicationWithoutReason()
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
        context.BcUsers.Add(CreateUser(1, "Rejected Candidate", "ST4002"));
        context.Tutors.Add(CreateTutor(1, TutorStatus.Pending));
        await context.SaveChangesAsync();

        ShortlistResult result = await TutorApplicationReview.RejectAsync(
            context,
            1,
            null,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Tutor rejected = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Assert.Equal(TutorStatus.Rejected, rejected.Status);
        Assert.Equal(
            TutorApplicationStage.Rejected,
            rejected.ApplicationStage);
        Assert.Null(rejected.ShortlistReason);
        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(
            "Your tutor application was not moved to the shortlist.",
            notification.Message);
    }

    [Fact]
    public async Task ClosingApplicationCycleDeletesRejectedTutorRows()
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
        context.BcUsers.Add(CreateUser(1, "Rejected Candidate", "ST4003"));
        context.Tutors.Add(CreateTutor(1, TutorStatus.Pending));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            OpenDate = DateTime.UtcNow.Date.AddDays(-1),
            CloseDate = DateTime.UtcNow.Date.AddDays(7),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        await TutorApplicationReview.RejectAsync(
            context,
            1,
            null,
            CancellationToken.None);

        Assert.NotNull(await context.Tutors.FindAsync(1));

        var page = new ApplicationsModel(context);
        await page.OnPostSettingsAsync(
            false,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Null(await context.Tutors.FindAsync(1));
    }

    [Fact]
    public async Task ExpiredApplicationCycleDeletesRejectedTutorRows()
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
        context.BcUsers.Add(CreateUser(1, "Expired Candidate", "ST4004"));
        Tutor rejected = CreateTutor(1, TutorStatus.Rejected);
        rejected.ApplicationStage = TutorApplicationStage.Rejected;
        context.Tutors.Add(rejected);
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            OpenDate = DateTime.UtcNow.Date.AddDays(-7),
            CloseDate = DateTime.UtcNow.Date.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        int removedCount = await TutorApplicationReview
            .RemoveRejectedApplicationsWhenCycleClosedAsync(
                context,
                CancellationToken.None);

        Assert.Equal(1, removedCount);
        Assert.Null(await context.Tutors.FindAsync(1));
    }

    [Fact]
    public async Task OpeningCycleSavesTargetAndApplicationDates()
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
        DateTime openDate = DateTime.UtcNow.Date;
        DateTime closeDate = openDate.AddMonths(1);
        await page.OnPostSettingsAsync(
            true,
            2,
            openDate,
            closeDate,
            CancellationToken.None);
        await page.OnPostShortlistAsync(
            1, "Meets the criteria", CancellationToken.None);

        TutorApplicationSettings settings = await context
            .TutorApplicationSettings.SingleAsync();
        Tutor shortlisted = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Tutor uncheckedCandidate = await context.Tutors.FindAsync(2)
            ?? throw new InvalidOperationException();

        Assert.True(settings.IsOpen);
        Assert.True(settings.NotifyStudents);
        Assert.Equal(2, settings.ShortlistLimit);
        Assert.Equal(openDate, settings.OpenDate);
        Assert.Equal(closeDate, settings.CloseDate);
        Assert.False(settings.ContinueAfterShortlistLimit);
        Assert.Equal(
            TutorApplicationStage.Shortlisted,
            shortlisted.ApplicationStage);
        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(shortlisted.BcUserId, notification.RecipientBcUserId);
        Assert.Contains("Meets the criteria", notification.Message);
        Assert.Equal(TutorStatus.Pending, uncheckedCandidate.Status);
    }

    [Fact]
    public async Task ShortlistingRequiresAReasonBeforeChangingTheApplication()
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
        context.BcUsers.Add(CreateUser(
            1,
            "Unreviewed Candidate",
            "ST5501"));
        context.Tutors.Add(CreateTutor(1, TutorStatus.Pending));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context);
        await page.OnPostShortlistAsync(
            1,
            "   ",
            CancellationToken.None);

        Tutor candidate = await context.Tutors.FindAsync(1)
            ?? throw new InvalidOperationException();
        Assert.Equal(
            TutorApplicationStage.Submitted,
            candidate.ApplicationStage);
        Assert.Null(candidate.ShortlistReason);
        Assert.Empty(context.UserNotifications);
        Assert.Contains(
            "reason",
            page.PageError,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShortlistTargetRequiresAdministratorConfirmationBeforeContinuing()
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
            CreateUser(1, "Target Candidate", "ST5601"),
            CreateUser(2, "Extra Candidate", "ST5602"));
        context.Tutors.AddRange(
            CreateTutor(1, TutorStatus.Pending),
            CreateTutor(2, TutorStatus.Pending));
        context.TutorApplicationSettings.Add(new TutorApplicationSettings
        {
            IsOpen = true,
            ShortlistLimit = 1,
            OpenDate = DateTime.UtcNow.Date,
            CloseDate = DateTime.UtcNow.Date.AddDays(7),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        ShortlistResult targetResult = await TutorApplicationReview.ShortlistAsync(
            context, 1, "Meets the target", CancellationToken.None);
        ShortlistResult blockedResult = await TutorApplicationReview.ShortlistAsync(
            context, 2, "Also suitable", CancellationToken.None);

        Assert.True(targetResult.Succeeded);
        Assert.True(targetResult.ShortlistLimitReached);
        Assert.False(blockedResult.Succeeded);
        Assert.True(blockedResult.RequiresContinuation);
        Assert.Equal(
            TutorApplicationStage.Submitted,
            (await context.Tutors.FindAsync(2))!.ApplicationStage);

        var page = new ApplicationsModel(context);
        await page.OnPostContinueShortlistingAsync(CancellationToken.None);
        ShortlistResult continuedResult = await TutorApplicationReview.ShortlistAsync(
            context, 2, "Also suitable", CancellationToken.None);

        Assert.True(continuedResult.Succeeded);
        Assert.Equal(
            TutorApplicationStage.Shortlisted,
            (await context.Tutors.FindAsync(2))!.ApplicationStage);
    }

    [Fact]
    public void ApplicationCycleOnlyAcceptsSubmissionsInsideConfiguredDates()
    {
        DateTime today = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var settings = new TutorApplicationSettings
        {
            IsOpen = true,
            OpenDate = today.Date,
            CloseDate = today.Date.AddDays(7)
        };

        Assert.True(settings.IsAcceptingApplications(today));
        Assert.False(settings.IsAcceptingApplications(today.AddDays(-1)));
        Assert.False(settings.IsAcceptingApplications(today.AddDays(8)));
        settings.IsOpen = false;
        Assert.False(settings.IsAcceptingApplications(today));
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
