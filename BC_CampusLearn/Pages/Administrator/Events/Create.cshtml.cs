using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Administrator.Events;

public class CreateModel(
    ApplicationDbContext context,
    CampusEventImageStore imageStore,
    TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public CampusEventInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? BannerImage { get; set; }

    public void OnGet()
    {
        DateTime localNow = timeProvider.GetLocalNow().DateTime;
        Input.StartsAt = localNow.Date.AddDays(1).AddHours(9);
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        AddCustomValidation();
        string? imageError = imageStore.GetValidationError(
            BannerImage, required: false);
        if (imageError is not null)
        {
            ModelState.AddModelError(nameof(BannerImage), imageError);
        }
        if (!ModelState.IsValid)
        {
            return Page();
        }

        string imagePath = BannerImage is { Length: > 0 }
            ? await imageStore.SaveAsync(BannerImage, cancellationToken)
            : string.Empty;
        CampusEvent campusEvent = BuildEvent(imagePath);
        context.CampusEvents.Add(campusEvent);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            imageStore.Delete(imagePath);
            throw;
        }

        TempData["EventMessage"] = $"{campusEvent.Title} was created.";
        return RedirectToPage("Index");
    }

    private CampusEvent BuildEvent(string imagePath)
    {
        CampusEvent campusEvent = new()
        {
            Title = Input.Title.Trim(),
            Description = Input.Description.Trim(),
            Disclaimer = string.IsNullOrWhiteSpace(Input.Disclaimer)
                ? null : Input.Disclaimer.Trim(),
            Location = Input.Location.Trim(),
            StartsAt = CampusEventFormValidator.AtSouthAfricaOffset(Input.StartsAt),
            EndsAt = CampusEventFormValidator.RemovalAt(Input.StartsAt),
            PublishAt = CampusEventFormValidator.ResolvePublishAt(
                Input.PublishAt, timeProvider.GetUtcNow()),
            IsPublished = Input.IsPublished,
            BannerImagePath = imagePath,
            CreatedAt = timeProvider.GetUtcNow()
        };
        AddDetails(campusEvent);
        return campusEvent;
    }

    private void AddDetails(CampusEvent campusEvent)
    {
        int position = 0;
        foreach (CampusEventDetailInput field in Input.CustomFields)
        {
            if (string.IsNullOrWhiteSpace(field.Title) &&
                string.IsNullOrWhiteSpace(field.Value)) continue;
            campusEvent.Details.Add(new CampusEventDetail
            {
                Title = field.Title.Trim(),
                DataType = CampusEventFormValidator.NormalizeDataType(
                    field.DataType, field.Value),
                Value = field.Value.Trim(),
                Position = position++
            });
        }
    }

    private void AddCustomValidation()
    {
        foreach ((string key, string message) in
            CampusEventFormValidator.Validate(Input))
        {
            ModelState.AddModelError(key, message);
        }
    }
}
