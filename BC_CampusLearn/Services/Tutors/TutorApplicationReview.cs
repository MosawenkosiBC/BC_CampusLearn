using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Tutors;

public static class TutorApplicationReview
{
    public static async Task<ShortlistResult> ShortlistAsync(
        ApplicationDbContext context,
        int tutorId,
        string? reason,
        CancellationToken cancellationToken)
    {
        string normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length is < 1 or > 1000)
        {
            return ShortlistResult.Failure(
                "Enter a reason of no more than 1,000 characters.");
        }

        TutorApplicationSettings? settings = await context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return ShortlistResult.Failure(
                "Configure the application cycle before reviewing candidates.");
        }

        int shortlistedCount = await context.Tutors.CountAsync(
            tutor => tutor.Status == TutorStatus.Pending &&
                tutor.ApplicationStage == TutorApplicationStage.Shortlisted,
            cancellationToken);
        if (settings.ShortlistLimit.HasValue &&
            shortlistedCount >= settings.ShortlistLimit.Value &&
            !settings.ContinueAfterShortlistLimit)
        {
            return ShortlistResult.Failure(
                "The shortlist target has been reached. Confirm whether you want to continue shortlisting.",
                requiresContinuation: true);
        }

        Tutor? candidate = await context.Tutors.SingleOrDefaultAsync(
            tutor => tutor.TutorId == tutorId,
            cancellationToken);
        if (candidate is null || candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Submitted)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer awaiting review.");
        }

        DateTime reviewedAt = DateTime.UtcNow;
        candidate.ApplicationStage = TutorApplicationStage.Shortlisted;
        candidate.ShortlistReason = normalizedReason;
        candidate.ReviewedAt = reviewedAt;
        candidate.UpdatedAt = reviewedAt;

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = candidate.BcUserId,
            Title = "Tutor application shortlisted",
            Message = "Your tutor application has been reviewed and moved to the shortlist. " +
                $"Reason: {normalizedReason}",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        bool shortlistLimitReached = settings.ShortlistLimit.HasValue &&
            shortlistedCount + 1 == settings.ShortlistLimit.Value &&
            !settings.ContinueAfterShortlistLimit;
        return ShortlistResult.Success(
            "Candidate shortlisted.",
            shortlistLimitReached);
    }

    public static async Task<ShortlistResult> RejectAsync(
        ApplicationDbContext context,
        int tutorId,
        string? reason,
        CancellationToken cancellationToken)
    {
        string normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length > 1000)
        {
            return ShortlistResult.Failure(
                "The rejection reason cannot exceed 1,000 characters.");
        }

        Tutor? candidate = await context.Tutors
            .Include(tutor => tutor.TutorCourseModules)
            .Include(tutor => tutor.TutorDocuments)
            .SingleOrDefaultAsync(
                tutor => tutor.TutorId == tutorId,
                cancellationToken);
        if (candidate is null)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer awaiting review.");
        }

        if (candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Submitted)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer awaiting review.");
        }

        int recipientBcUserId = candidate.BcUserId;

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = recipientBcUserId,
            Title = "Tutor application reviewed",
            Message = string.IsNullOrEmpty(normalizedReason)
                ? "Your tutor application was not moved to the shortlist."
                : "Your tutor application was not moved to the shortlist. " +
                    $"Reason: {normalizedReason}",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.TutorCourseModules.RemoveRange(candidate.TutorCourseModules);
        context.TutorDocuments.RemoveRange(candidate.TutorDocuments);
        context.Tutors.Remove(candidate);

        await context.SaveChangesAsync(cancellationToken);
        return ShortlistResult.Success(
            "Application rejected and removed.");
    }
}

public sealed record ShortlistResult(
    bool Succeeded,
    string Message,
    bool ShortlistLimitReached = false,
    bool RequiresContinuation = false)
{
    public static ShortlistResult Success(
        string message,
        bool shortlistLimitReached = false) =>
        new(true, message, shortlistLimitReached);

    public static ShortlistResult Failure(
        string message,
        bool requiresContinuation = false) =>
        new(false, message, RequiresContinuation: requiresContinuation);

}
