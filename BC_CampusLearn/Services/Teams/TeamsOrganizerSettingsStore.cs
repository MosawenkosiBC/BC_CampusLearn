using System.Net.Mail;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public sealed record TeamsOrganizerAccount(string Email, string? ObjectId)
{
    public string Source { get; init; } = "server environment";

    public string? ConfigurationError =>
        !Guid.TryParse(ObjectId, out Guid id) || id == Guid.Empty
            ? $"The shared Teams meeting account Object ID is missing or invalid in {Source}. Enter the account's Microsoft Entra Object ID (a GUID) in Teams meeting account and save it."
            : !MailAddress.TryCreate(Email.Trim(), out var address) || address.Address != Email.Trim()
                ? $"The shared Teams meeting account email is missing or invalid in {Source}. Enter its email in Teams meeting account and save it."
                : null;

    public string ValidatedObjectId()
    {
        if (ConfigurationError is { } error) throw new TeamsMeetingException(error);
        return Guid.Parse(ObjectId!).ToString("D");
    }
}

public sealed class TeamsOrganizerSettingsStore(ApplicationDbContext context, IOptions<TeamsMeetingOptions> options)
{
    public async Task<TeamsOrganizerAccount> GetAsync(CancellationToken ct)
    {
        var saved = await context.PlatformSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.PlatformSettingsId == PlatformSettings.SingletonId, ct);
        // Treat a saved account as a pair; never mix an old email with a new ID.
        bool hasSavedAccount = !string.IsNullOrWhiteSpace(saved?.TeamsSharedOrganizerObjectId) ||
            !string.IsNullOrWhiteSpace(saved?.TeamsSharedOrganizerEmail);
        return hasSavedAccount
            ? new(saved!.TeamsSharedOrganizerEmail?.Trim() ?? "", saved.TeamsSharedOrganizerObjectId?.Trim())
                { Source = "saved Super Admin settings (overriding server environment)" }
            : new(options.Value.SharedOrganizerEmail.Trim(), options.Value.SharedOrganizerObjectId?.Trim());
    }
}
