using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.ViewComponents;

public class TutorOnboardingViewComponent(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!currentUserService.IsAuthenticated ||
            currentUserService.GetRequiredUser().Role != BcUserRole.Tutor ||
            string.Equals(ViewContext.RouteData.Values["Page"]?.ToString(),
                "/Tutors/Onboarding", StringComparison.OrdinalIgnoreCase))
            return Content(string.Empty);

        int userId = currentUserService.GetRequiredUser().BcUserId;
        var tutor = await context.Tutors.AsNoTracking()
            .Where(t => t.BcUserId == userId && t.Status == TutorStatus.Approved && t.IsActive)
            .Select(t => new { t.Biography, t.PhoneNumber, t.PreferredTutoringMode })
            .SingleOrDefaultAsync(HttpContext.RequestAborted);

        if (tutor is null || !string.IsNullOrWhiteSpace(tutor.Biography))
            return Content(string.Empty);

        ViewData["TutorOnboardingStorageKey"] = $"campuslearn:tutor-onboarding:{userId}";
        ViewData["TutorOnboardingLoginId"] = HttpContext.User
            .FindFirst(TutorOnboardingSession.ClaimType)?.Value ?? "legacy";

        return View("~/Pages/Shared/Components/TutorOnboarding/Default.cshtml",
            new TutorOnboardingInput
            {
                PhoneNumber = tutor.PhoneNumber,
                PreferredTutoringMode = tutor.PreferredTutoringMode
            });
    }
}
