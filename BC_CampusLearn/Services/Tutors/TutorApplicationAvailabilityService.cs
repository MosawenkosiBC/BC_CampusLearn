using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Tutors;

public sealed class TutorApplicationAvailabilityService(
    IServiceScopeFactory scopeFactory,
    ILogger<TutorApplicationAvailabilityService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CloseExpiredApplicationsAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CloseExpiredApplicationsAsync(stoppingToken);
        }
    }

    private async Task CloseExpiredApplicationsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            TutorApplicationSettings? settings = await context
                .TutorApplicationSettings
                .SingleOrDefaultAsync(cancellationToken);

            if (settings is null)
            {
                return;
            }

            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            bool availabilityChanged = settings.CloseExpiredApplications(
                utcNow.UtcDateTime);
            int notificationCount = await TutorApplicationNotificationDispatcher
                .AddDueNotificationsAsync(
                    context,
                    settings,
                    utcNow,
                    cancellationToken);

            if (availabilityChanged || context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
                if (availabilityChanged)
                {
                    logger.LogInformation(
                        "Closed tutor application windows whose closing dates have passed.");
                }
                if (notificationCount > 0)
                {
                    logger.LogInformation(
                        "Sent {NotificationCount} application opening notifications.",
                        notificationCount);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Tutor application availability check failed.");
        }
    }
}
