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
    [Theory]
    [InlineData("en-ZA")]
    [InlineData("af-ZA")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void PaymentValidationHandlesRegionalDecimalSeparators(string cultureName)
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            var input = new TutorPaymentsModel.PaymentSettingsInput
            {
                TutorPaymentAmount = 125.50m,
                TutorHeadPaymentAmount = 999999999.99m
            };
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            bool Validate() => System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                input, new System.ComponentModel.DataAnnotations.ValidationContext(input),
                results, validateAllProperties: true);

            Assert.True(Validate());
            input.TutorPaymentAmount = -1m;
            input.TutorHeadPaymentAmount = 1000000000m;
            Assert.False(Validate());
            Assert.Contains(results, result => result.MemberNames.Contains("TutorPaymentAmount"));
            Assert.Contains(results, result => result.MemberNames.Contains("TutorHeadPaymentAmount"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task SuperAdminCanSaveAndReloadSeparatePaymentAmounts()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        await context.SaveChangesAsync();
        var page = CreatePaymentPage(context, BcUserRole.SuperAdmin);
        page.Input = new() { TutorPaymentAmount = 125.50m, TutorHeadPaymentAmount = 350m };

        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(CancellationToken.None));
        context.ChangeTracker.Clear();
        var reloaded = CreatePaymentPage(context, BcUserRole.SuperAdmin);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(
            await reloaded.OnGetAsync(CancellationToken.None));
        Assert.Equal(125.50m, reloaded.Input.TutorPaymentAmount);
        Assert.Equal(350m, reloaded.Input.TutorHeadPaymentAmount);
        Assert.Equal("ZAR", reloaded.CurrencyCode);
        Assert.Equal(2, await context.SettingAuditLogs.CountAsync());
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName == "Tutor head payment amount" && log.NewValue == "350.00");

        await reloaded.OnPostAsync(CancellationToken.None);
        Assert.Equal(2, await context.SettingAuditLogs.CountAsync());
    }

    [Theory]
    [InlineData(BcUserRole.Admin)]
    [InlineData(BcUserRole.Dev)]
    [InlineData(BcUserRole.HeadOfTutors)]
    public async Task PaymentSettingsRejectOtherRolesForReadsAndWrites(BcUserRole role)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, role);
        await context.SaveChangesAsync();
        var page = CreatePaymentPage(context, role);
        page.Input = new() { TutorPaymentAmount = 100m, TutorHeadPaymentAmount = 200m };

        Assert.IsType<ForbidResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.IsType<ForbidResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.Null((await context.PlatformSettings.SingleAsync()).TutorPaymentAmount);
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("-1")]
    [InlineData("1000000000")]
    [InlineData("1.001")]
    public async Task InvalidPaymentAmountDoesNotSaveEitherRole(string? value)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        await context.SaveChangesAsync();
        var page = CreatePaymentPage(context, BcUserRole.SuperAdmin);
        page.Input = new()
        {
            TutorPaymentAmount = 100m,
            TutorHeadPaymentAmount = value is null ? null : decimal.Parse(value,
                System.Globalization.CultureInfo.InvariantCulture)
        };

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(
            await page.OnPostAsync(CancellationToken.None));
        Assert.False(page.ModelState.IsValid);
        Assert.Null((await context.PlatformSettings.SingleAsync()).TutorPaymentAmount);
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
    }

    private static TutorPaymentsModel CreatePaymentPage(
        ApplicationDbContext context, BcUserRole role) => new(
            context, CurrentUserService(role), TimeProvider.System,
            new SettingsAuditService(context, TimeProvider.System));

    [Fact]
    public async Task SavingGeneralSettingsPersistsChangesAndAuditEntries()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        await context.SaveChangesAsync();
        GeneralModel page = CreateGeneralPage(context, BcUserRole.Admin);
        page.Input = new GeneralModel.GeneralSettingsInput
        {
            AcademicYear = 2027,
            DateTimeFormat = "dd/MM/yyyy HH:mm",
            Announcement = "Registration closes Friday.",
            IsAnnouncementEnabled = true
        };

        IActionResult result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        PlatformSettings settings = await context.PlatformSettings.SingleAsync();
        Assert.Equal("tutors@belgiumcampus.ac.za", settings.SupportEmail);
        Assert.Equal(2027, settings.AcademicYear);
        Assert.True(settings.IsAnnouncementEnabled);
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
            SupportEmail = "help@campus.test",
            Terms = "Arrive prepared.\r\n\r\nRespect your tutor.  ",
            ReviewDeadline = new DateOnly(2026, 11, 5),
            IsReviewDeadlineRecurring = true
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
        Assert.Equal(new DateOnly(2026, 10, 6),
            settings.TutorHeadReviewPeriodStartDate);
        Assert.Equal(new DateOnly(2026, 11, 5),
            settings.TutorHeadReviewPeriodEndDate);
        Assert.Equal(new DateOnly(2026, 11, 5),
            settings.TutorHeadReviewDeadline);
        Assert.True(settings.IsTutorHeadReviewDeadlineRecurring);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName == "Tutor Head review deadline");
    }

    [Fact]
    public async Task AdministratorCanMakeReviewDeadlineOneTime()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        await context.SaveChangesAsync();
        BookingsModel page = CreateBookingsPage(context, BcUserRole.Admin);
        page.Input = new BookingsModel.BookingTermsInput
        {
            Terms = PlatformSettings.DefaultBookingTerms,
            ReviewDeadline = new DateOnly(2026, 10, 5),
            IsReviewDeadlineRecurring = false
        };

        IActionResult result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.False((await context.PlatformSettings.SingleAsync())
            .IsTutorHeadReviewDeadlineRecurring);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName ==
                "Tutor Head review deadline recurrence" &&
                log.NewValue == "One time");
    }

    [Fact]
    public async Task AdministratorCanRepeatDeadlineOnLastDayOfMonth()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        await context.SaveChangesAsync();
        BookingsModel page = CreateBookingsPage(context, BcUserRole.Admin);
        page.Input = new BookingsModel.BookingTermsInput
        {
            Terms = PlatformSettings.DefaultBookingTerms,
            ReviewDeadline = new DateOnly(2026, 11, 5),
            IsReviewDeadlineRecurring = true,
            UseLastDayOfMonth = true
        };

        IActionResult result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        PlatformSettings settings = await context.PlatformSettings.SingleAsync();
        Assert.Equal(
            new DateOnly(2026, 11, 30),
            settings.TutorHeadReviewDeadline);
        Assert.Equal(
            new DateOnly(2026, 11, 1),
            settings.TutorHeadReviewPeriodStartDate);
        Assert.Equal(
            new DateOnly(2026, 11, 30),
            settings.TutorHeadReviewPeriodEndDate);
        Assert.True(settings.IsTutorHeadReviewDeadlineRecurring);
        Assert.True(settings
            .UseLastDayOfMonthForTutorHeadReviewDeadline);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName ==
                "Tutor Head review deadline monthly rule" &&
                log.NewValue == "Last day of the month");
    }

    [Fact]
    public async Task AdministratorCanConfigureRecurringAdminReviewDeadline()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.Admin);
        await context.SaveChangesAsync();
        PlatformSettings previousSettings =
            await context.PlatformSettings.SingleAsync();
        previousSettings.AdminSessionReviewDeadline =
            new DateOnly(2026, 10, 10);
        previousSettings.UseLastDayOfMonthForAdminSessionReviewDeadline =
            false;
        await context.SaveChangesAsync();
        BookingsModel page = CreateBookingsPage(context, BcUserRole.Admin);
        page.Input = new BookingsModel.BookingTermsInput
        {
            Terms = PlatformSettings.DefaultBookingTerms,
            ReviewDeadline = new DateOnly(2026, 10, 5),
            IsReviewDeadlineRecurring = true,
            AdminReviewPeriodStartDate = new DateOnly(2026, 10, 21),
            AdminReviewDeadline = new DateOnly(2026, 11, 5),
            IsAdminReviewDeadlineRecurring = true,
            UseLastDayOfMonthForAdminReview = true
        };

        IActionResult result = await page.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        PlatformSettings settings = await context.PlatformSettings.SingleAsync();
        Assert.Equal(
            new DateOnly(2026, 11, 30),
            settings.AdminSessionReviewDeadline);
        Assert.Equal(
            new DateOnly(2026, 10, 21),
            settings.AdminSessionReviewPeriodStartDate);
        Assert.True(settings.IsAdminSessionReviewDeadlineRecurring);
        Assert.True(settings
            .UseLastDayOfMonthForAdminSessionReviewDeadline);
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName ==
                "Administrator review deadline monthly rule" &&
                log.NewValue == "Last day of the month");
        Assert.Contains(await context.SettingAuditLogs.ToListAsync(),
            log => log.SettingName ==
                "Administrator review period start date" &&
                log.NewValue == "2026-10-21");
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

    [Fact]
    public async Task AdministrativeUsersCanBeFilteredBySearchRoleAndStatus()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.AddRange(
            new BcUser
            {
                BcUserId = 2,
                PersonnelNumber = "TARGET1",
                Email = "target@campus.test",
                DisplayName = "Target Administrator",
                Role = BcUserRole.SuperAdmin,
                IsAdministrativeAccessActive = false,
                CreatedAt = DateTime.UtcNow
            },
            new BcUser
            {
                BcUserId = 3,
                PersonnelNumber = "OTHER1",
                Email = "other@campus.test",
                DisplayName = "Other Administrator",
                Role = BcUserRole.Admin,
                IsAdministrativeAccessActive = true,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        UsersAccessModel page = CreateAccessPage(
            context,
            BcUserRole.SuperAdmin);
        page.SearchTerm = "target";
        page.Roles = [BcUserRole.SuperAdmin];
        page.AccessStatus =
            [UsersAccessModel.AccessStatusFilter.Inactive];

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(3, page.TotalAccessUsers);
        Assert.Equal(1, page.FilteredAccessUsers);
        Assert.Equal(
            "Target Administrator",
            Assert.Single(page.AccessUsers).DisplayName);
    }

    [Theory]
    [InlineData("st20")]
    [InlineData("USER@CAMPUS")]
    [InlineData("  st20  ")]
    public async Task AccessLookupFindsTutorsByNumberOrEmailAndIndicatesAssignedAccess(string term)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.AddRange(
            new BcUser { BcUserId = 2, PersonnelNumber = "ST200", Email = "user@campus.test", DisplayName = "Tutor", Role = BcUserRole.Tutor, Tutor = AccessTutor(2) },
            new BcUser { BcUserId = 3, PersonnelNumber = "ST201", Email = "other-user@campus.test", DisplayName = "Administrator", Role = BcUserRole.Admin, Tutor = AccessTutor(3), IsAdministrativeAccessActive = false },
            new BcUser { BcUserId = 4, PersonnelNumber = "ST202", Email = "dev-user@campus.test", DisplayName = "Developer", Role = BcUserRole.Dev });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);

        var result = Assert.IsType<JsonResult>(await page.OnGetSearchUsersAsync(term, CancellationToken.None));
        var matches = Assert.IsAssignableFrom<IEnumerable<UsersAccessModel.AccessUserMatch>>(result.Value).ToList();

        Assert.Equal(new[] { 3, 2 }, matches.Select(user => user.UserId));
        Assert.Equal("Admin", matches[0].Role);
        Assert.Equal("Tutor", matches[1].Role);
        Assert.True(matches[0].HasAdministrativeAccess);
        Assert.False(matches[0].IsActive);
        Assert.False(matches[1].HasAdministrativeAccess);
    }

    [Fact]
    public async Task AccessLookupRejectsNonSuperAdminsAndDoesNotReturnCurrentUser()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        await context.SaveChangesAsync();
        var admin = CreateAccessPage(context, BcUserRole.Admin);
        Assert.IsType<ForbidResult>(await admin.OnGetSearchUsersAsync("admin", CancellationToken.None));
        var superAdmin = CreateAccessPage(context, BcUserRole.SuperAdmin);
        foreach (string term in new[] { "admin", "a", "" })
        {
            var result = Assert.IsType<JsonResult>(await superAdmin.OnGetSearchUsersAsync(term, CancellationToken.None));
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<UsersAccessModel.AccessUserMatch>>(result.Value));
        }
    }

    [Fact]
    public async Task GrantAccessUsesSelectedAccountAndRejectsDeveloperAccount()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.AddRange(
            new BcUser { BcUserId = 2, PersonnelNumber = "ST200", DisplayName = "Selected Student", Role = BcUserRole.Student },
            new BcUser { BcUserId = 3, PersonnelNumber = "DEV1", DisplayName = "Developer", Role = BcUserRole.Dev });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        page.Input = new() { UserId = 2, UserIdentifier = "ST200", Role = BcUserRole.Admin, Reason = "Support the tutoring programme" };
        Assert.IsType<RedirectToPageResult>(await page.OnPostGrantAccessAsync(CancellationToken.None));
        Assert.Equal(BcUserRole.Admin, (await context.BcUsers.FindAsync(2))!.Role);

        page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        page.Input = new() { UserId = 3, UserIdentifier = "DEV1", Role = BcUserRole.Admin, Reason = "Support the tutoring programme" };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnPostGrantAccessAsync(CancellationToken.None));
        Assert.False(page.ModelState.IsValid);
        Assert.Equal(BcUserRole.Dev, (await context.BcUsers.FindAsync(3))!.Role);
    }

    [Theory]
    [InlineData(BcUserRole.Admin)]
    [InlineData(BcUserRole.Dev)]
    [InlineData(BcUserRole.HeadOfTutors)]
    [InlineData(BcUserRole.Student)]
    public async Task UsersAccessPageRejectsUsersWhoAreNotSuperAdmins(BcUserRole role)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, role);
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, role);

        Assert.IsType<ForbidResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.Empty(page.AccessUsers);
    }

    [Fact]
    public async Task UsersAccessPageAllowsSuperAdmins()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.True(page.CanManageAccess);
        Assert.Single(page.AccessUsers);
    }

    [Fact]
    public async Task AccessLookupIncludesUnnumberedAccountsAndExcludesOtherStudentsAndUnlistedTutors()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        var inactive = AccessTutor(5);
        inactive.IsActive = false;
        var pending = AccessTutor(6);
        pending.Status = TutorStatus.Pending;
        var applicant = AccessTutor(7);
        applicant.ApplicationStage = TutorApplicationStage.Submitted;
        context.BcUsers.AddRange(
            new BcUser { BcUserId = 2, Email = "match-admin@campus.test", DisplayName = "A", Role = BcUserRole.Admin },
            new BcUser { BcUserId = 3, PersonnelNumber = "  ", Email = "match-user@campus.test", DisplayName = "B", Role = BcUserRole.Student },
            new BcUser { BcUserId = 4, PersonnelNumber = "ST204", Email = "match-student@campus.test", DisplayName = "C", Role = BcUserRole.Student },
            new BcUser { BcUserId = 5, PersonnelNumber = "ST205", Email = "match-inactive@campus.test", DisplayName = "D", Role = BcUserRole.Tutor, Tutor = inactive },
            new BcUser { BcUserId = 6, PersonnelNumber = "ST206", Email = "match-pending@campus.test", DisplayName = "E", Role = BcUserRole.Tutor, Tutor = pending },
            new BcUser { BcUserId = 7, PersonnelNumber = "ST207", Email = "match-applicant@campus.test", DisplayName = "F", Role = BcUserRole.Tutor, Tutor = applicant });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        var result = Assert.IsType<JsonResult>(await page.OnGetSearchUsersAsync("match", CancellationToken.None));
        var matches = Assert.IsAssignableFrom<IEnumerable<UsersAccessModel.AccessUserMatch>>(result.Value);
        Assert.Equal(new[] { 2, 3 }, matches.Select(user => user.UserId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Help")]
    public async Task GrantAccessAcceptsOptionalReason(string? reason)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.Add(new BcUser { BcUserId = 2, Email = "new@campus.test", DisplayName = "New Admin", Role = BcUserRole.Student });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        page.Input = new() { UserId = 2, UserIdentifier = "new@campus.test", Role = BcUserRole.Admin, Reason = reason };
        var validation = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(page.Input,
            new System.ComponentModel.DataAnnotations.ValidationContext(page.Input), validation, true));
        Assert.IsType<RedirectToPageResult>(await page.OnPostGrantAccessAsync(CancellationToken.None));
        Assert.Equal(BcUserRole.Admin, (await context.BcUsers.FindAsync(2))!.Role);
        Assert.Equal(string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), (await context.SettingAuditLogs.SingleAsync()).Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModalRemovesSelectedAdministrativeAccessWithoutReason(bool tutor)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.Add(new BcUser { BcUserId = 2, Email = "existing@campus.test", DisplayName = "Existing Admin", Role = BcUserRole.Admin, Tutor = tutor ? AccessTutor(2) : null });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        page.Input = new() { UserId = 2, UserIdentifier = "existing@campus.test" };
        Assert.IsType<RedirectToPageResult>(await page.OnPostRemoveSelectedAccessAsync(CancellationToken.None));
        Assert.Equal(tutor ? BcUserRole.Tutor : BcUserRole.Student, (await context.BcUsers.FindAsync(2))!.Role);
        Assert.Null((await context.SettingAuditLogs.SingleAsync()).Reason);
    }

    [Fact]
    public async Task ModalRemovalPreservesSuperAdminProtection()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        (await context.BcUsers.FindAsync(1))!.IsAdministrativeAccessActive = false;
        context.BcUsers.Add(new BcUser { BcUserId = 2, Email = "last@campus.test", DisplayName = "Last Super Admin", Role = BcUserRole.SuperAdmin });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        page.Input = new() { UserId = 2, UserIdentifier = "last@campus.test" };
        await page.OnPostRemoveSelectedAccessAsync(CancellationToken.None);
        Assert.Equal(BcUserRole.SuperAdmin, (await context.BcUsers.FindAsync(2))!.Role);
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
        var admin = CreateAccessPage(context, BcUserRole.Admin);
        Assert.IsType<ForbidResult>(await admin.OnPostRemoveSelectedAccessAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(BcUserRole.HeadOfTutors, true, BcUserRole.Tutor)]
    [InlineData(BcUserRole.HeadOfTutors, false, BcUserRole.Student)]
    [InlineData(BcUserRole.Admin, true, BcUserRole.Tutor)]
    [InlineData(BcUserRole.Admin, false, BcUserRole.Student)]
    [InlineData(BcUserRole.SuperAdmin, false, BcUserRole.Admin)]
    public async Task DemotionRestoresTheExpectedRole(BcUserRole role, bool isTutor, BcUserRole expected)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.Add(new BcUser { BcUserId = 2, Email = "target@campus.test", DisplayName = "Target", Role = role, Tutor = isTutor ? AccessTutor(2) : null });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        Assert.IsType<RedirectToPageResult>(await page.OnPostDemoteAsync(2, null, CancellationToken.None));
        var target = (await context.BcUsers.FindAsync(2))!;
        Assert.Equal(expected, target.Role);
        Assert.True(target.IsAdministrativeAccessActive);
        var audit = await context.SettingAuditLogs.SingleAsync();
        Assert.Equal(role.ToString(), audit.PreviousValue);
        Assert.Equal(expected.ToString(), audit.NewValue);
        await page.OnGetAsync(CancellationToken.None);
        if (expected != BcUserRole.Admin) Assert.DoesNotContain(page.AccessUsers, user => user.BcUserId == 2);
    }

    [Theory]
    [InlineData(TutorStatus.Suspended)]
    [InlineData(TutorStatus.Deregistered)]
    public async Task DemotingFormerTutorHeadsDoesNotRestoreTutorAccess(TutorStatus status)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        var tutor = AccessTutor(2);
        tutor.Status = status;
        tutor.IsActive = false;
        context.BcUsers.Add(new BcUser { BcUserId = 2, DisplayName = "Former tutor", Role = BcUserRole.HeadOfTutors, Tutor = tutor });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        await page.OnPostDemoteAsync(2, null, CancellationToken.None);
        Assert.Equal(BcUserRole.Student, (await context.BcUsers.FindAsync(2))!.Role);
    }

    [Fact]
    public async Task PromotionOnlyAllowsHigherRolesAndCreatesAdminProfile()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.Add(new BcUser { BcUserId = 2, DisplayName = "Tutor", Role = BcUserRole.Tutor, Tutor = AccessTutor(2) });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        await page.OnPostPromoteAsync(2, BcUserRole.Tutor, null, CancellationToken.None);
        Assert.Equal(BcUserRole.Tutor, (await context.BcUsers.FindAsync(2))!.Role);
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
        await page.OnPostPromoteAsync(2, BcUserRole.Admin, null, CancellationToken.None);
        var target = await context.BcUsers.Include(user => user.Admin).SingleAsync(user => user.BcUserId == 2);
        Assert.Equal(BcUserRole.Admin, target.Role);
        Assert.NotNull(target.Admin);
        await page.OnPostPromoteAsync(2, BcUserRole.SuperAdmin, null, CancellationToken.None);
        Assert.Equal(BcUserRole.SuperAdmin, target.Role);
        await page.OnPostPromoteAsync(2, BcUserRole.Admin, null, CancellationToken.None);
        Assert.Equal(BcUserRole.SuperAdmin, target.Role);
        Assert.Equal(2, await context.SettingAuditLogs.CountAsync());
    }

    [Fact]
    public async Task PromotionAndDemotionProtectOwnDeveloperAndLastSuperAdminAccounts()
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.Add(new BcUser { BcUserId = 2, DisplayName = "Developer", Role = BcUserRole.Dev });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        await page.OnPostDemoteAsync(1, null, CancellationToken.None);
        Assert.Equal(BcUserRole.SuperAdmin, (await context.BcUsers.FindAsync(1))!.Role);
        Assert.IsType<ForbidResult>(await page.OnPostDemoteAsync(2, null, CancellationToken.None));
        Assert.IsType<ForbidResult>(await page.OnPostPromoteAsync(2, BcUserRole.SuperAdmin, null, CancellationToken.None));
        var admin = CreateAccessPage(context, BcUserRole.Admin);
        Assert.IsType<ForbidResult>(await admin.OnPostDemoteAsync(2, null, CancellationToken.None));
        Assert.IsType<ForbidResult>(await admin.OnPostPromoteAsync(2, BcUserRole.SuperAdmin, null, CancellationToken.None));
        (await context.BcUsers.FindAsync(1))!.IsAdministrativeAccessActive = false;
        context.BcUsers.Add(new BcUser { BcUserId = 3, DisplayName = "Last Super Admin", Role = BcUserRole.SuperAdmin });
        await context.SaveChangesAsync();
        await page.OnPostDemoteAsync(3, null, CancellationToken.None);
        Assert.Equal(BcUserRole.SuperAdmin, (await context.BcUsers.FindAsync(3))!.Role);
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(BcUserRole.Admin)]
    [InlineData(BcUserRole.SuperAdmin)]
    public async Task TutorHeadsCannotBePromotedThroughAnyAccessHandler(BcUserRole role)
    {
        await using ApplicationDbContext context = CreateContext();
        AddBaseData(context, BcUserRole.SuperAdmin);
        context.BcUsers.Add(new BcUser { BcUserId = 2, Email = "head@campus.test", DisplayName = "Tutor Head", Role = BcUserRole.HeadOfTutors, Tutor = AccessTutor(2) });
        await context.SaveChangesAsync();
        var page = CreateAccessPage(context, BcUserRole.SuperAdmin);
        await page.OnPostPromoteAsync(2, role, null, CancellationToken.None);
        Assert.Equal(BcUserRole.HeadOfTutors, (await context.BcUsers.FindAsync(2))!.Role);
        await page.OnPostChangeRoleAsync(2, role, "Role update", CancellationToken.None);
        Assert.Equal(BcUserRole.HeadOfTutors, (await context.BcUsers.FindAsync(2))!.Role);
        page.Input = new() { UserId = 2, UserIdentifier = "head@campus.test", Role = role };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnPostGrantAccessAsync(CancellationToken.None));
        Assert.Equal(BcUserRole.HeadOfTutors, (await context.BcUsers.FindAsync(2))!.Role);
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
    }

    private static Tutor AccessTutor(int id) => new()
    {
        TutorId = id, BcUserId = id, ProgrammeId = 1,
        ApplicationStage = TutorApplicationStage.Placement, IsActive = true, Status = TutorStatus.Approved,
        ReasonForTutoring = "Help students", TeachingStyle = "Practical",
        PreviousTutoringExperience = "None", CampusOfStudy = "Main", DemonstrationVideoUrl = "video"
    };

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
