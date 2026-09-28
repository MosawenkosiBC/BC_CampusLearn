using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Administrator.Admin;

public class SettingsRedirectModel : PageModel
{
    public IActionResult OnGet() =>
        RedirectToPage("/Administrator/Settings/General");
}
