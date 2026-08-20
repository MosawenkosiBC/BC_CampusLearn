using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

public class PublicProfileModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PublicProfileModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public string DisplayName { get; private set; } = string.Empty;

    public string Initials { get; private set; } = string.Empty;

    public string StudentNumber { get; private set; } = string.Empty;

    public string? ProfileImagePath { get; private set; }

    [BindProperty]
    public TutorPublicProfileInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        var tutor = await _context.Tutors
            .AsNoTracking()
            .Where(item => item.BcUserId == currentUser.BcUserId)
            .Select(item => new
            {
                item.BcUser.DisplayName,
                item.BcUser.PersonnelNumber,
                item.ProfileImagePath,
                item.Biography,
                item.PreferredTutoringMode,
                item.GitHubUrl,
                item.LinkedInUrl
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return Forbid();
        }

        SetIdentity(
            tutor.DisplayName,
            tutor.PersonnelNumber,
            tutor.ProfileImagePath,
            currentUser.DisplayName);

        Input = new TutorPublicProfileInput
        {
            Biography = tutor.Biography,
            PreferredTutoringMode = tutor.PreferredTutoringMode,
            GitHubUrl = tutor.GitHubUrl,
            LinkedInUrl = tutor.LinkedInUrl
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        var tutor = await _context.Tutors
            .Include(item => item.BcUser)
            .SingleOrDefaultAsync(
                item => item.BcUserId == currentUser.BcUserId,
                cancellationToken);

        if (tutor is null)
        {
            return Forbid();
        }

        string? githubUrl = ValidateAndNormalizeUrl(
            Input.GitHubUrl,
            "Input.GitHubUrl",
            "GitHub URL");
        string? linkedInUrl = ValidateAndNormalizeUrl(
            Input.LinkedInUrl,
            "Input.LinkedInUrl",
            "LinkedIn URL");

        if (!ModelState.IsValid)
        {
            SetIdentity(
                tutor.BcUser.DisplayName,
                tutor.BcUser.PersonnelNumber,
                tutor.ProfileImagePath,
                currentUser.DisplayName);
            return Page();
        }

        tutor.Biography = NullIfWhiteSpace(Input.Biography);
        tutor.PreferredTutoringMode = Input.PreferredTutoringMode!.Value;
        tutor.GitHubUrl = githubUrl;
        tutor.LinkedInUrl = linkedInUrl;
        tutor.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        TempData["PublicProfileSaved"] = true;
        return RedirectToPage();
    }

    private string? ValidateAndNormalizeUrl(
        string? value,
        string modelKey,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            ModelState.AddModelError(
                modelKey,
                $"Enter a valid {displayName} beginning with http:// or https://.");
            return null;
        }

        return uri.AbsoluteUri;
    }

    private void SetIdentity(
        string storedDisplayName,
        string personnelNumber,
        string? profileImagePath,
        string currentDisplayName)
    {
        DisplayName = !string.IsNullOrWhiteSpace(storedDisplayName)
            ? storedDisplayName
            : !string.IsNullOrWhiteSpace(currentDisplayName)
                ? currentDisplayName
                : "Tutor";
        StudentNumber = personnelNumber;
        ProfileImagePath = profileImagePath;

        string[] nameParts = DisplayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        Initials = nameParts.Length switch
        {
            > 1 => $"{nameParts[0][0]}{nameParts[^1][0]}"
                .ToUpperInvariant(),
            1 => nameParts[0][..1].ToUpperInvariant(),
            _ => "T"
        };
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
