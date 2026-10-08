using System.ComponentModel.DataAnnotations;

using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

[Authorize(Roles = nameof(BcUserRole.SuperAdmin))]
public class UsersAccessModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    SettingsAuditService auditService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<BcUserRole> Roles { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public List<AccessStatusFilter> AccessStatus { get; set; } = [];

    [BindProperty]
    public AccessInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public bool CanManageAccess { get; private set; }
    public int CurrentUserId { get; private set; }
    public IReadOnlyList<AccessUserRow> AccessUsers { get; private set; } = [];
    public int TotalAccessUsers { get; private set; }
    public int FilteredAccessUsers { get; private set; }
    public AccessUserMatch? SelectedAccessUser { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.GetRequiredUser().Role != BcUserRole.SuperAdmin)
            return Forbid();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetSearchUsersAsync(
        string? term, CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        if (currentUser.Role != BcUserRole.SuperAdmin) return Forbid();
        string search = term?.Trim().ToLowerInvariant() ?? string.Empty;
        if (search.Length < 2 || search.Length > 320)
            return new JsonResult(Array.Empty<AccessUserMatch>());

        var matches = await context.BcUsers.AsNoTracking()
            .Where(user => user.BcUserId != currentUser.BcUserId &&
                user.Role != BcUserRole.Dev &&
                ((user.Tutor != null && user.Tutor.ApplicationStage == TutorApplicationStage.Placement &&
                  user.Tutor.IsActive && user.Tutor.Status == TutorStatus.Approved) ||
                 user.PersonnelNumber == null || user.PersonnelNumber.Trim() == "") &&
                ((user.PersonnelNumber != null && user.PersonnelNumber.ToLower().Contains(search)) ||
                 (user.Email != null && user.Email.ToLower().Contains(search))))
            .OrderBy(user => user.DisplayName)
            .Take(10)
            .Select(user => new { user.BcUserId, user.DisplayName, user.PersonnelNumber, user.Email, user.Role, user.IsAdministrativeAccessActive })
            .ToListAsync(cancellationToken);
        return new JsonResult(matches.Select(user => new AccessUserMatch(
            user.BcUserId, user.DisplayName, user.PersonnelNumber, user.Email, RoleLabel(user.Role),
            IsAdministrativeRole(user.Role), user.IsAdministrativeAccessActive)));
    }

    public async Task<IActionResult> OnPostGrantAccessAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        if (currentUser.Role != BcUserRole.SuperAdmin) return Forbid();

        Input.UserIdentifier = Input.UserIdentifier?.Trim() ?? string.Empty;
        Input.Reason = Input.Reason?.Trim() ?? string.Empty;
        if (Input.Role is not (BcUserRole.HeadOfTutors or
            BcUserRole.Admin or BcUserRole.SuperAdmin))
        {
            ModelState.AddModelError("Input.Role", "Choose an administrative role.");
        }

