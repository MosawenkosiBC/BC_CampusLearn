using BC_CampusLearn.Models.Entities;
using System.Globalization;

namespace BC_CampusLearn.Services.Notifications;

public sealed record BookingNotificationDetails(
    string ModuleCode,
    string ModuleName,
    string Location,
    DateTimeOffset ScheduledStartTime,
    string TutorName,
    string StudentName,
    string? MeetingLinkUrl);

public static class BookingNotificationFactory
{
    public static UserNotification BookingSubmitted(
        int studentBcUserId,
        int bookingId,
        BookingNotificationDetails details,
        DateTimeOffset createdAt) => Create(
            studentBcUserId,
            "Booking submitted",
            $"Your booking request for {FormatModule(details)} with {details.TutorName} was submitted for {FormatScheduledTime(details)} at {details.Location}.",
            StudentSessionUrl(bookingId),
            createdAt);

    public static UserNotification BookingReceived(
        int tutorBcUserId,
        int bookingId,
        BookingNotificationDetails details,
        DateTimeOffset createdAt) => Create(
            tutorBcUserId,
            "New booking request",
            $"{details.StudentName} submitted a booking request for {FormatModule(details)} on {FormatScheduledTime(details)} at {details.Location}.",
            TutorSessionUrl(bookingId),
            createdAt);

    public static UserNotification StudentStatusChanged(
        int studentBcUserId,
        int bookingId,
        BookingStatus status,
        BookingNotificationDetails details,
        DateTimeOffset createdAt) => Create(
            studentBcUserId,
            GetStatusTitle(status),
            GetStatusMessage(status, details),
            StudentSessionUrl(bookingId),
            createdAt);

    public static UserNotification StudentCancelled(
        int tutorBcUserId,
        int bookingId,
        BookingNotificationDetails details,
        DateTimeOffset createdAt) => Create(
            tutorBcUserId,
            "Booking cancelled by student",
            $"{details.StudentName} cancelled the {FormatModule(details)} session scheduled for {FormatScheduledTime(details)} at {details.Location}.",
            TutorSessionUrl(bookingId),
            createdAt);

    public static UserNotification SessionReminderForStudent(
        int studentBcUserId,
        int bookingId,
        BookingNotificationDetails details,
        DateTimeOffset createdAt) => Create(
            studentBcUserId,
            "Session starts in one hour",
            $"Your {FormatModule(details)} session with {details.TutorName} starts in one hour ({FormatScheduledTime(details)}) at {details.Location}; join using {FormatMeetingLink(details)}.",
            StudentSessionUrl(bookingId),
            createdAt);

    public static UserNotification SessionReminderForTutor(
        int tutorBcUserId,
        int bookingId,
        BookingNotificationDetails details,
        DateTimeOffset createdAt) => Create(
            tutorBcUserId,
            "Session starts in one hour",
            $"Your {FormatModule(details)} session with {details.StudentName} starts in one hour ({FormatScheduledTime(details)}) at {details.Location}; join using {FormatMeetingLink(details)}.",
            TutorSessionUrl(bookingId),
            createdAt);

    public static UserNotification TutorSessionCompleted(
        int tutorBcUserId,
        int bookingId,
        BookingNotificationDetails details,
        DateTimeOffset createdAt) => Create(
            tutorBcUserId,
            "Session completed",
            $"Your {FormatModule(details)} session with {details.StudentName} on {FormatScheduledTime(details)} at {details.Location} has been completed; please complete your session review while the details are still fresh.",
            TutorSessionUrl(bookingId),
            createdAt);

    private static UserNotification Create(
        int recipientBcUserId,
        string title,
        string message,
        string linkUrl,
        DateTimeOffset createdAt) => new()
        {
            RecipientBcUserId = recipientBcUserId,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            CreatedAt = createdAt
        };

    private static string GetStatusTitle(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => "Booking confirmed",
        BookingStatus.Declined => "Booking declined",
        BookingStatus.Cancelled => "Booking cancelled",
        BookingStatus.InProgress => "Session started",
        BookingStatus.Completed => "Session completed",
        _ => "Booking status updated"
    };

    private static string GetStatusMessage(
        BookingStatus status,
        BookingNotificationDetails details) => status switch
        {
            BookingStatus.Confirmed =>
                $"Your {FormatModule(details)} session with {details.TutorName} is confirmed for {FormatScheduledTime(details)} at {details.Location}; join using {FormatMeetingLink(details)}.",
            BookingStatus.Declined =>
                $"Your {FormatModule(details)} session request with {details.TutorName} for {FormatScheduledTime(details)} at {details.Location} was declined.",
            BookingStatus.Cancelled =>
                $"Your {FormatModule(details)} session with {details.TutorName}, scheduled for {FormatScheduledTime(details)} at {details.Location}, was cancelled.",
            BookingStatus.InProgress =>
                $"Your {FormatModule(details)} session with {details.TutorName} has started at {details.Location}; join now using {FormatMeetingLink(details)}.",
            BookingStatus.Completed =>
                $"Great work completing your {FormatModule(details)} session with {details.TutorName} on {FormatScheduledTime(details)} at {details.Location}! Please complete your review and help us make future tutoring sessions even better.",
            _ =>
                $"Your {FormatModule(details)} booking for {FormatScheduledTime(details)} at {details.Location} changed to {status.ToDisplayText()}."
        };

    private static string FormatModule(BookingNotificationDetails details)
    {
        if (!string.IsNullOrWhiteSpace(details.ModuleCode) &&
            !string.IsNullOrWhiteSpace(details.ModuleName))
        {
            return $"{details.ModuleCode} ({details.ModuleName})";
        }

        return !string.IsNullOrWhiteSpace(details.ModuleCode)
            ? details.ModuleCode
            : !string.IsNullOrWhiteSpace(details.ModuleName)
                ? details.ModuleName
                : "module";
    }

    private static string FormatScheduledTime(
        BookingNotificationDetails details) =>
        details.ScheduledStartTime
            .ToOffset(TimeSpan.FromHours(2))
            .ToString(
                "dddd, d MMMM yyyy 'at' HH:mm",
                CultureInfo.GetCultureInfo("en-ZA"));

    private static string FormatMeetingLink(
        BookingNotificationDetails details) =>
        string.IsNullOrWhiteSpace(details.MeetingLinkUrl)
            ? "the View session button"
            : details.MeetingLinkUrl;

    private static string StudentSessionUrl(int bookingId) =>
        $"/Bookings/SessionDetails/{bookingId}";

    private static string TutorSessionUrl(int bookingId) =>
        $"/Tutors/SessionDetails/{bookingId}";
}
