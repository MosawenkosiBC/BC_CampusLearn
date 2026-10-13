using System.Net.Mail;
using System.Security.Cryptography;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public interface IBookingTeamsMeetingService
{
    Task<SessionLifecycleResult> CreateAsync(int tutorBcUserId, int bookingId, CancellationToken cancellationToken = default);
    Task<SessionLifecycleResult> CancelAsync(int bookingId, CancellationToken cancellationToken = default);
    Task ProcessPendingCancellationsAsync(CancellationToken cancellationToken = default);
}

public sealed class BookingTeamsMeetingService(
    ApplicationDbContext context,
    ITeamsCalendarClient calendar,
    IEntraIdentityProtector identities,
    IOptions<TeamsMeetingOptions> options,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<BookingTeamsMeetingService> logger) : IBookingTeamsMeetingService
{
    public async Task<SessionLifecycleResult> CreateAsync(
        int tutorBcUserId, int bookingId, CancellationToken cancellationToken = default)
    {
        if (!options.Value.AllowsTutor(tutorBcUserId))
            return SessionLifecycleResult.Failure("Teams meeting creation is not enabled for your account.");

        if (environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:UseDevelopmentAuthentication"))
            return SessionLifecycleResult.Failure("Sign in with your institutional Microsoft account to create a Teams meeting. Development sign-in cannot create meetings.");

        Tutor? tutor = await context.Tutors.Include(item => item.BcUser)
            .SingleOrDefaultAsync(item => item.BcUserId == tutorBcUserId &&
                item.Status == TutorStatus.Approved && item.IsActive, cancellationToken);
        if (tutor is null)
            return SessionLifecycleResult.Failure("Only an active tutor can create a meeting for their booking.");

        Booking? booking = await context.Bookings
            .Include(item => item.ProgrammeModule)
            .Include(item => item.StudyArea)
            .Include(item => item.StudentBcUser)
            .Include(item => item.MeetingLink)
            .SingleOrDefaultAsync(item => item.BookingId == bookingId && item.TutorId == tutor.TutorId, cancellationToken);
        if (booking is null)
            return SessionLifecycleResult.Failure("The session could not be found.");
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed) ||
            booking.ScheduledStartTime <= timeProvider.GetUtcNow())
            return SessionLifecycleResult.Failure("Only an upcoming pending or confirmed booking can have a meeting created.");
        if (!string.IsNullOrWhiteSpace(booking.MeetingLink?.Url))
            return booking.MeetingLink.TeamsEventId is not null && !booking.MeetingLink.TeamsManualFallbackAllowed
                ? SessionLifecycleResult.Success()
                : SessionLifecycleResult.Failure("This booking already has a manual meeting link. Mzala Connect will not replace it.");

        BcUser user = tutor.BcUser;
        if (string.IsNullOrWhiteSpace(user.EncryptedEntraTenantId) || string.IsNullOrWhiteSpace(user.EncryptedEntraObjectId))
            return SessionLifecycleResult.Failure("Your Microsoft account is not linked. Sign out and sign in with your institutional account before retrying.");

        EntraIdentity identity;
        try
        {
            identity = identities.Unprotect(user.EncryptedEntraTenantId, user.EncryptedEntraObjectId);
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException or FormatException)
        {
            logger.LogWarning("Stored Microsoft identity could not be read for BC user {BcUserId}. Failure type: {FailureType}.",
                tutorBcUserId, exception.GetType().Name);
            return SessionLifecycleResult.Failure("Your stored Microsoft sign-in details could not be read. Sign out and sign in with your institutional account, then retry. If this continues, ask the administrator to check persistent Data Protection key storage.");
        }

        if (!Guid.TryParse(configuration["AzureAd:TenantId"], out Guid tenantId) ||
            tenantId != Guid.Parse(identity.TenantId))
            return SessionLifecycleResult.Failure("Your Microsoft account does not belong to the configured institution.");

        string identityHash = identities.CreateLookupHash(identity.TenantId, identity.ObjectId);
        if (booking.MeetingLink?.TeamsOrganizerIdentityHash is string existingHash && existingHash != identityHash)
            return SessionLifecycleResult.Failure("This meeting request belongs to a different Microsoft account. Please contact the administrator.");

        string email = (booking.StudentBcUser?.Email ?? booking.StudentEmail)?.Trim() ?? string.Empty;
        if (!MailAddress.TryCreate(email, out MailAddress? address) || address.Address != email)
            return SessionLifecycleResult.Failure("The student needs a valid email address before an invitation can be sent.");
        if (booking.Duration != SessionDuration.OneHour)
            return SessionLifecycleResult.Failure("The booking duration is not supported.");

        try
        {
            if (booking.MeetingLink?.TeamsEventId is null &&
                !await calendar.SupportsTeamsAsync(identity.ObjectId, cancellationToken))
                return SessionLifecycleResult.Failure("Your Outlook calendar does not support Teams meetings. Please contact the administrator.");

            // Persist the request ID before contacting Graph. Retries reuse its transactionId,
            // including when Microsoft succeeds but the response or subsequent database write fails.
            MeetingLink link = booking.MeetingLink ??= new MeetingLink { BookingId = bookingId };
            if (link.TeamsCreationRequestId is null || link.TeamsManualFallbackAllowed)
            {
                link.TeamsCreationRequestId ??= Guid.NewGuid();
                link.TeamsOrganizerIdentityHash = identityHash;
                link.TeamsManualFallbackAllowed = false;
                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Another request may have reserved this booking. Do not send a second
                    // transaction ID to Microsoft; retry from the saved reservation instead.
                    logger.LogWarning("Could not reserve Teams creation for booking {BookingId}.", bookingId);
                    return SessionLifecycleResult.Failure("The meeting request could not be reserved. Please reload the page and retry.");
                }
            }

            TeamsCalendarEvent meeting = link.TeamsEventId is string eventId
                ? await calendar.GetAsync(identity.ObjectId, eventId, cancellationToken)
                : await calendar.CreateAsync(new TeamsCalendarRequest(
                    identity.ObjectId,
                    link.TeamsCreationRequestId.Value,
                    $"Mzala Connect tutoring session: {booking.ProgrammeModule.ModuleName}",
                    booking.ScheduledStartTime,
                    booking.ScheduledStartTime.AddHours((int)booking.Duration),
                    email,
                    booking.StudentName), cancellationToken);

            // Retain the event even if Microsoft is still preparing its online meeting link.
            link.TeamsEventId = meeting.Id;
            if (!string.IsNullOrWhiteSpace(meeting.JoinUrl))
                link.Url = meeting.JoinUrl;
            link.TeamsManualFallbackAllowed = string.IsNullOrWhiteSpace(link.Url);
            await context.Entry(booking).ReloadAsync(cancellationToken);
            bool bookingCancelled = booking.Status is BookingStatus.Cancelled or BookingStatus.Declined;
            if (bookingCancelled)
                link.TeamsCancellationRequestedAt ??= timeProvider.GetUtcNow();
            await context.SaveChangesAsync(cancellationToken);

            if (bookingCancelled)
            {
                await CancelAsync(bookingId, cancellationToken);
                return SessionLifecycleResult.Failure("The booking was cancelled while the meeting was being created. Its Teams cancellation has been requested. Reload the page.");
            }

            return string.IsNullOrWhiteSpace(link.Url)
                ? SessionLifecycleResult.Failure("The calendar invitation was created, but its Teams link is still being prepared. Retry shortly to retrieve the same meeting.")
                : SessionLifecycleResult.Success();
        }
        catch (TeamsMeetingException exception)
        {
            logger.LogWarning("Microsoft could not complete Teams creation for booking {BookingId}.", bookingId);
            if (booking.MeetingLink is { TeamsCreationRequestId: not null } failedLink)
            {
                failedLink.TeamsManualFallbackAllowed = true;
                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    return SessionLifecycleResult.Failure("The meeting request changed while it was being processed. Reload the page before retrying or entering a manual link.");
                }
            }
            return SessionLifecycleResult.Failure(exception.Message);
        }
        catch (DbUpdateException)
        {
            logger.LogWarning("Could not save Teams event details for booking {BookingId}.", bookingId);
            return SessionLifecycleResult.Failure("The meeting details could not be saved. Please reload and retry; CampusLearn will reuse the same meeting request.");
        }
    }

    public async Task<SessionLifecycleResult> CancelAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        // This method is internal to the booking lifecycle/background worker.
        // Authorization occurs before the booking is changed; only a persisted
        // cancelled/declined booking is eligible for remote cancellation.
        var booking = await context.Bookings.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new { item.TutorId, item.Status, item.ScheduledStartTime }).SingleOrDefaultAsync(cancellationToken);
        if (booking is null || booking.Status is not (BookingStatus.Cancelled or BookingStatus.Declined))
            return SessionLifecycleResult.Failure("Only cancelled or declined bookings can cancel their Teams meeting.");

        MeetingLink? link = await context.MeetingLinks.SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (link is null || link.TeamsCreationRequestId is null) return SessionLifecycleResult.Success();
        await context.Entry(link).ReloadAsync(cancellationToken);
        if (link.TeamsCancelledAt is not null) return SessionLifecycleResult.Success();
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (link.TeamsCancellationLastAttemptAt > now.AddMinutes(-1))
            return SessionLifecycleResult.Failure("Meeting cancellation is already pending.");
        link.TeamsCancellationRequestedAt ??= now;
        link.TeamsCancellationLastAttemptAt = now;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return SessionLifecycleResult.Failure("Meeting cancellation is already being processed.");
        }
        catch (DbUpdateException)
        {
            return SessionLifecycleResult.Failure("Meeting cancellation could not be claimed yet and remains queued.");
        }

        BcUser? organizer = await context.Tutors.AsNoTracking().Where(item => item.TutorId == booking.TutorId)
            .Select(item => item.BcUser).SingleOrDefaultAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(organizer?.EncryptedEntraTenantId) || string.IsNullOrWhiteSpace(organizer.EncryptedEntraObjectId))
                throw new TeamsMeetingException("The meeting organizer's Microsoft identity is unavailable.");
            EntraIdentity identity = identities.Unprotect(organizer.EncryptedEntraTenantId, organizer.EncryptedEntraObjectId);
            if (!Guid.TryParse(configuration["AzureAd:TenantId"], out Guid tenant) || tenant != Guid.Parse(identity.TenantId) ||
                identities.CreateLookupHash(identity.TenantId, identity.ObjectId) != link.TeamsOrganizerIdentityHash)
                throw new TeamsMeetingException("The saved meeting organizer could not be verified.");

            if (link.TeamsEventId is null)
            {
                link.TeamsEventId = await calendar.FindByTransactionAsync(identity.ObjectId, link.TeamsCreationRequestId.Value, booking.ScheduledStartTime, cancellationToken);
                if (link.TeamsEventId is null)
                    throw new TeamsMeetingException("The meeting request has not yet been located in Microsoft's calendar.");
                await context.SaveChangesAsync(cancellationToken);
            }
            await calendar.CancelAsync(identity.ObjectId, link.TeamsEventId, cancellationToken);
            link.TeamsCancelledAt = timeProvider.GetUtcNow();
            await context.SaveChangesAsync(cancellationToken);
            return SessionLifecycleResult.Success();
        }
        catch (Exception exception) when (exception is TeamsMeetingException or CryptographicException or InvalidOperationException or FormatException or DbUpdateException)
        {
            logger.LogWarning("Teams meeting cancellation remains pending for booking {BookingId}. Failure type: {FailureType}.", bookingId, exception.GetType().Name);
            return SessionLifecycleResult.Failure("Teams meeting cancellation is pending and will retry automatically.");
        }
    }

    public async Task ProcessPendingCancellationsAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = timeProvider.GetUtcNow().AddMinutes(-1);
        List<int> bookings = await context.MeetingLinks.AsNoTracking()
            .Where(link => link.TeamsCancellationRequestedAt != null && link.TeamsCancelledAt == null &&
                link.TeamsCreationRequestId != null && (link.TeamsCancellationLastAttemptAt == null || link.TeamsCancellationLastAttemptAt <= cutoff) &&
                (link.Booking.Status == BookingStatus.Cancelled || link.Booking.Status == BookingStatus.Declined))
            .OrderBy(link => link.TeamsCancellationLastAttemptAt ?? link.TeamsCancellationRequestedAt)
            .Select(link => link.BookingId).Take(5).ToListAsync(cancellationToken);
        foreach (int bookingId in bookings)
        {
            await CancelAsync(bookingId, cancellationToken);
            context.ChangeTracker.Clear();
        }
    }
}
