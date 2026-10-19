using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsMeetingException(string message) : Exception(message);

public sealed record TeamsCalendarRequest(
    string OrganizerObjectId,
    Guid TransactionId,
    string Subject,
    DateTimeOffset Start,
    DateTimeOffset End,
    string StudentEmail,
    string StudentName,
    string? TutorEmail = null,
    string? TutorName = null);

public sealed record TeamsCalendarEvent(string Id, string? JoinUrl);

public interface ITeamsCalendarClient
{
    Task<bool> SupportsTeamsAsync(string organizerObjectId, CancellationToken cancellationToken);
    Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest request, CancellationToken cancellationToken);
    Task<TeamsCalendarEvent> GetAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken);
    Task CancelAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken);
    Task<string?> FindByTransactionAsync(string organizerObjectId, Guid transactionId, DateTimeOffset start, CancellationToken cancellationToken);
    Task EnsureCoOrganizerAsync(string organizerObjectId, string joinUrl, string tutorObjectId, CancellationToken cancellationToken) =>
        throw new TeamsMeetingException("Automatic co-organiser assignment is not available.");
}

public sealed class TeamsCalendarClient(HttpClient client, ITeamsGraphTokenProvider tokens)
    : ITeamsCalendarClient
{
    public async Task<bool> SupportsTeamsAsync(string organizerObjectId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            $"users/{Uri.EscapeDataString(organizerObjectId)}/calendar?$select=allowedOnlineMeetingProviders");
        using JsonDocument document = await SendAsync(request, cancellationToken);
        return document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("allowedOnlineMeetingProviders", out JsonElement providers) &&
            providers.ValueKind == JsonValueKind.Array &&
            providers.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String && value.GetString() == "teamsForBusiness");
    }

    public async Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest meeting, CancellationToken cancellationToken)
    {
        var attendees = new List<object>
        {
            new { emailAddress = new { address = meeting.StudentEmail, name = meeting.StudentName }, type = "required" }
        };
        if (!string.IsNullOrWhiteSpace(meeting.TutorEmail))
            attendees.Add(new { emailAddress = new { address = meeting.TutorEmail, name = meeting.TutorName }, type = "required" });
        using HttpRequestMessage request = new(HttpMethod.Post,
            $"users/{Uri.EscapeDataString(meeting.OrganizerObjectId)}/calendar/events");
        request.Content = JsonContent.Create(new
        {
            subject = meeting.Subject,
            body = new { contentType = "text", content = "Tutoring session scheduled through Mzala Connect." },
            start = new { dateTime = FormatUtc(meeting.Start), timeZone = "UTC" },
            end = new { dateTime = FormatUtc(meeting.End), timeZone = "UTC" },
            attendees,
            isOnlineMeeting = true,
            onlineMeetingProvider = "teamsForBusiness",
            allowNewTimeProposals = false,
            transactionId = meeting.TransactionId.ToString("D")
        });
        using JsonDocument document = await SendAsync(request, cancellationToken);
        return ReadEvent(document.RootElement);
    }

    public async Task EnsureCoOrganizerAsync(string organizerObjectId, string joinUrl, string tutorObjectId, CancellationToken cancellationToken)
    {
        try
        {
        if (!Guid.TryParse(organizerObjectId, out _) || !Guid.TryParse(tutorObjectId, out _))
            throw new TeamsMeetingException("The meeting organiser or tutor Microsoft identity is invalid.");
        if (organizerObjectId.Equals(tutorObjectId, StringComparison.OrdinalIgnoreCase)) return;
        string root = $"users/{Uri.EscapeDataString(organizerObjectId)}/onlineMeetings";
        string filter = Uri.EscapeDataString($"JoinWebUrl eq '{joinUrl.Replace("'", "''")}'");
        using var lookup = new HttpRequestMessage(HttpMethod.Get, $"{root}?$filter={filter}");
        lookup.Headers.TryAddWithoutValidation("Prefer", "include-unknown-enum-members");
        using var found = await SendAsync(lookup, cancellationToken);
        JsonElement[] meetings = found.RootElement.GetProperty("value").EnumerateArray().ToArray();
        if (meetings.Length != 1)
            throw new TeamsMeetingException("The calendar meeting exists, but Teams is still preparing co-organiser settings. Retry to finish the same meeting.");
        JsonElement meeting = meetings[0];
        string id = meeting.GetProperty("id").GetString()!;
        var participants = meeting.GetProperty("participants");
        var attendees = participants.TryGetProperty("attendees", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(a => a.Clone()).ToList() : [];
        bool assigned = false;
        var updated = new List<JsonElement>();
        foreach (var attendee in attendees)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(attendee.GetRawText())!.AsObject();
            string? userId = node["identity"]?["user"]?["id"]?.GetValue<string>();
            if (string.Equals(userId, tutorObjectId, StringComparison.OrdinalIgnoreCase))
            {
                if (node["role"]?.GetValue<string>() == "coorganizer") return;
                node["role"] = "coorganizer";
                assigned = true;
            }
            updated.Add(JsonSerializer.SerializeToElement(node));
        }
        if (!assigned)
            updated.Add(JsonSerializer.SerializeToElement(new { identity = new { user = new { id = tutorObjectId } }, role = "coorganizer" }));
        using var patch = new HttpRequestMessage(HttpMethod.Patch, $"{root}/{Uri.EscapeDataString(id)}");
        patch.Headers.TryAddWithoutValidation("Prefer", "include-unknown-enum-members");
        // Graph requires the full attendee list. Retain students and other roles.
        patch.Content = JsonContent.Create(new { participants = new { attendees = updated } });
        using var response = await SendAsync(patch, cancellationToken);
        bool confirmed = response.RootElement.TryGetProperty("participants", out var returned) &&
            returned.TryGetProperty("attendees", out var confirmedAttendees) && confirmedAttendees.EnumerateArray().Any(a =>
                a.TryGetProperty("role", out var role) && role.GetString() == "coorganizer" &&
                a.TryGetProperty("identity", out var identity) && identity.TryGetProperty("user", out var user) &&
                user.TryGetProperty("id", out var userId) && string.Equals(userId.GetString(), tutorObjectId, StringComparison.OrdinalIgnoreCase));
        if (!confirmed)
            throw new TeamsMeetingException("The meeting exists, but Microsoft has not confirmed the tutor as co-organiser. Retry to finish setup.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new TeamsMeetingException("Microsoft returned unreadable co-organiser settings. The meeting is saved; retry to finish setup.");
        }
    }

    public async Task<TeamsCalendarEvent> GetAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            $"users/{Uri.EscapeDataString(organizerObjectId)}/events/{Uri.EscapeDataString(eventId)}?$select=id,onlineMeeting");
        using JsonDocument document = await SendAsync(request, cancellationToken);
        return ReadEvent(document.RootElement);
    }

    public async Task CancelAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            $"users/{Uri.EscapeDataString(organizerObjectId)}/events/{Uri.EscapeDataString(eventId)}/cancel");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        request.Headers.Add("Prefer", "IdType=\"ImmutableId\"");
        request.Content = JsonContent.Create(new { comment = "This tutoring session was cancelled in Mzala Connect." });
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            // Successful cancellation returns 202 with no JSON. A removed event
            // is already cancelled, including a retry after an ambiguous timeout.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return;
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                using HttpRequestMessage check = new(HttpMethod.Get,
                    $"users/{Uri.EscapeDataString(organizerObjectId)}/events/{Uri.EscapeDataString(eventId)}?$select=isCancelled");
                check.Headers.Authorization = request.Headers.Authorization;
                check.Headers.Add("Prefer", "IdType=\"ImmutableId\"");
                using HttpResponseMessage checkedEvent = await client.SendAsync(check, cancellationToken);
                if (checkedEvent.StatusCode == HttpStatusCode.NotFound) return;
                if (checkedEvent.IsSuccessStatusCode)
                {
                    using JsonDocument document = JsonDocument.Parse(await checkedEvent.Content.ReadAsStringAsync(cancellationToken));
                    if (document.RootElement.ValueKind == JsonValueKind.Object &&
                        document.RootElement.TryGetProperty("isCancelled", out JsonElement cancelled) && cancelled.ValueKind == JsonValueKind.True)
                        return;
                }
            }
            throw new TeamsMeetingException("Microsoft could not cancel the calendar meeting. Its cancellation will be retried automatically.");
        }
        catch (HttpRequestException)
        {
            throw new TeamsMeetingException("Microsoft could not be reached to cancel the meeting.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TeamsMeetingException("Microsoft took too long to acknowledge meeting cancellation.");
        }
        catch (JsonException)
        {
            throw new TeamsMeetingException("Microsoft returned an unexpected cancellation status.");
        }
    }

    public async Task<string?> FindByTransactionAsync(string organizerObjectId, Guid transactionId, DateTimeOffset start, CancellationToken cancellationToken)
    {
        // Recover a create request whose HTTP response was lost. Never recreate
        // a cancelled meeting or match by its human-editable title/email.
        string? next = $"users/{Uri.EscapeDataString(organizerObjectId)}/calendar/calendarView?startDateTime={Uri.EscapeDataString(start.AddDays(-1).ToString("O", CultureInfo.InvariantCulture))}&endDateTime={Uri.EscapeDataString(start.AddDays(1).ToString("O", CultureInfo.InvariantCulture))}&$select=id,transactionId&$top=100";
        string? found = null;
        for (int page = 0; next is not null && page < 10; page++)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, next);
            using JsonDocument document = await SendAsync(request, cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("value", out JsonElement events) || events.ValueKind != JsonValueKind.Array)
                throw new TeamsMeetingException("Microsoft returned an unexpected calendar search result.");
            foreach (JsonElement item in events.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("transactionId", out JsonElement transaction) ||
                    transaction.ValueKind != JsonValueKind.String || !Guid.TryParse(transaction.GetString(), out Guid id) || id != transactionId) continue;
                string eventId = ReadEvent(item).Id;
                if (found is not null && found != eventId)
                    throw new TeamsMeetingException("More than one event matches the saved meeting request. Please contact the administrator.");
                found = eventId;
            }
            next = document.RootElement.TryGetProperty("@odata.nextLink", out JsonElement nextLink) && nextLink.ValueKind == JsonValueKind.String
                ? nextLink.GetString() : null;
            if (next is not null && (!Uri.TryCreate(next, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme != "https" || uri.Host != "graph.microsoft.com" || uri.Port != 443 || !uri.AbsolutePath.StartsWith("/v1.0/", StringComparison.Ordinal)))
                throw new TeamsMeetingException("Microsoft returned an invalid calendar search continuation.");
        }
        if (next is not null) throw new TeamsMeetingException("Calendar search has not completed. Cancellation will be retried.");
        return found;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        request.Headers.Add("Prefer", "IdType=\"ImmutableId\"");
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Never include Graph response bodies, tokens or account identifiers in user-facing errors.
                throw new TeamsMeetingException(response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => request.RequestUri?.ToString().Contains("onlineMeetings", StringComparison.Ordinal) == true
                        ? "Microsoft denied co-organiser setup. Check OnlineMeetings.ReadWrite.All application consent and the Teams application access policy assigned to the meeting organiser. The existing meeting will be reused on retry."
                        : "Microsoft denied calendar access. Ask the administrator to check Calendars.ReadWrite application consent and access to the organiser's mailbox.",
                    HttpStatusCode.Unauthorized => "Microsoft could not authorize CampusLearn. Please contact the administrator.",
                    HttpStatusCode.NotFound => "The tutor's Microsoft calendar or meeting could not be found. Please contact the administrator.",
                    HttpStatusCode.TooManyRequests => "Microsoft is busy. Please wait a moment before retrying.",
                    _ => "Microsoft could not complete meeting creation. Please retry shortly or contact the administrator."
                });
            }
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException)
        {
            throw new TeamsMeetingException("Microsoft could not be reached. Please retry shortly.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TeamsMeetingException("Microsoft took too long to respond. Please retry; CampusLearn will reuse the same meeting request.");
        }
        catch (JsonException)
        {
            throw new TeamsMeetingException("Microsoft returned an unexpected response. Please contact the administrator.");
        }
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static TeamsCalendarEvent ReadEvent(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new TeamsMeetingException("Microsoft returned an unexpected calendar event. Please contact the administrator.");

        string? id = root.TryGetProperty("id", out JsonElement identifier) && identifier.ValueKind == JsonValueKind.String
            ? identifier.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 1024)
            throw new TeamsMeetingException("Microsoft did not return a valid calendar event. Please contact the administrator.");

        string? url = null;
        if (root.TryGetProperty("onlineMeeting", out JsonElement meeting) && meeting.ValueKind == JsonValueKind.Object &&
            meeting.TryGetProperty("joinUrl", out JsonElement join) && join.ValueKind == JsonValueKind.String)
            url = join.GetString();

        if (!string.IsNullOrWhiteSpace(url) &&
            (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new TeamsMeetingException("Microsoft returned an invalid meeting link. Please contact the administrator.");

        return new TeamsCalendarEvent(id, url);
    }
}
