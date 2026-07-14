namespace BC_CampusLearn.Services.Bookings;

public record BookingCreationResult(
    bool Succeeded,
    int? BookingId,
    string? ErrorMessage)
{
    public static BookingCreationResult Success(
        int bookingId)
    {
        return new BookingCreationResult(
            true,
            bookingId,
            null);
    }

    public static BookingCreationResult Failure(
        string errorMessage)
    {
        return new BookingCreationResult(
            false,
            null,
            errorMessage);
    }
}