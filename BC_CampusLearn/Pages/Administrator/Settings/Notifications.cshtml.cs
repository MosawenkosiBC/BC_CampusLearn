using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Announcements;
using BC_CampusLearn.Services.Notifications;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class NotificationsModel(ApplicationDbContext context,
    ICurrentUserService currentUserService, TimeProvider timeProvider) : PageModel
{
    [BindProperty] public AnnouncementInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "announcements";
    [BindProperty(SupportsGet = true)] public string Category { get; set; } = "Tutor applications";
    [TempData] public string? SuccessMessage { get; set; }
    public IReadOnlyList<NotificationTemplateItem> NotificationTemplates { get; private set; } = [];
    public string? TemplateError { get; private set; }
    public string? FailedTemplateKey { get; private set; }
    private bool CanSend => currentUserService.GetRequiredUser().Role is
        BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.Dev;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!CanSend) return Forbid();
        await LoadTemplatesAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CanSend) return Forbid();
        Tab = "announcements";
        Input.Title = Input.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Input.Title) || Input.Title.Length > 160)
            ModelState.AddModelError("Input.Title", "Enter a title of up to 160 characters.");
        if (!Enum.IsDefined(Input.Audience))
            ModelState.AddModelError("Input.Audience", "Choose an announcement audience.");
        if (!AnnouncementContent.TryGetText(Input.Content, out string text))
            ModelState.AddModelError("Input.Content", "Enter a message using supported formatting and HTTP or HTTPS links.");

        if (!ModelState.IsValid)
        {
            await LoadTemplatesAsync(cancellationToken);
            return Page();
        }

        var recipients = context.BcUsers.AsNoTracking().Where(user =>
            user.Role == BcUserRole.Student || user.Role == BcUserRole.Tutor ||
            user.Role == BcUserRole.SeniorTutor || user.Role == BcUserRole.HeadOfTutors);
        if (Input.Audience == AnnouncementAudience.TutorsOnly)
            recipients = recipients.Where(user => user.Tutor != null &&
                user.Tutor.Status == TutorStatus.Approved && user.Tutor.IsActive);
        var recipientIds = await recipients.Select(user => user.BcUserId).ToListAsync(cancellationToken);
        if (recipientIds.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "There are no recipients for the selected audience.");
            await LoadTemplatesAsync(cancellationToken);
            return Page();
        }

        var announcement = new Announcement
        {
            Title = Input.Title, Content = Input.Content, Audience = Input.Audience,
            SenderBcUserId = currentUserService.GetRequiredUser().BcUserId,
            SentAt = timeProvider.GetUtcNow()
        };
        string preview = text.Length > 300 ? text[..300] + "…" : text;
        foreach (int recipientId in recipientIds)
            announcement.Notifications.Add(new UserNotification
            {
                RecipientBcUserId = recipientId, Title = announcement.Title,
                Message = preview, CreatedAt = announcement.SentAt,
                LinkUrl = $"/Announcements/Details?announcementId={announcement.AnnouncementId}"
            });
        context.Announcements.Add(announcement);
        // The existing interceptor sends ReceiveUserNotification through SignalR after saving.
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"Announcement sent to {recipientIds.Count} recipient{(recipientIds.Count == 1 ? "" : "s")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveTemplateAsync(
        string templateKey, string? message, CancellationToken cancellationToken)
    {
        if (!CanSend) return Forbid();
        var definition = AdminNotificationCatalog.Find(templateKey);
        if (definition is null || !definition.Editable) return BadRequest();
        // The announcement form is independent of this template form.
        ModelState.Clear();
        message = message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > 4000)
        {
            Tab = "notifications";
            Category = definition.Category;
            FailedTemplateKey = templateKey;
            TemplateError = "Enter a notification message between 1 and 4,000 characters.";
            await LoadTemplatesAsync(cancellationToken);
            NotificationTemplates = NotificationTemplates.Select(item => item.Definition.Key == templateKey
                ? item with { Message = message ?? string.Empty } : item).ToList();
            return Page();
        }

        var setting = await context.NotificationTemplateSettings
            .SingleOrDefaultAsync(item => item.TemplateKey == templateKey, cancellationToken);
        string oldMessage = setting?.Message ?? definition.DefaultMessage;
        if (message == definition.DefaultMessage)
        {
            if (setting is not null) context.NotificationTemplateSettings.Remove(setting);
        }
        else if (oldMessage != message)
        {
            if (setting is null)
            {
                setting = new NotificationTemplateSetting { TemplateKey = templateKey };
                context.NotificationTemplateSettings.Add(setting);
            }
            setting.Message = message;
            setting.UpdatedAt = timeProvider.GetUtcNow();
            setting.UpdatedByBcUserId = currentUserService.GetRequiredUser().BcUserId;
        }
        bool changed = new SettingsAuditService(context, timeProvider).Record(
            "Admin notifications", definition.Title, oldMessage, message,
            currentUserService.GetRequiredUser());
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = changed ? "Notification message saved. Future notifications will use this wording."
            : "This notification message is already up to date.";
        return RedirectToPage(null, null, new { Tab = "notifications", Category = definition.Category }, "admin-notification-settings");
    }

    public async Task<IActionResult> OnPostResetTemplateAsync(
        string templateKey, CancellationToken cancellationToken)
    {
        if (!CanSend) return Forbid();
        var definition = AdminNotificationCatalog.Find(templateKey);
        if (definition is null || !definition.Editable) return BadRequest();
        var setting = await context.NotificationTemplateSettings
            .SingleOrDefaultAsync(item => item.TemplateKey == templateKey, cancellationToken);
        if (setting is not null)
        {
            new SettingsAuditService(context, timeProvider).Record("Admin notifications",
                definition.Title, setting.Message, definition.DefaultMessage,
                currentUserService.GetRequiredUser(), "Restored the default notification message.");
            context.NotificationTemplateSettings.Remove(setting);
            await context.SaveChangesAsync(cancellationToken);
        }
        SuccessMessage = "Default notification message restored.";
        return RedirectToPage(null, null, new { Tab = "notifications", Category = definition.Category }, "admin-notification-settings");
    }

    private async Task LoadTemplatesAsync(CancellationToken cancellationToken)
    {
        if (Tab is not ("announcements" or "notifications")) Tab = "announcements";
        if (!AdminNotificationCatalog.All.Any(item => item.Category == Category))
            Category = AdminNotificationCatalog.All[0].Category;
        var settings = await context.NotificationTemplateSettings.AsNoTracking()
            .ToDictionaryAsync(item => item.TemplateKey, cancellationToken);
        NotificationTemplates = AdminNotificationCatalog.All.Select(definition =>
        {
            settings.TryGetValue(definition.Key, out var setting);
            return new NotificationTemplateItem(definition, setting?.Message ?? definition.DefaultMessage,
                setting is not null);
        }).ToList();
    }

    public sealed record NotificationTemplateItem(AdminNotificationDefinition Definition,
        string Message, bool Customized);
    public sealed class AnnouncementInput
    {
        [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
        [Required, StringLength(500000)] public string Content { get; set; } = string.Empty;
        public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.AllStudents;
    }
}
