using BC_CampusLearn.Hubs;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BC_CampusLearn.Services.Notifications;

public sealed class UserNotificationSignalRInterceptor(
    IHubContext<SessionHub> hubContext,
    ILogger<UserNotificationSignalRInterceptor> logger)
    : SaveChangesInterceptor
{
    private IReadOnlyList<UserNotification> _pendingNotifications =
        Array.Empty<UserNotification>();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _pendingNotifications = eventData.Context?.ChangeTracker
            .Entries<UserNotification>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToArray() ?? Array.Empty<UserNotification>();

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserNotification> notifications =
            _pendingNotifications;
        _pendingNotifications = Array.Empty<UserNotification>();

        foreach (UserNotification notification in notifications)
        {
            try
            {
                await hubContext.Clients
                    .Group(SessionHub.GetUserGroupName(
                        notification.RecipientBcUserId))
                    .SendAsync(
                        "ReceiveUserNotification",
                        new
                        {
                            notification.UserNotificationId,
                            notification.Title,
                            notification.Message,
                            notification.CreatedAt,
                            OpenUrl =
                                $"/Notifications?notificationId={notification.UserNotificationId}"
                        },
                        CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Could not deliver notification {NotificationId} " +
                    "to user {RecipientBcUserId} in real time.",
                    notification.UserNotificationId,
                    notification.RecipientBcUserId);
            }
        }

        return await base.SavedChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingNotifications = Array.Empty<UserNotification>();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}
