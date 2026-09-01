namespace BC_CampusLearn.Services.Sessions;

public record SessionLifecycleResult(bool Succeeded, string? ErrorMessage)
{
    public static SessionLifecycleResult Success() => new(true, null);

    public static SessionLifecycleResult Failure(string message) =>
        new(false, message);
}
