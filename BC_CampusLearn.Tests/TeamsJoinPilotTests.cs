using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using BC_CampusLearn.Services.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BC_CampusLearn.Tests;

public sealed class TeamsJoinPilotTests
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111";
    private const string TutorObject = "22222222-2222-2222-2222-222222222222";
    private const string App = "33333333-3333-3333-3333-333333333333";
    private const string JoinUrl = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_pilot/0?context=test";

    [Fact]
    public async Task TutorJoin_StartsOnceWithSystemAuditAndNormalCountdown()
    {
        using var f = new Fixture();
        using var envelope = f.Envelope();
        await f.Handler.HandleAsync(envelope.RootElement, default);
        await f.Handler.HandleAsync(envelope.RootElement, default);
        f.Db.ChangeTracker.Clear();
        var booking = await f.Db.Bookings.Include(b => b.SessionExecution).Include(b => b.StatusHistory).SingleAsync();
        Assert.Equal(BookingStatus.InProgress, booking.Status);
        Assert.Equal(SessionStartSource.TeamsApplication, booking.SessionExecution!.StartSource);
        Assert.Equal(f.Clock.Now.Add(SessionSchedulingRules.TriggeredCountdownLength), booking.SessionExecution.ExpectedCompletionAt);
        var history = Assert.Single(booking.StatusHistory);
        Assert.True(history.ChangedBySystem);
        Assert.Null(history.ChangedByBcUserId);
        Assert.Equal("TutorJoinedTeams", history.ReasonCode);
        Assert.Single(await f.Db.UserNotifications.Where(n => n.Title == "Session started").ToListAsync());
    }

    [Theory]
    [InlineData("otherTutor")]
    [InlineData("otherParticipant")]
    [InlineData("otherTenant")]
    [InlineData("lobby")]
    [InlineData("left")]
    [InlineData("callStarted")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("tooEarly")]
    [InlineData("tooLate")]
    [InlineData("pending")]
    [InlineData("cancelled")]
    [InlineData("suspended")]
    [InlineData("wrongResource")]
    [InlineData("wrongState")]
    [InlineData("wrongCertificate")]
    [InlineData("tampered")]
    [InlineData("invalidToken")]
    [InlineData("missingToken")]
    [InlineData("disabled")]
    public async Task InvalidOrIneligibleEvents_DoNotStart(string scenario)
    {
        using var f = new Fixture();
        if (scenario == "otherTutor")
        {
            f.Db.ChangeTracker.Clear();
            var tutor = f.Db.Tutors.Single();
            tutor.BcUserId = 3;
            f.Db.BcUsers.Add(new BcUser { BcUserId = 3, DisplayName = "Other", CreatedAt = DateTime.UtcNow });
        }
        if (scenario == "tooEarly") f.Booking.ScheduledStartTime = f.Clock.Now.AddMinutes(6);
        if (scenario == "tooLate") f.Booking.ScheduledStartTime = f.Clock.Now.AddMinutes(-15);
        if (scenario == "pending") f.Booking.Status = BookingStatus.Pending;
        if (scenario == "cancelled") f.Booking.Status = BookingStatus.Cancelled;
        if (scenario == "suspended") f.Db.Tutors.Single().Status = TutorStatus.Suspended;
        if (scenario == "disabled") f.Options.Enabled = false;
        if (scenario == "invalidToken") f.Validator.Valid = false;
        await f.Db.SaveChangesAsync();
        using var envelope = f.Envelope(scenario);
        await f.Handler.HandleAsync(envelope.RootElement, default);
        Assert.Empty(await f.Db.SessionExecutions.ToListAsync());
    }

    [Fact]
    public async Task Webhook_ValidatesPlainTextAndBoundsChunkedRequests()
    {
        using var f = new Fixture();
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?validationToken=a%2Bb%20c");
        var result = await TeamsJoinWebhook.ReceiveAsync(context, Options.Create(f.Options), f.Handler,
            NullLogger<TeamsJoinNotificationHandler>.Instance);
        var text = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("a+b c", text.ResponseContent);
        Assert.Equal("text/plain", text.ContentType);
        context.Request.QueryString = QueryString.Empty;
        context.Request.Body = new MemoryStream(new byte[256 * 1024 + 1]);
        result = await TeamsJoinWebhook.ReceiveAsync(context, Options.Create(f.Options), f.Handler,
            NullLogger<TeamsJoinNotificationHandler>.Instance);
        Assert.Equal(413, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        f.Options.Enabled = false;
        result = await TeamsJoinWebhook.ReceiveAsync(context, Options.Create(f.Options), f.Handler,
            NullLogger<TeamsJoinNotificationHandler>.Instance);
        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Subscriptions_CreateForUser2_RestoreRenewAndRemoveWithoutTouchingUnrelatedOnes()
    {
        using var f = new Fixture();
        var http = new GraphHandler();
        using var client = new HttpClient(http) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var service = new TeamsJoinSubscriptionService(client, new TokenProvider(), f.Db, Options.Create(f.Options),
            f.Crypto, f.Clock, NullLogger<TeamsJoinSubscriptionService>.Instance);
        http.Subscriptions.Add(new { id = Guid.NewGuid().ToString(), resource = "unrelated", clientState = "someone-else" });
        await service.ReconcileAsync(default);
        var created = Assert.Single(http.Writes);
        Assert.Equal(HttpMethod.Post, created.Method);
        Assert.Equal(f.Resource, created.Body!.Value.GetProperty("resource").GetString());
        Assert.True(created.Body.Value.GetProperty("includeResourceData").GetBoolean());
        var subscription = JsonSerializer.Deserialize<Dictionary<string, object>>(created.Body.Value.GetRawText())!;
        subscription["id"] = Guid.NewGuid().ToString();
        subscription["expirationDateTime"] = f.Clock.Now.AddMinutes(5);
        subscription["clientState"] = null!; // Graph redacts this on list/get.
        http.Subscriptions.Add(subscription);
        http.Writes.Clear();
        await service.ReconcileAsync(default);
        Assert.Equal(HttpMethod.Patch, Assert.Single(http.Writes).Method);
        http.Writes.Clear();
        f.Booking.Status = BookingStatus.Cancelled;
        await f.Db.SaveChangesAsync();
        await service.ReconcileAsync(default);
        Assert.Equal(HttpMethod.Delete, Assert.Single(http.Writes).Method);
    }

    [Theory]
    [InlineData("valid1", true)]
    [InlineData("valid2", true)]
    [InlineData("wrongPublisher", false)]
    [InlineData("wrongTenant", false)]
    [InlineData("wrongAudience", false)]
    [InlineData("expired", false)]
    [InlineData("wrongSignature", false)]
    public async Task MicrosoftTokens_RequireSignatureTenantAudienceLifetimeAndPublisher(string scenario, bool expected)
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test" };
        var manager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
        {
            SigningKeys = { key }
        });
        var validator = new TeamsJoinTokenValidator(Configuration(), manager);
        bool v2 = scenario == "valid2";
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: scenario == "wrongTenant" ? "https://sts.windows.net/other/" :
                v2 ? $"https://login.microsoftonline.com/{Tenant}/v2.0" : $"https://sts.windows.net/{Tenant}/",
            audience: scenario == "wrongAudience" ? "other" : App,
            claims: [new Claim("tid", Tenant), new Claim("ver", v2 ? "2.0" : "1.0"),
                new Claim(v2 ? "azp" : "appid", scenario == "wrongPublisher" ? "other" : "0bf30f3b-4a52-48df-9a82-234910c4a086")],
            notBefore: now.AddHours(-2), expires: scenario == "expired" ? now.AddMinutes(-5) : now.AddMinutes(10),
            signingCredentials: new SigningCredentials(scenario == "wrongSignature" ?
                new RsaSecurityKey(other) { KeyId = "test" } : key, SecurityAlgorithms.RsaSha256));
        Assert.Equal(expected, await validator.ValidateAsync(new JwtSecurityTokenHandler().WriteToken(token), default));
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["AzureAd:TenantId"] = Tenant, ["AzureAd:ClientId"] = App }).Build();

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "mzala-join-test-" + Guid.NewGuid());
        public ApplicationDbContext Db { get; }
        public TeamsJoinPilotOptions Options { get; }
        public TeamsJoinPilotCrypto Crypto { get; }
        public TeamsJoinNotificationHandler Handler { get; }
        public Clock Clock { get; } = new();
        public Validator Validator { get; } = new();
        public Booking Booking { get; }
        public BcUser User { get; }
        public string Resource => TeamsJoinPilotCrypto.Resource(JoinUrl);
        private readonly X509Certificate2 certificate;

        public Fixture()
        {
            Directory.CreateDirectory(directory);
            using var rsa = RSA.Create(2048);
            certificate = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            Options = new TeamsJoinPilotOptions { Enabled = true, CertificatePath = Path.Combine(directory, "test.pfx"),
                CertificatePassword = "test-only", ClientStateKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
            File.WriteAllBytes(Options.CertificatePath, certificate.Export(X509ContentType.Pkcs12, Options.CertificatePassword));
            Crypto = new TeamsJoinPilotCrypto(Microsoft.Extensions.Options.Options.Create(Options));
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            User = new BcUser { BcUserId = 2, DisplayName = "Tutor", CreatedAt = DateTime.UtcNow,
                EncryptedEntraTenantId = Tenant, EncryptedEntraObjectId = TutorObject };
            var tutor = new Tutor { TutorId = 12, BcUserId = 2, BcUser = User, Status = TutorStatus.Approved,
                ReasonForTutoring = "Help", TeachingStyle = "Practical", PreviousTutoringExperience = "Peer",
                CampusOfStudy = "Pretoria", DemonstrationVideoUrl = "" };
            var module = new ProgrammeModule { ProgrammeModuleId = 3, ModuleCode = "TEST", ModuleName = "Test" };
            var assignment = new TutorCourseModule { TutorId = 12, Tutor = tutor, ProgrammeModuleId = 3, ProgrammeModule = module };
            Booking = new Booking { BookingId = 1, TutorId = 12, ProgrammeModuleId = 3, ProgrammeModule = module,
                TutorCourseModule = assignment, StudentBcUserId = 21, StudentName = "Student", Location = "Teams",
                Duration = SessionDuration.OneHour, Status = BookingStatus.Confirmed, ScheduledStartTime = Clock.Now,
                MeetingLink = new MeetingLink { Url = JoinUrl, TeamsEventId = "event-1" } };
            Db.Bookings.Add(Booking);
            Db.SaveChanges();
            Handler = new TeamsJoinNotificationHandler(Db, Microsoft.Extensions.Options.Options.Create(Options), Configuration(),
                Crypto, Validator, new Identities(), new SessionLifecycleService(Db, Clock), Clock,
                NullLogger<TeamsJoinNotificationHandler>.Instance);
        }

        public JsonDocument Envelope(string scenario = "valid")
        {
            var participant = new Dictionary<string, object?> {
                ["info"] = new { identity = new { user = new {
                    id = scenario == "otherParticipant" ? App : TutorObject, tenantId = scenario == "otherTenant" ? App : Tenant } } },
                ["isInLobby"] = scenario == "lobby"
            };
            if (scenario == "left") participant["removedState"] = new { reason = "left" };
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new {
                eventType = scenario == "callStarted" ? "callStarted" : "rosterUpdated",
                eventDateTime = scenario == "stale" ? Clock.Now.AddMinutes(-3) : scenario == "future" ? Clock.Now.AddMinutes(2) : Clock.Now,
            });
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(payload)!;
            data["participants@delta"] = new[] { participant };
            payload = JsonSerializer.SerializeToUtf8Bytes(data);
            byte[] key = RandomNumberGenerator.GetBytes(32);
            using var aes = Aes.Create();
            aes.Key = key;
            byte[] encrypted = aes.EncryptCbc(payload, key.AsSpan(0, 16), PaddingMode.PKCS7);
            using var rsa = certificate.GetRSAPublicKey()!;
            var content = new { data = Convert.ToBase64String(encrypted),
                dataKey = Convert.ToBase64String(rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA1)),
                dataSignature = Convert.ToBase64String(scenario == "tampered" ? new byte[32] : HMACSHA256.HashData(key, encrypted)),
                encryptionCertificateId = scenario == "wrongCertificate" ? "wrong" : Crypto.CertificateId(certificate.Thumbprint, 1) };
            var resource = scenario == "wrongResource" ? TeamsJoinPilotCrypto.Resource("https://teams.microsoft.com/other") : Resource;
            return JsonDocument.Parse(JsonSerializer.Serialize(new {
                validationTokens = scenario == "missingToken" ? Array.Empty<string>() : new[] { "test" },
                value = new[] { new { tenantId = Tenant, subscriptionId = Guid.NewGuid().ToString(), changeType = "updated",
                    resource, clientState = scenario == "wrongState" ? "wrong" : Crypto.ClientState(1, resource), encryptedContent = content } }
            }));
        }

        public void Dispose() { Db.Dispose(); certificate.Dispose(); Directory.Delete(directory, true); }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Validator : ITeamsJoinTokenValidator
    {
        public bool Valid { get; set; } = true;
        public Task<bool> ValidateAsync(string token, CancellationToken ct) => Task.FromResult(Valid);
    }
    private sealed class Identities : IEntraIdentityProtector
    {
        public ProtectedEntraIdentity Protect(string tenantId, string objectId) => new(tenantId, objectId, "test");
        public EntraIdentity Unprotect(string encryptedTenantId, string encryptedObjectId) => new(encryptedTenantId, encryptedObjectId);
        public string CreateLookupHash(string tenantId, string objectId) => "test";
    }
    private sealed class TokenProvider : ITeamsGraphTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken ct) => Task.FromResult("test-token");
    }
    private sealed class GraphHandler : HttpMessageHandler
    {
        public List<object> Subscriptions { get; } = [];
        public List<(HttpMethod Method, JsonElement? Body)> Writes { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { value = Subscriptions }) };
            JsonElement? body = request.Content is null ? null : JsonSerializer.Deserialize<JsonElement>(await request.Content.ReadAsStringAsync(ct));
            Writes.Add((request.Method, body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
