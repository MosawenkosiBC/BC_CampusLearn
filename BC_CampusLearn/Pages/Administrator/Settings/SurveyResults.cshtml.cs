using System.Globalization;
using System.Text.Json;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

[Authorize(Roles = nameof(BcUserRole.Admin) + "," + nameof(BcUserRole.SuperAdmin))]
public class SurveyResultsModel(ApplicationDbContext context) : PageModel
{
    private const int PageSize = 20;
    private const int SummaryBatchSize = 10;

    public Guid SurveyId { get; private set; }
    public SurveyAudience Audience { get; private set; }
    public string SurveyTitle { get; private set; } = string.Empty;
    public int ResponseCount { get; private set; }
    public int DurationDays { get; private set; }
    public string SurveyStatus { get; private set; } = string.Empty;
    public IReadOnlyList<QuestionResult> Questions { get; private set; } = [];
    public QuestionDetails? Details { get; private set; }

    public async Task<IActionResult> OnGetPdfAsync(Guid surveyId, SurveyAudience audience,
        CancellationToken cancellationToken)
    {
        var result = await OnGetAsync(surveyId, audience, cancellationToken);
        if (result is not PageResult)
            return result;

        Response.Headers.CacheControl = "no-store";
        return File(SurveyResultsPdf.Create(this, cancellationToken), "application/pdf", $"survey-results-{SurveyId:N}.pdf");
    }

    public async Task<IActionResult> OnGetAsync(Guid surveyId, SurveyAudience audience, CancellationToken cancellationToken,
        Guid? questionId = null, SurveyQuestionType? questionType = null, string? legacyPrompt = null, int detailPage = 1)
    {
        if (!ModelState.IsValid || surveyId == Guid.Empty || !Enum.IsDefined(audience))
            return BadRequest();

        var survey = await context.FeedbackSurveys.AsNoTracking()
            .Where(survey => survey.FeedbackSurveyId == surveyId && survey.Audience == audience && survey.PublishedAt != null)
            .Select(survey => new { survey.Title, survey.PublishedAt, survey.ClosingDate, survey.IsDashboardPopupHidden, survey.QuestionsJson })
            .SingleOrDefaultAsync(cancellationToken);
        if (survey is null)
            return NotFound();

        SurveyId = surveyId;
        Audience = audience;
        SurveyTitle = survey.Title;
        var campusOffset = TimeSpan.FromHours(2);
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(campusOffset).DateTime);
        var publishedDate = DateOnly.FromDateTime(survey.PublishedAt!.Value.ToOffset(campusOffset).DateTime);
        bool closed = survey.ClosingDate is DateOnly closingDate && closingDate < today;
        SurveyStatus = closed ? "Closed" : survey.IsDashboardPopupHidden ? "Paused" : "Active";
        DateOnly lastDay = closed ? survey.ClosingDate!.Value : today;
        DurationDays = Math.Max(0, lastDay.DayNumber - publishedDate.DayNumber);

        var responses = context.FeedbackSurveyResponses.AsNoTracking().Where(response => response.FeedbackSurveyId == surveyId);
        ResponseCount = await responses.CountAsync(cancellationToken);
        detailPage = Math.Clamp(detailPage, 1, Math.Max(1, (ResponseCount - 1) / PageSize + 1));
        int detailOffset = (detailPage - 1) * PageSize;
        QuestionKey? selectedKey = questionId is Guid id ? new QuestionKey(id, questionType, legacyPrompt) : null;
        var detailRows = new List<(int Number, int UserId, string Answer)>();
        int detailCount = 0;
        int responseNumber = 0;
        var questions = JsonSerializer.Deserialize<SurveyQuestion[]>(survey.QuestionsJson, JsonSerializerOptions.Web) ?? [];
        var summaries = questions.Where(question => question.Type != SurveyQuestionType.Section)
            .Select(question => new QuestionResult(new QuestionKey(question.Id, question.Type,
                question.Id == Guid.Empty ? question.Prompt : null), question.Prompt, question.Options, question.RatingLevels))
            .ToDictionary(question => question.Key);

        DateTimeOffset? after = null;
        int lastUserId = 0;
        while (true)
        {
            // Bound each query because submitted attachments are stored alongside the answers.
            var batchQuery = after is DateTimeOffset cutoff
                ? responses.Where(response => response.SubmittedAt > cutoff ||
                    (response.SubmittedAt == cutoff && response.BcUserId > lastUserId))
                : responses;
            var batch = await batchQuery.OrderBy(response => response.SubmittedAt)
                .ThenBy(response => response.BcUserId).Take(SummaryBatchSize)
                .Select(response => new { response.BcUserId, response.SubmittedAt, response.AnswersJson })
                .ToListAsync(cancellationToken);

            foreach (var response in batch)
            {
                responseNumber++;
                var answers = JsonSerializer.Deserialize<ResultAnswer[]>(response.AnswersJson, JsonSerializerOptions.Web) ?? [];
                foreach (var answer in answers)
                {
                    var key = new QuestionKey(answer.QuestionId, answer.Type, answer.QuestionId == Guid.Empty ? answer.Prompt : null);
                    if (!summaries.TryGetValue(key, out var summary))
                    {
                        summary = new QuestionResult(key, answer.Prompt, null, null);
                        summaries.Add(key, summary);
                    }

                    summary.AddAnswer(answer);
                    if (key == selectedKey)
                    {
                        detailCount++;
                        if (detailCount > detailOffset && detailRows.Count < PageSize)
                            detailRows.Add((responseNumber, response.BcUserId, answer.File?.Name ?? string.Join("\n", answer.Values)));
                    }
                }
            }

            if (batch.Count < SummaryBatchSize)
                break;

            after = batch[^1].SubmittedAt;
            lastUserId = batch[^1].BcUserId;
        }
        Questions = summaries.Values.ToList();

