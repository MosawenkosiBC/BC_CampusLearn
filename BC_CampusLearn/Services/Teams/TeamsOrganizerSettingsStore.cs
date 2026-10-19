using System.Net.Mail;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public sealed record TeamsOrganizerAccount(string Email, string? ObjectId)
{
    public string ValidatedObjectId()
    {
        if (!Guid.TryParse(ObjectId, out Guid id) || id == Guid.Empty ||
            !MailAddress.TryCreate(Email, out var address) || address.Address != Email)
            throw new TeamsMeetingException("The shared Teams meeting account is not configured. Set its Microsoft Entra Object ID and email in Super Admin → Bookings and Sessions, or in the server environment.");
        return id.ToString("D");
    }
}

public sealed class TeamsOrganizerSettingsStore(ApplicationDbContext context, IOptions<TeamsMeetingOptions> options)
{
    public async Task<TeamsOrganizerAccount> GetAsync(CancellationToken ct)
    {
        var saved = await context.PlatformSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.PlatformSettingsId == PlatformSettings.SingletonId, ct);
        // Treat a saved account as a pair; never mix an old email with a new ID.
        return saved?.TeamsSharedOrganizerObjectId is not null
            ? new(saved.TeamsSharedOrganizerEmail ?? "", saved.TeamsSharedOrganizerObjectId)
            : new(options.Value.SharedOrganizerEmail, options.Value.SharedOrganizerObjectId);
    }
}
