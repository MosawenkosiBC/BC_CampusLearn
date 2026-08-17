using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Tutors;

[Authorize]
public class DetailsModel : PageModel
{
    private const string MobileTermsAcceptanceKey =
        "MobileBookingTermsAcceptance";

    private readonly ITutorService _tutorService;

    public DetailsModel(ITutorService tutorService)
    {
        _tutorService = tutorService;
    }

    public TutorDetailsViewModel Tutor { get; private set; }
        = null!;

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        TutorDetailsViewModel? tutor =
            await _tutorService.GetTutorDetailsAsync(
                id,
                cancellationToken);

        if (tutor is null)
        {
            return NotFound();
        }

        Tutor = tutor;

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptTermsAsync(
        int id,
        int slotId,
        int programmeModuleId,
        CancellationToken cancellationToken)
    {
        TutorDetailsViewModel? tutor =
            await _tutorService.GetTutorDetailsAsync(
                id,
                cancellationToken);

        if (tutor is null)
        {
            return NotFound();
        }

        bool validModule = tutor.Modules.Any(module =>
            module.ProgrammeModuleId == programmeModuleId);
        bool validSlot = tutor.AvailabilitySlots.Any(slot =>
            slot.TutorAvailabilityId == slotId &&
            !slot.IsBooked &&
            slot.AvailableTime > DateTimeOffset.UtcNow);

        if (!validModule || !validSlot)
        {
            return BadRequest(
                "Select an available module, date, and time before " +
                "agreeing to the terms.");
        }

        TempData[MobileTermsAcceptanceKey] =
            CreateTermsAcceptanceValue(
                slotId,
                programmeModuleId);

        return RedirectToPage(
            "/Bookings/Create",
            new
            {
                slotId,
                programmeModuleId,
                mobileTerms = true
            });
    }

    private static string CreateTermsAcceptanceValue(
        int slotId,
        int programmeModuleId)
    {
        return $"{slotId}:{programmeModuleId}";
    }
}
