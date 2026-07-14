using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;

namespace BC_CampusLearn.Services.Tutors;

public interface ITutorService
{
    Task<IReadOnlyList<TutorCardViewModel>>
        GetTutorsAsync(
            int? courseModuleId,
            CancellationToken cancellationToken = default);

    Task<TutorDetailsViewModel?>
        GetTutorDetailsAsync(
            int tutorId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CourseModule>>
        GetModulesAsync(
            CancellationToken cancellationToken = default);
}