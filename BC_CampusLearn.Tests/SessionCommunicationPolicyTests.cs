using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using Xunit;

namespace BC_CampusLearn.Tests;

public class SessionCommunicationPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void CompletedSession_ClosesOnlyAfterBothReviews(
        bool tutorReviewSubmitted,
        bool studentReviewSubmitted,
        bool expectedClosed)
    {
        bool isClosed = SessionCommunicationPolicy.IsClosed(
            BookingStatus.Completed,
            tutorReviewSubmitted,
            studentReviewSubmitted);

        Assert.Equal(expectedClosed, isClosed);
    }

    [Theory]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Declined)]
    public void CancelledOrDeclinedSession_RemainsClosed(
        BookingStatus status)
    {
        Assert.True(SessionCommunicationPolicy.IsClosed(
            status,
            tutorReviewSubmitted: false,
            studentReviewSubmitted: false));
    }
}
