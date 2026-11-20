using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Notifications;

public sealed class SuperAdminReviewThresholdWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<EmailNotificationOptions> emailOptions,
    ILogger<SuperAdminReviewThresholdWorker> logger)
    : BackgroundService
{
    private const int Threshold = 5;
    private const string PendingFinalReviewUrl = "/Administrator/Admin/BookingsAndSessions?Stat=pending-final";
    public const string EventKeyPrefix = "superadmin-review-threshold:";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(
            TimeSpan.FromMinutes(1),
            timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckThresholdAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task CheckThresholdAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            int count = await context.Bookings
                .AsNoTracking()
                .Where(SessionReviewWorkflow.AwaitingFinalReview)
                .CountAsync(booking =>
                    booking.Status == BookingStatus.Completed &&
                    booking.StudentEvaluation != null &&
                    booking.TutorEvaluation != null,
                    cancellationToken);
            PlatformSettings settings = await context.PlatformSettings
                .SingleAsync(item =>
                    item.PlatformSettingsId == PlatformSettings.SingletonId,
                    cancellationToken);

            if (count <= Threshold)
            {
                // Keep the alert active while any part of the same review
                // backlog remains. Resetting at the threshold allowed the
                // queue to flap between five and six and send a new email on
                // every crossing.
                if (count == 0 && settings.IsSuperAdminReviewThresholdAlertActive)
                {
                    settings.IsSuperAdminReviewThresholdAlertActive = false;
                    await context.SaveChangesAsync(cancellationToken);
                }
                return;
            }

            if (settings.IsSuperAdminReviewThresholdAlertActive)
            {
                return;
            }

            settings.IsSuperAdminReviewThresholdAlertActive = true;
            settings.SuperAdminReviewThresholdSequence++;
            DateTimeOffset now = timeProvider.GetUtcNow();
            List<BcUser> superAdmins = await context.BcUsers
                .Where(user =>
                    user.Role == BcUserRole.SuperAdmin &&
                    user.IsAdministrativeAccessActive)
                .ToListAsync(cancellationToken);
            foreach (BcUser superAdmin in superAdmins)
            {
                context.UserNotifications.Add(new UserNotification
                {
                    RecipientBcUserId = superAdmin.BcUserId,
                    Title = "Sessions awaiting review",
                    Message = $"{count} sessions are awaiting Super Admin review.",
                    LinkUrl = PendingFinalReviewUrl,
                    CreatedAt = now
                });
                if (emailOptions.Value.SuperAdminReviewThresholdEmailEnabled)
                {
                    NotificationEmailQueue.Add(
                        context,
                        $"{EventKeyPrefix}{settings.SuperAdminReviewThresholdSequence}:{superAdmin.BcUserId}",
                        superAdmin.Email,
                        superAdmin.DisplayName,
                        "Sessions require Super Admin review",
                        $"{NotificationEmailQueue.Greeting(superAdmin.DisplayName)}" +
                        $"{Environment.NewLine}{Environment.NewLine}" +
                        $"There are currently {count} sessions awaiting Super Admin review.",
                        PendingFinalReviewUrl,
                        now);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "The Super Admin review threshold could not be evaluated.");
        }
    }
}
