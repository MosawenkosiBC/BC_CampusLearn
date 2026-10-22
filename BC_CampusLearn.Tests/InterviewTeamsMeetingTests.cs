using System.Net;
using System.Text;
using System.Text.Json;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BC_CampusLearn.Tests;

public sealed class InterviewTeamsMeetingTests
{
    private const string Organizer = "33333333-3333-3333-3333-333333333333";
    private const string Join = "https://teams.microsoft.com/l/meetup-join/interview";

    [Fact]
    public async Task CreatesSharedMeetingWithCandidateAndOrdinaryPanelThenAdvancesOnce()
    {
        await using var f = await Fixture.Create();
        var result = await f.Service.CreateAsync(1, 2, Input(), default);
        Assert.True(result.Succeeded, result.Message);
        var request = Assert.Single(f.Calendar.Requests);
        Assert.Equal(Organizer, request.OrganizerObjectId);
        Assert.Equal("candidate@example.com", request.StudentEmail);
        Assert.Equal(2, request.Panel!.Count);
        Assert.False(request.Panel[0].Optional);
        Assert.True(request.Panel[1].Optional);
        Assert.Equal(8, request.Start.UtcDateTime.Hour); // 10:00 SAST
        Assert.Equal("Welcome to your interview.", request.Message);
        var tutor = await f.Db.Tutors.SingleAsync();
        Assert.Equal(TutorApplicationStage.Interview, tutor.ApplicationStage);
        Assert.Equal(Join, tutor.InterviewLocation);
        Assert.Equal(10, tutor.InterviewScheduledAt!.Value.Hour);
        Assert.Equal("Private notes", tutor.InterviewPreparationNotes);
        Assert.Single(f.Db.TutorApplicationReviewDecisions);
        Assert.Single(f.Db.UserNotifications);
        Assert.True((await f.Service.CreateAsync(1, 2, Input(), default)).Succeeded);
        Assert.Single(f.Calendar.Requests);
        Assert.Single(f.Db.TutorApplicationReviewDecisions);
    }

    [Fact]
    public async Task TimeoutKeepsShortlistAndRetryReusesPayloadTransactionAndOrganizer()
    {
        await using var f = await Fixture.Create();
        f.Calendar.Fail = true;
        Assert.False((await f.Service.CreateAsync(1, 2, Input(), default)).Succeeded);
        Assert.Equal(TutorApplicationStage.Shortlisted, (await f.Db.Tutors.SingleAsync()).ApplicationStage);
        Assert.Empty(f.Db.TutorApplicationReviewDecisions);
        f.Options.SharedOrganizerObjectId = Guid.NewGuid().ToString();
        f.Calendar.Fail = false;
        var changed = Input(); changed.Title = "Changed after timeout";
        Assert.True((await f.Service.CreateAsync(1, 2, changed, default)).Succeeded);
        Assert.Equal(f.Calendar.Requests[0], f.Calendar.Requests[1] with { Panel = f.Calendar.Requests[0].Panel });
        Assert.Equal("Tutor interview", f.Calendar.Requests[1].Subject);
        Assert.Equal(Organizer, f.Calendar.Requests[1].OrganizerObjectId);
    }

    [Fact]
    public async Task MissingJoinLinkRetriesExistingEventWithoutSendingAnotherInvitation()
    {
        await using var f = await Fixture.Create();
        f.Calendar.JoinUrl = null;
        Assert.False((await f.Service.CreateAsync(1, 2, Input(), default)).Succeeded);
        Assert.Equal("event-1", (await f.Db.TutorInterviewMeetings.SingleAsync()).EventId);
        Assert.Equal(TutorApplicationStage.Shortlisted, (await f.Db.Tutors.SingleAsync()).ApplicationStage);
        f.Calendar.JoinUrl = Join;
        Assert.True((await f.Service.CreateAsync(1, 2, Input(), default)).Succeeded);
        Assert.Single(f.Calendar.Requests);
        Assert.Equal(1, f.Calendar.Reads);
    }

    [Fact]
    public async Task InvalidScheduleAndUnauthorisedUserCannotSendInvitations()
    {
        await using var f = await Fixture.Create();
        var input = Input(); input.Date = "2020-01-01";
        Assert.False((await f.Service.CreateAsync(1, 2, input, default)).Succeeded);
        Assert.False((await f.Service.CreateAsync(1, 1, Input(), default)).Succeeded);
        input = Input(); input.Panel.Add(new() { Email = "ADMIN@example.com" });
        Assert.False((await f.Service.CreateAsync(1, 2, input, default)).Succeeded);
        Assert.Empty(f.Calendar.Requests);
        Assert.Empty(f.Db.TutorInterviewMeetings);
    }

    [Fact]
    public async Task EmptyPanelCannotCreateOrUpdateAnInterview()
    {
        await using var f = await Fixture.Create();
        var input = Input();
        input.Panel.Clear();
        var create = await f.Service.CreateAsync(1, 2, input, default);
        Assert.False(create.Succeeded);
        Assert.Contains("at least one", create.Message);
        Assert.Empty(f.Calendar.Requests);
        Assert.Empty(f.Db.TutorInterviewMeetings);

        Assert.True((await f.Service.CreateAsync(1, 2, Input(), default)).Succeeded);
        var update = await f.Service.UpdateAsync(1, 2, input, default);
        Assert.False(update.Succeeded);
        Assert.Contains("at least one", update.Message);
        Assert.Empty(f.Calendar.Updates);
    }

