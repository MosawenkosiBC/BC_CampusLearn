using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Tutors;

public static class TutorApplicationNotificationDispatcher
{
    public static async Task<int> AddDueNotificationsAsync(
        ApplicationDbContext context,
        TutorApplicationSettings settings,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        int notificationCount = 0;
        DateTime today = utcNow.UtcDateTime.Date;

        if (settings.IsOpen &&
            settings.OpenDate.HasValue &&
            settings.OpenDate.Value.Date <= today &&
            settings.CloseDate.HasValue &&
            settings.CloseDate.Value.Date >= today &&
            !settings.TutorApplicationsNotificationSentAt.HasValue)
        {
            List<int> studentIds = await context.BcUsers
                .AsNoTracking()
                .Where(user => user.Role == BcUserRole.Student)
                .Select(user => user.BcUserId)
                .ToListAsync(cancellationToken);

            context.UserNotifications.AddRange(studentIds.Select(studentId =>
                new UserNotification
                {
                    RecipientBcUserId = studentId,
                    Title = "🥳 Tutor applications are open",
                    Message = "Interested in helping fellow students? Apply " +
                        "to become a peer tutor.\n\nWhat you'll need:\n" +
                        "• 65%+ overall average or 75%+ in a subject\n" +
                        "• No failed subjects\n" +
                        "• 8-15 hours per month to tutor\n" +
                        $"Applications close: {settings.CloseDate:dd MMMM yyyy}",
                    LinkUrl = "/Tutors/TutorApplication",
                    CreatedAt = utcNow
                }));
            settings.TutorApplicationsNotificationSentAt = utcNow;
            notificationCount += studentIds.Count;
        }

        if (settings.SeniorTutorApplicationsOpen &&
            settings.SeniorTutorOpenDate.HasValue &&
            settings.SeniorTutorOpenDate.Value.Date <= today &&
            settings.SeniorTutorCloseDate.HasValue &&
            settings.SeniorTutorCloseDate.Value.Date >= today &&
            !settings.SeniorTutorApplicationsNotificationSentAt.HasValue)
        {
            List<int> tutorUserIds = await context.BcUsers
                .AsNoTracking()
                .Where(user =>
                    user.Role == BcUserRole.Tutor &&
                    user.Tutor != null &&
                    user.Tutor.IsActive &&
                    user.Tutor.Status == TutorStatus.Approved)
                .Select(user => user.BcUserId)
                .ToListAsync(cancellationToken);
            AdminNotificationDefinition notificationDefinition =
                AdminNotificationCatalog.Find(
                    AdminNotificationCatalog.SeniorApplicationsOpen)!;
            string notificationMessage = notificationDefinition.DefaultMessage
                .Replace(
                    "{closing date}",
                    $"{settings.SeniorTutorCloseDate:dd MMMM yyyy}",
                    StringComparison.Ordinal);

            context.UserNotifications.AddRange(tutorUserIds.Select(userId =>
                new UserNotification
                {
                    RecipientBcUserId = userId,
                    Title = notificationDefinition.Title,
                    Message = notificationMessage,
                    LinkUrl = "/Tutors/SeniorTutorApplications",
                    CreatedAt = utcNow
                }));
            settings.SeniorTutorApplicationsNotificationSentAt = utcNow;
            notificationCount += tutorUserIds.Count;
        }

        return notificationCount;
    }
}
