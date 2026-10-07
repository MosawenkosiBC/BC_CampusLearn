using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using BC_CampusLearn.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminBookingsAndSessionsTests
{
    [Fact]
    public async Task SuperAdminQueueShowsOnlySessionsWithAdministratorReview()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.AddRange(
            tutorHead,
            new BcUser
            {
                BcUserId = 900,
                PersonnelNumber = "A900",
                DisplayName = "Admin User",
                Role = BcUserRole.Admin
            });
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Awaiting Admin", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead),
            CreateBooking(2, assignment, "Ready For Superadmin", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: ApprovedReview(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Reject"),
            CreateBooking(3, assignment, "Rejected By Admin", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: RejectedReview(),
                tutorHeadReviewer: tutorHead));
        await context.SaveChangesAsync();

        var page = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(),
            new RoleCurrentUserService(BcUserRole.SuperAdmin));

        await page.OnGetAsync(CancellationToken.None);

        Assert.True(page.IsSuperAdmin);
        Assert.Equal("Ready For Superadmin", Assert.Single(page.Sessions).StudentName);
        Assert.DoesNotContain(page.Sessions,
            session => session.StudentName == "Rejected By Admin");
        Assert.Equal("Rejected", page.Sessions.Single().TutorHeadDecisionLabel);
        Assert.Equal("Awaiting review", page.Sessions.Single().SuperAdminDecisionLabel);
    }

    [Fact]
    public void ConfiguredAdminPeriodAdvancesOnFirstDayOfEachMonth()
    {
        DateOnly configuredStart = new(2026, 10, 21);
        DateOnly configuredDeadline = new(2026, 11, 20);

        ReviewPeriodWindow beforeMonthChange =
            MonthlyReviewPeriod.ResolveByCalendarMonth(
            configuredStart,
            configuredDeadline,
            useLastDayOfMonth: false,
            today: new DateOnly(2026, 10, 25));
        ReviewPeriodWindow novemberPeriod =
            MonthlyReviewPeriod.ResolveByCalendarMonth(
            configuredStart,
            configuredDeadline,
            useLastDayOfMonth: false,
            today: new DateOnly(2026, 11, 1));
        ReviewPeriodWindow decemberPeriod =
            MonthlyReviewPeriod.ResolveByCalendarMonth(
            configuredStart,
            configuredDeadline,
            useLastDayOfMonth: false,
            today: new DateOnly(2026, 12, 1));

        Assert.Equal(new DateOnly(2026, 9, 21), beforeMonthChange.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 20), beforeMonthChange.EndDate);
        Assert.Equal(new DateOnly(2026, 10, 21), novemberPeriod.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 20), novemberPeriod.EndDate);
        Assert.Equal(new DateOnly(2026, 11, 21), decemberPeriod.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 20), decemberPeriod.EndDate);
    }

    [Fact]
    public async Task OneTimeAdminPeriodKeepsConfiguredStartAndDeadline()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(tutorHead);
        context.PlatformSettings.Add(new PlatformSettings
        {
            AdminSessionReviewPeriodStartDate = new DateOnly(2026, 10, 21),
            AdminSessionReviewDeadline = new DateOnly(2026, 11, 30),
            IsAdminSessionReviewDeadlineRecurring = false,
            UseLastDayOfMonthForAdminSessionReviewDeadline = false
        });
        context.Bookings.Add(CreateBookingWithDate(
            1,
            assignment,
            "One-time Period Student",
            new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero),
            tutorHead));
        await context.SaveChangesAsync();

        var page = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 12, 1, 10, 0, 0, TimeSpan.Zero)));

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("21 Oct 2026 – 30 Nov 2026", page.PeriodLabel);
        Assert.Equal("One-time Period Student", Assert.Single(page.Sessions).StudentName);
    }

    [Fact]
    public async Task PageShowsOnlyCompletedSessionsWithAllThreeReviews()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 900,
            PersonnelNumber = "A900",
            DisplayName = "Admin User",
            Role = BcUserRole.Admin
        });
        context.BcUsers.Add(tutorHead);
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Alex Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation()),
            CreateBooking(2, assignment, "Bianca Student", "Pretoria Campus",
                studentReview: new StudentEvaluation()),
            CreateBooking(3, assignment, "Chris Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: ApprovedReview(),
                tutorHeadReviewer: tutorHead),
            CreateBooking(4, assignment, "Dana Student", "Online",
                status: BookingStatus.Confirmed));
        await context.SaveChangesAsync();

        var page = new BookingsAndSessionsModel(context, new FixedTimeProvider());
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, page.FilteredSessionCount);
        Assert.Equal(1, page.DisplayedSessionCount);
        Assert.DoesNotContain(page.Sessions, session => session.StudentName == "Dana Student");
        Assert.DoesNotContain(page.Sessions, session => session.StudentName == "Alex Student");
        Assert.DoesNotContain(page.Sessions, session => session.StudentName == "Bianca Student");
        Assert.Equal("Accepted", page.Sessions
            .Single(session => session.StudentName == "Chris Student")
            .AdminApprovalLabel);
        Assert.Equal("Approved", page.Sessions.Single().TutorHeadDecisionLabel);
        Assert.Equal("is-approved", page.Sessions.Single().TutorHeadDecisionCssClass);
        Assert.Equal(0, page.ReviewStats.AwaitingAdminReviewPercentage);
    }

    [Fact]
    public async Task PageShowsTutorHeadApproveRejectAndEscalateDecisions()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(tutorHead);
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Approved Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Approve"),
            CreateBooking(2, assignment, "Approved Concern Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Approve",
                tutorHeadConcernLevel: "Concerns"),
            CreateBooking(3, assignment, "Escalated Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Escalate"),
            CreateBooking(4, assignment, "Escalated Concern Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Escalate",
                tutorHeadConcernLevel: "Concerns"),
            CreateBooking(5, assignment, "Rejected Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Reject"));
        await context.SaveChangesAsync();

        var page = new BookingsAndSessionsModel(context, new FixedTimeProvider());
        await page.OnGetAsync(CancellationToken.None);

        Assert.Contains(page.Sessions, session =>
            session.TutorHeadDecisionLabel == "Approved" &&
            session.TutorHeadDecisionCssClass == "is-approved");
        Assert.Contains(page.Sessions, session =>
            session.TutorHeadDecisionLabel == "Rejected" &&
            session.TutorHeadDecisionCssClass == "is-rejected");
        BookingsAndSessionsModel.CompletedSessionItem pendingTutorHeadRejection =
            page.Sessions.Single(session =>
                session.StudentName == "Rejected Student");
        Assert.Equal("Awaiting review", pendingTutorHeadRejection.AdminApprovalLabel);
        Assert.Equal("is-awaiting", pendingTutorHeadRejection.AdminApprovalCssClass);
        Assert.Contains(page.Sessions, session =>
            session.TutorHeadDecisionLabel == "Escalated" &&
            session.TutorHeadDecisionCssClass == "is-escalated");
        Assert.Equal(
            [
                "Approved Student",
                "Approved Concern Student",
                "Escalated Student",
                "Escalated Concern Student",
                "Rejected Student"
            ],
            page.Sessions.Select(session => session.StudentName).ToArray());
        Assert.Equal(2, page.ReviewStats.FlaggedConcerns);
        Assert.Equal(1, page.ReviewStats.RejectedByTutorHead);
        Assert.True(page.Sessions.Single(session =>
            session.StudentName == "Approved Concern Student").HasTutorHeadConcern);

        var flaggedPage = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider())
        {
            Stat = "flagged"
        };
        await flaggedPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, flaggedPage.Sessions.Count);

        var rejectedByTutorHeadPage = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider())
        {
            Stat = "tutor-head-rejected"
        };
        await rejectedByTutorHeadPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(
            "Rejected Student",
            Assert.Single(rejectedByTutorHeadPage.Sessions).StudentName);

        var awaitingPage = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider())
        {
            Stat = "awaiting"
        };
        await awaitingPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(5, awaitingPage.FilteredSessionCount);

        Booking acceptedConcern = await context.Bookings
            .SingleAsync(booking => booking.BookingId == 2);
        acceptedConcern.AdminSessionReview = ApprovedReview();
        await context.SaveChangesAsync();

        var refreshedPage = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider());
        await refreshedPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(1, refreshedPage.ReviewStats.FlaggedConcerns);
        Assert.False(refreshedPage.Sessions.Single(session =>
            session.StudentName == "Approved Concern Student").HasTutorHeadConcern);
    }

    [Fact]
    public async Task AdminDecisionDeterminesFinalStatusWithoutChangingTutorHeadDecision()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 900,
            PersonnelNumber = "A900",
            DisplayName = "Admin User",
            Role = BcUserRole.Admin
        });
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(tutorHead);
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Tutor Head Approved", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: RejectedReview(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Approve"),
            CreateBooking(2, assignment, "Tutor Head Rejected", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: ApprovedReview(),
                tutorHeadReviewer: tutorHead,
                tutorHeadDecision: "Reject"));
        await context.SaveChangesAsync();

        var page = new BookingsAndSessionsModel(context, new FixedTimeProvider());
        await page.OnGetAsync(CancellationToken.None);

        BookingsAndSessionsModel.CompletedSessionItem adminRejected =
            page.Sessions.Single(session =>
                session.StudentName == "Tutor Head Approved");
        Assert.Equal("Rejected", adminRejected.AdminApprovalLabel);
        Assert.Equal("Approved", adminRejected.TutorHeadDecisionLabel);

        BookingsAndSessionsModel.CompletedSessionItem adminAccepted =
            page.Sessions.Single(session =>
                session.StudentName == "Tutor Head Rejected");
        Assert.Equal("Accepted", adminAccepted.AdminApprovalLabel);
        Assert.Equal("Rejected", adminAccepted.TutorHeadDecisionLabel);
    }

    [Fact]
    public async Task PageFiltersReviewedSessionsBySearchAndApproval()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 900,
            PersonnelNumber = "A900",
            DisplayName = "Admin User",
            Role = BcUserRole.Admin
        });
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(tutorHead);
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Alex Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead),
            CreateBooking(2, assignment, "Bianca Student", "Pretoria Campus",
                studentReview: new StudentEvaluation()),
            CreateBooking(3, assignment, "Chris Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: ApprovedReview(),
                tutorHeadReviewer: tutorHead),
            CreateBooking(4, assignment, "Dana Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: RejectedReview(),
                tutorHeadReviewer: tutorHead));
        await context.SaveChangesAsync();

        var pending = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            Approval = "pending"
        };
        await pending.OnGetAsync(CancellationToken.None);
        BookingsAndSessionsModel.CompletedSessionItem pendingSession =
            Assert.Single(pending.Sessions);
        Assert.Equal("Alex Student", pendingSession.StudentName);
        Assert.Equal("Awaiting review", pendingSession.AdminApprovalLabel);
        Assert.Equal("is-awaiting", pendingSession.AdminApprovalCssClass);
        Assert.Equal(13, pending.ReviewStats.AwaitingAdminReviewPercentage);

        var approved = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            Search = "Chris",
            Approval = "approved"
        };
        await approved.OnGetAsync(CancellationToken.None);
        BookingsAndSessionsModel.CompletedSessionItem approvedSession =
            Assert.Single(approved.Sessions);
        Assert.Equal("Chris Student", approvedSession.StudentName);
        Assert.Equal("Accepted", approvedSession.AdminApprovalLabel);
        Assert.Equal("is-reviewed", approvedSession.AdminApprovalCssClass);

        var rejected = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            Approval = "rejected"
        };
        await rejected.OnGetAsync(CancellationToken.None);
        BookingsAndSessionsModel.CompletedSessionItem rejectedSession =
            Assert.Single(rejected.Sessions);
        Assert.Equal("Dana Student", rejectedSession.StudentName);
        Assert.Equal("Rejected", rejectedSession.AdminApprovalLabel);
        Assert.Equal("is-rejected", rejectedSession.AdminApprovalCssClass);
    }

    [Fact]
    public async Task PageDefaultsToCurrentMonthAndSupportsDayWeekAndCustomPeriods()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(tutorHead);
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Monday Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead),
            CreateBooking(9, assignment, "Current Day Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                tutorHeadReviewer: tutorHead),
            CreateBookingWithDate(20, assignment, "Older Student",
                new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero),
                tutorHead));
        await context.SaveChangesAsync();

        var monthly = new BookingsAndSessionsModel(context, new FixedTimeProvider());
        await monthly.OnGetAsync(CancellationToken.None);
        Assert.Equal("September 2026", monthly.PeriodLabel);
        Assert.Equal(2, monthly.FilteredSessionCount);

        var daily = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            Period = "day"
        };
        await daily.OnGetAsync(CancellationToken.None);
        Assert.Equal("Current Day Student", Assert.Single(daily.Sessions).StudentName);

        var weekly = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            Period = "week"
        };
        await weekly.OnGetAsync(CancellationToken.None);
        Assert.Equal("Current Day Student", Assert.Single(weekly.Sessions).StudentName);

        var custom = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            Period = "custom",
            From = new DateOnly(2026, 8, 1),
            To = new DateOnly(2026, 8, 31)
        };
        await custom.OnGetAsync(CancellationToken.None);
        Assert.Equal("Older Student", Assert.Single(custom.Sessions).StudentName);
    }

    [Fact]
    public async Task CompletedSessionTableUsesEightRowsPerPage()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(tutorHead);
        for (int id = 1; id <= 10; id++)
        {
            context.Bookings.Add(CreateBookingWithDate(
                id,
                assignment,
                $"Student {id}",
                new DateTimeOffset(2026, 9, 10 + id, 10, 0, 0, TimeSpan.Zero),
                tutorHead));
        }
        await context.SaveChangesAsync();

        var firstPage = new BookingsAndSessionsModel(context, new FixedTimeProvider());
        await firstPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(10, firstPage.FilteredSessionCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(8, firstPage.Sessions.Count);
        Assert.Equal(100, firstPage.ReviewStats.AwaitingAdminReviewPercentage);

        var secondPage = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            SessionPage = 2
        };
        await secondPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, secondPage.Sessions.Count);
    }

    [Fact]
    public async Task PassedAdminDeadlineCarriesPendingReviewsIntoNextPeriod()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        BcUser tutorHead = CreateTutorHead();
        context.BcUsers.Add(tutorHead);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 900,
            PersonnelNumber = "A900",
            DisplayName = "Admin User",
            Role = BcUserRole.Admin
        });
        context.PlatformSettings.Add(new PlatformSettings
        {
            AdminSessionReviewPeriodStartDate = new DateOnly(2026, 9, 21),
            AdminSessionReviewDeadline = new DateOnly(2026, 10, 20),
            IsAdminSessionReviewDeadlineRecurring = true,
            UseLastDayOfMonthForAdminSessionReviewDeadline = false
        });
        Booking carriedPending = CreateBookingWithDate(
            1,
            assignment,
            "Carried Pending Student",
            new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero),
            tutorHead);
        Booking historicalApproved = CreateBookingWithDate(
            2,
            assignment,
            "Historical Approved Student",
            new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero),
            tutorHead);
        historicalApproved.AdminSessionReview = ApprovedReview();
        Booking currentPending = CreateBookingWithDate(
            3,
            assignment,
            "Current Pending Student",
            new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero),
            tutorHead);
        Booking historicalRejected = CreateBookingWithDate(
            4,
            assignment,
            "Historical Rejected Student",
            new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero),
            tutorHead,
            "Reject");
        Booking currentRejected = CreateBookingWithDate(
            5,
            assignment,
            "Current Rejected Student",
            new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.Zero),
            tutorHead,
            "Reject");
        context.Bookings.AddRange(
            carriedPending,
            historicalApproved,
            currentPending,
            historicalRejected,
            currentRejected);
        await context.SaveChangesAsync();

        var afterDeadline = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 10, 25, 10, 0, 0, TimeSpan.Zero)));

        await afterDeadline.OnGetAsync(CancellationToken.None);

        BookingsAndSessionsModel.CompletedSessionItem finalizedRejection =
            afterDeadline.Sessions.Single(session =>
                session.StudentName == "Historical Rejected Student");
        Assert.Equal("Rejected", finalizedRejection.AdminApprovalLabel);
        Assert.Equal("is-rejected", finalizedRejection.AdminApprovalCssClass);
        BookingsAndSessionsModel.CompletedSessionItem approvedPendingAtDeadline =
            afterDeadline.Sessions.Single(session =>
                session.StudentName == "Carried Pending Student");
        Assert.Equal("Awaiting review", approvedPendingAtDeadline.AdminApprovalLabel);

        var page = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 11, 1, 10, 0, 0, TimeSpan.Zero)));

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("21 Oct 2026 – 20 Nov 2026", page.PeriodLabel);
        Assert.Equal("21 Oct 2026 – 20 Nov 2026", page.CurrentReviewPeriodLabel);
        Assert.Equal(1, page.ReviewStats.CarriedOver);
        Assert.Equal(3, page.ReviewStats.AwaitingAdminReview);
        Assert.Equal(38, page.ReviewStats.AwaitingAdminReviewPercentage);
        Assert.Equal(0, page.ReviewStats.FlaggedConcerns);
        Assert.Equal(1, page.ReviewStats.RejectedByTutorHead);
        Assert.Equal(3, page.FilteredSessionCount);
        Assert.Collection(
            page.Sessions,
            session => Assert.Equal(
                "Current Pending Student",
                session.StudentName),
            session => Assert.Equal(
                "Carried Pending Student",
                session.StudentName),
            session =>
            {
                Assert.Equal("Current Rejected Student", session.StudentName);
                Assert.Equal("Awaiting review", session.AdminApprovalLabel);
                Assert.Equal("is-awaiting", session.AdminApprovalCssClass);
            });
        Assert.DoesNotContain(page.Sessions, session =>
            session.StudentName == "Historical Approved Student");
        Assert.DoesNotContain(page.Sessions, session =>
            session.StudentName == "Historical Rejected Student");

        var carriedOver = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 11, 1, 10, 0, 0, TimeSpan.Zero)))
        {
            Stat = "carried-over"
        };

        await carriedOver.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            "Carried Pending Student",
            Assert.Single(carriedOver.Sessions).StudentName);

        var rejected = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 11, 1, 10, 0, 0, TimeSpan.Zero)))
        {
            Approval = "rejected"
        };

        await rejected.OnGetAsync(CancellationToken.None);

        Assert.Empty(rejected.Sessions);

        var historical = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 11, 1, 10, 0, 0, TimeSpan.Zero)))
        {
            Period = "custom",
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 20)
        };

        await historical.OnGetAsync(CancellationToken.None);

        Assert.Contains(historical.Sessions, session =>
            session.StudentName == "Historical Approved Student");
        BookingsAndSessionsModel.CompletedSessionItem historicalRejectedItem =
            Assert.Single(historical.Sessions, session =>
                session.StudentName == "Historical Rejected Student");
        Assert.Equal("Rejected", historicalRejectedItem.AdminApprovalLabel);
        Assert.Equal("is-rejected", historicalRejectedItem.AdminApprovalCssClass);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TutorCourseModule CreateAssignment()
    {
        var programme = new ProgrammeOfStudy { Id = 1, Name = "Computing" };
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
        string location,
        BookingStatus status = BookingStatus.Completed,
        StudentEvaluation? studentReview = null,
        TutorStudentEvaluation? tutorReview = null,
        AdminSessionReview? adminReview = null,
        BcUser? tutorHeadReviewer = null,
        string tutorHeadDecision = "Approve",
        string tutorHeadConcernLevel = "No Concerns") => new()
        {
            BookingId = id,
            TutorId = assignment.Tutor.TutorId,
            ProgrammeModuleId = assignment.ProgrammeModule.ProgrammeModuleId,
            TutorCourseModule = assignment,
            ProgrammeModule = assignment.ProgrammeModule,
            StudentName = studentName,
            Location = location,
            Status = status,
            ScheduledStartTime = new DateTimeOffset(2026, 9, 20 + id, 10, 0, 0, TimeSpan.Zero),
            DateBooked = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero),
            CompletedAt = status == BookingStatus.Completed
                ? new DateTimeOffset(2026, 9, 20 + id, 11, 0, 0, TimeSpan.Zero)
                : null,
            StudentEvaluation = studentReview,
            TutorEvaluation = tutorReview,
            AdminSessionReview = adminReview,
            SessionReviews = tutorHeadReviewer is null
                ? []
                : [new SessionReview
                {
                    ReviewerBcUserId = tutorHeadReviewer.BcUserId,
                    Reviewer = tutorHeadReviewer,
                    Rating = 5,
                    Decision = tutorHeadDecision,
                    ConcernLevel = tutorHeadConcernLevel,
                    CreatedAt = new DateTimeOffset(
                        2026, 9, 29, 9, 0, 0, TimeSpan.Zero)
                }]
        };

    private static BcUser CreateTutorHead() => new()
    {
        BcUserId = 901,
        PersonnelNumber = "H901",
        DisplayName = "Tutor Head",
        Role = BcUserRole.HeadOfTutors
    };

    private static Booking CreateBookingWithDate(
        int id,
        TutorCourseModule assignment,
        string studentName,
        DateTimeOffset sessionDate,
        BcUser tutorHeadReviewer,
        string tutorHeadDecision = "Approve") => new()
        {
            BookingId = id,
            TutorId = assignment.Tutor.TutorId,
            ProgrammeModuleId = assignment.ProgrammeModule.ProgrammeModuleId,
            TutorCourseModule = assignment,
            ProgrammeModule = assignment.ProgrammeModule,
            StudentName = studentName,
            Location = "Online",
            Status = BookingStatus.Completed,
            ScheduledStartTime = sessionDate,
            DateBooked = sessionDate.AddDays(-5),
            CompletedAt = sessionDate.AddHours(1),
            StudentEvaluation = new StudentEvaluation(),
            TutorEvaluation = new TutorStudentEvaluation(),
            SessionReviews = [new SessionReview
            {
                ReviewerBcUserId = tutorHeadReviewer.BcUserId,
                Reviewer = tutorHeadReviewer,
                Rating = 5,
                Decision = tutorHeadDecision,
                CreatedAt = sessionDate.AddHours(2)
            }]
        };

    private static AdminSessionReview ApprovedReview() => new()
    {
        ReviewerBcUserId = 900,
        ReviewEvidenceIsConsistent = true,
        HeadConfirmedQuality = true,
        ConcernsResolvedOrDocumented = true,
        EvidenceSupportsApproval = true,
        RecordedAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero)
    };

    private static AdminSessionReview RejectedReview() => new()
    {
        ReviewerBcUserId = 900,
        ReviewEvidenceIsConsistent = true,
        HeadConfirmedQuality = true,
        ConcernsResolvedOrDocumented = true,
        EvidenceSupportsApproval = false,
        RecordedAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero)
    };

    private sealed class FixedTimeProvider(DateTimeOffset? now = null)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            now ?? new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    }

    private sealed class RoleCurrentUserService(BcUserRole role)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public CurrentUser GetRequiredUser() =>
            new(999, "SA999", "Superadmin", null, role);
    }
}
