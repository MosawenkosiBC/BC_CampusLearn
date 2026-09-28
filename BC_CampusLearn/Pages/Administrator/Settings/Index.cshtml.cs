using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Administrator/Settings/General");
}
