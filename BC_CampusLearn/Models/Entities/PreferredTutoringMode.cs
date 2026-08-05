using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.Entities;

public enum PreferredTutoringMode
{
    [Display(Name = "Face-To-Face")]
    FaceToFace = 1,

    Online = 2,

    Both = 3
}
