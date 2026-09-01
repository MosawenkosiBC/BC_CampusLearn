using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class SessionLifecycleServiceTests
{
    [Fact]
    public async Task Confirm_RequiresLinkAndWritesHistoryAtomically()
    {
        DateTimeOffset now = new(2026, 8, 31, 9, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddHours(1));
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        SessionLifecycleResult invalid = await service.ConfirmAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            null);
        SessionLifecycleResult valid = await service.ConfirmAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            "https://teams.microsoft.com/meeting");

        Assert.False(invalid.Succeeded);
        Assert.True(valid.Succeeded);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal("https://teams.microsoft.com/meeting", booking.MeetingLink);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(BookingStatus.Pending, history.PreviousStatus);
        Assert.Equal(BookingStatus.Confirmed, history.NewStatus);
        Assert.False(history.ChangedBySystem);
    }

    [Fact]
    public async Task ProcessDueTransitions_DeclinesUnansweredPendingBooking()
    {
        DateTimeOffset now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        await service.ProcessDueTransitionsAsync();

        Assert.Equal(BookingStatus.Declined, booking.Status);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(
            SessionLifecycleService.UnreviewedReasonCode,
            history.ReasonCode);
        Assert.True(history.ChangedBySystem);
    }

    [Fact]
    public async Task Decline_PendingBooking_DoesNotRequireAReason()
    {
        DateTimeOffset now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddHours(1));
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        SessionLifecycleResult result = await service.DeclineAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            reason: null,
            reopenAvailability: false);

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Declined, booking.Status);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(SessionLifecycleService.TutorDeclinedReasonCode, history.ReasonCode);
        Assert.Null(history.Reason);
    }

    [Fact]
    public async Task Decline_ConfirmedBooking_CancelsAndRequiresAReason()
    {
        DateTimeOffset now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddHours(1));
        booking.Status = BookingStatus.Confirmed;
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        SessionLifecycleResult invalid = await service.DeclineAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            reason: null,
            reopenAvailability: false);
        SessionLifecycleResult valid = await service.DeclineAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            "Tutor is no longer available.",
            reopenAvailability: false);

        Assert.False(invalid.Succeeded);
        Assert.True(valid.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(SessionLifecycleService.TutorCancelledReasonCode, history.ReasonCode);
        Assert.Equal("Tutor is no longer available.", history.Reason);
    }

    [Fact]
    public async Task Start_CompletesWhenTriggeredCountdownEnds()
    {
        DateTimeOffset scheduled =
            new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        var clock = new TestTimeProvider(scheduled.AddMinutes(10));
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(scheduled);
        booking.Status = BookingStatus.Confirmed;
        booking.MeetingLink = "https://teams.microsoft.com/meeting";
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(context, clock);

        SessionLifecycleResult started = await service.StartAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            SessionStartSource.Manual);
        DateTimeOffset expectedCompletion =
            scheduled.AddMinutes(10)
                .Add(SessionSchedulingRules.TriggeredCountdownLength);
        clock.UtcNow = expectedCompletion;
        await service.ProcessDueTransitionsAsync();

        Assert.True(started.Succeeded);
        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.Equal(
            expectedCompletion,
            booking.SessionExecution!.ExpectedCompletionAt);
        Assert.Equal(expectedCompletion, booking.SessionExecution.CompletedAt);
    }

    [Fact]
    public async Task StartFiveMinutesEarly_UsesTheFullTriggeredCountdown()
    {
        DateTimeOffset scheduled =
            new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset earlyStart = scheduled.AddMinutes(-5);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(scheduled);
        booking.Status = BookingStatus.Confirmed;
        booking.MeetingLink = "https://teams.microsoft.com/meeting";
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(earlyStart));

        SessionLifecycleResult result = await service.StartAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            SessionStartSource.Manual);

        Assert.True(result.Succeeded);
        Assert.Equal(earlyStart, booking.SessionExecution!.StartedAt);
        Assert.Equal(
            earlyStart.Add(
                SessionSchedulingRules.TriggeredCountdownLength),
            booking.SessionExecution.ExpectedCompletionAt);
        Assert.Equal(
            SessionSchedulingRules.TriggeredCountdownLength,
            booking.SessionExecution.ExpectedCompletionAt -
                booking.SessionExecution.StartedAt);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Booking CreateBooking(DateTimeOffset scheduledStart)
    {
        return new Booking
        {
            TutorId = 12,
            ProgrammeModuleId = 3,
            StudentObjectId = Guid.NewGuid().ToString(),
            StudentTenantId = Guid.NewGuid().ToString(),
            StudentName = "Student",
            Location = "Teams",
            Status = BookingStatus.Pending,
            Duration = SessionDuration.OneHour,
            ScheduledStartTime = scheduledStart,
            DateBooked = scheduledStart.AddDays(-1)
        };
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public TestTimeProvider(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
