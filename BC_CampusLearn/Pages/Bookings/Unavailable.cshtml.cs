using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class UnavailableModel : PageModel
{
    public bool WasBooked { get; private set; }

    public void OnGet(string? reason)
    {
        WasBooked = string.Equals(
            reason,
            "booked",
            StringComparison.OrdinalIgnoreCase);
    }
}
