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
    private readonly IEntraIdentityProtector _identityProtector;

    public BcUserClaimsTransformation(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        IEntraIdentityProtector identityProtector)
    {
        _context = context;
        _environment = environment;
        _identityProtector = identityProtector;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        string? tenantId = principal.FindFirstValue(EntraClaimTypes.TenantId)
            ?? principal.FindFirstValue(EntraClaimTypes.TenantIdUri);
        string? objectId = principal.FindFirstValue(EntraClaimTypes.ObjectId)
            ?? principal.FindFirstValue(EntraClaimTypes.ObjectIdUri);

        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(objectId))
        {
            throw new InvalidOperationException(
                "The authenticated principal does not contain the required Entra tenant and object identifiers.");
        }

        string identityLookupHash = _identityProtector.CreateLookupHash(
            tenantId,
            objectId);

        string? personnelNumber =
            principal.FindFirstValue(EntraClaimTypes.PersonnelNumber);
        string? displayName = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(EntraClaimTypes.DisplayName);
        string? email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(EntraClaimTypes.PreferredUsername);

        if (string.IsNullOrWhiteSpace(personnelNumber))
        {
            personnelNumber = GetStudentNumberFromPreferredUsername(
                principal.FindFirstValue(
                    EntraClaimTypes.PreferredUsername));
        }

        string? normalizedPersonnelNumber = string.IsNullOrWhiteSpace(personnelNumber)
            ? null
            : personnelNumber.Trim();
        BcUser? user = await _context.BcUsers
            .Include(item => item.Admin)
            .SingleOrDefaultAsync(item =>
                item.EntraIdentityLookupHash == identityLookupHash);

        if (user is null && normalizedPersonnelNumber is not null)
        {
            user = await _context.BcUsers
                .Include(item => item.Admin)
                .SingleOrDefaultAsync(item =>
                    item.PersonnelNumber == normalizedPersonnelNumber);

            if (user?.EntraIdentityLookupHash is not null)
            {
                throw new InvalidOperationException(
                    "The supplied personnel number is already linked to another Entra identity.");
            }
        }

        // One-time bridge for administrator records created before encrypted
        // Entra identifiers were introduced. Once linked, all future lookups
        // use the immutable Entra identity fingerprint rather than email.
        if (user is null &&
            normalizedPersonnelNumber is null &&
            !string.IsNullOrWhiteSpace(email))
        {
            List<BcUser> emailMatches = await _context.BcUsers
                .Include(item => item.Admin)
                .Where(item =>
                    item.EntraIdentityLookupHash == null &&
                    item.Email == email.Trim())
                .Take(2)
                .ToListAsync();

            if (emailMatches.Count > 1)
            {
                throw new InvalidOperationException(
                    "More than one unlinked BC user has the authenticated email address.");
            }

            user = emailMatches.SingleOrDefault();
        }

        BcUserRole? developmentRole = GetDevelopmentRole(principal);

        if (user is null)
        {
            ProtectedEntraIdentity protectedIdentity =
                _identityProtector.Protect(tenantId, objectId);
            user = new BcUser
            {
                PersonnelNumber = normalizedPersonnelNumber,
                EncryptedEntraTenantId = protectedIdentity.TenantId,
                EncryptedEntraObjectId = protectedIdentity.ObjectId,
                EntraIdentityLookupHash = protectedIdentity.LookupHash,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? normalizedPersonnelNumber ?? "Institution user"
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
            if (user.EntraIdentityLookupHash is null)
            {
                ProtectedEntraIdentity protectedIdentity =
                    _identityProtector.Protect(tenantId, objectId);
                user.EncryptedEntraTenantId = protectedIdentity.TenantId;
                user.EncryptedEntraObjectId = protectedIdentity.ObjectId;
                user.EntraIdentityLookupHash = protectedIdentity.LookupHash;
            }

            if (string.IsNullOrWhiteSpace(user.PersonnelNumber) &&
                normalizedPersonnelNumber is not null)
            {
                user.PersonnelNumber = normalizedPersonnelNumber;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                user.DisplayName = displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                user.Email = email.Trim();
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

        if (developmentRole == BcUserRole.HeadOfTutors)
        {
            await EnsureDevelopmentTutorHeadProfileAsync(user);
        }

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
            EffectiveRole(user),
            tutorProfileImagePath,
            user.PersonnelNumber);

        return principal;
    }

    private async Task EnsureDevelopmentTutorHeadProfileAsync(BcUser user)
    {
        bool hasTutorProfile = await _context.Tutors
            .AnyAsync(tutor => tutor.BcUserId == user.BcUserId);

        if (hasTutorProfile)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        _context.Tutors.Add(new Tutor
        {
            BcUserId = user.BcUserId,
            ProgrammeId = 1,
            OverallAverage = 80m,
            YearOfStudy = 3,
            ReasonForTutoring = "Development Tutor Head account.",
            TeachingStyle = "Supportive and practical.",
            PreviousTutoringExperience = "Development account experience.",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = "https://example.com/development-tutor-head",
            Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Biography = "Development Tutor Head account used to preview Tutor Head functionality.",
            IsActive = true
        });

        await _context.SaveChangesAsync();
    }

    private static string? GetStudentNumberFromPreferredUsername(
        string? preferredUsername)
    {
        if (string.IsNullOrWhiteSpace(preferredUsername))
        {
            return null;
        }

        const string studentDomain =
            "@student.belgiumcampus.ac.za";
        string normalizedUsername = preferredUsername.Trim();
        if (!normalizedUsername.EndsWith(
                studentDomain,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string studentNumber = normalizedUsername[..^studentDomain.Length];
        return studentNumber.Length > 0 &&
            studentNumber.All(char.IsAsciiDigit)
                ? studentNumber
                : null;
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

    private static BcUserRole EffectiveRole(BcUser user)
    {
        if (user.IsAdministrativeAccessActive || user.Role == BcUserRole.Dev)
        {
            return user.Role;
        }

        return user.Role == BcUserRole.HeadOfTutors
            ? BcUserRole.Tutor
            : BcUserRole.Student;
    }

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
