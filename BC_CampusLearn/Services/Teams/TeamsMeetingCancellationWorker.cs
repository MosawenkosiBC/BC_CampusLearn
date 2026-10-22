namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsMeetingCancellationWorker(
    IServiceScopeFactory scopes, ILogger<TeamsMeetingCancellationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IBookingTeamsMeetingService>()
                    .ProcessPendingCancellationsAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<InterviewTeamsMeetingService>()
                    .ProcessCancellationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogWarning("Teams cancellation retry pass failed. Failure type: {FailureType}.", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
