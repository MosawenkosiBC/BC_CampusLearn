using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Account;

[Authorize]
public class PostLoginModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PostLoginModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        bool isTutor = await _context.Tutors
            .AsNoTracking()
            .AnyAsync(
                tutor =>
                    tutor.BcUserId == currentUser.BcUserId &&
                    tutor.Status == TutorStatus.Approved &&
                    tutor.IsActive,
                cancellationToken);

        return RedirectToPage(
            isTutor
                ? "/Tutors/TutorDashboard"
                : "/Student/Dashboard");
    }
}
