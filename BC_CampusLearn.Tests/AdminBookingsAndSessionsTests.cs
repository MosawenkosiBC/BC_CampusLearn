using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminBookingsAndSessionsTests
{
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
        Assert.Equal("Reviewed", page.Sessions
            .Single(session => session.StudentName == "Chris Student")
            .AdminApprovalLabel);
        Assert.Equal(0, page.ReviewStats.AwaitingAdminReviewPercentage);
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
        Assert.Equal("Reviewed", approvedSession.AdminApprovalLabel);
        Assert.Equal("is-reviewed", approvedSession.AdminApprovalCssClass);
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
            AdminSessionReviewDeadline = new DateOnly(2026, 10, 20),
            IsAdminSessionReviewDeadlineRecurring = true
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
        context.Bookings.AddRange(
            carriedPending,
            historicalApproved,
            currentPending);
        await context.SaveChangesAsync();
        var page = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 10, 25, 10, 0, 0, TimeSpan.Zero)));

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal("21 Oct 2026 – 20 Nov 2026", page.PeriodLabel);
        Assert.Equal("21 Oct 2026 – 20 Nov 2026", page.CurrentReviewPeriodLabel);
        Assert.Equal(1, page.ReviewStats.CarriedOver);
        Assert.Equal(2, page.ReviewStats.AwaitingAdminReview);
        Assert.Equal(25, page.ReviewStats.AwaitingAdminReviewPercentage);
        Assert.Equal(0, page.ReviewStats.FlaggedConcerns);
        Assert.Equal(0, page.ReviewStats.RejectedByTutorHead);
        Assert.Equal(2, page.FilteredSessionCount);
        Assert.Collection(
            page.Sessions,
            session => Assert.Equal(
                "Current Pending Student",
                session.StudentName),
            session => Assert.Equal(
                "Carried Pending Student",
                session.StudentName));
        Assert.DoesNotContain(page.Sessions, session =>
            session.StudentName == "Historical Approved Student");

        var historical = new BookingsAndSessionsModel(
            context,
            new FixedTimeProvider(new DateTimeOffset(
                2026, 10, 25, 10, 0, 0, TimeSpan.Zero)))
        {
            Period = "custom",
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 10, 20)
        };

        await historical.OnGetAsync(CancellationToken.None);

        Assert.Contains(historical.Sessions, session =>
            session.StudentName == "Historical Approved Student");
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
        BcUser? tutorHeadReviewer = null) => new()
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
        BcUser tutorHeadReviewer) => new()
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

    private sealed class FixedTimeProvider(DateTimeOffset? now = null)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            now ?? new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    }
}
