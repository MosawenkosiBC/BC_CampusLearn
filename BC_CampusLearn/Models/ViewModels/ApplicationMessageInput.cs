using System.ComponentModel.DataAnnotations;

namespace BC_CampusLearn.Models.ViewModels;

public sealed class ApplicationMessageInput
{
    [Required(ErrorMessage = "Enter an email subject.")]
    [StringLength(200, ErrorMessage = "The email subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the email message.")]
    [StringLength(5000, ErrorMessage = "The email message cannot exceed 5,000 characters.")]
    public string EmailBody { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "The notification message cannot exceed 1,000 characters.")]
    public string NotificationMessage { get; set; } = string.Empty;
}
