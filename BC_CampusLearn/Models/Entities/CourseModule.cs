namespace BC_CampusLearn.Models.Entities;

public class CourseModule
{
    public int CourseModuleId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<TutorCourseModule> TutorCourseModules { get; set; }
        = new List<TutorCourseModule>();

    public ICollection<TutorAvailability> AvailabilitySlots { get; set; }
        = new List<TutorAvailability>();
}
