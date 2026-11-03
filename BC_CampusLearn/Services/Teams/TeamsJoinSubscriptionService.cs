using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsJoinSubscriptionService(
    HttpClient client, ITeamsGraphTokenProvider tokens, ApplicationDbContext db,
    IOptions<TeamsJoinPilotOptions> options, TeamsJoinPilotCrypto crypto, TimeProvider clock,
    ILogger<TeamsJoinSubscriptionService> logger)
{
    public async Task ReconcileAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) return;
        using var certificate = crypto.LoadCertificate();
        var now = clock.GetUtcNow();
        var bookings = await (from booking in db.Bookings.AsNoTracking()
            join tutor in db.Tutors on booking.TutorId equals tutor.TutorId
            where tutor.BcUserId == TeamsJoinPilotOptions.PilotBcUserId &&
                tutor.Status == TutorStatus.Approved &&
                booking.Status == BookingStatus.Confirmed &&
                booking.ScheduledStartTime > now.AddMinutes(-15) &&
                booking.ScheduledStartTime <= now.AddHours(24) &&
                booking.MeetingLink != null && booking.MeetingLink.TeamsEventId != null &&
                booking.MeetingLink.TeamsCancellationRequestedAt == null
            select new { booking.BookingId, booking.MeetingLink!.Url }).ToListAsync(ct);
        var wanted = bookings.ToDictionary(b => b.BookingId, b => TeamsJoinPilotCrypto.Resource(b.Url));
        var existing = new List<JsonElement>();
        string? page = "subscriptions";
        do
        {
            using var response = await SendAsync(HttpMethod.Get, page, null, ct);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            existing.AddRange(document.RootElement.GetProperty("value").EnumerateArray().Select(e => e.Clone()));
            page = document.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
            if (page is not null && (!Uri.TryCreate(page, UriKind.Absolute, out var uri) ||
                uri.Scheme != "https" || uri.Host != "graph.microsoft.com" || !uri.AbsolutePath.StartsWith("/v1.0/", StringComparison.Ordinal)))
                throw new InvalidOperationException("Unexpected Graph subscriptions continuation URL.");
        } while (page is not null);

        var found = new HashSet<int>();
        foreach (var subscription in existing)
        {
            string resource = subscription.GetProperty("resource").GetString()!;
            string? certificateId = subscription.TryGetProperty("encryptionCertificateId", out var c) ? c.GetString() : null;
            if (!crypto.TryGetOwnedBooking(certificateId, out int bookingId)) continue;
            string id = subscription.GetProperty("id").GetString()!;
            if (!Guid.TryParse(id, out _)) continue;
            bool usable = wanted.TryGetValue(bookingId, out var desired) &&
                TeamsJoinPilotCrypto.NormalizeResource(resource) == desired &&
                subscription.GetProperty("notificationUrl").GetString() == settings.NotificationUrl &&
                certificateId == crypto.CertificateId(certificate.Thumbprint, bookingId);
            if (!usable || !found.Add(bookingId))
            {
                using var deleted = await SendAsync(HttpMethod.Delete, $"subscriptions/{id}", null, ct);
                continue;
            }
            if (subscription.GetProperty("expirationDateTime").GetDateTimeOffset() <= now.AddMinutes(15))
            {
                using var renewed = await SendAsync(HttpMethod.Patch, $"subscriptions/{id}",
                    new { expirationDateTime = now.AddMinutes(50) }, ct);
                logger.LogInformation("Renewed Teams join subscription for pilot booking {BookingId}.", bookingId);
            }
        }
        foreach (var (bookingId, resource) in wanted.Where(pair => !found.Contains(pair.Key)))
        {
            using var response = await SendAsync(HttpMethod.Post, "subscriptions", new
            {
                changeType = "updated", notificationUrl = settings.NotificationUrl, resource,
                includeResourceData = true,
                encryptionCertificate = Convert.ToBase64String(certificate.Export(X509ContentType.Cert)),
                encryptionCertificateId = crypto.CertificateId(certificate.Thumbprint, bookingId),
                expirationDateTime = now.AddMinutes(50), clientState = crypto.ClientState(bookingId, resource)
            }, ct);
            logger.LogInformation("Created Teams join subscription for pilot booking {BookingId}.", bookingId);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return response;
        var status = (int)response.StatusCode;
        response.Dispose();
        // Graph bodies can contain meeting URLs and identity information.
        throw new HttpRequestException($"Teams join subscription request failed with HTTP {status}.", null, (System.Net.HttpStatusCode)status);
    }
}

public sealed class TeamsJoinSubscriptionWorker(
    IServiceScopeFactory scopes, IOptions<TeamsJoinPilotOptions> options,
    ILogger<TeamsJoinSubscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        if (!options.Value.IsConfigured)
        {
            logger.LogWarning("Teams join pilot is inactive: configure its HTTPS webhook, PFX certificate, password and client-state key. Manual start remains available.");
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<TeamsJoinSubscriptionService>().ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Teams join pilot subscription request failed with HTTP {StatusCode}. Check application consent and webhook reachability. Manual start remains available.", (int?)ex.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Teams join pilot subscription pass failed ({FailureType}). Check Graph consent, certificate configuration and webhook reachability. Manual start remains available.", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
