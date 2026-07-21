namespace BC_CampusLearn.Models.Entities
{
    public class ProgrammeOfStudy
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Navigation property.
        public ICollection<ProgrammeModule> Modules { get; set; }
            = new List<ProgrammeModule>();
    }
}

