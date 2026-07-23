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
            int? programmeModuleId,
            CancellationToken cancellationToken = default)
    {
        IQueryable<Tutor> query =
            _context.Tutors
                .AsNoTracking()
                .Where(tutor =>
                    tutor.Status == TutorStatus.Approved &&
                    tutor.IsActive);

        if (programmeModuleId.HasValue)
        {
            query = query.Where(tutor =>
                tutor.TutorCourseModules.Any(item =>
                    item.ProgrammeModuleId ==
                    programmeModuleId.Value));
        }

        List<Tutor> tutors = await query
            .Include(tutor => tutor.TutorCourseModules)
                .ThenInclude(item => item.ProgrammeModule)
            .Include(tutor => tutor.BcUser)
            .OrderBy(tutor => tutor.BcUser.PersonnelNumber)
            .ToListAsync(cancellationToken);

        return tutors
            .Select(tutor => new TutorCardViewModel
            {
                TutorId = tutor.TutorId,
                DisplayName = tutor.BcUser.PersonnelNumber,
                Biography = tutor.Biography ?? string.Empty,
                ProfileImagePath =
                    tutor.ProfileImagePath,

                Modules = tutor.TutorCourseModules
                    .Select(item =>
                        item.ProgrammeModule.ModuleName)
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
                item.Status == TutorStatus.Approved &&
                item.IsActive)
            .Include(item => item.TutorCourseModules)
                .ThenInclude(item =>
                    item.ProgrammeModule)
            .Include(item => item.BcUser)
            .Include(item => item.TutorAvailabilities)
                .ThenInclude(slot => slot.ProgrammeModule)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return null;
        }

        return new TutorDetailsViewModel
        {
            TutorId = tutor.TutorId,
            DisplayName = tutor.BcUser.PersonnelNumber,
            Email = string.Empty,
            Biography = tutor.Biography ?? string.Empty,
            ProfileImagePath = tutor.ProfileImagePath,

            Modules = tutor.TutorCourseModules
                .Select(item =>
                    item.ProgrammeModule.ModuleName)
                .OrderBy(name => name)
                .ToList(),

            AvailabilitySlots =
                tutor.TutorAvailabilities
                    .Where(slot =>
                        slot.IsActive &&
                        slot.AvailableTime >
                        DateTimeOffset.UtcNow)
                    .OrderBy(slot => slot.AvailableTime)
                    .Select(slot =>
                        new AvailabilitySlotViewModel
                        {
                            TutorAvailabilityId =
                                slot.TutorAvailabilityId,

                            ModuleCode =
                                slot.ProgrammeModule.ModuleCode,

                            ModuleName =
                                slot.ProgrammeModule.ModuleName,

                            AvailableTime =
                                slot.AvailableTime
                        })
                    .ToList()
        };
    }

    public async Task<IReadOnlyList<ProgrammeModule>>
        GetModulesAsync(
            CancellationToken cancellationToken = default)
    {
        return await _context.ProgrammeModules
            .AsNoTracking()
            .OrderBy(module => module.ModuleName)
            .ToListAsync(cancellationToken);
    }
}
