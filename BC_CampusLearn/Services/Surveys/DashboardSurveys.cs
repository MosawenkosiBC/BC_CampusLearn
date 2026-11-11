using System.Globalization;
using System.Text.Json;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Services.Surveys;

public sealed class DashboardSurveys(ApplicationDbContext context, SurveyAudience audience)
{
    private const int MaxSurveyFileSize = 5 * 1024 * 1024;
    private const int MaxSurveyFilesTotalSize = 10 * 1024 * 1024;
    public const string ChangedMessage = "This survey changed or is no longer available. Check the dashboard for the latest survey.";
    public const string SubmittedMessage = "Thank you. Your survey response was submitted.";
    private IFormCollection? _surveyAnswers;
    private Guid? _invalidSurveyRevision;

    public IReadOnlyList<SurveyForm> Forms { get; private set; } = [];
    public SurveyAudience Audience => audience;
    public Guid? InvalidSurveyId { get; private set; }
    public string? ValidationError { get; private set; }
    public string? AvailabilityError { get; private set; }

    private IQueryable<FeedbackSurvey> AvailableSurveys()
    {
        DateOnly today = SouthAfricanDate(DateTimeOffset.UtcNow);
        return context.FeedbackSurveys.Where(survey => survey.Audience == audience &&
            survey.PublishedAt != null && !survey.IsDashboardPopupHidden &&
            (survey.ClosingDate == null || survey.ClosingDate >= today));
    }

    private static DateOnly SouthAfricanDate(DateTimeOffset timestamp) =>
        DateOnly.FromDateTime(timestamp.ToOffset(TimeSpan.FromHours(2)).DateTime);

    public async Task LoadAsync(int userId, CancellationToken cancellationToken)
    {
        var surveys = await AvailableSurveys().AsNoTracking()
            .Where(survey => !context.FeedbackSurveyResponses.Any(response =>
                response.FeedbackSurveyId == survey.FeedbackSurveyId && response.BcUserId == userId))
            .OrderByDescending(survey => survey.PublishedAt)
            .ThenBy(survey => survey.FeedbackSurveyId)
            .Select(survey => new { survey.FeedbackSurveyId, survey.Revision, survey.Title, survey.Description, survey.QuestionsJson })
            .ToListAsync(cancellationToken);

        if (InvalidSurveyId is Guid invalidId && !surveys.Any(survey =>
            survey.FeedbackSurveyId == invalidId && survey.Revision == _invalidSurveyRevision))
        {
            InvalidSurveyId = null;
            ValidationError = null;
            AvailabilityError = ChangedMessage;
        }

        Forms = surveys.Select(survey => new SurveyForm(survey.FeedbackSurveyId, survey.Revision,
            survey.Title, survey.Description, ReadQuestions(survey.QuestionsJson),
            survey.FeedbackSurveyId == InvalidSurveyId ? _surveyAnswers : null)).ToList();
    }

    public async Task<SubmissionResult> SubmitAsync(int userId, Guid surveyId, Guid revision,
        IFormCollection form, CancellationToken cancellationToken)
    {
        var survey = await AvailableSurveys().SingleOrDefaultAsync(survey =>
            survey.FeedbackSurveyId == surveyId, cancellationToken);
        if (survey is null)
            return SubmissionResult.Unavailable;

        if (await context.FeedbackSurveyResponses.FindAsync([surveyId, userId], cancellationToken) is not null)
            return SubmissionResult.Submitted;

        if (survey.Revision != revision)
            return SubmissionResult.Unavailable;

        var answers = new List<SurveyAnswer>();
        long uploadSize = 0;
        foreach (var question in ReadQuestions(survey.QuestionsJson))
        {
            if (question.Type == SurveyQuestionType.Section)
                continue;

            string name = $"answer-{question.Id:N}";
            if (question.Type == SurveyQuestionType.FileUpload)
            {
                var files = form.Files.GetFiles(name);
                if (files.Count == 0 && !question.Required)
                    continue;

                if (files.Count != 1 || files[0].Length is <= 0 or > MaxSurveyFileSize ||
                    uploadSize + files[0].Length > MaxSurveyFilesTotalSize)
                {
                    return Invalid(survey, form,
                        $"Choose one file for “{question.Prompt}”. The limit is 5 MB per file and 10 MB per survey.");
                }

                var file = files[0];
                string fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
                    return Invalid(survey, form, $"Choose a file with a valid name for “{question.Prompt}”.");

                uploadSize += file.Length;
                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, cancellationToken);
                answers.Add(new SurveyAnswer(question.Id, question.Prompt, question.Type, [], new SurveyFile(fileName, buffer.ToArray())));
                continue;
            }

            string[]? values = ReadAnswer(question, form, name);
            if (values is null || (question.Required && values.Length == 0))
            {
                return Invalid(survey, form, $"Enter a valid answer for “{question.Prompt}”.");
            }

            if (values.Length > 0)
                answers.Add(new SurveyAnswer(question.Id, question.Prompt, question.Type, values));
        }

