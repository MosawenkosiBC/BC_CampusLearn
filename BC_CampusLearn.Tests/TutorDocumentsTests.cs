using System.ComponentModel.DataAnnotations;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Tutors;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorDocumentsTests
{
    [Fact]
    public async Task GetPrefillsKnownTutorAndBankingInformation()
    {
        await using ApplicationDbContext context = CreateContext();
        Tutor tutor = SeedTutor(context);
        tutor.PhoneNumber = "0821234567";
        tutor.BankingDetails = new TutorBankingDetails
        {
            Tutor = tutor,
            AccountHolder = "Lebo Nkosi",
            RecipientAccountNumber = "1234567890",
            AccountType = "Cheque",
            BranchCode = "250655",
            BankName = "Example Bank"
        };
        await context.SaveChangesAsync();
        DocumentsModel page = CreatePage(context, tutor.BcUser, new FakePdf());

        Assert.IsType<PageResult>(await page.OnGetAsync(default));

        Assert.Equal("Lebo", page.Input.FullNames);
        Assert.Equal("Nkosi", page.Input.Surname);
        Assert.Equal("lebo@example.test", page.Input.EmailAddress);
        Assert.Equal("0821234567", page.Input.CellNumber);
        Assert.Equal("Example Bank", page.Input.BankName);
    }

    [Fact]
    public async Task CompletePersistsMissingTablesAndReturnsInlinePdf()
    {
        await using ApplicationDbContext context = CreateContext();
        Tutor tutor = SeedTutor(context);
        await context.SaveChangesAsync();
        FakePdf pdf = new();
        DocumentsModel page = CreatePage(context, tutor.BcUser, pdf);
        page.Input = CompleteInput();
        Validate(page, page.Input);

        FileContentResult result = Assert.IsType<FileContentResult>(
            await page.OnPostCompleteAsync(default));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("%PDF-test"u8.ToArray(), result.FileContents);
        Assert.Contains("inline", page.Response.Headers.ContentDisposition.ToString());
        Assert.Contains(
            "HR_Personal_Info_600001_Lebo_Nkosi.pdf",
            page.Response.Headers.ContentDisposition.ToString());
        TutorPersonalInformation saved = await context.TutorPersonalInformation.SingleAsync();
        Assert.Equal("Nkosi", saved.Surname);
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "12 Example Street",
                "Hatfield",
                "Pretoria, Gauteng",
                "South Africa, 0002"),
            saved.PhysicalHomeAddress);
        Assert.Equal("Lebo Nkosi", saved.SignatureName);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, saved.SignatureImage);
        Assert.NotNull(saved.SignedAt);
        TutorBankingDetails banking = await context.TutorBankingDetails.SingleAsync();
        Assert.Equal("Example Bank", banking.BankName);
        Assert.NotNull(pdf.Data);
        Assert.Same(saved, pdf.Data!.PersonalInformation);
    }

    [Fact]
    public async Task CompleteRequiresSignatureConfirmation()
    {
        await using ApplicationDbContext context = CreateContext();
        Tutor tutor = SeedTutor(context);
        await context.SaveChangesAsync();
        DocumentsModel page = CreatePage(context, tutor.BcUser, new FakePdf());
        page.Input = CompleteInput();
        page.Input.SignatureConfirmed = false;
        Validate(page, page.Input);

        Assert.IsType<PageResult>(await page.OnPostCompleteAsync(default));
        Assert.Contains(
            page.ModelState["Input.SignatureConfirmed"]!.Errors,
            error => error.ErrorMessage.Contains("electronic signature"));
        Assert.Empty(context.TutorPersonalInformation);
    }

    private static DocumentsModel.PersonalInformationInput CompleteInput() => new()
    {
        Surname = "Nkosi",
        FullNames = "Lebo",
        StreetAddress = "12 Example Street",
        Suburb = "Hatfield",
        City = "Pretoria",
        Province = "Gauteng",
        Country = "South Africa",
        PostalCode = "0002",
        IncomeTaxNumber = "1234567890",
        HomeLanguage = "Sepedi",
        CellNumber = "0821234567",
        Gender = "Female",
        Nationality = "South African",
        IdentityOrPassportNumber = "0101010000088",
        BirthDate = "01/01/2001",
        EmailAddress = "lebo@example.test",
        EmergencyContactName = "Mpho Nkosi",
        EmergencyContactPrimaryPhone = "0831234567",
        EmergencyContactRelationship = "Parent",
        BankName = "Example Bank",
        AccountNumber = "1234567890",
        AccountHolderName = "Lebo Nkosi",
        AccountType = "Cheque",
        SignatureName = "Lebo Nkosi",
        SignatureDataUrl = "data:image/png;base64,iVBORw0KGgo=",
        SignatureConfirmed = true
    };

    private static void Validate(
        PageModel page,
        DocumentsModel.PersonalInformationInput input)
    {
        ValidationContext validationContext = new(input);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(input, validationContext, results, true);
        foreach (ValidationResult validationResult in results)
        {
            foreach (string member in validationResult.MemberNames.DefaultIfEmpty(string.Empty))
            {
                page.ModelState.AddModelError(
                    string.IsNullOrEmpty(member) ? string.Empty : $"Input.{member}",
                    validationResult.ErrorMessage!);
            }
        }
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Tutor SeedTutor(ApplicationDbContext context)
    {
        BcUser user = new()
        {
            BcUserId = 1,
            PersonnelNumber = "600001",
            DisplayName = "Lebo Nkosi",
            Email = "lebo@example.test",
            Role = BcUserRole.Tutor
        };
        ProgrammeOfStudy programme = new() { Id = 1, Name = "Computing" };
        Tutor tutor = new()
        {
            TutorId = 1,
            BcUser = user,
            Programme = programme,
            OverallAverage = 75,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Style",
            PreviousTutoringExperience = "Experience",
            PreferredTutoringMode = PreferredTutoringMode.Both,
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = "https://example.test/video",
            Status = TutorStatus.Approved,
            ApplicationStage = TutorApplicationStage.Placement,
            IsActive = true
        };
        context.AddRange(user, programme, tutor);
        return tutor;
    }

    private static DocumentsModel CreatePage(
        ApplicationDbContext context,
        BcUser user,
        ITutorPersonalInformationPdf pdf)
    {
        DocumentsModel page = new(
            context,
            new TestCurrentUserService(new CurrentUser(
                user.BcUserId,
                user.PersonnelNumber,
                user.DisplayName,
                user.Email,
                BcUserRole.Tutor)),
            pdf);
        page.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return page;
    }

    private sealed class TestCurrentUserService(CurrentUser user)
        : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => user;
    }

    private sealed class FakePdf : ITutorPersonalInformationPdf
    {
        public TutorPersonalInformationPdfData? Data { get; private set; }

        public byte[] Create(TutorPersonalInformationPdfData data)
        {
            Data = data;
            return "%PDF-test"u8.ToArray();
        }
    }
}
