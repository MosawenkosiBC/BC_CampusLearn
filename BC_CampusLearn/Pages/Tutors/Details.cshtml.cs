using BC_CampusLearn.Authentication;
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
    private readonly ICurrentUserService _currentUserService;

    public DetailsModel(
        ITutorService tutorService,
        ICurrentUserService currentUserService)
    {
        _tutorService = tutorService;
        _currentUserService = currentUserService;
    }

    public TutorDetailsViewModel Tutor { get; private set; }
        = null!;

    public bool IsViewingOwnTutorProfile { get; private set; }

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
        IsViewingOwnTutorProfile =
            tutor.TutorBcUserId ==
            _currentUserService.GetRequiredUser().BcUserId;

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

        if (tutor.TutorBcUserId ==
            _currentUserService.GetRequiredUser().BcUserId)
        {
            TempData["ErrorMessage"] =
                "You cannot book a tutoring session with yourself.";
            return RedirectToPage(new { id });
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
