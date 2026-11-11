namespace BC_CampusLearn.Models.Entities;

public class FeedbackSurveyResponse
{
    public Guid FeedbackSurveyId { get; set; }
    public int BcUserId { get; set; }
    public Guid SurveyRevision { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string AnswersJson { get; set; } = "[]";
}
