using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Students;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Tutors;

public class IndexModel(ApplicationDbContext context, IStudentDetailsService studentDetailsService)
    : ManualTutorPageModel(context, studentDetailsService)
{
    public const int PageSize = 8;
    public const int ApplicationPageSize = 10;
    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "all";
    [BindProperty(SupportsGet = true)]
    public string? SearchName { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? SearchModule { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? SearchCourse { get; set; }
    [BindProperty(SupportsGet = true)]
    public List<int> Years { get; set; } = [];
    [BindProperty(SupportsGet = true)]
    public int TutorPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)]
    public int ApplicationPage { get; set; } = 1;
    public int TotalTutors { get; private set; }
    public int TotalPages { get; private set; }
    public int TotalApplications { get; private set; }
    public int ApplicationPages { get; private set; }
    public int PendingApplications { get; private set; }
    public IReadOnlyList<Tutor> Tutors { get; private set; } = [];
    public IReadOnlyList<SeniorTutorApplication> SeniorTutorApplications
    { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Tab = Tab == "senior-applications" ? "senior-applications" : "all";
        PendingApplications = await Context.SeniorTutorApplications
            .AsNoTracking()
            .CountAsync(
                application => application.Status == TutorAccountRequestStatus.Pending,
                cancellationToken);

        if (Tab == "senior-applications")
        {
            await LoadSeniorTutorApplicationsAsync(cancellationToken);
            return;
        }

        await LoadTutorsAsync(cancellationToken);
    }

    private async Task LoadTutorsAsync(CancellationToken cancellationToken)
    {
        await LoadManualTutorOptionsAsync(cancellationToken);

        var query = Context.Tutors.AsNoTracking().Where(tutor =>
            tutor.ApplicationStage == TutorApplicationStage.Placement &&
            tutor.IsActive &&
            tutor.Status == TutorStatus.Approved);
        if (!string.IsNullOrWhiteSpace(SearchName))
        {
            string name = SearchName.Trim();
            query = query.Where(tutor => tutor.BcUser.DisplayName.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(SearchModule))
        {
            string module = SearchModule.Trim();
            query = query.Where(tutor => tutor.TutorCourseModules.Where(a => a.IsActive).Any(item =>
                item.ProgrammeModule.ModuleCode.Contains(module) ||
                item.ProgrammeModule.ModuleName.Contains(module)));
        }
        if (!string.IsNullOrWhiteSpace(SearchCourse))
        {
            string course = SearchCourse.Trim();
            query = query.Where(tutor => tutor.Programme.Name.Contains(course));
        }
        if (Years.Count > 0)
        {
            query = query.Where(tutor => Years.Contains(tutor.YearOfStudy));
        }

        TotalTutors = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalTutors / (double)PageSize));
        TutorPage = Math.Clamp(TutorPage, 1, TotalPages);
        Tutors = await query
            .Include(tutor => tutor.BcUser)
            .Include(tutor => tutor.Programme)
            .OrderBy(tutor => tutor.BcUser.DisplayName)
            .ThenBy(tutor => tutor.TutorId)
            .Skip((TutorPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
    }

    private async Task LoadSeniorTutorApplicationsAsync(
        CancellationToken cancellationToken)
    {
        IQueryable<SeniorTutorApplication> query = Context.SeniorTutorApplications
            .AsNoTracking();

        TotalApplications = await query.CountAsync(cancellationToken);
        ApplicationPages = Math.Max(
            1,
            (int)Math.Ceiling(TotalApplications / (double)ApplicationPageSize));
        ApplicationPage = Math.Clamp(ApplicationPage, 1, ApplicationPages);

        SeniorTutorApplications = await query
            .Include(application => application.Tutor)
                .ThenInclude(tutor => tutor.BcUser)
            .Include(application => application.Tutor)
                .ThenInclude(tutor => tutor.Programme)
            .OrderBy(application => application.Status)
            .ThenByDescending(application => application.SubmittedAt)
            .ThenByDescending(application => application.SeniorTutorApplicationId)
            .Skip((ApplicationPage - 1) * ApplicationPageSize)
            .Take(ApplicationPageSize)
            .ToListAsync(cancellationToken);
    }
}
