using System.Net;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using BC_CampusLearn.Services.Teams;
using Microsoft.Extensions.Options;
using Xunit;

namespace BC_CampusLearn.Tests;

public sealed class GraphEmailNotificationClientTests
{
    [Fact]
    public async Task SendAsync_UsesFixedMzalaMailboxAndApplicationToken()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
        };
        var client = new GraphEmailNotificationClient(
            httpClient,
            new TokenProvider(),
            Options.Create(new EmailNotificationOptions
            {
                SenderMailbox = "mzalaconnect@belgiumcampus.ac.za",
                PublicBaseUrl = "https://campuslearn.example/"
            }));

        await client.SendAsync(Email(), CancellationToken.None);

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/users/mzalaconnect%40belgiumcampus.ac.za/sendMail",
            handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("application-token", handler.AuthorizationParameter);
        Assert.Contains("student@example.com", handler.Body);
        Assert.Contains("https://campuslearn.example/Bookings/SessionDetails/42", handler.Body);
    }

    [Fact]
    public async Task SendAsync_TreatsForbiddenAsRetryable()
    {
        var client = new GraphEmailNotificationClient(
            new HttpClient(new RecordingHandler(HttpStatusCode.Forbidden))
            {
                BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
            },
            new TokenProvider(),
            Options.Create(new EmailNotificationOptions()));

        EmailDeliveryException exception = await Assert.ThrowsAsync<
            EmailDeliveryException>(() => client.SendAsync(
                Email(),
                CancellationToken.None));

        Assert.False(exception.IsPermanent);
        Assert.Contains("HTTP 403", exception.Message);
    }

    [Fact]
    public async Task SendAsync_RejectsAnyOtherSenderBeforeCallingGraph()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        var client = new GraphEmailNotificationClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://graph.microsoft.com/v1.0/")
            },
            new TokenProvider(),
            Options.Create(new EmailNotificationOptions
            {
                SenderMailbox = "someone.else@example.com"
            }));

        EmailDeliveryException exception = await Assert.ThrowsAsync<
            EmailDeliveryException>(() => client.SendAsync(
                Email(),
                CancellationToken.None));

        Assert.True(exception.IsPermanent);
        Assert.Null(handler.RequestUri);
    }

    private static EmailNotificationOutbox Email() => new()
    {
        RecipientEmail = "student@example.com",
        RecipientName = "Student",
        Subject = "Booking pending",
        Body = "Your request is pending until the tutor confirms it.",
        ActionPath = "/Bookings/SessionDetails/42"
    };

    private sealed class TokenProvider : ITeamsGraphTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult("application-token");
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    statusCode == HttpStatusCode.Accepted
                        ? string.Empty
                        : "{\"error\":{\"code\":\"ErrorAccessDenied\"}}")
            };
        }
    }
}
