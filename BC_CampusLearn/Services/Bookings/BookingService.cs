using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Bookings;

public class BookingService : IBookingService
{
    private const long MaximumDocumentSize = 10 * 1024 * 1024;

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

                    Modules = slot.Tutor.TutorCourseModules
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

        if (preview is not null)
        {
            preview.TutorName =
                TutorDisplayNames.GetName(preview.TutorId);
        }

        return preview;
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
                    .ThenInclude(tutor => tutor.BcUser)
                .Include(item => item.Tutor)
                    .ThenInclude(tutor => tutor.TutorCourseModules)
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

        if (slot.AvailableTime <= DateTimeOffset.UtcNow)
        {
            return BookingCreationResult.Failure(
                "This availability slot is no longer available.");
        }

        bool tutorCanTeachModule =
            slot.Tutor.TutorCourseModules.Any(assignment =>
                assignment.ProgrammeModuleId ==
                    input.ProgrammeModuleId);

        if (!tutorCanTeachModule)
        {
            return BookingCreationResult.Failure(
                "Select a module assigned to this tutor.");
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

            ProgrammeModuleId = input.ProgrammeModuleId,

            StudentObjectId = student.ObjectId,
            StudentTenantId = student.TenantId,
            StudentName = student.DisplayName,
            StudentEmail = student.Email,

            Location = input.Location.Trim(),

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

            return BookingCreationResult.Success(
                booking.BookingId);
        }
        catch (DbUpdateConcurrencyException)
        {
            DeleteDocumentDirectory(documentDirectory);

            return BookingCreationResult.Failure(
                "Another student booked this slot first. " +
                "Please select another time.");
        }
        catch (DbUpdateException)
        {
            DeleteDocumentDirectory(documentDirectory);

            return BookingCreationResult.Failure(
                "The booking could not be saved. " +
                "The slot may already have been booked.");
        }
        catch
        {
            DeleteDocumentDirectory(documentDirectory);
            throw;
        }
    }

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
