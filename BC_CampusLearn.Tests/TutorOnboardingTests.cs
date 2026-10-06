using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using BC_CampusLearn.Pages.Tutors;
using BC_CampusLearn.ViewComponents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorOnboardingTests
{
    [Theory]
    [InlineData(null, TutorStatus.Approved, true, BcUserRole.Tutor, true)]
    [InlineData("  ", TutorStatus.Approved, true, BcUserRole.Tutor, true)]
    [InlineData("My existing bio", TutorStatus.Approved, true, BcUserRole.Tutor, false)]
    [InlineData(null, TutorStatus.Pending, true, BcUserRole.Tutor, false)]
    [InlineData(null, TutorStatus.Approved, false, BcUserRole.Tutor, false)]
    [InlineData(null, TutorStatus.Approved, true, BcUserRole.Student, false)]
    [InlineData(null, TutorStatus.Approved, true, BcUserRole.HeadOfTutors, false)]
    public async Task ModalOnlyAppearsForIncompleteApprovedTutors(
        string? biography, TutorStatus status, bool active, BcUserRole role, bool expected)
    {
        await using var context = CreateContext();
        Tutor tutor = SeedTutor(context, biography, status, active);
        await context.SaveChangesAsync();
        var component = new TutorOnboardingViewComponent(context, new TestUser(role))
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext
                {
                    HttpContext = new DefaultHttpContext(),
                    RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                    ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                        new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                        new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                }
            }
        };

        var result = await component.InvokeAsync();

        if (expected)
        {
            var view = Assert.IsType<ViewViewComponentResult>(result);
            Assert.NotNull(view.ViewData);
            var input = Assert.IsType<TutorOnboardingInput>(view.ViewData.Model);
            Assert.Equal(tutor.PhoneNumber, input.PhoneNumber);
            Assert.Equal(tutor.PreferredTutoringMode, input.PreferredTutoringMode);
        }
        else Assert.IsType<ContentViewComponentResult>(result);
    }

    [Fact]
    public async Task SavingProfilePersistsDetailsAndStopsOnboarding()
    {
        await using var context = CreateContext();
        Tutor tutor = SeedTutor(context);
        tutor.GitHubUrl = "https://github.com/example";
        await context.SaveChangesAsync();
        var page = CreatePage(context);
        page.Input = new TutorOnboardingInput
        {
            Biography = "  I explain programming with practical examples.  ",
            PhoneNumber = "0821234567",
            PreferredTutoringMode = PreferredTutoringMode.Both
        };
        Assert.True(Validate(page));

        var result = Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));

        Assert.Equal("/Tutors/ManageAvailability", result.PageName);
        Assert.Equal("I explain programming with practical examples.", tutor.Biography);
        Assert.Equal("0821234567", tutor.PhoneNumber);
        Assert.Equal(PreferredTutoringMode.Both, tutor.PreferredTutoringMode);
        Assert.Equal("https://github.com/example", tutor.GitHubUrl);
        Assert.IsType<RedirectToPageResult>(await CreatePage(context).OnGetAsync(default));
    }

    [Fact]
    public async Task InvalidBioKeepsModalOpenWithoutSavingOtherDetails()
    {
        await using var context = CreateContext();
        Tutor tutor = SeedTutor(context);
        await context.SaveChangesAsync();
        var page = CreatePage(context);
        page.Request.Headers.Accept = "application/json";
        page.Input = new TutorOnboardingInput { Biography = "  ", PreferredTutoringMode = PreferredTutoringMode.Both };
        Validate(page);

        var result = Assert.IsType<JsonResult>(await page.OnPostAsync(default));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Null(tutor.Biography);
        Assert.Equal(PreferredTutoringMode.Online, tutor.PreferredTutoringMode);
    }

    [Theory]
    [InlineData(TutorStatus.Pending, true, BcUserRole.Tutor)]
    [InlineData(TutorStatus.Approved, false, BcUserRole.Tutor)]
    [InlineData(TutorStatus.Approved, true, BcUserRole.Student)]
    public async Task UnapprovedOrInactiveUsersCannotSave(TutorStatus status, bool active, BcUserRole role)
    {
        await using var context = CreateContext();
        Tutor tutor = SeedTutor(context, status: status, active: active);
        await context.SaveChangesAsync();
        var page = CreatePage(context, role);
        page.Input = new TutorOnboardingInput { Biography = "My bio", PreferredTutoringMode = PreferredTutoringMode.Online };

        Assert.IsType<ForbidResult>(await page.OnPostAsync(default));
        Assert.Null(tutor.Biography);
    }

    [Fact]
    public async Task StaleSubmissionCannotOverwriteCompletedProfile()
    {
        await using var context = CreateContext();
        Tutor tutor = SeedTutor(context, "Already completed");
        await context.SaveChangesAsync();
        var page = CreatePage(context);
        page.Input = new TutorOnboardingInput { Biography = "Stale tab", PreferredTutoringMode = PreferredTutoringMode.Both };

        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
        Assert.Equal("Already completed", tutor.Biography);
        Assert.Equal(PreferredTutoringMode.Online, tutor.PreferredTutoringMode);
    }

    [Fact]
    public async Task SpoofedPhotoIsRejectedBeforeSaving()
    {
        await using var context = CreateContext();
        Tutor tutor = SeedTutor(context);
        await context.SaveChangesAsync();
        var page = CreatePage(context);
        page.Input = new TutorOnboardingInput { Biography = "My bio", PreferredTutoringMode = PreferredTutoringMode.Online };
        using var stream = new MemoryStream("not an image"u8.ToArray());
        page.ProfileImage = new FormFile(stream, 0, stream.Length, "ProfileImage", "photo.png")
        {
            Headers = new HeaderDictionary(), ContentType = "image/png"
        };

        Assert.IsType<PageResult>(await page.OnPostAsync(default));
        Assert.False(page.ModelState.IsValid);
        Assert.Null(tutor.Biography);
        Assert.Null(tutor.ProfileImagePath);
    }

    [Theory]
    [InlineData("123", false)]
    [InlineData("0821234567", true)]
    [InlineData(null, true)]
    public void PhoneIsOptionalButMustBeValidWhenProvided(string? phone, bool expected)
    {
        var input = new TutorOnboardingInput { Biography = "My bio", PhoneNumber = phone, PreferredTutoringMode = PreferredTutoringMode.Online };
        Assert.Equal(expected, Validator.TryValidateObject(input, new ValidationContext(input), new List<ValidationResult>(), true));
    }

    [Fact]
    public async Task ValidPhotoIsSavedAlongsideTheBio()
    {
        await using var context = CreateContext();
        Tutor tutor = SeedTutor(context);
        await context.SaveChangesAsync();
        string root = Path.Combine(Path.GetTempPath(), $"campus-onboarding-{Guid.NewGuid():N}");
        var page = new OnboardingModel(context, new TestUser(BcUserRole.Tutor),
            new TestEnvironment { WebRootPath = root })
        { PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };
        page.Input = new TutorOnboardingInput { Biography = "I help students learn.", PreferredTutoringMode = PreferredTutoringMode.Online };
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ9sAAAAASUVORK5CYII=");
        using var stream = new MemoryStream(png);
        page.ProfileImage = new FormFile(stream, 0, stream.Length, "ProfileImage", "photo.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
        string? savedPath = null;
        try
        {
            Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(default));
            Assert.NotNull(tutor.ProfileImagePath);
            Assert.StartsWith("/uploads/tutor-profiles/1/", tutor.ProfileImagePath);
            savedPath = Path.Combine(root, tutor.ProfileImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(png, await File.ReadAllBytesAsync(savedPath));
        }
        finally
        {
            if (savedPath is not null && File.Exists(savedPath)) File.Delete(savedPath);
            foreach (string directory in new[] {
                Path.Combine(root, "uploads", "tutor-profiles", "1"),
                Path.Combine(root, "uploads", "tutor-profiles"), Path.Combine(root, "uploads"), root })
                if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task GuestDoesNotSeeOnboarding()
    {
        await using var context = CreateContext();
        var component = new TutorOnboardingViewComponent(context, new TestUser(BcUserRole.Tutor, false));
        Assert.IsType<ContentViewComponentResult>(await component.InvokeAsync());
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Tutor SeedTutor(ApplicationDbContext context, string? biography = null,
        TutorStatus status = TutorStatus.Approved, bool active = true)
    {
        var tutor = new Tutor
        {
            TutorId = 1, BcUserId = 1, ProgrammeId = 1,
            ReasonForTutoring = "Help others", TeachingStyle = "Practical examples",
            PreviousTutoringExperience = "Peer study", CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = "https://example.com/video", PreferredTutoringMode = PreferredTutoringMode.Online,
            PhoneNumber = "0821111111", Biography = biography, Status = status, IsActive = active
        };
        context.Tutors.Add(tutor);
        return tutor;
    }

    private static OnboardingModel CreatePage(ApplicationDbContext context, BcUserRole role = BcUserRole.Tutor) =>
        new(context, new TestUser(role), new TestEnvironment())
        { PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };

    private static bool Validate(OnboardingModel page)
    {
        var errors = new List<ValidationResult>();
        bool valid = Validator.TryValidateObject(page.Input, new ValidationContext(page.Input), errors, true);
        foreach (var error in errors)
            foreach (string member in error.MemberNames)
                page.ModelState.AddModelError($"Input.{member}", error.ErrorMessage!);
        return valid;
    }

    private sealed class TestUser(BcUserRole role, bool authenticated = true) : ICurrentUserService
    {
        public bool IsAuthenticated => authenticated;
        public CurrentUser GetRequiredUser() => new(1, "600001", "Test Tutor", null, role);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "BC_CampusLearn";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testing";
    }
}
