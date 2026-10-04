namespace BC_CampusLearn.Services.Bookings;

public enum BookingFailureReason
{
    None,
    Unavailable,
    Expired,
    AlreadyBooked,
    Reserved,
    ReservationExpired
}

public record BookingCreationResult(
    bool Succeeded,
    int? BookingId,
    string? ErrorMessage,
    bool PendingReviewRequired)
{
    public BookingFailureReason FailureReason { get; init; }

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
        bool pendingReviewRequired = false,
        BookingFailureReason failureReason = BookingFailureReason.None)
    {
        return new BookingCreationResult(
            false,
            null,
            errorMessage,
            pendingReviewRequired)
        {
            FailureReason = failureReason
        };
    }
}
