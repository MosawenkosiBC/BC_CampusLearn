namespace BC_CampusLearn.Services.Sessions;

public static class SessionSchedulingRules
{
    public static readonly TimeSpan MinimumStartSeparation =
        TimeSpan.FromMinutes(75);

    public static readonly TimeSpan EarlyStartWindow =
        TimeSpan.FromMinutes(5);

    public static readonly TimeSpan LateStartWindow =
        TimeSpan.FromMinutes(15);

    public static readonly TimeSpan SessionLength =
        TimeSpan.FromHours(1);

    public static readonly TimeSpan TriggeredCountdownLength =
        new(1, 5, 59);
}
