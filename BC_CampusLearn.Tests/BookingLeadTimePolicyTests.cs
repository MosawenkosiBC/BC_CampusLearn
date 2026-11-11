using BC_CampusLearn.Data;
using BC_CampusLearn.Services.Bookings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class BookingLeadTimePolicyTests
{
    [Theory]
    [InlineData("30mins", 30)]
    [InlineData("30 minutes", 30)]
    [InlineData("2 hours", 120)]
    [InlineData("1 day", 1440)]
    [InlineData("0 mins (No lead time)", 0)]
    [InlineData("none", 0)]
    public void TypedDurationsAreParsed(string value, int expectedMinutes)
    {
        Assert.True(BookingLeadTimePolicy.TryParse(value, out int minutes));
        Assert.Equal(expectedMinutes, minutes);
    }

    [Fact]
    public void SessionAtExactLeadTimeBoundaryIsBookable()
    {
        DateTimeOffset now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

        bool bookable = BookingLeadTimePolicy.IsBookable(
            now.AddHours(24),
            minimumLeadTimeMinutes: 1440,
            now);

        Assert.True(bookable);
        Assert.False(BookingLeadTimePolicy.IsBookable(
            now.AddHours(24).AddTicks(-1),
            minimumLeadTimeMinutes: 1440,
            now));
    }

    [Fact]
    public void ReservationBoundaryTranslatesToSqlServerDateAdd()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=LeadTimeTranslation;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);
        DateTimeOffset now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

        string sql = context.TutorAvailabilities
            .Where(slot => slot.AvailableTime >= now.AddMinutes(
                slot.Tutor.MinimumBookingLeadTimeMinutes))
            .ToQueryString();

        Assert.Contains("DATEADD", sql, StringComparison.OrdinalIgnoreCase);
    }
}
