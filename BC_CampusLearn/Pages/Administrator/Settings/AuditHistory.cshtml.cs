using BC_CampusLearn.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class AuditHistoryModel(ApplicationDbContext context) : PageModel
{
    public IReadOnlyList<AuditRow> AuditRows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        AuditRows = await context.SettingAuditLogs.AsNoTracking()
            .OrderByDescending(log => log.ChangedAt)
            .Take(100)
            .Select(log => new AuditRow(
                log.SettingAuditLogId,
                log.Category,
                log.SettingName,
                log.PreviousValue,
                log.NewValue,
                log.ChangedBy.DisplayName,
                log.ChangedAt,
                log.Reason))
            .ToListAsync(cancellationToken);
    }

    public sealed record AuditRow(
        long Id,
        string Category,
        string SettingName,
        string? PreviousValue,
        string? NewValue,
        string Administrator,
        DateTimeOffset ChangedAt,
        string? Reason);
}
