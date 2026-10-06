using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Administrator.Admin;
using BC_CampusLearn.Services.Tutors;
using BC_CampusLearn.Services.Students;
using Microsoft.AspNetCore.Mvc;
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
        Assert.Empty(await context.UserNotifications.ToListAsync());
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
                "Microsoft Teams"),
            CancellationToken.None,
            reviewerBcUserId: 2);

        Assert.True(result.Succeeded);
        Assert.Equal(TutorApplicationStage.Interview, candidate.ApplicationStage);
        Assert.Equal(new DateTime(2026, 10, 12, 10, 30, 0),
            candidate.InterviewScheduledAt);
        Assert.Equal(45, candidate.InterviewDurationMinutes);
        Assert.Equal("Microsoft Teams", candidate.InterviewLocation);
        TutorApplicationReviewDecision decision = await context
            .TutorApplicationReviewDecisions.SingleAsync();
        Assert.Equal(TutorApplicationStage.Shortlisted, decision.PreviousStage);
        Assert.Equal(TutorApplicationStage.Interview, decision.NewStage);
        Assert.Equal(
            "Ask about the candidate's teaching demonstration.",
            decision.Reason);
        Assert.Equal(
            "Great news! Your tutor application has progressed to the interview stage. We’re excited to learn more about you and the contribution you could make as a Mzala Connect tutor. Your interview details will be shared with you shortly. Congratulations on reaching this stage, and best of luck with your interview!",
            (await context.UserNotifications.SingleAsync()).Message);
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
                    "Room B14"),
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
    public async Task SavingInterviewRoomNotesKeepsCandidateInInterviewStage()
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
        context.BcUsers.Add(CreateUser(1, "Interview Candidate", "ST3955"));
        Tutor candidate = CreateTutor(1, TutorStatus.Pending);
        candidate.ApplicationStage = TutorApplicationStage.Interview;
        context.Tutors.Add(candidate);
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context)
        {
            InterviewNotes = "  Strong explanation with clear examples.  "
        };

        await page.OnPostSaveInterviewNotesAsync(1, CancellationToken.None);

        Assert.Equal(
            "Strong explanation with clear examples.",
            candidate.InterviewNotes);
        Assert.Equal(TutorApplicationStage.Interview, candidate.ApplicationStage);
        Assert.Equal(1, page.OpenInterviewRoomCandidateId);
        Assert.Null(page.PageMessage);
        Assert.Equal("Interview notes saved.", page.InterviewRoomMessage);
    }

    [Fact]
    public async Task MovingInterviewCandidateToPlacementApprovesAndNotifiesTutor()
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
        BcUser user = CreateUser(1, "Interview Candidate", "ST3956");
        context.BcUsers.Add(user);
        Tutor candidate = CreateTutor(1, TutorStatus.Pending);
        candidate.ApplicationStage = TutorApplicationStage.Interview;
        context.Tutors.Add(candidate);
        await context.SaveChangesAsync();

        ShortlistResult result = await TutorApplicationReview
            .MoveInterviewToPlacementAsync(
                context,
                1,
                CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(TutorStatus.Approved, candidate.Status);
        Assert.Equal(TutorApplicationStage.Placement, candidate.ApplicationStage);
        Assert.True(candidate.IsActive);
        Assert.Equal(BcUserRole.Tutor, user.Role);
        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(candidate.BcUserId, notification.RecipientBcUserId);
        Assert.Equal(
            "Congratulations! We’re delighted to let you know that your tutor application has been approved. Welcome to the Mzala Connect Tutor Team! We’re excited to have you join us and look forward to the positive impact you’ll make by supporting and inspiring fellow students. Your tutoring journey starts here. Well done! 🎓",
            notification.Message);
    }

    [Fact]
    public async Task RejectingInterviewCandidateCreatesNotificationWithoutEmail()
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
        BcUser student = CreateUser(1, "Interview Candidate", "ST3957");
        student.Email = "interview.candidate@example.com";
        context.BcUsers.Add(student);
        Tutor candidate = CreateTutor(1, TutorStatus.Pending);
        candidate.ApplicationStage = TutorApplicationStage.Interview;
        context.Tutors.Add(candidate);
        await context.SaveChangesAsync();

        var emailSender = new RecordingTutorApplicationEmailSender();
        var page = new ApplicationsModel(context, emailSender: emailSender)
        {
            RejectionMessage = new ApplicationMessageInput
            {
                NotificationMessage = "Thank you for applying. Your tutor application was not successful."
            }
        };

        await page.OnPostRejectInterviewedAsync(
            1,
            CancellationToken.None);

        Assert.Equal(TutorStatus.Rejected, candidate.Status);
        Assert.Equal(TutorApplicationStage.Rejected, candidate.ApplicationStage);
        Assert.False(candidate.IsActive);
        UserNotification notification = await context.UserNotifications
            .SingleAsync();
        Assert.Equal(page.RejectionMessage.NotificationMessage, notification.Message);
        Assert.Null(emailSender.ComposedRecipientEmail);
    }

    [Fact]
    public async Task SendingCommunicationCreatesNotificationAndSendsComposedEmail()
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
        BcUser student = CreateUser(1, "Shortlisted Candidate", "ST3958");
        student.Email = "shortlisted.candidate@example.com";
        context.BcUsers.Add(student);
        Tutor candidate = CreateTutor(1, TutorStatus.Pending);
        candidate.ApplicationStage = TutorApplicationStage.Shortlisted;
        context.Tutors.Add(candidate);
        await context.SaveChangesAsync();

        var emailSender = new RecordingTutorApplicationEmailSender();
        var page = new ApplicationsModel(context, emailSender: emailSender)
        {
            Stage = "shortlist",
            Communication = new ApplicationMessageInput
            {
                Subject = "Tutor interview invitation",
                EmailBody = "Your tutor interview is scheduled for Monday at 10:00."
            }
        };

        var result = await page.OnPostSendCommunicationAsync(
            candidate.TutorId,
            CancellationToken.None);

        var redirect = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(result);
        Assert.Equal("shortlist", redirect.RouteValues?["Stage"]);
        UserNotification notification = await context.UserNotifications.SingleAsync();
        Assert.Equal(student.BcUserId, notification.RecipientBcUserId);
        Assert.Equal("Tutor application communication sent", notification.Title);
        Assert.Contains(student.Email, notification.Message);
        Assert.Equal("/Tutors/TutorApplication", notification.LinkUrl);
        Assert.Equal(student.Email, emailSender.ComposedRecipientEmail);
        Assert.Equal(page.Communication.Subject, emailSender.ComposedSubject);
        Assert.Equal(page.Communication.EmailBody, emailSender.ComposedBody);
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
        page.Stage = "shortlist";
        var closeRedirect = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(
            await page.OnPostSettingsAsync(
            false,
            null,
            null,
            null,
            CancellationToken.None));

        Assert.Null(await context.Tutors.FindAsync(1));
        Assert.False((await context.TutorApplicationSettings.SingleAsync()).IsOpen);
        Assert.Equal("applications", closeRedirect.RouteValues!["Stage"]);
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
        var shortlistRedirect = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(
            await page.OnPostShortlistAsync(
                1,
                "Meets the criteria",
                CancellationToken.None));

        Assert.Equal("applications", shortlistRedirect.RouteValues!["Stage"]);

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
        Assert.DoesNotContain(
            await context.UserNotifications.ToListAsync(),
            notification => notification.Title == "Tutor application shortlisted");
        Assert.Equal(TutorStatus.Pending, uncheckedCandidate.Status);
    }

    [Fact]
    public async Task OpeningApplicationsNotifiesEveryStudentWithApplicationLink()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.BcUsers.AddRange(
            CreateUser(1, "First Student", "ST5101"),
            CreateUser(2, "Second Student", "ST5102"),
            new BcUser
            {
                BcUserId = 3,
                DisplayName = "Campus Admin",
                PersonnelNumber = "AD5103",
                Role = BcUserRole.Admin
            });
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

        List<UserNotification> notifications = await context.UserNotifications
            .OrderBy(item => item.RecipientBcUserId)
            .ToListAsync();
        Assert.Collection(
            notifications,
            item => Assert.Equal(1, item.RecipientBcUserId),
            item => Assert.Equal(2, item.RecipientBcUserId));
        Assert.All(notifications, notification =>
        {
            Assert.Equal("🥳 Tutor applications are open", notification.Title);
            Assert.Contains("Interested in helping fellow students?", notification.Message);
            Assert.Contains("• No failed subjects", notification.Message);
            Assert.Contains(
                $"Applications close: {closeDate:dd MMMM yyyy}",
                notification.Message);
            Assert.Equal("/Tutors/TutorApplication", notification.LinkUrl);
        });

        await page.OnPostSettingsAsync(
            true,
            2,
            openDate,
            closeDate,
            CancellationToken.None);

        Assert.Equal(2, await context.UserNotifications.CountAsync());
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
        var continueRedirect = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(
            await page.OnPostContinueShortlistingAsync(CancellationToken.None));
        Assert.Equal("applications", continueRedirect.RouteValues!["Stage"]);
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
        Assert.Equal(2027, settings.GetApplicationYear(today));
        Assert.False(settings.IsAcceptingApplications(today.AddDays(-1)));
        Assert.False(settings.IsAcceptingApplications(today.AddDays(8)));
        settings.IsOpen = false;
        Assert.False(settings.IsAcceptingApplications(today));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdministratorCanOnlyPlaceStudentAfterApiVerification(bool apiAvailable)
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
        context.ProgrammeModules.Add(new ProgrammeModule
        {
            ProgrammeModuleId = 101,
            ProgrammeId = 1,
            ModuleCode = "CMPG101",
            ModuleName = "Introduction to Computing",
            YearOfStudy = 1
        });
        context.BcUsers.Add(CreateUser(1, "Manual Tutor", "ST6001"));
        await context.SaveChangesAsync();

        var page = new ApplicationsModel(context, studentDetailsService: new TestStudentDetailsService(apiAvailable))
        {
            ManualTutor = new ApplicationsModel.ManualTutorInput
            {
                BcUserId = 1,
                ProgrammeId = 999,
                YearOfStudy = 4,
                OverallAverage = 78,
                CampusOfStudy = "Untrusted Campus",
                PhoneNumber = "0123456789",
                PreferredTutoringMode = PreferredTutoringMode.Both,
                ProgrammeModuleIds = [101]
            }
        };
        page.ModelState.AddModelError(
            "InterviewPreparation.Notes",
            "An unrelated form is invalid.");

        var lookup = Assert.IsType<JsonResult>(await page.OnGetStudentDetailsAsync(1, CancellationToken.None));
        Assert.Equal(apiAvailable ? (int?)null : 400, lookup.StatusCode);
        var redirect = Assert.IsType<RedirectToPageResult>(await page.OnPostAddTutorAsync(CancellationToken.None));
        Assert.Equal("placement", redirect.RouteValues!["Stage"]);

        BcUser user = await context.BcUsers
            .Include(item => item.Tutor)
            .SingleAsync();
        if (!apiAvailable)
        {
            Assert.NotNull(page.PageError);
            Assert.Equal(BcUserRole.Student, user.Role);
            Assert.Null(user.Tutor);
            Assert.Empty(await context.Tutors.ToListAsync());
            return;
        }
        Assert.Equal(BcUserRole.Tutor, user.Role);
        Assert.NotNull(user.Tutor);
        Assert.Equal(TutorStatus.Approved, user.Tutor.Status);
        Assert.Equal(
            TutorApplicationStage.Placement,
            user.Tutor.ApplicationStage);
        Assert.True(user.Tutor.IsActive);
        Assert.Equal(1, user.Tutor.ProgrammeId);
        Assert.Equal(3, user.Tutor.YearOfStudy);
        Assert.Equal("Pretoria", user.Tutor.CampusOfStudy);
        Assert.Equal("verified@example.test", user.Email);
        Assert.Single(user.Tutor.TutorCourseModules);
        Assert.Equal(
            101,
            user.Tutor.TutorCourseModules.Single().ProgrammeModuleId);
        Assert.Equal(string.Empty, user.Tutor.ReasonForTutoring);
        Assert.Equal(string.Empty, user.Tutor.TeachingStyle);
        Assert.Equal(string.Empty, user.Tutor.PreviousTutoringExperience);
        Assert.Equal(string.Empty, user.Tutor.DemonstrationVideoUrl);
    }

    [Fact]
    public void ManualTutorValidationRejectsInvalidYearAndCampus()
    {
        var input = new ApplicationsModel.ManualTutorInput
        {
            BcUserId = 1,
            ProgrammeId = 1,
            YearOfStudy = 5,
            OverallAverage = 75,
            CampusOfStudy = "Unknown Campus",
            ProgrammeModuleIds = [101]
        };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        bool isValid = System.ComponentModel.DataAnnotations.Validator
            .TryValidateObject(
                input,
                new System.ComponentModel.DataAnnotations.ValidationContext(input),
                results,
                validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(input.YearOfStudy)));

        input.YearOfStudy = 4;
        results.Clear();
        isValid = System.ComponentModel.DataAnnotations.Validator
            .TryValidateObject(
                input,
                new System.ComponentModel.DataAnnotations.ValidationContext(input),
                results,
                validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(input.CampusOfStudy)));

        input.CampusOfStudy = "Online";
        results.Clear();
        isValid = System.ComponentModel.DataAnnotations.Validator
            .TryValidateObject(
                input,
                new System.ComponentModel.DataAnnotations.ValidationContext(input),
                results,
                validateAllProperties: true);

        Assert.True(isValid);
    }

    [Fact]
    public async Task DeactivatingTutorRestoresStudentRole()
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
        BcUser user = CreateUser(1, "Active Tutor", "ST6002");
        Tutor tutor = CreateTutor(1, TutorStatus.Approved);
        tutor.ApplicationStage = TutorApplicationStage.Placement;
        tutor.IsActive = true;
        context.BcUsers.Add(user);
        context.Tutors.Add(tutor);
        await context.SaveChangesAsync();
        Assert.Equal(BcUserRole.Tutor, user.Role);

        tutor.IsActive = false;
        await context.SaveChangesAsync();

        Assert.Equal(BcUserRole.Student, user.Role);
    }

    [Fact]
    public async Task RemovingTutorRestoresStudentRole()
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
        BcUser user = CreateUser(1, "Removed Tutor", "ST6003");
        Tutor tutor = CreateTutor(1, TutorStatus.Approved);
        tutor.ApplicationStage = TutorApplicationStage.Placement;
        tutor.IsActive = true;
        context.BcUsers.Add(user);
        context.Tutors.Add(tutor);
        await context.SaveChangesAsync();
        Assert.Equal(BcUserRole.Tutor, user.Role);

        context.Tutors.Remove(tutor);
        await context.SaveChangesAsync();

        Assert.Equal(BcUserRole.Student, user.Role);
    }

    private sealed class RecordingTutorApplicationEmailSender
        : ITutorApplicationEmailSender
    {
        public string? RecipientEmail { get; private set; }
        public string? RecipientName { get; private set; }
        public string? ComposedRecipientEmail { get; private set; }
        public string? ComposedSubject { get; private set; }
        public string? ComposedBody { get; private set; }

        public Task SendComposedAsync(
            string recipientEmail,
            string subject,
            string body,
            CancellationToken cancellationToken)
        {
            ComposedRecipientEmail = recipientEmail;
            ComposedSubject = subject;
            ComposedBody = body;
            return Task.CompletedTask;
        }

        public Task SendApplicationSubmittedAsync(
            string recipientEmail,
            string recipientName,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendInterviewRejectionAsync(
            string recipientEmail,
            string recipientName,
            CancellationToken cancellationToken)
        {
            RecipientEmail = recipientEmail;
            RecipientName = recipientName;
            return Task.CompletedTask;
        }
    }

    private sealed class TestStudentDetailsService(bool apiAvailable = true) : IStudentDetailsService
    {
        public Task<StudentDetailsResult> GetAsync(string personnelNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(apiAvailable
                ? StudentDetailsResult.Success(new StudentDetails(
                    personnelNumber, "Manual", null, "Tutor", "verified@example.test", "Bachelor of Computing", 3, "Pretoria"))
                : new StudentDetailsResult(StudentDetailsStatus.Unavailable));
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
