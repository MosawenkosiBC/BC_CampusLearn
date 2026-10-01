using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Events;

public class IndexModel(
    ApplicationDbContext context,
    CampusEventImageStore imageStore,
    TimeProvider timeProvider) : PageModel
{
    public IReadOnlyList<AdminEventListItem> Events { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string EventTab { get; set; } = "active";

    public int ActiveEventCount { get; private set; }
    public int DraftEventCount { get; private set; }
    public int PastEventCount { get; private set; }

    [TempData]
    public string? EventMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<AdminEventListItem> allEvents = await context.CampusEvents
            .AsNoTracking()
            .OrderBy(item => item.StartsAt)
            .Select(item => new AdminEventListItem(
                item.CampusEventId,
                item.Title,
                item.Location,
                item.StartsAt,
                item.EndsAt,
                item.PublishAt,
                item.IsPublished,
                item.BannerImagePath,
                item.Details.Count))
            .ToListAsync(cancellationToken);

        ActiveEventCount = allEvents.Count(item =>
            item.IsPublished && item.EndsAt >= now);
        DraftEventCount = allEvents.Count(item =>
            !item.IsPublished && item.EndsAt >= now);
        PastEventCount = allEvents.Count(item => item.EndsAt < now);

        EventTab = EventTab.ToLowerInvariant() switch
        {
            "drafts" => "drafts",
            "past" => "past",
            _ => "active"
        };
        Events = EventTab switch
        {
            "drafts" => allEvents
                .Where(item => !item.IsPublished && item.EndsAt >= now)
                .OrderBy(item => item.StartsAt)
                .ToList(),
            "past" => allEvents
                .Where(item => item.EndsAt < now)
                .OrderByDescending(item => item.EndsAt)
                .ToList(),
            _ => allEvents
                .Where(item => item.IsPublished && item.EndsAt >= now)
                .OrderBy(item => item.StartsAt)
                .ToList()
        };
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int id,
        CancellationToken cancellationToken)
    {
        CampusEvent? campusEvent = await context.CampusEvents
            .SingleOrDefaultAsync(item => item.CampusEventId == id,
                cancellationToken);
        if (campusEvent is null)
        {
            return NotFound();
        }

        context.CampusEvents.Remove(campusEvent);
        await context.SaveChangesAsync(cancellationToken);
        imageStore.Delete(campusEvent.BannerImagePath);
        EventMessage = $"{campusEvent.Title} was removed.";
        return RedirectToPage(new { EventTab });
    }

    public async Task<IActionResult> OnPostPublishAsync(
        int id,
        CancellationToken cancellationToken)
    {
        CampusEvent? campusEvent = await context.CampusEvents
            .SingleOrDefaultAsync(item => item.CampusEventId == id,
                cancellationToken);
        if (campusEvent is null)
        {
            return NotFound();
        }

        campusEvent.IsPublished = true;
        campusEvent.UpdatedAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);

        EventMessage = $"{campusEvent.Title} was published.";
        return RedirectToPage(new { EventTab = "active" });
    }

    public string Status(AdminEventListItem item)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (item.PublishAt > now) return "Scheduled";
        if (item.StartsAt > now) return "Upcoming";
        return "Active";
    }

    public string EmptyTitle => EventTab switch
    {
        "drafts" => "No draft events",
        "past" => "No past events",
        _ => "No active events"
    };

    public string EmptyMessage => EventTab switch
    {
        "drafts" => "Events saved as drafts will appear here.",
        "past" => "Events move here automatically ten minutes after their event time.",
        _ => "Create or publish an event to display it on the home page."
    };
}

public sealed record AdminEventListItem(
    int CampusEventId,
    string Title,
    string Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset PublishAt,
    bool IsPublished,
    string BannerImagePath,
    int CustomFieldCount);
