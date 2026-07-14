namespace BC_CampusLearn.Authentication;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    CurrentUser GetRequiredUser();
}