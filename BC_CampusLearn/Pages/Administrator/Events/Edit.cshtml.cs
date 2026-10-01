using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Events;

public class EditModel(
    ApplicationDbContext context,
    CampusEventImageStore imageStore,
    TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public CampusEventInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? BannerImage { get; set; }

    public int EventId { get; private set; }
    public string CurrentBannerImagePath { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        CampusEvent? campusEvent = await context.CampusEvents
            .AsNoTracking()
            .Include(item => item.Details.OrderBy(detail => detail.Position))
            .SingleOrDefaultAsync(item => item.CampusEventId == id,
                cancellationToken);
        if (campusEvent is null) return NotFound();

        EventId = id;
        CurrentBannerImagePath = campusEvent.BannerImagePath;
        Input = new CampusEventInput
        {
            Title = campusEvent.Title,
            Description = campusEvent.Description,
            Disclaimer = campusEvent.Disclaimer,
            Location = campusEvent.Location,
            StartsAt = campusEvent.StartsAt.ToOffset(TimeSpan.FromHours(2)).DateTime,
            PublishAt = campusEvent.PublishAt.ToOffset(TimeSpan.FromHours(2)).DateTime,
            IsPublished = campusEvent.IsPublished,
            CustomFields = campusEvent.Details.Select(detail =>
                new CampusEventDetailInput
                {
                    Title = detail.Title,
                    DataType = detail.DataType,
                    Value = detail.Value
                }).ToList()
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        int id,
        CancellationToken cancellationToken)
    {
        CampusEvent? campusEvent = await context.CampusEvents
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.CampusEventId == id,
                cancellationToken);
        if (campusEvent is null) return NotFound();

        EventId = id;
        CurrentBannerImagePath = campusEvent.BannerImagePath;
        foreach ((string key, string message) in
            CampusEventFormValidator.Validate(Input))
        {
            ModelState.AddModelError(key, message);
        }
        string? imageError = imageStore.GetValidationError(
            BannerImage, required: false);
        if (imageError is not null)
        {
            ModelState.AddModelError(nameof(BannerImage), imageError);
        }
        if (!ModelState.IsValid) return Page();

        string? oldImagePath = null;
        if (BannerImage is { Length: > 0 })
        {
            oldImagePath = campusEvent.BannerImagePath;
            campusEvent.BannerImagePath = await imageStore.SaveAsync(
                BannerImage, cancellationToken);
        }

        campusEvent.Title = Input.Title.Trim();
        campusEvent.Description = Input.Description.Trim();
        campusEvent.Disclaimer = string.IsNullOrWhiteSpace(Input.Disclaimer)
            ? null : Input.Disclaimer.Trim();
        campusEvent.Location = Input.Location.Trim();
        campusEvent.StartsAt = CampusEventFormValidator.AtSouthAfricaOffset(Input.StartsAt);
        campusEvent.EndsAt = CampusEventFormValidator.RemovalAt(Input.StartsAt);
        campusEvent.PublishAt = CampusEventFormValidator.ResolvePublishAt(
            Input.PublishAt, timeProvider.GetUtcNow());
        campusEvent.IsPublished = Input.IsPublished;
        campusEvent.UpdatedAt = timeProvider.GetUtcNow();
        context.CampusEventDetails.RemoveRange(campusEvent.Details);
        campusEvent.Details = Input.CustomFields
            .Where(field => !string.IsNullOrWhiteSpace(field.Title) ||
                !string.IsNullOrWhiteSpace(field.Value))
            .Select((field, position) => new CampusEventDetail
            {
                Title = field.Title.Trim(),
                DataType = CampusEventFormValidator.NormalizeDataType(
                    field.DataType, field.Value),
                Value = field.Value.Trim(),
                Position = position
            }).ToList();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (oldImagePath is not null)
            {
                imageStore.Delete(campusEvent.BannerImagePath);
            }
            throw;
        }
        if (oldImagePath is not null) imageStore.Delete(oldImagePath);

        TempData["EventMessage"] = $"{campusEvent.Title} was updated.";
        return RedirectToPage("Index");
    }
}
