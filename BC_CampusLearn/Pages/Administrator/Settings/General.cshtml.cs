using System.ComponentModel.DataAnnotations;

using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class GeneralModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    SettingsAuditService auditService) : PageModel
{
    [BindProperty]
    public GeneralSettingsInput Input { get; set; } = new();

    [BindProperty]
    public string? MaintenanceReason { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public bool CanManageMaintenance { get; private set; }
    public bool MaintenanceModeEnabled { get; private set; }

    public static IReadOnlyList<string> DateTimeFormats { get; } =
    [
        "dd MMMM yyyy, HH:mm",
        "dd/MM/yyyy HH:mm",
        "yyyy-MM-dd HH:mm",
        "MMMM dd, yyyy h:mm tt"
    ];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        Input.SupportEmail = Input.SupportEmail?.Trim() ?? string.Empty;
        Input.Announcement = Input.Announcement?.Trim();

        if (!DateTimeFormats.Contains(Input.DateTimeFormat))
        {
            ModelState.AddModelError(
                "Input.DateTimeFormat",
                "Choose one of the available date and time formats.");
        }
        if (Input.IsAnnouncementEnabled &&
            string.IsNullOrWhiteSpace(Input.Announcement))
        {
            ModelState.AddModelError(
                "Input.Announcement",
                "Enter an announcement before enabling the banner.");
        }

        PlatformSettings settings = await GetSettingsAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            SetPageState(settings, currentUser);
            return Page();
        }

        bool changed = false;
        changed |= auditService.Record("General", "Support email address",
            settings.SupportEmail, Input.SupportEmail, currentUser);
        changed |= auditService.Record("General", "Academic year",
            settings.AcademicYear.ToString(), Input.AcademicYear.ToString(), currentUser);
        changed |= auditService.Record("General", "Date and time format",
            settings.DateTimeFormat, Input.DateTimeFormat, currentUser);
        changed |= auditService.Record("General", "Announcement",
            settings.Announcement, Input.Announcement, currentUser);
        changed |= auditService.Record("General", "Announcement banner",
            Enabled(settings.IsAnnouncementEnabled),
            Enabled(Input.IsAnnouncementEnabled), currentUser);

        settings.SupportEmail = Input.SupportEmail;
        settings.AcademicYear = Input.AcademicYear;
        settings.DateTimeFormat = Input.DateTimeFormat;
        settings.Announcement = Input.Announcement;
        settings.IsAnnouncementEnabled = Input.IsAnnouncementEnabled;
        Stamp(settings, currentUser);
        await context.SaveChangesAsync(cancellationToken);

        SuccessMessage = changed
            ? "General settings saved."
            : "General settings are already up to date.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMaintenanceAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        if (currentUser.Role != BcUserRole.Dev) return Forbid();

        MaintenanceReason = MaintenanceReason?.Trim();
        PlatformSettings settings = await GetSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(MaintenanceReason))
        {
            ModelState.AddModelError(
                nameof(MaintenanceReason),
                "Enter a reason for changing maintenance mode.");
            PopulateInput(settings);
            SetPageState(settings, currentUser);
            return Page();
        }

        auditService.Record("General", "Maintenance mode",
            Enabled(settings.IsMaintenanceModeEnabled), Enabled(enabled),
            currentUser, MaintenanceReason);
        settings.IsMaintenanceModeEnabled = enabled;
        Stamp(settings, currentUser);
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = enabled
            ? "Maintenance mode enabled."
            : "Maintenance mode disabled.";
        return RedirectToPage(null, null, "maintenance");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        PlatformSettings settings = await GetSettingsAsync(cancellationToken);
        PopulateInput(settings);
        SetPageState(settings, currentUser);
    }

    private void SetPageState(PlatformSettings settings, CurrentUser currentUser)
    {
        MaintenanceModeEnabled = settings.IsMaintenanceModeEnabled;
        CanManageMaintenance = currentUser.Role == BcUserRole.Dev;
    }

    private void PopulateInput(PlatformSettings settings) =>
        Input = new GeneralSettingsInput
        {
            SupportEmail = settings.SupportEmail,
            AcademicYear = settings.AcademicYear,
            DateTimeFormat = settings.DateTimeFormat,
            Announcement = settings.Announcement,
            IsAnnouncementEnabled = settings.IsAnnouncementEnabled
        };

    private async Task<PlatformSettings> GetSettingsAsync(
        CancellationToken cancellationToken) =>
        await context.PlatformSettings.SingleAsync(
            settings => settings.PlatformSettingsId == PlatformSettings.SingletonId,
            cancellationToken);

    private void Stamp(PlatformSettings settings, CurrentUser currentUser)
    {
        settings.UpdatedByBcUserId = currentUser.BcUserId;
        settings.UpdatedAt = timeProvider.GetUtcNow();
    }

    private static string Enabled(bool value) => value ? "Enabled" : "Disabled";

    public sealed class GeneralSettingsInput
    {
        [Required, EmailAddress, StringLength(320)]
        [Display(Name = "Support email address")]
        public string SupportEmail { get; set; } = string.Empty;

        [Range(2000, 2200)]
        [Display(Name = "Academic year")]
        public int AcademicYear { get; set; }

        [Required, StringLength(80)]
        [Display(Name = "Date and time format")]
        public string DateTimeFormat { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Announcement message")]
        public string? Announcement { get; set; }

        [Display(Name = "Show announcement across the platform")]
        public bool IsAnnouncementEnabled { get; set; }
    }
}
