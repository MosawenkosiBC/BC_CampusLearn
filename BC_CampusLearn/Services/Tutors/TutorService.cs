using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Tutors;

public class TutorService : ITutorService
{
    private readonly ApplicationDbContext _context;

    public TutorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TutorCardViewModel>>
        GetTutorsAsync(
            int? courseModuleId,
            CancellationToken cancellationToken = default)
    {
        IQueryable<Tutor> query =
            _context.Tutors
                .AsNoTracking()
                .Where(tutor =>
                    tutor.IsApproved &&
                    tutor.IsActive);

        if (courseModuleId.HasValue)
        {
            query = query.Where(tutor =>
                tutor.TutorCourseModules.Any(item =>
                    item.CourseModuleId ==
                    courseModuleId.Value));
        }

        List<Tutor> tutors = await query
            .Include(tutor => tutor.TutorCourseModules)
                .ThenInclude(item => item.CourseModule)
            .OrderBy(tutor => tutor.DisplayName)
            .ToListAsync(cancellationToken);

        return tutors
            .Select(tutor => new TutorCardViewModel
            {
                TutorId = tutor.TutorId,
                DisplayName = tutor.DisplayName,
                Biography = tutor.Biography,
                ProfileImagePath =
                    tutor.ProfileImagePath,

                Modules = tutor.TutorCourseModules
                    .Select(item =>
                        item.CourseModule.Name)
                    .OrderBy(name => name)
                    .ToList()
            })
            .ToList();
    }

    public async Task<TutorDetailsViewModel?>
        GetTutorDetailsAsync(
            int tutorId,
            CancellationToken cancellationToken = default)
    {
        Tutor? tutor = await _context.Tutors
            .AsNoTracking()
            .Where(item =>
                item.TutorId == tutorId &&
                item.IsApproved &&
                item.IsActive)
            .Include(item => item.TutorCourseModules)
                .ThenInclude(item =>
                    item.CourseModule)
            .Include(item => item.AvailabilitySlots)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return null;
        }

        return new TutorDetailsViewModel
        {
            TutorId = tutor.TutorId,
            DisplayName = tutor.DisplayName,
            Email = tutor.Email,
            Biography = tutor.Biography,
            ProfileImagePath = tutor.ProfileImagePath,

            Modules = tutor.TutorCourseModules
                .Select(item =>
                    item.CourseModule.Name)
                .OrderBy(name => name)
                .ToList(),

            AvailabilitySlots =
                tutor.AvailabilitySlots
                    .Where(slot =>
                        slot.IsActive &&
                        !slot.IsBooked &&
                        slot.StartTime >
                        DateTimeOffset.UtcNow)
                    .OrderBy(slot => slot.StartTime)
                    .Select(slot =>
                        new AvailabilitySlotViewModel
                        {
                            TutorAvailabilityId =
                                slot.TutorAvailabilityId,

                            StartTime = slot.StartTime,
                            EndTime = slot.EndTime
                        })
                    .ToList()
        };
    }

    public async Task<IReadOnlyList<CourseModule>>
        GetModulesAsync(
            CancellationToken cancellationToken = default)
    {
        return await _context.CourseModules
            .AsNoTracking()
            .Where(module => module.IsActive)
            .OrderBy(module => module.Name)
            .ToListAsync(cancellationToken);
    }
}