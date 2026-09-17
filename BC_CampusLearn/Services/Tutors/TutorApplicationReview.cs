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
        CancellationToken cancellationToken,
        int? reviewerBcUserId = null)
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

        if (reviewerBcUserId.HasValue)
        {
            BcUser? reviewer = await context.BcUsers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    user => user.BcUserId == reviewerBcUserId.Value,
                    cancellationToken);
            if (reviewer is null)
            {
                return ShortlistResult.Failure(
                    "The reviewing administrator could not be identified.");
            }

            context.TutorApplicationReviewDecisions.Add(
                new TutorApplicationReviewDecision
                {
                    TutorId = candidate.TutorId,
                    ReviewerBcUserId = reviewerBcUserId.Value,
                    AdminName = string.IsNullOrWhiteSpace(reviewer.DisplayName)
                        ? reviewer.PersonnelNumber
                        : reviewer.DisplayName,
                    PreviousStage = TutorApplicationStage.Submitted,
                    NewStage = TutorApplicationStage.Shortlisted,
                    Reason = normalizedReason,
                    ReviewedAt = reviewedAt
                });
        }

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

        Tutor? candidate = await context.Tutors.SingleOrDefaultAsync(
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

        DateTime reviewedAt = DateTime.UtcNow;
        candidate.Status = TutorStatus.Rejected;
        candidate.ApplicationStage = TutorApplicationStage.Rejected;
        candidate.ShortlistReason = string.IsNullOrEmpty(normalizedReason)
            ? null
            : normalizedReason;
        candidate.ReviewedAt = reviewedAt;
        candidate.UpdatedAt = reviewedAt;
        candidate.IsActive = false;

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = candidate.BcUserId,
            Title = "Tutor application reviewed",
            Message = string.IsNullOrEmpty(normalizedReason)
                ? "Your tutor application was not moved to the shortlist."
                : "Your tutor application was not moved to the shortlist. " +
                    $"Reason: {normalizedReason}",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return ShortlistResult.Success(
            "Application rejected.");
    }

    public static async Task<ShortlistResult> MoveToInterviewAsync(
        ApplicationDbContext context,
        int tutorId,
        InterviewPreparationDetails preparation,
        CancellationToken cancellationToken,
        int? reviewerBcUserId = null)
    {
        Tutor? candidate = await context.Tutors.SingleOrDefaultAsync(
            tutor => tutor.TutorId == tutorId,
            cancellationToken);
        if (candidate is null || candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Shortlisted)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer in the shortlist.");
        }

        ShortlistResult preparationResult = ApplyInterviewPreparation(
            candidate,
            preparation,
            out string normalizedNotes);
        if (!preparationResult.Succeeded)
        {
            return preparationResult;
        }

        DateTime reviewedAt = DateTime.UtcNow;
        candidate.ApplicationStage = TutorApplicationStage.Interview;
        candidate.ReviewedAt = reviewedAt;
        candidate.UpdatedAt = reviewedAt;

        ShortlistResult auditResult = await AddReviewDecisionAsync(
            context,
            candidate,
            reviewerBcUserId,
            TutorApplicationStage.Shortlisted,
            TutorApplicationStage.Interview,
            string.IsNullOrEmpty(normalizedNotes)
                ? "Candidate moved to interview."
                : normalizedNotes,
            reviewedAt,
            cancellationToken);
        if (!auditResult.Succeeded)
        {
            return auditResult;
        }

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = candidate.BcUserId,
            Title = "Tutor application moved to interview",
            Message = "Your tutor application has progressed to the interview stage.",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return ShortlistResult.Success("Candidate moved to interview.");
    }

    public static async Task<ShortlistResult> SaveInterviewPreparationAsync(
        ApplicationDbContext context,
        int tutorId,
        InterviewPreparationDetails preparation,
        CancellationToken cancellationToken,
        int? reviewerBcUserId = null)
    {
        Tutor? candidate = await context.Tutors.SingleOrDefaultAsync(
            tutor => tutor.TutorId == tutorId,
            cancellationToken);
        if (candidate is null || candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Shortlisted)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer in the shortlist.");
        }

        ShortlistResult preparationResult = ApplyInterviewPreparation(
            candidate,
            preparation,
            out string normalizedNotes);
        if (!preparationResult.Succeeded)
        {
            return preparationResult;
        }

        DateTime savedAt = DateTime.UtcNow;
        candidate.UpdatedAt = savedAt;

        ShortlistResult auditResult = await AddReviewDecisionAsync(
            context,
            candidate,
            reviewerBcUserId,
            TutorApplicationStage.Shortlisted,
            TutorApplicationStage.Shortlisted,
            string.IsNullOrEmpty(normalizedNotes)
                ? "Interview preparation updated."
                : normalizedNotes,
            savedAt,
            cancellationToken);
        if (!auditResult.Succeeded)
        {
            return auditResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return ShortlistResult.Success("Interview preparation saved.");
    }

    public static async Task<ShortlistResult> RejectShortlistedAsync(
        ApplicationDbContext context,
        int tutorId,
        string? reason,
        CancellationToken cancellationToken,
        int? reviewerBcUserId = null)
    {
        string normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length > 1000)
        {
            return ShortlistResult.Failure(
                "The rejection reason cannot exceed 1,000 characters.");
        }

        Tutor? candidate = await context.Tutors.SingleOrDefaultAsync(
            tutor => tutor.TutorId == tutorId,
            cancellationToken);
        if (candidate is null || candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Shortlisted)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer in the shortlist.");
        }

        DateTime reviewedAt = DateTime.UtcNow;
        candidate.Status = TutorStatus.Rejected;
        candidate.ApplicationStage = TutorApplicationStage.Rejected;
        candidate.ReviewedAt = reviewedAt;
        candidate.UpdatedAt = reviewedAt;
        candidate.IsActive = false;

        ShortlistResult auditResult = await AddReviewDecisionAsync(
            context,
            candidate,
            reviewerBcUserId,
            TutorApplicationStage.Shortlisted,
            TutorApplicationStage.Rejected,
            string.IsNullOrEmpty(normalizedReason)
                ? "Candidate rejected during shortlist review."
                : normalizedReason,
            reviewedAt,
            cancellationToken);
        if (!auditResult.Succeeded)
        {
            return auditResult;
        }

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = candidate.BcUserId,
            Title = "Tutor application reviewed",
            Message = string.IsNullOrEmpty(normalizedReason)
                ? "Your tutor application will not progress to the interview stage."
                : "Your tutor application will not progress to the interview stage. " +
                    $"Reason: {normalizedReason}",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return ShortlistResult.Success("Candidate rejected.");
    }

    public static async Task<ShortlistResult> MoveInterviewToPlacementAsync(
        ApplicationDbContext context,
        int tutorId,
        CancellationToken cancellationToken,
        int? reviewerBcUserId = null)
    {
        Tutor? candidate = await context.Tutors
            .Include(tutor => tutor.BcUser)
            .SingleOrDefaultAsync(
                tutor => tutor.TutorId == tutorId,
                cancellationToken);
        if (candidate is null || candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Interview)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer in the interview stage.");
        }

        DateTime reviewedAt = DateTime.UtcNow;
        candidate.Status = TutorStatus.Approved;
        candidate.ApplicationStage = TutorApplicationStage.Placement;
        candidate.IsActive = true;
        candidate.ReviewedAt = reviewedAt;
        candidate.UpdatedAt = reviewedAt;
        candidate.BcUser.Role = BcUserRole.Tutor;

        ShortlistResult auditResult = await AddReviewDecisionAsync(
            context,
            candidate,
            reviewerBcUserId,
            TutorApplicationStage.Interview,
            TutorApplicationStage.Placement,
            "Candidate approved as a tutor after interview.",
            reviewedAt,
            cancellationToken);
        if (!auditResult.Succeeded)
        {
            return auditResult;
        }

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = candidate.BcUserId,
            Title = "Tutor application approved",
            Message = "Congratulations! Your tutor application has been approved. " +
                "You are now a BC CampusLearn tutor.",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return ShortlistResult.Success(
            "Candidate moved to placement and notified.");
    }

    public static async Task<ShortlistResult> RejectInterviewedAsync(
        ApplicationDbContext context,
        int tutorId,
        string? reason,
        CancellationToken cancellationToken,
        int? reviewerBcUserId = null)
    {
        string normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length > 1000)
        {
            return ShortlistResult.Failure(
                "The rejection reason cannot exceed 1,000 characters.");
        }

        Tutor? candidate = await context.Tutors.SingleOrDefaultAsync(
            tutor => tutor.TutorId == tutorId,
            cancellationToken);
        if (candidate is null || candidate.Status != TutorStatus.Pending ||
            candidate.ApplicationStage != TutorApplicationStage.Interview)
        {
            return ShortlistResult.Failure(
                "This candidate is no longer in the interview stage.");
        }

        DateTime reviewedAt = DateTime.UtcNow;
        candidate.Status = TutorStatus.Rejected;
        candidate.ApplicationStage = TutorApplicationStage.Rejected;
        candidate.IsActive = false;
        candidate.ReviewedAt = reviewedAt;
        candidate.UpdatedAt = reviewedAt;

        ShortlistResult auditResult = await AddReviewDecisionAsync(
            context,
            candidate,
            reviewerBcUserId,
            TutorApplicationStage.Interview,
            TutorApplicationStage.Rejected,
            string.IsNullOrEmpty(normalizedReason)
                ? "Candidate rejected after interview."
                : normalizedReason,
            reviewedAt,
            cancellationToken);
        if (!auditResult.Succeeded)
        {
            return auditResult;
        }

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = candidate.BcUserId,
            Title = "Tutor application reviewed",
            Message = string.IsNullOrEmpty(normalizedReason)
                ? "Your tutor application was not approved after the interview."
                : "Your tutor application was not approved after the interview. " +
                    $"Reason: {normalizedReason}",
            LinkUrl = "/Tutors/TutorApplication",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return ShortlistResult.Success("Candidate rejected.");
    }

    private static async Task<ShortlistResult> AddReviewDecisionAsync(
        ApplicationDbContext context,
        Tutor candidate,
        int? reviewerBcUserId,
        TutorApplicationStage previousStage,
        TutorApplicationStage newStage,
        string reason,
        DateTime reviewedAt,
        CancellationToken cancellationToken)
    {
        if (!reviewerBcUserId.HasValue)
        {
            return ShortlistResult.Success(string.Empty);
        }

        BcUser? reviewer = await context.BcUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.BcUserId == reviewerBcUserId.Value,
                cancellationToken);
        if (reviewer is null)
        {
            return ShortlistResult.Failure(
                "The reviewing administrator could not be identified.");
        }

        context.TutorApplicationReviewDecisions.Add(
            new TutorApplicationReviewDecision
            {
                TutorId = candidate.TutorId,
                ReviewerBcUserId = reviewerBcUserId.Value,
                AdminName = string.IsNullOrWhiteSpace(reviewer.DisplayName)
                    ? reviewer.PersonnelNumber
                    : reviewer.DisplayName,
                PreviousStage = previousStage,
                NewStage = newStage,
                Reason = reason,
                ReviewedAt = reviewedAt
            });

        return ShortlistResult.Success(string.Empty);
    }

    private static ShortlistResult ApplyInterviewPreparation(
        Tutor candidate,
        InterviewPreparationDetails preparation,
        out string normalizedNotes)
    {
        normalizedNotes = preparation.Notes?.Trim() ?? string.Empty;
        string normalizedLocation =
            preparation.LocationOrMeetingLink?.Trim() ?? string.Empty;

        if (normalizedNotes.Length > 1000)
        {
            return ShortlistResult.Failure(
                "Interview preparation notes cannot exceed 1,000 characters.");
        }

        if (normalizedLocation.Length > 500)
        {
            return ShortlistResult.Failure(
                "The interview location or meeting link cannot exceed 500 characters.");
        }

        if (preparation.DurationMinutes is < 15 or > 240)
        {
            return ShortlistResult.Failure(
                "Interview duration must be between 15 and 240 minutes.");
        }

        if (preparation.ScheduledDate.HasValue !=
            preparation.ScheduledTime.HasValue)
        {
            return ShortlistResult.Failure(
                "Enter both an interview date and time, or leave both blank.");
        }

        candidate.InterviewPreparationNotes = string.IsNullOrEmpty(normalizedNotes)
            ? null
            : normalizedNotes;

        bool hasScheduleInput = preparation.ScheduledDate.HasValue ||
            preparation.ScheduledTime.HasValue ||
            preparation.DurationMinutes.HasValue ||
            !string.IsNullOrEmpty(normalizedLocation);
        if (hasScheduleInput)
        {
            candidate.InterviewScheduledAt = preparation.ScheduledDate.HasValue
                ? preparation.ScheduledDate.Value.Date.Add(
                    preparation.ScheduledTime!.Value)
                : null;
            candidate.InterviewDurationMinutes = preparation.DurationMinutes;
            candidate.InterviewLocation = string.IsNullOrEmpty(normalizedLocation)
                ? null
                : normalizedLocation;
        }

        return ShortlistResult.Success(string.Empty);
    }

    public static async Task<int> RemoveRejectedApplicationsWhenCycleClosedAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        TutorApplicationSettings? settings = await context
            .TutorApplicationSettings.SingleOrDefaultAsync(cancellationToken);

        if (settings?.IsAcceptingApplications(DateTime.UtcNow) == true)
        {
            return 0;
        }

        List<Tutor> rejectedApplications = await context.Tutors
            .Include(tutor => tutor.TutorCourseModules)
            .Include(tutor => tutor.TutorDocuments)
            .Where(tutor =>
                tutor.ApplicationStage == TutorApplicationStage.Rejected ||
                tutor.Status == TutorStatus.Rejected)
            .ToListAsync(cancellationToken);

        if (rejectedApplications.Count == 0)
        {
            return 0;
        }

        context.TutorCourseModules.RemoveRange(
            rejectedApplications.SelectMany(tutor =>
                tutor.TutorCourseModules));
        context.TutorDocuments.RemoveRange(
            rejectedApplications.SelectMany(tutor =>
                tutor.TutorDocuments));
        context.Tutors.RemoveRange(rejectedApplications);
        await context.SaveChangesAsync(cancellationToken);

        return rejectedApplications.Count;
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

public sealed record InterviewPreparationDetails(
    string? Notes,
    DateTime? ScheduledDate,
    TimeSpan? ScheduledTime,
    int? DurationMinutes,
    string? LocationOrMeetingLink);
