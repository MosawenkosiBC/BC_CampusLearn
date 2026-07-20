namespace BC_CampusLearn.Models.Entities;

public class BookingDocument
{
    public int BookingDocumentId { get; set; }

    public int BookingId { get; set; }

    public byte Position { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
