using System.ComponentModel.DataAnnotations;

using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

public class UsersAccessModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    SettingsAuditService auditService) : PageModel
{
    [BindProperty]
    public AccessInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public bool CanManageAccess { get; private set; }
    public int CurrentUserId { get; private set; }
    public IReadOnlyList<AccessUserRow> AccessUsers { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostGrantAccessAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        if (currentUser.Role != BcUserRole.SuperAdmin) return Forbid();

        Input.PersonnelNumber = Input.PersonnelNumber?.Trim() ?? string.Empty;
        Input.Reason = Input.Reason?.Trim() ?? string.Empty;
        if (Input.Reason.Length < 5)
        {
            ModelState.AddModelError(
                "Input.Reason",
                "Enter a reason containing at least five characters.");
        }
        if (Input.Role is not (BcUserRole.HeadOfTutors or
            BcUserRole.Admin or BcUserRole.SuperAdmin))
        {
            ModelState.AddModelError("Input.Role", "Choose an administrative role.");
        }

        BcUser? target = await context.BcUsers
            .Include(user => user.Tutor)
            .Include(user => user.Admin)
            .SingleOrDefaultAsync(user =>
                user.PersonnelNumber == Input.PersonnelNumber,
                cancellationToken);
        ValidateTarget(target, currentUser);

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken, populateInput: false);
            return Page();
        }

        string previous = target!.Role.ToString();
        target.Role = Input.Role;
        target.IsAdministrativeAccessActive = true;
        EnsureAdminProfile(target);
        RecordAccessAudit(target, "Administrative role", previous,
            target.Role.ToString(), currentUser, Input.Reason);
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"{target.DisplayName} now has {RoleLabel(target.Role)} access.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(
        int userId,
        BcUserRole role,
        string reason,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        IActionResult? denied = ValidateManager(currentUser, userId, role);
        if (denied is not null) return denied;
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length < 5) return AccessError("A reason of at least five characters is required.");

        BcUser? target = await context.BcUsers
            .Include(user => user.Tutor)
            .Include(user => user.Admin)
            .SingleOrDefaultAsync(user => user.BcUserId == userId, cancellationToken);
        if (target is null) return NotFound();
        if (target.Role == BcUserRole.Dev) return Forbid();
        if (role == BcUserRole.HeadOfTutors && !IsApprovedTutor(target))
        {
            return AccessError("Head of Tutors can only be assigned to an active, approved tutor account.");
        }

        string previous = target.Role.ToString();
        target.Role = role;
        target.IsAdministrativeAccessActive = true;
        EnsureAdminProfile(target);
        RecordAccessAudit(target, "Administrative role", previous,
            role.ToString(), currentUser, reason);
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"{target.DisplayName}'s role was updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAccessAsync(
        int userId,
        bool active,
        string reason,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        IActionResult? denied = ValidateManager(currentUser, userId);
        if (denied is not null) return denied;
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length < 5) return AccessError("A reason of at least five characters is required.");

        BcUser? target = await context.BcUsers.SingleOrDefaultAsync(
            user => user.BcUserId == userId, cancellationToken);
        if (target is null) return NotFound();
        if (target.Role == BcUserRole.Dev) return Forbid();
        if (!active && target.Role == BcUserRole.SuperAdmin &&
            await IsLastActiveSuperAdminAsync(target.BcUserId, cancellationToken))
        {
            return AccessError("The last active Super Admin cannot be deactivated.");
        }

        string previous = Enabled(target.IsAdministrativeAccessActive);
        target.IsAdministrativeAccessActive = active;
        RecordAccessAudit(target, "Administrative access", previous,
            Enabled(active), currentUser, reason);
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"Administrative access for {target.DisplayName} was " +
            (active ? "activated." : "deactivated.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAccessAsync(
        int userId,
        string reason,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        IActionResult? denied = ValidateManager(currentUser, userId);
        if (denied is not null) return denied;
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length < 5) return AccessError("A reason of at least five characters is required.");

        BcUser? target = await context.BcUsers
            .Include(user => user.Tutor)
            .SingleOrDefaultAsync(user => user.BcUserId == userId, cancellationToken);
        if (target is null) return NotFound();
        if (target.Role == BcUserRole.Dev) return Forbid();
        if (target.Role == BcUserRole.SuperAdmin &&
            await IsLastActiveSuperAdminAsync(target.BcUserId, cancellationToken))
        {
            return AccessError("The last active Super Admin cannot be removed.");
        }

        string previous = target.Role.ToString();
        target.Role = IsApprovedTutor(target)
            ? BcUserRole.Tutor
            : BcUserRole.Student;
        target.IsAdministrativeAccessActive = true;
        RecordAccessAudit(target, "Administrative role", previous,
            target.Role.ToString(), currentUser, reason);
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"Administrative access was removed from {target.DisplayName}.";
        return RedirectToPage();
    }

    private async Task LoadAsync(
        CancellationToken cancellationToken,
        bool populateInput = true)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        CurrentUserId = currentUser.BcUserId;
        CanManageAccess = currentUser.Role == BcUserRole.SuperAdmin;
        if (populateInput) Input = new AccessInput();
        AccessUsers = await context.BcUsers.AsNoTracking()
            .Where(user => user.Role == BcUserRole.HeadOfTutors ||
                user.Role == BcUserRole.Admin ||
                user.Role == BcUserRole.SuperAdmin ||
                user.Role == BcUserRole.Dev)
            .OrderByDescending(user => user.Role)
            .ThenBy(user => user.DisplayName)
            .Select(user => new AccessUserRow(
                user.BcUserId,
                user.DisplayName,
                user.PersonnelNumber,
                user.Email,
                user.Role,
                user.IsAdministrativeAccessActive,
                user.LastLoginAt))
            .ToListAsync(cancellationToken);
    }

    private void ValidateTarget(BcUser? target, CurrentUser currentUser)
    {
        if (target is null)
        {
            ModelState.AddModelError(
                "Input.PersonnelNumber",
                "No existing user has that personnel number.");
        }
        else if (target.BcUserId == currentUser.BcUserId)
        {
            ModelState.AddModelError(
                "Input.PersonnelNumber",
                "You cannot change your own administrative access.");
        }
        else if (Input.Role == BcUserRole.HeadOfTutors && !IsApprovedTutor(target))
        {
            ModelState.AddModelError(
                "Input.Role",
                "Head of Tutors can only be assigned to an active, approved tutor account.");
        }
    }

    private IActionResult? ValidateManager(
        CurrentUser currentUser,
        int targetUserId,
        BcUserRole? role = null)
    {
        if (currentUser.Role != BcUserRole.SuperAdmin) return Forbid();
        if (currentUser.BcUserId == targetUserId)
        {
            return AccessError("You cannot change your own administrative access.");
        }
        if (role.HasValue && role is not (BcUserRole.HeadOfTutors or
            BcUserRole.Admin or BcUserRole.SuperAdmin)) return BadRequest();
        return null;
    }

    private void EnsureAdminProfile(BcUser target)
    {
        if (target.Role is BcUserRole.Admin or BcUserRole.SuperAdmin &&
            target.Admin is null)
        {
            target.Admin = new Models.Entities.Admin
            {
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            };
        }
    }

    private void RecordAccessAudit(
        BcUser target,
        string setting,
        string? previous,
        string? next,
        CurrentUser currentUser,
        string reason) =>
        auditService.Record(
            "Users and access",
            $"{setting}: {target.DisplayName} ({target.PersonnelNumber})",
            previous,
            next,
            currentUser,
            reason);

    private IActionResult AccessError(string message)
    {
        ErrorMessage = message;
        return RedirectToPage();
    }

    private async Task<bool> IsLastActiveSuperAdminAsync(
        int excludingUserId,
        CancellationToken cancellationToken) =>
        !await context.BcUsers.AnyAsync(user =>
            user.BcUserId != excludingUserId &&
            user.Role == BcUserRole.SuperAdmin &&
            user.IsAdministrativeAccessActive,
            cancellationToken);

    private static bool IsApprovedTutor(BcUser user) =>
        user.Tutor is { IsActive: true, Status: TutorStatus.Approved };

    private static string Enabled(bool value) => value ? "Enabled" : "Disabled";

    public static string RoleLabel(BcUserRole role) => role switch
    {
        BcUserRole.HeadOfTutors => "Head of Tutors",
        BcUserRole.SuperAdmin => "Super Admin",
        _ => role.ToString()
    };

    public sealed class AccessInput
    {
        [Required, StringLength(50)]
        [Display(Name = "Personnel number")]
        public string PersonnelNumber { get; set; } = string.Empty;

        [Required]
        public BcUserRole Role { get; set; } = BcUserRole.Admin;

        [Required, StringLength(1000, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed record AccessUserRow(
        int BcUserId,
        string DisplayName,
        string PersonnelNumber,
        string? Email,
        BcUserRole Role,
        bool IsActive,
        DateTime? LastLoginAt);
}
