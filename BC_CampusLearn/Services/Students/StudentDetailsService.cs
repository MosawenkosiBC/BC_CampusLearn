using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Students;

public sealed class StudentDetailsService(
    HttpClient httpClient,
    IOptions<StudentDetailsApiOptions> options,
    ILogger<StudentDetailsService> logger)
    : IStudentDetailsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly StudentDetailsApiOptions _options = options.Value;

    public async Task<StudentDetailsResult> GetAsync(
        string personnelNumber,
        CancellationToken cancellationToken = default)
    {
        string normalizedPersonnelNumber = personnelNumber.Trim();

        if (!_options.Enabled)
        {
            logger.LogWarning("The student details API is disabled.");
            return new(StudentDetailsStatus.Unavailable);
        }

        if (!IsValidPersonnelNumber(normalizedPersonnelNumber))
        {
            logger.LogWarning(
                "A student details request was rejected because the personnel number format was invalid.");
            return new(StudentDetailsStatus.InvalidResponse);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"webhook/studentdetails?studentid={Uri.EscapeDataString(normalizedPersonnelNumber)}");

        string credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{_options.Username}:{_options.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            credentials);
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("wstoken", _options.Token)
        ]);

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new(StudentDetailsStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The student details API returned status code {StatusCode}.",
                    (int)response.StatusCode);
                return new(StudentDetailsStatus.Unavailable);
            }

            await response.Content.LoadIntoBufferAsync(
                64 * 1024,
                cancellationToken);

            StudentDetailsApiResponse? apiResponse = await response.Content
                .ReadFromJsonAsync<StudentDetailsApiResponse>(
                    JsonOptions,
                    cancellationToken);

            if (!TryMapResponse(
                    apiResponse,
                    normalizedPersonnelNumber,
                    out StudentDetails? details))
            {
                logger.LogWarning(
                    "The student details API returned an invalid or mismatched response.");
                return new(StudentDetailsStatus.InvalidResponse);
            }

            return StudentDetailsResult.Success(details!);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("The student details API request timed out.");
            return new(StudentDetailsStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "The student details API request failed.");
            return new(StudentDetailsStatus.Unavailable);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "The student details API returned malformed JSON.");
            return new(StudentDetailsStatus.InvalidResponse);
        }
    }

    private static bool IsValidPersonnelNumber(string value) =>
        value.Length is > 0 and <= 50 &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_');

    private static bool TryMapResponse(
        StudentDetailsApiResponse? response,
        string requestedPersonnelNumber,
        out StudentDetails? details)
    {
        details = null;

        if (response is null ||
            string.IsNullOrWhiteSpace(response.StudentNumber) ||
            !string.Equals(
                response.StudentNumber.Trim(),
                requestedPersonnelNumber,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(response.FirstName) ||
            response.FirstName.Length > 100 ||
            string.IsNullOrWhiteSpace(response.Surname) ||
            response.Surname.Length > 100 ||
            string.IsNullOrWhiteSpace(response.Email) ||
            response.Email.Length > 320 ||
            string.IsNullOrWhiteSpace(response.Programme) ||
            response.Programme.Length > 100 ||
            response.YearOfStudy is < 1 or > 4 ||
            string.IsNullOrWhiteSpace(response.Campus) ||
            response.Campus.Length > 100 ||
            response.PreferredName?.Length > 100)
        {
            return false;
        }

        details = new StudentDetails(
            response.StudentNumber.Trim(),
            response.FirstName.Trim(),
            string.IsNullOrWhiteSpace(response.PreferredName)
                ? null
                : response.PreferredName.Trim(),
            response.Surname.Trim(),
            response.Email.Trim(),
            response.Programme.Trim(),
            response.YearOfStudy,
            response.Campus.Trim());
        return true;
    }

    private sealed class StudentDetailsApiResponse
    {
        public string? StudentNumber { get; init; }
        public string? FirstName { get; init; }
        public string? PreferredName { get; init; }
        public string? Surname { get; init; }
        public string? Email { get; init; }
        public string? Programme { get; init; }
        public int YearOfStudy { get; init; }
        public string? Campus { get; init; }
    }
}
