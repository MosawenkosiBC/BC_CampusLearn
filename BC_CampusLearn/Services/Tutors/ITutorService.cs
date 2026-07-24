using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;

namespace BC_CampusLearn.Services.Tutors;

public interface ITutorService
{
    Task<IReadOnlyList<TutorCardViewModel>>
        GetTutorsAsync(
            int? programmeModuleId,
            CancellationToken cancellationToken = default);

    Task<TutorDetailsViewModel?>
        GetTutorDetailsAsync(
            int tutorId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgrammeModule>>
        GetModulesAsync(
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgrammeOfStudy>>
        GetProgrammesAsync(
            CancellationToken cancellationToken = default);
}
