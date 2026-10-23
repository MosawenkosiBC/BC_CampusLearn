using System.Security.Cryptography;
using System.Text.Json;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public sealed class TeamsJoinNotificationHandler(
    ApplicationDbContext db, IOptions<TeamsJoinPilotOptions> options,
    IConfiguration configuration, TeamsJoinPilotCrypto crypto,
    ITeamsJoinTokenValidator validator, IEntraIdentityProtector identities,
    ISessionLifecycleService lifecycle, TimeProvider clock,
    ILogger<TeamsJoinNotificationHandler> logger)
{
    public async Task HandleAsync(JsonElement envelope, CancellationToken ct)
    {
        if (!options.Value.IsConfigured || envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("validationTokens", out var tokens) || tokens.ValueKind != JsonValueKind.Array ||
            tokens.GetArrayLength() == 0 || tokens.GetArrayLength() > 10 ||
            !envelope.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array) return;
        foreach (var token in tokens.EnumerateArray())
            if (token.ValueKind != JsonValueKind.String || !await validator.ValidateAsync(token.GetString()!, ct)) return;

        foreach (var notification in values.EnumerateArray())
        {
            try { await ProcessAsync(notification, ct); }
            catch (Exception ex) when (ex is CryptographicException or JsonException or
                KeyNotFoundException or FormatException or ArgumentException or InvalidOperationException)
            {
                logger.LogWarning("Ignored invalid Teams join notification ({FailureType}).", ex.GetType().Name);
            }
        }
    }

    private async Task ProcessAsync(JsonElement notification, CancellationToken ct)
    {
        if (!Guid.TryParse(notification.GetProperty("tenantId").GetString(), out var tenant) ||
            !Guid.TryParse(configuration["AzureAd:TenantId"], out var expectedTenant) || tenant != expectedTenant ||
            notification.GetProperty("changeType").GetString() != "updated" ||
            !Guid.TryParse(notification.GetProperty("subscriptionId").GetString(), out _)) return;
        var resource = notification.GetProperty("resource").GetString()!;
        if (!crypto.TryValidateState(notification.GetProperty("clientState").GetString(), resource, out int bookingId)) return;

        var target = await (from booking in db.Bookings.AsNoTracking()
            join tutor in db.Tutors on booking.TutorId equals tutor.TutorId
            join user in db.BcUsers on tutor.BcUserId equals user.BcUserId
            where booking.BookingId == bookingId && user.BcUserId == TeamsJoinPilotOptions.PilotBcUserId &&
                tutor.Status == TutorStatus.Approved && booking.Status == BookingStatus.Confirmed &&
                booking.MeetingLink != null && booking.MeetingLink.TeamsEventId != null &&
                booking.MeetingLink.TeamsCancellationRequestedAt == null
            select new { Booking = booking, User = user, Url = booking.MeetingLink!.Url }).SingleOrDefaultAsync(ct);
        if (target is null || TeamsJoinPilotCrypto.NormalizeResource(resource) != TeamsJoinPilotCrypto.Resource(target.Url)) return;
        var now = clock.GetUtcNow();
        if (!SessionLifecyclePolicy.CanStart(target.Booking.ScheduledStartTime, now)) return;
        using var data = crypto.Decrypt(notification.GetProperty("encryptedContent"), bookingId);
        var root = data.RootElement;
        var eventType = root.TryGetProperty("eventType", out var type) ? type : root.GetProperty("callEventType");
        if (eventType.GetString() != "rosterUpdated") return;
        var eventTime = root.GetProperty("eventDateTime").GetDateTimeOffset();
        if (eventTime < now.AddMinutes(-2) || eventTime > now.AddSeconds(30) ||
            !SessionLifecyclePolicy.CanStart(target.Booking.ScheduledStartTime, eventTime)) return;
        var identity = identities.UnprotectForUser(target.User);
        if (!Guid.TryParse(identity.TenantId, out var tutorTenant) || tutorTenant != tenant ||
            !Guid.TryParse(identity.ObjectId, out var tutorObject)) return;
        foreach (var participant in root.GetProperty("participants@delta").EnumerateArray())
        {
            if (!participant.TryGetProperty("isInLobby", out var lobby) || lobby.ValueKind != JsonValueKind.False ||
                participant.TryGetProperty("removedState", out var removed) && removed.ValueKind != JsonValueKind.Null ||
                !participant.TryGetProperty("info", out var info) ||
                !info.TryGetProperty("identity", out var participantIdentity) ||
                !participantIdentity.TryGetProperty("user", out var user)) continue;
            if (!user.TryGetProperty("id", out var id) || !Guid.TryParse(id.GetString(), out var objectId) || objectId != tutorObject ||
                !user.TryGetProperty("tenantId", out var tid) || !Guid.TryParse(tid.GetString(), out var participantTenant) ||
                participantTenant != tutorTenant) continue;
            var result = await lifecycle.StartAsync(target.Booking.TutorId, target.User.BcUserId,
                bookingId, SessionStartSource.TeamsApplication, ct);
            if (result.Succeeded)
                logger.LogInformation("Teams join started pilot booking {BookingId} for BcUserId 2.", bookingId);
            return;
        }
    }
}

public static class TeamsJoinWebhook
{
    public static void MapTeamsJoinWebhook(this WebApplication app)
    {
        app.MapPost(TeamsJoinPilotOptions.WebhookPath, ReceiveAsync).AllowAnonymous();
    }

    public static async Task<IResult> ReceiveAsync(HttpContext context,
        IOptions<TeamsJoinPilotOptions> options, TeamsJoinNotificationHandler handler,
        ILogger<TeamsJoinNotificationHandler> logger)
    {
        if (!options.Value.IsConfigured) return Results.NotFound();
        if (context.Request.Query.TryGetValue("validationToken", out var validationToken))
            return validationToken.Count == 1 && validationToken[0]!.Length <= 4096
                ? Results.Text(validationToken[0], "text/plain") : Results.BadRequest();

        // Bound chunked requests as well as Content-Length, without accepting a volatile queue.
        const int limit = 256 * 1024;
        if (context.Request.ContentLength > limit) return Results.StatusCode(413);
        using var body = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
        {
            if (body.Length + count > limit) return Results.StatusCode(413);
            body.Write(buffer, 0, count);
        }
        try
        {
            using var document = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            await handler.HandleAsync(document.RootElement, context.RequestAborted);
            return Results.Accepted();
        }
        catch (JsonException) { return Results.Accepted(); }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            // Transient DB/metadata failures must be retried by Graph. No body, URLs or tokens in logs.
            logger.LogWarning("Teams join webhook processing failed ({FailureType}); delivery can retry.", ex.GetType().Name);
            return Results.StatusCode(503);
        }
    }
}
