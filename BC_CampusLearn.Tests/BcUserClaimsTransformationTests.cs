using System.Security.Claims;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BC_CampusLearn.Tests;

public class BcUserClaimsTransformationTests
{
    [Theory]
    [InlineData("mzalaconnect@belgiumcampus.ac.za", false, true)]
    [InlineData("MZALACONNECT@BELGIUMCAMPUS.AC.ZA", true, true)]
    [InlineData("other@belgiumcampus.ac.za", false, false)]
    [InlineData("mzalaconnect@other.example", false, false)]
    public async Task OnlySharedAccountStoresReadableIdentifiersAfterSignIn(string email, bool existing, bool readable)
    {
        await using var context = CreateContext();
        var protector = CreateIdentityProtector();
        var transformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(Environments.Production), protector);
        if (existing)
        {
            await transformation.TransformAsync(CreatePrincipal());
            var original = await context.BcUsers.SingleAsync();
            original.Role = BcUserRole.SuperAdmin;
            await context.SaveChangesAsync();
        }

        ClaimsPrincipal SignIn()
        {
            var principal = CreatePrincipal();
            var identity = (ClaimsIdentity)principal.Identity!;
            identity.RemoveClaim(identity.FindFirst(ClaimTypes.Email)!);
            identity.AddClaim(new Claim(ClaimTypes.Email, email));
            return principal;
        }
        await transformation.TransformAsync(SignIn());
        var user = await context.BcUsers.SingleAsync();
        Assert.Equal(readable, Guid.TryParse(user.EncryptedEntraTenantId, out _));
        Assert.Equal(readable, Guid.TryParse(user.EncryptedEntraObjectId, out _));
        var stored = protector.UnprotectForUser(user);
        Assert.Equal("11111111-1111-1111-1111-111111111111", stored.ObjectId);
        Assert.Equal("22222222-2222-2222-2222-222222222222", stored.TenantId);
        Assert.Equal(user.EntraIdentityLookupHash, protector.CreateLookupHash(stored.TenantId, stored.ObjectId));
        string? objectBefore = user.EncryptedEntraObjectId;
        await transformation.TransformAsync(SignIn());
        Assert.Equal(objectBefore, user.EncryptedEntraObjectId);
        Assert.Single(context.BcUsers);
        if (existing) Assert.Equal(BcUserRole.SuperAdmin, user.Role);
    }

    [Fact]
    public void ReadableIdentifiersAreRejectedForOtherAccountsAndSnapshotsStayEncrypted()
    {
        var protector = CreateIdentityProtector();
        var user = new BcUser
        {
            Email = EntraIdentityProtector.SharedAccountEmail,
            EncryptedEntraTenantId = "22222222-2222-2222-2222-222222222222",
            EncryptedEntraObjectId = "11111111-1111-1111-1111-111111111111"
        };
        var identity = protector.UnprotectForUser(user);
        var snapshot = protector.Protect(identity.TenantId, identity.ObjectId);
        Assert.False(Guid.TryParse(snapshot.ObjectId, out _));
        Assert.Equal(identity, protector.Unprotect(snapshot.TenantId, snapshot.ObjectId));
        user.Email = "other@belgiumcampus.ac.za";
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() => protector.UnprotectForUser(user));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task StudentRefreshRecognisesTutorApproval(string environment)
    {
        await using var context = CreateContext();
        var transformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(environment), CreateIdentityProtector());
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.DevelopmentRole, nameof(BcUserRole.Student)));
        await transformation.TransformAsync(principal);
        var user = await context.BcUsers.SingleAsync();
        user.Role = BcUserRole.Tutor;
        context.Tutors.Add(new Tutor
        {
            BcUser = user, ProgrammeId = 1, CampusOfStudy = "Pretoria",
            ReasonForTutoring = "", TeachingStyle = "", PreviousTutoringExperience = "",
            DemonstrationVideoUrl = "", Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement, IsActive = true
        });
        await context.SaveChangesAsync();

        await transformation.TransformAsync(principal);
        await transformation.TransformAsync(principal);

        Assert.Equal(BcUserRole.Tutor, user.Role);
        Assert.True(principal.IsInRole(nameof(BcUserRole.Tutor)));
        Assert.False(principal.IsInRole(nameof(BcUserRole.Student)));
    }

    [Theory]
    [InlineData("Development", BcUserRole.Tutor)]
    [InlineData("Development", BcUserRole.SeniorTutor)]
    [InlineData("Development", BcUserRole.HeadOfTutors)]
    [InlineData("Production", BcUserRole.Tutor)]
    [InlineData("Production", BcUserRole.SeniorTutor)]
    [InlineData("Production", BcUserRole.HeadOfTutors)]
    public async Task DeregisteredTutorCannotRegainRoleFromExistingSignIn(string environment, BcUserRole role)
    {
        await using var context = CreateContext();
        var transformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(environment), CreateIdentityProtector());
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.DevelopmentRole, role.ToString()));
        await transformation.TransformAsync(principal);
        var user = await context.BcUsers.SingleAsync();
        user.Role = role;
        var tutor = await context.Tutors.SingleOrDefaultAsync();
        if (tutor is null)
        {
            tutor = new Tutor
            {
                BcUser = user, ProgrammeId = 1, CampusOfStudy = "Pretoria",
                ReasonForTutoring = "", TeachingStyle = "", PreviousTutoringExperience = "", DemonstrationVideoUrl = ""
            };
            context.Tutors.Add(tutor);
        }
        tutor.Status = TutorStatus.Deregistered;
        tutor.IsActive = false;
        await context.SaveChangesAsync();

        // Reuse the signed-in principal, including its previous application role claims.
        await transformation.TransformAsync(principal);
        await transformation.TransformAsync(principal);

        Assert.Equal(BcUserRole.Student, (await context.BcUsers.SingleAsync()).Role);
        Assert.True(principal.IsInRole(nameof(BcUserRole.Student)));
        Assert.False(principal.IsInRole(nameof(BcUserRole.Tutor)));
        Assert.False(principal.IsInRole(nameof(BcUserRole.SeniorTutor)));
        Assert.False(principal.IsInRole(nameof(BcUserRole.HeadOfTutors)));
        Assert.False((await context.Tutors.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task NewUsersDefaultToStudentRole()
    {
        await using ApplicationDbContext context = CreateContext();
        var transformation = new BcUserClaimsTransformation(
            context,
            new TestWebHostEnvironment(Environments.Production),
            CreateIdentityProtector());
        ClaimsPrincipal principal = CreatePrincipal();

        await transformation.TransformAsync(principal);

        BcUser user = await context.BcUsers.SingleAsync();
        Assert.Equal(BcUserRole.Student, user.Role);
        Assert.True(principal.IsInRole(nameof(BcUserRole.Student)));
        Assert.Empty(context.Admins);
    }

    [Fact]
    public async Task DevelopmentAdminLoginCreatesAdminProfileAndRoleClaim()
    {
        await using ApplicationDbContext context = CreateContext();
        var transformation = new BcUserClaimsTransformation(
            context,
            new TestWebHostEnvironment(Environments.Development),
            CreateIdentityProtector());
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(
                EntraClaimTypes.DevelopmentRole,
                nameof(BcUserRole.Admin)));

        await transformation.TransformAsync(principal);

        BcUser user = await context.BcUsers.SingleAsync();
        Admin admin = await context.Admins.SingleAsync();
        Assert.Equal(BcUserRole.Admin, user.Role);
        Assert.Equal(user.BcUserId, admin.BcUserId);
        Assert.True(principal.IsInRole(nameof(BcUserRole.Admin)));
    }

    [Fact]
    public async Task DevelopmentTutorHeadLoginCreatesActiveTutorProfileAndRoleClaim()
    {
        await using ApplicationDbContext context = CreateContext();
        var transformation = new BcUserClaimsTransformation(
            context,
            new TestWebHostEnvironment(Environments.Development),
            CreateIdentityProtector());
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(
                EntraClaimTypes.DevelopmentRole,
                nameof(BcUserRole.HeadOfTutors)));

        await transformation.TransformAsync(principal);

        BcUser user = await context.BcUsers.SingleAsync();
        Tutor tutor = await context.Tutors.SingleAsync();
        Assert.Equal(BcUserRole.HeadOfTutors, user.Role);
        Assert.Equal(user.BcUserId, tutor.BcUserId);
        Assert.Equal(TutorStatus.Approved, tutor.Status);
        Assert.True(tutor.IsActive);
        Assert.True(principal.IsInRole(nameof(BcUserRole.HeadOfTutors)));
    }

    [Fact]
    public async Task StudentNumberFallsBackToNumericPreferredUsername()
    {
        await using ApplicationDbContext context = CreateContext();
        var transformation = new BcUserClaimsTransformation(
            context,
            new TestWebHostEnvironment(Environments.Production),
            CreateIdentityProtector());
        ClaimsPrincipal principal = CreatePrincipalWithoutPersonnelNumber(
            "601334@student.belgiumcampus.ac.za");

        await transformation.TransformAsync(principal);

        BcUser user = await context.BcUsers.SingleAsync();
        Assert.Equal("601334", user.PersonnelNumber);
        Assert.Equal(
            "601334@student.belgiumcampus.ac.za",
            user.Email);
        Assert.Equal(
            "601334",
            principal.FindFirstValue(
                EntraClaimTypes.PersonnelNumber));
    }

    [Fact]
    public async Task DerivedStudentNumberRemainsAvailableOnRepeatedTransformation()
    {
        await using ApplicationDbContext context = CreateContext();
        var transformation = new BcUserClaimsTransformation(
            context,
            new TestWebHostEnvironment(Environments.Production),
            CreateIdentityProtector());
        ClaimsPrincipal principal = CreatePrincipalWithoutPersonnelNumber(
            "601334@student.belgiumcampus.ac.za");

        await transformation.TransformAsync(principal);
        await transformation.TransformAsync(principal);

        Assert.Equal(
            "601334",
            principal.FindFirstValue(
                EntraClaimTypes.PersonnelNumber));
        Assert.Single(context.BcUsers);
    }

    [Theory]
    [InlineData("surname.i@belgiumcampus.ac.za")]
    [InlineData("student.name@student.belgiumcampus.ac.za")]
    [InlineData("601334@external.example")]
    public async Task NonStudentPreferredUsernameCreatesUserWithoutPersonnelNumber(
        string preferredUsername)
    {
        await using ApplicationDbContext context = CreateContext();
        var transformation = new BcUserClaimsTransformation(
            context,
            new TestWebHostEnvironment(Environments.Production),
            CreateIdentityProtector());
        ClaimsPrincipal principal = CreatePrincipalWithoutPersonnelNumber(
            preferredUsername);

        await transformation.TransformAsync(principal);

        BcUser user = await context.BcUsers.SingleAsync();
        Assert.Null(user.PersonnelNumber);
        Assert.NotNull(user.EncryptedEntraObjectId);
        Assert.NotNull(user.EncryptedEntraTenantId);
        Assert.NotEqual(
            "11111111-1111-1111-1111-111111111111",
            user.EncryptedEntraObjectId);
        Assert.NotEqual(
            "22222222-2222-2222-2222-222222222222",
            user.EncryptedEntraTenantId);
        Assert.Equal(64, user.EntraIdentityLookupHash?.Length);
    }

    [Fact]
    public async Task ExistingAdministratorWithoutPersonnelNumberLinksByEmailOnce()
    {
        await using ApplicationDbContext context = CreateContext();
        context.BcUsers.Add(new BcUser
        {
            PersonnelNumber = null,
            DisplayName = "Campus Administrator",
            Email = "admin@belgiumcampus.ac.za",
            Role = BcUserRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        int existingUserId = (await context.BcUsers.SingleAsync()).BcUserId;
        var transformation = new BcUserClaimsTransformation(
            context,
            new TestWebHostEnvironment(Environments.Production),
            CreateIdentityProtector());
        ClaimsPrincipal principal = CreatePrincipalWithoutPersonnelNumber(
            "admin@belgiumcampus.ac.za");

        await transformation.TransformAsync(principal);

        BcUser user = await context.BcUsers.SingleAsync();
        Assert.Equal(existingUserId, user.BcUserId);
        Assert.Equal(BcUserRole.Admin, user.Role);
        Assert.NotNull(user.EntraIdentityLookupHash);
        Assert.True(principal.IsInRole(nameof(BcUserRole.Admin)));
    }

    [Theory]
    [InlineData("Development", BcUserRole.Admin, BcUserRole.Student)]
    [InlineData("Development", BcUserRole.Admin, BcUserRole.Tutor)]
    [InlineData("Development", BcUserRole.HeadOfTutors, BcUserRole.Student)]
    [InlineData("Development", BcUserRole.HeadOfTutors, BcUserRole.Tutor)]
    [InlineData("Development", BcUserRole.SuperAdmin, BcUserRole.Admin)]
    [InlineData("Production", BcUserRole.Admin, BcUserRole.Student)]
    [InlineData("Production", BcUserRole.Admin, BcUserRole.Tutor)]
    [InlineData("Production", BcUserRole.HeadOfTutors, BcUserRole.Student)]
    [InlineData("Production", BcUserRole.HeadOfTutors, BcUserRole.Tutor)]
    [InlineData("Production", BcUserRole.SuperAdmin, BcUserRole.Admin)]
    public async Task RefreshAndSignInRespectSavedDemotion(string environment, BcUserRole previous, BcUserRole next)
    {
        await using var context = CreateContext();
        var transformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(environment), CreateIdentityProtector());
        var principal = CreatePrincipal(new Claim(EntraClaimTypes.DevelopmentRole, previous.ToString()));
        await transformation.TransformAsync(principal);
        var user = await context.BcUsers.SingleAsync();
        user.Role = previous;
        await context.SaveChangesAsync();
        await transformation.TransformAsync(principal);
        Assert.True(principal.IsInRole(previous.ToString()));

        user.Role = next;
        await context.SaveChangesAsync();
        await transformation.TransformAsync(principal);
        await transformation.TransformAsync(principal);

        Assert.False(principal.IsInRole(previous.ToString()));
        Assert.True(principal.IsInRole(next.ToString()));
        Assert.Equal(next.ToString(), principal.FindFirstValue(EntraClaimTypes.BcRole));
        Assert.Equal(next, (await context.BcUsers.SingleAsync()).Role);

        var newSignIn = CreatePrincipal(new Claim(EntraClaimTypes.DevelopmentRole, previous.ToString()));
        await transformation.TransformAsync(newSignIn);
        Assert.False(newSignIn.IsInRole(previous.ToString()));
        Assert.True(newSignIn.IsInRole(next.ToString()));
        Assert.Equal(next, (await context.BcUsers.SingleAsync()).Role);
    }

    [Fact]
    public async Task SignInAfterLostEncryptionKeysRepairsIdentityAndPreservesTutorAccount()
    {
        await using ApplicationDbContext context = CreateContext();
        IEntraIdentityProtector oldProtector = CreateIdentityProtector();
        var oldTransformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(Environments.Production), oldProtector);
        await oldTransformation.TransformAsync(CreatePrincipal());
        BcUser user = await context.BcUsers.SingleAsync();
        user.Role = BcUserRole.Tutor;
        var tutor = new Tutor
        {
            BcUser = user, ProgrammeId = 1, CampusOfStudy = "Pretoria",
            ReasonForTutoring = "", TeachingStyle = "", PreviousTutoringExperience = "",
            DemonstrationVideoUrl = "", Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement, IsActive = true
        };
        context.Tutors.Add(tutor);
        await context.SaveChangesAsync();
        int userId = user.BcUserId;
        int tutorId = tutor.TutorId;
        string? originalHash = user.EntraIdentityLookupHash;
        string originalTenant = user.EncryptedEntraTenantId!;
        string originalObject = user.EncryptedEntraObjectId!;

        // A recreated container has a different key ring but the same lookup key.
        IEntraIdentityProtector currentProtector = CreateIdentityProtector();
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
            currentProtector.Unprotect(originalTenant, originalObject));
        var transformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(Environments.Production), currentProtector);
        ClaimsPrincipal newSignIn = CreatePrincipal();
        await transformation.TransformAsync(newSignIn);
        context.ChangeTracker.Clear();

        BcUser repaired = await context.BcUsers.SingleAsync();
        Assert.Equal(userId, repaired.BcUserId);
        Assert.Equal(originalHash, repaired.EntraIdentityLookupHash);
        Assert.Equal(BcUserRole.Tutor, repaired.Role);
        Assert.True(newSignIn.IsInRole(nameof(BcUserRole.Tutor)));
        Assert.Equal(tutorId, (await context.Tutors.SingleAsync()).TutorId);
        Assert.Equal(new EntraIdentity("22222222-2222-2222-2222-222222222222",
            "11111111-1111-1111-1111-111111111111"), currentProtector.Unprotect(
                repaired.EncryptedEntraTenantId!, repaired.EncryptedEntraObjectId!));
    }

    [Theory]
    [InlineData("missing-tenant")]
    [InlineData("missing-object")]
    [InlineData("corrupt")]
    [InlineData("different-encrypted-identity")]
    public async Task VerifiedMatchingSignInRepairsUnusableStoredIdentity(string scenario)
    {
        await using ApplicationDbContext context = CreateContext();
        IEntraIdentityProtector protector = CreateIdentityProtector();
        var transformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(Environments.Production), protector);
        await transformation.TransformAsync(CreatePrincipal());
        BcUser user = await context.BcUsers.SingleAsync();
        string hash = user.EntraIdentityLookupHash!;
        switch (scenario)
        {
            case "missing-tenant": user.EncryptedEntraTenantId = null; break;
            case "missing-object": user.EncryptedEntraObjectId = null; break;
            case "corrupt": user.EncryptedEntraObjectId = "unreadable"; break;
            case "different-encrypted-identity":
                user.EncryptedEntraObjectId = protector.Protect(
                    "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333").ObjectId;
                break;
        }
        await context.SaveChangesAsync();
        await transformation.TransformAsync(CreatePrincipal());

        EntraIdentity repaired = protector.Unprotect(user.EncryptedEntraTenantId!, user.EncryptedEntraObjectId!);
        Assert.Equal(hash, protector.CreateLookupHash(repaired.TenantId, repaired.ObjectId));
        Assert.Single(context.BcUsers);
    }

    [Fact]
    public async Task HealthyIdentityIsNotReencryptedOnEveryRequest()
    {
        await using ApplicationDbContext context = CreateContext();
        var transformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(Environments.Production), CreateIdentityProtector());
        await transformation.TransformAsync(CreatePrincipal());
        BcUser user = await context.BcUsers.SingleAsync();
        string? encryptedTenant = user.EncryptedEntraTenantId;
        string? encryptedObject = user.EncryptedEntraObjectId;
        await transformation.TransformAsync(CreatePrincipal());
        Assert.Equal(encryptedTenant, user.EncryptedEntraTenantId);
        Assert.Equal(encryptedObject, user.EncryptedEntraObjectId);
    }

    [Fact]
    public async Task DifferentMicrosoftIdentityCannotRepairAnAccountUsingItsPersonnelNumberOrEmail()
    {
        await using ApplicationDbContext context = CreateContext();
        var originalTransformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(Environments.Production), CreateIdentityProtector());
        await originalTransformation.TransformAsync(CreatePrincipal());
        BcUser user = await context.BcUsers.SingleAsync();
        string? encryptedObject = user.EncryptedEntraObjectId;
        string? hash = user.EntraIdentityLookupHash;

        ClaimsPrincipal otherIdentity = CreatePrincipal();
        var claims = (ClaimsIdentity)otherIdentity.Identity!;
        claims.RemoveClaim(claims.FindFirst(EntraClaimTypes.ObjectId)!);
        claims.AddClaim(new Claim(EntraClaimTypes.ObjectId, "33333333-3333-3333-3333-333333333333"));
        var recoveryTransformation = new BcUserClaimsTransformation(context,
            new TestWebHostEnvironment(Environments.Production), CreateIdentityProtector());
        await Assert.ThrowsAsync<InvalidOperationException>(() => recoveryTransformation.TransformAsync(otherIdentity));
        Assert.Equal(encryptedObject, user.EncryptedEntraObjectId);
        Assert.Equal(hash, user.EntraIdentityLookupHash);
        Assert.Single(context.BcUsers);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new(EntraClaimTypes.ObjectId, "11111111-1111-1111-1111-111111111111"),
            new(EntraClaimTypes.TenantId, "22222222-2222-2222-2222-222222222222"),
            new(EntraClaimTypes.PersonnelNumber, "TEST-0001"),
            new(ClaimTypes.Name, "Test User"),
            new(ClaimTypes.Email, "test.user@belgiumcampus.ac.za")
        };
        claims.AddRange(extraClaims);

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "Test"));
    }

    private static ClaimsPrincipal CreatePrincipalWithoutPersonnelNumber(
        string preferredUsername)
    {
        Claim[] claims =
        [
            new Claim(
                EntraClaimTypes.ObjectId,
                "11111111-1111-1111-1111-111111111111"),
            new Claim(
                EntraClaimTypes.TenantId,
                "22222222-2222-2222-2222-222222222222"),
            new Claim(ClaimTypes.Name, "Test Student"),
            new Claim(
                EntraClaimTypes.PreferredUsername,
                preferredUsername)
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "Test"));
    }

    private static IEntraIdentityProtector CreateIdentityProtector() =>
        new EntraIdentityProtector(
            new EphemeralDataProtectionProvider(),
            Options.Create(new IdentityProtectionOptions
            {
                LookupKey = Convert.ToBase64String(new byte[32])
            }));

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; }
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