        BcUser? target = await context.BcUsers
            .Include(user => user.Tutor)
            .Include(user => user.Admin)
            .SingleOrDefaultAsync(user =>
                Input.UserId.HasValue
                    ? user.BcUserId == Input.UserId.Value
                    : user.PersonnelNumber == Input.UserIdentifier || user.Email == Input.UserIdentifier,
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
        if (target.Role == BcUserRole.HeadOfTutors && role != BcUserRole.HeadOfTutors)
            return AccessError("Tutor Heads cannot be promoted to Admin or Super Admin.");
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

    public async Task<IActionResult> OnPostPromoteAsync(
        int userId, BcUserRole role, string? reason, CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        IActionResult? denied = ValidateManager(currentUser, userId, role);
        if (denied is not null) return denied;
        reason = reason?.Trim();
        if (reason?.Length > 1000) return AccessError("The reason must be no more than 1,000 characters.");
        BcUser? target = await context.BcUsers.Include(user => user.Tutor).Include(user => user.Admin)
            .SingleOrDefaultAsync(user => user.BcUserId == userId, cancellationToken);
        if (target is null) return NotFound();
        if (target.Role == BcUserRole.Dev) return Forbid();
        if (target.Role == BcUserRole.HeadOfTutors)
            return AccessError("Tutor Heads cannot be promoted to Admin or Super Admin.");
        if (role <= target.Role) return AccessError("Choose a higher role to promote this user.");
        if (role == BcUserRole.HeadOfTutors && !IsApprovedTutor(target))
            return AccessError("Head of Tutors can only be assigned to an active, approved tutor account.");

        string previous = target.Role.ToString();
        target.Role = role;
        target.IsAdministrativeAccessActive = true;
        EnsureAdminProfile(target);
        RecordAccessAudit(target, "Administrative role", previous, role.ToString(), currentUser, reason);
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"{target.DisplayName} was promoted to {RoleLabel(role)}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDemoteAsync(
        int userId, string? reason, CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        IActionResult? denied = ValidateManager(currentUser, userId);
        if (denied is not null) return denied;
        reason = reason?.Trim();
        if (reason?.Length > 1000) return AccessError("The reason must be no more than 1,000 characters.");
        BcUser? target = await context.BcUsers.Include(user => user.Tutor).Include(user => user.Admin)
            .SingleOrDefaultAsync(user => user.BcUserId == userId, cancellationToken);
        if (target is null) return NotFound();
        if (target.Role == BcUserRole.Dev) return Forbid();
        if (!IsAdministrativeRole(target.Role)) return AccessError("This user does not have an administrative role to demote.");
        if (target.Role == BcUserRole.SuperAdmin &&
            await IsLastActiveSuperAdminAsync(target.BcUserId, cancellationToken))
            return AccessError("The last active Super Admin cannot be demoted.");

        string previous = target.Role.ToString();
        target.Role = DemotionRole(target.Role, IsApprovedTutor(target));
        target.IsAdministrativeAccessActive = true;
        EnsureAdminProfile(target);
        RecordAccessAudit(target, "Administrative role", previous, target.Role.ToString(), currentUser, reason);
        await context.SaveChangesAsync(cancellationToken);
        SuccessMessage = $"{target.DisplayName} was demoted to {RoleLabel(target.Role)}.";
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

    public async Task<IActionResult> OnPostRemoveSelectedAccessAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.GetRequiredUser().Role != BcUserRole.SuperAdmin) return Forbid();
        if (!Input.UserId.HasValue)
            ModelState.AddModelError("Input.UserIdentifier", "Select a user before removing access.");
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken, populateInput: false);
            return Page();
        }
        return await OnPostRemoveAccessAsync(Input.UserId!.Value, Input.Reason, cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveAccessAsync(
        int userId,
        string? reason,
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        IActionResult? denied = ValidateManager(currentUser, userId);
        if (denied is not null) return denied;
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length > 1000) return AccessError("The reason must be no more than 1,000 characters.");

        BcUser? target = await context.BcUsers
            .Include(user => user.Tutor)
            .SingleOrDefaultAsync(user => user.BcUserId == userId, cancellationToken);
        if (target is null) return NotFound();
        if (target.Role == BcUserRole.Dev) return Forbid();
        if (!IsAdministrativeRole(target.Role)) return AccessError("This user does not have administrative access.");
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
        if (Input.UserId.HasValue)
        {
            BcUser? selected = await context.BcUsers.AsNoTracking()
                .SingleOrDefaultAsync(user => user.BcUserId == Input.UserId.Value, cancellationToken);
            if (selected is not null)
                SelectedAccessUser = new(selected.BcUserId, selected.DisplayName,
                    selected.PersonnelNumber, selected.Email, RoleLabel(selected.Role),
                    IsAdministrativeRole(selected.Role), selected.IsAdministrativeAccessActive);
        }
        IQueryable<BcUser> query = context.BcUsers.AsNoTracking()
            .Where(user => user.Role == BcUserRole.HeadOfTutors ||
                user.Role == BcUserRole.Admin ||
                user.Role == BcUserRole.SuperAdmin ||
                user.Role == BcUserRole.Dev);
        TotalAccessUsers = await query.CountAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            string search = SearchTerm.Trim();
            query = query.Where(user =>
                user.DisplayName.Contains(search) ||
                (user.PersonnelNumber != null &&
                    user.PersonnelNumber.Contains(search)) ||
                (user.Email != null && user.Email.Contains(search)));
        }
        if (Roles.Count > 0)
        {
            query = query.Where(user => Roles.Contains(user.Role));
        }
        if (AccessStatus.Count == 1)
        {
            bool isActive = AccessStatus[0] == AccessStatusFilter.Active;
            query = query.Where(user =>
                user.IsAdministrativeAccessActive == isActive);
        }

        FilteredAccessUsers = await query.CountAsync(cancellationToken);
        AccessUsers = await query
            .OrderByDescending(user => user.Role)
            .ThenBy(user => user.DisplayName)
            .Select(user => new AccessUserRow(
                user.BcUserId,
                user.DisplayName,
                user.PersonnelNumber,
                user.Email,
                user.Role,
                user.IsAdministrativeAccessActive,
                user.LastLoginAt,
                user.Tutor != null && user.Tutor.IsActive && user.Tutor.Status == TutorStatus.Approved))
            .ToListAsync(cancellationToken);
    }

    private void ValidateTarget(BcUser? target, CurrentUser currentUser)
    {
        if (target is null)
        {
            ModelState.AddModelError(
                "Input.UserIdentifier",
                "No existing user has that personnel number or email address.");
        }
        else if (target.BcUserId == currentUser.BcUserId)
        {
            ModelState.AddModelError(
                "Input.UserIdentifier",
                "You cannot change your own administrative access.");
        }
        else if (target.Role == BcUserRole.Dev)
        {
            ModelState.AddModelError("Input.UserIdentifier", "Developer access cannot be changed here.");
        }
        else if (target.Role == BcUserRole.HeadOfTutors && Input.Role != BcUserRole.HeadOfTutors)
        {
            ModelState.AddModelError("Input.Role", "Tutor Heads cannot be promoted to Admin or Super Admin.");
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
        string? reason) =>
        auditService.Record(
            "Users and access",
            $"{setting}: {target.DisplayName} ({target.PersonnelNumber ?? target.Email ?? "No identifier"})",
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

    private static bool IsAdministrativeRole(BcUserRole role) =>
        role is BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.HeadOfTutors;

    public static BcUserRole DemotionRole(BcUserRole role, bool isTutor) =>
        role == BcUserRole.SuperAdmin ? BcUserRole.Admin :
        isTutor ? BcUserRole.Tutor : BcUserRole.Student;

    private static string Enabled(bool value) => value ? "Enabled" : "Disabled";

    public static string RoleLabel(BcUserRole role) => role switch
    {
        BcUserRole.SeniorTutor => "Senior Tutor",
        BcUserRole.HeadOfTutors => "Head of Tutors",
        BcUserRole.SuperAdmin => "Super Admin",
        _ => role.ToString()
    };

    public sealed class AccessInput
    {
        public int? UserId { get; set; }

        [Required, StringLength(320)]
        [Display(Name = "Student / personnel number or email")]
        public string UserIdentifier { get; set; } = string.Empty;

        [Required]
        public BcUserRole Role { get; set; } = BcUserRole.Admin;

        [StringLength(1000)]
        [Display(Name = "Reason (optional)")]
        public string? Reason { get; set; }
    }

    public sealed record AccessUserMatch(
        int UserId, string DisplayName, string? PersonnelNumber, string? Email, string Role,
        bool HasAdministrativeAccess, bool IsActive);

    public sealed record AccessUserRow(
        int BcUserId,
        string DisplayName,
        string? PersonnelNumber,
        string? Email,
        BcUserRole Role,
        bool IsActive,
        DateTime? LastLoginAt,
        bool IsTutor);

    public enum AccessStatusFilter
    {
        Active,
        Inactive
    }
}
