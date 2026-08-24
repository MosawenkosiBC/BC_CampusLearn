using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BC_CampusLearn.Models.ViewModels;

public class LearningResourceInput
{
    public int? LearningResourceId { get; set; }

    [Required(ErrorMessage = "Select a module.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a module.")]
    public int ProgrammeModuleId { get; set; }

    [Required(ErrorMessage = "Enter a resource title or topic.")]
    [StringLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Required(ErrorMessage = "Add the learning content.")]
    [StringLength(500000, ErrorMessage = "The learning content is too large.")]
    public string Content { get; set; } = string.Empty;

    public bool AllowSubscriberComments { get; set; } = true;

    [Url(ErrorMessage = "Enter a valid URL, including https://.")]
    [StringLength(1000)]
    public string? Link1 { get; set; }

    [Url(ErrorMessage = "Enter a valid URL, including https://.")]
    [StringLength(1000)]
    public string? Link2 { get; set; }

    public List<IFormFile>? Documents { get; set; }
}
