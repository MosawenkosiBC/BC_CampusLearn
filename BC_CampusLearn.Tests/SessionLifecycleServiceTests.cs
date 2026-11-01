using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using BC_CampusLearn.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        AddTutorAssignment(context, booking);
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
        Assert.Equal(
            "https://teams.microsoft.com/meeting",
            booking.MeetingLink?.Url);
        Assert.Single(context.MeetingLinks);
        Assert.Empty(booking.PreparationLinks);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(BookingStatus.Pending, history.PreviousStatus);
        Assert.Equal(BookingStatus.Confirmed, history.NewStatus);
        Assert.False(history.ChangedBySystem);
        UserNotification notification = Assert.Single(
            context.UserNotifications);
        Assert.Equal(booking.StudentBcUserId, notification.RecipientBcUserId);
        Assert.Equal("Booking confirmed", notification.Title);
        Assert.Contains("MOD101 (Module Name)", notification.Message);
        Assert.Contains("Tutor Name", notification.Message);
        Assert.Contains("Teams", notification.Message);
        Assert.Contains(
            "https://teams.microsoft.com/meeting",
            notification.Message);
        Assert.DoesNotContain('\n', notification.Message);
    }

    [Theory]
    [InlineData(3600, "starts in 60 minutes", "12:00")]
    [InlineData(2700, "starts in 45 minutes", "11:45")]
    [InlineData(600, "starts in 10 minutes", "11:10")]
    [InlineData(90, "starts in 2 minutes", "11:01")]
    [InlineData(60, "starts in 1 minute", "11:01")]
    [InlineData(30, "starts in less than a minute", "11:00")]
    public async Task ProcessDueTransitions_SendsOneAccurateReminderToEachParticipant(
        int secondsUntilStart,
        string expectedTiming,
        string expectedStartTime)
    {
        DateTimeOffset now = new(2026, 8, 31, 9, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddSeconds(secondsUntilStart));
        booking.Status = BookingStatus.Confirmed;
        booking.MeetingLink = new MeetingLink
        {
            Url = "https://teams.microsoft.com/reminder"
        };
        AddTutorAssignment(context, booking);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        await service.ProcessDueTransitionsAsync();
        await service.ProcessDueTransitionsAsync();

        Assert.Equal(now, booking.ReminderSentAt);
        List<UserNotification> reminders = await context.UserNotifications
            .Where(item => item.Title == "Session " + expectedTiming)
            .ToListAsync();
        Assert.Equal(2, reminders.Count);
        Assert.Contains(reminders, item =>
            item.RecipientBcUserId == booking.StudentBcUserId);
        Assert.Contains(reminders, item => item.RecipientBcUserId == 8);
        Assert.All(reminders, item =>
        {
            Assert.Contains(expectedTiming, item.Message);
            Assert.Contains($"at {expectedStartTime}", item.Message);
            Assert.DoesNotContain("in one hour", item.Message);
            Assert.Contains("MOD101 (Module Name)", item.Message);
            Assert.Contains("Teams", item.Message);
            Assert.Contains(
                "https://teams.microsoft.com/reminder",
                item.Message);
            Assert.DoesNotContain('\n', item.Message);
        });
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
    public async Task ProcessDueTransitions_StoresSystemCancellationReason()
    {
        DateTimeOffset now =
            new(2026, 8, 31, 10, 15, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddMinutes(-15));
        booking.Status = BookingStatus.Confirmed;
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        await service.ProcessDueTransitionsAsync();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(
            SessionLifecycleService.NotStartedCancellationReason,
            booking.CancellationReason);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(
            SessionLifecycleService.NotStartedReasonCode,
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
        UserNotification notification = Assert.Single(
            context.UserNotifications);
        Assert.Equal(booking.StudentBcUserId, notification.RecipientBcUserId);
        Assert.Equal("Booking declined", notification.Title);
    }

    [Fact]
    public async Task Decline_PendingBooking_StoresOptionalReason()
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
            "Schedule conflict.",
            reopenAvailability: false);

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Declined, booking.Status);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(
            SessionLifecycleService.TutorDeclinedReasonCode,
            history.ReasonCode);
        Assert.Equal("Schedule conflict.", history.Reason);
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
        Assert.Equal(
            "Tutor is no longer available.",
            booking.CancellationReason);
    }

    [Fact]
    public async Task Decline_ConfirmedFutureBooking_ReopensAvailabilityWhenRequested()
    {
        DateTimeOffset now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddHours(2));
        booking.Status = BookingStatus.Confirmed;
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        SessionLifecycleResult result = await service.DeclineAsync(
            booking.TutorId,
            8,
            booking.BookingId,
            "Tutor is no longer available.",
            reopenAvailability: true);

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.True(history.AvailabilityReopened);
        TutorAvailability availability = Assert.Single(
            context.TutorAvailabilities);
        Assert.Equal(booking.TutorId, availability.TutorId);
        Assert.Equal(
            booking.ScheduledStartTime,
            availability.AvailableTime);
    }

    [Fact]
    public async Task StudentCancel_PendingBooking_CancelsWithoutAReason()
    {
        DateTimeOffset now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddHours(1));
        AddTutorAssignment(context, booking);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        SessionLifecycleResult result = await service.CancelByStudentAsync(
            booking.StudentBcUserId!.Value,
            booking.BookingId,
            reason: null);

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal(
            SessionLifecycleService.StudentCancelledReasonCode,
            history.ReasonCode);
        Assert.True(history.AvailabilityReopened);
        Assert.Null(history.Reason);
        Assert.Null(booking.CancellationReason);
        TutorAvailability availability = Assert.Single(
            context.TutorAvailabilities);
        Assert.Equal(booking.TutorId, availability.TutorId);
        Assert.Equal(
            booking.ScheduledStartTime,
            availability.AvailableTime);
        UserNotification notification = Assert.Single(
            context.UserNotifications);
        Assert.Equal(8, notification.RecipientBcUserId);
        Assert.Equal("Booking cancelled by student", notification.Title);
    }

    [Fact]
    public async Task StudentCancel_PendingPastBooking_DoesNotReopenAvailability()
    {
        DateTimeOffset now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddMinutes(-1));
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        SessionLifecycleResult result = await service.CancelByStudentAsync(
            booking.StudentBcUserId!.Value,
            booking.BookingId,
            reason: null);

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.False(Assert.Single(booking.StatusHistory).AvailabilityReopened);
        Assert.Empty(context.TutorAvailabilities);
    }

    [Fact]
    public async Task StudentCancel_ConfirmedBooking_RequiresAReason()
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

        SessionLifecycleResult invalid = await service.CancelByStudentAsync(
            booking.StudentBcUserId!.Value,
            booking.BookingId,
            reason: null);
        SessionLifecycleResult valid = await service.CancelByStudentAsync(
            booking.StudentBcUserId.Value,
            booking.BookingId,
            "I can no longer attend the session.");

        Assert.False(invalid.Succeeded);
        Assert.True(valid.Succeeded);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        BookingStatusHistory history = Assert.Single(booking.StatusHistory);
        Assert.Equal("I can no longer attend the session.", history.Reason);
        Assert.Equal(
            "I can no longer attend the session.",
            booking.CancellationReason);
        Assert.True(history.AvailabilityReopened);
        TutorAvailability availability = Assert.Single(
            context.TutorAvailabilities);
        Assert.Equal(booking.TutorId, availability.TutorId);
        Assert.Equal(
            booking.ScheduledStartTime,
            availability.AvailableTime);
    }

    [Fact]
    public async Task StudentCancel_DoesNotCancelAnotherStudentsBooking()
    {
        DateTimeOffset now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking booking = CreateBooking(now.AddHours(1));
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(now));

        SessionLifecycleResult result = await service.CancelByStudentAsync(
            studentBcUserId: 999,
            booking.BookingId,
            reason: null);

        Assert.False(result.Succeeded);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Empty(booking.StatusHistory);
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
        booking.MeetingLink = new MeetingLink
        {
            Url = "https://teams.microsoft.com/meeting"
        };
        AddTutorAssignment(context, booking);
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
        Assert.Equal(expectedCompletion, booking.CompletedAt);
        List<UserNotification> completionNotifications = await context
            .UserNotifications
            .Where(item => item.Title == "Session completed")
            .ToListAsync();
        Assert.Equal(2, completionNotifications.Count);
        UserNotification studentNotification = Assert.Single(
            completionNotifications,
            item => item.RecipientBcUserId == booking.StudentBcUserId);
        Assert.Contains("is complete", studentNotification.Message);
        Assert.Contains("submit your session review", studentNotification.Message);
        UserNotification tutorNotification = Assert.Single(
            completionNotifications,
            item => item.RecipientBcUserId == 8);
        Assert.Contains("Tutor Name", studentNotification.Message);
        Assert.Contains("Student", tutorNotification.Message);
        Assert.Contains("complete your session review", tutorNotification.Message);
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
        booking.MeetingLink = new MeetingLink
        {
            Url = "https://teams.microsoft.com/meeting"
        };
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
        UserNotification notification = Assert.Single(
            context.UserNotifications);
        Assert.Equal("Session started", notification.Title);
        Assert.Contains("has started", notification.Message);
        Assert.Contains(
            "https://teams.microsoft.com/meeting",
            notification.Message);
    }

    [Fact]
    public async Task Start_TargetsOnlyTheRequestedBookingAndItsMeetingLink()
    {
        DateTimeOffset scheduled =
            new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking requestedBooking = CreateBooking(scheduled);
        requestedBooking.Status = BookingStatus.Confirmed;
        requestedBooking.MeetingLink = new MeetingLink
        {
            Url = "https://teams.microsoft.com/requested-session"
        };
        Booking otherBooking = CreateBooking(scheduled);
        otherBooking.Status = BookingStatus.Confirmed;
        otherBooking.MeetingLink = new MeetingLink
        {
            Url = "https://teams.microsoft.com/other-session"
        };
        context.Bookings.AddRange(requestedBooking, otherBooking);
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(
            context,
            new TestTimeProvider(scheduled));

        SessionLifecycleResult result = await service.StartAsync(
            requestedBooking.TutorId,
            8,
            requestedBooking.BookingId,
            SessionStartSource.JoinMeeting);

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.InProgress, requestedBooking.Status);
        Assert.NotNull(requestedBooking.SessionExecution);
        Assert.Equal(BookingStatus.Confirmed, otherBooking.Status);
        Assert.Null(otherBooking.SessionExecution);
        Assert.Equal(
            "https://teams.microsoft.com/requested-session",
            requestedBooking.MeetingLink.Url);
        Assert.Equal(
            "https://teams.microsoft.com/other-session",
            otherBooking.MeetingLink.Url);
    }

    [Fact]
    public async Task ExistingCancellationEmail_DoesNotBlockAnyOverdueCompletion()
    {
        DateTimeOffset now = new(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        Booking cancelledBefore = CreateBooking(now.AddHours(-3));
        cancelledBefore.Status = BookingStatus.Confirmed;
        cancelledBefore.StudentEmail = "student@example.com";
        AddTutorAssignment(context, cancelledBefore);
        cancelledBefore.TutorCourseModule.Tutor.BcUser.Email = "tutor@example.com";
        Booking first = CreateOverdueSession(now);
        Booking second = CreateOverdueSession(now);
        context.Bookings.AddRange(cancelledBefore, first, second);
        await context.SaveChangesAsync();
        string cancellationKey = $"booking:{cancelledBefore.BookingId}:status:Cancelled:student";
        NotificationEmailQueue.Add(context, cancellationKey, cancelledBefore.StudentEmail,
            "Student", "Previously cancelled", "Original cancellation", null, now.AddHours(-4));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = new SessionLifecycleService(context, new TestTimeProvider(now));

        await service.ProcessDueTransitionsAsync();
        await service.ProcessDueTransitionsAsync();

        List<Booking> bookings = await context.Bookings.AsNoTracking()
            .Include(item => item.SessionExecution).ToListAsync();
        Assert.Equal(BookingStatus.Cancelled,
            bookings.Single(item => item.BookingId == cancelledBefore.BookingId).Status);
        Assert.All(bookings.Where(item => item.BookingId != cancelledBefore.BookingId), item =>
        {
            Assert.Equal(BookingStatus.Completed, item.Status);
            Assert.Equal(now, item.CompletedAt);
            Assert.Equal(now, item.SessionExecution!.CompletedAt);
        });
        List<EmailNotificationOutbox> emails = await context.EmailNotificationOutbox.AsNoTracking().ToListAsync();
        Assert.Equal(5, emails.Count);
        Assert.Equal("Original cancellation", Assert.Single(emails, item => item.EventKey == cancellationKey).Body);
        foreach (Booking completed in new[] { first, second })
        {
            Assert.Contains(emails, item => item.EventKey == $"booking:{completed.BookingId}:status:Completed:student");
            Assert.Contains(emails, item => item.EventKey == $"booking:{completed.BookingId}:completed-review:tutor");
            Assert.Equal(1, await context.BookingStatusHistory.CountAsync(item =>
                item.BookingId == completed.BookingId && item.NewStatus == BookingStatus.Completed));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedBooking_DoesNotRollBackOrBlockOtherBookings_AndIsRetriedNextRun(bool concurrencyFailure)
    {
        DateTimeOffset now = new(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);
        var interceptor = new BookingSaveFailureInterceptor();
        await using ApplicationDbContext context = CreateContext(interceptor);
        Booking first = CreateOverdueSession(now);
        Booking failing = CreateOverdueSession(now);
        Booking last = CreateOverdueSession(now);
        AddTutorAssignment(context, first);
        first.TutorCourseModule.Tutor.BcUser.Email = "tutor@example.com";
        context.Bookings.AddRange(first, failing, last);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        interceptor.FailingBookingId = failing.BookingId;
        interceptor.UseConcurrencyFailure = concurrencyFailure;
        var service = new SessionLifecycleService(context, new TestTimeProvider(now));

        await service.ProcessDueTransitionsAsync();

        List<Booking> bookings = await context.Bookings.AsNoTracking().ToListAsync();
        Assert.Equal(BookingStatus.Completed, bookings.Single(item => item.BookingId == first.BookingId).Status);
        Assert.Equal(BookingStatus.InProgress, bookings.Single(item => item.BookingId == failing.BookingId).Status);
        Assert.Equal(BookingStatus.Completed, bookings.Single(item => item.BookingId == last.BookingId).Status);
        Assert.Equal(4, await context.EmailNotificationOutbox.CountAsync());
        Assert.False(await context.BookingStatusHistory.AnyAsync(item => item.BookingId == failing.BookingId));

        interceptor.FailingBookingId = null;
        await service.ProcessDueTransitionsAsync();

        Assert.All(await context.Bookings.AsNoTracking().ToListAsync(), item =>
            Assert.Equal(BookingStatus.Completed, item.Status));
        Assert.Equal(6, await context.EmailNotificationOutbox.CountAsync());
        Assert.Equal(3, await context.BookingStatusHistory.CountAsync(item => item.NewStatus == BookingStatus.Completed));
    }

    [Fact]
    public async Task ConcurrentEmailInsert_RetriesWithFreshStateAndStillCompletes()
    {
        DateTimeOffset now = new(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);
        var interceptor = new BookingSaveFailureInterceptor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var interceptedOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
            .AddInterceptors(interceptor).Options;
        await using var context = new ApplicationDbContext(interceptedOptions);
        Booking booking = CreateOverdueSession(now);
        AddTutorAssignment(context, booking);
        booking.TutorCourseModule.Tutor.BcUser.Email = "tutor@example.com";
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        interceptor.BeforeConcurrencyFailure = async () =>
        {
            await using var competitor = new ApplicationDbContext(options);
            NotificationEmailQueue.Add(competitor, $"booking:{booking.BookingId}:status:Completed:student",
                booking.StudentEmail, "Student", "Completion", "Already queued", null, now);
            await competitor.SaveChangesAsync();
        };
        var service = new SessionLifecycleService(context, new TestTimeProvider(now));

        await service.ProcessDueTransitionsAsync();
        await service.ProcessDueTransitionsAsync();

        Assert.Equal(BookingStatus.Completed, (await context.Bookings.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(2, await context.EmailNotificationOutbox.CountAsync());
        Assert.Single(await context.BookingStatusHistory.ToListAsync());
        Assert.Equal(2, await context.UserNotifications.CountAsync());
    }

    [Fact]
    public async Task Cancellation_PropagatesWithoutSavingOrProcessingOtherBookings()
    {
        DateTimeOffset now = new(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);
        await using ApplicationDbContext context = CreateContext();
        context.Bookings.Add(CreateOverdueSession(now));
        await context.SaveChangesAsync();
        var service = new SessionLifecycleService(context, new TestTimeProvider(now));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ProcessDueTransitionsAsync(cancellation.Token));

        Assert.Equal(BookingStatus.InProgress, (await context.Bookings.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(context.EmailNotificationOutbox);
    }

    private static Booking CreateOverdueSession(DateTimeOffset now)
    {
        Booking booking = CreateBooking(now.AddHours(-2));
        booking.Status = BookingStatus.InProgress;
        booking.StudentEmail = "student@example.com";
        booking.SessionExecution = new SessionExecution
        {
            StartedAt = now.AddHours(-2),
            ExpectedCompletionAt = now.AddMinutes(-30),
            StartSource = SessionStartSource.Manual
        };
        return booking;
    }

    private sealed class BookingSaveFailureInterceptor : SaveChangesInterceptor
    {
        public int? FailingBookingId { get; set; }
        public bool UseConcurrencyFailure { get; set; }
        public Func<Task>? BeforeConcurrencyFailure { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Booking>().Any(entry =>
                entry.State == EntityState.Modified && entry.Entity.BookingId == FailingBookingId))
            {
                if (UseConcurrencyFailure)
                {
                    throw new DbUpdateConcurrencyException("Simulated repeated concurrent booking save failure.");
                }
                throw new DbUpdateException("Simulated booking save failure.");
            }
            if (BeforeConcurrencyFailure is { } beforeFailure)
            {
                BeforeConcurrencyFailure = null;
                await beforeFailure();
                throw new DbUpdateConcurrencyException("Simulated concurrent lifecycle update.");
            }
            return result;
        }
    }

    private static ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptors)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Booking CreateBooking(DateTimeOffset scheduledStart)
    {
        return new Booking
        {
            TutorId = 12,
            ProgrammeModuleId = 3,
            StudentBcUserId = 21,
            StudentName = "Student",
            Location = "Teams",
            Status = BookingStatus.Pending,
            Duration = SessionDuration.OneHour,
            ScheduledStartTime = scheduledStart,
            DateBooked = scheduledStart.AddDays(-1)
        };
    }

    private static void AddTutorAssignment(
        ApplicationDbContext context,
        Booking booking)
    {
        var tutor = new Tutor
        {
            TutorId = booking.TutorId,
            BcUserId = 8,
            BcUser = new BcUser
            {
                BcUserId = 8,
                PersonnelNumber = "TUTOR8",
                DisplayName = "Tutor Name",
                CreatedAt = DateTime.UtcNow
            },
            ProgrammeId = 1,
            ReasonForTutoring = "Help students",
            TeachingStyle = "Practical",
            PreviousTutoringExperience = "Peer tutoring",
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = string.Empty,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        var assignment = new TutorCourseModule
        {
            TutorId = booking.TutorId,
            Tutor = tutor,
            ProgrammeModuleId = booking.ProgrammeModuleId,
            ProgrammeModule = new ProgrammeModule
            {
                ProgrammeModuleId = booking.ProgrammeModuleId,
                ProgrammeId = 1,
                ModuleCode = "MOD101",
                ModuleName = "Module Name"
            },
            IsActive = true
        };
        booking.TutorCourseModule = assignment;
        booking.ProgrammeModule = assignment.ProgrammeModule;
        context.TutorCourseModules.Add(assignment);
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
