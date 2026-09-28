using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Settings;

public class SettingsAuditService(
    ApplicationDbContext context,
    TimeProvider timeProvider)
{
    public bool Record(
        string category,
        string settingName,
        string? previousValue,
        string? newValue,
        CurrentUser changedBy,
        string? reason = null)
    {
        if (string.Equals(
            previousValue ?? string.Empty,
            newValue ?? string.Empty,
            StringComparison.Ordinal))
        {
            return false;
        }

        context.SettingAuditLogs.Add(new SettingAuditLog
        {
            Category = category,
            SettingName = settingName,
            PreviousValue = Truncate(previousValue),
            NewValue = Truncate(newValue),
            ChangedByBcUserId = changedBy.BcUserId,
            ChangedAt = timeProvider.GetUtcNow(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
        return true;
    }

    private static string? Truncate(string? value) =>
        value is null || value.Length <= 4000
            ? value
            : value[..3997] + "...";
}
