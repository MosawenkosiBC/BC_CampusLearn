using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Bookings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_BlocksStudentWithPendingReview()
    {
        await using ApplicationDbContext context = CreateContext();
        string objectId = Guid.NewGuid().ToString();
        string tenantId = Guid.NewGuid().ToString();
        var completedBooking = new Booking
        {
            TutorId = 12,
            ProgrammeModuleId = 3,
            StudentBcUserId = 21,
            StudentObjectId = objectId,
            StudentTenantId = tenantId,
            StudentName = "Student",
            Location = "Teams",
            Status = BookingStatus.Completed,
            Duration = SessionDuration.OneHour,
            ScheduledStartTime = DateTimeOffset.UtcNow.AddDays(-1),
            DateBooked = DateTimeOffset.UtcNow.AddDays(-2)
        };
        context.Bookings.Add(completedBooking);
        await context.SaveChangesAsync();

        var currentUser = new CurrentUser(
            21,
            "STUDENT21",
            objectId,
            tenantId,
            "Student",
            "student@example.com");
        var service = new BookingService(
            context,
            new TestCurrentUserService(currentUser),
            new TestWebHostEnvironment());

        BookingCreationResult result = await service.CreateBookingAsync(
            new CreateBookingInput
            {
                TutorAvailabilityId = 45,
                ProgrammeModuleId = 3,
                Location = "Study room",
                AcceptedTerms = true
            });

        Assert.False(result.Succeeded);
        Assert.True(result.PendingReviewRequired);
        Assert.Equal(
            "Complete your pending session review before booking another session.",
            result.ErrorMessage);
        Assert.Single(context.Bookings);
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsTutorsOwnAvailability()
    {
        await using ApplicationDbContext context = CreateContext();
        var tutorUser = new BcUser
        {
            BcUserId = 31,
            PersonnelNumber = "TUTOR31",
            DisplayName = "Tutor Student",
            EntraObjectId = Guid.NewGuid(),
            EntraTenantId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        var tutor = new Tutor
        {
            TutorId = 12,
            BcUserId = tutorUser.BcUserId,
            BcUser = tutorUser,
            ProgrammeId = 1,
            ReasonForTutoring = "Help students",
            TeachingStyle = "Practical",
            PreviousTutoringExperience = "Peer tutoring",
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = string.Empty,
            Status = TutorStatus.Approved,
            IsActive = true,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        var availability = new TutorAvailability
        {
            TutorAvailabilityId = 45,
            TutorId = tutor.TutorId,
            Tutor = tutor,
            AvailableTime = DateTimeOffset.UtcNow.AddDays(2)
        };

        context.TutorAvailabilities.Add(availability);
        await context.SaveChangesAsync();

        var currentUser = new CurrentUser(
            tutorUser.BcUserId,
            tutorUser.PersonnelNumber,
            tutorUser.EntraObjectId.ToString(),
            tutorUser.EntraTenantId.ToString(),
            tutorUser.DisplayName,
            tutorUser.Email);
        var service = new BookingService(
            context,
            new TestCurrentUserService(currentUser),
            new TestWebHostEnvironment());

        BookingCreationResult result = await service.CreateBookingAsync(
            new CreateBookingInput
            {
                TutorAvailabilityId = availability.TutorAvailabilityId,
                ProgrammeModuleId = 1,
                Location = "Study room",
                Summary = new string('A', 75),
                AcceptedTerms = true
            });

        Assert.False(result.Succeeded);
        Assert.Equal(
            "You cannot book a tutoring session with yourself.",
            result.ErrorMessage);
        Assert.Empty(context.Bookings);
        Assert.Single(context.TutorAvailabilities);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public CurrentUser GetRequiredUser() => user;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";

        public IFileProvider WebRootFileProvider { get; set; }
            = new NullFileProvider();

        public string WebRootPath { get; set; } = Path.GetTempPath();

        public string EnvironmentName { get; set; } = "Testing";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; }
            = new NullFileProvider();
    }
}
