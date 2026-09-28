using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages;

public class MaintenanceModel(ApplicationDbContext context) : PageModel
{
    public string SupportEmail { get; private set; } =
        "tutors@belgiumcampus.ac.za";

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        var settings = await context.PlatformSettings.AsNoTracking()
            .Where(item => item.PlatformSettingsId == PlatformSettings.SingletonId)
            .Select(item => new
            {
                item.IsMaintenanceModeEnabled,
                item.SupportEmail
            })
            .SingleAsync(cancellationToken);

        if (!settings.IsMaintenanceModeEnabled)
        {
            return RedirectToPage("/Index");
        }

        SupportEmail = settings.SupportEmail;
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        Response.Headers.RetryAfter = "900";
        return Page();
    }
}
