using BC_CampusLearn.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public class CampusEventInput
{
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Disclaimer { get; set; }

    [Required, StringLength(300)]
    public string Location { get; set; } = string.Empty;

    [Required]
    public DateTime StartsAt { get; set; }

    public DateTime? PublishAt { get; set; }

    public bool IsPublished { get; set; } = true;

    public List<CampusEventDetailInput> CustomFields { get; set; } = [];
}

public class CampusEventDetailInput
{
    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    public CampusEventDetailDataType DataType { get; set; }

    [Required, StringLength(2000)]
    public string Value { get; set; } = string.Empty;
}