        if (selectedKey is QuestionKey detailKey)
        {
            if (!summaries.TryGetValue(detailKey, out var question))
                return NotFound();

            int pageCount = Math.Max(1, (detailCount - 1) / PageSize + 1);
            if (detailPage > pageCount)
                return RedirectToPage(new { surveyId, audience, questionId, questionType, legacyPrompt, detailPage = pageCount });

            int[] userIds = detailRows.Select(row => row.UserId).Distinct().ToArray();
            var names = await context.BcUsers.AsNoTracking().Where(user => userIds.Contains(user.BcUserId))
                .ToDictionaryAsync(user => user.BcUserId, user => user.DisplayName, cancellationToken);
            Details = new QuestionDetails(question, Questions.ToList().IndexOf(question) + 1, detailCount, detailPage, pageCount,
                detailRows.Select(row => new QuestionResponse(row.Number,
                    names.GetValueOrDefault(row.UserId) is { Length: > 0 } name ? name : "Unknown respondent", row.Answer)).ToList());
            Response.Headers.CacheControl = "no-store";
        }

        return Page();
    }

    public readonly record struct QuestionKey(Guid Id, SurveyQuestionType? Type, string? LegacyPrompt);
    public sealed record QuestionDetails(QuestionResult Question, int Number, int ResponseCount, int Page, int PageCount,
        IReadOnlyList<QuestionResponse> Rows);
    public sealed record QuestionResponse(int Number, string Name, string Answer);
    public sealed record ResultAnswer(Guid QuestionId, SurveyQuestionType? Type, string? Prompt, string[] Values, ResultFile? File);
    public sealed record ResultFile(string Name);

    public sealed class QuestionResult
    {
        private static readonly string[] ChartColors = ["#6376ed", "#e90089", "#269ca5", "#8d6ac8", "#298a38", "#d76c27", "#466689"];
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
        private readonly List<string> _latest = [];
        private long _ratingTotal;
        private int _ratingCount;

        public QuestionResult(QuestionKey key, string? prompt, string?[]? options, int? ratingLevels)
        {
            Key = key;
            Prompt = prompt;
            RatingLevels = ratingLevels ?? 5;
            if (Type == SurveyQuestionType.Choice)
            {
                foreach (string? option in options ?? [])
                {
                    if (option is not null)
                        _counts.TryAdd(option, 0);
                }
            }
            else if (IsRating)
            {
                for (int level = RatingLevels; level >= 1; level--)
                    _counts.Add(level.ToString(CultureInfo.InvariantCulture), 0);
            }
        }

        public QuestionKey Key { get; }
        public string? Prompt { get; }
        public SurveyQuestionType? Type => Key.Type;
        public int RatingLevels { get; private set; }
        public int ResponseCount { get; private set; }
        public bool IsRating => Type == SurveyQuestionType.Rating;
        public bool IsChoice => Type == SurveyQuestionType.Choice;
        public double? AverageRating => _ratingCount == 0 ? null : (double)_ratingTotal / _ratingCount;
        public IReadOnlyList<string> Latest => _latest;
        public IReadOnlyList<ChartRow> ChartRows => (IsRating
            ? _counts.OrderByDescending(item => int.Parse(item.Key, CultureInfo.InvariantCulture))
            : _counts.AsEnumerable()).Select((item, index) =>
                new ChartRow(item.Key, item.Value, ChartColors[index % ChartColors.Length])).ToList();

        public void AddAnswer(ResultAnswer answer)
        {
            ResponseCount++;
            if (IsChoice)
            {
                foreach (string value in answer.Values.Distinct(StringComparer.Ordinal))
                    _counts[value] = _counts.GetValueOrDefault(value) + 1;
            }
            else if (IsRating)
            {
                foreach (string value in answer.Values)
                {
                    if (int.TryParse(value, out int score) && score is >= 1 and <= 10)
                    {
                        while (RatingLevels < score)
                            _counts.TryAdd((++RatingLevels).ToString(CultureInfo.InvariantCulture), 0);

                        string level = score.ToString(CultureInfo.InvariantCulture);
                        _counts[level] = _counts.GetValueOrDefault(level) + 1;
                        _ratingTotal += score;
                        _ratingCount++;
                    }
                }
            }
            else
            {
                string display = answer.File?.Name ?? string.Join("\n", answer.Values);
                if (!string.IsNullOrWhiteSpace(display))
                {
                    if (_latest.Count == 3)
                        _latest.RemoveAt(_latest.Count - 1);
                    _latest.Insert(0, display);
                }
            }
        }
    }

    public sealed record ChartRow(string Label, int Count, string Color);
}
