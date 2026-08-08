using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Models.ViewModels;

public class TutorNextSessionViewModel
{
    public int BookingId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public DateTimeOffset ScheduledStartTime { get; set; }

    public SessionDuration Duration { get; set; }

    public DateTimeOffset SessionEnd =>
        ScheduledStartTime.AddHours((int)Duration);

    public string CountdownLabel =>
        FormatCountdown(
            ScheduledStartTime - DateTimeOffset.UtcNow);

    private static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "Starting now";
        }

        int totalMinutes =
            Math.Max(1, (int)Math.Ceiling(
                remaining.TotalMinutes));

        if (totalMinutes < 60)
        {
            return $"{totalMinutes} " +
                $"{(totalMinutes == 1 ? "min" : "mins")} left";
        }

        int totalHours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        if (totalHours < 24)
        {
            string hourLabel =
                $"{totalHours} " +
                $"{(totalHours == 1 ? "hr" : "hrs")}";

            return minutes == 0
                ? $"{hourLabel} left"
                : $"{hourLabel} {minutes} " +
                    $"{(minutes == 1 ? "min" : "mins")} left";
        }

        int days = totalHours / 24;
        int hours = totalHours % 24;
        string dayLabel =
            $"{days} {(days == 1 ? "day" : "days")}";

        return hours == 0
            ? $"{dayLabel} left"
            : $"{dayLabel} {hours} " +
                $"{(hours == 1 ? "hr" : "hrs")} left";
    }
}
