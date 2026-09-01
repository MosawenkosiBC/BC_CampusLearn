using BC_CampusLearn.Data;
using BC_CampusLearn.Services.Sessions;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Availability;

public class ExpiredAvailabilityCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval =
        TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredAvailabilityCleanupService> _logger;

    public ExpiredAvailabilityCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredAvailabilityCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await RemoveExpiredSlotsAsync(stoppingToken);

        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RemoveExpiredSlotsAsync(stoppingToken);
        }
    }

    private async Task RemoveExpiredSlotsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope =
                _scopeFactory.CreateAsyncScope();

            ApplicationDbContext context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            ISessionLifecycleService lifecycleService =
                scope.ServiceProvider
                    .GetRequiredService<ISessionLifecycleService>();

            await lifecycleService.ProcessDueTransitionsAsync(
                cancellationToken);

            int removedCount =
                await context.TutorAvailabilities
                    .Where(slot => slot.AvailableTime <= now)
                    .ExecuteDeleteAsync(cancellationToken);

            if (removedCount > 0)
            {
                _logger.LogInformation(
                    "Removed {ExpiredAvailabilityCount} " +
                    "expired unbooked tutor availability slots.",
                    removedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Expired tutor availability cleanup failed.");
        }
    }
}
