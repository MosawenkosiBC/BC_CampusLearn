using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Notifications;

public sealed class EmailNotificationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailNotificationOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailNotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(
            TimeSpan.FromMinutes(1),
            timeProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Value.Enabled)
            {
                await ProcessBatchAsync(stoppingToken);
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            IEmailNotificationClient client = scope.ServiceProvider
                .GetRequiredService<IEmailNotificationClient>();
            DateTimeOffset now = timeProvider.GetUtcNow();
            List<EmailNotificationOutbox> pending = await context
                .EmailNotificationOutbox
                .Where(item =>
                    item.SentAt == null &&
                    item.FailedAt == null &&
                    item.NextAttemptAt <= now)
                .OrderBy(item => item.CreatedAt)
                .Take(10)
                .ToListAsync(cancellationToken);

            foreach (EmailNotificationOutbox email in pending)
            {
                try
                {
                    await client.SendAsync(email, cancellationToken);
                    email.SentAt = timeProvider.GetUtcNow();
                    email.LastError = null;
                }
                catch (EmailDeliveryException exception)
                {
                    email.AttemptCount++;
                    email.LastError = exception.Message.Length <= 500
                        ? exception.Message
                        : exception.Message[..500];
                    if (exception.IsPermanent)
                    {
                        email.FailedAt = timeProvider.GetUtcNow();
                    }
                    else
                    {
                        int delayMinutes = Math.Min(
                            60,
                            Math.Max(5, 1 << Math.Min(email.AttemptCount, 6)));
                        email.NextAttemptAt = timeProvider.GetUtcNow()
                            .AddMinutes(delayMinutes);
                    }

                    logger.LogWarning(
                        "Email outbox item {EmailOutboxId} could not be sent. " +
                        "Attempt {AttemptCount}; permanent {IsPermanent}. {SafeError}",
                        email.EmailNotificationOutboxId,
                        email.AttemptCount,
                        exception.IsPermanent,
                        email.LastError);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException ||
                    !cancellationToken.IsCancellationRequested)
                {
                    email.AttemptCount++;
                    email.LastError =
                        "Email delivery failed before Microsoft accepted the request.";
                    int delayMinutes = Math.Min(
                        60,
                        Math.Max(5, 1 << Math.Min(email.AttemptCount, 6)));
                    email.NextAttemptAt = timeProvider.GetUtcNow()
                        .AddMinutes(delayMinutes);
                    logger.LogWarning(
                        exception,
                        "Email outbox item {EmailOutboxId} failed before " +
                        "delivery. Attempt {AttemptCount}.",
                        email.EmailNotificationOutboxId,
                        email.AttemptCount);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Email notification processing failed.");
        }
    }
}
