using System.Globalization;
using System.Net.Mail;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Services.Notifications;

public static class NotificationEmailQueue
{
    private static readonly CultureInfo SouthAfricanEnglish =
        CultureInfo.GetCultureInfo("en-ZA");

    public static void Add(
        ApplicationDbContext context,
        string eventKey,
        string? recipientEmail,
        string? recipientName,
        string subject,
        string body,
        string? actionPath,
        DateTimeOffset createdAt)
    {
        string email = recipientEmail?.Trim() ?? string.Empty;
        if (!MailAddress.TryCreate(email, out MailAddress? address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        context.EmailNotificationOutbox.Add(new EmailNotificationOutbox
        {
            EventKey = eventKey,
            RecipientEmail = email,
            RecipientName = CleanName(recipientName),
            Subject = subject,
            Body = body,
            ActionPath = actionPath,
            CreatedAt = createdAt,
            NextAttemptAt = createdAt
        });
    }

    public static string Greeting(string? name) =>
        $"Hello {CleanName(name)},";

    public static string SessionDetails(
        string moduleCode,
        string moduleName,
        DateTimeOffset scheduledStart,
        string location) =>
        $"Module: {FormatModule(moduleCode, moduleName)}{Environment.NewLine}" +
        $"Date: {FormatTime(scheduledStart)} SAST{Environment.NewLine}" +
        $"Location: {location}";

    public static string FormatModule(string moduleCode, string moduleName) =>
        !string.IsNullOrWhiteSpace(moduleCode) &&
        !string.IsNullOrWhiteSpace(moduleName)
            ? $"{moduleCode} - {moduleName}"
            : !string.IsNullOrWhiteSpace(moduleCode)
                ? moduleCode
                : !string.IsNullOrWhiteSpace(moduleName)
                    ? moduleName
                    : "Tutoring session";

    public static string FormatTime(DateTimeOffset value) =>
        value.ToOffset(TimeSpan.FromHours(2)).ToString(
            "dddd, d MMMM yyyy 'at' HH:mm",
            SouthAfricanEnglish);

    private static string CleanName(string? name)
    {
        string value = name?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return "there";
        }

        int separator = value.IndexOf(' ');
        return separator > 0 ? value[..separator] : value;
    }
}
