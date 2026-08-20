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
            .Include(tutor => tutor.Programme)
            .Include(tutor => tutor.BcUser)
            .Include(tutor => tutor.TutorAvailabilities)
            .OrderBy(tutor => tutor.TutorId)
            .ToListAsync(cancellationToken);
        return tutors
            .Select(tutor => new TutorCardViewModel
            {
                TutorId = tutor.TutorId,
                DisplayName = string.IsNullOrWhiteSpace(tutor.BcUser.DisplayName)
                    ? tutor.BcUser.PersonnelNumber
                    : tutor.BcUser.DisplayName,
                Biography = tutor.Biography ?? string.Empty,
                ProfileImagePath = tutor.ProfileImagePath,
                Initials = GetInitials(
                    string.IsNullOrWhiteSpace(tutor.BcUser.DisplayName)
                        ? tutor.BcUser.PersonnelNumber
                        : tutor.BcUser.DisplayName),
                ProgrammeId = tutor.ProgrammeId,
                ProgrammeName = tutor.Programme?.Name ?? "Belgium Campus programme",
                YearOfStudy = tutor.YearOfStudy,
                UpcomingAvailabilityCount = tutor.TutorAvailabilities.Count(slot =>
                    slot.AvailableTime > DateTimeOffset.UtcNow),
                NextAvailableAt = tutor.TutorAvailabilities
                    .Where(slot => slot.AvailableTime > DateTimeOffset.UtcNow)
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
            .Include(item => item.BcUser)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return null;
        }

        return new TutorDetailsViewModel
        {
            TutorId = tutor.TutorId,
            DisplayName = string.IsNullOrWhiteSpace(tutor.BcUser.DisplayName)
                ? tutor.BcUser.PersonnelNumber
                : tutor.BcUser.DisplayName,
            Email = tutor.BcUser.Email ?? string.Empty,
            Biography = tutor.Biography ?? string.Empty,
            ProfileImagePath = tutor.ProfileImagePath,
            Initials = GetInitials(
                string.IsNullOrWhiteSpace(tutor.BcUser.DisplayName)
                    ? tutor.BcUser.PersonnelNumber
                    : tutor.BcUser.DisplayName),
            LinkedInUrl = GetSafeExternalUrl(tutor.LinkedInUrl),
            GitHubUrl = GetSafeExternalUrl(tutor.GitHubUrl),
            Modules = tutor.TutorCourseModules
                .Select(item => new BookingModuleOptionViewModel
                {
                    ProgrammeModuleId = item.ProgrammeModuleId,
                    ModuleCode = item.ProgrammeModule.ModuleCode,
                    ModuleName = item.ProgrammeModule.ModuleName
                })
                .OrderBy(module => module.ModuleCode)
                .ToList(),

            AvailabilitySlots =
                tutor.TutorAvailabilities
                    .Where(slot =>
                        slot.AvailableTime >
                        DateTimeOffset.UtcNow)
                    .OrderBy(slot => slot.AvailableTime)
                    .Select(slot =>
                        new AvailabilitySlotViewModel
                        {
                            TutorAvailabilityId =
                                slot.TutorAvailabilityId,

                            AvailableTime =
                                slot.AvailableTime,

                            IsBooked = false
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

    private static string? GetSafeExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(
                url.Trim(),
                UriKind.Absolute,
                out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    private static string GetInitials(string displayName)
    {
        string[] nameParts = displayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        return nameParts.Length switch
        {
            > 1 => $"{nameParts[0][0]}{nameParts[^1][0]}"
                .ToUpperInvariant(),
            1 => nameParts[0][..1].ToUpperInvariant(),
            _ => "T"
        };
    }

}
