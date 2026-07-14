using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IBookingService _bookingService;

    public CreateModel(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [BindProperty]
    public CreateBookingInput Input { get; set; }
        = new CreateBookingInput();

    public BookingPreviewViewModel Preview
    { get; private set; }
        = null!;

    public async Task<IActionResult> OnGetAsync(
        int slotId,
        CancellationToken cancellationToken)
    {
        BookingPreviewViewModel? preview =
            await _bookingService.GetBookingPreviewAsync(
                slotId,
                cancellationToken);

        if (preview is null)
        {
            return NotFound();
        }

        Preview = preview;

        Input.TutorAvailabilityId = slotId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReloadPageAsync(
                Input.TutorAvailabilityId,
                cancellationToken);
        }

        BookingCreationResult result =
            await _bookingService.CreateBookingAsync(
                Input,
                cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage
                    ?? "The booking could not be created.");

            return await ReloadPageAsync(
                Input.TutorAvailabilityId,
                cancellationToken);
        }

        TempData["SuccessMessage"] =
            "Your tutoring session was booked successfully.";

        return RedirectToPage("/Bookings/Index");
    }

    private async Task<IActionResult> ReloadPageAsync(
        int slotId,
        CancellationToken cancellationToken)
    {
        BookingPreviewViewModel? preview =
            await _bookingService.GetBookingPreviewAsync(
                slotId,
                cancellationToken);

        if (preview is null)
        {
            return NotFound();
        }

        Preview = preview;

        return Page();
    }
}