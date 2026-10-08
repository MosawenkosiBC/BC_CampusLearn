using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Tutors;

public class SeniorTutorApplicationDetailsModel(ApplicationDbContext context)
    : PageModel
{
    [BindProperty, StringLength(500)]
    public string? ReviewNote { get; set; }

    [TempData]
    public string? ReviewMessage { get; set; }

    [TempData]
    public string? ReviewError { get; set; }

    public SeniorTutorApplication Application { get; private set; } = null!;
    public IReadOnlyList<SessionStatusStatistic> SessionStatuses
    { get; private set; } = [];
    public IReadOnlyList<ModuleStatistic> TopModules { get; private set; } = [];
    public int TotalSessions { get; private set; }
    public int CompletedSessions { get; private set; }
    public int UniqueTutees { get; private set; }
    public decimal TotalTutoringHours { get; private set; }
    public decimal CompletionRate { get; private set; }
    public decimal? AverageRating { get; private set; }
    public int RatingCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await LoadAsync(id, cancellationToken)
            ? Page()
            : NotFound();
    }

    public async Task<IActionResult> OnPostReviewAsync(
        int id,
        bool approve,
        CancellationToken cancellationToken)
    {
        ReviewNote = ReviewNote?.Trim();
        if (ReviewNote?.Length > 500 ||
            (!approve && string.IsNullOrWhiteSpace(ReviewNote)))
        {
            ModelState.AddModelError(
                nameof(ReviewNote),
                "Provide a reason when rejecting, using no more than 500 characters.");
            return await LoadAsync(id, cancellationToken)
                ? Page()
                : NotFound();
        }

        SeniorTutorApplication? application = await context
            .SeniorTutorApplications
            .Include(item => item.Tutor)
            .SingleOrDefaultAsync(
                item => item.SeniorTutorApplicationId == id,
                cancellationToken);

        if (application is null)
        {
            return NotFound();
        }

        if (application.Status != TutorAccountRequestStatus.Pending)
        {
            ReviewError = "This application has already been reviewed.";
            return RedirectToPage(new { id });
        }

        TutorAccountRequestStatus decision = approve
            ? TutorAccountRequestStatus.Approved
            : TutorAccountRequestStatus.Declined;
        application.Status = decision;
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedBy = User.Identity?.Name ?? "Administrator";
        application.ReviewNote = ReviewNote;

        context.UserNotifications.Add(new UserNotification
        {
            RecipientBcUserId = application.Tutor.BcUserId,
            Title = "Senior tutor application update",
            Message = approve
                ? "Your senior tutor application was accepted."
                : $"Your senior tutor application was rejected. {ReviewNote}",
            LinkUrl = "/Tutors/SeniorTutorApplications",
            CreatedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            ReviewError = "This application changed while it was being reviewed. Review the latest status and try again.";
            return RedirectToPage(new { id });
        }

        ReviewMessage = approve
            ? "Senior tutor application accepted. The tutor has been notified."
            : "Senior tutor application rejected. The tutor has been notified.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(
        int id,
        CancellationToken cancellationToken)
    {
        SeniorTutorApplication? application = await context
            .SeniorTutorApplications
            .AsNoTracking()
            .Include(item => item.Tutor)
                .ThenInclude(tutor => tutor.BcUser)
            .Include(item => item.Tutor)
                .ThenInclude(tutor => tutor.Programme)
            .Include(item => item.Tutor)
                .ThenInclude(tutor => tutor.TutorCourseModules.Where(module => module.IsActive))
                .ThenInclude(module => module.ProgrammeModule)
            .SingleOrDefaultAsync(
                item => item.SeniorTutorApplicationId == id,
                cancellationToken);

        if (application is null)
        {
            return false;
        }

        Application = application;
        List<BookingStatisticRow> bookings = await context.Bookings
            .AsNoTracking()
            .Where(booking => booking.TutorId == application.TutorId)
            .Select(booking => new BookingStatisticRow
            {
                Status = booking.Status,
                DurationHours = (int)booking.Duration,
                StudentBcUserId = booking.StudentBcUserId,
                StudentEmail = booking.StudentEmail,
                StudentName = booking.StudentName,
                StudentRating = booking.StudentEvaluation == null
                    ? null
                    : booking.StudentEvaluation.ModeRating,
                ModuleCode = booking.ProgrammeModule.ModuleCode,
                ModuleName = booking.ProgrammeModule.ModuleName
            })
            .ToListAsync(cancellationToken);

        TotalSessions = bookings.Count;
        List<BookingStatisticRow> completed = bookings
            .Where(booking => booking.Status == BookingStatus.Completed)
            .ToList();
        CompletedSessions = completed.Count;
        TotalTutoringHours = completed.Sum(booking => booking.DurationHours);
        UniqueTutees = completed
            .Select(TuteeKey)
            .Where(key => key is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        int concludedSessions = completed.Count + bookings.Count(booking =>
            booking.Status is BookingStatus.Cancelled or BookingStatus.Declined);
        CompletionRate = concludedSessions == 0
            ? 0
            : Math.Round(completed.Count * 100m / concludedSessions, 1);

        List<byte> ratings = completed
            .Where(booking => booking.StudentRating.HasValue)
            .Select(booking => booking.StudentRating!.Value)
            .ToList();
        RatingCount = ratings.Count;
        AverageRating = ratings.Count == 0
            ? null
            : Math.Round(ratings.Average(value => (decimal)value), 1);

        BookingStatus[] statusOrder =
        [
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            BookingStatus.InProgress,
            BookingStatus.Completed,
            BookingStatus.Cancelled,
            BookingStatus.Declined
        ];
        SessionStatuses = statusOrder
            .Select(status => new SessionStatusStatistic(
                status,
                bookings.Count(booking => booking.Status == status)))
            .ToList();
        TopModules = completed
            .GroupBy(booking => new { booking.ModuleCode, booking.ModuleName })
            .Select(group => new ModuleStatistic(
                group.Key.ModuleCode,
                group.Key.ModuleName,
                group.Count()))
            .OrderByDescending(module => module.SessionCount)
            .ThenBy(module => module.Code)
            .Take(5)
            .ToList();

        return true;
    }

    private static string? TuteeKey(BookingStatisticRow booking)
    {
        if (booking.StudentBcUserId.HasValue)
        {
            return $"id:{booking.StudentBcUserId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(booking.StudentEmail))
        {
            return $"email:{booking.StudentEmail.Trim()}";
        }

        return string.IsNullOrWhiteSpace(booking.StudentName)
            ? null
            : $"name:{booking.StudentName.Trim()}";
    }

    public sealed record SessionStatusStatistic(BookingStatus Status, int Count);
    public sealed record ModuleStatistic(string Code, string Name, int SessionCount);

    private sealed class BookingStatisticRow
    {
        public BookingStatus Status { get; set; }
        public int DurationHours { get; set; }
        public int? StudentBcUserId { get; set; }
        public string? StudentEmail { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public byte? StudentRating { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
    }
}
