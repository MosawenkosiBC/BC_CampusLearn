using System.Security.Claims;
using BC_CampusLearn.Authentication.Development;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Authentication;

public sealed class BcUserClaimsTransformation : IClaimsTransformation
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly DevelopmentUserOptions _developmentUser;

    public BcUserClaimsTransformation(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        IOptions<DevelopmentUserOptions> developmentUserOptions)
    {
        _context = context;
        _environment = environment;
        _developmentUser = developmentUserOptions.Value;
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
            var existingTutor = await _context.Tutors
                .AsNoTracking()
                .Where(tutor => tutor.BcUserId == existingBcUserId)
                .Select(tutor => new
                {
                    tutor.ProfileImagePath
                })
                .SingleOrDefaultAsync();

            var tutorIdentity = new ClaimsIdentity();
            if (!principal.HasClaim(
                    claim => claim.Type == EntraClaimTypes.IsTutor))
            {
                tutorIdentity.AddClaim(new Claim(
                    EntraClaimTypes.IsTutor,
                    (existingTutor is not null).ToString()));
            }

            if (!string.IsNullOrWhiteSpace(existingTutor?.ProfileImagePath) &&
                !principal.HasClaim(claim =>
                    claim.Type == EntraClaimTypes.TutorProfileImagePath))
            {
                tutorIdentity.AddClaim(new Claim(
                    EntraClaimTypes.TutorProfileImagePath,
                    existingTutor.ProfileImagePath));
            }

            if (tutorIdentity.Claims.Any())
            {
                principal.AddIdentity(tutorIdentity);
            }

            return principal;
        }

        string? objectIdValue = principal.FindFirstValue(EntraClaimTypes.ObjectId)
            ?? principal.FindFirstValue(EntraClaimTypes.ObjectIdUri);
        string? tenantIdValue = principal.FindFirstValue(EntraClaimTypes.TenantId)
            ?? principal.FindFirstValue(EntraClaimTypes.TenantIdUri);
        string? personnelNumber = principal.FindFirstValue(EntraClaimTypes.PersonnelNumber);
        string? displayName = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(EntraClaimTypes.DisplayName);
        string? email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(EntraClaimTypes.PreferredUsername);

        if (!Guid.TryParse(objectIdValue, out Guid objectId) ||
            !Guid.TryParse(tenantIdValue, out Guid tenantId))
        {
            throw new InvalidOperationException(
                "BC user provisioning requires valid oid and tid claims.");
        }

        BcUser? user = await _context.BcUsers.SingleOrDefaultAsync(item =>
            item.EntraTenantId == tenantId && item.EntraObjectId == objectId);

        if (string.IsNullOrWhiteSpace(personnelNumber) &&
            _environment.IsDevelopment() &&
            Guid.TryParse(_developmentUser.ObjectId, out Guid developmentObjectId) &&
            Guid.TryParse(_developmentUser.TenantId, out Guid developmentTenantId) &&
            developmentObjectId == objectId &&
            developmentTenantId == tenantId)
        {
            personnelNumber = _developmentUser.PersonnelNumber;
        }

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(personnelNumber))
            {
                throw new InvalidOperationException(
                    "No BC user is linked to this Entra account. " +
                    "A verified personnel number is required to create one.");
            }

            user = new BcUser
            {
                EntraObjectId = objectId,
                EntraTenantId = tenantId,
                PersonnelNumber = personnelNumber.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? personnelNumber.Trim()
                    : displayName.Trim(),
                Email = string.IsNullOrWhiteSpace(email)
                    ? null
                    : email.Trim(),
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _context.BcUsers.Add(user);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(personnelNumber))
            {
                user.PersonnelNumber = personnelNumber.Trim();
            }

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

            user.LastLoginAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var tutorProfile = await _context.Tutors
            .AsNoTracking()
            .Where(tutor => tutor.BcUserId == user.BcUserId)
            .Select(tutor => new
            {
                tutor.ProfileImagePath
            })
            .SingleOrDefaultAsync();

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(EntraClaimTypes.BcUserId, user.BcUserId.ToString()));
        identity.AddClaim(new Claim(
            EntraClaimTypes.IsTutor,
            (tutorProfile is not null).ToString()));
        if (!string.IsNullOrWhiteSpace(tutorProfile?.ProfileImagePath))
        {
            identity.AddClaim(new Claim(
                EntraClaimTypes.TutorProfileImagePath,
                tutorProfile.ProfileImagePath));
        }
        if (!principal.HasClaim(claim => claim.Type == EntraClaimTypes.PersonnelNumber))
        {
            identity.AddClaim(new Claim(EntraClaimTypes.PersonnelNumber, user.PersonnelNumber));
        }
        principal.AddIdentity(identity);
        return principal;
    }
}
