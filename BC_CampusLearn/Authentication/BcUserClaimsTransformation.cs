using System.Security.Claims;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Authentication;

public sealed class BcUserClaimsTransformation : IClaimsTransformation
{
    private readonly ApplicationDbContext _context;

    public BcUserClaimsTransformation(ApplicationDbContext context)
    {
        _context = context;
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
                .Where(tutor =>
                    tutor.BcUserId == existingBcUserId &&
                    tutor.Status == TutorStatus.Approved &&
                    tutor.IsActive)
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

        string? personnelNumber = principal.FindFirstValue(EntraClaimTypes.PersonnelNumber);
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
        BcUser? user = await _context.BcUsers.SingleOrDefaultAsync(item =>
            item.PersonnelNumber == normalizedPersonnelNumber);

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

            user.LastLoginAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var tutorProfile = await _context.Tutors
            .AsNoTracking()
            .Where(tutor =>
                tutor.BcUserId == user.BcUserId &&
                tutor.Status == TutorStatus.Approved &&
                tutor.IsActive)
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
