using BC_CampusLearn.Models.ViewModels;

namespace BC_CampusLearn.Services.Bookings;

public interface IBookingService
{
    Task<BookingPreviewViewModel?>
        GetBookingPreviewAsync(
            int tutorAvailabilityId,
            CancellationToken cancellationToken = default);

    Task<BookingCreationResult>
        CreateBookingAsync(
            CreateBookingInput input,
            CancellationToken cancellationToken = default);
}