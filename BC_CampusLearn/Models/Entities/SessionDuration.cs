using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.Entities;

public enum SessionDuration
{
    [Display(Name = "1 hour")]
    OneHour = 1,

    [Display(Name = "2 hours")]
    TwoHours = 2
}
