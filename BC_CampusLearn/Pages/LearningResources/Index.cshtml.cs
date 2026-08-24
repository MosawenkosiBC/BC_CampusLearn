using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.LearningResources;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public IReadOnlyList<StudentLearningResourceListItem> Resources
    { get; private set; } = Array.Empty<StudentLearningResourceListItem>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        List<string> subscribedModuleCodes = await _context.ResourceSubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.PersonnelNumber == currentUser.PersonnelNumber &&
                subscription.IsActive)
            .Select(subscription => subscription.ModuleCode)
            .ToListAsync(cancellationToken);

        Resources = await _context.LearningResources
            .AsNoTracking()
            .Where(resource =>
                resource.Status == LearningResourceStatus.Published &&
                subscribedModuleCodes.Contains(resource.ProgrammeModule.ModuleCode))
            .OrderByDescending(resource => resource.DatePublished ?? resource.DateCreated)
            .Select(resource => new StudentLearningResourceListItem
            {
                LearningResourceId = resource.LearningResourceId,
                Topic = resource.Topic,
                Content = resource.Content,
                ModuleCode = resource.ProgrammeModule.ModuleCode,
                ModuleName = resource.ProgrammeModule.ModuleName,
                TutorId = resource.TutorId,
                TutorName = string.IsNullOrWhiteSpace(resource.Tutor.BcUser.DisplayName)
                    ? resource.Tutor.BcUser.PersonnelNumber
                    : resource.Tutor.BcUser.DisplayName,
                TutorProfileImagePath = resource.Tutor.ProfileImagePath,
                DatePublished = resource.DatePublished
            })
            .ToListAsync(cancellationToken);
    }
}
