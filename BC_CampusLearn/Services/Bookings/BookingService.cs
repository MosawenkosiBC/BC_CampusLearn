using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Bookings;

public class BookingService : IBookingService
{
    private const long MaximumDocumentSize = 10 * 1024 * 1024;
    private static readonly TimeSpan ReservationDuration =
        TimeSpan.FromMinutes(25);

    private static readonly HashSet<string> AllowedDocumentExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx",
            ".png",
            ".jpg",
            ".jpeg"
        };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;

    public BookingService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _currentUserService = currentUserService;
        _environment = environment;
    }

    public async Task<BookingPreviewViewModel?>
        GetBookingPreviewAsync(
            int tutorAvailabilityId,
            CancellationToken cancellationToken = default)
    {
        BookingPreviewViewModel? preview =
            await _context.TutorAvailabilities
            .AsNoTracking()
            .Where(slot =>
                slot.TutorAvailabilityId ==
                    tutorAvailabilityId &&
                slot.AvailableTime >
                    DateTimeOffset.UtcNow)
            .Select(slot =>
                new BookingPreviewViewModel
                {
                    TutorAvailabilityId =
                        slot.TutorAvailabilityId,

                    TutorId = slot.TutorId,

                    TutorBcUserId = slot.Tutor.BcUserId,

                    TutorName = string.IsNullOrWhiteSpace(
                        slot.Tutor.BcUser.DisplayName)
                        ? slot.Tutor.BcUser.PersonnelNumber
                        : slot.Tutor.BcUser.DisplayName,

                    TutorEmail = slot.Tutor.BcUser.Email
                        ?? string.Empty,

                    Modules = slot.Tutor.TutorCourseModules.Where(a => a.IsActive)
                        .OrderBy(assignment =>
                            assignment.ProgrammeModule.ModuleCode)
                        .Select(assignment =>
                            new BookingModuleOptionViewModel
                            {
                                ProgrammeModuleId =
                                    assignment.ProgrammeModuleId,
                                ModuleCode =
                                    assignment.ProgrammeModule.ModuleCode,
                                ModuleName =
                                    assignment.ProgrammeModule.ModuleName
                            })
                        .ToList(),

                    AvailableTime = slot.AvailableTime
                })
            .FirstOrDefaultAsync(cancellationToken);

        return preview;
    }

    public async Task<BookingCreationResult>
        CreateBookingAsync(
            CreateBookingInput input,
            CancellationToken cancellationToken = default)
        => await _context.Database.CreateExecutionStrategy().ExecuteAsync(
            () => CreateBookingCoreAsync(input, cancellationToken));

    public async Task<BookingReservationResult> TryReserveSlotAsync(
        int tutorAvailabilityId,
        CancellationToken cancellationToken = default)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid token = Guid.NewGuid();
        DateTimeOffset reservationExpiresAt = now.Add(ReservationDuration);

        int updatedCount;
        if (_context.Database.IsRelational())
        {
            updatedCount = await _context.TutorAvailabilities
                .Where(slot =>
                    slot.TutorAvailabilityId == tutorAvailabilityId &&
                    slot.AvailableTime > now &&
                    (slot.ReservedByBcUserId == null ||
                     slot.ReservationExpiresAt <= now))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            slot => slot.ReservedByBcUserId,
                            student.BcUserId)
                        .SetProperty(
                            slot => slot.ReservationToken,
                            token)
                        .SetProperty(
                            slot => slot.ReservationExpiresAt,
                            reservationExpiresAt),
                    cancellationToken);
        }
        else
        {
            TutorAvailability? slot = await _context.TutorAvailabilities
                .SingleOrDefaultAsync(
                    item => item.TutorAvailabilityId == tutorAvailabilityId,
                    cancellationToken);

            bool canReserve = slot is not null &&
                slot.AvailableTime > now &&
                (slot.ReservedByBcUserId is null ||
                 slot.ReservationExpiresAt <= now);

            if (canReserve)
            {
                slot!.ReservedByBcUserId = student.BcUserId;
                slot.ReservationToken = token;
                slot.ReservationExpiresAt = reservationExpiresAt;
                await _context.SaveChangesAsync(cancellationToken);
                updatedCount = 1;
            }
            else
            {
                updatedCount = 0;
            }
        }

        if (updatedCount == 1)
        {
            return BookingReservationResult.Acquired(
                token,
                reservationExpiresAt);
        }

        var availability = await _context.TutorAvailabilities
            .AsNoTracking()
            .Where(slot => slot.TutorAvailabilityId == tutorAvailabilityId)
            .Select(slot => new
            {
                slot.AvailableTime,
                slot.ReservedByBcUserId,
                slot.ReservationToken,
                slot.ReservationExpiresAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (availability is null)
        {
            return BookingReservationResult.Failed(
                BookingReservationStatus.Unavailable);
        }

        if (availability.AvailableTime <= now)
        {
            return BookingReservationResult.Failed(
                BookingReservationStatus.Expired);
        }

        // A refresh or second tab from the same authenticated student keeps
        // the original token and deadline instead of granting more time.
        if (availability.ReservedByBcUserId == student.BcUserId &&
            availability.ReservationToken.HasValue &&
            availability.ReservationExpiresAt > now)
        {
            return BookingReservationResult.Acquired(
                availability.ReservationToken.Value,
                availability.ReservationExpiresAt.Value);
        }

        return BookingReservationResult.Failed(
            BookingReservationStatus.ReservedByAnotherStudent);
    }

    public async Task ReleaseSlotReservationAsync(
        int tutorAvailabilityId,
        Guid reservationToken,
        CancellationToken cancellationToken = default)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();

        if (_context.Database.IsRelational())
        {
            await _context.TutorAvailabilities
                .Where(slot =>
                    slot.TutorAvailabilityId == tutorAvailabilityId &&
                    slot.ReservedByBcUserId == student.BcUserId &&
                    slot.ReservationToken == reservationToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            slot => slot.ReservedByBcUserId,
                            (int?)null)
                        .SetProperty(
                            slot => slot.ReservationToken,
                            (Guid?)null)
                        .SetProperty(
                            slot => slot.ReservationExpiresAt,
                            (DateTimeOffset?)null),
                    cancellationToken);
            return;
        }

        TutorAvailability? availability =
            await _context.TutorAvailabilities.SingleOrDefaultAsync(
                slot =>
                    slot.TutorAvailabilityId == tutorAvailabilityId &&
                    slot.ReservedByBcUserId == student.BcUserId &&
                    slot.ReservationToken == reservationToken,
                cancellationToken);

        if (availability is null)
        {
            return;
        }

        availability.ReservedByBcUserId = null;
        availability.ReservationToken = null;
        availability.ReservationExpiresAt = null;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<BookingCreationResult> CreateBookingCoreAsync(
        CreateBookingInput input,
        CancellationToken cancellationToken)
    {
        CurrentUser student =
            _currentUserService.GetRequiredUser();

        if (await HasPendingStudentReviewAsync(
            student,
            cancellationToken))
        {
            return BookingCreationResult.Failure(
                "Complete your pending session review before booking another session.",
                pendingReviewRequired: true);
        }

        // Hold assignment reads until the booking is saved so an administrator cannot
        // remove the assignment between eligibility validation and booking creation.
        await using var assignmentTransaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        TutorAvailability? slot =
            await _context.TutorAvailabilities
                .Include(item => item.Tutor)
                    .ThenInclude(tutor => tutor.BcUser)
                .Include(item => item.Tutor)
                    .ThenInclude(tutor => tutor.TutorCourseModules)
                        .ThenInclude(assignment =>
                            assignment.ProgrammeModule)
                .FirstOrDefaultAsync(
                    item =>
                        item.TutorAvailabilityId ==
                        input.TutorAvailabilityId,
                    cancellationToken);

        if (slot is null)
        {
            return BookingCreationResult.Failure(
                "The selected availability slot does not exist.",
                failureReason: BookingFailureReason.Unavailable);
        }

        if (slot.Tutor.BcUserId == student.BcUserId)
        {
            return BookingCreationResult.Failure(
                "You cannot book a tutoring session with yourself.");
        }

        if (slot.AvailableTime <= DateTimeOffset.UtcNow)
        {
            return BookingCreationResult.Failure(
                "This availability slot is no longer available.",
                failureReason: BookingFailureReason.Expired);
        }

        DateTimeOffset reservationNow = DateTimeOffset.UtcNow;
        bool ownsActiveReservation =
            slot.ReservedByBcUserId == student.BcUserId &&
            slot.ReservationToken == input.ReservationToken &&
            slot.ReservationExpiresAt > reservationNow;

        if (!ownsActiveReservation)
        {
            bool ownReservationExpired =
                slot.ReservedByBcUserId == student.BcUserId &&
                slot.ReservationToken == input.ReservationToken &&
                slot.ReservationExpiresAt <= reservationNow;

            return BookingCreationResult.Failure(
                ownReservationExpired
                    ? "The time allowed to complete this booking has ended."
                    : "This session is currently reserved by another student.",
                failureReason: ownReservationExpired
                    ? BookingFailureReason.ReservationExpired
                    : BookingFailureReason.Reserved);
        }

        bool tutorCanTeachModule =
            slot.Tutor.TutorCourseModules.Any(assignment => assignment.IsActive &&
                assignment.ProgrammeModuleId ==
                    input.ProgrammeModuleId);

        if (!tutorCanTeachModule)
        {
            return BookingCreationResult.Failure(
                "Select a module assigned to this tutor.");
        }

        TutorCourseModule selectedAssignment =
            slot.Tutor.TutorCourseModules.Single(assignment =>
                assignment.IsActive &&
                assignment.ProgrammeModuleId == input.ProgrammeModuleId);

        StudyArea? selectedStudyArea = await _context.StudyAreas
            .AsNoTracking()
            .SingleOrDefaultAsync(
                studyArea =>
                    studyArea.StudyAreaId == input.StudyAreaId &&
                    studyArea.IsActive,
                cancellationToken);

        if (selectedStudyArea is null)
        {
            return BookingCreationResult.Failure(
                "Select an available location.");
        }

        List<string> preparationLinks = input.PreparationLinks
            .Where(link => !string.IsNullOrWhiteSpace(link))
            .Select(link => link!.Trim())
            .ToList();

        if (preparationLinks.Count > 3 ||
            preparationLinks.Any(link =>
                link.Length > 2048 ||
                !Uri.TryCreate(
                    link,
                    UriKind.Absolute,
                    out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps)))
        {
            return BookingCreationResult.Failure(
                "Add no more than three valid HTTP or HTTPS links.");
        }

        List<IFormFile> documents = input.Documents
            .Where(document => document is not null)
            .ToList();

        string? documentValidationError =
            ValidateDocuments(documents);

        if (documentValidationError is not null)
        {
            return BookingCreationResult.Failure(
                documentValidationError);
        }

        var booking = new Booking
        {
            TutorId = slot.TutorId,

            StudentBcUserId = student.BcUserId,

            ProgrammeModuleId = input.ProgrammeModuleId,

            StudentName = student.DisplayName,
            StudentEmail = student.Email,

            StudyAreaId = selectedStudyArea.StudyAreaId,

            Location = selectedStudyArea.Name,

            Summary = input.Summary?.Trim(),

            Status = BookingStatus.Pending,

            Duration = SessionDuration.OneHour,

            ScheduledStartTime = slot.AvailableTime,

            DateBooked = DateTimeOffset.UtcNow
        };

        for (int index = 0; index < preparationLinks.Count; index++)
        {
            booking.PreparationLinks.Add(
                new BookingPreparationLink
                {
                    Position = (byte)(index + 1),
                    Url = preparationLinks[index]
                });
        }

        string? documentDirectory = null;

        if (documents.Count > 0)
        {
            string directoryName =
                Guid.NewGuid().ToString("N");
            string relativeDirectory = Path.Combine(
                "App_Data",
                "booking-documents",
                directoryName);

            documentDirectory = Path.Combine(
                _environment.ContentRootPath,
                relativeDirectory);

            try
            {
                Directory.CreateDirectory(documentDirectory);

                for (int index = 0;
                    index < documents.Count;
                    index++)
                {
                    IFormFile document = documents[index];
                    string originalFileName =
                        Path.GetFileName(document.FileName);
                    string extension =
                        Path.GetExtension(originalFileName)
                            .ToLowerInvariant();
                    string storedFileName =
                        $"{Guid.NewGuid():N}{extension}";
                    string storedFilePath = Path.Combine(
                        documentDirectory,
                        storedFileName);

                    await using var stream = new FileStream(
                        storedFilePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true);

                    await document.CopyToAsync(
                        stream,
                        cancellationToken);

                    booking.Documents.Add(
                        new BookingDocument
                        {
                            Position = (byte)(index + 1),
                            OriginalFileName = originalFileName,
                            StoragePath = Path.Combine(
                                    relativeDirectory,
                                    storedFileName)
                                .Replace('\\', '/'),
                            ContentType =
                                GetSafeContentType(document.ContentType),
                            SizeBytes = document.Length,
                            UploadedAt = DateTimeOffset.UtcNow
                        });
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                DeleteDocumentDirectory(documentDirectory);
                throw;
            }
            catch (IOException)
            {
                DeleteDocumentDirectory(documentDirectory);

                return BookingCreationResult.Failure(
                    "The documents could not be stored. Please try again.");
            }
            catch (UnauthorizedAccessException)
            {
                DeleteDocumentDirectory(documentDirectory);

                return BookingCreationResult.Failure(
                    "The documents could not be stored. Please try again.");
            }
            catch
            {
                DeleteDocumentDirectory(documentDirectory);
                throw;
            }
        }

        _context.Bookings.Add(booking);
        _context.TutorAvailabilities.Remove(slot);

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);

            DateTimeOffset notificationTime = DateTimeOffset.UtcNow;
            string tutorName = string.IsNullOrWhiteSpace(
                slot.Tutor.BcUser.DisplayName)
                ? slot.Tutor.BcUser.PersonnelNumber
                : slot.Tutor.BcUser.DisplayName;
            var notificationDetails = new BookingNotificationDetails(
                selectedAssignment.ProgrammeModule.ModuleCode,
                selectedAssignment.ProgrammeModule.ModuleName,
                booking.Location,
                booking.ScheduledStartTime,
                tutorName,
                student.DisplayName,
                MeetingLinkUrl: null);
            _context.UserNotifications.AddRange(
                BookingNotificationFactory.BookingSubmitted(
                    student.BcUserId,
                    booking.BookingId,
                    notificationDetails,
                    notificationTime),
                BookingNotificationFactory.BookingReceived(
                    slot.Tutor.BcUserId,
                    booking.BookingId,
                    notificationDetails,
                    notificationTime));

            await _context.SaveChangesAsync(cancellationToken);

            if (assignmentTransaction is not null)
                await assignmentTransaction.CommitAsync(cancellationToken);

            return BookingCreationResult.Success(
                booking.BookingId);
        }
        catch (DbUpdateConcurrencyException)
        {
            DeleteDocumentDirectory(documentDirectory);

            return BookingCreationResult.Failure(
                "Another student booked this slot first. " +
                "Please select another time.",
                failureReason: BookingFailureReason.AlreadyBooked);
        }
        catch (DbUpdateException)
        {
            DeleteDocumentDirectory(documentDirectory);

            return BookingCreationResult.Failure(
                "The booking could not be saved. " +
                "The slot may already have been booked.",
                failureReason: BookingFailureReason.AlreadyBooked);
        }
        catch
        {
            DeleteDocumentDirectory(documentDirectory);
            throw;
        }
    }

    public Task<bool> HasPendingStudentReviewAsync(
        CancellationToken cancellationToken = default)
    {
        CurrentUser student =
            _currentUserService.GetRequiredUser();
        return HasPendingStudentReviewAsync(student, cancellationToken);
    }

    private Task<bool> HasPendingStudentReviewAsync(
        CurrentUser student,
        CancellationToken cancellationToken) =>
        _context.Bookings
            .AsNoTracking()
            .AnyAsync(booking =>
                booking.StudentBcUserId == student.BcUserId &&
                booking.Status == BookingStatus.Completed &&
                booking.StudentEvaluation == null,
                cancellationToken);

    private static string? ValidateDocuments(
        IReadOnlyCollection<IFormFile> documents)
    {
        if (documents.Count > 2)
        {
            return "Add no more than two documents.";
        }

        foreach (IFormFile document in documents)
        {
            string originalFileName =
                Path.GetFileName(document.FileName);
            string extension =
                Path.GetExtension(originalFileName);

            if (string.IsNullOrWhiteSpace(originalFileName) ||
                originalFileName.Length > 255 ||
                !AllowedDocumentExtensions.Contains(extension))
            {
                return "Documents must be PDF, Word, PNG, or JPG files.";
            }

            if (document.Length <= 0 ||
                document.Length > MaximumDocumentSize)
            {
                return "Each document must be larger than 0 bytes " +
                    "and no more than 10 MB.";
            }
        }

        return null;
    }

    private static string GetSafeContentType(string? contentType)
    {
        return string.IsNullOrWhiteSpace(contentType) ||
            contentType.Length > 100
            ? "application/octet-stream"
            : contentType;
    }

    private static void DeleteDocumentDirectory(
        string? documentDirectory)
    {
        if (string.IsNullOrWhiteSpace(documentDirectory) ||
            !Directory.Exists(documentDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(
                documentDirectory,
                recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup after a failed booking.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup after a failed booking.
        }
    }
}
