using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Notifications;

public sealed class CampusEventNotificationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<CampusEventNotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(
            TimeSpan.FromMinutes(1),
            timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            await QueuePublishedEventsAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task QueuePublishedEventsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            DateTimeOffset now = timeProvider.GetUtcNow();
            List<CampusEvent> events = await context.CampusEvents
                .Where(item =>
                    item.IsPublished &&
                    item.NotificationsQueuedAt == null &&
                    item.PublishAt <= now &&
                    item.EndsAt > now)
                .OrderBy(item => item.PublishAt)
                .Take(5)
                .ToListAsync(cancellationToken);
            if (events.Count == 0)
            {
                return;
            }

            List<int> studentIds = await context.BcUsers
                .AsNoTracking()
                .Where(user =>
                    user.Role == BcUserRole.Student ||
                    user.Role == BcUserRole.Tutor ||
                    user.Role == BcUserRole.SeniorTutor ||
                    user.Role == BcUserRole.HeadOfTutors)
                .Select(user => user.BcUserId)
                .ToListAsync(cancellationToken);
            foreach (CampusEvent campusEvent in events)
            {
                string time = NotificationEmailQueue.FormatTime(
                    campusEvent.StartsAt);
                foreach (int studentId in studentIds)
                {
                    context.UserNotifications.Add(new UserNotification
                    {
                        RecipientBcUserId = studentId,
                        Title = "New campus event",
                        Message = $"{campusEvent.Title} will take place on {time} SAST at {campusEvent.Location}.",
                        LinkUrl = "/#events",
                        CreatedAt = now
                    });
                }

                campusEvent.NotificationsQueuedAt = now;
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
                "Published campus event notifications could not be queued.");
        }
    }
}
