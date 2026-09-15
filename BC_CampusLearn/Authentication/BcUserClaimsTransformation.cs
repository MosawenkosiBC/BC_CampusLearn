using System.Security.Claims;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Authentication;

public sealed class BcUserClaimsTransformation : IClaimsTransformation
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public BcUserClaimsTransformation(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        string? bcUserIdValue =
            principal.FindFirstValue(EntraClaimTypes.BcUserId);

        if (int.TryParse(bcUserIdValue, out int existingBcUserId))
        {
            BcUser existingUser = await _context.BcUsers
                .Include(user => user.Admin)
                .SingleOrDefaultAsync(user =>
                    user.BcUserId == existingBcUserId)
                ?? throw new InvalidOperationException(
                    "The authenticated principal is linked to a BC user that no longer exists.");

            if (RequiresAdminProfile(existingUser.Role) &&
                existingUser.Admin is null)
            {
                existingUser.Admin = new Admin
                {
                    CreatedAt = DateTime.UtcNow
                };
                await _context.SaveChangesAsync();
            }

            string? existingTutorProfileImagePath = existingUser.Role is
                    BcUserRole.Tutor or BcUserRole.HeadOfTutors
                ? await _context.Tutors
                    .AsNoTracking()
                    .Where(tutor => tutor.BcUserId == existingBcUserId)
                    .Select(tutor => tutor.ProfileImagePath)
                    .SingleOrDefaultAsync()
                : null;

            AddApplicationClaims(
                principal,
                existingBcUserId,
                existingUser.Role,
                existingTutorProfileImagePath,
                personnelNumber: null);

            return principal;
        }

        string? personnelNumber =
            principal.FindFirstValue(EntraClaimTypes.PersonnelNumber);
        string? displayName = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(EntraClaimTypes.DisplayName);
        string? email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(EntraClaimTypes.PreferredUsername);

        if (string.IsNullOrWhiteSpace(personnelNumber))
        {
            throw new InvalidOperationException(
                "A verified personnel number is required to link a BC user.");
        }

        string normalizedPersonnelNumber = personnelNumber.Trim();
        BcUser? user = await _context.BcUsers
            .Include(item => item.Admin)
            .SingleOrDefaultAsync(item =>
                item.PersonnelNumber == normalizedPersonnelNumber);

        BcUserRole? developmentRole = GetDevelopmentRole(principal);

        if (user is null)
        {
            user = new BcUser
            {
                PersonnelNumber = normalizedPersonnelNumber,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? normalizedPersonnelNumber
                    : displayName.Trim(),
                Email = string.IsNullOrWhiteSpace(email)
                    ? null
                    : email.Trim(),
                Role = developmentRole ?? BcUserRole.Student,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _context.BcUsers.Add(user);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                user.DisplayName = displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                user.Email = email.Trim();
            }

            if (string.IsNullOrWhiteSpace(user.PersonnelNumber))
            {
                throw new InvalidOperationException(
                    "The linked BC user does not have a verified personnel number.");
            }

            if (developmentRole.HasValue)
            {
                user.Role = developmentRole.Value;
            }

            user.LastLoginAt = DateTime.UtcNow;
        }

        if (RequiresAdminProfile(user.Role) &&
            user.Admin is null)
        {
            user.Admin = new Admin
            {
                CreatedAt = DateTime.UtcNow
            };
        }

        await _context.SaveChangesAsync();

        string? tutorProfileImagePath = user.Role is
                BcUserRole.Tutor or BcUserRole.HeadOfTutors
            ? await _context.Tutors
                .AsNoTracking()
                .Where(tutor => tutor.BcUserId == user.BcUserId)
                .Select(tutor => tutor.ProfileImagePath)
                .SingleOrDefaultAsync()
            : null;

        AddApplicationClaims(
            principal,
            user.BcUserId,
            user.Role,
            tutorProfileImagePath,
            user.PersonnelNumber);

        return principal;
    }

    private BcUserRole? GetDevelopmentRole(ClaimsPrincipal principal)
    {
        if (!_environment.IsDevelopment())
        {
            return null;
        }

        string? roleValue = principal.FindFirstValue(
            EntraClaimTypes.DevelopmentRole);

        return Enum.TryParse(roleValue, ignoreCase: false, out BcUserRole role) &&
            Enum.IsDefined(role)
                ? role
                : null;
    }

    private static bool RequiresAdminProfile(BcUserRole role) =>
        role is BcUserRole.Admin or
            BcUserRole.SuperAdmin or
            BcUserRole.Dev;

    private static void AddApplicationClaims(
        ClaimsPrincipal principal,
        int bcUserId,
        BcUserRole role,
        string? tutorProfileImagePath,
        string? personnelNumber)
    {
        ClaimsIdentity? applicationIdentity = principal.Identities
            .FirstOrDefault(identity =>
                identity.AuthenticationType == "BcUser");

        if (applicationIdentity is not null)
        {
            foreach (Claim claim in applicationIdentity.Claims.ToList())
            {
                applicationIdentity.RemoveClaim(claim);
            }
        }

        var claims = new List<Claim>
        {
            new(
                EntraClaimTypes.BcUserId,
                bcUserId.ToString()),
            new(
                EntraClaimTypes.BcRole,
                role.ToString())
        };

        if (!string.IsNullOrWhiteSpace(tutorProfileImagePath) &&
            !principal.HasClaim(claim =>
                claim.Type == EntraClaimTypes.TutorProfileImagePath))
        {
            claims.Add(new Claim(
                EntraClaimTypes.TutorProfileImagePath,
                tutorProfileImagePath));
        }

        if (!string.IsNullOrWhiteSpace(personnelNumber) &&
            !principal.HasClaim(claim =>
                claim.Type == EntraClaimTypes.PersonnelNumber))
        {
            claims.Add(new Claim(
                EntraClaimTypes.PersonnelNumber,
                personnelNumber));
        }

        if (claims.Count == 0)
        {
            return;
        }

        if (applicationIdentity is null)
        {
            principal.AddIdentity(new ClaimsIdentity(
                claims,
                authenticationType: "BcUser",
                nameType: ClaimTypes.Name,
                roleType: EntraClaimTypes.BcRole));
        }
        else
        {
            applicationIdentity.AddClaims(claims);
        }
    }
}
