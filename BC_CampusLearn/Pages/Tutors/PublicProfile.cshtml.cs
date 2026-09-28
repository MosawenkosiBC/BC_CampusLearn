using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
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

    public string EmailAddress { get; private set; } = string.Empty;

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
            .Where(item =>
                item.BcUserId == currentUser.BcUserId &&
                item.Status == TutorStatus.Approved &&
                item.IsActive)
            .Select(item => new
            {
                item.BcUser.DisplayName,
                item.BcUser.PersonnelNumber,
                item.BcUser.Email,
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
            tutor.Email,
            tutor.ProfileImagePath,
            currentUser.DisplayName,
            currentUser.Email);

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
                item =>
                    item.BcUserId == currentUser.BcUserId &&
                    item.Status == TutorStatus.Approved &&
                    item.IsActive,
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
                tutor.BcUser.Email,
                tutor.ProfileImagePath,
                currentUser.DisplayName,
                currentUser.Email);
            return Page();
        }

        string? biography = NullIfWhiteSpace(Input.Biography);
        PreferredTutoringMode preferredMode =
            Input.PreferredTutoringMode!.Value;
        List<string> updatedFields = [];

        if (!string.Equals(tutor.Biography, biography, StringComparison.Ordinal))
        {
            updatedFields.Add("biography");
        }

        if (tutor.PreferredTutoringMode != preferredMode)
        {
            updatedFields.Add("tutoring preference");
        }

        if (!string.Equals(tutor.GitHubUrl, githubUrl, StringComparison.Ordinal))
        {
            updatedFields.Add("GitHub link");
        }

        if (!string.Equals(tutor.LinkedInUrl, linkedInUrl, StringComparison.Ordinal))
        {
            updatedFields.Add("LinkedIn link");
        }

        tutor.Biography = biography;
        tutor.PreferredTutoringMode = preferredMode;
        tutor.GitHubUrl = githubUrl;
        tutor.LinkedInUrl = linkedInUrl;
        tutor.UpdatedAt = DateTime.UtcNow;

        if (updatedFields.Count > 0)
        {
            _context.UserNotifications.Add(new UserNotification
            {
                RecipientBcUserId = tutor.BcUserId,
                Title = "Public profile updated",
                Message = $"Your public tutor profile was updated successfully. Updated: {FormatUpdatedFields(updatedFields)}. Students will now see this information when they view your profile.",
                LinkUrl = "/Tutors/PublicProfile",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

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
        string? storedEmail,
        string? profileImagePath,
        string currentDisplayName,
        string? currentEmail)
    {
        DisplayName = !string.IsNullOrWhiteSpace(storedDisplayName)
            ? storedDisplayName
            : !string.IsNullOrWhiteSpace(currentDisplayName)
                ? currentDisplayName
                : "Tutor";
        StudentNumber = personnelNumber;
        EmailAddress = !string.IsNullOrWhiteSpace(storedEmail)
            ? storedEmail.Trim()
            : currentEmail?.Trim() ?? string.Empty;
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

    private static string FormatUpdatedFields(IReadOnlyList<string> fields) =>
        fields.Count switch
        {
            1 => fields[0],
            2 => $"{fields[0]} and {fields[1]}",
            _ => $"{string.Join(", ", fields.Take(fields.Count - 1))}, and {fields[^1]}"
        };
}
