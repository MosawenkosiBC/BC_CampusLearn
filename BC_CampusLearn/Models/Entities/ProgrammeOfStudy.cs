namespace BC_CampusLearn.Models.Entities;

public class ProgrammeOfStudy
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<ProgrammeModule> ProgrammeModules { get; set; }
        = new List<ProgrammeModule>();
}