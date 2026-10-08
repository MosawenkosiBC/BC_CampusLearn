using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using BC_CampusLearn.Pages.Administrator.Settings;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminCompensationTests
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void SeniorTutorUsesRoleSixAndHasReadableLabels()
    {
        Assert.Equal(6, (int)BcUserRole.SeniorTutor);
        Assert.Equal(7, (int)BcUserRole.Dev);
        Assert.Equal("Senior Tutor", UsersAccessModel.RoleLabel((BcUserRole)6));
        var tutor = new CompensationModel.TutorEarnings(1, "Tutor", "T1", "", "Pretoria", 1, 100m, (BcUserRole)6);
        Assert.Equal("Senior Tutor", tutor.RoleLabel);
    }

    [Fact]
    public async Task PaginationShowsElevenTutorsAndPreservesWholePeriodTotalsAndExport()
    {
        await using var context = CreateContext();
        for (int id = 1; id <= 12; id++)
        {
            var assignment = Assignment(id);
            assignment.Tutor.BcUser.DisplayName = $"Tutor {id:00}";
            assignment.Tutor.BcUser.Role = id == 12 ? BcUserRole.HeadOfTutors :
                id == 11 ? BcUserRole.SeniorTutor : BcUserRole.Tutor;
            context.TutorCourseModules.Add(assignment);
            context.Bookings.Add(Booking(id, assignment, "2026-10-01T08:00:00Z", 100m));
        }
        await context.SaveChangesAsync();
        var page = Page(context);
        page.Period = "custom";
        page.From = new(2026, 10, 1);
        page.To = new(2026, 10, 1);
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(11, page.PagedTutors.Count);
        Assert.Equal(2, page.TotalPages);
        Assert.All(page.PagedTutors.Take(10), tutor => Assert.Equal("Tutor", tutor.RoleLabel));
        Assert.Equal("Senior Tutor", page.PagedTutors[10].RoleLabel);

        page.TutorPage = 2;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(12, Assert.Single(page.PagedTutors).TutorId);
        Assert.Equal("Head of Tutor", page.PagedTutors[0].RoleLabel);
        Assert.Equal(12, page.TutorCount);
        Assert.Equal(12, page.ApprovedSessionCount);
        Assert.Equal(1200m, page.TotalEarnings);
        var export = Assert.IsType<FileContentResult>(await page.OnGetDownloadAsync(CancellationToken.None));
        using var zip = new ZipArchive(new MemoryStream(export.FileContents));
        Assert.Equal("1200", Cell(Read(zip, "xl/worksheets/sheet1.xml"), "G25").Element(S + "v")?.Value);

        page.TutorPage = 999;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, page.TutorPage);
        page.TutorPage = -1;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(1, page.TutorPage);
    }

    [Fact]
    public async Task OpeningWithoutQueryParametersDoesNotRequirePeriodAndShowsConfiguredDates()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvc();
        using var provider = services.BuildServiceProvider();
        var metadata = provider.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForProperty(typeof(CompensationModel), nameof(CompensationModel.Period));
        Assert.False(metadata.IsRequired);

        await using var context = CreateContext();
        await context.SaveChangesAsync();
        var page = Page(context);
        page.Period = null;
        await page.OnGetAsync(CancellationToken.None);
        Assert.True(page.ModelState.IsValid);
        Assert.Equal("current", page.Period);
        Assert.Equal(new DateOnly(2026, 9, 21), page.From);
        Assert.Equal(new DateOnly(2026, 10, 20), page.To);
        Assert.Equal(0, page.ApprovedSessionCount);
        Assert.Equal(0m, page.TotalEarnings);
        Assert.Equal(0, page.TutorCount);
    }

    [Fact]
    public async Task ConfiguredPeriodCountsOnlyFinalApprovalsAndIncludesBothLocalDateBoundaries()
    {
        await using var context = CreateContext();
        var first = Assignment(1);
        var second = Assignment(2);
        context.TutorCourseModules.AddRange(first, second);
        context.Bookings.AddRange(
            Booking(1, first, "2026-09-20T22:00:00Z", 125.50m),
            Booking(2, first, "2026-10-20T21:59:59Z", 150m),
            Booking(3, second, "2026-10-01T10:00:00Z", 100m),
            Booking(4, first, "2026-09-20T21:59:59Z", 100m),
            Booking(5, first, "2026-10-20T22:00:00Z", 100m),
            Booking(6, first, "2026-10-01T10:00:00Z", 100m, accepted: false),
            Booking(7, first, "2026-10-01T10:00:00Z", 100m, status: BookingStatus.Cancelled));
        var pending = Booking(8, first, "2026-10-01T10:00:00Z", 100m);
        pending.SuperAdminSessionReview = null;
        context.Bookings.Add(pending);
        await context.SaveChangesAsync();
        // An inactive tutor must still receive their historical earnings.
        first.Tutor.IsActive = false;
        await context.SaveChangesAsync();

        var page = Page(context);
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 21), page.PeriodStart);
        Assert.Equal(new DateOnly(2026, 10, 20), page.PeriodEnd);
        Assert.Equal(3, page.ApprovedSessionCount);
        Assert.Equal(2, page.TutorCount);
        Assert.Equal(375.50m, page.TotalEarnings);
        Assert.Equal(275.50m, page.Tutors.Single(tutor => tutor.TutorId == 1).Earnings);
        Assert.True(page.CanExport);
    }

    [Fact]
    public async Task CustomDatesDriveTableAndExcelDatesAndMatchingTotals()
    {
        await using var context = CreateContext();
        var assignment = Assignment(1);
        assignment.Tutor.BcUser.DisplayName = "=SUM(1,2) & Tutor";
        context.TutorCourseModules.Add(assignment);
        context.Bookings.AddRange(Booking(1, assignment, "2026-09-01T08:00:00Z", 125.50m),
            Booking(2, assignment, "2026-09-02T08:00:00Z", 200m));
        await context.SaveChangesAsync();
        var page = Page(context);
        page.Period = "custom";
        page.From = new(2026, 9, 1);
        page.To = new(2026, 9, 1);

        var result = Assert.IsType<FileContentResult>(await page.OnGetDownloadAsync(CancellationToken.None));

        Assert.Equal(125.50m, page.TotalEarnings);
        Assert.Equal("Tutor-compensation_2026-09-01_to_2026-09-01.xlsx", result.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        using var zip = new ZipArchive(new MemoryStream(result.FileContents));
        // Parse every part to verify the complete workbook is well formed.
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            XDocument.Load(stream);
        }
        var summary = Read(zip, "xl/worksheets/sheet1.xml");
        var details = Read(zip, "xl/worksheets/sheet2.xml");
        Assert.Equal("inlineStr", Cell(summary, "B11").Attribute("t")?.Value);
        Assert.Equal("=SUM(1,2) & Tutor", Cell(summary, "B11").Descendants(S + "t").Single().Value);
        Assert.Equal("125.50", Cell(summary, "G11").Element(S + "v")?.Value);
        Assert.Equal("SUM(G11:G11)", Cell(summary, "G14").Element(S + "f")?.Value);
        Assert.Equal("125.50", Cell(summary, "G14").Element(S + "v")?.Value);
        Assert.Equal("125.50", Cell(details, "I14").Element(S + "v")?.Value);
        foreach (var sheet in new[] { summary, details })
        {
            Assert.DoesNotContain(sheet.Descendants(S + "row"), row => new[] { "2", "12", "13" }.Contains(row.Attribute("r")?.Value));
            Assert.DoesNotContain(sheet.Descendants(S + "t"), text => text.Value is "Currency" or "Period basis" or "Tutor ID");
            Assert.Equal("6", Cell(sheet, "A14").Attribute("s")?.Value);
        }
        Assert.Equal("Number", Cell(summary, "A10").Descendants(S + "t").Single().Value);
        Assert.Equal("Number", Cell(details, "B10").Descendants(S + "t").Single().Value);
        var styles = Read(zip, "xl/styles.xml");
        var totalStyle = styles.Root!.Element(S + "cellXfs")!.Elements().ElementAt(6);
        Assert.Equal("2", totalStyle.Attribute("fontId")?.Value);
        Assert.Equal("3", totalStyle.Attribute("fillId")?.Value);
        Assert.Equal("FF000000", styles.Root.Element(S + "fills")!.Elements().ElementAt(3)
            .Descendants(S + "fgColor").Single().Attribute("rgb")?.Value);
        double startDate = double.Parse(Cell(summary, "B3").Element(S + "v")!.Value, CultureInfo.InvariantCulture);
        Assert.Equal(new DateTime(2026, 9, 1), DateTime.FromOADate(startDate));
        Assert.Equal(Cell(summary, "B3").Element(S + "v")!.Value, Cell(summary, "B4").Element(S + "v")!.Value);
        Assert.Equal("5", Cell(details, "G11").Attribute("s")?.Value);
        Assert.Equal(new DateTime(2026, 9, 1, 10, 0, 0), DateTime.FromOADate(
            double.Parse(Cell(details, "G11").Element(S + "v")!.Value, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task SuperAdminApprovalSavesRateAndRepeatedApprovalDoesNotDoubleCountOrReprice()
    {
        await using var context = CreateContext();
        var assignment = Assignment(1);
        var head = new BcUser { BcUserId = 901, DisplayName = "Tutor Head", Role = BcUserRole.HeadOfTutors };
        context.BcUsers.Add(head);
        context.TutorCourseModules.Add(assignment);
        var booking = Booking(1, assignment, "2026-10-01T08:00:00Z", null);
        booking.SuperAdminSessionReview = null;
        booking.StudentEvaluation = new();
        booking.TutorEvaluation = new();
        booking.AdminSessionReview = new() { ReviewerBcUserId = 900, EvidenceSupportsApproval = true };
        booking.SessionReviews.Add(new() { ReviewerBcUserId = 901, Reviewer = head, Decision = "Approve" });
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        var details = new BC_CampusLearn.Pages.Administrator.Tutors.SessionDetailsModel(context,
            null!, new UserService(BcUserRole.SuperAdmin), TimeProvider.System);

        Assert.IsType<RedirectToPageResult>(await details.OnPostSuperAdminReviewAsync(1, true, CancellationToken.None));
        Assert.Equal(125.50m, (await context.SuperAdminSessionReviews.SingleAsync()).CompensationAmount);
        (await context.PlatformSettings.SingleAsync()).TutorPaymentAmount = 200m;
        await context.SaveChangesAsync();
        await details.OnPostSuperAdminReviewAsync(1, true, CancellationToken.None);
        var page = Page(context);
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(1, page.ApprovedSessionCount);
        Assert.Equal(125.50m, page.TotalEarnings);
        await details.OnPostSuperAdminReviewAsync(1, false, CancellationToken.None);
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(0, page.ApprovedSessionCount);
        Assert.Equal(0m, page.TotalEarnings);
    }

    [Fact]
    public async Task RecurringPeriodAdvancesAndOneTimePeriodStaysFixed()
    {
        await using var context = CreateContext();
        await context.SaveChangesAsync();
        var november = Page(context, now: "2026-11-01T08:00:00Z");
        await november.OnGetAsync(CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 10, 21), november.PeriodStart);
        Assert.Equal(new DateOnly(2026, 11, 20), november.PeriodEnd);
        var settings = await context.PlatformSettings.SingleAsync();
        settings.IsAdminSessionReviewDeadlineRecurring = false;
        await context.SaveChangesAsync();
        await november.OnGetAsync(CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 9, 21), november.PeriodStart);
        Assert.Equal(new DateOnly(2026, 10, 20), november.PeriodEnd);
    }

    [Theory]
    [InlineData(BcUserRole.Admin)]
    [InlineData(BcUserRole.HeadOfTutors)]
    [InlineData(BcUserRole.Tutor)]
    public async Task OtherRolesCannotViewOrDownloadCompensation(BcUserRole role)
    {
        await using var context = CreateContext();
        var page = Page(context, role);
        Assert.IsType<ForbidResult>(await page.OnGetAsync(CancellationToken.None));
        Assert.IsType<ForbidResult>(await page.OnGetDownloadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InvalidPeriodCannotExportAndEmptyPeriodExportsZeroTotals()
    {
        await using var context = CreateContext();
        await context.SaveChangesAsync();
        var page = Page(context);
        page.Period = "custom";
        page.From = new(2026, 10, 20);
        page.To = new(2026, 10, 1);
        Assert.IsType<PageResult>(await page.OnGetDownloadAsync(CancellationToken.None));
        Assert.False(page.ModelState.IsValid);
        var empty = Page(context);
        Assert.IsType<FileContentResult>(await empty.OnGetDownloadAsync(CancellationToken.None));
        Assert.Equal(0m, empty.TotalEarnings);
        Assert.Equal(0, empty.TutorCount);
    }

    [Fact]
    public async Task MissingRateBlocksFinanceExportAndSettingRateDoesNotRepriceExistingEarnings()
    {
        await using var context = CreateContext();
        var assignment = Assignment(1);
        context.TutorCourseModules.Add(assignment);
        context.Bookings.AddRange(Booking(1, assignment, "2026-10-01T08:00:00Z", null),
            Booking(2, assignment, "2026-10-01T08:00:00Z", 90m));
        await context.SaveChangesAsync();
        var page = Page(context);
        Assert.IsType<PageResult>(await page.OnGetDownloadAsync(CancellationToken.None));
        Assert.Null(page.TotalEarnings);
        Assert.Equal(1, page.UnpricedSessionCount);

        var settingsPage = new TutorPaymentsModel(context, new UserService(BcUserRole.SuperAdmin),
            TimeProvider.System, new SettingsAuditService(context, TimeProvider.System));
        settingsPage.Input = new() { TutorPaymentAmount = 150m, SeniorTutorPaymentAmount = 200m, TutorHeadPaymentAmount = 300m };
        await settingsPage.OnPostAsync(CancellationToken.None);
        var reloaded = Page(context);
        await reloaded.OnGetAsync(CancellationToken.None);
        Assert.Equal(240m, reloaded.TotalEarnings);
        settingsPage.Input.TutorPaymentAmount = 200m;
        await settingsPage.OnPostAsync(CancellationToken.None);
        await reloaded.OnGetAsync(CancellationToken.None);
        Assert.Equal(240m, reloaded.TotalEarnings);
    }

    private static XDocument Read(ZipArchive zip, string path)
    {
        using var stream = zip.GetEntry(path)!.Open();
        return XDocument.Load(stream);
    }
    private static XElement Cell(XDocument document, string address) =>
        document.Descendants(S + "c").Single(cell => (string?)cell.Attribute("r") == address);
    private static CompensationModel Page(ApplicationDbContext context,
        BcUserRole role = BcUserRole.SuperAdmin, string now = "2026-10-07T08:00:00Z") =>
        new(context, new UserService(role), new FixedTime(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture)));
    private sealed class UserService(BcUserRole role) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => new(900, "SA900", "Super Admin", null, role);
    }
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private static ApplicationDbContext CreateContext()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.PlatformSettings.Add(new PlatformSettings
        {
            AdminSessionReviewPeriodStartDate = new(2026, 9, 21),
            AdminSessionReviewDeadline = new(2026, 10, 20),
            UseLastDayOfMonthForAdminSessionReviewDeadline = false,
            TutorPaymentAmount = 125.50m
        });
        context.BcUsers.Add(new BcUser { BcUserId = 900, DisplayName = "Super Admin", Role = BcUserRole.SuperAdmin });
        return context;
    }
    private static TutorCourseModule Assignment(int id) => new()
    {
        TutorId = id, ProgrammeModuleId = id, IsActive = true,
        Tutor = new Tutor
        {
            TutorId = id, BcUserId = id, ProgrammeId = id, Programme = new() { Id = id, Name = "IT" },
            BcUser = new() { BcUserId = id, DisplayName = $"Tutor {id}", PersonnelNumber = $"T{id}", Email = $"t{id}@campus.test" },
            ReasonForTutoring = "Help", TeachingStyle = "Practical", PreviousTutoringExperience = "None",
            CampusOfStudy = "Pretoria", DemonstrationVideoUrl = "video", IsActive = true, Status = TutorStatus.Approved
        },
        ProgrammeModule = new() { ProgrammeModuleId = id, ProgrammeId = id, ModuleCode = $"MOD{id}", ModuleName = "Programming" }
    };
    private static Booking Booking(int id, TutorCourseModule assignment, string date, decimal? amount,
        bool accepted = true, BookingStatus status = BookingStatus.Completed) => new()
    {
        BookingId = id, TutorId = assignment.TutorId, ProgrammeModuleId = assignment.ProgrammeModuleId,
        TutorCourseModule = assignment, ProgrammeModule = assignment.ProgrammeModule, Status = status,
        Duration = SessionDuration.OneHour, Location = "Online", StudentName = "Student",
        ScheduledStartTime = DateTimeOffset.Parse(date, CultureInfo.InvariantCulture).AddHours(-1),
        CompletedAt = DateTimeOffset.Parse(date, CultureInfo.InvariantCulture),
        SuperAdminSessionReview = new()
        {
            BookingId = id, ReviewerBcUserId = 900, IsAccepted = accepted,
            RecordedAt = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero), CompensationAmount = amount
        }
    };
}
