using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminDashboardTests
{
    [Fact]
    public async Task DashboardCountsCurrentAdministrativeWorkload()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Tutors.AddRange(
            CreateTutor(1, TutorStatus.Approved, isActive: true),
            CreateTutor(2, TutorStatus.Approved, isActive: false),
            CreateTutor(3, TutorStatus.Pending, isActive: false));
        context.LearningResources.AddRange(
            CreateResource(1),
            CreateResource(2));
        context.TutorModuleChangeRequests.Add(new TutorModuleChangeRequest
        {
            TutorId = 1,
            ProgrammeModuleId = 1,
            Status = TutorAccountRequestStatus.Pending
        });
        context.TutorDeregistrationRequests.Add(new TutorDeregistrationRequest
        {
            TutorId = 1,
            Status = TutorAccountRequestStatus.Pending,
            Reason = "Test request"
        });
        context.Bookings.AddRange(
            CreateBooking(1, BookingStatus.Completed),
            CreateBooking(2, BookingStatus.Confirmed));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        context.CampusEvents.AddRange(
            CreateEvent("Current event", now.AddDays(-1), now.AddDays(1),
                isPublished: true, publishAt: now.AddDays(-2)),
            CreateEvent("Draft event", now.AddDays(-1), now.AddDays(1),
                isPublished: false, publishAt: now.AddDays(-2)),
            CreateEvent("Past event", now.AddDays(-2), now.AddDays(-1),
                isPublished: true, publishAt: now.AddDays(-3)),
            CreateEvent("Scheduled event", now.AddDays(1), now.AddDays(2),
                isPublished: true, publishAt: now.AddHours(1)));
        await context.SaveChangesAsync();

        var page = new DashboardModel(context);
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, page.ActiveTutorCount);
        Assert.Equal(2, page.TutorResourceCount);
        Assert.Equal(2, page.PendingAdminRequestCount);
        Assert.Equal(1, page.CompletedSessionCount);
        var currentEvent = Assert.Single(page.CurrentEvents);
        Assert.Equal("Current event", currentEvent.Title);
    }

    private static Tutor CreateTutor(
        int tutorId,
        TutorStatus status,
        bool isActive) => new()
    {
        TutorId = tutorId,
        BcUserId = tutorId,
        ProgrammeId = 1,
        ReasonForTutoring = "Test reason",
        TeachingStyle = "Test style",
        PreviousTutoringExperience = "Test experience",
        CampusOfStudy = "Pretoria",
        DemonstrationVideoUrl = "https://example.com/demo",
        Status = status,
        IsActive = isActive
    };

    private static LearningResource CreateResource(int resourceId) => new()
    {
        LearningResourceId = resourceId,
        TutorId = 1,
        ProgrammeModuleId = 1,
        Topic = $"Resource {resourceId}",
        Content = "Test content"
    };

    private static Booking CreateBooking(
        int bookingId,
        BookingStatus status) => new()
    {
        BookingId = bookingId,
        TutorId = 1,
        ProgrammeModuleId = 1,
        StudentName = "Test Student",
        Location = "Online",
        Status = status,
        Duration = SessionDuration.OneHour,
        ScheduledStartTime = DateTimeOffset.UtcNow,
        DateBooked = DateTimeOffset.UtcNow
    };

    private static CampusEvent CreateEvent(
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isPublished,
        DateTimeOffset publishAt) => new()
    {
        Title = title,
        Description = "Event description",
        Location = "Pretoria",
        StartsAt = startsAt,
        EndsAt = endsAt,
        IsPublished = isPublished,
        PublishAt = publishAt,
        BannerImagePath = "/uploads/events/banner.jpg",
        CreatedAt = startsAt.AddDays(-1)
    };
}
