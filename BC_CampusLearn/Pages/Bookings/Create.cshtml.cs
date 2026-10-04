using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class CreateModel : PageModel
{
    private const string MobileTermsAcceptanceKey =
        "MobileBookingTermsAcceptance";

    private readonly IBookingService _bookingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ApplicationDbContext _context;

    public CreateModel(
        IBookingService bookingService,
        ICurrentUserService currentUserService,
        ApplicationDbContext context)
    {
        _bookingService = bookingService;
        _currentUserService = currentUserService;
        _context = context;
    }

    [BindProperty]
    public CreateBookingInput Input { get; set; }
        = new CreateBookingInput();

    [BindProperty]
    public bool MobileTermsAccepted { get; set; }

    [BindProperty]
    public long SlotStartUnixTimeSeconds { get; set; }

    public BookingPreviewViewModel Preview
    { get; private set; }
        = null!;

    public bool ShowPendingReviewModal { get; private set; }

    public long ReservationExpiresAtUnixTimeMilliseconds
    { get; private set; }

    public IReadOnlyList<string> BookingTerms { get; private set; } = [];

    public IReadOnlyList<StudyArea> StudyAreas { get; private set; } = [];

    public string SupportEmail { get; private set; } =
        "tutors@belgiumcampus.ac.za";

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
            bool slotHasExpired = await _context.TutorAvailabilities
                .AsNoTracking()
                .AnyAsync(
                    slot =>
                        slot.TutorAvailabilityId == slotId &&
                        slot.AvailableTime <= DateTimeOffset.UtcNow,
                    cancellationToken);

            return RedirectToPage(
                "/Bookings/Unavailable",
                new { reason = slotHasExpired ? "expired" : "unavailable" });
        }

        if (preview.TutorBcUserId ==
            _currentUserService.GetRequiredUser().BcUserId)
        {
            TempData["ErrorMessage"] =
                "You cannot book a tutoring session with yourself.";
            return RedirectToPage(
                "/Tutors/Details",
                new { id = preview.TutorId });
        }

        Preview = preview;
        SlotStartUnixTimeSeconds = preview.AvailableTime.ToUnixTimeSeconds();
        await LoadPlatformSettingsAsync(cancellationToken);

        ShowPendingReviewModal =
            await _bookingService.HasPendingStudentReviewAsync(
                cancellationToken);

        if (!ShowPendingReviewModal)
        {
            BookingReservationResult reservation =
                await _bookingService.TryReserveSlotAsync(
                    slotId,
                    cancellationToken);

            if (reservation.Status != BookingReservationStatus.Acquired)
            {
                string reason = reservation.Status switch
                {
                    BookingReservationStatus.Expired => "expired",
                    BookingReservationStatus.ReservedByAnotherStudent =>
                        "reserved",
                    _ => "unavailable"
                };

                return RedirectToPage(
                    "/Bookings/Unavailable",
                    new { reason });
            }

            Input.ReservationToken = reservation.ReservationToken!.Value;
            ReservationExpiresAtUnixTimeMilliseconds = reservation
                .ReservationExpiresAt!.Value
                .ToUnixTimeMilliseconds();
        }

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
        if (SlotStartUnixTimeSeconds > 0 &&
            SlotStartUnixTimeSeconds <=
                DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return RedirectToPage(
                "/Bookings/Unavailable",
                new { reason = "expired" });
        }

        if (await _bookingService.HasPendingStudentReviewAsync(
            cancellationToken))
        {
            ShowPendingReviewModal = true;
            return await ReloadPageAsync(
                Input.TutorAvailabilityId,
                cancellationToken);
        }

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
            if (result.FailureReason == BookingFailureReason.Reserved)
            {
                return RedirectToPage(
                    "/Bookings/Unavailable",
                    new { reason = "reserved" });
            }

            if (result.FailureReason ==
                BookingFailureReason.ReservationExpired)
            {
                return RedirectToPage(
                    "/Bookings/Unavailable",
                    new { reason = "reservation-expired" });
            }

            ShowPendingReviewModal = result.PendingReviewRequired;
            if (!result.PendingReviewRequired)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage
                        ?? "The booking could not be created.");
            }

            return await ReloadPageAsync(
                Input.TutorAvailabilityId,
                cancellationToken,
                bookingAttemptFailed: true,
                failureReason: result.FailureReason);
        }

        TempData["SuccessMessage"] =
            "Your tutoring session was booked successfully.";
        TempData.Remove(MobileTermsAcceptanceKey);

        return RedirectToPage("/Bookings/Index");
    }

    public async Task<IActionResult> OnPostReleaseAsync(
        int tutorAvailabilityId,
        Guid reservationToken,
        CancellationToken cancellationToken)
    {
        await _bookingService.ReleaseSlotReservationAsync(
            tutorAvailabilityId,
            reservationToken,
            cancellationToken);

        return new NoContentResult();
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
        CancellationToken cancellationToken,
        bool bookingAttemptFailed = false,
        BookingFailureReason failureReason = BookingFailureReason.None)
    {
        BookingPreviewViewModel? preview =
            await _bookingService.GetBookingPreviewAsync(
                slotId,
                cancellationToken);

        if (preview is null)
        {
            return RedirectToPage(
                "/Bookings/Unavailable",
                new
                {
                    reason = bookingAttemptFailed
                        ? failureReason == BookingFailureReason.Expired
                            ? "expired"
                            : failureReason == BookingFailureReason.Unavailable
                                ? "unavailable"
                                : "booked"
                        : "unavailable"
                });
        }

        Preview = preview;

        if (Input.ReservationToken != Guid.Empty)
        {
            int currentUserId =
                _currentUserService.GetRequiredUser().BcUserId;
            DateTimeOffset? reservationExpiresAt = await _context
                .TutorAvailabilities
                .AsNoTracking()
                .Where(slot =>
                    slot.TutorAvailabilityId == slotId &&
                    slot.ReservedByBcUserId == currentUserId &&
                    slot.ReservationToken == Input.ReservationToken)
                .Select(slot => slot.ReservationExpiresAt)
                .SingleOrDefaultAsync(cancellationToken);

            if (reservationExpiresAt.HasValue)
            {
                ReservationExpiresAtUnixTimeMilliseconds =
                    reservationExpiresAt.Value.ToUnixTimeMilliseconds();
            }
        }

        await LoadPlatformSettingsAsync(cancellationToken);

        return Page();
    }

    private async Task LoadPlatformSettingsAsync(
        CancellationToken cancellationToken)
    {
        List<StudyArea> activeStudyAreas = await _context.StudyAreas
            .AsNoTracking()
            .Where(studyArea => studyArea.IsActive)
            .OrderBy(studyArea => studyArea.DisplayOrder)
            .ThenBy(studyArea => studyArea.Name)
            .ToListAsync(cancellationToken);
        StudyAreas = activeStudyAreas
            .Where(studyArea =>
                TutoringModeLocationPolicy.AllowsLocation(
                    Preview.PreferredTutoringMode,
                    studyArea.Name))
            .ToList();

        var settings = await _context.PlatformSettings
            .AsNoTracking()
            .Where(item => item.PlatformSettingsId == PlatformSettings.SingletonId)
            .Select(item => new
            {
                item.BookingTermsAndConditions,
                item.SupportEmail
            })
            .SingleAsync(cancellationToken);

        BookingTerms = settings.BookingTermsAndConditions
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);
        SupportEmail = settings.SupportEmail;
    }
}
