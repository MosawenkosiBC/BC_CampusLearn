using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

[Authorize(Roles = nameof(BcUserRole.Tutor))]
public class OnboardingModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    IWebHostEnvironment environment) : PageModel
{
    [BindProperty]
    public TutorOnboardingInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? ProfileImage { get; set; }

    private Task<Tutor?> FindTutorAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        if (currentUser.Role != BcUserRole.Tutor) return Task.FromResult<Tutor?>(null);
        int userId = currentUser.BcUserId;
        return context.Tutors.SingleOrDefaultAsync(t =>
            t.BcUserId == userId && t.Status == TutorStatus.Approved && t.IsActive,
            cancellationToken);
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Tutor? tutor = await FindTutorAsync(cancellationToken);
        if (tutor is null) return Forbid();
        if (!string.IsNullOrWhiteSpace(tutor.Biography))
            return RedirectToPage("/Tutors/ManageAvailability");
        Input.PhoneNumber = tutor.PhoneNumber;
        Input.PreferredTutoringMode = tutor.PreferredTutoringMode;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Tutor? tutor = await FindTutorAsync(cancellationToken);
        if (tutor is null) return Forbid();
        // A second submission from an older tab must not overwrite a completed profile.
        if (!string.IsNullOrWhiteSpace(tutor.Biography)) return Completed();

        if (string.IsNullOrWhiteSpace(Input.Biography))
            ModelState.AddModelError("Input.Biography", "Write a short bio to introduce yourself to students.");

        string? extension = null;
        if (ProfileImage is { Length: > 0 })
        {
            if (ProfileImage.Length > 5 * 1024 * 1024)
                ModelState.AddModelError(nameof(ProfileImage), "Choose a profile photo up to 5 MB.");
            else
            {
                extension = ProfileImage.ContentType.ToLowerInvariant() switch
                {
                    "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => null
                };
                if (extension is null || !await ProfileModel.HasValidImageHeaderAsync(
                    ProfileImage, extension, cancellationToken))
                    ModelState.AddModelError(nameof(ProfileImage), "Choose a JPG, PNG or WebP image.");
            }
        }

        if (!ModelState.IsValid)
        {
            if (WantsJson)
                return new JsonResult(new
                {
                    errors = ModelState.Values.SelectMany(v => v.Errors)
                        .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Check the details you entered." : e.ErrorMessage)
                        .Distinct().ToArray()
                }) { StatusCode = StatusCodes.Status400BadRequest };
            return Page();
        }

        string? filePath = null;
        try
        {
            if (ProfileImage is { Length: > 0 } && extension is not null)
            {
                string directory = Path.Combine(environment.WebRootPath, "uploads", "tutor-profiles", tutor.TutorId.ToString());
                Directory.CreateDirectory(directory);
                string fileName = $"{Guid.NewGuid():N}{extension}";
                filePath = Path.Combine(directory, fileName);
                await using (var destination = new FileStream(filePath, FileMode.CreateNew))
                    await ProfileImage.CopyToAsync(destination, cancellationToken);
                tutor.ProfileImagePath = $"/uploads/tutor-profiles/{tutor.TutorId}/{fileName}";
            }
            tutor.Biography = Input.Biography!.Trim();
            tutor.PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();
            tutor.PreferredTutoringMode = Input.PreferredTutoringMode!.Value;
            tutor.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (filePath is not null && System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            throw;
        }
        return Completed();
    }

    private bool WantsJson => Request.Headers.Accept.Any(value =>
        value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    private IActionResult Completed() => WantsJson
        ? new JsonResult(new { nextUrl = Url.Page("/Tutors/ManageAvailability") })
        : RedirectToPage("/Tutors/ManageAvailability");
}
