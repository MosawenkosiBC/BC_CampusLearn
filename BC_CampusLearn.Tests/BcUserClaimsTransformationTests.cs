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