    [Fact]
    public async Task DeletedApplicationStillHasEnoughSavedInformationToCancel()
    {
        await using var f = await Fixture.Create();
        f.Calendar.JoinUrl = null;
        await f.Service.CreateAsync(1, 2, Input(), default);
        f.Db.Tutors.Remove(await f.Db.Tutors.SingleAsync());
        await f.Db.SaveChangesAsync();
        await f.Service.ProcessCancellationsAsync(default);
        Assert.Equal(1, f.Calendar.Cancellations);
        Assert.NotNull((await f.Db.TutorInterviewMeetings.SingleAsync()).CancelledAt);
    }

    [Fact]
    public async Task GraphPayloadContainsRequiredCandidateOptionalPanelAndNoCoorganizer()
    {
        string? json = null;
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Contains($"users/{Organizer}/calendar/events", request.RequestUri!.ToString());
            json = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created)
            { Content = new StringContent("{\"id\":\"event\",\"onlineMeeting\":{\"joinUrl\":\"" + Join + "\"}}", Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var client = new TeamsCalendarClient(http, new Tokens());
        var start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(2));
        await client.CreateAsync(new(Organizer, Guid.NewGuid(), "Interview", start, start.AddMinutes(30), "candidate@example.com", "Candidate",
            Panel: [new("admin@example.com", "Admin"), new("panel@example.com", "Panel", true), new("CANDIDATE@example.com", "Duplicate", true)], Message: "Welcome"), default);
        using var body = JsonDocument.Parse(json!);
        var attendees = body.RootElement.GetProperty("attendees").EnumerateArray().ToArray();
        Assert.Equal(3, attendees.Length);
        Assert.Equal("required", attendees[0].GetProperty("type").GetString());
        Assert.Equal("required", attendees[1].GetProperty("type").GetString());
        Assert.Equal("optional", attendees[2].GetProperty("type").GetString());
        Assert.DoesNotContain("coorganizer", json!);
        Assert.Equal("Welcome", body.RootElement.GetProperty("body").GetProperty("content").GetString());
        Assert.True(body.RootElement.GetProperty("isOnlineMeeting").GetBoolean());
    }

    [Fact]
    public async Task UpdateReusesMeetingAndRetainsOriginalMessageAndOrganizer()
    {
        await using var f = await Fixture.Create();
        Assert.True((await f.Service.CreateAsync(1, 2, Input(), default)).Succeeded);
        var input = Input(); input.Time = "12:00"; input.Title = "Rescheduled interview";
        input.Message = "This must not replace the Teams join block";
        input.Panel.RemoveAt(1);
        f.Options.SharedOrganizerObjectId = Guid.NewGuid().ToString();
        Assert.True((await f.Service.UpdateAsync(1, 2, input, default)).Succeeded);
        Assert.Single(f.Calendar.Requests);
        var update = Assert.Single(f.Calendar.Updates);
        Assert.Equal(Organizer, update.OrganizerObjectId);
        Assert.Equal("Rescheduled interview", update.Subject);
        Assert.Equal("Welcome to your interview.", update.Message);
        Assert.Single(update.Panel!);
        Assert.Equal(12, (await f.Db.Tutors.SingleAsync()).InterviewScheduledAt!.Value.Hour);
        Assert.Equal(Join, (await f.Db.Tutors.SingleAsync()).InterviewLocation);
        Assert.Equal(2, f.Db.TutorApplicationReviewDecisions.Count());
        Assert.True((await f.Service.UpdateAsync(1, 2, input, default)).Succeeded);
        Assert.Single(f.Calendar.Updates);
    }

    [Fact]
    public async Task FailedUpdateRetainsSavedUpdateAndDoesNotCreateAnotherMeeting()
    {
        await using var f = await Fixture.Create();
        await f.Service.CreateAsync(1, 2, Input(), default);
        var input = Input(); input.Time = "12:00";
        f.Calendar.Fail = true;
        Assert.False((await f.Service.UpdateAsync(1, 2, input, default)).Succeeded);
        Assert.Equal(10, (await f.Db.Tutors.SingleAsync()).InterviewScheduledAt!.Value.Hour);
        f.Calendar.Fail = false;
        input.Time = "15:00";
        Assert.True((await f.Service.UpdateAsync(1, 2, input, default)).Succeeded);
        Assert.Equal(12, (await f.Db.Tutors.SingleAsync()).InterviewScheduledAt!.Value.Hour);
        Assert.Single(f.Calendar.Requests);
        Assert.Equal(2, f.Calendar.Updates.Count);
    }

