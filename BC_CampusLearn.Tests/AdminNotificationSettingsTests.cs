using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Settings;
using BC_CampusLearn.Services.Notifications;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminNotificationSettingsTests
{
    [Fact]
    public async Task CatalogDisplaysDefaultsAndSavedMessages()
    {
        await using var context = CreateContext();
        context.NotificationTemplateSettings.Add(new()
        {
            TemplateKey = AdminNotificationCatalog.Approval, Message = "Welcome aboard!",
            UpdatedByBcUserId = 10
        });
        await context.SaveChangesAsync();
        var page = Page(context);
        Assert.IsType<PageResult>(await page.OnGetAsync(default));
        Assert.Equal(16, page.NotificationTemplates.Count);
        Assert.Equal(16, page.NotificationTemplates.Select(item => item.Definition.Key).Distinct().Count());
        Assert.Equal(3, page.NotificationTemplates.Count(item => item.Definition.Editable));
        var approval = Assert.Single(page.NotificationTemplates, item => item.Definition.Key == AdminNotificationCatalog.Approval);
        Assert.Equal("Welcome aboard!", approval.Message);
        Assert.True(approval.Customized);
        Assert.Contains(page.NotificationTemplates, item => item.Definition.Key == AdminNotificationCatalog.Farewell);
    }

    [Fact]
    public async Task SaveReloadAndResetAreAuditedAndDoNotSendOrRewriteNotifications()
    {
        await using var context = CreateContext();
        context.UserNotifications.Add(new()
        {
            RecipientBcUserId = 1, Title = "Tutor application approved",
            Message = "Previously sent wording", CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var page = Page(context);
        page.ModelState.AddModelError("Input.Title", "Unrelated announcement validation");
        Assert.IsType<RedirectToPageResult>(await page.OnPostSaveTemplateAsync(
            AdminNotificationCatalog.Approval, " Welcome to our tutors! ", default));
        var setting = await context.NotificationTemplateSettings.SingleAsync();
        Assert.Equal("Welcome to our tutors!", setting.Message);
        Assert.Equal(10, setting.UpdatedByBcUserId);
        Assert.Single(await context.SettingAuditLogs.ToListAsync());
        Assert.Equal("Previously sent wording", (await context.UserNotifications.SingleAsync()).Message);
        await page.OnPostSaveTemplateAsync(AdminNotificationCatalog.Approval, setting.Message, default);
        Assert.Single(await context.SettingAuditLogs.ToListAsync());
        var reloaded = Page(context);
        await reloaded.OnGetAsync(default);
        Assert.Equal(setting.Message, Assert.Single(reloaded.NotificationTemplates,
            item => item.Definition.Key == AdminNotificationCatalog.Approval).Message);
        Assert.IsType<RedirectToPageResult>(await reloaded.OnPostResetTemplateAsync(AdminNotificationCatalog.Approval, default));
        Assert.Empty(await context.NotificationTemplateSettings.ToListAsync());
        Assert.Equal(2, await context.SettingAuditLogs.CountAsync());
        Assert.Equal(AdminNotificationCatalog.Find(AdminNotificationCatalog.Approval)!.DefaultMessage,
            await AdminNotificationCatalog.GetMessageAsync(context, AdminNotificationCatalog.Approval, default));
        Assert.Single(await context.UserNotifications.ToListAsync());
    }

    [Theory]
    [InlineData(BcUserRole.Student)]
    [InlineData(BcUserRole.Tutor)]
    [InlineData(BcUserRole.HeadOfTutors)]
    public async Task NonAdminsCannotReadSaveOrReset(BcUserRole role)
    {
        await using var context = CreateContext();
        var page = Page(context, role);
        Assert.IsType<ForbidResult>(await page.OnGetAsync(default));
        Assert.IsType<ForbidResult>(await page.OnPostSaveTemplateAsync(AdminNotificationCatalog.Approval, "Message", default));
        Assert.IsType<ForbidResult>(await page.OnPostResetTemplateAsync(AdminNotificationCatalog.Approval, default));
        Assert.Empty(await context.NotificationTemplateSettings.ToListAsync());
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("applications-open")]
    public async Task CannotChangeReadOnlyOrUnknownTemplates(string key)
    {
        await using var context = CreateContext();
        var page = Page(context);
        Assert.IsType<BadRequestResult>(await page.OnPostSaveTemplateAsync(key, "Changed", default));
        Assert.IsType<BadRequestResult>(await page.OnPostResetTemplateAsync(key, default));
        Assert.Empty(await context.NotificationTemplateSettings.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyMessagesKeepExistingSettingsAndShowError(string message)
    {
        await using var context = CreateContext();
        var page = Page(context);
        Assert.IsType<PageResult>(await page.OnPostSaveTemplateAsync(AdminNotificationCatalog.Approval, message, default));
        Assert.NotNull(page.TemplateError);
        Assert.Equal(AdminNotificationCatalog.Approval, page.FailedTemplateKey);
        Assert.Empty(await context.NotificationTemplateSettings.ToListAsync());
        Assert.Empty(await context.SettingAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task EnforcesMessageLengthAndSavingDefaultRemovesOverride()
    {
        await using var context = CreateContext();
        var page = Page(context);
        Assert.IsType<PageResult>(await page.OnPostSaveTemplateAsync(AdminNotificationCatalog.Approval, new string('a', 4001), default));
        Assert.Empty(await context.NotificationTemplateSettings.ToListAsync());
        Assert.IsType<RedirectToPageResult>(await page.OnPostSaveTemplateAsync(AdminNotificationCatalog.Approval, new string('a', 4000), default));
        await page.OnPostSaveTemplateAsync(AdminNotificationCatalog.Approval,
            AdminNotificationCatalog.Find(AdminNotificationCatalog.Approval)!.DefaultMessage, default);
        Assert.Empty(await context.NotificationTemplateSettings.ToListAsync());
    }

    [Theory]
    [InlineData(AdminNotificationCatalog.Interview)]
    [InlineData(AdminNotificationCatalog.Approval)]
    [InlineData(AdminNotificationCatalog.Farewell)]
    public async Task TutorWorkflowSendsCustomMessageAndKeepsRoutingAndAudience(string key)
    {
        await using var context = CreateContext();
        var user = new BcUser { BcUserId = 1, DisplayName = "Tutor" };
        context.BcUsers.Add(user);
        context.Tutors.Add(new()
        {
            TutorId = 1, BcUserId = 1,
            Status = key == AdminNotificationCatalog.Farewell ? TutorStatus.Approved : TutorStatus.Pending,
            ApplicationStage = key == AdminNotificationCatalog.Interview ? TutorApplicationStage.Shortlisted :
                key == AdminNotificationCatalog.Approval ? TutorApplicationStage.Interview : TutorApplicationStage.Placement,
            IsActive = key == AdminNotificationCatalog.Farewell,
            ReasonForTutoring = "", TeachingStyle = "", PreviousTutoringExperience = "",
            CampusOfStudy = "", DemonstrationVideoUrl = ""
        });
        await context.SaveChangesAsync();
        const string message = "Our customised notification.";
        await Page(context).OnPostSaveTemplateAsync(key, message, default);
        if (key == AdminNotificationCatalog.Interview)
            Assert.True((await TutorApplicationReview.MoveToInterviewAsync(context, 1,
                new(null, null, null, null, null), default)).Succeeded);
        else if (key == AdminNotificationCatalog.Approval)
            Assert.True((await TutorApplicationReview.MoveInterviewToPlacementAsync(context, 1, default)).Succeeded);
        else
            Assert.Null(await new TutorDeregistrationService(context, TimeProvider.System).DeregisterAsync(
                1, new(10, null, "Admin", null, BcUserRole.Admin), "Term complete"));
        var notification = await context.UserNotifications.SingleAsync();
        Assert.Equal(message, notification.Message);
        Assert.Equal(AdminNotificationCatalog.Find(key)!.Title, notification.Title);
        Assert.Equal(1, notification.RecipientBcUserId);
        Assert.Equal(key == AdminNotificationCatalog.Farewell ? "/Tutors/Sessions" : "/Tutors/TutorApplication", notification.LinkUrl);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static NotificationsModel Page(ApplicationDbContext context, BcUserRole role = BcUserRole.Admin) =>
        new(context, new UserService(role), TimeProvider.System);
    private sealed class UserService(BcUserRole role) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => new(10, null, "Admin", null, role);
    }
}