        DateTimeOffset submittedAt = DateTimeOffset.UtcNow;
        if (survey.ClosingDate is DateOnly closingDate && closingDate < SouthAfricanDate(submittedAt))
            return SubmissionResult.Unavailable;

        context.FeedbackSurveyResponses.Add(new FeedbackSurveyResponse
        {
            FeedbackSurveyId = surveyId,
            BcUserId = userId,
            SurveyRevision = revision,
            SubmittedAt = submittedAt,
            AnswersJson = JsonSerializer.Serialize(answers, JsonSerializerOptions.Web)
        });

        // Check the survey revision in the same transaction that inserts the response.
        context.Entry(survey).Property(item => item.Revision).IsModified = true;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return SubmissionResult.Unavailable;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return SubmissionResult.Submitted;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            return SubmissionResult.Unavailable;
        }

        return SubmissionResult.Submitted;
    }

    private SubmissionResult Invalid(FeedbackSurvey survey, IFormCollection form, string error)
    {
        InvalidSurveyId = survey.FeedbackSurveyId;
        _invalidSurveyRevision = survey.Revision;
        _surveyAnswers = form;
        ValidationError = error;
        return SubmissionResult.Invalid;
    }

    private static SurveyQuestion[] ReadQuestions(string json) =>
        JsonSerializer.Deserialize<SurveyQuestion[]>(json, JsonSerializerOptions.Web) ?? [];

    private static string[]? ReadAnswer(SurveyQuestion question, IFormCollection form, string name)
    {
        var options = question.Options ?? [];
        if (question.Type is SurveyQuestionType.Ranking or SurveyQuestionType.Likert)
        {
            int count = question.Type == SurveyQuestionType.Ranking ? options.Length : question.Statements!.Length;
            var selections = Enumerable.Range(0, count).Select(index => ReadSingle(form, $"{name}-{index}")).ToArray();
            if (selections.All(value => value == string.Empty))
                return [];
            if (selections.Any(value => !TryIndex(value, options.Length, out _)))
                return null;

            int[] indexes = selections.Select(value => int.Parse(value!, CultureInfo.InvariantCulture)).ToArray();
            if (question.Type == SurveyQuestionType.Ranking)
                return indexes.Distinct().Count() == count
                    ? Enumerable.Range(0, count).OrderBy(index => indexes[index]).Select(index => options[index]!).ToArray()
                    : null;

            return indexes.Select((option, index) => $"{question.Statements![index]}: {options[option]}").ToArray();
        }

        if (question.Type == SurveyQuestionType.Choice)
        {
            var selections = form[name];
            if ((!question.MultipleAnswers && selections.Count > 1) || selections.Distinct().Count() != selections.Count)
                return null;

            var choices = new List<string>();
            foreach (var selection in selections)
            {
                if (TryIndex(selection, options.Length, out int index))
                    choices.Add(options[index]!);
                else if (question.HasOtherOption && selection == "other" && ReadSingle(form, $"{name}-other") is { Length: > 0 and <= 4000 } other)
                    choices.Add(other);
                else
                    return null;
            }
            return choices.ToArray();
        }

        string? answer = ReadSingle(form, name);
        if (answer == string.Empty)
            return [];
        bool valid = question.Type switch
        {
            SurveyQuestionType.Text => answer is { Length: <= 4000 },
            SurveyQuestionType.Date => DateOnly.TryParseExact(answer, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            SurveyQuestionType.Rating => int.TryParse(answer, out int score) && score >= 1 && score <= question.RatingLevels,
            SurveyQuestionType.NetPromoterScore => TryIndex(answer, 11, out _),
            _ => false
        };
        return valid ? [answer!] : null;
    }

    private static string? ReadSingle(IFormCollection form, string name) => form[name].Count switch
    {
        0 => string.Empty,
        1 => form[name][0]?.Trim() ?? string.Empty,
        _ => null
    };

    private static bool TryIndex(string? value, int count, out int index) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0 && index < count &&
        value == index.ToString(CultureInfo.InvariantCulture);

    public enum SubmissionResult { Submitted, Invalid, Unavailable }

    public sealed record SurveyForm(Guid Id, Guid Revision, string Title, string? Description,
        IReadOnlyList<SurveyQuestion> Questions, IFormCollection? PreviousAnswers);

    private sealed record SurveyAnswer(Guid QuestionId, string? Prompt, SurveyQuestionType? Type, string[] Values, SurveyFile? File = null);
    private sealed record SurveyFile(string Name, byte[] Content);
}
