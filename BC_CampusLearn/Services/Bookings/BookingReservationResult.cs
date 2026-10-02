namespace BC_CampusLearn.Services.Bookings;

public enum BookingReservationStatus
{
    Acquired,
    ReservedByAnotherStudent,
    Unavailable,
    Expired
}

public record BookingReservationResult(
    BookingReservationStatus Status,
    Guid? ReservationToken)
{
    public static BookingReservationResult Acquired(Guid token) =>
        new(BookingReservationStatus.Acquired, token);

    public static BookingReservationResult Failed(
        BookingReservationStatus status) => new(status, null);
}
