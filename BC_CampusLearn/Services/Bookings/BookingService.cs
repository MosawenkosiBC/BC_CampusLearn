using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Bookings;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BookingService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<BookingPreviewViewModel?>
        GetBookingPreviewAsync(
            int tutorAvailabilityId,
            CancellationToken cancellationToken = default)
    {
        return await _context.TutorAvailabilities
            .AsNoTracking()
            .Where(slot =>
                slot.TutorAvailabilityId ==
                    tutorAvailabilityId &&
                slot.IsActive &&
                !slot.IsBooked &&
                slot.StartTime >
                    DateTimeOffset.UtcNow)
            .Select(slot =>
                new BookingPreviewViewModel
                {
                    TutorAvailabilityId =
                        slot.TutorAvailabilityId,

                    TutorName =
                        slot.Tutor.DisplayName,

                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime
                })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BookingCreationResult>
        CreateBookingAsync(
            CreateBookingInput input,
            CancellationToken cancellationToken = default)
    {
        CurrentUser student =
            _currentUserService.GetRequiredUser();

        TutorAvailability? slot =
            await _context.TutorAvailabilities
                .Include(item => item.Tutor)
                .FirstOrDefaultAsync(
                    item =>
                        item.TutorAvailabilityId ==
                        input.TutorAvailabilityId,
                    cancellationToken);

        if (slot is null)
        {
            return BookingCreationResult.Failure(
                "The selected availability slot does not exist.");
        }

        if (!slot.IsActive ||
            slot.IsBooked ||
            slot.StartTime <= DateTimeOffset.UtcNow)
        {
            return BookingCreationResult.Failure(
                "This availability slot is no longer available.");
        }

        slot.IsBooked = true;

        var booking = new Booking
        {
            TutorId = slot.TutorId,

            TutorAvailabilityId =
                slot.TutorAvailabilityId,

            StudentObjectId = student.ObjectId,
            StudentTenantId = student.TenantId,
            StudentName = student.DisplayName,
            StudentEmail = student.Email,

            SessionStart = slot.StartTime,
            SessionEnd = slot.EndTime,

            Reason = input.Reason,

            Status = BookingStatus.Pending,

            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Bookings.Add(booking);

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);

            return BookingCreationResult.Success(
                booking.BookingId);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BookingCreationResult.Failure(
                "Another student booked this slot first. " +
                "Please select another time.");
        }
        catch (DbUpdateException)
        {
            return BookingCreationResult.Failure(
                "The booking could not be saved. " +
                "The slot may already have been booked.");
        }
    }
}