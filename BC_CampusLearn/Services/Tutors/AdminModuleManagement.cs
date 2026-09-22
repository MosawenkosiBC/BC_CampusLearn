using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;

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
        if (add && tutor.ProgrammeId != module.ProgrammeId)
            return "Select a tutor from the module's programme.";

        var assignment = await context.TutorCourseModules.FindAsync([tutorId, moduleId], cancellationToken);
        if (add == (assignment?.IsActive == true))
            return add ? "This tutor is already assigned to the module." : "This tutor is no longer assigned to the module.";
        if (!add && await context.Bookings.AnyAsync(booking => booking.TutorId == tutorId &&
            booking.ProgrammeModuleId == moduleId && (booking.Status == BookingStatus.Pending ||
            booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.InProgress), cancellationToken))
            return "Complete, cancel or decline outstanding sessions for this module before removing the tutor.";

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
}
