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
    IReadOnlyList<string> Transcript);

public sealed record GeminiSessionAssessment(
    string Validity,
    string TopicCoverage,
    string Summary,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Concerns,
    string Recommendation);

public sealed class GeminiAssessmentException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
