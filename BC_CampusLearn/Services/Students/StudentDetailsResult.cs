namespace BC_CampusLearn.Services.Students;

public enum StudentDetailsStatus
{
    Success,
    NotFound,
    Unavailable,
    InvalidResponse
}

public sealed record StudentDetailsResult(
    StudentDetailsStatus Status,
    StudentDetails? Details = null)
{
    public static StudentDetailsResult Success(StudentDetails details) =>
        new(StudentDetailsStatus.Success, details);
}
