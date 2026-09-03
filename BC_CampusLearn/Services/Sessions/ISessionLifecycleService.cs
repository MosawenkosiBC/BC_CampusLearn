using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Sessions;

public interface ISessionLifecycleService
{
    Task ProcessDueTransitionsAsync(
        CancellationToken cancellationToken = default);

    Task<SessionLifecycleResult> ConfirmAsync(
        int tutorId,
        int changedByBcUserId,
        int bookingId,
        string? meetingLink,
        CancellationToken cancellationToken = default);

    Task<SessionLifecycleResult> DeclineAsync(
        int tutorId,
        int changedByBcUserId,
        int bookingId,
        string? reason,
        bool reopenAvailability,
        CancellationToken cancellationToken = default);

    Task<SessionLifecycleResult> CancelByStudentAsync(
        int studentBcUserId,
        string studentObjectId,
        string studentTenantId,
        int bookingId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<SessionLifecycleResult> StartAsync(
        int tutorId,
        int changedByBcUserId,
        int bookingId,
        SessionStartSource source,
        CancellationToken cancellationToken = default);
}
