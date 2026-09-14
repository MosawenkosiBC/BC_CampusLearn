using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.ViewComponents;

public class AdminHeaderViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public AdminHeaderViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        string displayName = UserClaimsPrincipal.FindFirst("name")?.Value
            ?? UserClaimsPrincipal.Identity?.Name
            ?? "Administrator";
        string roleName = GetAdministratorRoleName();

        var model = new AdminHeaderViewModel
        {
            DisplayName = displayName,
            Initials = GetInitials(displayName),
            RoleName = roleName,
            PendingTutorApplications = await _context.Tutors
                .AsNoTracking()
                .CountAsync(tutor => tutor.Status == TutorStatus.Pending),
            PendingModuleRequests = await _context.TutorModuleChangeRequests
                .AsNoTracking()
                .CountAsync(request =>
                    request.Status == TutorAccountRequestStatus.Pending),
            PendingDeregistrationRequests = await _context
                .TutorDeregistrationRequests
                .AsNoTracking()
                .CountAsync(request =>
                    request.Status == TutorAccountRequestStatus.Pending)
        };

        return View(
            "~/Pages/Administrator/Shared/Components/AdminHeader/Default.cshtml",
            model);
    }

    private string GetAdministratorRoleName()
    {
        if (UserClaimsPrincipal.IsInRole(nameof(BcUserRole.Dev)))
        {
            return "Developer";
        }

        if (UserClaimsPrincipal.IsInRole(nameof(BcUserRole.SuperAdmin)))
        {
            return "Super admin";
        }

        return "Admin";
    }

    private static string GetInitials(string displayName)
    {
        string[] nameParts = displayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (nameParts.Length == 0)
        {
            return "AD";
        }

        return nameParts.Length == 1
            ? nameParts[0][..1].ToUpperInvariant()
            : $"{nameParts[0][0]}{nameParts[^1][0]}".ToUpperInvariant();
    }
}
