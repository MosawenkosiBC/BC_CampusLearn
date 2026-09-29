using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminBookingsAndSessionsTests
{
    [Fact]
    public async Task PageShowsCompletedSessionsAndReviewQueues()
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

        Assert.Equal(3, page.CompletedSessionCount);
        Assert.Equal(2, page.RequireAdminReviewCount);
        Assert.Equal(2, page.RequireTutorHeadReviewCount);
        Assert.Equal(3, page.DisplayedSessionCount);
        Assert.DoesNotContain(page.Sessions, session => session.StudentName == "Dana Student");
        Assert.Equal("Approved", page.Sessions
            .Single(session => session.StudentName == "Chris Student")
            .AdminApprovalLabel);
        Assert.Equal("Pending approval", page.Sessions
            .Single(session => session.StudentName == "Alex Student")
            .AdminApprovalLabel);
        Assert.Equal("Awaiting reviews", page.Sessions
            .Single(session => session.StudentName == "Bianca Student")
            .AdminApprovalLabel);
    }

    [Fact]
    public async Task PageFiltersByQueueSearchAndApproval()
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
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Alex Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation()),
            CreateBooking(2, assignment, "Bianca Student", "Pretoria Campus",
                studentReview: new StudentEvaluation()),
            CreateBooking(3, assignment, "Chris Student", "Online",
                studentReview: new StudentEvaluation(),
                tutorReview: new TutorStudentEvaluation(),
                adminReview: ApprovedReview()));
        await context.SaveChangesAsync();

        var adminQueue = new BookingsAndSessionsModel(context, new FixedTimeProvider()) { Queue = "admin" };
        await adminQueue.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, adminQueue.Sessions.Count);
        Assert.Contains(adminQueue.Sessions, session => session.StudentName == "Alex Student");
        Assert.Contains(adminQueue.Sessions, session => session.StudentName == "Bianca Student");

        var tutorHeadQueue = new BookingsAndSessionsModel(context, new FixedTimeProvider()) { Queue = "head" };
        await tutorHeadQueue.OnGetAsync(CancellationToken.None);
        Assert.Equal(3, tutorHeadQueue.Sessions.Count);
        Assert.Equal("Approved", tutorHeadQueue.Sessions
            .Single(session => session.StudentName == "Chris Student")
            .AdminApprovalLabel);
        Assert.Equal("Pending approval", tutorHeadQueue.Sessions
            .Single(session => session.StudentName == "Alex Student")
            .AdminApprovalLabel);

        var approved = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            Search = "Chris",
            Approval = "approved"
        };
        await approved.OnGetAsync(CancellationToken.None);
        Assert.Equal("Chris Student", Assert.Single(approved.Sessions).StudentName);
    }

    [Fact]
    public async Task PageDefaultsToCurrentMonthAndSupportsDayWeekAndCustomPeriods()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorCourseModule assignment = CreateAssignment();
        context.TutorCourseModules.Add(assignment);
        context.Bookings.AddRange(
            CreateBooking(1, assignment, "Monday Student", "Online"),
            CreateBooking(9, assignment, "Current Day Student", "Online"),
            CreateBookingWithDate(20, assignment, "Older Student", new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero)));
        await context.SaveChangesAsync();

        var monthly = new BookingsAndSessionsModel(context, new FixedTimeProvider());
        await monthly.OnGetAsync(CancellationToken.None);
        Assert.Equal("September 2026", monthly.PeriodLabel);
        Assert.Equal(2, monthly.CompletedSessionCount);

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
        for (int id = 1; id <= 10; id++)
        {
            context.Bookings.Add(CreateBookingWithDate(
                id,
                assignment,
                $"Student {id}",
                new DateTimeOffset(2026, 9, 10 + id, 10, 0, 0, TimeSpan.Zero)));
        }
        await context.SaveChangesAsync();

        var firstPage = new BookingsAndSessionsModel(context, new FixedTimeProvider());
        await firstPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(10, firstPage.FilteredSessionCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(8, firstPage.Sessions.Count);

        var secondPage = new BookingsAndSessionsModel(context, new FixedTimeProvider())
        {
            SessionPage = 2
        };
        await secondPage.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, secondPage.Sessions.Count);
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
        DateTimeOffset sessionDate) => new()
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
            CompletedAt = sessionDate.AddHours(1)
        };

    private static AdminSessionReview ApprovedReview() => new()
    {
        ReviewerBcUserId = 900,
        AllReviewsSubmitted = true,
        HeadConfirmedSession = true,
        HeadConfirmedQuality = true,
        ConcernsResolvedOrDocumented = true,
        EvidenceSupportsApproval = true,
        RecordedAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero)
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    }
}
