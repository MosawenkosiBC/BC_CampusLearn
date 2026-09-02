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

    public override async Task OnConnectedAsync()
    {
        CurrentUser user = _currentUserService.GetRequiredUser();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetUserGroupName(user.BcUserId));
        await base.OnConnectedAsync();
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

        int? recipientBcUserId = participant.TutorBcUserId == user.BcUserId
            ? participant.StudentBcUserId
            : participant.TutorBcUserId;
        if (!recipientBcUserId.HasValue)
        {
            throw new HubException(
                "The other session participant could not be found.");
        }

        var message = new SessionMessage
        {
            BookingId = bookingId,
            SenderBcUserId = user.BcUserId,
            RecipientBcUserId = recipientBcUserId,
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

        await Clients.Group(GetUserGroupName(recipientBcUserId.Value))
            .SendAsync(
                "ReceiveMessageNotification",
                new
                {
                    message.SessionMessageId,
                    message.BookingId,
                    SenderName = user.DisplayName,
                    message.MessageText,
                    message.SentAt,
                    OpenUrl = $"/Messages/Open/{message.SessionMessageId}"
                },
                Context.ConnectionAborted);
    }

    public async Task MarkMessagesRead(int bookingId)
    {
        CurrentUser user = _currentUserService.GetRequiredUser();
        await EnsureParticipantAsync(
            bookingId,
            user,
            Context.ConnectionAborted);

        DateTimeOffset readAt = _timeProvider.GetUtcNow();
        var unreadMessages = await _context.SessionMessages
            .AsNoTracking()
            .Where(message =>
                message.BookingId == bookingId &&
                message.RecipientBcUserId == user.BcUserId &&
                message.ReadAt == null)
            .Select(message => new
            {
                message.SessionMessageId,
                message.SenderBcUserId
            })
            .ToListAsync(Context.ConnectionAborted);
        if (unreadMessages.Count == 0)
        {
            return;
        }

        long[] messageIds = unreadMessages
            .Select(message => message.SessionMessageId)
            .ToArray();
        await _context.SessionMessages
            .Where(message =>
                messageIds.Contains(message.SessionMessageId))
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    message => message.ReadAt,
                    readAt),
                Context.ConnectionAborted);

        foreach (var senderMessages in unreadMessages.GroupBy(
            message => message.SenderBcUserId))
        {
            await Clients.Group(GetUserGroupName(senderMessages.Key))
                .SendAsync(
                    "MessagesRead",
                    new
                    {
                        BookingId = bookingId,
                        MessageIds = senderMessages
                            .Select(message => message.SessionMessageId)
                            .ToArray(),
                        ReadAt = readAt
                    },
                    Context.ConnectionAborted);
        }
    }

    public async Task SetTyping(int bookingId, bool isTyping)
    {
        CurrentUser user = _currentUserService.GetRequiredUser();
        BookingParticipant participant = await EnsureParticipantAsync(
            bookingId,
            user,
            Context.ConnectionAborted);
        if (participant.Status is BookingStatus.Cancelled or
            BookingStatus.Declined)
        {
            return;
        }

        await Clients.OthersInGroup(GetGroupName(bookingId)).SendAsync(
            "TypingChanged",
            new
            {
                BookingId = bookingId,
                UserBcUserId = user.BcUserId,
                IsTyping = isTyping
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

    private static string GetUserGroupName(int bcUserId) =>
        $"user-{bcUserId}";

    private sealed record BookingParticipant(
        BookingStatus Status,
        int TutorBcUserId,
        int? StudentBcUserId,
        string StudentObjectId,
        string StudentTenantId);
}
