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
    string StudentName);

public sealed record TeamsCalendarEvent(string Id, string? JoinUrl);

public interface ITeamsCalendarClient
{
    Task<bool> SupportsTeamsAsync(string organizerObjectId, CancellationToken cancellationToken);
    Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest request, CancellationToken cancellationToken);
    Task<TeamsCalendarEvent> GetAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken);
    Task CancelAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken);
    Task<string?> FindByTransactionAsync(string organizerObjectId, Guid transactionId, DateTimeOffset start, CancellationToken cancellationToken);
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
        using HttpRequestMessage request = new(HttpMethod.Post,
            $"users/{Uri.EscapeDataString(meeting.OrganizerObjectId)}/calendar/events");
        request.Content = JsonContent.Create(new
        {
            subject = meeting.Subject,
            body = new { contentType = "text", content = "Tutoring session scheduled through Mzala Connect." },
            start = new { dateTime = FormatUtc(meeting.Start), timeZone = "UTC" },
            end = new { dateTime = FormatUtc(meeting.End), timeZone = "UTC" },
            attendees = new[]
            {
                new { emailAddress = new { address = meeting.StudentEmail, name = meeting.StudentName }, type = "required" }
            },
            isOnlineMeeting = true,
            onlineMeetingProvider = "teamsForBusiness",
            allowNewTimeProposals = false,
            transactionId = meeting.TransactionId.ToString("D")
        });
        using JsonDocument document = await SendAsync(request, cancellationToken);
        return ReadEvent(document.RootElement);
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
                    HttpStatusCode.Forbidden => "Microsoft denied calendar access. Ask the administrator to check Calendars.ReadWrite application consent and access to the tutor's mailbox.",
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
