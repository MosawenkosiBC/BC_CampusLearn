using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Hubs;

[Authorize]
public class SessionHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    public SessionHub(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
    }

    public async Task JoinSession(int bookingId)
    {
        CurrentUser user = _currentUserService.GetRequiredUser();
        await EnsureParticipantAsync(bookingId, user, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetGroupName(bookingId));
    }

    public async Task SendMessage(int bookingId, string? messageText)
    {
        CurrentUser user = _currentUserService.GetRequiredUser();
        BookingParticipant participant = await EnsureParticipantAsync(
            bookingId,
            user,
            Context.ConnectionAborted);
        string text = messageText?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > 2000)
        {
            throw new HubException(
                "Messages must contain between 1 and 2000 characters.");
        }

        if (participant.Status is BookingStatus.Cancelled or
            BookingStatus.Declined)
        {
            throw new HubException(
                "Messages are closed for this session.");
        }

        var message = new SessionMessage
        {
            BookingId = bookingId,
            SenderBcUserId = user.BcUserId,
            MessageText = text,
            SentAt = _timeProvider.GetUtcNow()
        };
        _context.SessionMessages.Add(message);
        await _context.SaveChangesAsync(Context.ConnectionAborted);

        await Clients.Group(GetGroupName(bookingId)).SendAsync(
            "ReceiveMessage",
            new
            {
                message.SessionMessageId,
                message.BookingId,
                SenderName = user.DisplayName,
                SenderBcUserId = user.BcUserId,
                message.MessageText,
                message.SentAt
            },
            Context.ConnectionAborted);
    }

    private async Task<BookingParticipant> EnsureParticipantAsync(
        int bookingId,
        CurrentUser user,
        CancellationToken cancellationToken)
    {
        BookingParticipant? participant = await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Select(booking => new BookingParticipant(
                booking.Status,
                booking.TutorCourseModule.Tutor.BcUserId,
                booking.StudentBcUserId,
                booking.StudentObjectId,
                booking.StudentTenantId))
            .SingleOrDefaultAsync(cancellationToken);

        bool isParticipant = participant is not null &&
            (participant.TutorBcUserId == user.BcUserId ||
             participant.StudentBcUserId == user.BcUserId ||
             (participant.StudentObjectId == user.ObjectId &&
              participant.StudentTenantId == user.TenantId));
        if (!isParticipant)
        {
            throw new HubException(
                "You do not have access to this session conversation.");
        }

        return participant!;
    }

    private static string GetGroupName(int bookingId) =>
        $"session-{bookingId}";

    private sealed record BookingParticipant(
        BookingStatus Status,
        int TutorBcUserId,
        int? StudentBcUserId,
        string StudentObjectId,
        string StudentTenantId);
}
