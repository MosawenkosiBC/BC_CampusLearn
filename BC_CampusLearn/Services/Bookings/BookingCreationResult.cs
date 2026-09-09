namespace BC_CampusLearn.Services.Bookings;

public record BookingCreationResult(
    bool Succeeded,
    int? BookingId,
    string? ErrorMessage,
    bool PendingReviewRequired)
{
    public static BookingCreationResult Success(
        int bookingId)
    {
        return new BookingCreationResult(
            true,
            bookingId,
            null,
            false);
    }

    public static BookingCreationResult Failure(
        string errorMessage,
        bool pendingReviewRequired = false)
    {
        return new BookingCreationResult(
            false,
            null,
            errorMessage,
            pendingReviewRequired);
    }
}
