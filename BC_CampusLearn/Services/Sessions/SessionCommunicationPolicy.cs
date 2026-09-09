using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Sessions;

public static class SessionCommunicationPolicy
{
    public static bool IsClosed(
        BookingStatus status,
        bool tutorReviewSubmitted,
        bool studentReviewSubmitted) =>
        status is BookingStatus.Cancelled or BookingStatus.Declined ||
        (tutorReviewSubmitted && studentReviewSubmitted);
}
