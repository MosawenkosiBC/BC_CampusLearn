using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Sessions;

public static class SessionLifecyclePolicy
{
    public static bool CanStart(
        DateTimeOffset scheduledStart,
        DateTimeOffset now)
    {
        return now >= scheduledStart.Subtract(
                SessionSchedulingRules.EarlyStartWindow) &&
            now < scheduledStart.Add(
                SessionSchedulingRules.LateStartWindow);
    }

    public static bool CanJoin(
        BookingStatus status,
        DateTimeOffset scheduledStart,
        DateTimeOffset now)
    {
        return status == BookingStatus.InProgress ||
            status == BookingStatus.Confirmed &&
            CanStart(scheduledStart, now);
    }

    public static BookingStatus? GetAutomaticTransition(
        BookingStatus currentStatus,
        DateTimeOffset scheduledStart,
        DateTimeOffset? expectedCompletion,
        DateTimeOffset now)
    {
        if (currentStatus == BookingStatus.Pending &&
            scheduledStart <= now)
        {
            return BookingStatus.Declined;
        }

        if (currentStatus == BookingStatus.Confirmed &&
            scheduledStart.Add(SessionSchedulingRules.LateStartWindow) <= now)
        {
            return BookingStatus.Cancelled;
        }

        if (currentStatus == BookingStatus.InProgress &&
            expectedCompletion.HasValue &&
            expectedCompletion.Value <= now)
        {
            return BookingStatus.Completed;
        }

        return null;
    }
}
