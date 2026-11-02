using BC_CampusLearn.Data;
using BC_CampusLearn.Hubs;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Notifications;

public interface ITutorHeadSessionReviewNotifier
{
    Task NotifyIfAvailableAsync(
        int bookingId,
        CancellationToken cancellationToken);
}

public sealed class TutorHeadSessionReviewNotifier(
    ApplicationDbContext context,
    IHubContext<SessionHub> hubContext,
    TimeProvider timeProvider,
    ILogger<TutorHeadSessionReviewNotifier> logger)
    : ITutorHeadSessionReviewNotifier
{
    public async Task NotifyIfAvailableAsync(
        int bookingId,
        CancellationToken cancellationToken)
    {
        Booking? booking = context.Bookings.Local.FirstOrDefault(item =>
            item.BookingId == bookingId) ?? await context.Bookings
            .SingleOrDefaultAsync(item => item.BookingId == bookingId,
                cancellationToken);
        if (booking is null ||
            booking.Status != BookingStatus.Completed ||
            booking.TutorHeadReviewAvailableAt.HasValue)
        {
            return;
        }

        bool hasBothReviews = await context.StudentEvaluations
            .AnyAsync(review => review.BookingId == bookingId,
                cancellationToken) &&
            await context.TutorStudentEvaluations
                .AnyAsync(review => review.BookingId == bookingId,
                    cancellationToken);
        if (!hasBothReviews)
        {
            return;
        }

        booking.TutorHeadReviewAvailableAt = timeProvider.GetUtcNow();
        DateTimeOffset availableAt = booking.TutorHeadReviewAvailableAt.Value;
        var details = await context.Bookings
            .AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new
            {
                item.StudentName,
                item.ScheduledStartTime,
                item.Location,
                item.ProgrammeModule.ModuleCode,
                item.ProgrammeModule.ModuleName,
                TutorName = item.TutorCourseModule.Tutor.BcUser.DisplayName
            })
            .SingleAsync(cancellationToken);
        List<BcUser> tutorHeads = await context.BcUsers
            .Where(user =>
                user.Role == BcUserRole.HeadOfTutors &&
                user.IsAdministrativeAccessActive)
            .ToListAsync(cancellationToken);
        string module = NotificationEmailQueue.FormatModule(
            details.ModuleCode,
            details.ModuleName);
        foreach (BcUser tutorHead in tutorHeads)
        {
            context.UserNotifications.Add(new UserNotification
            {
                RecipientBcUserId = tutorHead.BcUserId,
                Title = "Session ready for review",
                Message = $"The student and tutor reviews for {module} are complete. The session is ready for your review.",
                LinkUrl = $"/TutorHead/SessionDetails/{bookingId}",
                CreatedAt = availableAt
            });
            NotificationEmailQueue.Add(
                context,
                $"booking:{bookingId}:ready-for-tutor-head:{tutorHead.BcUserId}",
                tutorHead.Email,
                tutorHead.DisplayName,
                "A session is ready for your review",
                $"{NotificationEmailQueue.Greeting(tutorHead.DisplayName)}" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "The student and tutor have completed their reviews for this session." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"Tutor: {details.TutorName}{Environment.NewLine}" +
                $"Student: {details.StudentName}{Environment.NewLine}" +
                NotificationEmailQueue.SessionDetails(
                    details.ModuleCode,
                    details.ModuleName,
                    details.ScheduledStartTime,
                    details.Location),
                $"/TutorHead/SessionDetails/{bookingId}",
                availableAt);
        }
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            await hubContext.Clients
                .Group(SessionHub.TutorHeadsGroupName)
                .SendAsync(
                    "SessionReviewAvailable",
                    new
                    {
                        booking.BookingId,
                        booking.TutorHeadReviewAvailableAt
                    },
                    CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not notify Tutor Heads that booking {BookingId} " +
                "is ready for review.",
                bookingId);
        }
    }
}
