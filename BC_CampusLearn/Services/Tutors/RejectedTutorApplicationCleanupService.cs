using BC_CampusLearn.Data;

namespace BC_CampusLearn.Services.Tutors;

public sealed class RejectedTutorApplicationCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RejectedTutorApplicationCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CleanupInterval =
        TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await RemoveRejectedApplicationsAsync(stoppingToken);

        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RemoveRejectedApplicationsAsync(stoppingToken);
        }
    }

    private async Task RemoveRejectedApplicationsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope =
                scopeFactory.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            int removedCount = await TutorApplicationReview
                .RemoveRejectedApplicationsWhenCycleClosedAsync(
                    context,
                    cancellationToken);

            if (removedCount > 0)
            {
                logger.LogInformation(
                    "Removed {RejectedApplicationCount} rejected tutor " +
                    "applications after the application cycle closed.",
                    removedCount);
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
                "Rejected tutor application cleanup failed.");
        }
    }
}
