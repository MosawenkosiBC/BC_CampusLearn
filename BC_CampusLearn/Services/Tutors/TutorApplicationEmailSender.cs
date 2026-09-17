using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace BC_CampusLearn.Services.Tutors;

public interface ITutorApplicationEmailSender
{
    Task SendInterviewRejectionAsync(
        string recipientEmail,
        string recipientName,
        CancellationToken cancellationToken);
}

public sealed class TutorApplicationEmailOptions
{
    public const string SectionName = "TutorApplicationEmail";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "BC CampusLearn Tutor Team";
}

public sealed class TutorApplicationEmailSender(
    IOptions<TutorApplicationEmailOptions> options,
    ILogger<TutorApplicationEmailSender> logger)
    : ITutorApplicationEmailSender
{
    private readonly TutorApplicationEmailOptions _options = options.Value;

    public async Task SendInterviewRejectionAsync(
        string recipientEmail,
        string recipientName,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Tutor application email delivery is disabled. The interview rejection email for {RecipientEmail} was not sent.",
                recipientEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            logger.LogWarning(
                "Tutor application email delivery is enabled but the SMTP host or sender address is missing.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = "Tutor application outcome",
            Body = $"Dear {recipientName},{Environment.NewLine}{Environment.NewLine}" +
                "Thank you for the time, effort, and enthusiasm you invested " +
                "in the tutor application and interview process." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "After careful consideration, we have decided to move forward " +
                "with other candidates for this tutor intake. This decision " +
                "does not take away from your abilities or the value you could " +
                "bring to the learning community." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "We sincerely appreciate your interest in supporting fellow " +
                "students, and we encourage you to continue developing your " +
                "skills and to apply again when another opportunity becomes " +
                "available." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "We wish you every success with your studies and future goals." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"Kind regards,{Environment.NewLine}" +
                "BC CampusLearn Tutor Team",
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(
                _options.Username,
                _options.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "The interview rejection email for {RecipientEmail} could not be sent.",
                recipientEmail);
        }
    }
}
