using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace BC_CampusLearn.Pages.Tutors;

public class DocumentsModel : PageModel
{
    private const int MaximumSignatureBytes = 500_000;
    private const string PngDataUrlPrefix = "data:image/png;base64,";
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITutorPersonalInformationPdf _pdf;

    public DocumentsModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITutorPersonalInformationPdf pdf)
    {
        _context = context;
        _currentUserService = currentUserService;
        _pdf = pdf;
    }

    [BindProperty]
    public PersonalInformationInput Input { get; set; } = new();

    public bool HasSavedInformation { get; private set; }
    public bool HasCompletedInformation { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Tutor? tutor = await CurrentTutorQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (tutor is null) return Forbid();

        PopulateInput(tutor);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        RemoveCompletionValidation();
        ValidateDates(out DateOnly birthDate, out DateOnly? workPermitExpiryDate);
        Tutor? tutor = await CurrentTutorQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (tutor is null) return Forbid();

        TryResolveSignature(tutor, required: false, out byte[]? signatureImage);

        if (!ModelState.IsValid)
        {
            SetStatus(tutor);
            return Page();
        }

        SaveInput(tutor, sign: false, birthDate, workPermitExpiryDate, signatureImage);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Your personal information has been saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCompleteAsync(CancellationToken cancellationToken)
    {
        ValidateDates(out DateOnly birthDate, out DateOnly? workPermitExpiryDate);
        if (string.IsNullOrWhiteSpace(Input.SignatureName))
        {
            ModelState.AddModelError(
                "Input.SignatureName",
                "Type your full name to sign the form.");
        }
        if (!Input.SignatureConfirmed)
        {
            ModelState.AddModelError(
                "Input.SignatureConfirmed",
                "Confirm that the typed name is your electronic signature.");
        }

        Tutor? tutor = await CurrentTutorQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (tutor is null) return Forbid();

        TryResolveSignature(tutor, required: true, out byte[]? signatureImage);

        if (!ModelState.IsValid)
        {
            SetStatus(tutor);
            return Page();
        }

        SaveInput(tutor, sign: true, birthDate, workPermitExpiryDate, signatureImage);
        await _context.SaveChangesAsync(cancellationToken);

        TutorPersonalInformation info = tutor.PersonalInformation!;
        TutorBankingDetails banking = tutor.BankingDetails!;
        byte[] document = _pdf.Create(new TutorPersonalInformationPdfData(
            info,
            banking,
            tutor.BcUser.Email!,
            tutor.PhoneNumber!));
        string fileName = TutorPersonalInformationPdf.BuildFileName(
            tutor.BcUser.PersonnelNumber,
            tutor.BcUser.DisplayName);
        ContentDispositionHeaderValue disposition = new("inline")
        {
            FileNameStar = fileName
        };
        Response.Headers.ContentDisposition = disposition.ToString();
        return File(document, MediaTypeNames.Application.Pdf);
    }

    private IQueryable<Tutor> CurrentTutorQuery()
    {
        CurrentUser currentUser = _currentUserService.GetRequiredUser();
        return _context.Tutors
            .Include(tutor => tutor.BcUser)
            .Include(tutor => tutor.PersonalInformation)
            .Include(tutor => tutor.BankingDetails)
            .Where(tutor =>
                tutor.BcUserId == currentUser.BcUserId &&
                tutor.Status == TutorStatus.Approved &&
                tutor.IsActive);
    }

    private void PopulateInput(Tutor tutor)
    {
        TutorPersonalInformation? info = tutor.PersonalInformation;
        TutorBankingDetails? banking = tutor.BankingDetails;
        string[] names = tutor.BcUser.DisplayName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        Input = new PersonalInformationInput
        {
            Surname = info?.Surname ?? names.LastOrDefault(),
            FullNames = info?.FullNames ??
                (names.Length > 1 ? string.Join(' ', names[..^1]) : names.FirstOrDefault()),
            StreetAddress = info?.StreetAddress,
            Suburb = info?.Suburb,
            City = info?.City,
            Province = info?.Province,
            Country = info?.Country ?? "South Africa",
            PostalCode = info?.PostalCode,
            IncomeTaxNumber = info?.IncomeTaxNumber,
            HomeLanguage = info?.HomeLanguage,
            CellNumber = tutor.PhoneNumber,
            Gender = info?.Gender,
            Nationality = info?.Nationality,
            IdentityOrPassportNumber = info?.IdentityOrPassportNumber,
            WorkPermitExpiryDate = info?.WorkPermitExpiryDate?.ToString("dd/MM/yyyy"),
            AsylumNumber = info?.AsylumNumber,
            BirthDate = info?.BirthDate.ToString("dd/MM/yyyy"),
            EmailAddress = tutor.BcUser.Email,
            EmergencyContactName = info?.EmergencyContactName,
            EmergencyContactPrimaryPhone = info?.EmergencyContactPrimaryPhone,
            EmergencyContactRelationship = info?.EmergencyContactRelationship,
            BankName = banking?.BankName,
            AccountNumber = banking?.RecipientAccountNumber,
            AccountHolderName = banking?.AccountHolder,
            AccountType = banking?.AccountType,
            SignatureName = info?.SignatureName,
            SignatureDataUrl = info?.SignatureImage is { Length: > 0 } signature
                ? $"{PngDataUrlPrefix}{Convert.ToBase64String(signature)}"
                : null
        };
        SetStatus(tutor);
    }

    private void SaveInput(
        Tutor tutor,
        bool sign,
        DateOnly birthDate,
        DateOnly? workPermitExpiryDate,
        byte[]? signatureImage)
    {
        TutorPersonalInformation info = tutor.PersonalInformation ?? new()
        {
            TutorId = tutor.TutorId
        };
        if (tutor.PersonalInformation is null)
        {
            tutor.PersonalInformation = info;
            _context.TutorPersonalInformation.Add(info);
        }

        TutorBankingDetails banking = tutor.BankingDetails ?? new()
        {
            TutorId = tutor.TutorId,
            BranchCode = string.Empty
        };
        if (tutor.BankingDetails is null)
        {
            tutor.BankingDetails = banking;
            _context.TutorBankingDetails.Add(banking);
        }

        info.Surname = Input.Surname!.Trim();
        info.FullNames = Input.FullNames!.Trim();
        info.PhysicalHomeAddress = BuildPhysicalHomeAddress();
        info.StreetAddress = Input.StreetAddress!.Trim();
        info.Suburb = Input.Suburb!.Trim();
        info.City = Input.City!.Trim();
        info.Province = Input.Province!.Trim();
        info.Country = Input.Country!.Trim();
        info.PostalCode = Input.PostalCode!.Trim();
        info.IncomeTaxNumber = Input.IncomeTaxNumber!.Trim();
        info.HomeLanguage = Input.HomeLanguage!.Trim();
        info.Gender = Input.Gender!.Trim();
        info.Nationality = Input.Nationality!.Trim();
        info.IdentityOrPassportNumber = Input.IdentityOrPassportNumber!.Trim();
        info.WorkPermitExpiryDate = workPermitExpiryDate;
        info.AsylumNumber = Normalize(Input.AsylumNumber);
        info.BirthDate = birthDate;
        info.EmergencyContactName = Input.EmergencyContactName!.Trim();
        info.EmergencyContactPrimaryPhone = Input.EmergencyContactPrimaryPhone!.Trim();
        info.EmergencyContactRelationship = Input.EmergencyContactRelationship!.Trim();
        info.SignatureName = Normalize(Input.SignatureName);
        info.SignatureImage = signatureImage;
        info.UpdatedAt = DateTimeOffset.UtcNow;
        if (sign)
        {
            info.SignedAt = DateTimeOffset.UtcNow;
        }

        tutor.PhoneNumber = Input.CellNumber!.Trim();
        tutor.BcUser.Email = Input.EmailAddress!.Trim();
        tutor.UpdatedAt = DateTime.UtcNow;
        banking.BankName = Input.BankName!.Trim();
        banking.RecipientAccountNumber = Input.AccountNumber!.Trim();
        banking.AccountHolder = Input.AccountHolderName!.Trim();
        banking.AccountType = Input.AccountType!.Trim();
    }

    private string BuildPhysicalHomeAddress()
    {
        string address = string.Join(
            Environment.NewLine,
            new[]
            {
                Input.StreetAddress!.Trim(),
                Input.Suburb!.Trim(),
                $"{Input.City!.Trim()}, {Input.Province!.Trim()}",
                $"{Input.Country!.Trim()}, {Input.PostalCode!.Trim()}"
            });
        return address.Length <= 500 ? address : address[..500];
    }

    private bool TryResolveSignature(
        Tutor tutor,
        bool required,
        out byte[]? signatureImage)
    {
        signatureImage = Input.SignatureCleared
            ? null
            : tutor.PersonalInformation?.SignatureImage;
        string? value = Normalize(Input.SignatureDataUrl);
        if (value is not null)
        {
            if (!value.StartsWith(PngDataUrlPrefix, StringComparison.Ordinal))
            {
                ModelState.AddModelError(
                    "Input.SignatureDataUrl",
                    "Draw the signature again using the signature pad.");
                return false;
            }

            try
            {
                byte[] candidate = Convert.FromBase64String(value[PngDataUrlPrefix.Length..]);
                if (candidate.Length == 0 ||
                    candidate.Length > MaximumSignatureBytes ||
                    !candidate.AsSpan().StartsWith(PngSignature))
                {
                    throw new FormatException();
                }
                signatureImage = candidate;
            }
            catch (FormatException)
            {
                ModelState.AddModelError(
                    "Input.SignatureDataUrl",
                    "Draw the signature again using the signature pad.");
                return false;
            }
        }

        if (required && signatureImage is null)
        {
            ModelState.AddModelError(
                "Input.SignatureDataUrl",
                "Draw your electronic signature before previewing the PDF.");
            return false;
        }
        return true;
    }

    private void SetStatus(Tutor tutor)
    {
        HasSavedInformation = tutor.PersonalInformation is not null;
        HasCompletedInformation = tutor.PersonalInformation?.SignedAt is not null;
    }

    private void RemoveCompletionValidation()
    {
        ModelState.Remove("Input.SignatureName");
        ModelState.Remove("Input.SignatureConfirmed");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ValidateDates(
        out DateOnly birthDate,
        out DateOnly? workPermitExpiryDate)
    {
        const string format = "dd/MM/yyyy";
        if (!DateOnly.TryParseExact(
            Input.BirthDate,
            format,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out birthDate))
        {
            ModelState.AddModelError(
                "Input.BirthDate",
                "Enter the birth date as dd/mm/yyyy.");
        }

        workPermitExpiryDate = null;
        if (!string.IsNullOrWhiteSpace(Input.WorkPermitExpiryDate))
        {
            if (DateOnly.TryParseExact(
                Input.WorkPermitExpiryDate,
                format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateOnly parsedWorkPermitDate))
            {
                workPermitExpiryDate = parsedWorkPermitDate;
            }
            else
            {
                ModelState.AddModelError(
                    "Input.WorkPermitExpiryDate",
                    "Enter the work permit expiry date as dd/mm/yyyy.");
            }
        }
    }

    public sealed class PersonalInformationInput
    {
        [Required, StringLength(100)]
        public string? Surname { get; set; }

        [Required, StringLength(200), Display(Name = "Full names")]
        public string? FullNames { get; set; }

        [Required, StringLength(200), Display(Name = "Street address")]
        public string? StreetAddress { get; set; }

        [Required, StringLength(100)]
        public string? Suburb { get; set; }

        [Required, StringLength(100), Display(Name = "City / town")]
        public string? City { get; set; }

        [Required, StringLength(100)]
        public string? Province { get; set; }

        [Required, StringLength(100)]
        public string? Country { get; set; }

        [Required, StringLength(20), Display(Name = "Postal code")]
        public string? PostalCode { get; set; }

        [Required, StringLength(50), Display(Name = "Income tax number")]
        public string? IncomeTaxNumber { get; set; }

        [Required, StringLength(100), Display(Name = "Home language")]
        public string? HomeLanguage { get; set; }

        [Required, Phone, StringLength(32), Display(Name = "Cell number")]
        public string? CellNumber { get; set; }

        [Required, StringLength(50)]
        public string? Gender { get; set; }

        [Required, StringLength(100)]
        public string? Nationality { get; set; }

        [Required, StringLength(100), Display(Name = "Passport / ID number")]
        public string? IdentityOrPassportNumber { get; set; }

        [StringLength(10), Display(Name = "Work permit expiry date")]
        public string? WorkPermitExpiryDate { get; set; }

        [StringLength(100), Display(Name = "Asylum number")]
        public string? AsylumNumber { get; set; }

        [Required, StringLength(10), Display(Name = "Birth date")]
        public string? BirthDate { get; set; }

        [Required, EmailAddress, StringLength(254), Display(Name = "Email address")]
        public string? EmailAddress { get; set; }

        [Required, StringLength(200), Display(Name = "Contact name")]
        public string? EmergencyContactName { get; set; }

        [Required, Phone, StringLength(32), Display(Name = "Primary phone")]
        public string? EmergencyContactPrimaryPhone { get; set; }

        [Required, StringLength(100)]
        public string? EmergencyContactRelationship { get; set; }

        [Required, StringLength(150), Display(Name = "Bank name")]
        public string? BankName { get; set; }

        [Required, StringLength(50), Display(Name = "Account number")]
        public string? AccountNumber { get; set; }

        [Required, StringLength(200), Display(Name = "Account holder name")]
        public string? AccountHolderName { get; set; }

        [Required, StringLength(50), Display(Name = "Type of account")]
        public string? AccountType { get; set; }

        [StringLength(200), Display(Name = "Electronic signature")]
        public string? SignatureName { get; set; }

        public string? SignatureDataUrl { get; set; }

        public bool SignatureCleared { get; set; }

        public bool SignatureConfirmed { get; set; }
    }
}
