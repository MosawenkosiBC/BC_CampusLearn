using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Sessions;

public class SessionLifecycleService : ISessionLifecycleService
{
    public const string UnreviewedReasonCode =
        "TutorDidNotReviewBooking";
    public const string UnreviewedWarningMessage =
        "The booking time passed before you responded. Please ensure that you only make times available when you can review and accept booking requests.";
    public const string NotStartedReasonCode =
        "TutorDidNotStartSession";
    public const string NotStartedCancellationReason =
        "The session was cancelled because the tutor did not start it within the allowed time.";
    public const string NotStartedWarningMessage =
        "You accepted this booking but did not start the session. The administrator has been notified, and repeated incidents may result in disciplinary action.";
    public const string TutorDeclinedReasonCode = "TutorDeclined";
    public const string TutorCancelledReasonCode = "TutorCancelled";
    public const string StudentCancelledReasonCode = "StudentCancelled";

    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SessionLifecycleService(
        ApplicationDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task ProcessDueTransitionsAsync(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<Booking> dueBookings = await _context.Bookings
            .Include(booking => booking.SessionExecution)
            .Where(booking =>
                (booking.Status == BookingStatus.Pending &&
                 booking.ScheduledStartTime <= now) ||
                (booking.Status == BookingStatus.Confirmed &&
                 booking.ScheduledStartTime.AddMinutes(15) <= now) ||
                (booking.Status == BookingStatus.InProgress &&
                 booking.SessionExecution != null &&
                 booking.SessionExecution.ExpectedCompletionAt <= now))
            .ToListAsync(cancellationToken);

        List<Booking> reminderBookings = await _context.Bookings
            .Where(booking =>
                booking.Status == BookingStatus.Confirmed &&
                booking.StudentBcUserId != null &&
                booking.ReminderSentAt == null &&
                booking.ScheduledStartTime > now &&
                booking.ScheduledStartTime <= now.AddHours(1))
            .ToListAsync(cancellationToken);

        foreach (Booking booking in dueBookings)
        {
            BookingStatus? automaticStatus =
                SessionLifecyclePolicy.GetAutomaticTransition(
                    booking.Status,
                    booking.ScheduledStartTime,
                    booking.SessionExecution?.ExpectedCompletionAt,
                    now);

            if (automaticStatus == BookingStatus.Declined &&
                booking.Status == BookingStatus.Pending)
            {
                ChangeStatus(
                    booking,
                    BookingStatus.Declined,
                    now,
                    UnreviewedReasonCode,
                    UnreviewedWarningMessage);
                await AddStudentStatusNotificationAsync(
                    booking,
                    BookingStatus.Declined,
                    now,
                    cancellationToken);
            }
            else if (automaticStatus == BookingStatus.Cancelled &&
                booking.Status == BookingStatus.Confirmed)
            {
                ChangeStatus(
                    booking,
                    BookingStatus.Cancelled,
                    now,
                    NotStartedReasonCode,
                    NotStartedCancellationReason);
                await AddStudentStatusNotificationAsync(
                    booking,
                    BookingStatus.Cancelled,
                    now,
                    cancellationToken);
            }
            else if (automaticStatus == BookingStatus.Completed)
            {
                booking.SessionExecution!.CompletedAt = now;
                booking.CompletedAt = now;
                ChangeStatus(
                    booking,
                    BookingStatus.Completed,
                    now);
                await AddStudentStatusNotificationAsync(
                    booking,
                    BookingStatus.Completed,
                    now,
                    cancellationToken);
                await AddTutorCompletionNotificationAsync(
                    booking,
                    now,
                    cancellationToken);
            }
        }

        foreach (Booking booking in reminderBookings)
        {
            BookingNotificationContext notificationContext =
                await GetNotificationContextAsync(
                    booking,
                    cancellationToken);
            if (!notificationContext.TutorBcUserId.HasValue)
            {
                continue;
            }

            int studentBcUserId = booking.StudentBcUserId!.Value;
            booking.ReminderSentAt = now;
            _context.UserNotifications.AddRange(
                BookingNotificationFactory.SessionReminderForStudent(
                    studentBcUserId,
                    booking.BookingId,
                    notificationContext.Details,
                    now),
                BookingNotificationFactory.SessionReminderForTutor(
                    notificationContext.TutorBcUserId.Value,
                    booking.BookingId,
                    notificationContext.Details,
                    now));
        }

        if (dueBookings.Count > 0 || reminderBookings.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<SessionLifecycleResult> ConfirmAsync(
        int tutorId,
        int changedByBcUserId,
        int bookingId,
        string? meetingLink,
        CancellationToken cancellationToken = default)
    {
        string link = meetingLink?.Trim() ?? string.Empty;
        if (!IsValidMeetingLink(link))
        {
            return SessionLifecycleResult.Failure(
                "Enter a valid HTTP or HTTPS meeting link.");
        }

        Booking? booking = await GetTutorBookingAsync(
            tutorId,
            bookingId,
            cancellationToken);
        if (booking is null)
        {
            return SessionLifecycleResult.Failure(
                "The session could not be found.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (booking.Status != BookingStatus.Pending ||
            booking.ScheduledStartTime <= now)
        {
            return SessionLifecycleResult.Failure(
                "Only an upcoming pending booking can be confirmed.");
        }

        booking.MeetingLink ??= new MeetingLink
        {
            BookingId = booking.BookingId
        };
        booking.MeetingLink.Url = link;
        ChangeStatus(
            booking,
            BookingStatus.Confirmed,
            now,
            changedByBcUserId: changedByBcUserId);
        await AddStudentStatusNotificationAsync(
            booking,
            BookingStatus.Confirmed,
            now,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return SessionLifecycleResult.Success();
    }

    public async Task<SessionLifecycleResult> DeclineAsync(
        int tutorId,
        int changedByBcUserId,
        int bookingId,
        string? reason,
        bool reopenAvailability,
        CancellationToken cancellationToken = default)
    {
        Booking? booking = await GetTutorBookingAsync(
            tutorId,
            bookingId,
            cancellationToken);
        if (booking is null)
        {
            return SessionLifecycleResult.Failure(
                "The session could not be found.");
        }

        if (booking.Status != BookingStatus.Pending &&
            booking.Status != BookingStatus.Confirmed)
        {
            return SessionLifecycleResult.Failure(
                "Only a pending booking can be declined or a confirmed booking can be cancelled.");
        }

        string actionReason = reason?.Trim() ?? string.Empty;
        if (actionReason.Length > 1000)
        {
            return SessionLifecycleResult.Failure(
                "Provide a reason of up to 1000 characters.");
        }

        if (booking.Status == BookingStatus.Confirmed &&
            actionReason.Length < 5)
        {
            return SessionLifecycleResult.Failure(
                "Provide a cancellation reason between 5 and 1000 characters.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        bool availabilityReopened = reopenAvailability &&
            booking.ScheduledStartTime > now &&
            !await HasScheduleConflictAsync(
                booking,
                cancellationToken);

        BookingStatus previousStatus = booking.Status;
        BookingStatus newStatus = previousStatus == BookingStatus.Pending
            ? BookingStatus.Declined
            : BookingStatus.Cancelled;
        booking.Status = newStatus;
        if (newStatus == BookingStatus.Cancelled)
        {
            booking.CancellationReason = actionReason;
        }
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ReasonCode = previousStatus == BookingStatus.Pending
                ? TutorDeclinedReasonCode
                : TutorCancelledReasonCode,
            Reason = string.IsNullOrWhiteSpace(actionReason)
                ? null
                : actionReason,
            ChangedByBcUserId = changedByBcUserId,
            ChangedAt = now,
            AvailabilityReopened = availabilityReopened
        });

        if (availabilityReopened)
        {
            _context.TutorAvailabilities.Add(new TutorAvailability
            {
                TutorId = booking.TutorId,
                AvailableTime = booking.ScheduledStartTime
            });
        }

        await AddStudentStatusNotificationAsync(
            booking,
            newStatus,
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return SessionLifecycleResult.Success();
    }

    public async Task<SessionLifecycleResult> CancelByStudentAsync(
        int studentBcUserId,
        int bookingId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        Booking? booking = await _context.Bookings
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(item =>
                item.BookingId == bookingId &&
                item.StudentBcUserId == studentBcUserId,
                cancellationToken);
        if (booking is null)
        {
            return SessionLifecycleResult.Failure(
                "The session could not be found.");
        }

        if (booking.Status is not (BookingStatus.Pending or
            BookingStatus.Confirmed))
        {
            return SessionLifecycleResult.Failure(
                "Only a pending or confirmed session can be cancelled.");
        }

        string actionReason = reason?.Trim() ?? string.Empty;
        if (booking.Status == BookingStatus.Confirmed &&
            (actionReason.Length < 5 || actionReason.Length > 1000))
        {
            return SessionLifecycleResult.Failure(
                "Provide a cancellation reason between 5 and 1000 characters.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        bool availabilityReopened = booking.ScheduledStartTime > now &&
            !await HasScheduleConflictAsync(
                booking,
                cancellationToken);
        bool reasonIsRequired = booking.Status == BookingStatus.Confirmed;
        ChangeStatus(
            booking,
            BookingStatus.Cancelled,
            now,
            StudentCancelledReasonCode,
            reasonIsRequired ? actionReason : null,
            studentBcUserId,
            availabilityReopened);

        if (availabilityReopened)
        {
            _context.TutorAvailabilities.Add(new TutorAvailability
            {
                TutorId = booking.TutorId,
                AvailableTime = booking.ScheduledStartTime
            });
        }

        BookingNotificationContext notificationContext =
            await GetNotificationContextAsync(
                booking,
                cancellationToken);
        if (notificationContext.TutorBcUserId.HasValue)
        {
            _context.UserNotifications.Add(
                BookingNotificationFactory.StudentCancelled(
                    notificationContext.TutorBcUserId.Value,
                    booking.BookingId,
                    notificationContext.Details,
                    now));
        }

        await _context.SaveChangesAsync(cancellationToken);
        return SessionLifecycleResult.Success();
    }

    public async Task<SessionLifecycleResult> StartAsync(
        int tutorId,
        int changedByBcUserId,
        int bookingId,
        SessionStartSource source,
        CancellationToken cancellationToken = default)
    {
        Booking? booking = await GetTutorBookingAsync(
            tutorId,
            bookingId,
            cancellationToken);
        if (booking is null)
        {
            return SessionLifecycleResult.Failure(
                "The session could not be found.");
        }

        if (booking.Status == BookingStatus.InProgress &&
            booking.SessionExecution is not null)
        {
            return SessionLifecycleResult.Success();
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (booking.Status != BookingStatus.Confirmed ||
            string.IsNullOrWhiteSpace(booking.MeetingLink?.Url) ||
            !SessionLifecyclePolicy.CanStart(
                booking.ScheduledStartTime,
                now))
        {
            return SessionLifecycleResult.Failure(
                "The session can only start from 5 minutes before until 15 minutes after its scheduled time.");
        }

        booking.SessionExecution = new SessionExecution
        {
            StartedAt = now,
            ExpectedCompletionAt = now.Add(
                SessionSchedulingRules.TriggeredCountdownLength),
            StartSource = source
        };
        ChangeStatus(
            booking,
            BookingStatus.InProgress,
            now,
            changedByBcUserId: changedByBcUserId);
        await AddStudentStatusNotificationAsync(
            booking,
            BookingStatus.InProgress,
            now,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return SessionLifecycleResult.Success();
    }

    private async Task<Booking?> GetTutorBookingAsync(
        int tutorId,
        int bookingId,
        CancellationToken cancellationToken)
    {
        return await _context.Bookings
            .Include(booking => booking.SessionExecution)
            .Include(booking => booking.MeetingLink)
            .Include(booking => booking.StatusHistory)
            .SingleOrDefaultAsync(booking =>
                booking.BookingId == bookingId &&
                booking.TutorId == tutorId,
                cancellationToken);
    }

    private async Task<bool> HasScheduleConflictAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        DateTimeOffset rangeStart = booking.ScheduledStartTime.Subtract(
            SessionSchedulingRules.MinimumStartSeparation);
        DateTimeOffset rangeEnd = booking.ScheduledStartTime.Add(
            SessionSchedulingRules.MinimumStartSeparation);

        bool availabilityConflict = await _context.TutorAvailabilities
            .AnyAsync(slot =>
                slot.TutorId == booking.TutorId &&
                slot.AvailableTime > rangeStart &&
                slot.AvailableTime < rangeEnd,
                cancellationToken);
        if (availabilityConflict)
        {
            return true;
        }

        return await _context.Bookings
            .AsNoTracking()
            .AnyAsync(item =>
                item.BookingId != booking.BookingId &&
                item.TutorId == booking.TutorId &&
                item.Status != BookingStatus.Cancelled &&
                item.Status != BookingStatus.Declined &&
                item.ScheduledStartTime > rangeStart &&
                item.ScheduledStartTime < rangeEnd,
                cancellationToken);
    }

    private static void ChangeStatus(
        Booking booking,
        BookingStatus newStatus,
        DateTimeOffset changedAt,
        string? reasonCode = null,
        string? reason = null,
        int? changedByBcUserId = null,
        bool availabilityReopened = false)
    {
        BookingStatus previousStatus = booking.Status;
        booking.Status = newStatus;
        if (newStatus == BookingStatus.Cancelled)
        {
            booking.CancellationReason = string.IsNullOrWhiteSpace(reason)
                ? null
                : reason.Trim();
        }
        booking.StatusHistory.Add(new BookingStatusHistory
        {
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ReasonCode = reasonCode,
            Reason = reason,
            ChangedByBcUserId = changedByBcUserId,
            ChangedBySystem = !changedByBcUserId.HasValue,
            ChangedAt = changedAt,
            AvailabilityReopened = availabilityReopened
        });
    }

    private static bool IsValidMeetingLink(string link)
    {
        return link.Length <= 2048 &&
            Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps);
    }

    private async Task AddStudentStatusNotificationAsync(
        Booking booking,
        BookingStatus status,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        if (booking.StudentBcUserId is not int studentBcUserId)
        {
            return;
        }

        BookingNotificationContext notificationContext =
            await GetNotificationContextAsync(
                booking,
                cancellationToken);

        _context.UserNotifications.Add(
            BookingNotificationFactory.StudentStatusChanged(
                studentBcUserId,
                booking.BookingId,
                status,
                notificationContext.Details,
                createdAt));
    }

    private async Task AddTutorCompletionNotificationAsync(
        Booking booking,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        BookingNotificationContext notificationContext =
            await GetNotificationContextAsync(
                booking,
                cancellationToken);
        if (!notificationContext.TutorBcUserId.HasValue)
        {
            return;
        }

        _context.UserNotifications.Add(
            BookingNotificationFactory.TutorSessionCompleted(
                notificationContext.TutorBcUserId.Value,
                booking.BookingId,
                notificationContext.Details,
                createdAt));
    }

    private async Task<BookingNotificationContext>
        GetNotificationContextAsync(
            Booking booking,
            CancellationToken cancellationToken)
    {
        ModuleNotificationDetails? module = await _context.ProgrammeModules
            .AsNoTracking()
            .Where(item =>
                item.ProgrammeModuleId == booking.ProgrammeModuleId)
            .Select(item => new ModuleNotificationDetails(
                item.ModuleCode,
                item.ModuleName))
            .SingleOrDefaultAsync(cancellationToken);

        int? tutorBcUserId = await _context.Tutors
            .AsNoTracking()
            .Where(tutor => tutor.TutorId == booking.TutorId)
            .Select(tutor => (int?)tutor.BcUserId)
            .SingleOrDefaultAsync(cancellationToken);

        string tutorName = "your tutor";
        if (tutorBcUserId.HasValue)
        {
            UserNameDetails? tutorUser = await _context.BcUsers
                .AsNoTracking()
                .Where(user => user.BcUserId == tutorBcUserId.Value)
                .Select(user => new UserNameDetails(
                    user.DisplayName,
                    user.PersonnelNumber))
                .SingleOrDefaultAsync(cancellationToken);
            if (tutorUser is not null)
            {
                tutorName = string.IsNullOrWhiteSpace(tutorUser.DisplayName)
                    ? tutorUser.PersonnelNumber
                    : tutorUser.DisplayName;
            }
        }

        string? meetingLinkUrl = booking.MeetingLink?.Url;
        if (string.IsNullOrWhiteSpace(meetingLinkUrl))
        {
            meetingLinkUrl = await _context.MeetingLinks
                .AsNoTracking()
                .Where(link => link.BookingId == booking.BookingId)
                .Select(link => link.Url)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new BookingNotificationContext(
            new BookingNotificationDetails(
                module?.ModuleCode ?? string.Empty,
                module?.ModuleName ?? string.Empty,
                booking.Location,
                booking.ScheduledStartTime,
                tutorName,
                booking.StudentName,
                meetingLinkUrl),
            tutorBcUserId);
    }

    private sealed record BookingNotificationContext(
        BookingNotificationDetails Details,
        int? TutorBcUserId);

    private sealed record ModuleNotificationDetails(
        string ModuleCode,
        string ModuleName);

    private sealed record UserNameDetails(
        string DisplayName,
        string PersonnelNumber);
}
