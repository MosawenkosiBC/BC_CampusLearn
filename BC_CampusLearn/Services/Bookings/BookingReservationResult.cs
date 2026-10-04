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
    Guid? ReservationToken,
    DateTimeOffset? ReservationExpiresAt)
{
    public static BookingReservationResult Acquired(
        Guid token,
        DateTimeOffset reservationExpiresAt) =>
        new(
            BookingReservationStatus.Acquired,
            token,
            reservationExpiresAt);

    public static BookingReservationResult Failed(
        BookingReservationStatus status) => new(status, null, null);
}
