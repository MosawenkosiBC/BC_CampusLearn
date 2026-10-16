using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BC_CampusLearn.Services.Tutors;

// Assignments are retained because historical bookings reference their composite key.
public class AdminModuleManagement(ApplicationDbContext context)
{
    public async Task<string?> ChangeAssignmentAsync(int tutorId, int moduleId, bool add,
        CancellationToken cancellationToken)
    {
        var tutor = await context.Tutors.FindAsync([tutorId], cancellationToken);
        var module = await context.ProgrammeModules.FindAsync([moduleId], cancellationToken);
        if (tutor is null || module is null) return "The tutor or module no longer exists.";
        if (add && (!tutor.IsActive || tutor.Status != TutorStatus.Approved ||
            tutor.ApplicationStage != TutorApplicationStage.Placement))
            return "Only active, approved tutors in placement can be assigned modules.";
        var assignment = await context.TutorCourseModules.FindAsync([tutorId, moduleId], cancellationToken);
        if (add == (assignment?.IsActive == true))
            return add ? "This tutor is already assigned to the module." : "This tutor is no longer assigned to the module.";
        if (!add && await context.Bookings.AnyAsync(booking => booking.TutorId == tutorId &&
            booking.ProgrammeModuleId == moduleId && (booking.Status == BookingStatus.Pending ||
            booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.InProgress), cancellationToken))
            return "One or more sessions for this module are pending, confirmed, or in progress. They must be resolved before the tutor can be removed.";

        if (assignment is null)
            context.TutorCourseModules.Add(new TutorCourseModule { TutorId = tutorId, ProgrammeModuleId = moduleId });
        else assignment.IsActive = add;
        return null;
    }

    public void Notify(Tutor tutor, ProgrammeModule module, string message)
    {
        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = tutor.BcUserId,
            Title = "Module assignment update",
            Message = $"{module.ModuleCode} — {module.ModuleName}: {message}",
            LinkUrl = "/Tutors/Profile",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    public void NotifyAssignmentChanges(Tutor tutor,
        IReadOnlyCollection<ProgrammeModule> modules,
        bool assigned)
    {
        const int maximumMessageLength = 4000;
        List<string> moduleCodes = modules
            .OrderBy(module => module.ModuleCode)
            .ThenBy(module => module.ModuleName)
            .Select(module => module.ModuleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string moduleLabel = modules.Count == 1 ? "module" : "modules";
        string summary = assigned
            ? $"An administrator assigned you to {modules.Count} {moduleLabel}:"
            : $"An administrator removed your assignment from {modules.Count} {moduleLabel}:";
        var message = new StringBuilder(summary).AppendLine();
        for (int index = 0; index < moduleCodes.Count; index++)
        {
            string separator = index == 0 ? string.Empty : ", ";
            int omittedCount = moduleCodes.Count - index;
            string omittedSuffix = $" … and {omittedCount} more.";
            if (message.Length + separator.Length + moduleCodes[index].Length +
                (index < moduleCodes.Count - 1 ? omittedSuffix.Length : 0) >
                maximumMessageLength)
            {
                message.Append(omittedSuffix);
                break;
            }

            message.Append(separator).Append(moduleCodes[index]);
        }

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = tutor.BcUserId,
            Title = "Module assignment update",
            Message = message.ToString(),
            LinkUrl = "/Tutors/Profile",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
