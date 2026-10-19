namespace BC_CampusLearn.Services.Gemini;

public interface IGeminiSessionAssessmentService
{
    Task<GeminiSessionAssessment> AssessAsync(
        string apiKey,
        GeminiSessionEvidence evidence,
        CancellationToken cancellationToken);
}

public sealed record GeminiSessionEvidence(
    string BookingSummary,
    string Module,
    IReadOnlyDictionary<string, string> StudentReview,
    IReadOnlyDictionary<string, string> TutorReview,
    GeminiTranscriptDocument? UploadedTranscript,
    string? TeamsAttendanceEvidence = null);

public sealed record GeminiTranscriptDocument(
    string FileName,
    string ContentType,
    string? ExtractedText,
    string? Base64Data);

public sealed record GeminiSessionAssessment(
    string Validity,
    string TopicCoverage,
    string Summary,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Concerns,
    string Recommendation,
    string TranscriptDuration = "Could not determine from transcript",
    IReadOnlyList<GeminiWatchRange>? SuggestedWatchRanges = null);

public sealed record GeminiWatchRange(int StartSeconds, int EndSeconds, string Reason)
{
    public string DisplayRange => $"{Format(StartSeconds)} – {Format(EndSeconds)}";
    private static string Format(int seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss");
}

public sealed class GeminiAssessmentException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
