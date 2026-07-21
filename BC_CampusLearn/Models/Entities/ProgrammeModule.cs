namespace BC_CampusLearn.Models.Entities
{
    public class ProgrammeModule
    {
        public int ProgrammeId { get; set; }

        public string ModuleName { get; set; } = string.Empty;

        public string ModuleCode { get; set; } = string.Empty;

        public int YearOfStudy { get; set; }

        // Navigation property.
        public ProgrammeOfStudy Programme { get; set; } = null!;
    }
}
