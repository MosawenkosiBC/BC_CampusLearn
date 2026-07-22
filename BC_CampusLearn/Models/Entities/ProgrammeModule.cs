namespace BC_CampusLearn.Models.Entities;

public class ProgrammeModule
{
    public int ProgrammeModuleId { get; set; }

    public int ProgrammeId { get; set; }

    public string ModuleName { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public int YearOfStudy { get; set; }

    public ProgrammeOfStudy Programme { get; set; } = null!;

    public ICollection<TutorCourseModule> TutorCourseModules { get; set; }
        = new List<TutorCourseModule>();

    public ICollection<TutorAvailability> TutorAvailabilities { get; set; }
        = new List<TutorAvailability>();
}