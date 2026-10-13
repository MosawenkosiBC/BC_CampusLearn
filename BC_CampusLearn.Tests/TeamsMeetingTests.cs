using System.Net;
using System.Text;
using System.Text.Json;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
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

public sealed class TeamsMeetingTests
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string ObjectId = "22222222-2222-2222-2222-222222222222";
    private const string JoinUrl = "https://teams.microsoft.com/l/meetup-join/test";

    [Fact]
    public async Task Create_SavesTutorEventAndLink_WithoutConfirmingBooking_AndReusesSavedMeeting()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        MeetingLink link = await fixture.Context.MeetingLinks.SingleAsync();
        Assert.Equal(JoinUrl, link.Url);
        Assert.Equal("event-1", link.TeamsEventId);
        Assert.NotNull(link.TeamsCreationRequestId);
        Assert.Equal(fixture.Identities.CreateLookupHash(TenantId, ObjectId), link.TeamsOrganizerIdentityHash);
        Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.Empty(fixture.Context.BookingStatusHistory);
        TeamsCalendarRequest request = Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(ObjectId, request.OrganizerObjectId);
        Assert.Equal("student@institution.example", request.StudentEmail);
        Assert.Equal(fixture.Now.AddHours(2), request.Start);
        Assert.Equal(request.Start.AddHours(1), request.End);
        Assert.Contains("MOD101", request.Subject);
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Single(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task Create_PendingJoinLink_RetryRetrievesSameEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.Result = new("event-1", null);
        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal("event-1", (await fixture.Context.MeetingLinks.SingleAsync()).TeamsEventId);
        fixture.Calendar.Result = new("event-1", JoinUrl);
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(1, fixture.Calendar.GetCalls);
    }

    [Fact]
    public async Task Create_AmbiguousFailure_RetryUsesPersistedTransactionId()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.FailCreate = true;
        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        Guid? persistedId = (await fixture.Context.MeetingLinks.SingleAsync()).TeamsCreationRequestId;
        fixture.Calendar.FailCreate = false;
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Equal(2, fixture.Calendar.Requests.Count);
        Assert.All(fixture.Calendar.Requests, request => Assert.Equal(persistedId, request.TransactionId));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("not-allowlisted")]
    [InlineData("other-tutor")]
    [InlineData("inactive")]
    [InlineData("not-approved")]
    [InlineData("past")]
    [InlineData("cancelled")]
    [InlineData("in-person")]
    [InlineData("missing-identity")]
    [InlineData("corrupt-identity")]
    [InlineData("other-tenant")]
    [InlineData("bad-email")]
    [InlineData("development-login")]
    [InlineData("manual-link")]
    public async Task Create_InvalidEligibility_DoesNotContactMicrosoft(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        Booking booking = await fixture.Context.Bookings.Include(item => item.StudyArea).SingleAsync();
        Tutor tutor = await fixture.Context.Tutors.Include(item => item.BcUser).SingleAsync();
        int actor = 8;
        switch (scenario)
        {
            case "disabled": fixture.Options.Enabled = false; break;
            case "not-allowlisted": fixture.Options.PilotTutorBcUserIds = []; break;
            case "other-tutor": actor = 9; fixture.Options.PilotTutorBcUserIds = [9]; break;
            case "inactive": tutor.IsActive = false; break;
            case "not-approved": tutor.Status = TutorStatus.Deregistered; break;
            case "past": booking.ScheduledStartTime = fixture.Now; break;
            case "cancelled": booking.Status = BookingStatus.Cancelled; break;
            case "in-person": booking.StudyArea!.Name = "Library"; break;
            case "missing-identity": tutor.BcUser.EncryptedEntraObjectId = null; break;
            case "corrupt-identity": tutor.BcUser.EncryptedEntraObjectId = "invalid"; break;
            case "other-tenant": fixture.Configuration["AzureAd:TenantId"] = Guid.NewGuid().ToString(); break;
            case "bad-email": booking.StudentEmail = "invalid"; break;
            case "development-login": fixture.Environment.EnvironmentName = "Development"; fixture.Configuration["Authentication:UseDevelopmentAuthentication"] = "true"; break;
            case "manual-link": booking.MeetingLink = new MeetingLink { Url = "https://example.com/existing" }; break;
        }
        await fixture.Context.SaveChangesAsync();
        Assert.False((await fixture.Service.CreateAsync(actor, 1)).Succeeded);
        Assert.Equal(0, fixture.Calendar.SupportCalls);
        Assert.Empty(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task Create_UnsupportedCalendar_DoesNotCreateOrReserveMeeting()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.SupportsTeams = false;
        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Empty(fixture.Context.MeetingLinks);
        Assert.Empty(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task Confirm_CannotOverwriteGeneratedLink()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now));
        Assert.False((await lifecycle.ConfirmAsync(12, 8, 1, "https://example.com/replacement")).Succeeded);
        Assert.Equal(JoinUrl, (await fixture.Context.MeetingLinks.SingleAsync()).Url);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, JoinUrl)).Succeeded);
    }

    [Fact]
    public async Task ConcurrentReservation_OnlyWinnerContactsMicrosoftToCreateMeeting()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.MeetingLinks.Add(new MeetingLink { BookingId = 1 });
        await fixture.Context.SaveChangesAsync();
        await using var competingContext = new ApplicationDbContext(fixture.ContextOptions);
        var competingCalendar = new FakeCalendar();
        var competingService = new BookingTeamsMeetingService(competingContext, competingCalendar,
            fixture.Identities, Options.Create(fixture.Options), fixture.Configuration, fixture.Environment,
            new FixedTimeProvider(fixture.Now), NullLogger<BookingTeamsMeetingService>.Instance);
        fixture.Calendar.BeforeSupport = async () => Assert.True((await competingService.CreateAsync(8, 1)).Succeeded);

        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Empty(fixture.Calendar.Requests);
        Assert.Single(competingCalendar.Requests);
    }

    [Fact]
    public async Task CalendarRequest_UsesTutorCalendar_ApplicationToken_AndUtcTimes()
    {
        string? capturedBody = null;
        var handler = new FakeHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"https://graph.microsoft.com/v1.0/users/{ObjectId}/calendar/events", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            Assert.Contains("IdType=\"ImmutableId\"", request.Headers.GetValues("Prefer"));
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.Created, "{\"id\":\"event-1\",\"onlineMeeting\":{\"joinUrl\":\"" + JoinUrl + "\"}}");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var client = new TeamsCalendarClient(http, new FakeTokens());
        var transactionId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 10, 14, 14, 0, 0, TimeSpan.FromHours(2));
        TeamsCalendarEvent result = await client.CreateAsync(new(ObjectId, transactionId, "MOD101", start, start.AddHours(1), "student@institution.example", "Student"), default);
        using var body = JsonDocument.Parse(capturedBody!);
        Assert.True(body.RootElement.GetProperty("isOnlineMeeting").GetBoolean());
        Assert.Equal("teamsForBusiness", body.RootElement.GetProperty("onlineMeetingProvider").GetString());
        Assert.Equal(transactionId.ToString("D"), body.RootElement.GetProperty("transactionId").GetString());
        Assert.Equal("2026-10-14T12:00:00", body.RootElement.GetProperty("start").GetProperty("dateTime").GetString());
        Assert.Equal("2026-10-14T13:00:00", body.RootElement.GetProperty("end").GetProperty("dateTime").GetString());
        Assert.Equal("UTC", body.RootElement.GetProperty("start").GetProperty("timeZone").GetString());
        Assert.Equal("student@institution.example", body.RootElement.GetProperty("attendees")[0].GetProperty("emailAddress").GetProperty("address").GetString());
        Assert.Equal(JoinUrl, result.JoinUrl);
    }

    [Theory]
    [InlineData(403, "calendar access")]
    [InlineData(429, "busy")]
    [InlineData(500, "retry")]
    public async Task CalendarErrors_AreActionable_AndDoNotExposeResponseBody(int status, string message)
    {
        using var http = new HttpClient(new FakeHandler(_ => Task.FromResult(JsonResponse((HttpStatusCode)status, "sensitive-details"))))
            { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var client = new TeamsCalendarClient(http, new FakeTokens());
        TeamsMeetingException exception = await Assert.ThrowsAsync<TeamsMeetingException>(() => client.SupportsTeamsAsync(ObjectId, default));
        Assert.Contains(message, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive-details", exception.Message);
    }

    [Theory]
    [InlineData("{\"allowedOnlineMeetingProviders\":[\"teamsForBusiness\"]}", true)]
    [InlineData("{\"allowedOnlineMeetingProviders\":[\"skypeForBusiness\"]}", false)]
    [InlineData("{\"allowedOnlineMeetingProviders\":[null,123]}", false)]
    public async Task CalendarCapability_ChecksTutorsDefaultCalendar(string json, bool supported)
    {
        using var http = new HttpClient(new FakeHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"/v1.0/users/{ObjectId}/calendar", request.RequestUri!.AbsolutePath);
            Assert.Equal("?$select=allowedOnlineMeetingProviders", request.RequestUri.Query);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, json));
        })) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        Assert.Equal(supported, await new TeamsCalendarClient(http, new FakeTokens()).SupportsTeamsAsync(ObjectId, default));
    }

    [Fact]
    public async Task TokenProvider_MissingCredentialsFailsWithoutNetworkAccess()
    {
        var provider = new TeamsGraphTokenProvider(new ConfigurationBuilder().AddInMemoryCollection().Build());
        TeamsMeetingException exception = await Assert.ThrowsAsync<TeamsMeetingException>(() => provider.GetTokenAsync(default));
        Assert.Contains("not configured", exception.Message);
    }

    [Theory]
    [InlineData("https://teams.microsoft.com/valid", true)]
    [InlineData("http://teams.microsoft.com/insecure", false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task CalendarEvent_RejectsUnsafeLinks(string url, bool valid)
    {
        using var http = new HttpClient(new FakeHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { id = "event-1", onlineMeeting = new { joinUrl = url } })))))
            { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var client = new TeamsCalendarClient(http, new FakeTokens());
        if (valid) Assert.Equal(url, (await client.GetAsync(ObjectId, "event-1", default)).JoinUrl);
        else await Assert.ThrowsAsync<TeamsMeetingException>(() => client.GetAsync(ObjectId, "event-1", default));
    }

    [Fact]
    public void Identity_CanBeRecoveredWithSameProtector_WhileStoredValuesRemainEncrypted()
    {
        IEntraIdentityProtector protector = CreateProtector();
        ProtectedEntraIdentity stored = protector.Protect(TenantId, ObjectId);
        Assert.DoesNotContain(TenantId, stored.TenantId);
        Assert.DoesNotContain(ObjectId, stored.ObjectId);
        Assert.Equal(new EntraIdentity(TenantId, ObjectId), protector.Unprotect(stored.TenantId, stored.ObjectId));
    }

    private static IEntraIdentityProtector CreateProtector() => new EntraIdentityProtector(
        new EphemeralDataProtectionProvider(), Options.Create(new IdentityProtectionOptions { LookupKey = Convert.ToBase64String(new byte[32]) }));
    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
    private sealed class FakeTokens : ITeamsGraphTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult("test-token");
    }
    private sealed class FakeCalendar : ITeamsCalendarClient
    {
        public List<TeamsCalendarRequest> Requests { get; } = [];
        public TeamsCalendarEvent Result { get; set; } = new("event-1", JoinUrl);
        public bool FailCreate { get; set; }
        public bool SupportsTeams { get; set; } = true;
        public int SupportCalls { get; private set; }
        public int GetCalls { get; private set; }
        public Func<Task>? BeforeSupport { get; set; }
        public async Task<bool> SupportsTeamsAsync(string organizerObjectId, CancellationToken cancellationToken)
        {
            SupportCalls++;
            if (BeforeSupport is not null) await BeforeSupport();
            return SupportsTeams;
        }
        public Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (FailCreate) throw new TeamsMeetingException("Microsoft took too long to respond.");
            return Task.FromResult(Result);
        }
        public Task<TeamsCalendarEvent> GetAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken)
        {
            GetCalls++;
            Assert.Equal(ObjectId, organizerObjectId);
            Assert.Equal("event-1", eventId);
            return Task.FromResult(Result);
        }
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public DbContextOptions<ApplicationDbContext> ContextOptions { get; } = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public ApplicationDbContext Context { get; }
        public IEntraIdentityProtector Identities { get; } = CreateProtector();
        public FakeCalendar Calendar { get; } = new();
        public TeamsMeetingOptions Options { get; } = new() { Enabled = true, PilotTutorBcUserIds = [8] };
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AzureAd:TenantId"] = TenantId }).Build();
        public TestEnvironment Environment { get; } = new();
        public DateTimeOffset Now { get; } = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);
        public BookingTeamsMeetingService Service => new(Context, Calendar, Identities, Microsoft.Extensions.Options.Options.Create(Options), Configuration, Environment, new FixedTimeProvider(Now), NullLogger<BookingTeamsMeetingService>.Instance);

        public Fixture() => Context = new(ContextOptions);

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            ProtectedEntraIdentity stored = fixture.Identities.Protect(TenantId, ObjectId);
            var tutor = new Tutor
            {
                TutorId = 12, BcUserId = 8, Status = TutorStatus.Approved, IsActive = true,
                ReasonForTutoring = "Help", TeachingStyle = "Practical", PreviousTutoringExperience = "Peer", CampusOfStudy = "Pretoria", DemonstrationVideoUrl = "",
                BcUser = new BcUser { BcUserId = 8, DisplayName = "Tutor", Email = "tutor@institution.example", EncryptedEntraTenantId = stored.TenantId, EncryptedEntraObjectId = stored.ObjectId, EntraIdentityLookupHash = stored.LookupHash }
            };
            var module = new ProgrammeModule { ProgrammeModuleId = 3, ModuleCode = "MOD101", ModuleName = "Module" };
            var assignment = new TutorCourseModule { TutorId = 12, Tutor = tutor, ProgrammeModuleId = 3, ProgrammeModule = module, IsActive = true };
            fixture.Context.Bookings.Add(new Booking
            {
                BookingId = 1, TutorId = 12, TutorCourseModule = assignment, ProgrammeModuleId = 3, ProgrammeModule = module,
                StudyArea = new StudyArea { Name = "Online" }, StudentName = "Student", StudentEmail = "student@institution.example",
                Location = "Online", Status = BookingStatus.Pending, Duration = SessionDuration.OneHour, ScheduledStartTime = fixture.Now.AddHours(2), DateBooked = fixture.Now
            });
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
