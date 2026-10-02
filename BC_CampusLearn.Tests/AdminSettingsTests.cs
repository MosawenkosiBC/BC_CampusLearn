using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Settings;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminSettingsTests
{
    [Fact]
    public async Task SavingGeneralSettingsPersistsChangesAndAuditEntries()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        await context.SaveChangesAsync();
        GeneralModel page = CreateGeneralPage(context, BcUserRole.Admin);
        page.Input = new GeneralModel.GeneralSettingsInput
        {
            SupportEmail = "help@campus.test",
            AcademicYear = 2027,
            DateTimeFormat = "dd/MM/yyyy HH:mm",
            Announcement = "Registration closes Friday.",
            IsAnnouncementEnabled = true
        };

        IActionResult result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        PlatformSettings settings = await context.PlatformSettings.SingleAsync();
        Assert.Equal("help@campus.test", settings.SupportEmail);
        Assert.Equal(2027, settings.AcademicYear);
        Assert.True(settings.IsAnnouncementEnabled);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName == "Support email address" &&
                log.PreviousValue == "tutors@belgiumcampus.ac.za" &&
                log.NewValue == "help@campus.test");
    }

    [Fact]
    public async Task SavingBookingTermsNormalizesLinesAndCreatesAuditEntry()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        await context.SaveChangesAsync();
        BookingsModel page = CreateBookingsPage(context, BcUserRole.Admin);
        page.Input = new BookingsModel.BookingTermsInput
        {
            Terms = "Arrive prepared.\r\n\r\nRespect your tutor.  ",
            PeriodStartDate = new DateOnly(2026, 10, 1),
            PeriodEndDate = new DateOnly(2026, 10, 31),
            ReviewDeadline = new DateOnly(2026, 11, 5)
        };

        IActionResult result = await page.OnPostAsync(
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Arrive prepared.\nRespect your tutor.",
            (await context.PlatformSettings.SingleAsync())
                .BookingTermsAndConditions);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName == "Booking terms and conditions");
        PlatformSettings settings = await context.PlatformSettings.SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 1),
            settings.TutorHeadReviewPeriodStartDate);
        Assert.Equal(new DateOnly(2026, 10, 31),
            settings.TutorHeadReviewPeriodEndDate);
        Assert.Equal(new DateOnly(2026, 11, 5),
            settings.TutorHeadReviewDeadline);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName == "Tutor Head review deadline");
    }

    [Fact]
    public async Task AdministratorCanAddStudyArea()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        await context.SaveChangesAsync();
        StudyAreasModel page = CreateStudyAreasPage(context, BcUserRole.Admin);

        IActionResult result = await page.OnPostCreateAsync(
            new StudyAreasModel.StudyAreaInput
            {
                Name = "  Innovation Hub  ",
                Description = "  Located beside reception.  ",
                DisplayOrder = 9,
                IsActive = true
            },
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        StudyArea area = await context.StudyAreas.SingleAsync();
        Assert.Equal("Innovation Hub", area.Name);
        Assert.Equal("Located beside reception.", area.Description);
        Assert.Equal(9, area.DisplayOrder);
        Assert.True(area.IsActive);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.Category == "Study areas" &&
                log.SettingName == "Study area: Innovation Hub");
    }

    [Fact]
    public async Task StudyAreaFiltersSearchDescriptionAndAvailability()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        context.StudyAreas.AddRange(
            new StudyArea
            {
                StudyAreaId = 10,
                Name = "Innovation Hub",
                Description = "Located beside reception.",
                DisplayOrder = 1,
                IsActive = true
            },
            new StudyArea
            {
                StudyAreaId = 11,
                Name = "Old Lab",
                Description = "Located beside reception.",
                DisplayOrder = 2,
                IsActive = false
            },
            new StudyArea
            {
                StudyAreaId = 12,
                Name = "Library",
                Description = "Ground floor.",
                DisplayOrder = 3,
                IsActive = true
            });
        await context.SaveChangesAsync();
        StudyAreasModel page = CreateStudyAreasPage(context, BcUserRole.Admin);
        page.SearchTerm = "reception";
        page.Availability =
            [StudyAreasModel.AvailabilityFilter.Available];

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(3, page.TotalStudyAreas);
        Assert.Equal(1, page.FilteredStudyAreas);
        Assert.Equal("Innovation Hub", Assert.Single(page.StudyAreas).Name);
    }

    [Fact]
    public async Task StudyAreasArePaginatedAfterEightEntries()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        context.StudyAreas.AddRange(Enumerable.Range(1, 10).Select(index =>
            new StudyArea
            {
                StudyAreaId = index,
                Name = $"Study area {index}",
                DisplayOrder = index,
                IsActive = true
            }));
        await context.SaveChangesAsync();
        StudyAreasModel page = CreateStudyAreasPage(context, BcUserRole.Admin);
        page.StudyAreaPage = 2;

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.StudyAreas.Count);
        Assert.Equal("Study area 9", page.StudyAreas[0].Name);
    }

    [Fact]
    public async Task AdministratorCanUpdateStudyArea()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        context.StudyAreas.Add(new StudyArea
        {
            StudyAreaId = 10,
            Name = "Old name",
            Description = "Old description",
            DisplayOrder = 3,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var campusLabelStore = new StudyAreaCampusLabelStore();
        StudyAreasModel page = CreateStudyAreasPage(
            context,
            BcUserRole.Admin,
            campusLabelStore);

        IActionResult result = await page.OnPostUpdateAsync(
            10,
            new StudyAreasModel.StudyAreaInput
            {
                Name = "New name",
                Description = "New description",
                CampusLabel = "Midrand Campus",
                DisplayOrder = 4,
                IsActive = false
            },
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        StudyArea area = await context.StudyAreas.SingleAsync();
        Assert.Equal("New name", area.Name);
        Assert.Equal("New description", area.Description);
        Assert.Equal(4, area.DisplayOrder);
        Assert.False(area.IsActive);
        Assert.Equal(
            "Midrand Campus",
            await campusLabelStore.GetLabelAsync(area.StudyAreaId, area.Name));
    }

    [Fact]
    public async Task AdministratorCanDeleteUnusedStudyArea()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        context.StudyAreas.Add(new StudyArea
        {
            StudyAreaId = 10,
            Name = "Temporary location",
            DisplayOrder = 10,
            IsActive = true
        });
        await context.SaveChangesAsync();
        StudyAreasModel page = CreateStudyAreasPage(context, BcUserRole.Admin);

        IActionResult result = await page.OnPostDeleteAsync(
            10,
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(await context.StudyAreas.ToListAsync());
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.Category == "Study areas" && log.NewValue == null);
    }

    [Fact]
    public async Task StudyAreaUsedByBookingCannotBeDeleted()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        context.StudyAreas.Add(new StudyArea
        {
            StudyAreaId = 10,
            Name = "Used location",
            DisplayOrder = 10,
            IsActive = true
        });
        context.Bookings.Add(new Booking
        {
            BookingId = 20,
            StudyAreaId = 10,
            StudentName = "Student",
            Location = "Used location",
            ScheduledStartTime = DateTimeOffset.UtcNow,
            DateBooked = DateTimeOffset.UtcNow,
            Duration = SessionDuration.OneHour
        });
        await context.SaveChangesAsync();
        StudyAreasModel page = CreateStudyAreasPage(context, BcUserRole.Admin);

        IActionResult result = await page.OnPostDeleteAsync(
            10,
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(await context.StudyAreas.FindAsync(10));
        Assert.Contains("cannot be deleted", page.ErrorMessage);
    }

    [Fact]
    public async Task OnlySuperAdminCanGrantAdministrativeAccess()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "ST200",
            DisplayName = "Existing User",
            Role = BcUserRole.Student,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        UsersAccessModel page = CreateAccessPage(context, BcUserRole.Admin);
        page.Input = new UsersAccessModel.AccessInput
        {
            UserIdentifier = "ST200",
            Role = BcUserRole.Admin,
            Reason = "Support the tutoring programme"
        };

        IActionResult result = await page.OnPostGrantAccessAsync(
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(BcUserRole.Student,
            (await context.BcUsers.FindAsync(2))!.Role);
    }

    [Fact]
    public async Task SuperAdminCanGrantAccessToExistingUser()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 2,
            PersonnelNumber = "ST200",
            DisplayName = "Existing User",
            Role = BcUserRole.Student,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        UsersAccessModel page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        page.Input = new UsersAccessModel.AccessInput
        {
            UserIdentifier = "ST200",
            Role = BcUserRole.Admin,
            Reason = "Support the tutoring programme"
        };

        IActionResult result = await page.OnPostGrantAccessAsync(
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        BcUser updated = await context.BcUsers.Include(user => user.Admin)
            .SingleAsync(user => user.BcUserId == 2);
        Assert.Equal(BcUserRole.Admin, updated.Role);
        Assert.True(updated.IsAdministrativeAccessActive);
        Assert.NotNull(updated.Admin);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.Category == "Users and access" &&
                log.Reason == "Support the tutoring programme");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void AddBaseData(
        ApplicationDbContext context,
        BcUserRole role)
    {
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 1,
            PersonnelNumber = "ADMIN1",
            DisplayName = "Settings Administrator",
            Email = "admin@campus.test",
            Role = role,
            CreatedAt = DateTime.UtcNow,
            IsAdministrativeAccessActive = true
        });
        context.PlatformSettings.Add(new PlatformSettings
        {
            SupportEmail = "tutors@belgiumcampus.ac.za",
            CampusTimeZoneId = "Africa/Johannesburg",
            CurrencyCode = "ZAR",
            AcademicYear = 2026,
            AcademicSemester = "Semester 1",
            DateTimeFormat = "dd MMMM yyyy, HH:mm",
            BookingTermsAndConditions = PlatformSettings.DefaultBookingTerms,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private static GeneralModel CreateGeneralPage(
        ApplicationDbContext context,
        BcUserRole role) => new(
            context,
            CurrentUserService(role),
            TimeProvider.System,
            new SettingsAuditService(context, TimeProvider.System));

    private static BookingsModel CreateBookingsPage(
        ApplicationDbContext context,
        BcUserRole role) => new(
            context,
            CurrentUserService(role),
            TimeProvider.System,
            new SettingsAuditService(context, TimeProvider.System));

    private static StudyAreasModel CreateStudyAreasPage(
        ApplicationDbContext context,
        BcUserRole role,
        StudyAreaCampusLabelStore? campusLabelStore = null) => new(
            context,
            CurrentUserService(role),
            new SettingsAuditService(context, TimeProvider.System),
            campusLabelStore ?? new StudyAreaCampusLabelStore());

    private static UsersAccessModel CreateAccessPage(
        ApplicationDbContext context,
        BcUserRole role) => new(
            context,
            CurrentUserService(role),
            TimeProvider.System,
            new SettingsAuditService(context, TimeProvider.System));

    private static TestCurrentUserService CurrentUserService(BcUserRole role) =>
        new(new CurrentUser(
            1,
            "ADMIN1",
            "Settings Administrator",
            "admin@campus.test",
            role));

    private sealed class TestCurrentUserService(CurrentUser currentUser)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => currentUser;
    }
}
