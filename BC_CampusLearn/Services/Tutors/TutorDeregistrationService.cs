using BC_CampusLearn.Services.Notifications;
using System.Data;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Tutors;

public sealed class TutorDeregistrationService(ApplicationDbContext context, TimeProvider timeProvider)
{
    public async Task<string?> DeregisterAsync(int tutorId, CurrentUser administrator, string? reason,
        CancellationToken cancellationToken = default)
    {
        if (administrator.Role is not (BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.Dev))
            return "Only administrators can deregister tutors.";
        reason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return "Enter a reason between 1 and 1,000 characters.";

        // Prevent a booking or a second deregistration from racing the cleanup.
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var tutor = await context.Tutors.Include(t => t.BcUser)
                .Include(t => t.TutorCourseModules).Include(t => t.TutorAvailabilities)
                .Include(t => t.ModuleChangeRequests).Include(t => t.DeregistrationRequests)
                .Include(t => t.ResourceTutorNominations)
                .SingleOrDefaultAsync(t => t.TutorId == tutorId, cancellationToken);
            if (tutor is null) return "Tutor not found.";
            if (tutor.Status == TutorStatus.Deregistered)
                return "This tutor has already been deregistered.";
            if (tutor.Status is not (TutorStatus.Approved or TutorStatus.Suspended))
                return "Only registered tutors can be deregistered.";
            bool hasOpenSessions = await context.Bookings.AnyAsync(b => b.TutorId == tutorId &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress),
                cancellationToken);
            if (hasOpenSessions)
                return "Resolve all pending, confirmed and in-progress sessions before deregistering this tutor.";

            var now = timeProvider.GetUtcNow();
            tutor.Status = TutorStatus.Deregistered;
            tutor.IsActive = false;
            tutor.DeregisteredAt = now;
            tutor.DeregisteredByBcUserId = administrator.BcUserId;
            tutor.DeregisteredByName = administrator.DisplayName;
            tutor.DeregistrationReason = reason;
            tutor.UpdatedAt = now.UtcDateTime;
            foreach (var assignment in tutor.TutorCourseModules) assignment.IsActive = false;
            context.TutorAvailabilities.RemoveRange(tutor.TutorAvailabilities);
            foreach (var request in tutor.ModuleChangeRequests.Where(r => r.Status == TutorAccountRequestStatus.Pending))
            {
                request.Status = TutorAccountRequestStatus.Declined;
                request.ReviewedAt = now.UtcDateTime;
                request.ReviewedBy = administrator.DisplayName;
                request.ReviewNote = "Closed because the tutor was deregistered.";
            }
            foreach (var request in tutor.DeregistrationRequests.Where(r => r.Status == TutorAccountRequestStatus.Pending))
            {
                request.Status = TutorAccountRequestStatus.Approved;
                request.ReviewedAt = now.UtcDateTime;
            }
            foreach (var nomination in tutor.ResourceTutorNominations.Where(n => n.IsActive))
            {
                nomination.IsActive = false;
                nomination.DenominatedAt = now;
                nomination.DenominatedByBcUserId = administrator.BcUserId;
            }
            if (tutor.BcUser.Role is BcUserRole.Tutor or BcUserRole.HeadOfTutors)
                tutor.BcUser.Role = BcUserRole.Student;
            context.UserNotifications.Add(new UserNotification
            {
                RecipientBcUserId = tutor.BcUserId,
                Title = "Thank you for your contribution as a tutor",
                Message = await AdminNotificationCatalog.GetMessageAsync(context, AdminNotificationCatalog.Farewell, cancellationToken),
                LinkUrl = "/Tutors/Sessions",
                CreatedAt = now
            });
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return (string?)null;
        });
    }
}
