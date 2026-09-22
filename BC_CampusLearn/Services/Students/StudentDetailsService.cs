using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

            StudentDetailsApiResponse? apiResponse =
                await DeserializeResponseAsync(
                    response.Content,
                    cancellationToken);

            if (!TryMapResponse(
                    apiResponse,
                    normalizedPersonnelNumber,
                    out StudentDetails? details,
                    out StudentDetailsValidationFailure validationFailure))
            {
                logger.LogWarning(
                    "The student details API response failed validation: {ValidationFailure}.",
                    validationFailure);
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
        out StudentDetails? details,
        out StudentDetailsValidationFailure failure)
    {
        details = null;
        failure = StudentDetailsValidationFailure.None;
        string? responseStudentNumber = GetStudentNumber(response);

        if (response is null)
        {
            failure = StudentDetailsValidationFailure.UnsupportedResponseShape;
            return false;
        }

        if (string.IsNullOrWhiteSpace(responseStudentNumber))
        {
            failure = StudentDetailsValidationFailure.MissingStudentNumber;
            return false;
        }

        if (!string.Equals(
                responseStudentNumber,
                requestedPersonnelNumber,
                StringComparison.Ordinal))
        {
            failure = StudentDetailsValidationFailure.StudentNumberMismatch;
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.FirstName))
        {
            failure = StudentDetailsValidationFailure.MissingFirstName;
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.Surname))
        {
            failure = StudentDetailsValidationFailure.MissingSurname;
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.Email))
        {
            failure = StudentDetailsValidationFailure.MissingEmail;
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.Programme))
        {
            failure = StudentDetailsValidationFailure.MissingProgramme;
            return false;
        }

        if (response.YearOfStudy is < 1 or > 4)
        {
            failure = StudentDetailsValidationFailure.InvalidYearOfStudy;
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.Campus))
        {
            failure = StudentDetailsValidationFailure.MissingCampus;
            return false;
        }

        if (response.FirstName.Length > 100 ||
            response.Surname.Length > 100 ||
            response.Email.Length > 320 ||
            response.Programme.Length > 100 ||
            response.Campus.Length > 100 ||
            response.PreferredName?.Length > 100)
        {
            failure = StudentDetailsValidationFailure.ValueTooLong;
            return false;
        }

        details = new StudentDetails(
            responseStudentNumber,
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

    private static async Task<StudentDetailsApiResponse?>
        DeserializeResponseAsync(
            HttpContent content,
            CancellationToken cancellationToken)
    {
        await using Stream stream = await content.ReadAsStreamAsync(
            cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        JsonElement payload = document.RootElement;

        if (payload.ValueKind == JsonValueKind.Array)
        {
            if (payload.GetArrayLength() != 1)
            {
                return null;
            }

            payload = payload[0];
        }

        if (payload.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoringCase(payload, "data", out JsonElement data))
        {
            payload = data;

            if (payload.ValueKind == JsonValueKind.Array)
            {
                if (payload.GetArrayLength() != 1)
                {
                    return null;
                }

                payload = payload[0];
            }
        }

        return payload.ValueKind == JsonValueKind.Object
            ? payload.Deserialize<StudentDetailsApiResponse>(JsonOptions)
            : null;
    }

    private static bool TryGetPropertyIgnoringCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? GetStudentNumber(
        StudentDetailsApiResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        return response.StudentNumber.ValueKind switch
        {
            JsonValueKind.String =>
                response.StudentNumber.GetString()?.Trim(),
            JsonValueKind.Number =>
                response.StudentNumber.GetRawText(),
            _ => null
        };
    }

    private sealed class StudentDetailsApiResponse
    {
        public JsonElement StudentNumber { get; init; }
        public string? FirstName { get; init; }
        public string? PreferredName { get; init; }
        public string? Surname { get; init; }
        public string? Email { get; init; }
        public string? Programme { get; init; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int YearOfStudy { get; init; }
        public string? Campus { get; init; }
    }

    private enum StudentDetailsValidationFailure
    {
        None,
        UnsupportedResponseShape,
        MissingStudentNumber,
        StudentNumberMismatch,
        MissingFirstName,
        MissingSurname,
        MissingEmail,
        MissingProgramme,
        InvalidYearOfStudy,
        MissingCampus,
        ValueTooLong
    }
}
