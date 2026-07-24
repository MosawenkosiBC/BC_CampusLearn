using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Tutors;

public class TutorService : ITutorService
{
    private static readonly string[] StaticTutorNames =
    {
        "Michelle Duma",
        "Mishakaylin Diniso",
        "Naledi Mogadingoane",
        "Karabo Mosethe",
        "Mosa Msiza",
        "Thembi Sefini",
        "Karabelo Mokhubu",
        "Keleabetswe Molefe"
    };

    private static readonly string[] FallbackProfileImages =
    {
        "/Media/tutorsProfiles/Tutor 1.webp",
        "/Media/tutorsProfiles/Tutor 3.webp",
        "/Media/tutorsProfiles/Tutor 5.webp",
        "/Media/tutorsProfiles/image.png"
    };

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
            .Include(tutor => tutor.Programme)
            .Include(tutor => tutor.TutorAvailabilities)
            .OrderBy(tutor => tutor.TutorId)
            .ToListAsync(cancellationToken);

        return tutors
            .Select((tutor, index) => new TutorCardViewModel
            {
                TutorId = tutor.TutorId,
                DisplayName = GetStaticTutorName(tutor.TutorId),
                Biography = tutor.Biography ?? string.Empty,
                ProfileImagePath = string.IsNullOrWhiteSpace(tutor.ProfileImagePath)
                    ? FallbackProfileImages[index % FallbackProfileImages.Length]
                    : tutor.ProfileImagePath,
                ProgrammeId = tutor.ProgrammeId,
                ProgrammeName = tutor.Programme?.Name ?? "Belgium Campus programme",
                YearOfStudy = tutor.YearOfStudy,
                UpcomingAvailabilityCount = tutor.TutorAvailabilities.Count(slot =>
                    slot.IsActive && slot.AvailableTime > DateTimeOffset.UtcNow),
                NextAvailableAt = tutor.TutorAvailabilities
                    .Where(slot =>
                        slot.IsActive && slot.AvailableTime > DateTimeOffset.UtcNow)
                    .Select(slot => (DateTimeOffset?)slot.AvailableTime)
                    .OrderBy(value => value)
                    .FirstOrDefault(),

                Modules = tutor.TutorCourseModules
                    .Select(item =>
                        item.ProgrammeModule.ModuleName)
                    .OrderBy(name => name)
                    .ToList(),

                ModuleCodes = tutor.TutorCourseModules
                    .Select(item => item.ProgrammeModule.ModuleCode)
                    .OrderBy(code => code)
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
            DisplayName = GetStaticTutorName(tutor.TutorId),
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

    public async Task<IReadOnlyList<ProgrammeOfStudy>>
        GetProgrammesAsync(
            CancellationToken cancellationToken = default)
    {
        return await _context.ProgrammesOfStudy
            .AsNoTracking()
            .OrderBy(programme => programme.Name)
            .ToListAsync(cancellationToken);
    }

    private static string GetStaticTutorName(int tutorId)
    {
        int nameIndex = Math.Max(tutorId - 1, 0);
        return StaticTutorNames[nameIndex % StaticTutorNames.Length];
    }
}
