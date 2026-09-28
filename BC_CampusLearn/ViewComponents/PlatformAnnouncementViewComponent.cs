using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.ViewComponents;

public class PlatformAnnouncementViewComponent(
    ApplicationDbContext context) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        string? announcement = await context.PlatformSettings
            .AsNoTracking()
            .Where(settings =>
                settings.PlatformSettingsId == PlatformSettings.SingletonId &&
                settings.IsAnnouncementEnabled)
            .Select(settings => settings.Announcement)
            .SingleOrDefaultAsync();

        return View("~/Pages/Shared/Components/PlatformAnnouncement/Default.cshtml",
            announcement);
    }
}