    [Fact]
    public async Task ShortlistFormIncludesLoggedInAdminAutomatically()
    {
        await using var f = await Fixture.Create();
        var page = new BC_CampusLearn.Pages.Administrator.Admin.ApplicationsModel(f.Db, new CurrentAdmin()) { Stage = "shortlist" };
        await page.OnGetAsync(default);
        var input = page.GetMeetingInput(Assert.Single(page.Candidates));
        var panel = Assert.Single(input.Panel);
        Assert.Equal("admin@example.com", panel.Email);
        Assert.False(panel.Optional);
    }

    [Fact]
    public async Task GraphUpdatePreservesTeamsBodyAndUsesExistingEvent()
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method);
            Assert.EndsWith("/events/existing-event", request.RequestUri!.ToString());
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.False(json.RootElement.TryGetProperty("body", out _));
            Assert.Equal("Updated interview", json.RootElement.GetProperty("subject").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"existing-event\"}", Encoding.UTF8, "application/json") };
        })) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(2));
        await new TeamsCalendarClient(http, new Tokens()).UpdateInterviewAsync("existing-event",
            new(Organizer, Guid.NewGuid(), "Updated interview", start, start.AddMinutes(30), "candidate@example.com", "Candidate"), default);
    }

    private sealed class CurrentAdmin : BC_CampusLearn.Authentication.ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public BC_CampusLearn.Authentication.CurrentUser GetRequiredUser() => new(2, "AD2", "Admin", "admin@example.com", BcUserRole.Admin);
    }

    private static InterviewMeetingInput Input() => new()
    {
        Title = "Tutor interview", Date = "2026-11-01", Time = "10:00", DurationMinutes = 30, Message = "Welcome to your interview.",
        Panel = [new() { Email = "admin@example.com", Name = "Admin" }, new() { Email = "panel@example.com", Name = "Panel", Optional = true }]
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; }
        public Calendar Calendar { get; } = new();
        public TeamsMeetingOptions Options { get; } = new() { Enabled = true, SharedOrganizerObjectId = Organizer };
        public InterviewTeamsMeetingService Service { get; }
        private Fixture()
        {
            Db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Service = new(Db, Calendar, new TeamsOrganizerSettingsStore(Db, Microsoft.Extensions.Options.Options.Create(Options)),
                Microsoft.Extensions.Options.Options.Create(Options), new ConfigurationBuilder().Build(), new Environment(), new Clock(), NullLogger<InterviewTeamsMeetingService>.Instance);
        }
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            f.Db.ProgrammesOfStudy.Add(new() { Id = 1, Name = "Computing" });
            f.Db.BcUsers.AddRange(new BcUser { BcUserId = 1, DisplayName = "Candidate", Email = "candidate@example.com", PersonnelNumber = "ST1" },
                new BcUser { BcUserId = 2, DisplayName = "Admin", Email = "admin@example.com", PersonnelNumber = "AD2", Role = BcUserRole.Admin });
            f.Db.Tutors.Add(new Tutor { TutorId = 1, BcUserId = 1, ProgrammeId = 1, Status = TutorStatus.Pending, ApplicationStage = TutorApplicationStage.Shortlisted,
                SubmittedAt = new DateTime(2026, 10, 21), InterviewPreparationNotes = "Private notes", YearOfStudy = 2, OverallAverage = 80,
                ReasonForTutoring = "Teach", TeachingStyle = "Examples", PreviousTutoringExperience = "None", CampusOfStudy = "Pretoria", DemonstrationVideoUrl = "https://example.com" });
            await f.Db.SaveChangesAsync(); return f;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class Calendar : ITeamsCalendarClient
    {
        public List<TeamsCalendarRequest> Requests { get; } = [];
        public List<TeamsCalendarRequest> Updates { get; } = [];
        public bool Fail { get; set; }
        public string? JoinUrl { get; set; } = Join;
        public int Reads { get; set; }
        public int Cancellations { get; set; }
        public Task<bool> SupportsTeamsAsync(string id, CancellationToken ct) => Task.FromResult(true);
        public Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest request, CancellationToken ct)
        { Requests.Add(request); if (Fail) throw new TeamsMeetingException("Timeout"); return Task.FromResult(new TeamsCalendarEvent("event-1", JoinUrl)); }
        public Task<TeamsCalendarEvent> GetAsync(string id, string eventId, CancellationToken ct)
        { Reads++; return Task.FromResult(new TeamsCalendarEvent(eventId, JoinUrl)); }
        public Task<TeamsCalendarEvent> UpdateInterviewAsync(string eventId, TeamsCalendarRequest request, CancellationToken ct)
        { Updates.Add(request); if (Fail) throw new TeamsMeetingException("Timeout"); return Task.FromResult(new TeamsCalendarEvent(eventId, JoinUrl)); }
        public Task CancelAsync(string id, string eventId, CancellationToken ct) { Cancellations++; return Task.CompletedTask; }
        public Task<string?> FindByTransactionAsync(string id, Guid transaction, DateTimeOffset start, CancellationToken ct) => Task.FromResult<string?>("event-1");
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero); }
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Tokens : ITeamsGraphTokenProvider { public Task<string> GetTokenAsync(CancellationToken ct) => Task.FromResult("test"); }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request); }
}
