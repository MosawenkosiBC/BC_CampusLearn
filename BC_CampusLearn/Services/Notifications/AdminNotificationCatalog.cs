using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Notifications;

public sealed record AdminNotificationDefinition(string Key, string Category, string Title, string Recipient,
    string Trigger, string DefaultMessage, bool Editable = false, string Source = "Administrator");

public static class AdminNotificationCatalog
{
    public const string Interview = "tutor-interview";
    public const string Approval = "tutor-approval";
    public const string Farewell = "tutor-farewell";

    public static IReadOnlyList<AdminNotificationDefinition> All { get; } =
    [
        new(Interview, "Tutor applications", "Tutor application moved to interview", "Tutor applicant",
            "A shortlisted applicant is moved to the interview stage.", "Great news! Your tutor application has progressed to the interview stage. We’re excited to learn more about you and the contribution you could make as a Mzala Connect tutor. Your interview details will be shared with you shortly. Congratulations on reaching this stage, and best of luck with your interview!", true),
        new(Approval, "Tutor applications", "Tutor application approved", "New tutor",
            "An interviewed applicant is approved and moved to placement.", "Congratulations! We’re delighted to let you know that your tutor application has been approved. Welcome to the Mzala Connect Tutor Team! We’re excited to have you join us and look forward to the positive impact you’ll make by supporting and inspiring fellow students. Your tutoring journey starts here. Well done! 🎓", true),
        new(Farewell, "Tutor management", "Thank you for your contribution as a tutor", "Deregistered tutor",
            "An administrator ends the tutor's registration.", "Your registration in the tutoring programme has ended. Thank you for the time, care and knowledge you shared with fellow students. " +
                    "Your contribution is appreciated, and we wish you every success in your studies and future opportunities. " +
                    "Your student account remains available, and you can still view your past sessions and conversations. " +
                    "If you would like to discuss this change or future tutoring opportunities, please contact Student Support.", true),
        new("applications-open", "Tutor applications", "🥳 Tutor applications are open", "Students",
            "The administrator opens a new tutor application cycle.",
            "Interested in helping fellow students? Apply to become a peer tutor.\n\nWhat you'll need:\n• 65%+ overall average or 75%+ in a subject\n• No failed subjects\n• 8-15 hours per month to tutor\nApplications close: {closing date}"),
        new("application-rejected", "Tutor applications", "Tutor application unsuccessful", "Tutor applicant",
            "An application is rejected before shortlisting. An individually composed message can replace the default.",
            "Your tutor application was not moved to the shortlist. Reason: {reason, when provided}"),
        new("shortlist-rejected", "Tutor applications", "Tutor application unsuccessful", "Shortlisted applicant",
            "A shortlisted applicant is rejected before interview. An individually composed message can replace the default.",
            "Your tutor application will not progress to the interview stage. Reason: {reason, when provided}"),
        new("interview-rejected", "Tutor applications", "Tutor application unsuccessful", "Interviewed applicant",
            "An interviewed applicant is rejected. An individually composed message can replace the default.",
            "Thank you for the time and effort you put into your tutor interview. Although we will not be progressing your application further on this occasion, we truly appreciate your interest in becoming a Mzala Connect tutor. We encourage you to keep developing your skills and to apply again in the future. We wish you every success on your journey! Reason: {reason, when provided}"),
        new("application-email", "Tutor applications", "Tutor application communication sent", "Tutor applicant",
            "The administrator sends an application email.",
            "The Mzala Connect Tutor Team sent you an email about your tutor application. Please check {email address} for the full message."),
        new("senior-approved", "Senior tutor applications", "Senior tutor application update", "Tutor",
            "A senior tutor application is accepted.", "Your senior tutor application was accepted."),
        new("senior-rejected", "Senior tutor applications", "Senior tutor application update", "Tutor",
            "A senior tutor application is rejected.", "Your senior tutor application was rejected. {review note}"),
        new("module-assignment", "Tutor management", "Module assignment update", "Tutor",
            "An administrator changes a module assignment or reviews a module change request.",
            "{module code} — {module name}: {assignment or review decision}"),
        new("resource-removed-admin", "Learning resources", "Learning resource removed", "Resource author",
            "An administrator removes a learning resource.",
            "Your learning resource \"{resource topic}\" was removed by an administrator."),
        new("resource-nominated", "Learning resources", "Resource nomination added", "Tutor",
            "The Tutor Head nominates a tutor to create resources for a module.",
            "You can now create learning resources for {module code}.", Source: "Tutor Head"),
        new("resource-denominated", "Learning resources", "Resource nomination removed", "Tutor",
            "The Tutor Head removes a resource nomination.",
            "You can no longer create learning resources for {module code}. Existing resources remain available.", Source: "Tutor Head"),
        new("resource-removed-head", "Learning resources", "Learning resource removed", "Resource author",
            "The Tutor Head removes a learning resource.",
            "Your learning resource \"{resource topic}\" was removed by the Tutor Head.", Source: "Tutor Head"),
        new("announcement", "Announcements", "Announcement title", "Selected students or tutors",
            "An administrator sends an announcement to the selected audience.",
            "The title and message written in the announcement form above are sent to its selected audience.")
    ];

    public static AdminNotificationDefinition? Find(string? key) =>
        All.SingleOrDefault(item => item.Key == key);

    public static async Task<string> GetMessageAsync(ApplicationDbContext context,
        string key, CancellationToken cancellationToken)
    {
        var definition = Find(key);
        if (definition is null || !definition.Editable)
            throw new ArgumentException("Unknown editable notification.", nameof(key));
        return await context.NotificationTemplateSettings.AsNoTracking()
            .Where(item => item.TemplateKey == key).Select(item => item.Message)
            .SingleOrDefaultAsync(cancellationToken) ?? definition.DefaultMessage;
    }
}
