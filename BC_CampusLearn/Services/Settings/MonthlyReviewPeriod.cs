namespace BC_CampusLearn.Services.Settings;

public static class MonthlyReviewPeriod
{
    public static ReviewPeriodWindow Resolve(
        DateOnly configuredDeadline,
        bool useLastDayOfMonth,
        DateOnly today)
    {
        DateOnly activeDeadline = configuredDeadline;
        int elapsedPeriods = 0;
        while (today > activeDeadline)
        {
            elapsedPeriods++;
            activeDeadline = Occurrence(
                configuredDeadline,
                elapsedPeriods,
                useLastDayOfMonth);
        }

        DateOnly previousDeadline = Occurrence(
            configuredDeadline,
            elapsedPeriods - 1,
            useLastDayOfMonth);
        return new ReviewPeriodWindow(
            previousDeadline.AddDays(1),
            activeDeadline,
            activeDeadline);
    }

    public static DateOnly NormalizeDeadline(
        DateOnly selectedDeadline,
        bool useLastDayOfMonth) => useLastDayOfMonth
            ? LastDayOfMonth(selectedDeadline)
            : selectedDeadline;

    public static DateOnly PreviousOccurrence(
        DateOnly deadline,
        bool useLastDayOfMonth) => Occurrence(
            deadline,
            -1,
            useLastDayOfMonth);

    private static DateOnly Occurrence(
        DateOnly deadline,
        int months,
        bool useLastDayOfMonth)
    {
        DateOnly occurrence = deadline.AddMonths(months);
        return useLastDayOfMonth
            ? LastDayOfMonth(occurrence)
            : occurrence;
    }

    private static DateOnly LastDayOfMonth(DateOnly date) => new DateOnly(
        date.Year,
        date.Month,
        1).AddMonths(1).AddDays(-1);
}

public sealed record ReviewPeriodWindow(
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly Deadline);
