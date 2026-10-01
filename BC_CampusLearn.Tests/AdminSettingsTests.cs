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
