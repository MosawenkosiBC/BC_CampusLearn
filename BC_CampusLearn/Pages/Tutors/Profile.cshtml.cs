using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Tutors;

public class ProfileModel : PageModel
{
    private const long MaximumProfileImageSize = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;

    public ProfileModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _currentUserService = currentUserService;
        _environment = environment;
    }

    public string DisplayName { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string Surname { get; private set; } = string.Empty;

    public string Initials { get; private set; } = string.Empty;

    public string StudentNumber { get; private set; } = string.Empty;

    public string EmailAddress { get; private set; } = string.Empty;

    public int YearOfStudy { get; private set; }

    public string ProgramOfStudy { get; private set; } = string.Empty;

    public string? ProfileImagePath { get; private set; }

    public string PhoneNumber { get; private set; } = string.Empty;

    [BindProperty]
    public IFormFile? ProfileImage { get; set; }

    [BindProperty]
    public TutorPhoneNumberInput PhoneInput { get; set; } = new();

    [BindProperty]
    public TutorModuleChangeRequestInput ModuleRequestInput { get; set; } = new();

    public bool OpenModuleRequestModal { get; private set; }

    public IReadOnlyList<TutorModuleOptionViewModel> ModulesAllowedToTutor { get; private set; }
        = Array.Empty<TutorModuleOptionViewModel>();

    public IReadOnlyList<TutorModuleOptionViewModel> ModulesAvailableToAdd { get; private set; }
        = Array.Empty<TutorModuleOptionViewModel>();

    public IReadOnlyList<TutorPendingModuleRequestViewModel> PendingModuleRequests { get; private set; }
        = Array.Empty<TutorPendingModuleRequestViewModel>();

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        bool loaded = await LoadProfileAsync(cancellationToken);
        if (!loaded)
        {
            return Forbid();
        }

        PhoneInput.PhoneNumber = PhoneNumber;
        ModuleRequestInput.RequestType = TutorModuleChangeRequestType.Add;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdatePhoneAsync(CancellationToken cancellationToken)
    {
        RemoveModelStateEntriesExcept(nameof(PhoneInput));

        if (!ModelState.IsValid)
        {
            return await ReloadPageAsync(cancellationToken, populatePhoneInput: true);
        }

        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        Tutor? tutor = await _context.Tutors.SingleOrDefaultAsync(
            item => item.BcUserId == currentUser.BcUserId,
            cancellationToken);
        if (tutor is null)
        {
            return Forbid();
        }

        tutor.PhoneNumber = PhoneInput.PhoneNumber!.Trim();
        tutor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        TempData["PhoneNumberSaved"] = true;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRequestModuleChangeAsync(CancellationToken cancellationToken)
    {
        RemoveModelStateEntriesExcept(nameof(ModuleRequestInput));
        OpenModuleRequestModal = true;

        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        var tutor = await _context.Tutors
            .Where(item => item.BcUserId == currentUser.BcUserId)
            .Select(item => new { item.TutorId, item.ProgrammeId })
            .SingleOrDefaultAsync(cancellationToken);
        if (tutor is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return await ReloadPageAsync(cancellationToken, populatePhoneInput: true);
        }

        int moduleId = ModuleRequestInput.ProgrammeModuleId!.Value;
        TutorModuleChangeRequestType requestType = ModuleRequestInput.RequestType!.Value;
        bool belongsToProgramme = await _context.ProgrammeModules.AnyAsync(
            module => module.ProgrammeModuleId == moduleId && module.ProgrammeId == tutor.ProgrammeId,
            cancellationToken);
        bool currentlyAssigned = await _context.TutorCourseModules.AnyAsync(
            assignment => assignment.TutorId == tutor.TutorId &&
                assignment.ProgrammeModuleId == moduleId,
            cancellationToken);

        if (!belongsToProgramme)
        {
            ModelState.AddModelError(
                "ModuleRequestInput.ProgrammeModuleId",
                "Select a module from your programme.");
        }
        else if (requestType == TutorModuleChangeRequestType.Add && currentlyAssigned)
        {
            ModelState.AddModelError(
                "ModuleRequestInput.ProgrammeModuleId",
                "You are already approved to tutor this module.");
        }
        else if (requestType == TutorModuleChangeRequestType.Remove && !currentlyAssigned)
        {
            ModelState.AddModelError(
                "ModuleRequestInput.ProgrammeModuleId",
                "You are not currently assigned to this module.");
        }

        bool duplicatePendingRequest = await _context.TutorModuleChangeRequests.AnyAsync(
            request => request.TutorId == tutor.TutorId &&
                request.ProgrammeModuleId == moduleId &&
                request.Status == TutorAccountRequestStatus.Pending,
            cancellationToken);
        if (duplicatePendingRequest)
        {
            ModelState.AddModelError(
                "ModuleRequestInput.ProgrammeModuleId",
                "A change request for this module is already pending.");
        }

        if (!ModelState.IsValid)
        {
            return await ReloadPageAsync(cancellationToken, populatePhoneInput: true);
        }

        _context.TutorModuleChangeRequests.Add(new TutorModuleChangeRequest
        {
            TutorId = tutor.TutorId,
            ProgrammeModuleId = moduleId,
            RequestType = requestType,
            Status = TutorAccountRequestStatus.Pending,
            Reason = string.IsNullOrWhiteSpace(ModuleRequestInput.Reason)
                ? null
                : ModuleRequestInput.Reason.Trim(),
            SubmittedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);

        TempData["ModuleRequestSaved"] = true;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUploadProfileImageAsync(
        string? returnPage,
        CancellationToken cancellationToken)
    {
        if (ProfileImage is null || ProfileImage.Length == 0)
        {
            TempData["ProfileImageError"] = "Choose an image to upload.";
            return RedirectAfterProfileImageUpload(returnPage);
        }

        if (ProfileImage.Length > MaximumProfileImageSize)
        {
            TempData["ProfileImageError"] =
                "The profile image must be smaller than 5 MB.";
            return RedirectAfterProfileImageUpload(returnPage);
        }

        string? extension = ProfileImage.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };

        if (extension is null ||
            !await HasValidImageHeaderAsync(
                ProfileImage,
                extension,
                cancellationToken))
        {
            TempData["ProfileImageError"] =
                "Choose a JPG, PNG, or WebP image.";
            return RedirectAfterProfileImageUpload(returnPage);
        }

        CurrentUser currentUser =
            _currentUserService.GetRequiredUser();

        var tutor = await _context.Tutors
            .SingleOrDefaultAsync(
                item => item.BcUserId == currentUser.BcUserId,
                cancellationToken);

        if (tutor is null)
        {
            return Forbid();
        }

        string relativeDirectory = Path.Combine(
            "uploads",
            "tutor-profiles",
            tutor.TutorId.ToString());
        string uploadDirectory = Path.Combine(
            _environment.WebRootPath,
            relativeDirectory);

        Directory.CreateDirectory(uploadDirectory);

        string fileName = $"{Guid.NewGuid():N}{extension}";
        string filePath = Path.Combine(uploadDirectory, fileName);

        await using (FileStream destination = new(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            await ProfileImage.CopyToAsync(
                destination,
                cancellationToken);
        }

        tutor.ProfileImagePath =
            $"/{relativeDirectory.Replace('\\', '/')}/{fileName}";
        tutor.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return RedirectAfterProfileImageUpload(returnPage);
    }

    private IActionResult RedirectAfterProfileImageUpload(
        string? returnPage)
    {
        return !string.IsNullOrWhiteSpace(returnPage) &&
            Url.IsLocalUrl(returnPage)
                ? LocalRedirect(returnPage)
                : RedirectToPage();
    }

    private async Task<IActionResult> ReloadPageAsync(
        CancellationToken cancellationToken,
        bool populatePhoneInput = false)
    {
        if (!await LoadProfileAsync(cancellationToken))
        {
            return Forbid();
        }

        if (populatePhoneInput)
        {
            PhoneInput.PhoneNumber = PhoneNumber;
        }

        return Page();
    }

    private async Task<bool> LoadProfileAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        var tutor = await _context.Tutors
            .AsNoTracking()
            .Where(item => item.BcUserId == currentUser.BcUserId)
            .Select(item => new
            {
                item.TutorId,
                item.ProgrammeId,
                item.BcUser.DisplayName,
                item.BcUser.PersonnelNumber,
                item.BcUser.Email,
                item.YearOfStudy,
                ProgramOfStudy = item.Programme.Name,
                item.ProfileImagePath,
                item.PhoneNumber,
                Modules = item.TutorCourseModules
                    .OrderBy(module => module.ProgrammeModule.ModuleName)
                    .Select(module => new TutorModuleOptionViewModel
                    {
                        ProgrammeModuleId = module.ProgrammeModuleId,
                        ModuleName = module.ProgrammeModule.ModuleName
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (tutor is null)
        {
            return false;
        }

        DisplayName = !string.IsNullOrWhiteSpace(tutor.DisplayName)
            ? tutor.DisplayName
            : !string.IsNullOrWhiteSpace(currentUser.DisplayName)
                ? currentUser.DisplayName
                : "Tutor";
        StudentNumber = tutor.PersonnelNumber;
        EmailAddress = tutor.Email ?? string.Empty;
        YearOfStudy = tutor.YearOfStudy;
        ProgramOfStudy = tutor.ProgramOfStudy;
        ProfileImagePath = tutor.ProfileImagePath;
        PhoneNumber = tutor.PhoneNumber ?? string.Empty;
        ModulesAllowedToTutor = tutor.Modules;

        int[] assignedModuleIds = tutor.Modules
            .Select(module => module.ProgrammeModuleId)
            .ToArray();
        ModulesAvailableToAdd = await _context.ProgrammeModules
            .AsNoTracking()
            .Where(module => module.ProgrammeId == tutor.ProgrammeId &&
                !assignedModuleIds.Contains(module.ProgrammeModuleId))
            .OrderBy(module => module.ModuleName)
            .Select(module => new TutorModuleOptionViewModel
            {
                ProgrammeModuleId = module.ProgrammeModuleId,
                ModuleName = module.ModuleName
            })
            .ToListAsync(cancellationToken);

        PendingModuleRequests = await _context.TutorModuleChangeRequests
            .AsNoTracking()
            .Where(request => request.TutorId == tutor.TutorId &&
                request.Status == TutorAccountRequestStatus.Pending)
            .OrderByDescending(request => request.SubmittedAt)
            .Select(request => new TutorPendingModuleRequestViewModel
            {
                ModuleName = request.ProgrammeModule.ModuleName,
                RequestType = request.RequestType
            })
            .ToListAsync(cancellationToken);

        string[] nameParts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        FirstName = nameParts.FirstOrDefault() ?? DisplayName;
        Surname = nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : string.Empty;
        Initials = nameParts.Length > 1
            ? $"{nameParts[0][0]}{nameParts[^1][0]}".ToUpperInvariant()
            : nameParts.Length == 1 ? nameParts[0][..1].ToUpperInvariant() : "T";

        return true;
    }

    private void RemoveModelStateEntriesExcept(string prefix)
    {
        foreach (string key in ModelState.Keys
            .Where(key => !key.StartsWith(prefix, StringComparison.Ordinal))
            .ToList())
        {
            ModelState.Remove(key);
        }
    }

    private static async Task<bool> HasValidImageHeaderAsync(
        IFormFile image,
        string extension,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[12];

        await using Stream stream = image.OpenReadStream();
        int bytesRead = await stream.ReadAsync(
            header.AsMemory(0, header.Length),
            cancellationToken);

        return extension switch
        {
            ".jpg" => bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF,
            ".png" => bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(
                    new byte[]
                    {
                        0x89, 0x50, 0x4E, 0x47,
                        0x0D, 0x0A, 0x1A, 0x0A
                    }),
            ".webp" => bytesRead >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
