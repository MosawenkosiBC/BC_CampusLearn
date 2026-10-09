using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Announcements;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class NotificationsModel(ApplicationDbContext context,
    ICurrentUserService currentUserService, TimeProvider timeProvider) : PageModel
{
    [BindProperty] public AnnouncementInput Input { get; set; } = new();
    [TempData] public string? SuccessMessage { get; set; }
    private bool CanSend => currentUserService.GetRequiredUser().Role is
        BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.Dev;

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(CanSend ? Page() : Forbid());

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!CanSend) return Forbid();
        Input.Title = Input.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Input.Title) || Input.Title.Length > 160)
            ModelState.AddModelError("Input.Title", "Enter a title of up to 160 characters.");
        if (!Enum.IsDefined(Input.Audience))
            ModelState.AddModelError("Input.Audience", "Choose an announcement audience.");
        if (!AnnouncementContent.TryGetText(Input.Content, out string text))
            ModelState.AddModelError("Input.Content", "Enter a message using supported formatting and HTTP or HTTPS links.");

        if (!ModelState.IsValid)
        {
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

    public sealed class AnnouncementInput
    {
        [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
        [Required, StringLength(500000)] public string Content { get; set; } = string.Empty;
        public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.AllStudents;
    }
}
