namespace BC_CampusLearn.Models.Entities;

public class TutorCourseModule
{
    public int TutorId { get; set; }  //foreign key to the Tutor entity.

    public Tutor Tutor { get; set; } = null!;

    public int ProgrammeModuleId { get; set; }

    public ProgrammeModule ProgrammeModule { get; set; } = null!;

    public ICollection<Booking> Bookings { get; set; }
        = new List<Booking>();
}
