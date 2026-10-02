using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class StudyAreasModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    SettingsAuditService auditService,
    StudyAreaCampusLabelStore campusLabelStore) : PageModel
{
    public const int PageSize = 8;

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<AvailabilityFilter> Availability { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int StudyAreaPage { get; set; } = 1;

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IReadOnlyList<StudyAreaRow> StudyAreas { get; private set; } = [];

    public int TotalStudyAreas { get; private set; }

    public int FilteredStudyAreas { get; private set; }

    public int TotalPages { get; private set; }

    public StudyAreaInput FormInput { get; private set; } = new();

    public int? EditingStudyAreaId { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(
        StudyAreaInput input,
        CancellationToken cancellationToken)
    {
        Normalize(input);
        await ValidateInputAsync(input, null, cancellationToken);
        if (!ModelState.IsValid)
        {
            FormInput = input;
            await LoadAsync(cancellationToken, initializeInput: false);
            return Page();
        }

        var studyArea = new StudyArea
        {
            Name = input.Name,
            Description = input.Description,
            DisplayOrder = input.DisplayOrder,
            IsActive = input.IsActive
        };
        context.StudyAreas.Add(studyArea);
        await context.SaveChangesAsync(cancellationToken);
        await campusLabelStore.SetLabelAsync(
            studyArea.StudyAreaId,
            studyArea.Name,
            input.CampusLabel,
            cancellationToken);
        RecordAudit(
            studyArea.Name,
            null,
            Describe(studyArea, input.CampusLabel));
        await context.SaveChangesAsync(cancellationToken);

        SuccessMessage = $"{studyArea.Name} was added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        int studyAreaId,
        StudyAreaInput input,
        CancellationToken cancellationToken)
    {
        StudyArea? studyArea = await context.StudyAreas.SingleOrDefaultAsync(
            area => area.StudyAreaId == studyAreaId,
            cancellationToken);
        if (studyArea is null) return NotFound();

        Normalize(input);
        await ValidateInputAsync(input, studyAreaId, cancellationToken);
        if (!ModelState.IsValid)
        {
            FormInput = input;
            EditingStudyAreaId = studyAreaId;
            await LoadAsync(cancellationToken, initializeInput: false);
            return Page();
        }

        string previousName = studyArea.Name;
        string previousLabel = await campusLabelStore.GetLabelAsync(
            studyArea.StudyAreaId,
            studyArea.Name,
            cancellationToken);
        string previousValue = Describe(studyArea, previousLabel);
        studyArea.Name = input.Name;
        studyArea.Description = input.Description;
        studyArea.DisplayOrder = input.DisplayOrder;
        studyArea.IsActive = input.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        await campusLabelStore.SetLabelAsync(
            studyArea.StudyAreaId,
            studyArea.Name,
            input.CampusLabel,
            cancellationToken);
        RecordAudit(
            previousName,
            previousValue,
            Describe(studyArea, input.CampusLabel));
        await context.SaveChangesAsync(cancellationToken);

        SuccessMessage = $"{studyArea.Name} was updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        int studyAreaId,
        CancellationToken cancellationToken)
    {
        StudyArea? studyArea = await context.StudyAreas.SingleOrDefaultAsync(
            area => area.StudyAreaId == studyAreaId,
            cancellationToken);
        if (studyArea is null) return NotFound();

        if (await context.Bookings.AnyAsync(
            booking => booking.StudyAreaId == studyAreaId,
            cancellationToken))
        {
            ErrorMessage = $"{studyArea.Name} cannot be deleted because it is used by existing bookings. Edit it and turn off availability instead.";
            return RedirectToPage();
        }

        string campusLabel = await campusLabelStore.GetLabelAsync(
            studyArea.StudyAreaId,
            studyArea.Name,
            cancellationToken);
        RecordAudit(
            studyArea.Name,
            Describe(studyArea, campusLabel),
            null);
        context.StudyAreas.Remove(studyArea);
        await context.SaveChangesAsync(cancellationToken);
        await campusLabelStore.RemoveLabelAsync(
            studyAreaId,
            cancellationToken);

        SuccessMessage = $"{studyArea.Name} was deleted.";
        return RedirectToPage();
    }

    private async Task LoadAsync(
        CancellationToken cancellationToken,
        bool initializeInput = true)
    {
        IQueryable<StudyArea> query = context.StudyAreas.AsNoTracking();
        TotalStudyAreas = await query.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            string search = SearchTerm.Trim();
            query = query.Where(area =>
                area.Name.Contains(search) ||
                (area.Description != null && area.Description.Contains(search)));
        }
        if (Availability.Count == 1)
        {
            bool isActive = Availability[0] == AvailabilityFilter.Available;
            query = query.Where(area => area.IsActive == isActive);
        }

        FilteredStudyAreas = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(
            1,
            (int)Math.Ceiling(FilteredStudyAreas / (double)PageSize));
        StudyAreaPage = Math.Clamp(StudyAreaPage, 1, TotalPages);
        List<StudyAreaRow> rows = await query
            .OrderBy(area => area.DisplayOrder)
            .ThenBy(area => area.Name)
            .Skip((StudyAreaPage - 1) * PageSize)
            .Take(PageSize)
            .Select(area => new StudyAreaRow(
                area.StudyAreaId,
                area.Name,
                area.Description,
                area.DisplayOrder,
                area.IsActive,
                area.Bookings.Count,
                string.Empty))
            .ToListAsync(cancellationToken);
        var labelledRows = new List<StudyAreaRow>(rows.Count);
        foreach (StudyAreaRow row in rows)
        {
            string campusLabel = await campusLabelStore.GetLabelAsync(
                row.Id,
                row.Name,
                cancellationToken);
            labelledRows.Add(row with { CampusLabel = campusLabel });
        }
        StudyAreas = labelledRows;

        if (initializeInput)
        {
            int highestDisplayOrder = await context.StudyAreas
                .Select(area => (int?)area.DisplayOrder)
                .MaxAsync(cancellationToken) ?? 0;
            FormInput = new StudyAreaInput
            {
                DisplayOrder = highestDisplayOrder + 1,
                IsActive = true,
                CampusLabel = StudyAreaDisplay.PretoriaCampus
            };
        }
    }

    private async Task ValidateInputAsync(
        StudyAreaInput input,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        if (input.Name.Length is < 2 or > 100)
        {
            ModelState.AddModelError(
                "input.Name",
                "Enter a study area name containing between 2 and 100 characters.");
        }
        if (input.Description?.Length > 300)
        {
            ModelState.AddModelError(
                "input.Description",
                "The description cannot exceed 300 characters.");
        }
        if (input.DisplayOrder is < 1 or > 9999)
        {
            ModelState.AddModelError(
                "input.DisplayOrder",
                "Display order must be between 1 and 9,999.");
        }
        if (!StudyAreaDisplay.IsCampusLabel(input.CampusLabel))
        {
            ModelState.AddModelError(
                "input.CampusLabel",
                "Enter a campus label containing no more than 80 characters.");
        }

        IQueryable<StudyArea> matchingAreas = context.StudyAreas;
        if (excludingId.HasValue)
        {
            matchingAreas = matchingAreas.Where(
                area => area.StudyAreaId != excludingId.Value);
        }

        string comparableName = input.Name.ToUpper();
        if (input.Name.Length <= 100 &&
            await matchingAreas.AnyAsync(
                area => area.Name.ToUpper() == comparableName,
                cancellationToken))
        {
            ModelState.AddModelError(
                "input.Name",
                "A study area with this name already exists.");
        }
    }

    private void RecordAudit(
        string name,
        string? previousValue,
        string? newValue) =>
        auditService.Record(
            "Study areas",
            $"Study area: {name}",
            previousValue,
            newValue,
            currentUserService.GetRequiredUser());

    private static void Normalize(StudyAreaInput input)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        input.Description = string.IsNullOrWhiteSpace(input.Description)
            ? null
            : input.Description.Trim();
        input.CampusLabel = string.IsNullOrWhiteSpace(input.CampusLabel)
            ? StudyAreaDisplay.GetSubtext(input.Name)
            : input.CampusLabel.Trim();
    }

    private static string Describe(StudyArea area, string campusLabel) =>
        $"Name: {area.Name}; Description: {area.Description ?? "None"}; " +
        $"Campus label: {campusLabel}; Display order: {area.DisplayOrder}; " +
        $"Available: {(area.IsActive ? "Yes" : "No")}";

    public sealed class StudyAreaInput
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public string CampusLabel { get; set; } =
            StudyAreaDisplay.PretoriaCampus;
    }

    public sealed record StudyAreaRow(
        int Id,
        string Name,
        string? Description,
        int DisplayOrder,
        bool IsActive,
        int BookingCount,
        string CampusLabel);

    public enum AvailabilityFilter
    {
        Available,
        Unavailable
    }
}
