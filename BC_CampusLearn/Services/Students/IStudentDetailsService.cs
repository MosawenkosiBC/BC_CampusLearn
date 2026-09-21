namespace BC_CampusLearn.Services.Students;

public interface IStudentDetailsService
{
    Task<StudentDetailsResult> GetAsync(
        string personnelNumber,
        CancellationToken cancellationToken = default);
}
