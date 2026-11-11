using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

[Authorize(Roles = nameof(BcUserRole.Admin) + "," + nameof(BcUserRole.SuperAdmin))]
public class SurveyModel(ApplicationDbContext context, TimeProvider timeProvider) : PageModel
{
    public const int MaxQuestions = 100;
    public const int MaxEntries = 20;

    [BindProperty(SupportsGet = true)]
    public SurveyAudience Audience { get; set; }

    [BindProperty]
    public SurveyInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }
    [TempData]
    public string? ListErrorMessage { get; set; }

    public IReadOnlyList<SurveyListItem> Surveys { get; private set; } = [];
    public bool IsEditing => Input.SurveyId != Guid.Empty;
    public bool IsPublished { get; private set; }
    public bool ShowMissingQuestionWarning { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid? surveyId, bool create, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(Audience))
            return BadRequest();

        if (surveyId is Guid id)
        {
            var survey = await context.FeedbackSurveys.AsNoTracking()
                .SingleOrDefaultAsync(survey => survey.FeedbackSurveyId == id &&
                    survey.Audience == Audience, cancellationToken);
            if (survey is null)
                return NotFound();

            IsPublished = survey.PublishedAt.HasValue;
            Input = new SurveyInput
            {
                SurveyId = survey.FeedbackSurveyId,
                Revision = survey.Revision,
                Title = survey.Title,
                ClosingDate = survey.ClosingDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                Description = survey.Description,
                QuestionsJson = survey.QuestionsJson
            };
        }
        else if (create)
        {
            Input.SurveyId = Guid.NewGuid();
        }
        else
        {
            Surveys = await context.FeedbackSurveys.AsNoTracking()
                .Where(survey => survey.Audience == Audience)
                .OrderBy(survey => survey.Title)
                .ThenBy(survey => survey.FeedbackSurveyId)
                .Select(survey => new SurveyListItem(
                    survey.FeedbackSurveyId, survey.Title, survey.Description,
                    survey.PublishedAt, survey.ClosingDate, survey.Revision, !survey.IsDashboardPopupHidden))
                .ToListAsync(cancellationToken);
        }

        return Page();
    }

    public Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken) =>
        SaveAsync(publish: false, cancellationToken);

    public Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken) =>
        SaveAsync(publish: true, cancellationToken);

    public async Task<IActionResult> OnPostDashboardPopupAsync(
        Guid surveyId, Guid revision, bool? showPopup, CancellationToken cancellationToken)
    {
        if (surveyId == Guid.Empty || revision == Guid.Empty || showPopup is null)
            return BadRequest();

        var survey = await context.FeedbackSurveys.SingleOrDefaultAsync(survey =>
            survey.FeedbackSurveyId == surveyId && survey.Audience == SurveyAudience.Tutors &&
            survey.PublishedAt != null, cancellationToken);
        if (survey is null)
            return NotFound();

        bool hidePopup = !showPopup.Value;
        if (survey.IsDashboardPopupHidden == hidePopup)
            return RedirectToPage(new { audience = SurveyAudience.Tutors });

        if (survey.Revision != revision)
            return ListConflict(SurveyAudience.Tutors);

        survey.IsDashboardPopupHidden = hidePopup;
        survey.Revision = Guid.NewGuid();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ListConflict(SurveyAudience.Tutors);
        }

        SuccessMessage = hidePopup ? "Tutor dashboard popup turned off." : "Tutor dashboard popup turned on.";
        return RedirectToPage(new { audience = SurveyAudience.Tutors });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        Guid surveyId, Guid revision, CancellationToken cancellationToken)
    {
        if (surveyId == Guid.Empty || revision == Guid.Empty || !Enum.IsDefined(Audience) ||
            ModelState[nameof(Audience)]?.Errors.Count > 0)
            return BadRequest();

        var survey = await context.FeedbackSurveys.SingleOrDefaultAsync(survey =>
            survey.FeedbackSurveyId == surveyId && survey.Audience == Audience, cancellationToken);
        if (survey is null)
            return NotFound();

        if (survey.Revision != revision)
            return ListConflict(Audience);

        context.FeedbackSurveys.Remove(survey);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ListConflict(Audience);
        }

        SuccessMessage = "Survey, questions, and any responses deleted.";
        return RedirectToPage(new { audience = Audience });
    }

    private IActionResult ListConflict(SurveyAudience audience)
    {
        ListErrorMessage = "This survey changed. Review the current survey and try again.";
        return RedirectToPage(new { audience });
    }

    private async Task<IActionResult> SaveAsync(bool publish, CancellationToken cancellationToken)
    {
        if (!IsEditing || !Enum.IsDefined(Audience) || ModelState[nameof(Audience)]?.Errors.Count > 0)
            return BadRequest();

        bool isNew = Input.Revision is null;
        var survey = await context.FeedbackSurveys.FindAsync([Input.SurveyId], cancellationToken);
        if ((survey is null && !isNew) || (survey is not null && survey.Audience != Audience))
            return NotFound();

        IsPublished = survey?.PublishedAt is not null;
        if (survey?.Revision != Input.Revision)
            return SaveConflict();

        var questions = ValidateQuestions();
        DateOnly? closingDate = null;
        if (!string.IsNullOrWhiteSpace(Input.ClosingDate))
        {
            if (DateOnly.TryParseExact(Input.ClosingDate.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly parsedDate))
                closingDate = parsedDate;
            else
                ModelState.AddModelError("Input.ClosingDate", "Enter a valid closing date in dd/mm/yyyy format.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (publish && closingDate.HasValue &&
            closingDate < DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(2)).DateTime))
        {
            ModelState.AddModelError("Input.ClosingDate", "Choose today or a future closing date to publish this survey.");
        }

        if (!ModelState.IsValid)
            return Page();

        if ((publish || IsPublished) &&
            !questions.Any(question => question.Type != SurveyQuestionType.Section))
        {
            ShowMissingQuestionWarning = true;
            return Page();
        }

        if (survey is null)
        {
            survey = new FeedbackSurvey { FeedbackSurveyId = Input.SurveyId, Audience = Audience };
            context.FeedbackSurveys.Add(survey);
        }

        survey.Title = Input.Title.Trim();
        survey.ClosingDate = closingDate;
        survey.Description = Input.Description?.Trim();
        survey.QuestionsJson = JsonSerializer.Serialize(questions, JsonSerializerOptions.Web);
        survey.Revision = Guid.NewGuid();
        if (publish)
            survey.PublishedAt ??= now;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return SaveConflict();
        }
        catch (DbUpdateException ex) when (isNew && ex.InnerException is SqlException { Number: 2627 })
        {
            return SaveConflict();
        }

        SuccessMessage = publish && !IsPublished ? "Survey published."
            : IsPublished ? "Survey changes saved." : "Survey draft saved.";
        return IsPublished && !publish
            ? RedirectToPage(new { audience = Audience, surveyId = survey.FeedbackSurveyId })
            : RedirectToPage(new { audience = Audience });
    }

    private IActionResult SaveConflict()
    {
        ModelState.AddModelError(string.Empty,
            "A saved version of this survey is newer than this editor. Open it from the survey list to continue editing.");
        return Page();
    }

    private List<SurveyQuestion> ValidateQuestions()
    {
        SurveyQuestion?[]? questions;
        try
        {
            questions = string.IsNullOrWhiteSpace(Input.QuestionsJson) ? null
                : JsonSerializer.Deserialize<SurveyQuestion?[]>(Input.QuestionsJson, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            questions = null;
        }

        if (questions is null)
        {
            ModelState.AddModelError(string.Empty, "The survey questions could not be read.");
            return [];
        }

        if (questions.Length > MaxQuestions)
        {
            ModelState.AddModelError(string.Empty, $"A survey can contain up to {MaxQuestions} questions or sections.");
            return [];
        }

        var validated = new List<SurveyQuestion>(questions.Length);
        var ids = new HashSet<Guid>();
        for (int index = 0; index < questions.Length; index++)
        {
            var question = questions[index];
            string label = $"Item {index + 1}";
            if (question?.Type is not SurveyQuestionType type || !Enum.IsDefined(type))
            {
                ModelState.AddModelError(string.Empty, $"{label} has an invalid question type.");
                continue;
            }

            string? prompt = question.Prompt?.Trim();
            if (string.IsNullOrEmpty(prompt) || prompt.Length > 500)
                ModelState.AddModelError(string.Empty, $"{label} needs a title of 1 to 500 characters.");

            Guid id = question.Id == Guid.Empty ? Guid.NewGuid() : question.Id;
            if (!ids.Add(id))
                ModelState.AddModelError(string.Empty, $"{label} has a duplicate question ID.");

            bool isSection = type == SurveyQuestionType.Section;
            string? description = isSection ? question.Description?.Trim() : null;
            if (description?.Length > 2000)
                ModelState.AddModelError(string.Empty, $"{label} has a section description over 2,000 characters.");

            int? ratingLevels = type == SurveyQuestionType.Rating ? question.RatingLevels : null;
            if (type == SurveyQuestionType.Rating && ratingLevels is not (>= 2 and <= 10))
                ModelState.AddModelError(string.Empty, $"{label} needs between 2 and 10 rating levels.");

            SurveyRatingSymbol? ratingSymbol = type == SurveyQuestionType.Rating
                ? question.RatingSymbol ?? SurveyRatingSymbol.Star : null;
            if (ratingSymbol is SurveyRatingSymbol symbol && !Enum.IsDefined(symbol))
                ModelState.AddModelError(string.Empty, $"{label} has an invalid rating symbol.");

            bool isNps = type == SurveyQuestionType.NetPromoterScore;
            string? lowScoreLabel = isNps ? question.LowScoreLabel?.Trim() : null;
            string? highScoreLabel = isNps ? question.HighScoreLabel?.Trim() : null;
            if (lowScoreLabel?.Length > 160 || highScoreLabel?.Length > 160)
                ModelState.AddModelError(string.Empty, $"{label} score labels must be at most 160 characters.");

            var options = type is SurveyQuestionType.Choice or SurveyQuestionType.Ranking or SurveyQuestionType.Likert
                ? ValidateEntries(question.Options, 2, $"{label} options") : null;
            var statements = type == SurveyQuestionType.Likert
                ? ValidateEntries(question.Statements, 1, $"{label} statements") : null;

            validated.Add(new SurveyQuestion(id, type, prompt, !isSection && question.Required,
                options, statements, ratingLevels, type == SurveyQuestionType.Choice && question.MultipleAnswers,
                type == SurveyQuestionType.Text && question.LongAnswer, description,
                type == SurveyQuestionType.Choice && question.HasOtherOption,
                ratingSymbol, lowScoreLabel, highScoreLabel));
        }

        return validated;
    }

    private string[] ValidateEntries(string?[]? entries, int minimum, string label)
    {
        string[] normalized = entries?.Select(entry => entry?.Trim() ?? string.Empty).ToArray() ?? [];
        if (normalized.Length < minimum || normalized.Length > MaxEntries ||
            normalized.Any(entry => entry.Length is 0 or > 160) ||
            normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            ModelState.AddModelError(string.Empty,
                $"{label} must contain {minimum} to {MaxEntries} different entries, each up to 160 characters.");
        }

        return normalized;
    }

    public sealed class SurveyInput
    {
        public Guid SurveyId { get; set; }
        public Guid? Revision { get; set; }

        [Required, StringLength(160)]
        public string Title { get; set; } = string.Empty;

        public string? ClosingDate { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        public string QuestionsJson { get; set; } = "[]";
    }

    public sealed record SurveyListItem(Guid Id, string Title, string? Description, DateTimeOffset? PublishedAt,
        DateOnly? ClosingDate, Guid Revision, bool ShowDashboardPopup);
}
