using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public sealed record TeamsReviewArtifact(string Id, DateTimeOffset? Start, DateTimeOffset? End);
public sealed record TeamsAttendanceInterval(DateTimeOffset Join, DateTimeOffset Leave);
public sealed record TeamsParticipantAttendance(string Label, IReadOnlyList<TeamsAttendanceInterval> Intervals)
{
    public TimeSpan Duration => TimeSpan.FromSeconds(Intervals.Sum(i => (i.Leave - i.Join).TotalSeconds));
}
public sealed class TeamsSessionReview
{
    public List<string> Notices { get; } = [];
    public List<TeamsReviewArtifact> Recordings { get; set; } = [];
    public List<TeamsReviewArtifact> Transcripts { get; set; } = [];
    public DateTimeOffset? Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public TeamsParticipantAttendance Tutor { get; set; } = new("Tutor", []);
    public TeamsParticipantAttendance Student { get; set; } = new("Student", []);
    public TimeSpan? Duration => End >= Start && Start.HasValue ? End - Start : null;
}

public interface ITeamsSessionReviewService
{
    Task<TeamsSessionReview> GetAsync(int bookingId, CancellationToken cancellationToken);
    Task<string> GetTranscriptAsync(int bookingId, string transcriptId, CancellationToken cancellationToken);
    Task<HttpResponseMessage> OpenRecordingAsync(int bookingId, string recordingId, string? range, CancellationToken cancellationToken);
}

