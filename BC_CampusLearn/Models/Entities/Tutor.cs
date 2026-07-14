namespace BC_CampusLearn.Models.Entities;

public class Tutor
{
    public int TutorId { get; set; }

    // These remain nullable until tutors are linked to Entra accounts.
    public string? EntraObjectId { get; set; }

    public string? EntraTenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Biography { get; set; } = string.Empty;

    public string? ProfileImagePath { get; set; }

    public bool IsApproved { get; set; }

    public bool IsActive { get; set; }

    //links for profile
    public string? LinkedInUrl { get; set; }   //can be null

    public string? GitHubUrl { get; set; }     //can be null


    // Creates relationships with TutorCourseModule and TutorAvailability entities.
    public ICollection<TutorCourseModule> TutorCourseModules { get; set; }
        = new List<TutorCourseModule>();

    public ICollection<TutorAvailability> AvailabilitySlots { get; set; }
        = new List<TutorAvailability>();

    public ICollection<Booking> Bookings { get; set; }
        = new List<Booking>();
}