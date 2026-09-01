using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using Xunit;

namespace BC_CampusLearn.Tests;

public class SessionLifecyclePolicyTests
{
    private static readonly DateTimeOffset ScheduledStart =
        new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-5, true)]
    [InlineData(-6, false)]
    [InlineData(0, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public void CanStart_EnforcesTwentyMinuteWindow(
        int offsetMinutes,
        bool expected)
    {
        bool actual = SessionLifecyclePolicy.CanStart(
            ScheduledStart,
            ScheduledStart.AddMinutes(offsetMinutes));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PendingAtStart_IsAutomaticallyDeclined()
    {
        BookingStatus? result =
            SessionLifecyclePolicy.GetAutomaticTransition(
                BookingStatus.Pending,
                ScheduledStart,
                null,
                ScheduledStart);

        Assert.Equal(BookingStatus.Declined, result);
    }

    [Fact]
    public void ConfirmedAtLateDeadline_IsAutomaticallyCancelled()
    {
        BookingStatus? result =
            SessionLifecyclePolicy.GetAutomaticTransition(
                BookingStatus.Confirmed,
                ScheduledStart,
                null,
                ScheduledStart.AddMinutes(15));

        Assert.Equal(BookingStatus.Cancelled, result);
    }

    [Fact]
    public void StartedSession_CompletesOneHourAfterActualStart()
    {
        DateTimeOffset actualStart = ScheduledStart.AddMinutes(10);
        DateTimeOffset completion = actualStart.AddHours(1);

        BookingStatus? before =
            SessionLifecyclePolicy.GetAutomaticTransition(
                BookingStatus.InProgress,
                ScheduledStart,
                completion,
                completion.AddSeconds(-1));
        BookingStatus? atCompletion =
            SessionLifecyclePolicy.GetAutomaticTransition(
                BookingStatus.InProgress,
                ScheduledStart,
                completion,
                completion);

        Assert.Null(before);
        Assert.Equal(BookingStatus.Completed, atCompletion);
    }

    [Fact]
    public void PendingAfterStart_RemainsAutomaticallyDeclined()
    {
        BookingStatus? result =
            SessionLifecyclePolicy.GetAutomaticTransition(
                BookingStatus.Pending,
                ScheduledStart,
                null,
                ScheduledStart.AddHours(2));

        Assert.Equal(BookingStatus.Declined, result);
    }

    [Fact]
    public void AvailabilityStartsRequireSeventyFiveMinutes()
    {
        Assert.Equal(
            TimeSpan.FromMinutes(75),
            SessionSchedulingRules.MinimumStartSeparation);
    }
}
