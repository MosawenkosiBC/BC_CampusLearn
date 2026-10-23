namespace BC_CampusLearn.Models.Entities;

public class SeniorTutorApplicationModule
{
    public int SeniorTutorApplicationId { get; set; }

    public int ProgrammeModuleId { get; set; }

    public SeniorTutorApplication SeniorTutorApplication { get; set; } = null!;

    public ProgrammeModule ProgrammeModule { get; set; } = null!;
}
