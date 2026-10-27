using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Teams;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Notifications;

public sealed class EmailNotificationOptions
{
    public const string SectionName = "EmailNotifications";
    public bool Enabled { get; set; } = true;
    public string SenderMailbox { get; set; } =
        "mzalaconnect@belgiumcampus.ac.za";
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public sealed class EmailDeliveryException(
    string message,
    bool isPermanent = false) : Exception(message)
{
    public bool IsPermanent { get; } = isPermanent;
}

public interface IEmailNotificationClient
{
    Task SendAsync(
        EmailNotificationOutbox email,
        CancellationToken cancellationToken);
}

public sealed class GraphEmailNotificationClient(
    HttpClient client,
    ITeamsGraphTokenProvider tokens,
    IOptions<EmailNotificationOptions> options)
    : IEmailNotificationClient
{
    private readonly EmailNotificationOptions _options = options.Value;

    public async Task SendAsync(
        EmailNotificationOutbox email,
        CancellationToken cancellationToken)
    {
        string sender = _options.SenderMailbox.Trim();
        if (!string.Equals(
            sender,
            "mzalaconnect@belgiumcampus.ac.za",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new EmailDeliveryException(
                "The configured sender mailbox is not permitted.",
                isPermanent: true);
        }

        string body = BuildBody(email);
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"users/{Uri.EscapeDataString(sender)}/sendMail");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await tokens.GetTokenAsync(cancellationToken));
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                subject = email.Subject,
                body = new { contentType = "Text", content = body },
                toRecipients = new[]
                {
                    new
                    {
                        emailAddress = new
                        {
                            address = email.RecipientEmail,
                            name = email.RecipientName
                        }
                    }
                }
            },
            saveToSentItems = true
        });

        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                request,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return;
            }

            TeamsGraphFailureDiagnostics failure =
                await TeamsGraphFailureDiagnostics.ReadAsync(
                    response,
                    cancellationToken);
            string reference = failure.RequestId is null
                ? string.Empty
                : $" Microsoft request {failure.RequestId}.";
            bool permanent = response.StatusCode is HttpStatusCode.BadRequest or
                HttpStatusCode.NotFound;
            throw new EmailDeliveryException(
                $"Graph email request failed with HTTP {(int)response.StatusCode} " +
                $"and code {failure.Code}.{reference}",
                permanent);
        }
        catch (HttpRequestException exception)
        {
            throw new EmailDeliveryException(
                $"Microsoft Graph could not be reached: {exception.GetType().Name}.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException(
                "Microsoft Graph did not respond before the email timeout.");
        }
    }

    private string BuildBody(EmailNotificationOutbox email)
    {
        string action = "Sign in to Mzala Connect to view the update.";
        string configuredBase = _options.PublicBaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(email.ActionPath) &&
            Uri.TryCreate(configuredBase, UriKind.Absolute, out Uri? baseUri) &&
            baseUri.Scheme == Uri.UriSchemeHttps)
        {
            action = new Uri(baseUri, email.ActionPath).AbsoluteUri;
        }

        return $"{email.Body.Trim()}{Environment.NewLine}{Environment.NewLine}" +
            $"{action}{Environment.NewLine}{Environment.NewLine}" +
            "This mailbox is not monitored. Please do not reply to this email." +
            $"{Environment.NewLine}{Environment.NewLine}Regards," +
            $"{Environment.NewLine}Mzala Connect";
    }
}
