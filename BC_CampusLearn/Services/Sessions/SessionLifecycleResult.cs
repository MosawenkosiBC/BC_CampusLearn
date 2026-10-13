namespace BC_CampusLearn.Services.Sessions;

public record SessionLifecycleResult(bool Succeeded, string? ErrorMessage, string? WarningMessage = null)
{
    public static SessionLifecycleResult Success(string? warningMessage = null) => new(true, null, warningMessage);

    public static SessionLifecycleResult Failure(string message) =>
        new(false, message);
}
