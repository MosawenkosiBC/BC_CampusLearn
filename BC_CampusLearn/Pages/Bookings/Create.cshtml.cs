using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class CreateModel : PageModel
{
    private const string MobileTermsAcceptanceKey =
        "MobileBookingTermsAcceptance";

    private readonly IBookingService _bookingService;

    public CreateModel(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [BindProperty]
    public CreateBookingInput Input { get; set; }
        = new CreateBookingInput();

    [BindProperty]
    public bool MobileTermsAccepted { get; set; }

    public BookingPreviewViewModel Preview
    { get; private set; }
        = null!;

    public async Task<IActionResult> OnGetAsync(
        int slotId,
        int? programmeModuleId,
        bool mobileTerms,
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

        if (programmeModuleId.HasValue &&
            preview.Modules.Any(module =>
                module.ProgrammeModuleId ==
                    programmeModuleId.Value))
        {
            Input.ProgrammeModuleId =
                programmeModuleId.Value;
        }

        MobileTermsAccepted =
            mobileTerms &&
            programmeModuleId.HasValue &&
            HasValidMobileTermsAcceptance(
                slotId,
                programmeModuleId.Value);

        if (MobileTermsAccepted)
        {
            Input.AcceptedTerms = true;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (MobileTermsAccepted)
        {
            bool hasValidAcceptance =
                HasValidMobileTermsAcceptance(
                    Input.TutorAvailabilityId,
                    Input.ProgrammeModuleId);

            ModelState.Remove("Input.AcceptedTerms");
            Input.AcceptedTerms = hasValidAcceptance;

            if (!hasValidAcceptance)
            {
                MobileTermsAccepted = false;
                ModelState.AddModelError(
                    "Input.AcceptedTerms",
                    "Review and accept the terms and conditions " +
                    "before booking the session.");
            }
        }

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
        TempData.Remove(MobileTermsAcceptanceKey);

        return RedirectToPage("/Bookings/Index");
    }

    private bool HasValidMobileTermsAcceptance(
        int slotId,
        int programmeModuleId)
    {
        string expectedValue =
            $"{slotId}:{programmeModuleId}";

        return string.Equals(
            TempData.Peek(MobileTermsAcceptanceKey) as string,
            expectedValue,
            StringComparison.Ordinal);
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
