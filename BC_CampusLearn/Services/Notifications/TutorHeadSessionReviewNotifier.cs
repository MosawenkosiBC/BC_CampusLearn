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
