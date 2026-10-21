using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Settings;
using BC_CampusLearn.Services.Settings;
using BC_CampusLearn.Services.Teams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TeamsOrganizerSettingsTests
{
    private const string ObjectId = "33333333-3333-3333-3333-333333333333";
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class User(BcUserRole role) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => new(900, "ADMIN", "Admin", "admin@example.com", role);
    }

    [Theory]
    [InlineData(BcUserRole.Admin)]
    [InlineData(BcUserRole.HeadOfTutors)]
    [InlineData(BcUserRole.Tutor)]
    public async Task OnlySuperAdminCanSaveOrTestAccountEvenWhenPostingDirectly(BcUserRole role)
    {
        await using var db = Context();
        var page = CreatePage(db, role);
        Assert.False(page.CanManageTeams);
        Assert.IsType<ForbidResult>(await page.OnPostSaveTeamsOrganizerAsync(default));
        Assert.IsType<ForbidResult>(await page.OnPostTestTeamsOrganizerAsync(default));
        Assert.Empty(db.PlatformSettings);
        Assert.Empty(db.SettingAuditLogs);
    }

    [Fact]
    public async Task SuperAdminCanSaveAccountAndChangesAreAuditedAndOverrideEnvironment()
    {
        await using var db = Context();
        db.BcUsers.Add(new() { BcUserId = 900, Role = BcUserRole.SuperAdmin });
        await db.SaveChangesAsync();
        var page = new BookingsModel(db, new User(BcUserRole.SuperAdmin), TimeProvider.System, new SettingsAuditService(db, TimeProvider.System))
        {
            TeamsOrganizer = new() { Email = "mzalaconnect@belgiumcampus.ac.za", ObjectId = ObjectId }
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostSaveTeamsOrganizerAsync(default));
        var account = await new TeamsOrganizerSettingsStore(db, Options.Create(new TeamsMeetingOptions
        {
            SharedOrganizerEmail = "old@example.com", SharedOrganizerObjectId = Guid.NewGuid().ToString()
        })).GetAsync(default);
        Assert.Equal(ObjectId, account.ValidatedObjectId());
        Assert.Equal("mzalaconnect@belgiumcampus.ac.za", account.Email);
        Assert.Equal(2, db.SettingAuditLogs.Count());
        Assert.All(db.SettingAuditLogs, log => Assert.Equal(900, log.ChangedByBcUserId));
    }

    [Fact]
    public async Task PasswordLikeValueIsRejectedWithoutPersistingIt()
    {
        await using var db = Context();
        var page = new BookingsModel(db, new User(BcUserRole.SuperAdmin), TimeProvider.System, new SettingsAuditService(db, TimeProvider.System))
        {
            TeamsOrganizer = new() { Email = "mzalaconnect@belgiumcampus.ac.za", ObjectId = "not-an-object-id" }
        };
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await page.OnPostSaveTeamsOrganizerAsync(default));
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(db.PlatformSettings);
        Assert.Empty(db.SettingAuditLogs);
    }

    [Fact]
    public async Task EmptyDatabaseUsesEnvironmentAccountAsFallback()
    {
        await using var db = Context();
        var account = await new TeamsOrganizerSettingsStore(db, Options.Create(new TeamsMeetingOptions
        {
            SharedOrganizerObjectId = ObjectId
        })).GetAsync(default);
        Assert.Equal(ObjectId, account.ValidatedObjectId());
        Assert.Equal("mzalaconnect@belgiumcampus.ac.za", account.Email);
    }

    [Fact]
    public async Task EmptySavedAccountDoesNotMaskEnvironmentAndWhitespaceIsNormalized()
    {
        await using var db = Context();
        db.PlatformSettings.Add(new() { TeamsSharedOrganizerEmail = " ", TeamsSharedOrganizerObjectId = "" });
        await db.SaveChangesAsync();
        var account = await new TeamsOrganizerSettingsStore(db, Options.Create(new TeamsMeetingOptions
        {
            SharedOrganizerEmail = " mzalaconnect@belgiumcampus.ac.za ",
            SharedOrganizerObjectId = $" {ObjectId} "
        })).GetAsync(default);
        Assert.Equal("server environment", account.Source);
        Assert.Null(account.ConfigurationError);
        Assert.Equal(ObjectId, account.ValidatedObjectId());
        Assert.Equal("mzalaconnect@belgiumcampus.ac.za", account.Email);
    }

    [Theory]
    [InlineData("old@example.com", "bad-id", "Object ID")]
    [InlineData("", ObjectId, "email")]
    public async Task InvalidSavedAccountIdentifiesTheSourceWithoutMixingEnvironmentValues(string email, string id, string field)
    {
        await using var db = Context();
        db.PlatformSettings.Add(new() { TeamsSharedOrganizerEmail = email, TeamsSharedOrganizerObjectId = id });
        await db.SaveChangesAsync();
        var account = await new TeamsOrganizerSettingsStore(db, Options.Create(new TeamsMeetingOptions
            { SharedOrganizerObjectId = Guid.NewGuid().ToString() })).GetAsync(default);
        var error = Assert.Throws<TeamsMeetingException>(() => account.ValidatedObjectId());
        Assert.Contains("saved Super Admin settings", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Equal(email, account.Email);
        Assert.Equal(id, account.ObjectId);
    }

    [Fact]
    public async Task JsonSaveReturnsConfirmationAndTheSavedAccountIsImmediatelyUsed()
    {
        await using var db = Context();
        db.BcUsers.Add(new() { BcUserId = 900, Role = BcUserRole.SuperAdmin });
        db.PlatformSettings.Add(new() { TeamsSharedOrganizerEmail = "old@example.com", TeamsSharedOrganizerObjectId = "bad-id" });
        await db.SaveChangesAsync();
        var page = JsonPage(db);
        page.TeamsOrganizer = new() { Email = " mzalaconnect@belgiumcampus.ac.za ", ObjectId = $" {{{ObjectId}}} " };
        var result = Assert.IsType<JsonResult>(await page.OnPostSaveTeamsOrganizerAsync(default));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.True(json.GetProperty("succeeded").GetBoolean());
        Assert.Equal(ObjectId, json.GetProperty("objectId").GetString());
        var account = await new TeamsOrganizerSettingsStore(db, Options.Create(new TeamsMeetingOptions())).GetAsync(default);
        Assert.Equal(ObjectId, account.ValidatedObjectId());
        Assert.Equal("mzalaconnect@belgiumcampus.ac.za", account.Email);
    }

    [Fact]
    public async Task BookingSettingsLoadsTeamsAccountForSuperAdminWithoutChangingOtherSettings()
    {
        await using var db = Context();
        db.PlatformSettings.Add(new()
        {
            BookingTermsAndConditions = "Keep the existing booking terms unchanged.",
            TeamsSharedOrganizerEmail = "mzalaconnect@belgiumcampus.ac.za",
            TeamsSharedOrganizerObjectId = ObjectId
        });
        await db.SaveChangesAsync();
        var page = CreatePage(db, BcUserRole.SuperAdmin);
        await page.OnGetAsync(default);
        Assert.True(page.CanManageTeams);
        Assert.Equal(ObjectId, page.TeamsOrganizer.ObjectId);
        Assert.Equal("Keep the existing booking terms unchanged.", page.Input.Terms);
        Assert.Contains("saved Super Admin", page.TeamsOrganizerConfigurationSource);
        Assert.Empty(db.SettingAuditLogs);
        Assert.Null(page.TeamsOrganizerConfigurationError);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("unsupported")]
    [InlineData("denied")]
    public async Task JsonCalendarTestAlwaysReturnsVisibleResultAndNeverSavesOrCreatesMeeting(string outcome)
    {
        await using var db = Context();
        var calendar = new Calendar(outcome);
        var page = JsonPage(db, calendar);
        var result = Assert.IsType<JsonResult>(await page.OnPostTestTeamsOrganizerAsync(default));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(outcome == "success", json.GetProperty("succeeded").GetBoolean());
        Assert.Contains(outcome switch { "success" => "succeeded", "unsupported" => "calendar does not support", _ => "denied calendar access" },
            json.GetProperty("message").GetString());
        Assert.Equal(outcome == "success" ? 200 : 400, result.StatusCode);
        Assert.Equal(ObjectId, calendar.CheckedId);
        Assert.Empty(db.PlatformSettings);
        Assert.Empty(db.SettingAuditLogs);
    }

    [Fact]
    public async Task JsonValidationErrorIsReturnedWithoutCallingMicrosoftOrReloadingSettings()
    {
        await using var db = Context();
        var calendar = new Calendar("success");
        var page = JsonPage(db, calendar);
        page.TeamsOrganizer.ObjectId = "not-a-guid";
        var result = Assert.IsType<JsonResult>(await page.OnPostTestTeamsOrganizerAsync(default));
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("GUID", JsonSerializer.SerializeToElement(result.Value).GetProperty("message").GetString());
        Assert.Null(calendar.CheckedId);
        Assert.Equal("not-a-guid", page.TeamsOrganizer.ObjectId);
    }

    private static BookingsModel CreatePage(ApplicationDbContext db, BcUserRole role) =>
        new(db, new User(role), TimeProvider.System, new SettingsAuditService(db, TimeProvider.System));

    private static BookingsModel JsonPage(ApplicationDbContext db, ITeamsCalendarClient? calendar = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Accept = "application/json";
        return new(db, new User(BcUserRole.SuperAdmin), TimeProvider.System, new SettingsAuditService(db, TimeProvider.System), teamsCalendar: calendar)
        {
            PageContext = new PageContext { HttpContext = http },
            TeamsOrganizer = new() { Email = "mzalaconnect@belgiumcampus.ac.za", ObjectId = ObjectId }
        };
    }

    private sealed class Calendar(string outcome) : ITeamsCalendarClient
    {
        public string? CheckedId { get; private set; }
        public Task<bool> SupportsTeamsAsync(string organizerObjectId, CancellationToken ct)
        {
            CheckedId = organizerObjectId;
            if (outcome == "denied") throw new TeamsMeetingException("Microsoft denied calendar access.");
            return Task.FromResult(outcome == "success");
        }
        public Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<TeamsCalendarEvent> GetAsync(string id, string eventId, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string id, string eventId, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> FindByTransactionAsync(string id, Guid transactionId, DateTimeOffset start, CancellationToken ct) => throw new NotSupportedException();
    }
}
