using BC_CampusLearn.Authentication;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Account;

[Authorize]
public class PostLoginModel : PageModel
{
    private readonly ICurrentUserService _currentUserService;

    public PostLoginModel(
        ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public IActionResult OnGet()
    {
        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        string dashboardPage = currentUser.Role switch
        {
            BcUserRole.Tutor or BcUserRole.HeadOfTutors =>
                "/Tutors/TutorDashboard",
            BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.Dev =>
                "/Administrator/Admin/Dashboard",
            _ => "/Student/Dashboard"
        };

        return RedirectToPage(dashboardPage);
    }
}
