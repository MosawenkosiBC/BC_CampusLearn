namespace BC_CampusLearn.Models.Entities;

public class TutorCourseModule
{
    public int TutorId { get; set; }  //foreign key to the Tutor entity.

    public Tutor Tutor { get; set; } = null!;

    public int CourseModuleId { get; set; }  

    public CourseModule CourseModule { get; set; } = null!;
}