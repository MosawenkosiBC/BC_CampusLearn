using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using BC_CampusLearn.Services.Teams;
using Microsoft.AspNetCore.Mvc;
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
        var page = new BookingsAndSessionsModel(db, currentUserService: new User(role));
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
        var page = new BookingsAndSessionsModel(db, currentUserService: new User(BcUserRole.SuperAdmin))
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
        var page = new BookingsAndSessionsModel(db, currentUserService: new User(BcUserRole.SuperAdmin))
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
}
