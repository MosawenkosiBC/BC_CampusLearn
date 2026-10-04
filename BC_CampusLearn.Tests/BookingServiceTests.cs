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
    public async Task CreateBookingAsync_NotifiesStudentAndTutor()
    {
        await using ApplicationDbContext context = CreateContext();
        DateTimeOffset scheduledStart = DateTimeOffset.UtcNow.AddDays(2);
        TutorAvailability availability = AddTutorAvailability(
            context,
            scheduledStart);
        context.StudyAreas.Add(new StudyArea
        {
            StudyAreaId = 1,
            Name = "Online",
            DisplayOrder = 1,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var currentUser = new CurrentUser(
            21,
            "STUDENT21",
            "Student Name",
            "student@example.com");
        var service = new BookingService(
            context,
            new TestCurrentUserService(currentUser),
            new TestWebHostEnvironment());

        BookingReservationResult reservation =
            await service.TryReserveSlotAsync(
                availability.TutorAvailabilityId);

        BookingCreationResult result = await service.CreateBookingAsync(
            new CreateBookingInput
            {
                TutorAvailabilityId = availability.TutorAvailabilityId,
                ReservationToken = reservation.ReservationToken!.Value,
                ProgrammeModuleId = 3,
                StudyAreaId = 1,
                AcceptedTerms = true
            });

        Assert.True(result.Succeeded);
        Booking booking = await context.Bookings.SingleAsync();
        Assert.Equal(1, booking.StudyAreaId);
        Assert.Equal("Online", booking.Location);
        List<UserNotification> notifications = await context
            .UserNotifications
            .OrderBy(item => item.RecipientBcUserId)
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, item =>
            item.RecipientBcUserId == 21 &&
            item.Title == "Booking submitted");
        Assert.Contains(notifications, item =>
            item.RecipientBcUserId == 31 &&
            item.Title == "New booking request");
        Assert.All(notifications, item =>
        {
            Assert.Contains("MOD101 (Module)", item.Message);
            Assert.Contains("Online", item.Message);
            Assert.Contains(
                scheduledStart.ToOffset(TimeSpan.FromHours(2))
                    .ToString("d MMMM yyyy"),
                item.Message);
        });
        Assert.All(notifications, item => Assert.Contains(
            $"/{result.BookingId}",
            item.LinkUrl));
    }

    [Fact]
    public async Task TryReserveSlotAsync_BlocksAnotherStudent()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorAvailability availability = AddTutorAvailability(
            context,
            DateTimeOffset.UtcNow.AddDays(2));
        await context.SaveChangesAsync();

        var firstStudentService = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                21,
                "STUDENT21",
                "First Student",
                "first@example.com")),
            new TestWebHostEnvironment());
        var secondStudentService = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                22,
                "STUDENT22",
                "Second Student",
                "second@example.com")),
            new TestWebHostEnvironment());

        BookingReservationResult first =
            await firstStudentService.TryReserveSlotAsync(
                availability.TutorAvailabilityId);
        BookingReservationResult second =
            await secondStudentService.TryReserveSlotAsync(
                availability.TutorAvailabilityId);

        Assert.Equal(BookingReservationStatus.Acquired, first.Status);
        Assert.Equal(
            BookingReservationStatus.ReservedByAnotherStudent,
            second.Status);
        Assert.Null(second.ReservationToken);
    }

    [Fact]
    public async Task TryReserveSlotAsync_ReloadKeepsOriginalDeadline()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorAvailability availability = AddTutorAvailability(
            context,
            DateTimeOffset.UtcNow.AddDays(2));
        await context.SaveChangesAsync();

        var service = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                21,
                "STUDENT21",
                "Student Name",
                "student@example.com")),
            new TestWebHostEnvironment());

        BookingReservationResult first =
            await service.TryReserveSlotAsync(
                availability.TutorAvailabilityId);
        DateTimeOffset? originalExpiry = availability.ReservationExpiresAt;
        BookingReservationResult reloaded =
            await service.TryReserveSlotAsync(
                availability.TutorAvailabilityId);

        Assert.Equal(BookingReservationStatus.Acquired, reloaded.Status);
        Assert.Equal(first.ReservationToken, reloaded.ReservationToken);
        Assert.Equal(originalExpiry, availability.ReservationExpiresAt);
    }

    [Fact]
    public async Task TryReserveSlotAsync_GrantsTwentyFiveMinuteHold()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorAvailability availability = AddTutorAvailability(
            context,
            DateTimeOffset.UtcNow.AddDays(2));
        await context.SaveChangesAsync();

        var service = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                21,
                "STUDENT21",
                "Student Name",
                "student@example.com")),
            new TestWebHostEnvironment());
        DateTimeOffset beforeReservation = DateTimeOffset.UtcNow;

        BookingReservationResult result =
            await service.TryReserveSlotAsync(
                availability.TutorAvailabilityId);

        Assert.Equal(BookingReservationStatus.Acquired, result.Status);
        Assert.NotNull(result.ReservationExpiresAt);
        Assert.InRange(
            result.ReservationExpiresAt.Value,
            beforeReservation.AddMinutes(25),
            DateTimeOffset.UtcNow.AddMinutes(25));
    }

    [Fact]
    public async Task ReleaseSlotReservationAsync_ReleasesOnlyOwnersHold()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorAvailability availability = AddTutorAvailability(
            context,
            DateTimeOffset.UtcNow.AddDays(2));
        await context.SaveChangesAsync();

        var ownerService = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                21,
                "STUDENT21",
                "First Student",
                "first@example.com")),
            new TestWebHostEnvironment());
        var otherStudentService = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                22,
                "STUDENT22",
                "Second Student",
                "second@example.com")),
            new TestWebHostEnvironment());
        BookingReservationResult reservation =
            await ownerService.TryReserveSlotAsync(
                availability.TutorAvailabilityId);

        await otherStudentService.ReleaseSlotReservationAsync(
            availability.TutorAvailabilityId,
            reservation.ReservationToken!.Value);

        Assert.Equal(21, availability.ReservedByBcUserId);

        await ownerService.ReleaseSlotReservationAsync(
            availability.TutorAvailabilityId,
            reservation.ReservationToken.Value);

        Assert.Null(availability.ReservedByBcUserId);
        Assert.Null(availability.ReservationToken);
        Assert.Null(availability.ReservationExpiresAt);

        BookingReservationResult nextReservation =
            await otherStudentService.TryReserveSlotAsync(
                availability.TutorAvailabilityId);

        Assert.Equal(
            BookingReservationStatus.Acquired,
            nextReservation.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_IdentifiesOwnersExpiredReservation()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorAvailability availability = AddTutorAvailability(
            context,
            DateTimeOffset.UtcNow.AddDays(2));
        Guid reservationToken = Guid.NewGuid();
        availability.ReservedByBcUserId = 21;
        availability.ReservationToken = reservationToken;
        availability.ReservationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        var service = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                21,
                "STUDENT21",
                "Student Name",
                "student@example.com")),
            new TestWebHostEnvironment());

        BookingCreationResult result = await service.CreateBookingAsync(
            new CreateBookingInput
            {
                TutorAvailabilityId = availability.TutorAvailabilityId,
                ReservationToken = reservationToken,
                ProgrammeModuleId = 3,
                StudyAreaId = 1,
                AcceptedTerms = true
            });

        Assert.False(result.Succeeded);
        Assert.Equal(
            BookingFailureReason.ReservationExpired,
            result.FailureReason);
        Assert.Empty(context.Bookings);
    }

    [Fact]
    public async Task CreateBookingAsync_BlocksStudentWithPendingReview()
    {
        await using ApplicationDbContext context = CreateContext();
        var completedBooking = new Booking
        {
            TutorId = 12,
            ProgrammeModuleId = 3,
            StudentBcUserId = 21,
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
                StudyAreaId = 1,
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
                StudyAreaId = 1,
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

    [Theory]
    [InlineData(PreferredTutoringMode.Online, "Main Library & Study area")]
    [InlineData(PreferredTutoringMode.FaceToFace, "Online")]
    public async Task CreateBookingAsync_RejectsLocationOutsideTutorPreference(
        PreferredTutoringMode tutoringMode,
        string studyAreaName)
    {
        await using ApplicationDbContext context = CreateContext();
        TutorAvailability availability = AddTutorAvailability(
            context,
            DateTimeOffset.UtcNow.AddDays(2),
            tutoringMode);
        context.StudyAreas.Add(new StudyArea
        {
            StudyAreaId = 1,
            Name = studyAreaName,
            DisplayOrder = 1,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new BookingService(
            context,
            new TestCurrentUserService(new CurrentUser(
                21,
                "STUDENT21",
                "Student Name",
                "student@example.com")),
            new TestWebHostEnvironment());

        BookingCreationResult result = await service.CreateBookingAsync(
            new CreateBookingInput
            {
                TutorAvailabilityId = availability.TutorAvailabilityId,
                ProgrammeModuleId = 3,
                StudyAreaId = 1,
                AcceptedTerms = true
            });

        Assert.False(result.Succeeded);
        Assert.Equal(
            "Select a location that matches this tutor's tutoring preference.",
            result.ErrorMessage);
        Assert.Empty(context.Bookings);
        Assert.Single(context.TutorAvailabilities);
    }

    [Theory]
    [InlineData(PreferredTutoringMode.Online, "Online", true)]
    [InlineData(PreferredTutoringMode.Online, "Chi study", false)]
    [InlineData(PreferredTutoringMode.FaceToFace, "Online", false)]
    [InlineData(PreferredTutoringMode.FaceToFace, "Chi study", true)]
    [InlineData(PreferredTutoringMode.Both, "Online", true)]
    [InlineData(PreferredTutoringMode.Both, "Chi study", true)]
    public void LocationPolicyMatchesTutoringPreference(
        PreferredTutoringMode tutoringMode,
        string studyAreaName,
        bool expected)
    {
        Assert.Equal(
            expected,
            TutoringModeLocationPolicy.AllowsLocation(
                tutoringMode,
                studyAreaName));
    }

    [Fact]
    public async Task CreateBookingAsync_IdentifiesExpiredAvailability()
    {
        await using ApplicationDbContext context = CreateContext();
        TutorAvailability availability = AddTutorAvailability(
            context,
            DateTimeOffset.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();

        var currentUser = new CurrentUser(
            21,
            "STUDENT21",
            "Student Name",
            "student@example.com");
        var service = new BookingService(
            context,
            new TestCurrentUserService(currentUser),
            new TestWebHostEnvironment());

        BookingCreationResult result = await service.CreateBookingAsync(
            new CreateBookingInput
            {
                TutorAvailabilityId = availability.TutorAvailabilityId,
                ProgrammeModuleId = 3,
                StudyAreaId = 1,
                AcceptedTerms = true
            });

        Assert.False(result.Succeeded);
        Assert.Equal(BookingFailureReason.Expired, result.FailureReason);
        Assert.Empty(context.Bookings);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static TutorAvailability AddTutorAvailability(
        ApplicationDbContext context,
        DateTimeOffset availableTime,
        PreferredTutoringMode tutoringMode = PreferredTutoringMode.Both)
    {
        var tutorUser = new BcUser
        {
            BcUserId = 31,
            PersonnelNumber = "TUTOR31",
            DisplayName = "Tutor Name",
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
            PreferredTutoringMode = tutoringMode,
            Status = TutorStatus.Approved,
            IsActive = true,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 3,
            ProgrammeId = 1,
            ModuleCode = "MOD101",
            ModuleName = "Module"
        };
        tutor.TutorCourseModules.Add(new TutorCourseModule
        {
            TutorId = tutor.TutorId,
            Tutor = tutor,
            ProgrammeModuleId = module.ProgrammeModuleId,
            ProgrammeModule = module,
            IsActive = true
        });
        var availability = new TutorAvailability
        {
            TutorAvailabilityId = 45,
            TutorId = tutor.TutorId,
            Tutor = tutor,
            AvailableTime = availableTime
        };
        context.TutorAvailabilities.Add(availability);
        return availability;
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
