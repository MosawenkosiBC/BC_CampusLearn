using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.LearningResources;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DetailsModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public LearningResource Resource { get; private set; } = null!;
    public string TutorName { get; private set; } = string.Empty;
    public int CurrentBcUserId { get; private set; }
    public IReadOnlyList<ResourceComment> Comments { get; private set; }
        = Array.Empty<ResourceComment>();

    [TempData]
    public string? DiscussionMessage { get; set; }

    [TempData]
    public bool DiscussionError { get; set; }

    public int CommentCount => Comments.Count(comment => !comment.IsDeleted);

    public async Task<IActionResult> OnGetAsync(
        int resourceId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        CurrentBcUserId = currentUser.BcUserId;
        LearningResource? resource = await _context.LearningResources
            .AsNoTracking()
            .Include(item => item.ProgrammeModule)
            .Include(item => item.Tutor)
                .ThenInclude(tutor => tutor.BcUser)
            .Include(item => item.Documents)
            .SingleOrDefaultAsync(item =>
                item.LearningResourceId == resourceId &&
                item.Status == LearningResourceStatus.Published,
                cancellationToken);
        if (resource is null)
        {
            return NotFound();
        }

        ResourceSubscription? subscription = await _context.ResourceSubscriptions
            .SingleOrDefaultAsync(item =>
                item.PersonnelNumber == currentUser.PersonnelNumber &&
                item.ModuleCode == resource.ProgrammeModule.ModuleCode &&
                item.IsActive,
                cancellationToken);
        if (subscription is null)
        {
            return Forbid();
        }

        subscription.LastAccessedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        Resource = resource;
        TutorName = string.IsNullOrWhiteSpace(resource.Tutor.BcUser.DisplayName)
            ? resource.Tutor.BcUser.PersonnelNumber
            : resource.Tutor.BcUser.DisplayName;
        Comments = await _context.ResourceComments
            .AsNoTracking()
            .Where(comment => comment.ResourceId == resourceId)
            .Include(comment => comment.Author)
                .ThenInclude(author => author.Tutor)
            .OrderByDescending(comment => comment.IsPinned)
            .ThenBy(comment => comment.DateCreated)
            .ToListAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAddCommentAsync(
        int resourceId,
        string? commentText,
        int? parentCommentId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        LearningResource? resource = await _context.LearningResources
            .AsNoTracking()
            .Include(item => item.ProgrammeModule)
            .SingleOrDefaultAsync(item =>
                item.LearningResourceId == resourceId &&
                item.Status == LearningResourceStatus.Published,
                cancellationToken);
        if (resource is null)
        {
            return NotFound();
        }

        bool hasActiveSubscription = await _context.ResourceSubscriptions
            .AsNoTracking()
            .AnyAsync(item =>
                item.PersonnelNumber == currentUser.PersonnelNumber &&
                item.ModuleCode == resource.ProgrammeModule.ModuleCode &&
                item.IsActive,
                cancellationToken);
        if (!hasActiveSubscription)
        {
            return Forbid();
        }

        if (!resource.AllowSubscriberComments)
        {
            DiscussionError = true;
            DiscussionMessage = "Comments are closed for this resource.";
            return RedirectToPage(
                null,
                null,
                new { resourceId },
                "discussion");
        }

        string text = commentText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
        {
            DiscussionError = true;
            DiscussionMessage = string.IsNullOrWhiteSpace(text)
                ? "Enter a comment before posting."
                : "Comments cannot exceed 2000 characters.";
            return RedirectToPage(
                null,
                null,
                new { resourceId },
                "discussion");
        }

        if (parentCommentId.HasValue)
        {
            ResourceComment? parent = await _context.ResourceComments
                .AsNoTracking()
                .SingleOrDefaultAsync(comment =>
                    comment.CommentId == parentCommentId.Value &&
                    comment.ResourceId == resourceId &&
                    !comment.IsDeleted,
                    cancellationToken);
            if (parent is null || parent.ParentCommentId.HasValue)
            {
                DiscussionError = true;
                DiscussionMessage = "That comment is no longer available for replies.";
                return RedirectToPage(
                    null,
                    null,
                    new { resourceId },
                    "discussion");
            }
        }

        ResourceComment newComment = new()
        {
            ResourceId = resourceId,
            AuthorUserId = currentUser.BcUserId,
            ParentCommentId = parentCommentId,
            CommentText = text,
            DateCreated = DateTime.UtcNow
        };
        _context.ResourceComments.Add(newComment);
        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToPage(
            null,
            null,
            new { resourceId },
            $"comment-{newComment.CommentId}");
    }

    public async Task<IActionResult> OnPostEditCommentAsync(
        int resourceId,
        int commentId,
        string? commentText,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        IActionResult? accessFailure = await GetDiscussionAccessFailureAsync(
            currentUser,
            resourceId,
            cancellationToken);
        if (accessFailure is not null)
        {
            return accessFailure;
        }

        ResourceComment? comment = await _context.ResourceComments
            .SingleOrDefaultAsync(item =>
                item.CommentId == commentId &&
                item.ResourceId == resourceId &&
                item.AuthorUserId == currentUser.BcUserId &&
                !item.IsDeleted,
                cancellationToken);
        if (comment is null)
        {
            DiscussionError = true;
            DiscussionMessage = "That comment is no longer available.";
            return RedirectToDiscussion(resourceId);
        }

        string text = commentText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
        {
            DiscussionError = true;
            DiscussionMessage = string.IsNullOrWhiteSpace(text)
                ? "A comment cannot be empty."
                : "Comments cannot exceed 2000 characters.";
            return RedirectToDiscussion(resourceId);
        }

        comment.CommentText = text;
        comment.IsEdited = true;
        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToDiscussion(resourceId);
    }

    public async Task<IActionResult> OnPostDeleteCommentAsync(
        int resourceId,
        int commentId,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        IActionResult? accessFailure = await GetDiscussionAccessFailureAsync(
            currentUser,
            resourceId,
            cancellationToken);
        if (accessFailure is not null)
        {
            return accessFailure;
        }

        ResourceComment? comment = await _context.ResourceComments
            .SingleOrDefaultAsync(item =>
                item.CommentId == commentId &&
                item.ResourceId == resourceId &&
                item.AuthorUserId == currentUser.BcUserId &&
                !item.IsDeleted,
                cancellationToken);
        if (comment is null)
        {
            DiscussionError = true;
            DiscussionMessage = "That comment is no longer available.";
            return RedirectToDiscussion(resourceId);
        }

        comment.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        return RedirectToDiscussion(resourceId);
    }

    public IEnumerable<ResourceComment> GetRootComments() =>
        Comments.Where(comment =>
            comment.ParentCommentId is null &&
            (!comment.IsDeleted || Comments.Any(reply =>
                reply.ParentCommentId == comment.CommentId &&
                !reply.IsDeleted)));

    public IEnumerable<ResourceComment> GetReplies(int commentId) =>
        Comments.Where(comment =>
            comment.ParentCommentId == commentId &&
            !comment.IsDeleted);

    public string GetAuthorName(ResourceComment comment) =>
        string.IsNullOrWhiteSpace(comment.Author.DisplayName)
            ? comment.Author.PersonnelNumber
            : comment.Author.DisplayName;

    public string GetAuthorRole(ResourceComment comment) =>
        comment.AuthorUserId == Resource.Tutor.BcUserId
            ? "Tutor"
            : "Student";

    public static string GetAuthorInitials(ResourceComment comment)
    {
        string name = string.IsNullOrWhiteSpace(comment.Author.DisplayName)
            ? comment.Author.PersonnelNumber
            : comment.Author.DisplayName;
        string[] words = name.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Take(2).Select(word =>
            char.ToUpperInvariant(word[0])));
    }

    public static string GetCommentTimeDisplay(DateTime dateCreated)
    {
        TimeSpan southAfricaOffset = TimeSpan.FromHours(2);
        DateTimeOffset created = new(
            DateTime.SpecifyKind(dateCreated, DateTimeKind.Utc));
        DateTimeOffset localCreated = created.ToOffset(southAfricaOffset);
        DateTimeOffset localNow = DateTimeOffset.UtcNow.ToOffset(southAfricaOffset);
        TimeSpan elapsed = localNow - localCreated;

        if (elapsed < TimeSpan.FromMinutes(1)) return "Just now";
        if (elapsed < TimeSpan.FromHours(1))
        {
            int minutes = Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes));
            return $"{minutes} min ago";
        }
        if (localCreated.Date == localNow.Date)
        {
            int hours = Math.Max(1, (int)Math.Floor(elapsed.TotalHours));
            return $"{hours} hr{(hours == 1 ? string.Empty : "s")} ago";
        }
        if (localCreated.Date == localNow.Date.AddDays(-1)) return "Yesterday";
        if (localCreated.Date >= localNow.Date.AddDays(-6))
        {
            return localCreated.ToString("dddd");
        }
        return localCreated.ToString("dd/MM/yyyy");
    }

    private async Task<IActionResult?> GetDiscussionAccessFailureAsync(
        CurrentUser currentUser,
        int resourceId,
        CancellationToken cancellationToken)
    {
        LearningResource? resource = await _context.LearningResources
            .AsNoTracking()
            .Include(item => item.ProgrammeModule)
            .SingleOrDefaultAsync(item =>
                item.LearningResourceId == resourceId &&
                item.Status == LearningResourceStatus.Published,
                cancellationToken);
        if (resource is null)
        {
            return NotFound();
        }

        bool hasActiveSubscription = await _context.ResourceSubscriptions
            .AsNoTracking()
            .AnyAsync(item =>
                item.PersonnelNumber == currentUser.PersonnelNumber &&
                item.ModuleCode == resource.ProgrammeModule.ModuleCode &&
                item.IsActive,
                cancellationToken);
        return hasActiveSubscription ? null : Forbid();
    }

    private RedirectToPageResult RedirectToDiscussion(int resourceId) =>
        RedirectToPage(
            null,
            null,
            new { resourceId },
            "discussion");
}
