using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Tutors;

public static class TutorApplicationClosingReminderDispatcher
{
    private static readonly int[] ReminderDays = [3, 1];

    public static async Task<int> AddDueEmailsAsync(
        ApplicationDbContext context,
        TutorApplicationSettings settings,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        if (!settings.IsOpen || !settings.CloseDate.HasValue)
        {
            return 0;
        }

        DateTime today = utcNow.ToOffset(TimeSpan.FromHours(2)).Date;
        DateTime closingDate = settings.CloseDate.Value.Date;
        if (settings.OpenDate.HasValue && settings.OpenDate.Value.Date > today)
        {
            return 0;
        }

        int daysRemaining = (closingDate - today).Days;
        if (!ReminderDays.Contains(daysRemaining))
        {
            return 0;
        }

        List<BcUser> superAdmins = await context.BcUsers
            .AsNoTracking()
            .Where(user =>
                user.Role == BcUserRole.SuperAdmin &&
                user.IsAdministrativeAccessActive)
            .ToListAsync(cancellationToken);

        string dayLabel = daysRemaining == 1 ? "day" : "days";
        string closingDateText = closingDate.ToString("dd MMMM yyyy");
        string cycleStart = settings.OpenDate?.ToString("yyyyMMdd") ?? "unscheduled";
        foreach (BcUser superAdmin in superAdmins)
        {
            NotificationEmailQueue.Add(
                context,
                $"tutor-application-closing:{cycleStart}:{closingDate:yyyyMMdd}:{daysRemaining}:{superAdmin.BcUserId}",
                superAdmin.Email,
                superAdmin.DisplayName,
                $"Tutor applications close in {daysRemaining} {dayLabel}",
                $"{NotificationEmailQueue.Greeting(superAdmin.DisplayName)}" +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"The current tutor application cycle closes in {daysRemaining} {dayLabel}, " +
                $"on {closingDateText}. You can review the application pipeline in Mzala Connect.",
                "/Administrator/Admin/Applications",
                utcNow);
        }

        return superAdmins.Count;
    }
}
