namespace BC_CampusLearn.Services.Students;

public sealed record StudentDetails(
    string StudentNumber,
    string FirstName,
    string? PreferredName,
    string Surname,
    string Email,
    string Programme,
    int YearOfStudy,
    string Campus);
