namespace BC_CampusLearn.Models.Entities;

public class Tutor
{
    public int TutorId { get; set; }
    public int BcUserId { get; set; }
    public int ProgrammeId { get; set; }
    public decimal OverallAverage { get; set; }
    public int YearOfStudy { get; set; }
    public string ReasonForTutoring { get; set; } = null!;
    public string TeachingStyle { get; set; } = null!;
    public string PreviousTutoringExperience { get; set; } = null!;
    public PreferredTutoringMode PreferredTutoringMode { get; set; }
    public string CampusOfStudy { get; set; } = null!;
    public string DemonstrationVideoUrl { get; set; } = null!;
    public TutorStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string? Biography { get; set; }

    public string? ProfileImagePath { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    //links for profile
    public string? LinkedInUrl { get; set; }   //can be null

    public string? GitHubUrl { get; set; }     //can be null

    public BcUser BcUser { get; set; } = null!;
    public ProgrammeOfStudy Programme { get; set; } = null!;
    public ICollection<TutorDocument> TutorDocuments { get; set; } = new List<TutorDocument>();


    // Creates relationships with TutorCourseModule and TutorAvailability entities.
    public ICollection<TutorCourseModule> TutorCourseModules { get; set; }
        = new List<TutorCourseModule>();

    public ICollection<TutorAvailability> TutorAvailabilities { get; set; }
        = new List<TutorAvailability>();

    public ICollection<TutorModuleChangeRequest> ModuleChangeRequests { get; set; }
        = new List<TutorModuleChangeRequest>();

    public ICollection<TutorDeregistrationRequest> DeregistrationRequests { get; set; }
        = new List<TutorDeregistrationRequest>();

}
