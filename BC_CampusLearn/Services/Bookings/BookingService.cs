using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Bookings;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BookingService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<BookingPreviewViewModel?>
        GetBookingPreviewAsync(
            int tutorAvailabilityId,
            CancellationToken cancellationToken = default)
    {
        return await _context.TutorAvailabilities
            .AsNoTracking()
            .Where(slot =>
                slot.TutorAvailabilityId ==
                    tutorAvailabilityId &&
                slot.IsActive &&
                slot.AvailableTime >
                    DateTimeOffset.UtcNow)
            .Select(slot =>
                new BookingPreviewViewModel
                {
                    TutorAvailabilityId =
                        slot.TutorAvailabilityId,

                    TutorId = slot.TutorId,

                    TutorName =
                        slot.Tutor.DisplayName,

                    ModuleName =
                        slot.CourseModule.Name,

                    AvailableTime = slot.AvailableTime
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BookingCreationResult>
        CreateBookingAsync(
            CreateBookingInput input,
            CancellationToken cancellationToken = default)
    {
        CurrentUser student =
            _currentUserService.GetRequiredUser();

        TutorAvailability? slot =
            await _context.TutorAvailabilities
                .Include(item => item.Tutor)
                .FirstOrDefaultAsync(
                    item =>
                        item.TutorAvailabilityId ==
                        input.TutorAvailabilityId,
                    cancellationToken);

        if (slot is null)
        {
            return BookingCreationResult.Failure(
                "The selected availability slot does not exist.");
        }

        if (!slot.IsActive ||
            slot.AvailableTime <= DateTimeOffset.UtcNow)
        {
            return BookingCreationResult.Failure(
                "This availability slot is no longer available.");
        }

        if (!Enum.IsDefined(input.Duration))
        {
            return BookingCreationResult.Failure(
                "Select a valid session duration.");
        }

        List<string> preparationLinks = input.PreparationLinks
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Select(link => link!.Trim())
            .ToList();

        if (preparationLinks.Count > 3 ||
            preparationLinks.Any(link =>
                link.Length > 2048 ||
                !Uri.TryCreate(
                    link,
                    UriKind.Absolute,
                    out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps)))
        {
            return BookingCreationResult.Failure(
                "Add no more than three valid HTTP or HTTPS links.");
        }

        slot.IsActive = false;

        var booking = new Booking
        {
            TutorAvailabilityId =
                slot.TutorAvailabilityId,

            StudentObjectId = student.ObjectId,
            StudentTenantId = student.TenantId,
            StudentName = student.DisplayName,
            StudentEmail = student.Email,

            Location = input.Location.Trim(),

            Summary = input.Summary?.Trim(),

            Status = BookingStatus.Pending,

            Duration = input.Duration,

            DateBooked = DateTimeOffset.UtcNow
        };

        for (int index = 0; index < preparationLinks.Count; index++)
        {
            booking.PreparationLinks.Add(
                new BookingPreparationLink
                {
                    Position = (byte)(index + 1),
                    Url = preparationLinks[index]
                });
        }

        _context.Bookings.Add(booking);

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);

            return BookingCreationResult.Success(
                booking.BookingId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BookingCreationResult.Failure(
                "Another student booked this slot first. " +
                "Please select another time.");
        }
        catch (DbUpdateException)
        {
            return BookingCreationResult.Failure(
                "The booking could not be saved. " +
                "The slot may already have been booked.");
        }
    }
}
