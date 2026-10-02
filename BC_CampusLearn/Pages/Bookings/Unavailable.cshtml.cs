using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Bookings;

[Authorize]
public class UnavailableModel : PageModel
{
    public bool WasBooked { get; private set; }
    public bool HasExpired { get; private set; }
    public bool IsReserved { get; private set; }
    public bool ReservationExpired { get; private set; }

    public void OnGet(string? reason)
    {
        WasBooked = string.Equals(
            reason,
            "booked",
            StringComparison.OrdinalIgnoreCase);
        HasExpired = string.Equals(
            reason,
            "expired",
            StringComparison.OrdinalIgnoreCase);
        IsReserved = string.Equals(
            reason,
            "reserved",
            StringComparison.OrdinalIgnoreCase);
        ReservationExpired = string.Equals(
            reason,
            "reservation-expired",
            StringComparison.OrdinalIgnoreCase);
    }
}
