namespace BC_CampusLearn.Models.Entities;

public static class BookingStatusExtensions
{
    public static string ToDisplayText(this BookingStatus status) =>
        status == BookingStatus.InProgress
            ? "In Progress"
            : status.ToString();
}
