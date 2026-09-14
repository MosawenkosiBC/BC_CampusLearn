using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Tutors;

public class IndexModel(ApplicationDbContext context) : PageModel
{
    public const int PageSize = 8;
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
    public int TotalTutors { get; private set; }
    public int TotalPages { get; private set; }
    public IReadOnlyList<Tutor> Tutors { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = context.Tutors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(SearchName))
        {
            string name = SearchName.Trim();
            query = query.Where(tutor => tutor.BcUser.DisplayName.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(SearchModule))
        {
            string module = SearchModule.Trim();
            query = query.Where(tutor => tutor.TutorCourseModules.Any(item =>
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
}