// Only saved, application-created meetings for allowlisted tutors can be read.
// User-supplied recording URLs are never fetched with the application's credentials.
public sealed class TeamsSessionReviewService(
    HttpClient client, ITeamsGraphTokenProvider tokens, ApplicationDbContext context,
    IEntraIdentityProtector identities, IOptions<TeamsMeetingOptions> options,
    IConfiguration configuration, IHostEnvironment environment,
    ILogger<TeamsSessionReviewService>? logger = null) : ITeamsSessionReviewService
{
    private sealed record MeetingContext(Booking Booking, string Root);

    private async Task<MeetingContext> ResolveAsync(int bookingId, CancellationToken ct)
    {
        Booking? booking = await context.Bookings.AsNoTracking()
            .Include(b => b.MeetingLink).Include(b => b.StudentBcUser)
            .Include(b => b.TutorCourseModule).ThenInclude(a => a.Tutor).ThenInclude(t => t.BcUser)
            .SingleOrDefaultAsync(b => b.BookingId == bookingId && b.Status == BookingStatus.Completed &&
                b.TutorEvaluation != null && b.StudentEvaluation != null, ct);
        if (booking is null)
            throw new TeamsMeetingException("Teams evidence requires a completed session with both the student and tutor reviews submitted.");
        if (!options.Value.Enabled)
            throw new TeamsMeetingException("Automatic Teams evidence is disabled on this server. Ask the administrator to check the Teams integration setting.");
        MeetingLink? link = booking.MeetingLink;
        if (link is null || string.IsNullOrWhiteSpace(link.TeamsEventId) || link.TeamsManualFallbackAllowed)
            throw new TeamsMeetingException("This session uses a manually supplied or older meeting link. Automatic video preview and attendance are available only for meetings created with CampusLearn's Create Teams meeting button. Use Open submitted recording to view this session's recording.");
        if (string.IsNullOrWhiteSpace(link.TeamsOrganizerIdentityHash))
            throw new TeamsMeetingException("The saved Teams meeting is missing its organizer verification details. Ask the administrator to investigate this session's meeting metadata.");
        if (string.IsNullOrWhiteSpace(link.Url))
            throw new TeamsMeetingException("This session has no saved Teams joining link. Ask the administrator to investigate this session's meeting metadata.");
        if (environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:UseDevelopmentAuthentication"))
            throw new TeamsMeetingException("Use institutional Microsoft sign-in to access Teams session evidence.");
        BcUser tutor = booking.TutorCourseModule.Tutor.BcUser;
        string? protectedTenant = link.EncryptedTeamsOrganizerTenantId ?? tutor.EncryptedEntraTenantId;
        string? protectedObject = link.EncryptedTeamsOrganizerObjectId ?? tutor.EncryptedEntraObjectId;
        if (protectedTenant is null || protectedObject is null)
            throw new TeamsMeetingException("The tutor needs to sign in with Microsoft again before Teams evidence can be retrieved.");
        try
        {
            EntraIdentity identity = identities.Unprotect(protectedTenant, protectedObject);
            if (!Guid.TryParse(configuration["AzureAd:TenantId"], out Guid tenant) ||
                !Guid.TryParse(identity.TenantId, out Guid actualTenant) || tenant != actualTenant ||
                !Guid.TryParse(identity.ObjectId, out _) ||
                identities.CreateLookupHash(identity.TenantId, identity.ObjectId) != link.TeamsOrganizerIdentityHash)
                throw new TeamsMeetingException("The saved Teams organizer could not be verified.");
            string root = $"users/{Uri.EscapeDataString(identity.ObjectId)}/onlineMeetings";
            string filter = Uri.EscapeDataString($"JoinWebUrl eq '{link.Url.Replace("'", "''")}'");
            List<JsonElement> meetings = await ListAsync($"{root}?$filter={filter}", ct);
            if (meetings.Count != 1)
                throw new TeamsMeetingException("The saved Teams meeting is unavailable or has expired.");
            return new(booking, $"{root}/{Uri.EscapeDataString(meetings[0].GetProperty("id").GetString()!)}");
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new TeamsMeetingException("The tutor's Microsoft identity could not be read. Check persistent encryption keys and ask the tutor to sign in again.");
        }
    }

    public async Task<TeamsSessionReview> GetAsync(int bookingId, CancellationToken cancellationToken)
    {
        var result = new TeamsSessionReview();
        try
        {
            MeetingContext meeting = await ResolveAsync(bookingId, cancellationToken);
            // Independent permissions: an attendance failure must not hide an available recording.
            try
            {
                List<JsonElement> reports = await ListAsync($"{meeting.Root}/attendanceReports", cancellationToken);
                if (reports.Count == 1)
                {
                    JsonElement report = reports[0];
                    result.Start = Date(report, "meetingStartDateTime");
                    result.End = Date(report, "meetingEndDateTime");
                    var records = await ListAsync($"{meeting.Root}/attendanceReports/{Uri.EscapeDataString(report.GetProperty("id").GetString()!)}/attendanceRecords", cancellationToken);
                    result.Tutor = MatchAttendance("Tutor", meeting.Booking.TutorCourseModule.Tutor.BcUser.Email, records);
                    result.Student = MatchAttendance("Student", meeting.Booking.StudentBcUser?.Email ?? meeting.Booking.StudentEmail, records);
                }
                else result.Notices.Add(reports.Count == 0
                    ? "Teams attendance is not available yet, was disabled, or has expired."
                    : "Teams returned multiple meeting occurrences. Attendance is withheld until the correct occurrence can be identified.");
            }
            catch (TeamsMeetingException ex) { result.Notices.Add($"Attendance: {ex.Message}"); }
            foreach (string kind in new[] { "recordings", "transcripts" })
            {
                try
                {
                    var artifacts = (await ListAsync($"{meeting.Root}/{kind}", cancellationToken))
                        .Select(a => new TeamsReviewArtifact(a.GetProperty("id").GetString()!, Date(a, "createdDateTime"), Date(a, "endDateTime")))
                        .OrderBy(a => a.Start).ToList();
                    if (kind == "recordings") result.Recordings = artifacts;
                    else result.Transcripts = artifacts;
                }
                catch (TeamsMeetingException ex) { result.Notices.Add($"{kind}: {ex.Message}"); }
            }
        }
        catch (TeamsMeetingException ex) { result.Notices.Add(ex.Message); }
        return result;
    }

    public static TeamsParticipantAttendance MatchAttendance(string label, string? email, IEnumerable<JsonElement> records)
    {
        if (string.IsNullOrWhiteSpace(email)) return new(label, []);
        var intervals = records.Where(r => string.Equals(Text(r, "emailAddress")?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
            .SelectMany(r => r.TryGetProperty("attendanceIntervals", out var items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().ToArray() : [])
            .Select(i => (Join: Date(i, "joinDateTime"), Leave: Date(i, "leaveDateTime")))
            .Where(i => i.Join.HasValue && i.Leave.HasValue && i.Leave > i.Join)
            .Select(i => new TeamsAttendanceInterval(i.Join!.Value, i.Leave!.Value)).OrderBy(i => i.Join).ToList();
        // Merge overlap when a participant joins from multiple devices; reconnect gaps remain visible.
        var merged = new List<TeamsAttendanceInterval>();
        foreach (var interval in intervals)
        {
            if (merged.Count > 0 && interval.Join <= merged[^1].Leave)
                merged[^1] = merged[^1] with { Leave = interval.Leave > merged[^1].Leave ? interval.Leave : merged[^1].Leave };
            else merged.Add(interval);
        }
        return new(label, merged);
    }

    public async Task<string> GetTranscriptAsync(int bookingId, string transcriptId, CancellationToken cancellationToken)
    {
        string root = await ArtifactRootAsync(bookingId, "transcripts", transcriptId, cancellationToken);
        using HttpResponseMessage response = await SendAsync($"{root}/content?$format=text/vtt", null, cancellationToken);
        return await ReadTextAsync(response, cancellationToken);
    }

    public async Task<HttpResponseMessage> OpenRecordingAsync(int bookingId, string recordingId, string? range, CancellationToken cancellationToken)
    {
        string root = await ArtifactRootAsync(bookingId, "recordings", recordingId, cancellationToken);
        return await SendAsync($"{root}/content", range, cancellationToken);
    }

    private async Task<string> ArtifactRootAsync(int bookingId, string kind, string id, CancellationToken ct)
    {
        MeetingContext meeting = await ResolveAsync(bookingId, ct);
        if (string.IsNullOrWhiteSpace(id)) throw new TeamsMeetingException("Teams evidence is unavailable for this booking.");
        var artifacts = await ListAsync($"{meeting.Root}/{kind}", ct);
        if (!artifacts.Any(a => Text(a, "id") == id)) throw new TeamsMeetingException("This artifact does not belong to the saved meeting.");
        return $"{meeting.Root}/{kind}/{Uri.EscapeDataString(id)}";
    }

    private async Task<List<JsonElement>> ListAsync(string path, CancellationToken ct)
    {
        var items = new List<JsonElement>();
        string originalPath = new Uri(client.BaseAddress!, path).AbsolutePath;
        for (int page = 0; page < 20; page++)
        {
            if (new Uri(client.BaseAddress!, path).AbsolutePath != originalPath)
                throw new TeamsMeetingException("Teams returned an invalid continuation.");
            using var response = await SendAsync(path, null, ct);
            try
            {
                using var json = JsonDocument.Parse(await ReadTextAsync(response, ct));
                items.AddRange(json.RootElement.GetProperty("value").EnumerateArray().Select(i => i.Clone()));
                if (!json.RootElement.TryGetProperty("@odata.nextLink", out var next)) return items;
                path = next.GetString() ?? throw new TeamsMeetingException("Teams returned an invalid continuation.");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            { throw new TeamsMeetingException("Microsoft returned unreadable Teams evidence. Try again later."); }
        }
        throw new TeamsMeetingException("Teams returned too many artifacts. Ask the administrator to investigate.");
    }

    private static async Task<string> ReadTextAsync(HttpResponseMessage response, CancellationToken ct)
    {
        const int limit = 2 * 1024 * 1024;
        try
        {
            await response.Content.LoadIntoBufferAsync(limit, ct);
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException) { throw new TeamsMeetingException("The Teams evidence could not be read or exceeded the supported size."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TeamsMeetingException("Reading the Teams evidence timed out. Try again later."); }
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string? range, CancellationToken ct)
    {
        // Validate continuation URLs before attaching a bearer token.
        Uri uri = new(client.BaseAddress!, path);
        if (uri.Scheme != "https" || uri.Host != "graph.microsoft.com" || uri.Port != 443 || !uri.AbsolutePath.StartsWith("/v1.0/users/", StringComparison.Ordinal))
            throw new TeamsMeetingException("Teams returned an unsupported resource address.");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
        if (!string.IsNullOrWhiteSpace(range))
        {
            if (!RangeHeaderValue.TryParse(range, out var parsed) || parsed.Unit != "bytes" || parsed.Ranges.Count != 1)
                throw new TeamsMeetingException("The video range request is invalid.");
            request.Headers.Range = parsed;
        }
        try
        {
            HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode || (range is not null && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)) return response;
            var status = response.StatusCode;
            TeamsGraphFailureDiagnostics failure;
            try { failure = await TeamsGraphFailureDiagnostics.ReadAsync(response, ct); }
            finally { response.Dispose(); }
            string operation = Operation(uri);
            logger?.LogWarning("Teams evidence request failed. Operation {Operation}; HTTP status {StatusCode}; Graph code {GraphCode}; request {RequestId}; missing application access policy {MissingAccessPolicy}.",
                operation, (int)status, failure.Code, failure.RequestId, failure.MissingAccessPolicy);
            string reference = $"HTTP {(int)status}, {failure.Code}" +
                (failure.RequestId is null ? "." : $"; request {failure.RequestId}.");
            string denied = failure.MissingAccessPolicy
                ? "Microsoft reports that no application access policy was found for this app and organiser. Ask the administrator to assign a Teams application access policy containing the deployed app's client ID to the tutor's Microsoft account."
                : "Access denied. Ask the administrator to verify the deployed app's Graph consent, the organiser's Teams application access policy, and artifact access settings. Your personal ability to open the recording does not grant the application access.";
            throw new TeamsMeetingException(status switch
            {
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => $"Teams {operation}: {denied} Reference: {reference}",
                HttpStatusCode.NotFound => "The Teams artifact is not available yet or has expired.",
                HttpStatusCode.TooManyRequests => "Microsoft is limiting requests. Try again later.",
                _ => "Microsoft could not supply the Teams evidence. Try again later."
            });
        }
        catch (HttpRequestException) { throw new TeamsMeetingException("Microsoft Teams could not be reached. Try again later."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TeamsMeetingException("Microsoft Teams took too long to respond. Try again later."); }
    }

    private static string Operation(Uri uri)
    {
        string path = uri.AbsolutePath;
        if (path.Contains("/recordings/", StringComparison.Ordinal) && path.EndsWith("/content", StringComparison.Ordinal)) return "recording playback";
        if (path.Contains("/transcripts/", StringComparison.Ordinal) && path.EndsWith("/content", StringComparison.Ordinal)) return "transcript download";
        if (path.EndsWith("/recordings", StringComparison.Ordinal)) return "recording lookup";
        if (path.EndsWith("/transcripts", StringComparison.Ordinal)) return "transcript lookup";
        if (path.Contains("/attendanceReports", StringComparison.Ordinal)) return "attendance lookup";
        return "meeting lookup";
    }

    private static string? Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static DateTimeOffset? Date(JsonElement item, string name) => DateTimeOffset.TryParse(Text(item, name), out var date) ? date : null;
}
