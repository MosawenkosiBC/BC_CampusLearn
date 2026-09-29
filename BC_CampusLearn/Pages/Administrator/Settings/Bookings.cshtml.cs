using System.ComponentModel.DataAnnotations;

using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class BookingsModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    SettingsAuditService auditService) : PageModel
{
    [BindProperty]
    public BookingTermsInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Input.Terms = await context.PlatformSettings.AsNoTracking()
            .Where(settings => settings.PlatformSettingsId == PlatformSettings.SingletonId)
            .Select(settings => settings.BookingTermsAndConditions)
            .SingleAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        Input.Terms = NormalizeLines(Input.Terms ?? string.Empty);
        if (Input.Terms.Length is < 20 or > 8000)
        {
            ModelState.AddModelError(
                "Input.Terms",
                "Booking terms must contain between 20 and 8,000 characters.");
        }
        if (!ModelState.IsValid) return Page();

        CurrentUser currentUser = currentUserService.GetRequiredUser();
        PlatformSettings settings = await context.PlatformSettings.SingleAsync(
            item => item.PlatformSettingsId == PlatformSettings.SingletonId,
            cancellationToken);
        bool changed = auditService.Record(
            "Bookings and sessions",
            "Booking terms and conditions",
            settings.BookingTermsAndConditions,
            Input.Terms,
            currentUser);
        settings.BookingTermsAndConditions = Input.Terms;
        settings.UpdatedByBcUserId = currentUser.BcUserId;
        settings.UpdatedAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);

        SuccessMessage = changed
            ? "Booking terms and conditions saved."
            : "Booking terms are already up to date.";
        return RedirectToPage();
    }

    private static string NormalizeLines(string value) =>
        string.Join('\n', value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries));

    public sealed class BookingTermsInput
    {
        [Required, StringLength(8000, MinimumLength = 20)]
        [Display(Name = "Booking terms and conditions")]
        public string Terms { get; set; } = string.Empty;
    }
}
