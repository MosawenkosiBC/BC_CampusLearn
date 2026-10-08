using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Students;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator;

// Shared manual tutor workflow for the placement and tutor directory pages.
public abstract class ManualTutorPageModel(ApplicationDbContext context, IStudentDetailsService? studentDetailsService) : PageModel
{
    private readonly ApplicationDbContext _context = context;
    protected ApplicationDbContext Context => _context;

    protected virtual IActionResult RedirectAfterAddTutor() => RedirectToPage();

    public bool RequiresStudentVerification => true;
    private StudentDetails? _verifiedStudent;

    private static readonly Expression<Func<BcUser, bool>> EligibleStudent = user =>
        user.Role == BcUserRole.Student &&
        (user.Tutor == null || (!user.Tutor.IsActive &&
            (user.Tutor.Status == TutorStatus.Approved ||
             user.Tutor.Status == TutorStatus.Suspended ||
             user.Tutor.Status == TutorStatus.Deregistered)));
    private static readonly Func<BcUser, bool> IsEligibleStudent = EligibleStudent.Compile();

    private bool IsManualTutorJsonRequest => HttpContext?.Request.Headers.Accept
        .Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true) == true;

    private IActionResult ManualTutorFailure(string message, string field)
        => ManualTutorFailure(message, new Dictionary<string, string>
        {
            [$"ManualTutor.{field}"] = message
        });

    private IActionResult ManualTutorFailure(string message, Dictionary<string, string> errors)
    {
        if (IsManualTutorJsonRequest)
            return new JsonResult(new { errors }) { StatusCode = 400 };

        PageError = message;
        return RedirectAfterAddTutor();
    }

    public async Task<IActionResult> OnGetStudentDetailsAsync(int studentId, CancellationToken cancellationToken)
    {
        var student = await Context.BcUsers.AsNoTracking().Where(EligibleStudent).SingleOrDefaultAsync(
            user => user.BcUserId == studentId,
            cancellationToken);
        if (student is null)
            return new JsonResult(new { error = "Select an eligible student." }) { StatusCode = 400 };

        var (details, programmeId, error) = await LookupStudentAsync(student, cancellationToken);
        if (error is not null)
            return new JsonResult(new { error }) { StatusCode = 400 };

        return new JsonResult(new
        {
            studentId, displayName = DisplayName(details!), details!.StudentNumber,
            details.Email, programmeId, details.YearOfStudy, campus = details.Campus
        });
    }

    protected async Task<string?> VerifyManualTutorAsync(BcUser student, CancellationToken cancellationToken)
    {
        _verifiedStudent = null;
        var (details, programmeId, error) = await LookupStudentAsync(student, cancellationToken);
        if (error is not null) return error;
        _verifiedStudent = details;
        ManualTutor.ProgrammeId = programmeId;
        ManualTutor.YearOfStudy = details!.YearOfStudy;
        ManualTutor.CampusOfStudy = details.Campus;
        return null;
    }

    protected void ApplyVerifiedStudentIdentity(BcUser student)
    {
        student.DisplayName = DisplayName(_verifiedStudent!);
        student.Email = _verifiedStudent!.Email;
    }

    private static string DisplayName(StudentDetails details) =>
        $"{(string.IsNullOrWhiteSpace(details.PreferredName) ? details.FirstName : details.PreferredName)} {details.Surname}";

    private async Task<(StudentDetails? Details, int ProgrammeId, string? Error)> LookupStudentAsync(
        BcUser student, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(student.PersonnelNumber))
            return (null, 0, "The student's student number is missing. Please contact Student Support.");
        var result = studentDetailsService is null
            ? new StudentDetailsResult(StudentDetailsStatus.Unavailable)
            : await studentDetailsService.GetAsync(student.PersonnelNumber, cancellationToken);
        if (result is not { Status: StudentDetailsStatus.Success, Details: not null })
            return (null, 0, result.Status switch
            {
                StudentDetailsStatus.NotFound => "Student information could not be found. Select another student or contact Student Support.",
                StudentDetailsStatus.InvalidResponse => "Student information could not be verified. Please contact Student Support.",
                _ => "Student information is temporarily unavailable. Please try again."
            });

        var details = result.Details;
        if (!string.Equals(details.StudentNumber.Trim(), student.PersonnelNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            return (null, 0, "Student information could not be verified. Please contact Student Support.");
        var programmes = await Context.ProgrammesOfStudy.AsNoTracking().ToListAsync(cancellationToken);
        var programme = programmes.SingleOrDefault(item =>
            string.Equals(item.Name.Trim(), details.Programme.Trim(), StringComparison.OrdinalIgnoreCase));
        if (programme is null)
            return (null, 0, "The student's registered programme is not supported. Please contact Student Support.");
        if (details.YearOfStudy is < 1 or > 4)
            return (null, 0, "The student's year of study must be between 1 and 4. Please contact Student Support.");
        if (string.IsNullOrWhiteSpace(details.Campus) || details.Campus.Trim().Length > 100)
            return (null, 0, "The student's campus information could not be verified. Please contact Student Support.");
        details = details with { Campus = details.Campus.Trim() };
        return (details, programme.Id, null);
    }

    public static readonly IReadOnlyList<string> ManualTutorCampuses =
    [
        "Pretoria Campus",
        "Kempton Park Campus",
        "Stellenbosch Campus",
        "Online"
    ];
    [TempData]
    public string? PageMessage { get; set; }

    [TempData]
    public string? PageError { get; set; }

    public IReadOnlyList<SelectListItem> StudentOptions { get; private set; }
        = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ProgrammeOptions { get; private set; }
        = Array.Empty<SelectListItem>();
    public IReadOnlyList<ManualModuleOption> ManualTutorModuleOptions
    { get; private set; } = Array.Empty<ManualModuleOption>();

    [BindProperty]
    public ManualTutorInput ManualTutor { get; set; } = new();

    public async Task<IActionResult> OnPostAddTutorAsync(
        CancellationToken cancellationToken)
    {
        // This page contains several independent forms. Validate only the
        // manual tutor input so application/interview fields cannot block it.
        ModelState.Clear();
        BcUser? student = await _context.BcUsers
            .Include(user => user.Tutor)
                .ThenInclude(tutor => tutor!.TutorCourseModules)
            .SingleOrDefaultAsync(
                user => user.BcUserId == ManualTutor.BcUserId,
                cancellationToken);

        if (student is null)
        {
            return ManualTutorFailure("Select an eligible student.", nameof(ManualTutorInput.BcUserId));
        }

        if (student.Tutor is not null && !IsEligibleStudent(student))
        {
            return ManualTutorFailure("This student already has a tutor profile.", nameof(ManualTutorInput.BcUserId));
        }

        if (student.Role != BcUserRole.Student)
        {
            return ManualTutorFailure("Only students can be added as tutors.", nameof(ManualTutorInput.BcUserId));
        }

        string? verificationError = await VerifyManualTutorAsync(student, cancellationToken);
        if (verificationError is not null)
        {
            return ManualTutorFailure(verificationError, nameof(ManualTutorInput.BcUserId));
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(ManualTutor);
        validationContext.Items[nameof(RequiresStudentVerification)] = RequiresStudentVerification;
        if (!Validator.TryValidateObject(ManualTutor, validationContext,
                validationResults, validateAllProperties: true))
        {
            string message = validationResults.FirstOrDefault(result =>
                result.MemberNames.Contains(nameof(ManualTutorInput.OverallAverage)))?.ErrorMessage
                ?? "Enter valid details for the tutor you want to add.";
            var errors = validationResults
                .SelectMany(result => result.MemberNames.Select(member => new
                {
                    Field = $"ManualTutor.{member}",
                    Message = result.ErrorMessage ?? message
                }))
                .GroupBy(error => error.Field)
                .ToDictionary(group => group.Key, group => group.First().Message);
            return ManualTutorFailure(message, errors);
        }

        bool programmeExists = await _context.ProgrammesOfStudy.AnyAsync(
            programme => programme.Id == ManualTutor.ProgrammeId, cancellationToken);
        if (!programmeExists)
        {
            return ManualTutorFailure("Select a valid programme.", nameof(ManualTutorInput.ProgrammeId));
        }

        List<int> moduleIds = ManualTutor.ProgrammeModuleIds
            .Distinct()
            .ToList();
        if (moduleIds.Count == 0)
        {
            return ManualTutorFailure("Select at least one module for the tutor.", nameof(ManualTutorInput.ProgrammeModuleIds));
        }

        int eligibleModuleCount = await _context.ProgrammeModules
            .AsNoTracking()
            .CountAsync(
                module => moduleIds.Contains(module.ProgrammeModuleId) &&
                    module.ProgrammeId == ManualTutor.ProgrammeId &&
                    module.YearOfStudy <= ManualTutor.YearOfStudy,
                cancellationToken);
        if (eligibleModuleCount != moduleIds.Count)
        {
            return ManualTutorFailure("Select modules from the chosen programme that are eligible for the tutor's year of study.",
                nameof(ManualTutorInput.ProgrammeModuleIds));
        }

        DateTime addedAt = DateTime.UtcNow;
        ApplyVerifiedStudentIdentity(student);
        student.Role = BcUserRole.Tutor;
        // Reuse former tutors so their bookings, documents and audit history stay linked.
        var tutor = student.Tutor ?? new Tutor
        {
            ReasonForTutoring = "",
            TeachingStyle = "",
            PreviousTutoringExperience = "",
            DemonstrationVideoUrl = "",
            SubmittedAt = addedAt,
            CreatedAt = addedAt
        };
        tutor.ProgrammeId = ManualTutor.ProgrammeId;
        tutor.OverallAverage = ManualTutor.OverallAverage!.Value;
        tutor.YearOfStudy = ManualTutor.YearOfStudy;
        tutor.PhoneNumber = string.IsNullOrWhiteSpace(ManualTutor.PhoneNumber)
                ? null
                : ManualTutor.PhoneNumber.Trim();
        tutor.CampusOfStudy = ManualTutor.CampusOfStudy.Trim();
        tutor.PreferredTutoringMode = ManualTutor.PreferredTutoringMode;
        tutor.Status = TutorStatus.Approved;
        tutor.ApplicationStage = TutorApplicationStage.Placement;
        tutor.IsActive = true;
        tutor.ReviewedAt = addedAt;
        tutor.UpdatedAt = addedAt;

        foreach (var assignment in tutor.TutorCourseModules)
            assignment.IsActive = moduleIds.Contains(assignment.ProgrammeModuleId);

        foreach (int moduleId in moduleIds)
        {
            if (!tutor.TutorCourseModules.Any(assignment => assignment.ProgrammeModuleId == moduleId))
            {
                tutor.TutorCourseModules.Add(new TutorCourseModule
                {
                    ProgrammeModuleId = moduleId
                });
            }
        }
        student.Tutor = tutor;

        await _context.SaveChangesAsync(cancellationToken);
        PageMessage = $"{student.DisplayName} was added as a tutor.";
        if (IsManualTutorJsonRequest)
            return new JsonResult(new { succeeded = true });
        return RedirectAfterAddTutor();
    }

    protected async Task LoadManualTutorOptionsAsync(
        CancellationToken cancellationToken)
    {
        StudentOptions = await _context.BcUsers
            .AsNoTracking()
            .Where(EligibleStudent)
            .OrderBy(user => user.DisplayName)
            .Select(user => new SelectListItem
            {
                Value = user.BcUserId.ToString(),
                Text = user.DisplayName + " (" + user.PersonnelNumber + ")"
            })
            .ToListAsync(cancellationToken);

        ProgrammeOptions = await _context.ProgrammesOfStudy
            .AsNoTracking()
            .OrderBy(programme => programme.Name)
            .Select(programme => new SelectListItem
            {
                Value = programme.Id.ToString(),
                Text = programme.Name
            })
            .ToListAsync(cancellationToken);

        ManualTutorModuleOptions = await _context.ProgrammeModules
            .AsNoTracking()
            .OrderBy(module => module.Programme.Name)
            .ThenBy(module => module.YearOfStudy)
            .ThenBy(module => module.ModuleCode)
            .Select(module => new ManualModuleOption
            {
                ProgrammeModuleId = module.ProgrammeModuleId,
                ProgrammeId = module.ProgrammeId,
                YearOfStudy = module.YearOfStudy,
                Code = module.ModuleCode,
                Name = module.ModuleName
            })
            .ToListAsync(cancellationToken);
    }

    public sealed class ManualTutorInput : IValidatableObject
    {
        [Range(1, int.MaxValue)]
        public int BcUserId { get; set; }

        [Range(1, int.MaxValue)]
        public int ProgrammeId { get; set; }

        [Range(1, 4)]
        public int YearOfStudy { get; set; }

        [Required(ErrorMessage = "Enter the tutor's overall average.")]
        [Range(typeof(decimal), "65", "100", MinimumIsExclusive = true,
            ErrorMessage = "The overall average must be greater than 65% and no more than 100%.")]
        public decimal? OverallAverage { get; set; }

        [Required, StringLength(100)]
        public string CampusOfStudy { get; set; } = string.Empty;

        [Phone, StringLength(32)]
        public string? PhoneNumber { get; set; }

        public PreferredTutoringMode PreferredTutoringMode { get; set; }
            = PreferredTutoringMode.Both;

        public List<int> ProgrammeModuleIds { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            // API-verified campus labels follow the student details API contract,
            // while the manual placement form still uses its fixed campus list.
            bool verifiedCampus = validationContext.Items.TryGetValue(
                nameof(RequiresStudentVerification), out var verified) && verified is true;
            if (!verifiedCampus && !ManualTutorCampuses.Contains(
                CampusOfStudy,
                StringComparer.Ordinal))
            {
                yield return new ValidationResult(
                    "Select a valid campus.",
                    [nameof(CampusOfStudy)]);
            }
        }
    }

    public sealed class ManualModuleOption
    {
        public int ProgrammeModuleId { get; init; }
        public int ProgrammeId { get; init; }
        public int YearOfStudy { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

}
