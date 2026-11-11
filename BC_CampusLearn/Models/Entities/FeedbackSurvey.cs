using System.Text.Json.Serialization;

namespace BC_CampusLearn.Models.Entities;

public class FeedbackSurvey
{
    public Guid FeedbackSurveyId { get; set; }
    public SurveyAudience Audience { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public Guid Revision { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateOnly? ClosingDate { get; set; }
    public bool IsDashboardPopupHidden { get; set; }
}

public enum SurveyAudience
{
    Students,
    Tutors
}

[JsonConverter(typeof(JsonStringEnumConverter<SurveyQuestionType>))]
public enum SurveyQuestionType
{
    Choice,
    Text,
    Rating,
    Date,
    Ranking,
    Likert,
    FileUpload,
    NetPromoterScore,
    Section
}

[JsonConverter(typeof(JsonStringEnumConverter<SurveyRatingSymbol>))]
public enum SurveyRatingSymbol
{
    Star,
    Number
}

public sealed record SurveyQuestion(
    Guid Id,
    SurveyQuestionType? Type,
    string? Prompt,
    bool Required,
    string?[]? Options,
    string?[]? Statements,
    int? RatingLevels,
    bool MultipleAnswers,
    bool LongAnswer,
    string? Description,
    bool HasOtherOption = false,
    SurveyRatingSymbol? RatingSymbol = null,
    string? LowScoreLabel = null,
    string? HighScoreLabel = null);
