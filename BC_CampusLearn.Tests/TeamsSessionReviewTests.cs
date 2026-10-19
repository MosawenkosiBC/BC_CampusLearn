using System.Net;
using System.Text;
using System.Text.Json;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Gemini;
using BC_CampusLearn.Services.Teams;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TeamsSessionReviewTests
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111";
    private const string Organizer = "22222222-2222-2222-2222-222222222222";

    [Fact]
    public async Task SharedRecordingLookupUsesSavedAccountWithoutDependingOnTutorsCurrentIdentity()
    {
        await using var db = await CreateContextAsync();
        const string sharedId = "33333333-3333-3333-3333-333333333333";
        var snapshot = Identities.Protect(Tenant, sharedId);
        var booking = await db.Bookings.Include(b => b.MeetingLink).SingleAsync();
        booking.MeetingLink!.UsesSharedTeamsOrganizer = true;
        booking.MeetingLink.EncryptedTeamsOrganizerTenantId = snapshot.TenantId;
        booking.MeetingLink.EncryptedTeamsOrganizerObjectId = snapshot.ObjectId;
        booking.MeetingLink.TeamsOrganizerIdentityHash = snapshot.LookupHash;
        var tutor = await db.BcUsers.SingleAsync();
        tutor.EncryptedEntraObjectId = null;
        await db.SaveChangesAsync();
        var service = CreateService(db, new Handler(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            Assert.StartsWith($"/v1.0/users/{sharedId}/onlineMeetings", path);
            if (path.EndsWith("/onlineMeetings")) return Json("""{"value":[{"id":"meeting-1"}]}""");
            if (path.EndsWith("/recordings")) return Json("""{"value":[{"id":"recording-1"}]}""");
            return Json("""{"value":[]}""");
        }));
        Assert.Single((await service.GetAsync(1, default)).Recordings);
    }

    [Fact]
    public void AttendanceMatchesEmailAndMergesMultipleDevicesButPreservesReconnects()
    {
        using var document = JsonDocument.Parse("""
            [{"emailAddress":"TUTOR@example.com","attendanceIntervals":[
              {"joinDateTime":"2026-10-19T08:00:00Z","leaveDateTime":"2026-10-19T08:20:00Z"},
              {"joinDateTime":"2026-10-19T08:10:00Z","leaveDateTime":"2026-10-19T08:25:00Z"},
              {"joinDateTime":"2026-10-19T08:30:00Z","leaveDateTime":"2026-10-19T08:50:00Z"}]},
             {"emailAddress":"other@example.com","attendanceIntervals":[
              {"joinDateTime":"2026-10-19T08:00:00Z","leaveDateTime":"2026-10-19T09:00:00Z"}]}]
            """);
        var result = TeamsSessionReviewService.MatchAttendance("Tutor", "tutor@example.com", document.RootElement.EnumerateArray());
        Assert.Equal(2, result.Intervals.Count);
        Assert.Equal(TimeSpan.FromMinutes(45), result.Duration);
        Assert.Empty(TeamsSessionReviewService.MatchAttendance("Student", "guest@example.com", document.RootElement.EnumerateArray()).Intervals);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("not-pilot")]
    [InlineData("manual")]
    [InlineData("incomplete")]
    [InlineData("organizer-mismatch")]
    [InlineData("older-link")]
    [InlineData("missing-organizer")]
    public async Task IneligibleBookingsCannotReadOrStreamTeamsArtifacts(string scenario)
    {
        await using var db = await CreateContextAsync();
        var options = new TeamsMeetingOptions { Enabled = scenario != "disabled", PilotTutorBcUserIds = scenario == "not-pilot" ? [99] : [8] };
        var booking = await db.Bookings.Include(b => b.MeetingLink).SingleAsync();
        if (scenario == "manual") booking.MeetingLink!.TeamsManualFallbackAllowed = true;
        if (scenario == "incomplete") booking.Status = BookingStatus.Confirmed;
        if (scenario == "organizer-mismatch") booking.MeetingLink!.TeamsOrganizerIdentityHash = "different";
        if (scenario == "older-link") booking.MeetingLink!.TeamsEventId = null;
        if (scenario == "missing-organizer") booking.MeetingLink!.TeamsOrganizerIdentityHash = null;
        await db.SaveChangesAsync();
        var handler = new Handler(_ => throw new Exception("Must not contact Graph"));
        var service = CreateService(db, handler, options);
        var result = await service.GetAsync(1, default);
        Assert.Empty(result.Recordings);
        Assert.NotEmpty(result.Notices);
        string expectedReason = scenario switch
        {
            "disabled" => "disabled on this server",
            "not-pilot" => "not included in the Teams pilot",
            "manual" or "older-link" => "manually supplied or older meeting link",
            "missing-organizer" => "missing its organizer verification details",
            "incomplete" => "completed session",
            _ => "organizer could not be verified"
        };
        Assert.Contains(expectedReason, Assert.Single(result.Notices));
        await Assert.ThrowsAsync<TeamsMeetingException>(() => service.OpenRecordingAsync(1, "recording-1", null, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task AttendancePermissionFailureDoesNotHideRecordingOrTranscript()
    {
        await using var db = await CreateContextAsync();
        var service = CreateService(db, new Handler(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            if (path.EndsWith("/onlineMeetings")) return Json("""{"value":[{"id":"meeting-1"}]}""");
            if (path.EndsWith("/attendanceReports")) return new(HttpStatusCode.Forbidden);
            return Json("""{"value":[{"id":"artifact-1"}]}""");
        }));
        var result = await service.GetAsync(1, default);
        Assert.Single(result.Recordings);
        Assert.Single(result.Transcripts);
        Assert.Null(result.Duration);
        Assert.Contains(result.Notices, n => n.Contains("application access policy"));
    }

    [Fact]
    public async Task PilotBcUserTwoResolvesRecordingsUsingMicrosoftOrganizerIdRatherThanTutorId()
    {
        await using var db = await CreateContextAsync(tutorBcUserId: 2);
        var service = CreateService(db, new Handler(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            Assert.StartsWith($"/v1.0/users/{Organizer}/onlineMeetings", path);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            if (path.EndsWith("/onlineMeetings")) return Json("""{"value":[{"id":"meeting-1"}]}""");
            if (path.EndsWith("/recordings")) return Json("""{"value":[{"id":"recording-1"}]}""");
            return Json("""{"value":[]}""");
        }), new() { Enabled = true, PilotTutorBcUserIds = [2] });
        var result = await service.GetAsync(1, default);
        Assert.Equal("recording-1", Assert.Single(result.Recordings).Id);
        Assert.DoesNotContain(result.Notices, n => n.Contains("not included"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GraphDenialsIdentifyOperationAndMissingPolicyWithoutExposingRawMessage(bool missingPolicy)
    {
        await using var db = await CreateContextAsync();
        const string requestId = "33333333-3333-3333-3333-333333333333";
        var service = CreateService(db, new Handler(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (!missingPolicy && path.EndsWith("/onlineMeetings")) return Json("""{"value":[{"id":"meeting-1"}]}""");
            if (!missingPolicy && !path.EndsWith("/recordings")) return Json("""{"value":[]}""");
            var denied = Json(JsonSerializer.Serialize(new
            {
                error = new { code = "Forbidden", message = missingPolicy
                    ? "No application access policy found for this app. private-user-data"
                    : "Access denied for private-user-data" }
            }));
            denied.StatusCode = HttpStatusCode.Forbidden;
            denied.Headers.Add("request-id", requestId);
            return denied;
        }));
        var result = await service.GetAsync(1, default);
        string notice = Assert.Single(result.Notices.Where(n => n.Contains("HTTP 403")));
        Assert.Contains(missingPolicy ? "Teams meeting lookup" : "Teams recording lookup", notice);
        Assert.Contains(requestId, notice);
        Assert.Equal(missingPolicy, notice.Contains("Microsoft reports that no application access policy"));
        Assert.DoesNotContain("private-user-data", notice);
    }

    [Fact]
    public async Task GraphDiagnosticParserRejectsRawErrorsAndNonGuidReferences()
    {
        using var response = Json("""{"error":{"code":"Bearer secret token","message":"private recording content"}}""");
        response.Headers.Add("request-id", "private-reference");
        var failure = await TeamsGraphFailureDiagnostics.ReadAsync(response, default);
        Assert.Equal("unknown", failure.Code);
        Assert.Null(failure.RequestId);
        Assert.False(failure.MissingAccessPolicy);
    }

    [Fact]
    public async Task RecordingMustBelongToMeetingAndByteRangesAreForwarded()
    {
        await using var db = await CreateContextAsync();
        bool contentRequested = false;
        var service = CreateService(db, new Handler(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/onlineMeetings")) return Json("""{"value":[{"id":"meeting-1"}]}""");
            if (path.EndsWith("/recordings")) return Json("""{"value":[{"id":"recording-1"}]}""");
            Assert.EndsWith("/recordings/recording-1/content", path);
            Assert.Equal("bytes=0-3", request.Headers.Range?.ToString());
            contentRequested = true;
            return new(HttpStatusCode.PartialContent) { Content = new ByteArrayContent([1, 2, 3, 4]) };
        }));
        await Assert.ThrowsAsync<TeamsMeetingException>(() => service.OpenRecordingAsync(1, "other-meeting-recording", null, default));
        Assert.False(contentRequested);
        using var response = await service.OpenRecordingAsync(1, "recording-1", "bytes=0-3", default);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MeetingDurationUsesReportAndAmbiguousOccurrencesAreWithheld(bool multipleReports)
    {
        await using var db = await CreateContextAsync();
        var service = CreateService(db, new Handler(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/onlineMeetings")) return Json("""{"value":[{"id":"meeting-1"}]}""");
            if (path.EndsWith("/attendanceReports")) return Json(multipleReports
                ? """{"value":[{"id":"report-1"},{"id":"report-2"}]}"""
                : """{"value":[{"id":"report-1","meetingStartDateTime":"2026-10-19T08:02:00Z","meetingEndDateTime":"2026-10-19T08:49:00Z"}]}""");
            if (path.EndsWith("/attendanceRecords")) return Json("""
                {"value":[{"emailAddress":"student@example.com","attendanceIntervals":[
                {"joinDateTime":"2026-10-19T08:05:00Z","leaveDateTime":"2026-10-19T08:45:00Z"}]}]}
                """);
            return Json("""{"value":[]}""");
        }));
        var result = await service.GetAsync(1, default);
        if (multipleReports)
        {
            Assert.Null(result.Duration);
            Assert.Empty(result.Student.Intervals);
            Assert.Contains(result.Notices, n => n.Contains("multiple meeting occurrences"));
        }
        else
        {
            Assert.Equal(TimeSpan.FromMinutes(47), result.Duration);
            Assert.Equal(TimeSpan.FromMinutes(40), result.Student.Duration);
            Assert.Empty(result.Tutor.Intervals);
        }
    }

    [Fact]
    public async Task ContinuationCannotSendTokenToAnotherHost()
    {
        await using var db = await CreateContextAsync();
        var handler = new Handler(_ => Json("""{"value":[],"@odata.nextLink":"https://attacker.example/v1.0/users/123/onlineMeetings"}"""));
        var result = await CreateService(db, handler).GetAsync(1, default);
        Assert.NotEmpty(result.Notices);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("WEBVTT\n00:00:00.000 --> 00:22:00.000\nA worked example", 1)]
    [InlineData("A transcript without timestamps", 0)]
    public async Task AiSuggestionsRejectInvalidAndOutOfTranscriptRanges(string transcript, int expectedCount)
    {
        var handler = new Handler(_ => Json(JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text = JsonSerializer.Serialize(new
            {
                validity = "Possibly valid", topicCoverage = "Partially covered", summary = "Summary",
                evidence = new[] { "Example" }, concerns = Array.Empty<string>(), recommendation = "Review",
                suggestedWatchRanges = new[]
                {
                    new { startSeconds = 1221, endSeconds = 1320, reason = "Worked example" },
                    new { startSeconds = -1, endSeconds = 20, reason = "Invalid" },
                    new { startSeconds = 20, endSeconds = 20, reason = "Invalid" },
                    new { startSeconds = 1500, endSeconds = 1600, reason = "Beyond transcript" }
                }
            }) } } } } }
        })));
        var service = new GeminiSessionAssessmentService(new HttpClient(handler) { BaseAddress = new("https://generativelanguage.googleapis.com/") },
            new ConfigurationBuilder().Build(), NullLogger<GeminiSessionAssessmentService>.Instance);
        var result = await service.AssessAsync("test-key", new("Summary", "Module", new Dictionary<string, string>(), new Dictionary<string, string>(),
            new("transcript.vtt", "text/vtt", transcript, null)), default);
        Assert.Equal(expectedCount, result.SuggestedWatchRanges!.Count);
        if (expectedCount > 0) Assert.Equal("20:21 – 22:00", result.SuggestedWatchRanges[0].DisplayRange);
    }

    private static IEntraIdentityProtector Protector() => new EntraIdentityProtector(new EphemeralDataProtectionProvider(),
        Options.Create(new IdentityProtectionOptions { LookupKey = Convert.ToBase64String(new byte[32]) }));
    private static readonly IEntraIdentityProtector Identities = Protector();
    private static TeamsSessionReviewService CreateService(ApplicationDbContext db, Handler handler, TeamsMeetingOptions? options = null) =>
        new(new HttpClient(handler) { BaseAddress = new("https://graph.microsoft.com/v1.0/") }, new Tokens(), db, Identities,
            Options.Create(options ?? new() { Enabled = true, PilotTutorBcUserIds = [8] }),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AzureAd:TenantId"] = Tenant }).Build(), new Environment());

    private static async Task<ApplicationDbContext> CreateContextAsync(int tutorBcUserId = 8)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var stored = Identities.Protect(Tenant, Organizer);
        var tutor = new Tutor
        {
            TutorId = 12, BcUserId = tutorBcUserId, Status = TutorStatus.Approved, IsActive = true,
            ReasonForTutoring = "Help", TeachingStyle = "Practical", PreviousTutoringExperience = "Peer", CampusOfStudy = "Pretoria", DemonstrationVideoUrl = "",
            BcUser = new() { BcUserId = tutorBcUserId, Email = "tutor@example.com", EncryptedEntraTenantId = stored.TenantId, EncryptedEntraObjectId = stored.ObjectId }
        };
        var module = new ProgrammeModule { ProgrammeModuleId = 3, ModuleCode = "MOD101", ModuleName = "Module" };
        db.Bookings.Add(new()
        {
            BookingId = 1, TutorId = 12, TutorCourseModule = new() { Tutor = tutor, TutorId = 12, ProgrammeModule = module, ProgrammeModuleId = 3 },
            ProgrammeModule = module, ProgrammeModuleId = 3, StudentName = "Student", StudentEmail = "student@example.com", Location = "Online",
            Status = BookingStatus.Completed, StudentEvaluation = new(), TutorEvaluation = new(),
            MeetingLink = new() { Url = "https://teams.microsoft.com/l/meetup-join/test", TeamsEventId = "event-1", TeamsOrganizerIdentityHash = stored.LookupHash }
        });
        await db.SaveChangesAsync();
        return db;
    }
    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return Task.FromResult(send(request)); }
    }
    private sealed class Tokens : ITeamsGraphTokenProvider { public Task<string> GetTokenAsync(CancellationToken ct) => Task.FromResult("test-token"); }
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
