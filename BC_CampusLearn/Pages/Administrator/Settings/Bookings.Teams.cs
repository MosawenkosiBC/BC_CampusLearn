using BC_CampusLearn.Authentication;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Teams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public partial class BookingsModel
{
    public bool CanManageTeams => currentUserService.GetRequiredUser().Role == BcUserRole.SuperAdmin;

    public TeamsOrganizerInput TeamsOrganizer { get; set; } = new();
    [TempData]
    public string? TeamsOrganizerMessage { get; set; }
    public bool SharedOrganizerEnabled => teamsOptions?.Value.SharedOrganizerEnabled == true;
    public string TeamsOrganizerConfigurationSource { get; private set; } = "server environment";
    public string? TeamsOrganizerConfigurationError { get; private set; }

    public sealed class TeamsOrganizerInput
    {
        [Required, EmailAddress, StringLength(320)]
        public string Email { get; set; } = "mzalaconnect@belgiumcampus.ac.za";
        [Required, StringLength(64)]
        public string ObjectId { get; set; } = "";
    }

    public async Task<IActionResult> OnPostSaveTeamsOrganizerAsync(CancellationToken ct,
        [Bind(Prefix = "TeamsOrganizer")] TeamsOrganizerInput? organizer = null)
    {
        if (currentUserService?.GetRequiredUser().Role != BcUserRole.SuperAdmin) return Forbid();
        TeamsOrganizer = organizer ?? TeamsOrganizer;
        if (!ValidateTeamsOrganizer()) return await TeamsSettingsPageAsync(ct);
        var settings = await context.PlatformSettings.SingleOrDefaultAsync(s => s.PlatformSettingsId == PlatformSettings.SingletonId, ct);
        if (settings is null) { settings = new PlatformSettings(); context.PlatformSettings.Add(settings); }
        CurrentUser user = currentUserService.GetRequiredUser();
        auditService.Record("Bookings and sessions", "Teams organiser email", settings.TeamsSharedOrganizerEmail, TeamsOrganizer.Email, user);
        auditService.Record("Bookings and sessions", "Teams organiser Microsoft Object ID", settings.TeamsSharedOrganizerObjectId, TeamsOrganizer.ObjectId, user);
        settings.TeamsSharedOrganizerEmail = TeamsOrganizer.Email;
        settings.TeamsSharedOrganizerObjectId = TeamsOrganizer.ObjectId;
        settings.UpdatedByBcUserId = user.BcUserId;
        settings.UpdatedAt = timeProvider.GetUtcNow();
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("TeamsOrganizer.ObjectId", "The settings changed while saving. Reload and try again.");
            return await TeamsSettingsPageAsync(ct);
        }
        TeamsOrganizerMessage = "Teams meeting account saved. New pilot meetings will use this account; existing meetings keep their saved organiser.";
        if (WantsTeamsJson())
            return new JsonResult(new { succeeded = true, message = TeamsOrganizerMessage,
                source = "saved Super Admin settings (overriding server environment)",
                email = TeamsOrganizer.Email, objectId = TeamsOrganizer.ObjectId });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestTeamsOrganizerAsync(CancellationToken ct,
        [Bind(Prefix = "TeamsOrganizer")] TeamsOrganizerInput? organizer = null)
    {
        if (currentUserService?.GetRequiredUser().Role != BcUserRole.SuperAdmin) return Forbid();
        TeamsOrganizer = organizer ?? TeamsOrganizer;
        if (!ValidateTeamsOrganizer()) return await TeamsSettingsPageAsync(ct);
        TeamsOrganizerMessage = null;
        try
        {
            if (teamsCalendar is null)
                ModelState.AddModelError("TeamsOrganizer.ObjectId", "The Teams calendar service is unavailable on this server.");
            else if (!await teamsCalendar.SupportsTeamsAsync(TeamsOrganizer.ObjectId, ct))
                ModelState.AddModelError("TeamsOrganizer.ObjectId", "The account's calendar does not support Teams meetings. Ask the administrator to check its Exchange/Outlook calendar and Teams configuration. No settings were saved.");
            else TeamsOrganizerMessage = "Calendar connection succeeded. No meeting was created or settings saved. Verify co-organiser and recording permissions in a pilot session.";
        }
        catch (TeamsMeetingException ex) { ModelState.AddModelError("TeamsOrganizer.ObjectId", ex.Message); }
        return await TeamsSettingsPageAsync(ct);
    }

    private bool ValidateTeamsOrganizer()
    {
        TeamsOrganizer.Email = (TeamsOrganizer.Email ?? "").Trim();
        if (TeamsOrganizer.Email.Length > 320 || !System.Net.Mail.MailAddress.TryCreate(TeamsOrganizer.Email, out var address) || address.Address != TeamsOrganizer.Email)
            ModelState.AddModelError("TeamsOrganizer.Email", "Enter the meeting account email address.");
        if (!Guid.TryParse(TeamsOrganizer.ObjectId, out var id) || id == Guid.Empty)
            ModelState.AddModelError("TeamsOrganizer.ObjectId", "Enter the Microsoft Entra Object ID (a GUID), not a password or an application client ID.");
        else TeamsOrganizer.ObjectId = id.ToString("D");
        return ModelState.IsValid;
    }

    private async Task<IActionResult> TeamsSettingsPageAsync(CancellationToken ct)
    {
        if (WantsTeamsJson())
        {
            string message = ModelState.IsValid ? TeamsOrganizerMessage ?? "Request completed." :
                string.Join(" ", ModelState.Values.SelectMany(value => value.Errors)
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The account details could not be read. Check the email and Object ID." : error.ErrorMessage).Distinct());
            return new JsonResult(new { succeeded = ModelState.IsValid, message })
                { StatusCode = ModelState.IsValid ? 200 : 400 };
        }
        var input = TeamsOrganizer;
        await OnGetAsync(ct);
        TeamsOrganizer = input;
        return Page();
    }

    private bool WantsTeamsJson() => HttpContext?.Request.Headers.Accept
        .Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true) == true;

    private async Task LoadTeamsOrganizerAsync(CancellationToken ct)
    {
        if (!CanManageTeams) return;
        var account = await (teamsOrganizerSettings ?? new TeamsOrganizerSettingsStore(context,
            Options.Create(teamsOptions?.Value ?? new TeamsMeetingOptions()))).GetAsync(ct);
        TeamsOrganizer = new() { Email = account.Email, ObjectId = account.ObjectId ?? "" };
        TeamsOrganizerConfigurationSource = account.Source;
        TeamsOrganizerConfigurationError = account.ConfigurationError;
    }
}
