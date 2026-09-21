using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Students;

public sealed class StudentDetailsApiOptionsValidator
    : IValidateOptions<StudentDetailsApiOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        StudentDetailsApiOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("StudentDetailsApi:BaseUrl must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            failures.Add("StudentDetailsApi:Username is required when the API is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            failures.Add("StudentDetailsApi:Password is required when the API is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            failures.Add("StudentDetailsApi:Token is required when the API is enabled.");
        }

        if (options.TimeoutSeconds is < 1 or > 30)
        {
            failures.Add("StudentDetailsApi:TimeoutSeconds must be between 1 and 30.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
