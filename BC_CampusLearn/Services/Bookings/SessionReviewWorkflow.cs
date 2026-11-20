using System.Linq.Expressions;
using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Bookings;

public static class SessionReviewWorkflow
{
    public const string TutorHeadApproval = "Approve";
    public const string TutorHeadApprovalWithFeedback = "Approve with feedback";

    public static readonly Expression<Func<Booking, bool>> AwaitingTutorHeadReview = booking =>
        booking.AdminSessionReview == null && booking.SuperAdminSessionReview == null &&
        !booking.SessionReviews.Any(review => review.Reviewer.Role == BcUserRole.HeadOfTutors);

    public static readonly Expression<Func<Booking, bool>> AwaitingAdminReview = booking =>
        booking.AdminSessionReview == null && booking.SuperAdminSessionReview == null &&
        booking.SessionReviews
            .Where(review => review.Reviewer.Role == BcUserRole.HeadOfTutors)
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.SessionReviewId)
            .Select(review => review.Decision)
            .Take(1)
            .Any(decision => decision == TutorHeadApproval || decision == TutorHeadApprovalWithFeedback);

    public static readonly Expression<Func<Booking, bool>> AwaitingFinalReview = booking =>
        booking.AdminSessionReview != null && booking.SuperAdminSessionReview == null &&
        booking.SessionReviews.Any(review => review.Reviewer.Role == BcUserRole.HeadOfTutors);

    public static bool HasParticipantReviews(Booking booking) =>
        booking.Status == BookingStatus.Completed &&
        booking.StudentEvaluation is not null && booking.TutorEvaluation is not null;

    public static bool IsTutorHeadApproval(string? decision) =>
        decision is TutorHeadApproval or TutorHeadApprovalWithFeedback;

    public static SessionReview? LatestTutorHeadReview(Booking booking) => booking.SessionReviews
        .Where(review => review.Reviewer.Role == BcUserRole.HeadOfTutors)
        .OrderByDescending(review => review.CreatedAt)
        .ThenByDescending(review => review.SessionReviewId)
        .FirstOrDefault();
}
